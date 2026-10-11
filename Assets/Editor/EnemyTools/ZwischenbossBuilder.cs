using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut die Prefabs der drei Zwischenbosse aus Eiswelt, Geisterwald und
/// Vulkanwelt (Stracciatella, Knusperhaus, Ofenkartoffel) und traegt sie im
/// SpawnCatalog von GameCore ein.
///
/// Wie beim Milchpanzer: mehrere Bildstreifen, die das Skript selbst abspielt,
/// darum nicht die normale Werkstatt-Bauweise. Die Werkstatt leitet "Prefab
/// bauen" fuer sie hierher um (<see cref="SpecialBuilders"/>).
///
/// Wiederholbar: ein vorhandenes Prefab wird aktualisiert, nicht ersetzt.
/// </summary>
public static class ZwischenbossBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/miniboss/";
    private const string BossArtFolder = "Assets/Art/Gegner/new/boss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";

    /// <summary>Von hier kommt das unbeleuchtete Material der Leuchtebene (Geisterwald).</summary>
    private const string GlowTemplatePrefab = "Assets/Prefabs/Enemy/Gegner/Kuerbiswicht.prefab";

    private const string SortingLayerName = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    public static readonly EnemyId[] All = { EnemyId.Stracciatella, EnemyId.Knusperhaus, EnemyId.Ofenkartoffel };

    [MenuItem("Tools/Gegner/Zwischenbosse bauen (Eis, Geist, Vulkan)", false, 107)]
    public static void BuildFromMenu()
    {
        string result = string.Join("\n", All.Select(Build));
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register(All);
        EditorUtility.DisplayDialog("Zwischenbosse", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod ZwischenbossBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        foreach (EnemyId id in All) Debug.Log("[Zwischenbosse] " + Build(id));
        Debug.Log("[Zwischenbosse] " + Register(All));
    }

    public static bool Handles(EnemyId id) => System.Array.IndexOf(All, id) >= 0;

    /// <summary>
    /// Fehlt nach dem Kompilieren eines der Prefabs (Bilder liegen aber da),
    /// baut der Editor es selbst und traegt es in GameCore ein - damit die
    /// Zwischenbosse ohne Handgriff im Boss-Menue der Test-Szene und in den
    /// Wellenplaenen auftauchen. Ist alles da, passiert nichts.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void BuildMissingAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode) return;

            EnemyId[] missing = All.Where(id =>
            {
                EnemyDef def = EnemyCatalog.Get(id);
                return def != null
                    && AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) == null
                    && AssetDatabase.LoadAssetAtPath<Texture2D>(def.Sheet) != null;
            }).ToArray();
            if (missing.Length == 0) return;

            foreach (EnemyId id in missing) Debug.Log("[Zwischenbosse] " + Build(id));
            Debug.Log("[Zwischenbosse] " + RegisterInBackground(missing));
        };
    }

    /// <summary>
    /// Wie <see cref="Register"/>, aber ohne die offene Szene zu wechseln:
    /// GameCore wird dazugeladen, gespeichert und wieder geschlossen. Ist
    /// GameCore schon offen und hat ungespeicherte Aenderungen, wird nur
    /// eingetragen - speichern muss man dann selbst.
    /// </summary>
    private static string RegisterInBackground(EnemyId[] ids)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(CoreScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        bool wasDirty = wasLoaded && scene.isDirty;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);

        SpawnCatalog catalog = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            catalog = go.GetComponentInChildren<SpawnCatalog>(true);
            if (catalog != null) break;
        }

        string result;
        if (catalog == null)
        {
            result = "In " + CoreScenePath + " steckt kein SpawnCatalog.";
        }
        else
        {
            int n = 0;
            foreach (EnemyId id in ids)
            {
                EnemyDef def = EnemyCatalog.Get(id);
                GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
                if (asset == null) continue;
                catalog.EditorSet(id, asset);
                n++;
            }
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            result = n + " im SpawnCatalog (GameCore) eingetragen" + (wasDirty ? " - GameCore bitte speichern." : ".");
        }

        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        return result;
    }

    public static string Build(EnemyId id)
    {
        EnemyDef def = EnemyCatalog.Get(id);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer " + id + ".";

        string sheetName = System.IO.Path.GetFileNameWithoutExtension(def.Sheet);
        Sprite[] walk = Load(ArtFolder, sheetName);
        if (walk.Length == 0) return id + ": keine Bilder unter " + ArtFolder + sheetName + ".png.";

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));
        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject(id.ToString());
        string detail;

        try
        {
            root.name = id.ToString();
            root.tag = "Enemy";
            root.layer = EnemyLayer();
            root.transform.localScale = Vector3.one * def.Scale;

            Animator rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) Object.DestroyImmediate(rootAnimator, true);
            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) Object.DestroyImmediate(rootRenderer, true);

            SortingGroup group = GetOrAdd<SortingGroup>(root);
            group.sortingLayerName = SortingLayerName;
            group.sortingOrder = 1;

            SpriteRenderer bodyR = Child(root, "Bild", 1, TemplateMaterial());
            bodyR.sprite = walk[0];

            Rigidbody2D rb = GetOrAdd<Rigidbody2D>(root);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.mass = 40f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.None;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(id);
            enemy.EditorBind(bodyR, rb, enemy.EditorDestroyEffect != null ? enemy.EditorDestroyEffect : DefaultDestroyEffect());

            detail = BindBoss(id, root, bodyR, walk);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("{0}: Prefab {1} ({2}).", id, existed ? "aktualisiert" : "angelegt", detail);
    }

    private static string BindBoss(EnemyId id, GameObject root, SpriteRenderer bodyR, Sprite[] walk)
    {
        switch (id)
        {
            case EnemyId.Stracciatella:
            {
                Sprite[] count = Load(ArtFolder, "stracciatella_zaehlen");
                Sprite[] glare = Load(ArtFolder, "stracciatella_blick");
                Sprite[] eye = Load(ArtFolder, "stracciatella_auge");
                Sprite[] spark = Load(ArtFolder, "stracciatella_strahl");
                Sprite[] ice = Load(BossArtFolder, "eiskaiser_eisblock");
                GetOrAdd<EnemyStracciatella>(root).EditorBind(bodyR, walk, count, glare, eye, spark, ice);
                return string.Format("Gleiten {0}, Zaehlen {1}, Blick {2}, Auge {3}, Funken {4}, Eisblock {5}",
                                     walk.Length, count.Length, glare.Length, eye.Length, spark.Length, ice.Length);
            }
            case EnemyId.Knusperhaus:
            {
                Sprite[] call = Load(ArtFolder, "knusperhaus_ruf");
                Sprite[] step = Load(ArtFolder, "knusperhaus_spur");
                Sprite[] wisp = Load(ArtFolder, "knusperhaus_irrlicht");
                Sprite[] walkGlow = Load(ArtFolder, "knusperhaus_lauf_glow");
                Sprite[] callGlow = Load(ArtFolder, "knusperhaus_ruf_glow");
                Material unlit = GlowMaterial();

                GetOrAdd<EnemyKnusperhaus>(root).EditorBind(bodyR, walk, call, step, wisp, unlit);

                // Leuchtebene: Fenster, Rauch und Tuerlicht gluehen im dunklen Wald
                SpriteRenderer glowR = Child(root, "Leuchten", 2, unlit);
                glowR.sprite = walkGlow.Length > 0 ? walkGlow[0] : null;
                EnemyGlowLayer layer = GetOrAdd<EnemyGlowLayer>(root);
                var so = new SerializedObject(layer);
                so.FindProperty("body").objectReferenceValue = bodyR;
                so.FindProperty("glow").objectReferenceValue = glowR;
                SetArray(so.FindProperty("bodyFrames"), walk.Concat(call).ToArray());
                SetArray(so.FindProperty("glowFrames"), walkGlow.Concat(callGlow).ToArray());
                so.FindProperty("castLight").boolValue = true;
                so.FindProperty("lightColor").colorValue = new Color(1f, 0.78f, 0.45f);
                so.FindProperty("lightIntensity").floatValue = 0.9f;
                so.FindProperty("lightRadius").floatValue = 3.2f;
                so.FindProperty("lightOffset").vector2Value = new Vector2(0f, 1.6f);
                so.ApplyModifiedPropertiesWithoutUndo();
                return string.Format("Laufen {0}, Ruf {1}, Leuchten {2}+{3}, Spur {4}, Irrlicht {5}",
                                     walk.Length, call.Length, walkGlow.Length, callGlow.Length, step.Length, wisp.Length);
            }
            case EnemyId.Ofenkartoffel:
            {
                Sprite[] throwing = Load(ArtFolder, "ofenkartoffel_wurf");
                Sprite[] potato = Load(ArtFolder, "ofenkartoffel_kartoffel");
                Sprite[] boom = Load(ArtFolder, "ofenkartoffel_boom");
                GetOrAdd<EnemyOfenkartoffel>(root).EditorBind(bodyR, walk, throwing, potato, boom);
                return string.Format("Laufen {0}, Wurf {1}, Kartoffel {2}, Explosion {3}",
                                     walk.Length, throwing.Length, potato.Length, boom.Length);
            }
            default:
                return "kein Skript";
        }
    }

    /// <summary>Traegt die Prefabs im SpawnCatalog von GameCore ein. Oeffnet dafuer GameCore und danach wieder die vorige Szene.</summary>
    public static string Register(params EnemyId[] ids)
    {
        string before = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
        SpawnCatalog catalog = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            catalog = go.GetComponentInChildren<SpawnCatalog>(true);
            if (catalog != null) break;
        }

        string result;
        if (catalog == null)
        {
            result = "In " + CoreScenePath + " steckt kein SpawnCatalog.";
        }
        else
        {
            int n = 0;
            foreach (EnemyId id in ids)
            {
                EnemyDef def = EnemyCatalog.Get(id);
                GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
                if (asset == null) continue;
                catalog.EditorSet(id, asset);
                n++;
            }
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CoreScenePath);
            result = n + " im SpawnCatalog (GameCore) eingetragen.";
        }

        if (!Application.isBatchMode && !string.IsNullOrEmpty(before) && before != CoreScenePath)
        {
            EditorSceneManager.OpenScene(before, OpenSceneMode.Single);
        }
        return result;
    }

    // ------------------------------------------------------------- Kleinkram

    private static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static SpriteRenderer Child(GameObject root, string name, int order, Material material)
    {
        Transform t = root.transform.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
        }
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        t.gameObject.layer = root.layer;

        SpriteRenderer r = GetOrAdd<SpriteRenderer>(t.gameObject);
        r.sortingLayerName = SortingLayerName;
        r.sortingOrder = order;
        if (material != null) r.sharedMaterial = material;
        return r;
    }

    /// <summary>Alle Bilder eines Streifens, nach der Zahl am Namensende sortiert (_10 nach _9).</summary>
    private static Sprite[] Load(string folder, string sheet)
    {
        return AssetDatabase.LoadAllAssetsAtPath(folder + sheet + ".png")
                            .OfType<Sprite>()
                            .OrderBy(s => TrailingNumber(s.name))
                            .ToArray();
    }

    private static int TrailingNumber(string name)
    {
        int end = name.Length;
        while (end > 0 && char.IsDigit(name[end - 1])) end--;
        return end < name.Length && int.TryParse(name.Substring(end), out int v) ? v : int.MaxValue;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    private static int EnemyLayer()
    {
        foreach (string name in LayerCandidates)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;
        }
        return 0;
    }

    private static Material TemplateMaterial()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
        SpriteRenderer r = template != null ? template.GetComponent<SpriteRenderer>() : null;
        return r != null ? r.sharedMaterial : null;
    }

    private static Material GlowMaterial()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(GlowTemplatePrefab);
        if (template == null) return null;
        Transform glow = template.transform.Find("Leuchten");
        SpriteRenderer r = glow != null ? glow.GetComponent<SpriteRenderer>() : null;
        return r != null ? r.sharedMaterial : null;
    }

    private static GameObject DefaultDestroyEffect()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
        Enemy e = template != null ? template.GetComponent<Enemy>() : null;
        return e != null ? e.EditorDestroyEffect : null;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
