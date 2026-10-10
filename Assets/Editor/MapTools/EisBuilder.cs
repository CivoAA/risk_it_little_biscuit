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
/// Baut die Eiswelt (Level 3, Szene Map_World5) aus den Python-Bildern:
///  1. ein Tile je Sprite aus eis_boden.png (Vanilleschnee + Waffel),
///  2. ein Prefab je stehendem Prop (Waffeltanne, Eiskugeln, ... - Sprite,
///     Fuss-Kollision, MixerBlocker) und je Glatteis-Pfuetze (Flipbook +
///     <see cref="Glatteis"/>, kein Collider - man rutscht drueber),
///  3. die Szene: 3x3-Endloswelt wie Wald und Vulkan (WorldManager3x3 +
///     Mixer-Spawner), jeder Chunk fertig belegt aus eis_layout.json, die
///     der ChunkPropRandomizer beim Umsetzen neu verteilt; am Map-Objekt
///     haengt der <see cref="Schneefall"/>,
///  4. traegt die Szene in die Build Settings ein und legt Level 3 in der
///     Levelauswahl des Hubs auf die neue Welt.
///
/// Wiederholbar: Tiles und Prefabs werden aktualisiert, die Welt (Grid) wird
/// neu gebaut, das Map-Objekt bleibt stehen (Musik/Wellenplan bleiben).
/// Bilder: Tools/eis_props.py, dann Tools/eis_boden.py, Vorschau Tools/eis_vorschau.py.
/// </summary>
public static class EisBuilder
{
    private const string TileSheet = "Assets/Art/Tiles_Eis/eis_boden.png";
    private const string TileFolder = "Assets/Art/Tiles_Eis/Tiles";
    private const string LayoutPath = "Assets/Art/Tiles_Eis/eis_layout.json";
    private const string ArtFolder = "Assets/Art/World-Objects/Eis";
    private const string InfoPath = ArtFolder + "/eis_props.json";
    private const string PrefabFolder = "Assets/Prefabs/MapObjects/Eis";
    private const string ScenePath = "Assets/Scenes/Maps/Map_World5.unity";
    private const string HubPath = "Assets/Scenes/hub.unity";
    private const string PreviewPath = "Assets/Art/new/Hub/level_preview_eis.png";
    private const string PreviewWidePath = "Assets/Art/new/Hub/level_preview_eis_wide.png";

    // Mixer-Prefab des Wald-Spawners (wie im VulkanBuilder)
    private const string MixerPrefabGuid = "29b3d2893f45c544695f29b321342ab2";

    private const int MapId = 5;
    private const int LevelSlot = 2;               // Station 3 der Levelauswahl
    private const string WorldName = "World5";
    private const float ChunkWidth = 52f;
    private const float ChunkHeight = 40f;
    private const float Ppu = 32f;
    private const int FloorOrder = 5;              // wie Wald- und Vulkanboden
    private const int IceOrder = 10;               // flach im Boden, unter Gegnern/XP
    private const int PropLayer = 3;               // "Baundaries": haelt den Spieler, Gegner laufen drueber
    private const float Gap = 1.4f;                // wie GAP in Tools/eis_boden.py

    private const string PondPrefix = "glatteis_";

    private class Pond
    {
        public string name;
        public float fps;
        public Vector2[] outline;
    }

    private class Prop
    {
        public string name;
        public float colliderW;
        public float colliderH;
    }

    [MenuItem("Tools/Welt/Eis einrichten", false, 122)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorUtility.DisplayDialog("Eis", Build(), "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod EisBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Eis] " + Build());
    }

