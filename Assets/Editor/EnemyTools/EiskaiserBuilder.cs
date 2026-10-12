using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut das Prefab des Eiskaisers (Endboss der Eiswelt) und traegt es im
/// SpawnCatalog von GameCore ein.
///
/// Eigener Bauer wie beim Schleimkoenig: Laufen + Kick in fuenf Ansichten,
/// Zauber, Bruellen, Tod (von vorn) und vier Effekt-Streifen,
/// das Bild sitzt auf einem Kind, gesteuert wird ohne Animator
/// (<see cref="EnemyEiskaiser"/>). Wiederholbar: ein vorhandenes Prefab wird
/// aktualisiert.
/// </summary>
public static class EiskaiserBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/boss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";
    private const string SortingLayerName = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    [MenuItem("Tools/Gegner/Eiskaiser bauen", false, 106)]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Eiskaiser", "Bitte erst den Play-Modus beenden.", "Ok");
            return;
        }
        string result = Build();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Eiskaiser", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod EiskaiserBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Eiskaiser] " + Build());
        Debug.Log("[Eiskaiser] " + Register());
    }

    public static string Build()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Eiskaiser);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer den Eiskaiser.";

        EnemyEiskaiser.Views walk = LoadViews("eiskaiser_walk");
        EnemyEiskaiser.Views kick = LoadViews("eiskaiser_kick");
        Sprite[] cast = Load("eiskaiser_cast");
        Sprite[] roar = Load("eiskaiser_roar");
        Sprite[] death = Load("eiskaiser_death");
        Sprite[] crystal = Load("eiskaiser_kristall");
        Sprite[] block = Load("eiskaiser_eisblock");
        Sprite[] ball = Load("eiskaiser_kugel");
        Sprite[] burst = Load("eiskaiser_platzt");
        string missing = Missing(walk, "eiskaiser_walk") + Missing(kick, "eiskaiser_kick");
        if (cast.Length == 0) missing += " eiskaiser_cast";
        if (roar.Length == 0) missing += " eiskaiser_roar";
        if (death.Length == 0) missing += " eiskaiser_death";
        if (missing.Length > 0)
            return "Bilder fehlen unter " + ArtFolder + ":" + missing + ".";

        string ballResult = BuildSnowball(ball, burst, out GameObject ballPrefab);

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Eiskaiser");

        try
        {
            root.name = "Eiskaiser";
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

            Transform t = root.transform.Find("Bild");
            if (t == null)
            {
                t = new GameObject("Bild").transform;
                t.SetParent(root.transform, false);
            }
            t.localPosition = Vector3.zero;
            t.gameObject.layer = root.layer;
            SpriteRenderer bodyR = GetOrAdd<SpriteRenderer>(t.gameObject);
            bodyR.sortingLayerName = SortingLayerName;
            bodyR.sortingOrder = 1;
            bodyR.sprite = walk.s[0];
            Material material = TemplateMaterial();
            if (material != null) bodyR.sharedMaterial = material;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 40f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.Eiskaiser);
            enemy.EditorBind(bodyR, body, enemy.EditorDestroyEffect != null
                                          ? enemy.EditorDestroyEffect
                                          : DefaultDestroyEffect());

            EnemyEiskaiser boss = GetOrAdd<EnemyEiskaiser>(root);
            boss.EditorBind(bodyR, collider, walk, kick, cast, roar, death, crystal, block, ballPrefab);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} (Laufen 5x{2}, Kick 5x{3}, Zauber {4}, Bruellen {5}, Tod {6}, Kristall {7}, Eisblock {8}). {9}",
            existed ? "aktualisiert" : "angelegt", def.Prefab, walk.s.Length, kick.s.Length, cast.Length,
            roar.Length, death.Length, crystal.Length, block.Length, ballResult);
    }

    /// <summary>Die Lawinenkugel: eigener Gegner (Id 38) mit Rollbildern und Platz-Effekt.</summary>
    private static string BuildSnowball(Sprite[] ball, Sprite[] burst, out GameObject prefab)
    {
        prefab = null;
        EnemyDef def = EnemyCatalog.Get(EnemyId.Lawinenkugel);
        if (def == null || ball.Length == 0) return "Lawinenkugel: kein Katalogeintrag oder keine Bilder.";

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Lawinenkugel");
        try
        {
            root.name = "Lawinenkugel";
            root.tag = "Enemy";
            root.layer = EnemyLayer();

            SpriteRenderer sr = GetOrAdd<SpriteRenderer>(root);
            sr.sortingLayerName = SortingLayerName;
            sr.sortingOrder = 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.sprite = ball[0];
            Material material = TemplateMaterial();
            if (material != null) sr.sharedMaterial = material;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 8f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.Lawinenkugel);
            enemy.EditorBind(sr, body, null);

            EiskaiserLawinenkugel kugel = GetOrAdd<EiskaiserLawinenkugel>(root);
            kugel.EditorBind(sr, collider, ball, burst);

            EditorUtility.SetDirty(root);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
        return "Lawinenkugel " + (existed ? "aktualisiert" : "angelegt") + " (" + ball.Length + " Rollbilder, " + burst.Length + " Platzen).";
    }

    /// <summary>Traegt das Prefab im SpawnCatalog von GameCore ein.</summary>
    public static string Register()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Eiskaiser);
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
            catalog.EditorSet(EnemyId.Eiskaiser, asset);
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CoreScenePath);
            result = "Im SpawnCatalog (GameCore) eingetragen.";
        }

        if (!Application.isBatchMode && !string.IsNullOrEmpty(before) && before != CoreScenePath)
            EditorSceneManager.OpenScene(before, OpenSceneMode.Single);
        return result;
    }

    // ------------------------------------------------------------- Kleinkram

    private static Sprite[] Load(string sheet)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ArtFolder + sheet + ".png")
                            .OfType<Sprite>()
                            .OrderBy(s => TrailingNumber(s.name))
                            .ToArray();
    }

    /// <summary>Fuenf Ansichten: [sheet]_s, _se, _e, _ne, _n.</summary>
    private static EnemyEiskaiser.Views LoadViews(string sheet)
    {
        return new EnemyEiskaiser.Views
        {
            s = Load(sheet + "_s"),
            se = Load(sheet + "_se"),
            e = Load(sheet + "_e"),
            ne = Load(sheet + "_ne"),
            n = Load(sheet + "_n"),
        };
    }

    private static string Missing(EnemyEiskaiser.Views v, string sheet)
    {
        string m = "";
        if (v.s.Length == 0) m += " " + sheet + "_s";
        if (v.se.Length == 0) m += " " + sheet + "_se";
        if (v.e.Length == 0) m += " " + sheet + "_e";
        if (v.ne.Length == 0) m += " " + sheet + "_ne";
        if (v.n.Length == 0) m += " " + sheet + "_n";
        return m;
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
