using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut das Prefab des Schleimkoenigs (Zwischenboss) und traegt es im
/// SpawnCatalog von GameCore ein.
///
/// Warum nicht die normale Werkstatt-Bauweise: er hat acht Bildstreifen,
/// einen Schatten und einen Aufprall-Effekt als Kinder, und das Bild sitzt
/// auf einem Kind, das der Code anhebt (Sprunghoehe). Die Werkstatt leitet
/// "Prefab bauen" fuer ihn hierher um (<see cref="SpecialBuilders"/>), Werte
/// aendert man dort trotzdem ganz normal.
///
/// Wiederholbar: ein vorhandenes Prefab wird aktualisiert, nicht ersetzt.
/// Die Bilder kommen aus Tools/schleimkoenig.py.
/// </summary>
public static class SchleimkoenigBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/miniboss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";
    private const string SortingLayerName = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    [MenuItem("Tools/Gegner/Schleimkoenig bauen", false, 105)]
    public static void BuildFromMenu()
    {
        string result = Build();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Schleimkoenig", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod SchleimkoenigBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Schleimkoenig] " + Build());
        Debug.Log("[Schleimkoenig] " + Register());
    }

    public static string Build()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Schleimkoenig);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer den Schleimkoenig.";

        Sprite[] hop = Load("schleimkoenig_hop");
        Sprite[] hopBlink = Load("schleimkoenig_hop_blink");
        Sprite[] duck = Load("schleimkoenig_ducken");
        Sprite[] launch = Load("schleimkoenig_absprung");
        Sprite[] fall = Load("schleimkoenig_fall");
        Sprite[] land = Load("schleimkoenig_landung");
        Sprite[] splash = Load("schleimkoenig_platsch");
        Sprite[] shadow = Load("schleimkoenig_schatten");
        if (hop.Length == 0) return "Keine Bilder unter " + ArtFolder + " - erst Tools/schleimkoenig.py laufen lassen.";

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Schleimkoenig");

        try
        {
            root.name = "Schleimkoenig";
            root.tag = "Enemy";
            root.layer = EnemyLayer();
            root.transform.localScale = Vector3.one * def.Scale;

            // Baut die Werkstatt ihn doch einmal auf die normale Art, haengt
            // sie Bild und Animator an die Wurzel - die gehoeren aufs Kind.
            Animator rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) Object.DestroyImmediate(rootAnimator, true);
            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) Object.DestroyImmediate(rootRenderer, true);

            // Alles in einer Sortiergruppe: Schatten, Platsch und Koerper
            // sortieren untereinander fest, nach aussen zaehlt die Wurzel.
            SortingGroup group = GetOrAdd<SortingGroup>(root);
            group.sortingLayerName = SortingLayerName;
            group.sortingOrder = 1;

            Material material = TemplateMaterial();

            SpriteRenderer shadowR = Child(root, "Schatten", -1, material);
            shadowR.sprite = shadow.Length > 0 ? shadow[0] : null;

            SpriteRenderer splashR = Child(root, "Platsch", 0, material);
            splashR.sprite = splash.Length > 0 ? splash[0] : null;
            splashR.enabled = false;

            SpriteRenderer bodyR = Child(root, "Bild", 1, material);
            bodyR.sprite = hop[0];

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
            enemy.EditorSetId(EnemyId.Schleimkoenig);
            enemy.EditorBind(bodyR, body, enemy.EditorDestroyEffect != null
                                          ? enemy.EditorDestroyEffect
                                          : DefaultDestroyEffect());

            EnemySchleimkoenig boss = GetOrAdd<EnemySchleimkoenig>(root);
            boss.EditorBind(bodyR, shadowR, splashR, collider,
                            hop, hopBlink, duck, launch, fall, land, splash, shadow);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} (Hopser {2}+{3}, Ducken {4}, Absprung {5}, Fall {6}, Landung {7}, Platsch {8}, Schatten {9}).",
            existed ? "aktualisiert" : "angelegt", def.Prefab, hop.Length, hopBlink.Length, duck.Length,
            launch.Length, fall.Length, land.Length, splash.Length, shadow.Length);
    }

    /// <summary>
    /// Traegt das Prefab im SpawnCatalog von GameCore ein. Oeffnet dafuer
    /// GameCore und danach wieder die Szene, die vorher offen war.
    /// </summary>
    public static string Register()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Schleimkoenig);
        GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
        if (asset == null) return "Kein Prefab zum Eintragen.";

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
            catalog.EditorSet(EnemyId.Schleimkoenig, asset);
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CoreScenePath);
            result = "Im SpawnCatalog (GameCore) eingetragen.";
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

/// <summary>
/// Gegner, deren Prefab nicht die Werkstatt baut, sondern ein eigenes Skript
/// (mehrere Bildstreifen, Kinder, eigene Steuerung). Die Werkstatt fragt hier
/// nach, bevor sie ein Prefab auf die normale Art baut - sonst wuerde ein
/// Klick auf "Prefab neu bauen" einen Boss auf einen Laufclip zurueckstutzen.
/// </summary>
public static class SpecialBuilders
{
    public static bool Handles(EnemyId id)
    {
        return id == EnemyId.Glutwurz || id == EnemyId.Schleimkoenig
            || id == EnemyId.Verkohlter || id == EnemyId.VerkohlterTod
            || id == EnemyId.Eiskaiser || id == EnemyId.Lawinenkugel
            || id == EnemyId.Squiddy || id == EnemyId.QuallenBaby;
    }

    /// <summary>Baut nur das Prefab. Eintragen in GameCore macht die Werkstatt selbst.</summary>
    public static string Build(EnemyId id)
    {
        switch (id)
        {
            case EnemyId.Glutwurz: return GlutwurzBuilder.Build();
            case EnemyId.Schleimkoenig: return SchleimkoenigBuilder.Build();
            case EnemyId.Verkohlter:
            case EnemyId.VerkohlterTod: return VerkohlterBuilder.Build(id);
            case EnemyId.Eiskaiser:
            case EnemyId.Lawinenkugel: return EiskaiserBuilder.Build();
            case EnemyId.Squiddy:
            case EnemyId.QuallenBaby: return SquiddyBuilder.Build();
            default: return id + ": kein eigener Bauer.";
        }
    }
}