    internal static string Build()
    {
        AssetDatabase.Refresh();
        if (!File.Exists(LayoutPath) || !File.Exists(InfoPath))
            return "eis_layout.json oder eis_props.json fehlt - erst Tools/eis_props.py und Tools/eis_boden.py laufen lassen.";

        // Szene zuerst oeffnen bzw. anlegen: OpenScene entlaedt unbenutzte Assets,
        // vorher geladene Tiles waeren danach kaputt (siehe KuecheBuilder).
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Tile[] tiles = BuildTiles();
        if (tiles == null) return "Keine Sprites in " + TileSheet + ".";

        var info = MiniJson.Parse(File.ReadAllText(InfoPath)) as Dictionary<string, object>;
        Dictionary<string, GameObject> prefabs = BuildPropPrefabs(info);
        int propCount = prefabs.Count;
        foreach (var kv in BuildPondPrefabs(info)) prefabs[kv.Key] = kv.Value;
        AssetDatabase.SaveAssets();
        if (prefabs.Count == 0) return "Keine Prefabs gebaut - Bilder fehlen?";

        EnsureMapObject(scene);
        int placed = BuildWorld(scene, tiles, prefabs);
        EditorSceneManager.MarkSceneDirty(scene);
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);

        bool added = AddToBuildSettings();
        string hub = SetupHubLevel();

        return $"{tiles.Length} Tiles, {propCount} Props + {prefabs.Count - propCount} Glatteis-Prefabs, " +
               $"{placed} Objekte in 9 Chunks. " + (added ? "Szene in die Build Settings eingetragen. " : "") + hub;
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

    // --- 2. Prefabs ------------------------------------------------------------

    private static Dictionary<string, GameObject> BuildPropPrefabs(Dictionary<string, object> info)
    {
        var result = new Dictionary<string, GameObject>();
        EnsureFolder(PrefabFolder);

        foreach (Dictionary<string, object> d in ((List<object>)info["props"]).OfType<Dictionary<string, object>>())
        {
            var p = new Prop
            {
                name = (string)d["name"],
                colliderW = Convert.ToSingle(d["colliderW"]),
                colliderH = Convert.ToSingle(d["colliderH"]),
            };
            string artPath = $"{ArtFolder}/eis_{p.name}.png";
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogWarning("[Eis] Kein Sprite: " + artPath);
                continue;
            }

            string prefabPath = $"{PrefabFolder}/Eis_{Title(p.name)}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Eis_" + Title(p.name));

            root.layer = PropLayer;

            var sr = GetOrAdd<SpriteRenderer>(root);
            sr.sprite = sprite;
            sr.sortingLayerName = "Objects";
            sr.sortingOrder = 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            // Kollision nur am Fuss: der Keks laeuft hinter der Tanne vorbei,
            // stoesst aber unten dagegen. Mindestens 12 px tief (wie Kueche).
            var box = GetOrAdd<BoxCollider2D>(root);
            box.isTrigger = false;
            box.size = new Vector2(p.colliderW / Ppu, Mathf.Max(p.colliderH, 12f) / Ppu);
            box.offset = new Vector2(0f, box.size.y * 0.3f);

            GetOrAdd<MixerBlocker>(root);

