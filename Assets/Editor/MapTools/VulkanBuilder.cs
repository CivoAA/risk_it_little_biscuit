using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Baut die Vulkanwelt (Level 5, Szene Map_World4) aus den Python-Bildern:
///  1. ein Tile je Sprite aus vulkan_boden.png,
///  2. ein Prefab je Lavaloch (Flipbook, PolygonCollider2D am Lochrand, MixerBlocker),
///  3. die Szene: 3x3-Endloswelt wie der Wald (WorldManager3x3 + Mixer-Spawner),
///     jeder Chunk fertig belegt aus vulkan_layout.json, Lavaloecher als Props,
///     die der ChunkPropRandomizer beim Umsetzen neu verteilt,
///  4. traegt die Szene in die Build Settings ein und legt Level 5 in der
///     Levelauswahl des Hubs auf die neue Welt.
///
/// Wiederholbar: Tiles und Prefabs werden aktualisiert, die Welt (Grid) wird
/// neu gebaut, das Map-Objekt (MapDefinition) bleibt stehen - Musik oder
/// Wellenplan, die dort spaeter eingetragen werden, gehen also nicht verloren.
/// Bilder: Tools/vulkan_lava.py, dann Tools/vulkan_boden.py, Vorschau
/// Tools/vulkan_vorschau.py.
/// </summary>
public static class VulkanBuilder
{
    private const string TileSheet = "Assets/Art/Tiles_Vulkan/vulkan_boden.png";
    private const string TileFolder = "Assets/Art/Tiles_Vulkan/Tiles";
    private const string LayoutPath = "Assets/Art/Tiles_Vulkan/vulkan_layout.json";
    private const string LavaArtFolder = "Assets/Art/World-Objects/Vulkan";
    private const string LavaInfoPath = LavaArtFolder + "/vulkan_lava.json";
    private const string PrefabFolder = "Assets/Prefabs/MapObjects/Vulkan";
    private const string ScenePath = "Assets/Scenes/Maps/Map_World4.unity";
    private const string HubPath = "Assets/Scenes/hub.unity";
    private const string PreviewPath = "Assets/Art/new/Hub/level_preview_vulkan.png";
    private const string PreviewWidePath = "Assets/Art/new/Hub/level_preview_vulkan_wide.png";

    // Mixer-Prefab des Wald-Spawners (Map_World3, World3 -> RandomObjectSpawner3x3)
    private const string MixerPrefabGuid = "29b3d2893f45c544695f29b321342ab2";

    private const int MapId = 4;
    private const int LevelSlot = 4;               // Station 5 der Levelauswahl
    private const string WorldName = "World4";
    private const float ChunkWidth = 52f;
    private const float ChunkHeight = 40f;
    private const int FloorOrder = 5;              // wie der Waldboden
    private const int LavaOrder = 10;              // flach im Boden, unter Gegnern/XP (Objects)
    private const int PropLayer = 3;               // "Baundaries": haelt den Spieler, Gegner laufen drueber
    private const float PoolGap = 1.6f;            // wie POOL_GAP in Tools/vulkan_boden.py

    [Serializable] private class PoolInfo
    {
        public string name;
        public int frames;
        public float fps;
        public float[] colliderX;
        public float[] colliderY;
    }

    [Serializable] private class ChunkPool { public string name; public float x; public float y; }

    [MenuItem("Tools/Welt/Vulkan einrichten", false, 121)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorUtility.DisplayDialog("Vulkan", Build(), "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod VulkanBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Vulkan] " + Build());
    }

