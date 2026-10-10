using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Baut den Lebkuchen-Geisterwald (Level 4, Szene Map_World6) aus den Python-Bildern:
///  1. ein Tile je Sprite aus geist_boden.png (Kakao-Erde + Lebkuchen-Pflaster),
///  2. ein Prefab je Prop: Grundbild (beleuchtet), bei Leuchtprops ein Kind
///     "Glow" mit Sprite-Unlit-Default (Flammen, Kuerbisgesichter, Pilzhuete,
///     Trank - bleibt im Dunkeln hell, beim Kessel per SpriteFlipbook animiert)
///     und ein Kind "Licht" (Light2D, Werte aus geist_props.json) plus
///     <see cref="LichtFlackern"/>. Stehende Props: Fuss-Kollision,
///     MixerBlocker, SortingGroup (Glow sortiert mit dem Prop, nicht davor).
///     Flache Props (Laub, Pilzring, Pfuetze) liegen im Background-Layer.
///  3. die Szene: 3x3-Endloswelt wie Eis und Vulkan (WorldManager3x3 +
///     Mixer-Spawner), jeder Chunk fertig belegt aus geist_layout.json; der
///     ChunkPropRandomizer stellt beim Umsetzen nie ein Hindernis auf den Weg
///     (avoidTiles = alle Tiles mit Pflaster). Am Map-Objekt haengt der
///     <see cref="Geisterwald"/> (Nacht, Spielerlicht, Nebel, Irrlichter,
///     Zuckergeister, Fledermaeuse),
///  4. traegt die Szene in die Build Settings ein und legt Level 4 in der
///     Levelauswahl des Hubs auf die neue Welt.
///
/// Wiederholbar: Tiles und Prefabs werden aktualisiert, die Welt (Grid) wird
/// neu gebaut, das Map-Objekt bleibt stehen (Musik/Wellenplan bleiben).
/// Bilder: Tools/geist_props.py, dann geist_boden.py, geist_atmo.py, geist_vorschau.py;
/// Musik: Tools/geist_musik.py (Notenblatt in SongSheet.cs, Eintrag "geist").
/// </summary>
public static class GeistBuilder
{
    private const string TileSheet = "Assets/Art/Tiles_Geist/geist_boden.png";
    private const string TileFolder = "Assets/Art/Tiles_Geist/Tiles";
    private const string LayoutPath = "Assets/Art/Tiles_Geist/geist_layout.json";
    private const string ArtFolder = "Assets/Art/World-Objects/Geist";
    private const string AtmoFolder = ArtFolder + "/Atmo";
    private const string InfoPath = ArtFolder + "/geist_props.json";
    private const string PrefabFolder = "Assets/Prefabs/MapObjects/Geist";
    private const string ScenePath = "Assets/Scenes/Maps/Map_World6.unity";
    private const string HubPath = "Assets/Scenes/hub.unity";
    private const string PreviewPath = "Assets/Art/new/Hub/level_preview_geist.png";
    private const string PreviewWidePath = "Assets/Art/new/Hub/level_preview_geist_wide.png";
    private const string MusicPath = "Assets/music/geist.wav";

    // Mixer-Prefab des Wald-Spawners (wie im VulkanBuilder)
    private const string MixerPrefabGuid = "29b3d2893f45c544695f29b321342ab2";

    private const int MapId = 6;
    private const int LevelSlot = 3;               // Station 4 der Levelauswahl
    private const string WorldName = "World6";
    private const float ChunkWidth = 52f;
    private const float ChunkHeight = 40f;
    private const float Ppu = 32f;
    private const int FloorOrder = 5;              // wie Wald-, Vulkan- und Eisboden
    private const int FlatOrder = 10;              // flach im Boden, unter Gegnern/XP
    private const int PropLayer = 3;               // "Baundaries": haelt den Spieler, Gegner laufen drueber
    private const float Gap = 1.2f;                // wie GAP in Tools/geist_boden.py

    private class Prop
    {
        public string name;
        public bool flat;
        public float colliderW;
        public float colliderH;
        public int glowFrames;
        public float glowFps;
        public Dictionary<string, object> light;
    }

