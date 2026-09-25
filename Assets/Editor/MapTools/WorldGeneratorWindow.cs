using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Fenster unter Tools -> Welt -> Welt-Generator: waehlt eine Map-Szene, eine
/// Welt (World0..N) und ein Preset aus und fuellt damit die neun Chunks des
/// 3x3-Rasters mit zufaelligen Tiles und Props.
///
/// Die Map-Szenen (Assets/Scenes/Maps) sind der Master - das Fenster oeffnet
/// sie direkt, damit eine bestehende Welt nachbearbeitet werden kann. Neue
/// Tiles und Objekte werden ins Preset ERGAENZT, ohne die fertig eingestellten
/// Gruppen und Gewichte anzufassen.
/// </summary>
public class WorldGeneratorWindow : EditorWindow
{
    private const string PresetPrefKey = "WorldGenerator.Preset";
    private const string TileFolderPrefKey = "WorldGenerator.TileFolder";
    private const string PropFolderPrefKey = "WorldGenerator.PropFolder";
    private const string WorldPrefKey = "WorldGenerator.World";

    private const string DefaultTileFolder = "Assets/Art/Tiles_Juri/Nelly_Tiles";
    private const string DefaultPropFolder = "Assets/Prefabs/MapObjects";
    private const string PresetFolder = "Assets/Editor/MapTools/Presets";
    private const string MapSceneFolder = "Assets/Scenes/Maps";

    private WorldGenPreset preset;
    private Editor presetEditor;

    private string[] mapScenes = new string[0];
    private int mapSceneIndex;

    private WorldManager3x3[] worlds = new WorldManager3x3[0];
    private int worldIndex;

    private string tileFolder = DefaultTileFolder;
    private string propFolder = DefaultPropFolder;
    private string newWorldName = "World4";
    private string status = string.Empty;

    private Vector2 scroll;