    internal static string Build()
    {
        AssetDatabase.Refresh();
        if (!File.Exists(LayoutPath) || !File.Exists(LavaInfoPath))
            return "vulkan_layout.json oder vulkan_lava.json fehlt - erst Tools/vulkan_lava.py und Tools/vulkan_boden.py laufen lassen.";

        // Szene zuerst oeffnen bzw. anlegen: OpenScene entlaedt unbenutzte Assets,
        // vorher geladene Tiles waeren danach kaputt (siehe KuecheBuilder).
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Tile[] tiles = BuildTiles();
        if (tiles == null) return "Keine Sprites in " + TileSheet + ".";

        Dictionary<string, GameObject> pools = BuildPoolPrefabs();
        AssetDatabase.SaveAssets();
        if (pools.Count == 0) return "Keine Lavaloch-Prefabs gebaut - Bilder fehlen?";

        EnsureMapObject(scene);
        int pooled = BuildWorld(scene, tiles, pools);
        EditorSceneManager.MarkSceneDirty(scene);
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);

        bool added = AddToBuildSettings();
        string hub = SetupHubLevel();

        return $"{tiles.Length} Tiles, {pools.Count} Lavaloch-Prefabs, {pooled} Loecher in 9 Chunks. " +
               (added ? "Szene in die Build Settings eingetragen. " : "") + hub;
    }

    // --- 1. Tiles ------------------------------------------------------------

    private static Tile[] BuildTiles()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TileSheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
        if (sprites.Length == 0) return null;

        EnsureFolder(TileFolder);
        var tiles = new Tile[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            string path = $"{TileFolder}/{sprites[i].name}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprites[i];
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles[i] = tile;
        }
        return tiles;
    }

    // --- 2. Lavaloecher --------------------------------------------------------

    private static Dictionary<string, GameObject> BuildPoolPrefabs()
    {
        var result = new Dictionary<string, GameObject>();
        EnsureFolder(PrefabFolder);

        foreach (PoolInfo p in ReadPools())
        {
            string artPath = $"{LavaArtFolder}/vulkan_lava_{p.name}.png";
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
                .ToArray();
            if (frames.Length == 0)
            {
                Debug.LogWarning("[Vulkan] Keine Sprites: " + artPath);
                continue;
            }

            string prefabPath = $"{PrefabFolder}/Vulkan_Lava_{Title(p.name)}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Vulkan_Lava_" + Title(p.name));

            root.layer = PropLayer;

            var sr = GetOrAdd<SpriteRenderer>(root);
            sr.sprite = frames[0];
            sr.sortingLayerName = "Background";
            sr.sortingOrder = LavaOrder;

            var book = GetOrAdd<SpriteFlipbook>(root);
            var so = new SerializedObject(book);
            SerializedProperty list = so.FindProperty("frames");
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("fps").floatValue = p.fps > 0 ? p.fps : 8f;
            so.FindProperty("randomStart").boolValue = true;
            so.FindProperty("fadeIn").floatValue = 0f;
            so.FindProperty("fadeOut").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Kollision = Lochrand (2 px eingerueckt): der Keks steht auf der
            // Kruste, faellt aber nicht hinein. Gegner (eigene Layer) laufen drueber.
            var poly = GetOrAdd<PolygonCollider2D>(root);
            poly.isTrigger = false;
            var pts = new Vector2[p.colliderX.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(p.colliderX[i], p.colliderY[i]);
            poly.pathCount = 1;
            poly.SetPath(0, pts);

            GetOrAdd<MixerBlocker>(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);

            result[p.name] = saved;
        }
        return result;
    }

    /// <summary>vulkan_lava.json von Hand lesen - JsonUtility kann keine verschachtelten Punktlisten.</summary>
    private static List<PoolInfo> ReadPools()
    {
        var pools = new List<PoolInfo>();
        var root = MiniJson.Parse(File.ReadAllText(LavaInfoPath)) as Dictionary<string, object>;
        if (root == null || !(root["pools"] is List<object> list)) return pools;

        foreach (Dictionary<string, object> d in list.OfType<Dictionary<string, object>>())
        {
            var pts = (List<object>)d["collider"];
            var info = new PoolInfo
            {
                name = (string)d["name"],
                frames = Convert.ToInt32(d["frames"]),
                fps = Convert.ToSingle(d["fps"]),
                colliderX = new float[pts.Count],
                colliderY = new float[pts.Count],
            };
            for (int i = 0; i < pts.Count; i++)
            {
                var xy = (List<object>)pts[i];
                info.colliderX[i] = Convert.ToSingle(xy[0]);
                info.colliderY[i] = Convert.ToSingle(xy[1]);
            }
            pools.Add(info);
        }
        return pools;
    }

    // --- 3. Szene --------------------------------------------------------------

    private static void EnsureMapObject(Scene scene)
    {
        GameObject map = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<MapDefinition>() != null);
        if (map != null) return;

        map = new GameObject("Map");
        SceneManager.MoveGameObjectToScene(map, scene);
        var def = map.AddComponent<MapDefinition>();
        map.AddComponent<MapBootstrap>();

        var so = new SerializedObject(def);
        so.FindProperty("mapName").stringValue = WorldName;
        so.FindProperty("legacyMapId").intValue = MapId;
        // Noch kein eigener Wellenplan: leer = Plan zum Weltnamen, den
        // WavePlans nicht kennt -> Kuechenplan (World1). Kommt in Schritt 2.
        so.FindProperty("planId").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int BuildWorld(Scene scene, Tile[] tiles, Dictionary<string, GameObject> pools)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "Grid").ToArray())
            UnityEngine.Object.DestroyImmediate(old);

        var layout = MiniJson.Parse(File.ReadAllText(LayoutPath)) as Dictionary<string, object>;
        int width = Convert.ToInt32(layout["width"]);
        int height = Convert.ToInt32(layout["height"]);
        int minX = Convert.ToInt32(layout["minX"]);
        int minY = Convert.ToInt32(layout["minY"]);
        var chunkData = (List<object>)layout["chunks"];

        var gridGo = new GameObject("Grid");
        SceneManager.MoveGameObjectToScene(gridGo, scene);
        gridGo.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);

        var worldGo = new GameObject(WorldName);
        worldGo.transform.SetParent(gridGo.transform, false);
        var world = worldGo.AddComponent<WorldManager3x3>();
        world.chunkWidth = ChunkWidth;
        world.chunkHeight = ChunkHeight;

        var spawner = worldGo.AddComponent<RandomObjectSpawner3x3>();
        GameObject mixer = FindMixerPrefab();
        spawner.spawnablePrefabs = mixer != null ? new[] { mixer } : new GameObject[0];
        spawner.blockSize = 20f;
        spawner.spawnRadiusInBlocks = 5;
        spawner.maxObjectsPerBlock = 1;
        if (mixer == null) Debug.LogWarning("[Vulkan] Kein Mixer-Prefab gefunden - die Welt hat keine Mixer.");

        int placed = 0;
        var flat = new GameObject[9];
        for (int i = 0; i < 9; i++)
        {
            var chunk = new GameObject($"{WorldName}_Floor ({i + 1})");
            chunk.transform.SetParent(worldGo.transform, false);
            int column = i % 3, row = i / 3;
            chunk.transform.localPosition = new Vector3((column - 1) * ChunkWidth, (1 - row) * ChunkHeight, 0f);

            var tilemap = chunk.AddComponent<Tilemap>();
            var renderer = chunk.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = FloorOrder;

            var data = (Dictionary<string, object>)chunkData[i];
            var rows = (List<object>)data["tiles"];
            var cells = new TileBase[width * height];
            for (int y = 0; y < height; y++)
            {
                var r = (List<object>)rows[y];
                for (int x = 0; x < width; x++)
                {
                    int idx = Convert.ToInt32(r[x]);
                    cells[y * width + x] = idx >= 0 && idx < tiles.Length ? tiles[idx] : tiles[0];
                }
            }
            tilemap.SetTilesBlock(new BoundsInt(minX, minY, 0, width, height, 1), cells);
            tilemap.CompressBounds();

            var props = new GameObject(WorldGenerator.PropContainerName);
            props.transform.SetParent(chunk.transform, false);
            foreach (Dictionary<string, object> p in ((List<object>)data["pools"]).OfType<Dictionary<string, object>>())
            {
                if (!pools.TryGetValue((string)p["name"], out GameObject prefab)) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.transform.SetParent(props.transform, false);
                inst.transform.localPosition = new Vector3(Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]), 0f);
                placed++;
            }

            var shuffle = chunk.AddComponent<ChunkPropRandomizer>();
            shuffle.propContainer = props.transform;
            shuffle.chunkWidth = ChunkWidth;
            shuffle.chunkHeight = ChunkHeight;
            shuffle.edgeMargin = 1.5f;
            shuffle.minDistance = PoolGap;
            shuffle.spacingByBounds = true;
            shuffle.avoidMixers = true;
            shuffle.clumping = 0.25f;
            shuffle.clumpSize = 14f;
            shuffle.scaleRange = Vector2.one;      // Pixelart: nie skalieren
            shuffle.randomFlipX = false;           // der Collider spiegelt nicht mit
            shuffle.countJitter = 0.25f;
            shuffle.editorScaleMid = 1f;

            flat[i] = chunk;
        }
        world.chunksFlat = flat;
        return placed;
    }

    private static GameObject FindMixerPrefab()
    {
        string path = AssetDatabase.GUIDToAssetPath(MixerPrefabGuid);
        GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null && MixerObject.IsMixer(prefab)) return prefab;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && MixerObject.IsMixer(go)) return go;
        }
        return null;
    }

    // --- 4. Build Settings + Hub ------------------------------------------------

    private static bool AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return false;

        // direkt hinter die anderen Map-Szenen
        int at = scenes.FindLastIndex(s => s.path.StartsWith("Assets/Scenes/Maps/"));
        scenes.Insert(at < 0 ? scenes.Count : at + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }

    private static string SetupHubLevel()
    {
        if (!File.Exists(HubPath)) return "hub.unity nicht gefunden - Levelauswahl nicht angefasst.";

        Scene hub = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Single);
        HubLevelSelectUI ui = hub.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<HubLevelSelectUI>(true))
            .FirstOrDefault();
        if (ui == null) return "Keine HubLevelSelectUI im Hub - Levelauswahl nicht angefasst.";

        var so = new SerializedObject(ui);
        SerializedProperty levels = so.FindProperty("levels");
        if (levels.arraySize <= LevelSlot) levels.arraySize = LevelSlot + 1;
        SerializedProperty e = levels.GetArrayElementAtIndex(LevelSlot);

        e.FindPropertyRelative("displayName").stringValue = "Vulkan";
        e.FindPropertyRelative("description").stringValue = "Heisse Asche, kochende Lava. Nicht reinfallen!";
        e.FindPropertyRelative("preview").objectReferenceValue = LoadSprite(PreviewPath);
        e.FindPropertyRelative("previewWide").objectReferenceValue = LoadSprite(PreviewWidePath);
        e.FindPropertyRelative("mapId").intValue = MapId;
        e.FindPropertyRelative("sceneToLoad").stringValue = "";
        e.FindPropertyRelative("ambience").enumValueIndex = (int)HubLevelSelectUI.Ambience.Volcano;
        // planId und Sperren bleiben, wie sie im Hub stehen.
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(hub);
        EditorSceneManager.SaveScene(hub);
        return "Level 5 im Hub zeigt auf Map_World4.";
    }

    private static Sprite LoadSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    // --- Kleinkram -----------------------------------------------------------------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static string Title(string name)
    {
        // klein_a -> Klein_A
        return string.Join("_", name.Split('_').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    /// <summary>Kleiner JSON-Leser (Objekte, Listen, Zahlen, Text, true/false/null).</summary>
    private static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            return Value(text, ref i);
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    string key = (string)Value(s, ref i);
                    Skip(s, ref i);
                    i++; // :
                    d[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i++] == '}') return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i++] == ']') return l;
                }
            }
            if (c == '"')
            {
                var sb = new System.Text.StringBuilder();
                i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
