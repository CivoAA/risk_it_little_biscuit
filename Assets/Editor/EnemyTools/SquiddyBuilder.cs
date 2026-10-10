using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut die Prefabs des Gespensts / Squiddys (Endboss des Geisterwalds) und
/// der Baby-Squiddies und traegt beide im SpawnCatalog von GameCore ein.
/// Bilder aus Tools/squiddy.py + Tools/squiddy_fx.py.
///
/// Wie beim Eiskaiser: das Bild sitzt auf einem Kind, gesteuert wird ohne
/// Animator (<see cref="EnemySquiddy"/>), alle Streifen haengen am Skript.
/// Das Bild ist unbeleuchtet (Sprite-Unlit) - im dunklen Wald leuchten Geist
/// und Qualle selbst, dazu wirft der Boss ein eigenes Light2D.
/// Wiederholbar: vorhandene Prefabs werden aktualisiert.
/// </summary>
public static class SquiddyBuilder
{
    private const string ArtFolder = "Assets/Art/Gegner/new/boss/";
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string TemplatePrefab = "Assets/Prefabs/Enemy/Archiv/Vorlagen/fin_marshmallow_0.prefab";
    private const string SortingLayerName = "Objects";
    /// <summary>Phase-2-Musik aus Tools/squiddy_musik.py (setzt beim PLING ein).</summary>
    private const string MusicPath = "Assets/music/squiddy.wav";
    private static readonly string[] LayerCandidates = { "Enemys ", "Enemys", "Enemy" };

    private static readonly string[] Strips =
    {
        "geist_schweben", "geist_spaeher", "geist_abtauchen", "geist_buh", "geist_reigen", "enthuellung",
        "schwimm", "nessel", "brut", "spannung", "tod",
        "schatten", "buh_welle", "spukgeist", "laken_flug", "laken_boden", "nessel_tentakel", "entladung", "baby",
    };

    [MenuItem("Tools/Gegner/Squiddy bauen", false, 107)]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Squiddy", "Bitte erst den Play-Modus beenden.", "Ok");
            return;
        }
        string result = Build();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        result += "\n" + Register();
        EditorUtility.DisplayDialog("Squiddy", result, "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod SquiddyBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Squiddy] " + Build());
        Debug.Log("[Squiddy] " + Register());
    }

    public static string Build()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Squiddy);
        if (def == null || string.IsNullOrEmpty(def.Prefab)) return "Kein Katalogeintrag fuer Squiddy.";

        var s = new Dictionary<string, Sprite[]>();
        var missing = new List<string>();
        foreach (string name in Strips)
        {
            s[name] = Load("squiddy_" + name);
            if (s[name].Length == 0) missing.Add(name);
        }
        if (missing.Count > 0)
            return "Bilder fehlen (" + string.Join(", ", missing) + ") - erst Tools/squiddy.py und Tools/squiddy_fx.py laufen lassen.";

        Material unlit = FindUnlitMaterial();
        string babyResult = BuildBaby(s["baby"], unlit, out GameObject babyPrefab);

        EnsureFolder(System.IO.Path.GetDirectoryName(def.Prefab).Replace('\\', '/'));
        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("Squiddy");

        try
        {
            root.name = "Squiddy";
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
            bodyR.sprite = s["geist_schweben"][0];
            if (unlit != null) bodyR.sharedMaterial = unlit;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 40f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            // gegen Ruckeln bei 144 Hz (siehe Keks-Koenig)
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.Squiddy);
            enemy.EditorBind(bodyR, body, enemy.EditorDestroyEffect != null ? enemy.EditorDestroyEffect : DefaultDestroyEffect());

            EnemySquiddy boss = GetOrAdd<EnemySquiddy>(root);
            boss.EditorBind(bodyR, collider, s, babyPrefab);
            boss.EditorSetMusic(AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath));

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        return string.Format("Prefab {0}: {1} ({2} Streifen). {3}", existed ? "aktualisiert" : "angelegt",
                             def.Prefab, Strips.Length, babyResult);
    }

    private static string BuildBaby(Sprite[] frames, Material unlit, out GameObject prefab)
    {
        prefab = null;
        EnemyDef def = EnemyCatalog.Get(EnemyId.QuallenBaby);
        if (def == null || frames.Length == 0) return "Baby: kein Katalogeintrag oder keine Bilder.";

        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) != null;
        GameObject root = existed ? PrefabUtility.LoadPrefabContents(def.Prefab) : new GameObject("QuallenBaby");
        try
        {
            root.name = "QuallenBaby";
            root.tag = "Enemy";
            root.layer = EnemyLayer();

            SortingGroup group = GetOrAdd<SortingGroup>(root);
            group.sortingLayerName = SortingLayerName;
            group.sortingOrder = 0;

            Transform t = root.transform.Find("Bild");
            if (t == null)
            {
                t = new GameObject("Bild").transform;
                t.SetParent(root.transform, false);
            }
            t.gameObject.layer = root.layer;
            SpriteRenderer sr = GetOrAdd<SpriteRenderer>(t.gameObject);
            sr.sortingLayerName = SortingLayerName;
            sr.sortingOrder = 0;
            sr.sprite = frames[0];
            if (unlit != null) sr.sharedMaterial = unlit;

            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.mass = 2f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = def.ColliderRadius;
            collider.offset = def.ColliderOffset;

            Enemy enemy = GetOrAdd<Enemy>(root);
            enemy.EditorSetId(EnemyId.QuallenBaby);
            enemy.EditorBind(sr, body, null);

            SquiddyBaby baby = GetOrAdd<SquiddyBaby>(root);
            baby.EditorBind(sr, collider, frames);

            EditorUtility.SetDirty(root);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, def.Prefab);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
        return "Baby " + (existed ? "aktualisiert" : "angelegt") + " (" + frames.Length + " Bilder).";
    }

    /// <summary>Traegt Boss und Baby im SpawnCatalog von GameCore ein.</summary>
    public static string Register()
    {
        EnemyDef def = EnemyCatalog.Get(EnemyId.Squiddy);
        EnemyDef babyDef = EnemyCatalog.Get(EnemyId.QuallenBaby);
        GameObject asset = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab) : null;
        GameObject babyAsset = babyDef != null ? AssetDatabase.LoadAssetAtPath<GameObject>(babyDef.Prefab) : null;
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
            catalog.EditorSet(EnemyId.Squiddy, asset);
            if (babyAsset != null) catalog.EditorSet(EnemyId.QuallenBaby, babyAsset);
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
                            .OrderBy(sp => TrailingNumber(sp.name))
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

    private static Material FindUnlitMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (m != null) return m;
        foreach (string guid in AssetDatabase.FindAssets("Sprite-Unlit-Default t:Material"))
        {
            m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) return m;
        }
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