    [MenuItem("Tools/Welt/Geisterwald einrichten", false, 123)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorUtility.DisplayDialog("Geisterwald", Build(), "Ok");
    }

    /// <summary>Unity -batchmode -executeMethod GeistBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[Geist] " + Build());
    }

    internal static string Build()
    {
        AssetDatabase.Refresh();
        if (!File.Exists(LayoutPath) || !File.Exists(InfoPath))
            return "geist_layout.json oder geist_props.json fehlt - erst Tools/geist_props.py und Tools/geist_boden.py laufen lassen.";

        // Szene zuerst oeffnen bzw. anlegen: OpenScene entlaedt unbenutzte Assets,
        // vorher geladene Tiles waeren danach kaputt (siehe KuecheBuilder).
        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Tile[] tiles = BuildTiles();
        if (tiles == null) return "Keine Sprites in " + TileSheet + ".";

        Material unlit = FindUnlitMaterial();
        if (unlit == null) return "Sprite-Unlit-Default nicht gefunden - URP-Paket fehlt?";

        var info = MiniJson.Parse(File.ReadAllText(InfoPath)) as Dictionary<string, object>;
        Dictionary<string, GameObject> prefabs = BuildPrefabs(info, unlit);
        AssetDatabase.SaveAssets();
        if (prefabs.Count == 0) return "Keine Prefabs gebaut - Bilder fehlen?";

        EnsureMapObject(scene, unlit);
        int placed = BuildWorld(scene, tiles, prefabs);
        EditorSceneManager.MarkSceneDirty(scene);
        EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, ScenePath);

        bool added = AddToBuildSettings();
        string hub = SetupHubLevel();

        return $"{tiles.Length} Tiles, {prefabs.Count} Prefabs, {placed} Objekte in 9 Chunks. " +
               (added ? "Szene in die Build Settings eingetragen. " : "") + hub;
    }

    private static Material FindUnlitMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (mat != null) return mat;
        foreach (string guid in AssetDatabase.FindAssets("Sprite-Unlit-Default t:Material"))
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null) return mat;
        }
        return null;
    }

    // --- 1. Tiles ------------------------------------------------------------

    private static Tile[] BuildTiles()
    {
        Sprite[] sprites = LoadStrip(TileSheet);
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

    private static Sprite[] LoadStrip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();

    // --- 2. Prefabs ------------------------------------------------------------

    private static Dictionary<string, GameObject> BuildPrefabs(Dictionary<string, object> info, Material unlit)
    {
        var result = new Dictionary<string, GameObject>();
        EnsureFolder(PrefabFolder);

        foreach (Dictionary<string, object> d in ((List<object>)info["props"]).OfType<Dictionary<string, object>>())
        {
            var p = new Prop
            {
                name = (string)d["name"],
                flat = (bool)d["flat"],
                colliderW = Convert.ToSingle(d["colliderW"]),
                colliderH = Convert.ToSingle(d["colliderH"]),
                glowFrames = Convert.ToInt32(d["glowFrames"]),
                glowFps = Convert.ToSingle(d["glowFps"]),
                light = d["light"] as Dictionary<string, object>,
            };
            string artPath = $"{ArtFolder}/geist_{p.name}.png";
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogWarning("[Geist] Kein Sprite: " + artPath);
                continue;
            }

            string prefabPath = $"{PrefabFolder}/Geist_{Title(p.name)}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("Geist_" + Title(p.name));

            string layer = p.flat ? "Background" : "Objects";
            int order = p.flat ? FlatOrder : 0;

            var sr = GetOrAdd<SpriteRenderer>(root);
            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            if (p.flat)
            {
                root.layer = 0;
                RemoveIfPresent<BoxCollider2D>(root);
                RemoveIfPresent<MixerBlocker>(root);
                RemoveIfPresent<SortingGroup>(root);
            }
            else
            {
                root.layer = PropLayer;
                // Kollision nur am Fuss: der Keks laeuft hinter dem Baum vorbei,
                // stoesst aber unten dagegen. Mindestens 12 px tief (wie Kueche/Eis).
                var box = GetOrAdd<BoxCollider2D>(root);
                box.isTrigger = false;
                box.size = new Vector2(p.colliderW / Ppu, Mathf.Max(p.colliderH, 12f) / Ppu);
                box.offset = new Vector2(0f, box.size.y * 0.3f);
                GetOrAdd<MixerBlocker>(root);

                // Glow-Kind sortiert mit dem Prop - sonst laege die Flamme vor jedem Gegner.
                if (p.glowFrames > 0)
                {
                    var group = GetOrAdd<SortingGroup>(root);
                    group.sortingLayerName = layer;
                    group.sortingOrder = 0;
                }
                else RemoveIfPresent<SortingGroup>(root);
            }

            // Glow
            Transform glowTr = root.transform.Find("Glow");
            SpriteRenderer glowSr = null;
            if (p.glowFrames > 0)
            {
                Sprite[] frames = LoadStrip($"{ArtFolder}/geist_{p.name}_glow.png");
                if (frames.Length == 0) Debug.LogWarning("[Geist] Kein Glow-Bild fuer " + p.name);
                else
                {
                    if (glowTr == null)
                    {
                        glowTr = new GameObject("Glow").transform;
                        glowTr.SetParent(root.transform, false);
                    }
                    glowTr.localPosition = Vector3.zero;
                    glowTr.gameObject.layer = 0;
                    glowSr = GetOrAdd<SpriteRenderer>(glowTr.gameObject);
                    glowSr.sprite = frames[0];
                    glowSr.sharedMaterial = unlit;
                    glowSr.sortingLayerName = layer;
                    glowSr.sortingOrder = order + 1;
                    glowSr.spriteSortPoint = SpriteSortPoint.Pivot;

                    if (frames.Length > 1)
                    {
                        var book = GetOrAdd<SpriteFlipbook>(glowTr.gameObject);
                        var so = new SerializedObject(book);
                        SerializedProperty list = so.FindProperty("frames");
                        list.arraySize = frames.Length;
                        for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                        so.FindProperty("fps").floatValue = p.glowFps > 0 ? p.glowFps : 8f;
                        so.FindProperty("randomStart").boolValue = true;
                        so.FindProperty("fadeIn").floatValue = 0f;
                        so.FindProperty("fadeOut").floatValue = 0f;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else RemoveIfPresent<SpriteFlipbook>(glowTr.gameObject);
                }
            }
            else if (glowTr != null) UnityEngine.Object.DestroyImmediate(glowTr.gameObject);

            // Licht
            Transform lightTr = root.transform.Find("Licht");
            Light2D light = null;
            if (p.light != null)
            {
                if (lightTr == null)
                {
                    lightTr = new GameObject("Licht").transform;
                    lightTr.SetParent(root.transform, false);
                }
                lightTr.localPosition = new Vector3(F(p.light, "x"), F(p.light, "y"), 0f);
                light = GetOrAdd<Light2D>(lightTr.gameObject);
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(F(p.light, "r"), F(p.light, "g"), F(p.light, "b"));
                light.intensity = F(p.light, "intensity");
                light.pointLightOuterRadius = F(p.light, "radius");
                light.pointLightInnerRadius = F(p.light, "inner");
                light.falloffIntensity = 0.6f;
                light.shadowsEnabled = false;
                light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();

                var flicker = GetOrAdd<LichtFlackern>(root);
                var fso = new SerializedObject(flicker);
                fso.FindProperty("licht").objectReferenceValue = light;
                fso.FindProperty("glow").objectReferenceValue = glowSr;
                fso.FindProperty("amount").floatValue = F(p.light, "flicker");
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                if (lightTr != null) UnityEngine.Object.DestroyImmediate(lightTr.gameObject);
                RemoveIfPresent<LichtFlackern>(root);
            }

            result[p.name] = Save(root, existing, prefabPath);
        }
        return result;
    }

    private static float F(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out object v) && v != null ? Convert.ToSingle(v) : 0f;

    private static GameObject Save(GameObject root, GameObject existing, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing != null) PrefabUtility.UnloadPrefabContents(root);
        else UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    // --- 3. Szene --------------------------------------------------------------

    private static void EnsureMapObject(Scene scene, Material unlit)
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

        // Lauf-Musik: "Mitternacht im Lebkuchenwald" (Tools/geist_musik.py), nur
        // wenn noch keine andere eingetragen ist.
        var defSo = new SerializedObject(map.GetComponent<MapDefinition>());
        SerializedProperty music = defSo.FindProperty("music");
        if (music.objectReferenceValue == null)
        {
            music.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            defSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Frisch anlegen: so gelten immer die Standardwerte aus Geisterwald.cs
        // (Nachtfarbe, Spielerlicht ...), nicht die beim ersten Bauen gespeicherten.
        RemoveIfPresent<Geisterwald>(map);
        var wald = map.AddComponent<Geisterwald>();
        var w = new SerializedObject(wald);
        w.FindProperty("unlitMaterial").objectReferenceValue = unlit;
        w.FindProperty("litMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        SetSprites(w.FindProperty("fogSprites"), new[]
        {
            FirstSprite($"{AtmoFolder}/geist_nebel_0.png"),
            FirstSprite($"{AtmoFolder}/geist_nebel_1.png"),
            FirstSprite($"{AtmoFolder}/geist_nebel_2.png"),
        });
        SetSprites(w.FindProperty("ghostFrames"), LoadStrip($"{AtmoFolder}/geist_zuckergeist.png"));
        SetSprites(w.FindProperty("wispSprites"), LoadStrip($"{AtmoFolder}/geist_irrlicht.png"));
        SetSprites(w.FindProperty("batFrames"), LoadStrip($"{AtmoFolder}/geist_fledermaus.png"));
        w.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite FirstSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private static void SetSprites(SerializedProperty list, Sprite[] sprites)
    {
        sprites = sprites.Where(s => s != null).ToArray();
        if (sprites.Length == 0) Debug.LogWarning("[Geist] Bilder fehlen fuer " + list.name + " - Tools/geist_atmo.py laufen lassen.");
        list.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
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
        TileBase[] pathTiles = ((List<object>)layout["pathTiles"])
            .Select(o => Convert.ToInt32(o)).Where(i => i >= 0 && i < tiles.Length)
            .Select(i => (TileBase)tiles[i]).ToArray();

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
        if (mixer == null) Debug.LogWarning("[Geist] Kein Mixer-Prefab gefunden - die Welt hat keine Mixer.");

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
            shuffle.avoidTiles = pathTiles;
            shuffle.clumping = 0.25f;
            shuffle.clumpSize = 12f;
            shuffle.scaleRange = Vector2.one;      // Pixelart: nie skalieren
            shuffle.randomFlipX = false;           // Glow-Kind wuerde nicht mitspiegeln
            shuffle.countJitter = 0.15f;
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

        e.FindPropertyRelative("displayName").stringValue = "Geisterwald";
        e.FindPropertyRelative("description").stringValue = "Lebkuchenbaeume, Kuerbislaternen und Irrlichter. Bleib im Licht!";
        e.FindPropertyRelative("preview").objectReferenceValue = FirstSprite(PreviewPath);
        e.FindPropertyRelative("previewWide").objectReferenceValue = FirstSprite(PreviewWidePath);
        e.FindPropertyRelative("mapId").intValue = MapId;
        e.FindPropertyRelative("sceneToLoad").stringValue = "";
        e.FindPropertyRelative("ambience").enumValueIndex = (int)HubLevelSelectUI.Ambience.Ghost;
        // planId und Sperren bleiben, wie sie im Hub stehen.
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(hub);
        EditorSceneManager.SaveScene(hub);
        return "Level 4 im Hub zeigt auf Map_World6.";
    }

    // --- Kleinkram -----------------------------------------------------------------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void RemoveIfPresent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) UnityEngine.Object.DestroyImmediate(c, true);
    }

    private static string Title(string name)
    {
        // grabkreuz -> Grabkreuz
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