            result[p.name] = Save(root, existing, prefabPath);
        }
        return result;
    }

    private static Dictionary<string, GameObject> BuildPondPrefabs(Dictionary<string, object> info)
    {
        var result = new Dictionary<string, GameObject>();
        EnsureFolder(PrefabFolder);

        foreach (Dictionary<string, object> d in ((List<object>)info["ponds"]).OfType<Dictionary<string, object>>())
        {
            var pts = (List<object>)d["collider"];
            var p = new Pond
            {
                name = (string)d["name"],
                fps = Convert.ToSingle(d["fps"]),
                outline = pts.Cast<List<object>>()
                    .Select(xy => new Vector2(Convert.ToSingle(xy[0]), Convert.ToSingle(xy[1]))).ToArray(),
            };

            string artPath = $"{ArtFolder}/eis_{PondPrefix}{p.name}.png";
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
                .ToArray();
            if (frames.Length == 0)
            {
                Debug.LogWarning("[Eis] Keine Sprites: " + artPath);
                continue;
            }

            string prefabPath = $"{PrefabFolder}/Eis_Glatteis_{Title(p.name)}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Eis_Glatteis_" + Title(p.name));

            root.layer = 0;   // kein Collider, die Ebene ist egal

            var sr = GetOrAdd<SpriteRenderer>(root);
            sr.sprite = frames[0];
            sr.sortingLayerName = "Background";
            sr.sortingOrder = IceOrder;

            var book = GetOrAdd<SpriteFlipbook>(root);
            var so = new SerializedObject(book);
            SerializedProperty list = so.FindProperty("frames");
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("fps").floatValue = p.fps > 0 ? p.fps : 9f;
            so.FindProperty("randomStart").boolValue = true;
            so.FindProperty("fadeIn").floatValue = 0f;
            so.FindProperty("fadeOut").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var ice = GetOrAdd<Glatteis>(root);
            var iso = new SerializedObject(ice);
            SerializedProperty outline = iso.FindProperty("outline");
            outline.arraySize = p.outline.Length;
            for (int i = 0; i < p.outline.Length; i++) outline.GetArrayElementAtIndex(i).vector2Value = p.outline[i];
            iso.ApplyModifiedPropertiesWithoutUndo();

            // Kein Mixer mitten auf dem Eis
            GetOrAdd<MixerBlocker>(root);

            result[PondPrefix + p.name] = Save(root, existing, prefabPath);
        }
        return result;
    }

    private static GameObject Save(GameObject root, GameObject existing, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing != null) PrefabUtility.UnloadPrefabContents(root);
        else UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    // --- 3. Szene --------------------------------------------------------------

    private static void EnsureMapObject(Scene scene)
    {
        GameObject map = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<MapDefinition>() != null);
        if (map == null)
        {
            map = new GameObject("Map");
            SceneManager.MoveGameObjectToScene(map, scene);
            var def = map.AddComponent<MapDefinition>();
            map.AddComponent<MapBootstrap>();

            var so = new SerializedObject(def);
            so.FindProperty("mapName").stringValue = WorldName;
            so.FindProperty("legacyMapId").intValue = MapId;
            // Noch kein eigener Wellenplan: leer = Plan zum Weltnamen, den
            // WavePlans nicht kennt -> Kuechenplan (World1), wie beim Vulkan.
            so.FindProperty("planId").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GetOrAdd<Schneefall>(map);
    }

    private static int BuildWorld(Scene scene, Tile[] tiles, Dictionary<string, GameObject> prefabs)
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
        if (mixer == null) Debug.LogWarning("[Eis] Kein Mixer-Prefab gefunden - die Welt hat keine Mixer.");

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
            foreach (Dictionary<string, object> p in ((List<object>)data["props"]).OfType<Dictionary<string, object>>())
            {
                if (!prefabs.TryGetValue((string)p["name"], out GameObject prefab)) continue;
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
            shuffle.minDistance = Gap;
            shuffle.spacingByBounds = true;
            shuffle.avoidMixers = true;
            shuffle.clumping = 0.3f;
            shuffle.clumpSize = 14f;
            shuffle.scaleRange = Vector2.one;      // Pixelart: nie skalieren
            shuffle.randomFlipX = false;           // das Eis-Polygon spiegelt nicht mit
            shuffle.countJitter = 0.2f;
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

        e.FindPropertyRelative("displayName").stringValue = "Eisgletscher";
        e.FindPropertyRelative("description").stringValue = "Vanilleschnee, Waffeltannen und spiegelglattes Eis. Wer bremst, verliert!";
        e.FindPropertyRelative("preview").objectReferenceValue = LoadSprite(PreviewPath);
        e.FindPropertyRelative("previewWide").objectReferenceValue = LoadSprite(PreviewWidePath);
        e.FindPropertyRelative("mapId").intValue = MapId;
        e.FindPropertyRelative("sceneToLoad").stringValue = "";
        e.FindPropertyRelative("ambience").enumValueIndex = (int)HubLevelSelectUI.Ambience.Snow;
        // planId und Sperren bleiben, wie sie im Hub stehen.
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(hub);
        EditorSceneManager.SaveScene(hub);
        return "Level 3 im Hub zeigt auf Map_World5.";
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
        // eis_am_stiel -> Eis_Am_Stiel
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
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