    [MenuItem("Tools/Welt/Welt-Generator", false, 101)]
    public static void Open()
    {
        var window = GetWindow<WorldGeneratorWindow>("Welt-Generator");
        window.minSize = new Vector2(380f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        tileFolder = EditorPrefs.GetString(TileFolderPrefKey, DefaultTileFolder);
        propFolder = EditorPrefs.GetString(PropFolderPrefKey, DefaultPropFolder);

        string presetPath = EditorPrefs.GetString(PresetPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(presetPath))
            preset = AssetDatabase.LoadAssetAtPath<WorldGenPreset>(presetPath);

        RefreshMapScenes();
        RefreshWorlds();
        if (preset == null && SelectedWorld != null) AutoPickPreset(SelectedWorld);
    }

    private void OnProjectChange()
    {
        RefreshMapScenes();
        Repaint();
    }

    private void OnDisable()
    {
        if (presetEditor != null) DestroyImmediate(presetEditor);
    }

    private void OnHierarchyChange()
    {
        RefreshWorlds();
        Repaint();
    }

    private void RefreshWorlds()
    {
        worlds = FindObjectsByType<WorldManager3x3>(FindObjectsInactive.Include);
        System.Array.Sort(worlds, (a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        string wanted = EditorPrefs.GetString(WorldPrefKey, string.Empty);
        worldIndex = 0;
        for (int i = 0; i < worlds.Length; i++)
            if (worlds[i].name == wanted) worldIndex = i;
    }

    private WorldManager3x3 SelectedWorld =>
        worlds.Length > 0 && worldIndex >= 0 && worldIndex < worlds.Length ? worlds[worldIndex] : null;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Fuellt die neun Tilemaps einer Welt mit zufaelligen Tiles und verteilt Props (Baeume) darauf.\n" +
            "Der WorldManager3x3 schiebt die neun Chunks im Kreis - deshalb wirkt die Welt endlos, " +
            "egal wie weit der Spieler laeuft.", MessageType.Info);

        DrawMapSceneSection();
        EditorGUILayout.Space(8f);
        DrawWorldSection();
        EditorGUILayout.Space(8f);
        DrawPresetSection();

        if (preset != null)
        {
            EditorGUILayout.Space(8f);
            DrawHelperSection();
            EditorGUILayout.Space(8f);
            DrawPresetInspector();
            EditorGUILayout.Space(10f);
            DrawGenerateSection();
        }

        if (!string.IsNullOrEmpty(status))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------- Map-Szene

    private void RefreshMapScenes()
    {
        var paths = new List<string>();
        if (AssetDatabase.IsValidFolder(MapSceneFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SceneAsset", new[] { MapSceneFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        paths.Sort(EditorUtility.NaturalCompare);
        mapScenes = paths.ToArray();

        // Vorauswahl: die Map-Szene, die gerade offen ist.
        string active = SceneManager.GetActiveScene().path;
        int open = System.Array.IndexOf(mapScenes, active);
        if (open >= 0) mapSceneIndex = open;
        mapSceneIndex = Mathf.Clamp(mapSceneIndex, 0, Mathf.Max(0, mapScenes.Length - 1));
    }

    private void DrawMapSceneSection()
    {
        EditorGUILayout.LabelField("Map-Szene", EditorStyles.boldLabel);

        if (mapScenes.Length == 0)
        {
            EditorGUILayout.HelpBox($"Keine Szenen in {MapSceneFolder} gefunden.", MessageType.Warning);
            return;
        }

        var names = new string[mapScenes.Length];
        for (int i = 0; i < mapScenes.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(mapScenes[i]);
            names[i] = IsSceneOpen(mapScenes[i]) ? name + "  (offen)" : name;
        }

        string path = mapScenes[mapSceneIndex];
        bool isOpen = IsSceneOpen(path);

        using (new EditorGUILayout.HorizontalScope())
        {
            mapSceneIndex = EditorGUILayout.Popup("Szene", mapSceneIndex, names);
            path = mapScenes[mapSceneIndex];
            isOpen = IsSceneOpen(path);

            using (new EditorGUI.DisabledScope(isOpen))
            {
                if (GUILayout.Button("Oeffnen", GUILayout.Width(70f)))
                {
                    OpenMapScene(path);
                    GUIUtility.ExitGUI();
                }
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            using (new EditorGUI.DisabledScope(!isOpen || !scene.isDirty))
            {
                if (GUILayout.Button("Speichern", GUILayout.Width(75f)))
                {
                    EditorSceneManager.SaveScene(scene);
                    status = $"{scene.name} gespeichert.";
                }
            }
        }

        if (!isOpen)
        {
            EditorGUILayout.HelpBox(
                "Diese Map ist nicht geladen. \"Oeffnen\" laedt sie - danach steht ihre Welt unten zur Auswahl " +
                "und kann nachbearbeitet werden.", MessageType.None);
        }
    }

    private static bool IsSceneOpen(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded;
    }

    private void OpenMapScene(string path)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        RefreshWorlds();

        // Welt der frisch geoeffneten Szene waehlen und ihr Preset dazu.
        for (int i = 0; i < worlds.Length; i++)
        {
            if (worlds[i].gameObject.scene != scene) continue;
            SelectWorld(i);
            break;
        }

        status = SelectedWorld != null && SelectedWorld.gameObject.scene == scene
            ? $"{scene.name} geoeffnet - Welt {SelectedWorld.name} ausgewaehlt."
            : $"{scene.name} geoeffnet, aber keine Welt mit WorldManager3x3 darin gefunden.";
    }

    private void SelectWorld(int index)
    {
        worldIndex = index;
        var world = SelectedWorld;
        if (world == null) return;

        EditorPrefs.SetString(WorldPrefKey, world.name);
        AutoPickPreset(world);
    }

    /// <summary>Nimmt das Preset "&lt;Welt&gt;_Preset", falls es eins gibt.</summary>
    private void AutoPickPreset(WorldManager3x3 world)
    {
        string path = $"{PresetFolder}/{world.name}_Preset.asset";
        var match = AssetDatabase.LoadAssetAtPath<WorldGenPreset>(path);
        if (match == null || match == preset) return;

        preset = match;
        EditorPrefs.SetString(PresetPrefKey, path);
        if (presetEditor != null) { DestroyImmediate(presetEditor); presetEditor = null; }
    }

    // ------------------------------------------------------------------- Welt

    private void DrawWorldSection()
    {
        EditorGUILayout.LabelField("Welt", EditorStyles.boldLabel);

        if (worlds.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Keine Welt mit WorldManager3x3 in den geladenen Szenen gefunden. " +
                "Oben eine Map-Szene oeffnen oder unten eine neue Welt anlegen.", MessageType.Warning);
        }
        else
        {
            var names = new string[worlds.Length];
            for (int i = 0; i < worlds.Length; i++) names[i] = worlds[i].name;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("Ziel-Welt", worldIndex, names);
            if (EditorGUI.EndChangeCheck())
                SelectWorld(picked);

            var world = SelectedWorld;
            if (world != null)
            {
                int chunkCount = WorldGenerator.CollectChunks(world).Count;
                EditorGUILayout.LabelField(
                    "Chunks",
                    $"{chunkCount} / 9   ({world.chunkWidth} x {world.chunkHeight} Tiles pro Chunk)");

                if (chunkCount < 9)
                {
                    EditorGUILayout.HelpBox(
                        "Diese Welt hat keine neun Tilemap-Kinder. Das 3x3-Raster braucht genau neun.",
                        MessageType.Warning);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Chunks ausrichten & verknuepfen"))
                    {
                        Undo.SetCurrentGroupName("Chunks ausrichten");
                        WorldGenerator.AlignChunks(world);
                        status = $"{world.name}: Chunks im 3x3-Raster ausgerichtet und im WorldManager3x3 eingetragen.";
                    }

                    if (GUILayout.Button("Auswaehlen"))
                        Selection.activeGameObject = world.gameObject;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            newWorldName = EditorGUILayout.TextField("Neue Welt", newWorldName);
            if (GUILayout.Button("Anlegen", GUILayout.Width(80f)))
                CreateWorld();
        }
    }

    private void CreateWorld()
    {
        if (string.IsNullOrWhiteSpace(newWorldName))
        {
            status = "Bitte einen Namen fuer die neue Welt eingeben.";
            return;
        }

        var template = SelectedWorld;
        Transform parent = template != null ? template.transform.parent : null;
        if (parent == null)
        {
            var grid = FindAnyObjectByType<Grid>(FindObjectsInactive.Include);
            parent = grid != null ? grid.transform : null;
        }

        if (parent == null)
        {
            status = "Kein Grid in der Szene gefunden - die neue Welt braucht ein Grid als Elternobjekt.";
            return;
        }

        int group = Undo.GetCurrentGroup();
        var created = WorldGenerator.CreateWorld(parent, newWorldName, template);
        Undo.SetCurrentGroupName("Neue Welt anlegen");
        Undo.CollapseUndoOperations(group);

        RefreshWorlds();
        for (int i = 0; i < worlds.Length; i++)
            if (worlds[i] == created) worldIndex = i;

        Selection.activeGameObject = created.gameObject;
        status = $"{newWorldName} angelegt: neun leere Chunks unter {parent.name}. " +
                 "Nicht vergessen, die Welt im WorldSelector einzutragen.";
    }

    // ----------------------------------------------------------------- Preset

    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        preset = (WorldGenPreset)EditorGUILayout.ObjectField("Einstellungen", preset, typeof(WorldGenPreset), false);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(PresetPrefKey, preset != null ? AssetDatabase.GetAssetPath(preset) : string.Empty);
            if (presetEditor != null) { DestroyImmediate(presetEditor); presetEditor = null; }
        }

        if (preset == null)
        {
            EditorGUILayout.HelpBox(
                "Noch kein Preset. Ein Preset speichert Tile-Liste, Haeufigkeiten und Prop-Einstellungen - " +
                "pro Welt eins, dann bleibt jede Welt einstellbar.", MessageType.Info);

            if (GUILayout.Button("Neues Preset anlegen (inkl. Tiles und Prefabs aus den Ordnern)"))
                CreatePreset();
        }
    }

    private void CreatePreset()
    {
        if (!AssetDatabase.IsValidFolder(PresetFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor/MapTools"))
                AssetDatabase.CreateFolder("Assets/Editor", "MapTools");
            AssetDatabase.CreateFolder("Assets/Editor/MapTools", "Presets");
        }

        var world = SelectedWorld;
        string fileName = world != null ? $"{world.name}_Preset.asset" : "WorldGenPreset.asset";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{PresetFolder}/{fileName}");

        var asset = CreateInstance<WorldGenPreset>();
        AssetDatabase.CreateAsset(asset, path);

        preset = asset;
        LoadTilesFromFolder(false);
        LoadPropsFromFolder(false);

        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetString(PresetPrefKey, path);
        status = $"Preset angelegt: {path}";
    }

    private void DrawPresetInspector()
    {
        EditorGUILayout.LabelField("Einstellungen", EditorStyles.boldLabel);

        if (presetEditor == null || presetEditor.target != preset)
        {
            if (presetEditor != null) DestroyImmediate(presetEditor);
            presetEditor = Editor.CreateEditor(preset);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            presetEditor.OnInspectorGUI();
        }

        DrawDistributionPreview();
    }

    /// <summary>Zeigt, was die beiden Regler gerade bedeuten.</summary>
    private void DrawDistributionPreview()
    {
        var world = SelectedWorld;
        float chunkWidth = world != null ? world.chunkWidth : 52f;
        float chunkHeight = world != null ? world.chunkHeight : 40f;

        int patches = WorldGenerator.EstimatePatchCount(preset, chunkWidth, chunkHeight);
        float groupTotal = preset.TotalGroupWeight();

        var text = new System.Text.StringBuilder();
        text.Append($"Ca. {patches} Farbflecken pro Chunk (Durchmesser {preset.patchSize} Tiles).");

        if (groupTotal > 0f)
        {
            text.Append("   Davon:  ");
            for (int i = 0; i < preset.patchGroups.Count; i++)
            {
                var group = preset.patchGroups[i];
                if (group == null || group.weight <= 0f) continue;
                text.Append($"{group.name} {Mathf.RoundToInt(group.weight / groupTotal * 100f)}%   ");
            }
        }

        float backgroundTotal = WorldGenPreset.TotalTileWeight(preset.backgroundTiles);
        if (backgroundTotal > 0f)
        {
            text.Append("\nUntergrund:  ");
            for (int i = 0; i < preset.backgroundTiles.Count; i++)
            {
                var entry = preset.backgroundTiles[i];
                if (entry == null || entry.tile == null || entry.weight <= 0f) continue;
                text.Append($"{entry.tile.name} {Mathf.RoundToInt(entry.weight / backgroundTotal * 100f)}%   ");
            }
        }

        EditorGUILayout.LabelField(text.ToString(), EditorStyles.wordWrappedMiniLabel);
    }

    // ---------------------------------------------------------------- Helfer

    private void DrawHelperSection()
    {
        EditorGUILayout.LabelField("Listen fuellen", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "\"Ergaenzen\" nimmt nur dazu, was noch nicht im Preset steht (Gewicht 1) - Gruppen und Gewichte " +
            "bleiben wie sie sind. \"Ersetzen\" baut die Liste komplett neu.", EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            tileFolder = EditorGUILayout.TextField("Tile-Ordner", tileFolder);
            if (GUILayout.Button("...", GUILayout.Width(28f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Ordner mit Tiles", tileFolder, string.Empty);
                if (!string.IsNullOrEmpty(picked)) tileFolder = ToProjectPath(picked);
            }
            if (GUILayout.Button("Ergaenzen", GUILayout.Width(75f)))
                AddNewTilesFromFolder();
            if (GUILayout.Button("Ersetzen", GUILayout.Width(65f)) && ConfirmReplace("Tile-Listen"))
                LoadTilesFromFolder(true);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            propFolder = EditorGUILayout.TextField("Prefab-Ordner", propFolder);
            if (GUILayout.Button("...", GUILayout.Width(28f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Ordner mit Prefabs", propFolder, string.Empty);
                if (!string.IsNullOrEmpty(picked)) propFolder = ToProjectPath(picked);
            }
            if (GUILayout.Button("Ergaenzen", GUILayout.Width(75f)))
                AddNewPropsFromFolder();
            if (GUILayout.Button("Ersetzen", GUILayout.Width(65f)) && ConfirmReplace("Prop-Liste"))
                LoadPropsFromFolder(true);
        }

        if (GUILayout.Button("Markierte Sprites -> Prop-Prefabs (ins Preset)"))
            CreatePropsFromSelectedSprites();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Neuer Zufall (Seed wuerfeln)"))
            {
                Undo.RecordObject(preset, "Seed wuerfeln");
                preset.seed = Random.Range(1, 999999);
                EditorUtility.SetDirty(preset);
            }
        }
    }

    private static string ToProjectPath(string absolutePath)
    {
        string projectPath = Application.dataPath;
        if (absolutePath.StartsWith(projectPath))
            return "Assets" + absolutePath.Substring(projectPath.Length).Replace('\\', '/');
        return absolutePath;
    }

    private void LoadTilesFromFolder(bool report)
    {
        if (preset == null) return;

        EditorPrefs.SetString(TileFolderPrefKey, tileFolder);
        if (!AssetDatabase.IsValidFolder(tileFolder))
        {
            status = $"Ordner nicht gefunden: {tileFolder}";
            return;
        }

        int createdTiles = CreateMissingTileAssets(tileFolder);

        var found = new List<TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("t:TileBase", new[] { tileFolder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null) found.Add(tile);
        }
        found.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        Undo.RecordObject(preset, "Tiles laden");
        SortIntoGroups(found);

        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        if (report)
        {
            var groups = new List<string>();
            foreach (var group in preset.patchGroups) groups.Add($"{group.name} {group.tiles.Count}");

            status = $"{found.Count} Tiles aus {tileFolder} nach Farbe sortiert" +
                     (createdTiles > 0 ? $" ({createdTiles} Tile-Assets frisch aus Sprites angelegt)" : "") +
                     $": {string.Join(", ", groups)}, Untergrund (gruen) {preset.backgroundTiles.Count}. " +
                     "Gruppen und Gewichte kannst du unten frei umbauen.";
        }
    }

    // ---------------------------------------------------------- Farberkennung

    private enum TileColor { Gruen, Lila, Blau, Rot }

    private static readonly TileColor[] PatchColors = { TileColor.Lila, TileColor.Blau, TileColor.Rot };

    /// <summary>
    /// Sortiert die Tiles nach ihren Pixeln: Tiles mit lila, blauen oder roten
    /// Blumen werden je eine Farb-Gruppe (die Flecken), alles Gruene wird
    /// Untergrund fuer die Zwischenraeume. Das ruhigste Gras-Tile bekommt mehr
    /// Gewicht, sonst wirkt der Boden ueberladen. Gewicht und Dichte einer
    /// Gruppe, die es schon gab, bleiben erhalten.
    /// </summary>
    private void SortIntoGroups(List<TileBase> tiles)
    {
        var oldGroups = new Dictionary<string, WorldGenPreset.TileGroup>();
        foreach (var group in preset.patchGroups)
            if (group != null && !oldGroups.ContainsKey(group.name)) oldGroups.Add(group.name, group);

        preset.backgroundTiles.Clear();
        preset.patchGroups.Clear();

        var analysis = AnalyzeTiles(tiles);

        foreach (TileColor color in PatchColors)
        {
            string name = color.ToString();
            oldGroups.TryGetValue(name, out var old);
            var group = new WorldGenPreset.TileGroup
            {
                name = name,
                weight = old != null ? old.weight : 1f,
                density = old != null ? old.density : 0.7f
            };

            foreach (var tile in tiles)
                if (analysis[tile].color == color)
                    group.tiles.Add(new WorldGenPreset.TileEntry { tile = tile, weight = 1f });

            if (group.tiles.Count > 0) preset.patchGroups.Add(group);
        }

        TileBase calmest = null;
        foreach (var tile in tiles)
        {
            if (analysis[tile].color != TileColor.Gruen) continue;
            if (calmest == null || analysis[tile].busy < analysis[calmest].busy) calmest = tile;
        }

        foreach (var tile in tiles)
        {
            if (analysis[tile].color != TileColor.Gruen) continue;
            preset.backgroundTiles.Add(new WorldGenPreset.TileEntry
            {
                tile = tile,
                weight = tile == calmest ? CalmTileWeight(analysis[tile].busy) : 1f
            });
        }
    }

    /// <summary>Ein ganz leeres Tile darf viel haeufiger sein als eins mit ein paar Halmen.</summary>
    private static float CalmTileWeight(int busyPixels) => busyPixels == 0 ? 12f : 4f;

    /// <summary>Ordnet ein einzelnes Tile einer vorhandenen Gruppe zu (oder dem Untergrund).</summary>
    private void AddTileByColor(TileBase tile, TileColor color)
    {
        if (color == TileColor.Gruen)
        {
            preset.backgroundTiles.Add(new WorldGenPreset.TileEntry { tile = tile, weight = 1f });
            return;
        }

        string name = color.ToString();
        var group = preset.patchGroups.Find(g => g != null && g.name == name);
        if (group == null)
        {
            group = new WorldGenPreset.TileGroup { name = name, weight = 1f, density = 0.7f };
            preset.patchGroups.Add(group);
        }
        group.tiles.Add(new WorldGenPreset.TileEntry { tile = tile, weight = 1f });
    }

    private struct TileInfo
    {
        public TileColor color;
        public int busy;   // Pixel, die nicht die Grundfarbe des Tiles haben
    }

    /// <summary>
    /// Liest die Pixel jedes Tile-Sprites. Die Bilder sind nicht als "Read/Write"
    /// importiert, darum wird die PNG-Datei direkt gelesen - das aendert nichts
    /// an den Import-Einstellungen.
    /// </summary>
    private static Dictionary<TileBase, TileInfo> AnalyzeTiles(List<TileBase> tiles)
    {
        var result = new Dictionary<TileBase, TileInfo>();
        var textures = new Dictionary<string, Texture2D>();

        try
        {
            foreach (var tile in tiles)
            {
                var info = new TileInfo { color = TileColor.Gruen, busy = int.MaxValue };
                Sprite sprite = (tile as Tile)?.sprite;
                Texture2D pixels = sprite != null ? LoadReadable(sprite.texture, textures) : null;

                if (pixels != null)
                {
                    Rect rect = sprite.rect;
                    info = Classify(pixels.GetPixels(
                        Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                        Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)));
                }

                result[tile] = info;
            }
        }
        finally
        {
            foreach (var texture in textures.Values)
                if (texture != null) DestroyImmediate(texture);
        }

        return result;
    }

    private static Texture2D LoadReadable(Texture2D source, Dictionary<string, Texture2D> cache)
    {
        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path)) return null;
        if (cache.TryGetValue(path, out var cached)) return cached;

        Texture2D texture = null;
        if (File.Exists(path))
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                DestroyImmediate(texture);
                texture = null;
            }
        }

        cache[path] = texture;
        return texture;
    }

    /// <summary>
    /// Blumenfarbe nach Farbton: rot unter 25 Grad oder ab 320, blau 170-230,
    /// lila 230-320. Gras (gelb bis gruen) und blasse Pixel zaehlen nicht.
    /// Es gewinnt die Farbe mit den meisten Pixeln, ab zwei Pixeln.
    /// </summary>
    private static TileInfo Classify(Color[] pixels)
    {
        int red = 0, blue = 0, purple = 0;
        var counts = new Dictionary<Color32, int>();

        foreach (Color pixel in pixels)
        {
            if (pixel.a < 0.1f) continue;

            Color32 key = pixel;
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;

            Color.RGBToHSV(pixel, out float h, out float s, out float v);
            if (s < 0.3f || v < 0.2f) continue;

            float hue = h * 360f;
            if (hue < 25f || hue >= 320f) red++;
            else if (hue >= 170f && hue < 230f) blue++;
            else if (hue >= 230f && hue < 320f) purple++;
        }

        int baseCount = 0, total = 0;
        foreach (int n in counts.Values)
        {
            total += n;
            if (n > baseCount) baseCount = n;
        }

        var info = new TileInfo { color = TileColor.Gruen, busy = total - baseCount };
        int best = 1;
        if (purple > best) { best = purple; info.color = TileColor.Lila; }
        if (blue > best) { best = blue; info.color = TileColor.Blau; }
        if (red > best) { info.color = TileColor.Rot; }
        return info;
    }

    private void LoadPropsFromFolder(bool report)
    {
        if (preset == null) return;

        EditorPrefs.SetString(PropFolderPrefKey, propFolder);
        if (!AssetDatabase.IsValidFolder(propFolder))
        {
            status = $"Ordner nicht gefunden: {propFolder}";
            return;
        }

        var found = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { propFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null) found.Add(prefab);
        }
        found.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        Undo.RecordObject(preset, "Prefabs laden");
        preset.props.Clear();
        foreach (var prefab in found)
            preset.props.Add(new WorldGenPreset.PropEntry { prefab = prefab, weight = 1f });

        EditorUtility.SetDirty(preset);
        if (report) status = $"{found.Count} Prefabs aus {propFolder} geladen - alle mit Gewicht 1.";
    }

    // ------------------------------------------------------------- Ergaenzen

    private bool ConfirmReplace(string what)
    {
        return EditorUtility.DisplayDialog(
            "Liste ersetzen",
            $"Die {what} im Preset \"{preset.name}\" wird komplett neu aufgebaut - eingestellte Gruppen und " +
            "Gewichte gehen verloren.\n\nNur Neues dazunehmen geht mit \"Ergaenzen\".",
            "Ersetzen", "Abbrechen");
    }

    /// <summary>
    /// Legt fuer Sprites im Tile-Ordner, die noch kein Tile haben, ein Tile an
    /// und nimmt alle Tiles, die das Preset noch nicht kennt, als Untergrund
    /// mit Gewicht 1 dazu. Von dort lassen sie sich im Inspector in eine
    /// Farb-Gruppe verschieben.
    /// </summary>
    private void AddNewTilesFromFolder()
    {
        EditorPrefs.SetString(TileFolderPrefKey, tileFolder);
        if (!AssetDatabase.IsValidFolder(tileFolder))
        {
            status = $"Ordner nicht gefunden: {tileFolder}";
            return;
        }

        int createdTiles = CreateMissingTileAssets(tileFolder);

        var known = new HashSet<TileBase>();
        foreach (var entry in preset.backgroundTiles)
            if (entry != null && entry.tile != null) known.Add(entry.tile);
        foreach (var group in preset.patchGroups)
            if (group != null)
                foreach (var entry in group.tiles)
                    if (entry != null && entry.tile != null) known.Add(entry.tile);

        var found = new List<TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("t:TileBase", new[] { tileFolder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null && !known.Contains(tile)) found.Add(tile);
        }
        found.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        Undo.RecordObject(preset, "Tiles ergaenzen");

        // Eintraege geloeschter Tiles fliegen raus, sonst bleiben Luecken stehen.
        int removed = preset.backgroundTiles.RemoveAll(e => e == null || e.tile == null);
        foreach (var group in preset.patchGroups)
            if (group != null) removed += group.tiles.RemoveAll(e => e == null || e.tile == null);

        var analysis = AnalyzeTiles(found);
        var added = new List<string>();
        foreach (var tile in found)
        {
            TileColor color = analysis[tile].color;
            AddTileByColor(tile, color);
            added.Add($"{tile.name} ({(color == TileColor.Gruen ? "Untergrund" : color.ToString())})");
        }
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();

        string cleanup = removed > 0 ? $" {removed} Eintraege geloeschter Tiles entfernt." : "";
        status = found.Count == 0
            ? $"Keine neuen Tiles in {tileFolder} - das Preset kennt schon alle.{cleanup}"
            : $"{found.Count} neue Tiles nach Farbe einsortiert" +
              (createdTiles > 0 ? $" ({createdTiles} davon frisch aus Sprites angelegt)" : "") +
              ": " + string.Join(", ", added) + "." + cleanup;
    }

    /// <summary>
    /// Tile-Assets fuer alle Sprites der Bilder direkt im Ordner, die noch von
    /// keinem Tile benutzt werden. Einstellungen (Collider, Farbe) kommen von
    /// einem vorhandenen Tile im Ordner, damit die neuen gleich funktionieren.
    /// </summary>
    private static int CreateMissingTileAssets(string folder)
    {
        var usedSprites = new HashSet<Sprite>();
        Tile template = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { folder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null) continue;
            if (tile.sprite != null) usedSprites.Add(tile.sprite);
            if (template == null) template = tile;
        }

        int created = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is Sprite sprite) || usedSprites.Contains(sprite)) continue;

                var tile = CreateInstance<Tile>();
                tile.sprite = sprite;
                if (template != null)
                {
                    tile.color = template.color;
                    tile.colliderType = template.colliderType;
                    tile.flags = template.flags;
                    tile.transform = template.transform;
                }

                AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sprite.name}.asset"));
                usedSprites.Add(sprite);
                created++;
            }
        }
        return created;
    }

    private void AddNewPropsFromFolder()
    {
        EditorPrefs.SetString(PropFolderPrefKey, propFolder);
        if (!AssetDatabase.IsValidFolder(propFolder))
        {
            status = $"Ordner nicht gefunden: {propFolder}";
            return;
        }

        var added = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { propFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && AddPropIfMissing(prefab, "Prefabs ergaenzen")) added.Add(prefab);
        }
        EditorUtility.SetDirty(preset);

        status = added.Count == 0
            ? $"Keine neuen Prefabs in {propFolder} - das Preset kennt schon alle."
            : $"{added.Count} Prefabs ergaenzt: {string.Join(", ", added.ConvertAll(p => p.name))}.";
    }

    private bool AddPropIfMissing(GameObject prefab, string undoName)
    {
        foreach (var entry in preset.props)
            if (entry != null && entry.prefab == prefab) return false;

        Undo.RecordObject(preset, undoName);
        preset.props.Add(new WorldGenPreset.PropEntry { prefab = prefab, weight = 1f });
        return true;
    }

    /// <summary>
    /// Baut aus den im Project-Fenster markierten Bildern/Sprites je ein Prop-
    /// Prefab im Prefab-Ordner und traegt es ins Preset ein. Vorlage ist das
    /// erste Prefab im Preset (Layer, Sorting Layer, Collider) - Collider und
    /// Groesse werden an das neue Sprite angepasst.
    /// </summary>
    private void CreatePropsFromSelectedSprites()
    {
        var sprites = new List<Sprite>();
        foreach (Object selected in Selection.objects)
        {
            if (selected is Sprite sprite) { if (!sprites.Contains(sprite)) sprites.Add(sprite); continue; }
            if (!(selected is Texture2D)) continue;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(selected)))
                if (asset is Sprite s && !sprites.Contains(s)) sprites.Add(s);
        }

        if (sprites.Count == 0)
        {
            status = "Keine Sprites markiert. Im Project-Fenster die neuen Bilder (z.B. nadelbaum.png) markieren " +
                     "und den Button noch mal druecken.";
            return;
        }

        if (!AssetDatabase.IsValidFolder(propFolder))
        {
            status = $"Prefab-Ordner nicht gefunden: {propFolder}";
            return;
        }

        GameObject template = null;
        foreach (var entry in preset.props)
            if (entry != null && entry.prefab != null) { template = entry.prefab; break; }

        var made = new List<string>();
        foreach (Sprite sprite in sprites)
        {
            string path = $"{propFolder}/{sprite.name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                prefab = BuildPropPrefab(sprite, template, path);
                made.Add(sprite.name);
            }
            AddPropIfMissing(prefab, "Props aus Sprites");
        }

        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();

        status = $"{sprites.Count} Sprites ins Preset uebernommen" +
                 (made.Count > 0 ? $", neue Prefabs in {propFolder}: {string.Join(", ", made)}" : "") +
                 (template != null ? $" (Vorlage: {template.name})." : " (ohne Vorlage - Collider bitte pruefen).") +
                 " Danach \"Nur Props\" generieren.";
    }

    private static GameObject BuildPropPrefab(Sprite sprite, GameObject template, string path)
    {
        GameObject go;
        if (template != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(template);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            go = new GameObject();
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
        }

        go.name = sprite.name;
        go.transform.position = Vector3.zero;

        var renderer = go.GetComponentInChildren<SpriteRenderer>();
        Sprite old = renderer.sprite;
        renderer.sprite = sprite;

        if (old != null)
        {
            // Gleiche Pixeldichte wie die Vorlage, auch wenn das neue Bild
            // eine andere Pixels-per-Unit-Einstellung hat.
            float ppuFactor = old.pixelsPerUnit / sprite.pixelsPerUnit;
            Vector3 scale = go.transform.localScale;
            go.transform.localScale = new Vector3(scale.x * ppuFactor, scale.y * ppuFactor, scale.z);

            // Stamm-Collider im selben Verhaeltnis mitwachsen lassen.
            var box = renderer.GetComponent<BoxCollider2D>();
            if (box != null && old.bounds.size.x > 0f && old.bounds.size.y > 0f)
            {
                var ratio = new Vector2(
                    sprite.bounds.size.x / old.bounds.size.x,
                    sprite.bounds.size.y / old.bounds.size.y);
                box.size = Vector2.Scale(box.size, ratio);
                box.offset = Vector2.Scale(box.offset, ratio);
            }
        }
        else
        {
            var box = go.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                // Ohne Vorlage: kleiner Collider am Fuss des Sprites.
                Bounds b = sprite.bounds;
                box.size = new Vector2(b.size.x * 0.3f, b.size.y * 0.1f);
                box.offset = new Vector2(b.center.x, b.min.y + box.size.y * 0.5f);
            }
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------ Generieren

    private void DrawGenerateSection()
    {
        var world = SelectedWorld;
        using (new EditorGUI.DisabledScope(world == null))
        {
            EditorGUILayout.LabelField("Generieren", EditorStyles.boldLabel);

            if (GUILayout.Button("Alles generieren (Tiles + Props)", GUILayout.Height(30f)))
                Generate(world, true, true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nur Tiles")) Generate(world, true, false);
                if (GUILayout.Button("Nur Props")) Generate(world, false, true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tiles loeschen"))
                {
                    int group = Undo.GetCurrentGroup();
                    WorldGenerator.ClearTiles(world);
                    Undo.SetCurrentGroupName("Tiles loeschen");
                    Undo.CollapseUndoOperations(group);
                    status = $"{world.name}: alle Tiles entfernt.";
                }

                if (GUILayout.Button("Props loeschen"))
                {
                    int group = Undo.GetCurrentGroup();
                    WorldGenerator.ClearProps(world);
                    Undo.SetCurrentGroupName("Props loeschen");
                    Undo.CollapseUndoOperations(group);
                    status = $"{world.name}: alle Props entfernt.";
                }
            }
        }
    }

    private void Generate(WorldManager3x3 world, bool tiles, bool props)
    {
        if (world == null || preset == null) return;

        int group = Undo.GetCurrentGroup();
        int tileCount = 0;
        int propCount = 0;

        WorldGenerator.AlignChunks(world);
        if (tiles) tileCount = WorldGenerator.FillTiles(world, preset);
        if (props) propCount = WorldGenerator.PlaceProps(world, preset);

        Undo.SetCurrentGroupName("Welt generieren");
        Undo.CollapseUndoOperations(group);

        AssetDatabase.SaveAssets();

        var parts = new List<string>();
        if (tiles) parts.Add($"{tileCount} Tiles");
        if (props) parts.Add($"{propCount} Props");
        status = $"{world.name}: {string.Join(" und ", parts)} erzeugt (Seed {preset.seed}).";
        Debug.Log($"Welt-Generator - {status}");
    }
}
