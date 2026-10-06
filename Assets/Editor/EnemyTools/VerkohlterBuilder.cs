using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut die beiden Prefabs des Verkohlten - den Boss (EnemyId.Verkohlter)
/// und den Tod der Demo (EnemyId.VerkohlterTod) - samt Zerfall-Effekt und
/// traegt beide im SpawnCatalog von GameCore ein. Gleiches Skript
/// (<see cref="EnemyVerkohlter"/>), gleiche Bilder; was er ist, entscheidet
/// die Id.
///
/// Wiederholbar: vorhandene Prefabs werden aktualisiert, nicht ersetzt.
/// Die Bilder kommen aus Tools/verkohlter.py.
/// </summary>
public static class VerkohlterBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/boss/";
    private const string EffectPrefab = "Assets/Prefabs/Enemy/Boss/Verkohlter_Zerfall.prefab";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";
    private const string SortingLayerName = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    [MenuItem("Tools/Gegner/Verkohlter bauen", false, 106)]
    public static void BuildFromMenu()
    {
        // Im Play-Modus baut Unity zwar noch die Prefabs, wirft aber beim
        // Szenen-Speichern - dann fehlt der Eintrag in GameCore, und im Lauf
        // kommt der Tod nie (so am 03.10.2026 passiert).
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Der Verkohlte", "Erst den Play-Modus beenden, dann noch einmal bauen.", "Ok");
            return;
        }

        string result = Build(EnemyId.Verkohlter) + "\n" + Build(EnemyId.VerkohlterTod);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Der Verkohlte", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod VerkohlterBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Verkohlter] " + Build(EnemyId.Verkohlter));
        Debug.Log("[Verkohlter] " + Build(EnemyId.VerkohlterTod));
        Debug.Log("[Verkohlter] " + Register());
    }

    public static string Build(EnemyId id)
    {
        EnemyDef def = EnemyCatalog.Get(id);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer " + id + ".";

        Sprite[] idle = Load("verkohlter_idle");
        Sprite[] walk = Load("verkohlter_walk");
        Sprite[] burst = Load("verkohlter_ausbruch");
        Sprite[] ashOut = Load("verkohlter_asche_weg");
        Sprite[] ashIn = Load("verkohlter_asche_da");
        Sprite[] shadowIdle = Load("verkohlter_schatten_idle");
        Sprite[] shadowBurst = Load("verkohlter_schatten_ausbruch");
        Sprite[] shadowAshOut = Load("verkohlter_schatten_asche_weg");
        Sprite[] shadowAshIn = Load("verkohlter_schatten_asche_da");
        Sprite[] crumb = Load("verkohlter_kruemel");
        Sprite[] patch = Load("verkohlter_glutfleck");
        if (idle.Length == 0) return "Keine Bilder unter " + ArtFolder + " - erst Tools/verkohlter.py laufen lassen.";

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));
        GameObject effect = BuildEffect();

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        string objectName = id == EnemyId.VerkohlterTod ? "VerkohlterTod" : "Verkohlter";
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject(objectName);

        try
        {
            root.name = objectName;
            root.tag = "Enemy";
            root.layer = EnemyLayer();
            root.transform.localScale = Vector3.one * def.Scale;

            // Baut die Werkstatt ihn doch einmal auf die normale Art, haengt
            // sie Bild und Animator an die Wurzel - die gehoeren aufs Kind.
            Animator rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) Object.DestroyImmediate(rootAnimator, true);
            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) Object.DestroyImmediate(rootRenderer, true);

            SortingGroup group = GetOrAdd<SortingGroup>(root);
            group.sortingLayerName = SortingLayerName;
            group.sortingOrder = 1;

            SpriteRenderer bodyR = Child(root, "Bild", 1, TemplateMaterial());
            bodyR.sprite = idle[0];

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            // Schwer: die Horden sollen ihn nicht vor sich herschieben
            body.mass = 30f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(id);
            enemy.EditorBind(bodyR, body, effect);

            EnemyVerkohlter boss = GetOrAdd<EnemyVerkohlter>(root);
            boss.EditorBind(bodyR, collider, idle, walk, burst, ashOut, ashIn,
                            shadowIdle, shadowBurst, shadowAshOut, shadowAshIn, crumb, patch);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} (Idle {2}, Laufen {3}, Ausbruch {4}, Asche {5}/{6}, Schatten {7}, Kruemel {8}, Glut {9}).",
            existed ? "aktualisiert" : "angelegt", def.Prefab, idle.Length, walk.Length, burst.Length,
            ashOut.Length, ashIn.Length, shadowBurst.Length, crumb.Length, patch.Length);
    }

    /// <summary>
    /// Der Todeseffekt des Bosses: der ganze Zerfall (24 Bilder) als
    /// Einmal-Abspieler. Enemy.Die setzt ihn an die Stelle des Gegners - Pivot
    /// und Zelle sind dieselben wie beim Boss, er steht also genau dort.
    /// </summary>
    private static GameObject BuildEffect()
    {
        Sprite[] death = Load("verkohlter_tod");
        if (death.Length == 0) return null;

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(EffectPrefab) : new GameObject("Verkohlter_Zerfall");
        try
        {
            root.name = "Verkohlter_Zerfall";
            SpriteRenderer r = GetOrAdd<SpriteRenderer>(root);
            r.sprite = death[0];
            r.sortingLayerName = SortingLayerName;
            r.sortingOrder = 1;
            Material material = TemplateMaterial();
            if (material != null) r.sharedMaterial = material;

            OneShotFlipbook flip = GetOrAdd<OneShotFlipbook>(root);
            flip.EditorBind(death, 12f, 1.5f);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, EffectPrefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefab);
    }

    /// <summary>
    /// Traegt beide Prefabs im SpawnCatalog von GameCore ein. Oeffnet dafuer
    /// GameCore und danach wieder die Szene, die vorher offen war.
    /// </summary>
    public static string Register()
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
            int count = 0;
            foreach (EnemyId id in new[] { EnemyId.Verkohlter, EnemyId.VerkohlterTod })
            {
                EnemyDef def = EnemyCatalog.Get(id);
                GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
                if (asset == null) continue;
                catalog.EditorSet(id, asset);
                count++;
            }
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CoreScenePath);
            result = count + " Prefab(s) im SpawnCatalog (GameCore) eingetragen.";
        }

        if (!Application.isBatchMode && !string.IsNullOrEmpty(before) && before != CoreScenePath)
        {
            EditorSceneManager.OpenScene(before, OpenSceneMode.Single);
        }
        return result;
    }

    // ------------------------------------------------------------- Kleinkram

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
    private static Sprite[] Load(string sheet)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ArtFolder + sheet + ".png")
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

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
