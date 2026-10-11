using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut das Prefab des Milchpanzers (Zwischenboss der Kueche) und traegt es
/// im SpawnCatalog von GameCore ein.
///
/// Warum nicht die normale Werkstatt-Bauweise: er hat sechs Bildstreifen
/// (Fahren, Halm, Saugen, Ruelpsen, Tropfen, Spritzer) und spielt sie selbst
/// ab (<see cref="EnemyMilchpanzer"/>). Die Werkstatt leitet "Prefab bauen"
/// fuer ihn hierher um (<see cref="SpecialBuilders"/>), Werte aendert man
/// dort trotzdem ganz normal.
///
/// Wiederholbar: ein vorhandenes Prefab wird aktualisiert, nicht ersetzt.
/// </summary>
public static class MilchpanzerBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/miniboss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";
    private const string SortingLayerName = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    [MenuItem("Tools/Gegner/Milchpanzer bauen", false, 106)]
    public static void BuildFromMenu()
    {
        string result = Build();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Milchpanzer", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod MilchpanzerBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Milchpanzer] " + Build());
        Debug.Log("[Milchpanzer] " + Register());
    }

    public static string Build()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Milchpanzer);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer den Milchpanzer.";

        Sprite[] drive = Load("milchpanzer_fahrt");
        Sprite[] straw = Load("milchpanzer_halm");
        Sprite[] suck = Load("milchpanzer_saugen");
        Sprite[] burp = Load("milchpanzer_ruelps");
        Sprite[] droplet = Load("milchpanzer_tropfen");
        Sprite[] splash = Load("milchpanzer_spritzer");
        if (drive.Length == 0) return "Keine Bilder unter " + ArtFolder + ".";

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Milchpanzer");

        try
        {
            root.name = "Milchpanzer";
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

            Material material = TemplateMaterial();

            SpriteRenderer bodyR = Child(root, "Bild", 1, material);
            bodyR.sprite = drive[0];

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            // Schwer: ein Panzer laesst sich von der Horde nicht schieben
            body.mass = 40f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.Milchpanzer);
            enemy.EditorBind(bodyR, body, enemy.EditorDestroyEffect != null
                                          ? enemy.EditorDestroyEffect
                                          : DefaultDestroyEffect());

            EnemyMilchpanzer boss = GetOrAdd<EnemyMilchpanzer>(root);
            boss.EditorBind(bodyR, collider, drive, straw, suck, burp, droplet, splash);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} (Fahren {2}, Halm {3}, Saugen {4}, Ruelps {5}, Tropfen {6}, Spritzer {7}).",
            existed ? "aktualisiert" : "angelegt", def.Prefab, drive.Length, straw.Length, suck.Length,
            burp.Length, droplet.Length, splash.Length);
    }

    /// <summary>
    /// Traegt das Prefab im SpawnCatalog von GameCore ein. Oeffnet dafuer
    /// GameCore und danach wieder die Szene, die vorher offen war.
    /// </summary>
    public static string Register()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Milchpanzer);
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
            catalog.EditorSet(EnemyId.Milchpanzer, asset);
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
