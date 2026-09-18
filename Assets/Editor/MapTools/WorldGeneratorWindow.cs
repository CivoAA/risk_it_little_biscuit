using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Fenster unter Tools -> Map -> Welt-Generator: waehlt eine Welt (World0..N)
/// und ein Preset aus und fuellt damit die neun Chunks des 3x3-Rasters mit
/// zufaelligen Tiles und Props.
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

    private WorldGenPreset preset;
    private Editor presetEditor;

    private WorldManager3x3[] worlds = new WorldManager3x3[0];
    private int worldIndex;

    private string tileFolder = DefaultTileFolder;
    private string propFolder = DefaultPropFolder;
    private string newWorldName = "World4";
    private string status = string.Empty;

    private Vector2 scroll;

    [MenuItem("Tools/Map/Welt-Generator", false, 0)]
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

        RefreshWorlds();
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

    // ------------------------------------------------------------------- Welt

    private void DrawWorldSection()
    {
        EditorGUILayout.LabelField("Welt", EditorStyles.boldLabel);

        if (worlds.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Keine Welt mit WorldManager3x3 in den geladenen Szenen gefunden. " +
                "Game-Szene oeffnen oder unten eine neue Welt anlegen.", MessageType.Warning);
        }
        else
        {
            var names = new string[worlds.Length];
            for (int i = 0; i < worlds.Length; i++) names[i] = worlds[i].name;

            EditorGUI.BeginChangeCheck();
            worldIndex = EditorGUILayout.Popup("Ziel-Welt", worldIndex, names);
            if (EditorGUI.EndChangeCheck() && SelectedWorld != null)
                EditorPrefs.SetString(WorldPrefKey, SelectedWorld.name);

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
        EditorGUILayout.LabelField("Listen schnell fuellen", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            tileFolder = EditorGUILayout.TextField("Tile-Ordner", tileFolder);
            if (GUILayout.Button("...", GUILayout.Width(28f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Ordner mit Tiles", tileFolder, string.Empty);
                if (!string.IsNullOrEmpty(picked)) tileFolder = ToProjectPath(picked);
            }
            if (GUILayout.Button("Laden + einsortieren", GUILayout.Width(130f)))
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
            if (GUILayout.Button("Laden", GUILayout.Width(60f)))
                LoadPropsFromFolder(true);
        }

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
        if (report)
        {
            status = $"{found.Count} Tiles aus {tileFolder} geladen: " +
                     $"{preset.patchGroups.Count} Farb-Gruppen, {preset.backgroundTiles.Count} Untergrund-Tiles. " +
                     "Gruppen und Gewichte kannst du unten frei umbauen.";
        }
    }

    /// <summary>
    /// Sortiert die geladenen Tiles in die Farb-Gruppen ein. Die Zuordnung
    /// entspricht der Reihenfolge im gras.png: 1+3 lila, 2+4 blau, 5+6 rot,
    /// der Rest ist Gras und das letzte Tile ist das leere Gras-Tile.
    /// </summary>
    private void SortIntoGroups(List<TileBase> tiles)
    {
        preset.backgroundTiles.Clear();
        preset.patchGroups.Clear();

        if (tiles.Count < 7)
        {
            // Zu wenige Tiles fuer die feste Aufteilung - alles wird Untergrund.
            foreach (var tile in tiles)
                preset.backgroundTiles.Add(new WorldGenPreset.TileEntry { tile = tile, weight = 1f });
            return;
        }

        AddGroup("Lila", tiles, 0, 2);
        AddGroup("Blau", tiles, 1, 3);
        AddGroup("Rot", tiles, 4, 5);

        // Rest: Gras-Tiles mit Gewicht 1, das letzte (leere) Tile deutlich
        // haeufiger - sonst wirkt der Boden schnell ueberladen.
        for (int i = 6; i < tiles.Count; i++)
        {
            bool isBlank = i == tiles.Count - 1;
            preset.backgroundTiles.Add(new WorldGenPreset.TileEntry
            {
                tile = tiles[i],
                weight = isBlank ? 12f : 1f
            });
        }
    }

    private void AddGroup(string name, List<TileBase> tiles, params int[] indices)
    {
        var group = new WorldGenPreset.TileGroup { name = name, weight = 1f, density = 0.7f };
        foreach (int index in indices)
        {
            if (index < 0 || index >= tiles.Count) continue;
            group.tiles.Add(new WorldGenPreset.TileEntry { tile = tiles[index], weight = 1f });
        }

        if (group.tiles.Count > 0) preset.patchGroups.Add(group);
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
