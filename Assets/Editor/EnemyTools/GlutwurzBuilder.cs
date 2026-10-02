using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Baut das Prefab der Glutwurz (Wald-Endboss) und traegt es im SpawnCatalog
/// von GameCore ein.
///
/// Warum nicht einfach die Gegner-Werkstatt: die baut einen Gegner mit EINEM
/// Laufclip. Die Glutwurz hat acht Bildstreifen und einen Flammenwurf als
/// Kind - das steckt dieses Skript zusammen. Danach ist sie ein ganz normaler
/// Katalog-Gegner: Werte in der Werkstatt aendern geht wie bei jedem anderen,
/// und baut die Werkstatt das Prefab neu, bleibt alles hier Angelegte stehen
/// (den Animator, den sie dazuhaengt, schaltet EnemyGlutwurz selbst ab).
///
/// Wiederholbar: ein vorhandenes Prefab wird aktualisiert, nicht ersetzt.
/// Die Bilder kommen aus Tools/baumboss.py und Tools/baumboss_feuer.py.
/// </summary>
public static class GlutwurzBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/boss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/fin_marshmallow_0.prefab";
    private const string SortingLayer = "Objects";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    /// <summary>
    /// Der Flammenwurf steht doppelt so gross: dann ist ein Feuerpixel so gross
    /// wie ein Pixel der Vorlage (die ist per Scale2x verdoppelt) und der
    /// Strahl rund 1.2 Einheiten dick.
    /// </summary>
    private const float BeamScale = 2f;

    [MenuItem("Tools/Gegner/Glutwurz bauen", false, 104)]
    public static void BuildFromMenu()
    {
        string result = Build();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Glutwurz", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod GlutwurzBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Glutwurz] " + Build());
        Debug.Log("[Glutwurz] " + Register());
    }

    private static string Build()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Glutwurz);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer die Glutwurz.";

        Sprite[] front = Load("baumboss_walk");
        Sprite[] back = Load("baumboss_walk_back");
        Sprite[] charge = Load("baumboss_charge");
        Sprite[] roar = Load("baumboss_roar");
        Sprite[] roarLoop = Load("baumboss_roar_loop");
        Sprite[] chargeBack = Load("baumboss_charge_back");
        Sprite[] roarBack = Load("baumboss_roar_back");
        Sprite[] roarLoopBack = Load("baumboss_roar_loop_back");
        Sprite[] fireStart = Load("baumboss_fire_start");
        Sprite[] fireMid = Load("baumboss_fire_mid");
        Sprite[] fireEnd = Load("baumboss_fire_end");
        if (front.Length == 0) return "Keine Bilder unter " + ArtFolder + " - erst Tools/baumboss.py laufen lassen.";

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Glutwurz");

        try
        {
            root.name = "Glutwurz";
            root.tag = "Enemy";
            root.layer = EnemyLayer();
            root.transform.localScale = Vector3.one * def.Scale;

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
            renderer.sprite = front[0];
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = 1;
            Material material = TemplateMaterial();
            if (material != null) renderer.sharedMaterial = material;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            // Schwer: die Gegnerhorden sollen ihn nicht vor sich herschieben
            body.mass = 30f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.Glutwurz);
            enemy.EditorBind(renderer, body, enemy.EditorDestroyEffect != null
                                              ? enemy.EditorDestroyEffect
                                              : DefaultDestroyEffect());

            Transform flameT = root.transform.Find("Flammenwurf");
            if (flameT == null)
            {
                flameT = new GameObject("Flammenwurf").transform;
                flameT.SetParent(root.transform, false);
            }
            flameT.localPosition = new Vector3(0f, 0.83f, 0f);
            flameT.localRotation = Quaternion.identity;
            flameT.localScale = new Vector3(BeamScale, BeamScale, 1f);
            FlameBeam beam = GetOrAdd<FlameBeam>(flameT.gameObject);
            beam.EditorBind(fireStart, fireMid, fireEnd);
            flameT.gameObject.SetActive(false);

            EnemyGlutwurz boss = GetOrAdd<EnemyGlutwurz>(root);
            boss.EditorBind(front, back, charge, roar, roarLoop, chargeBack, roarBack, roarLoopBack, beam);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} ({2}+{3} Lauf-, {4}+{5} Lade-, {6}+{7}/{8}+{9} Bruellbilder, Feuer {10}/{11}/{12}).",
            existed ? "aktualisiert" : "angelegt", def.Prefab, front.Length, back.Length, charge.Length,
            chargeBack.Length, roar.Length, roarLoop.Length, roarBack.Length, roarLoopBack.Length,
            fireStart.Length, fireMid.Length, fireEnd.Length);
    }

    private static string Register()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Glutwurz);
        GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
        if (asset == null) return "Kein Prefab zum Eintragen.";

        var scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
        SpawnCatalog catalog = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            catalog = go.GetComponentInChildren<SpawnCatalog>(true);
            if (catalog != null) break;
        }
        if (catalog == null) return "In " + CoreScenePath + " steckt kein SpawnCatalog.";

        catalog.EditorSet(EnemyId.Glutwurz, asset);
        EditorUtility.SetDirty(catalog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, CoreScenePath);
        return "Im SpawnCatalog (GameCore) eingetragen.";
    }

    // ------------------------------------------------------------- Kleinkram

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
