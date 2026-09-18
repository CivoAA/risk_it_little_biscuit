using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Teilt "Assets/Scenes/Game.unity" in das neue Szenen-Paar auf:
///
///   Assets/Scenes/Core/GameCore.unity    alles Gemeinsame - Managers, UI Canvas,
///                                        Audio Controller, EventSystem, Player,
///                                        Kamera, Enemy Spawner samt Waves
///   Assets/Scenes/Maps/Map_World0.unity  nur Grid + World0 (+ Map-Info)
///   Assets/Scenes/Maps/Map_World1.unity  nur Grid + World1
///   ...                                  pro Welt unter "Grid" eine Szene
///
/// Game.unity selbst wird nie veraendert - jede Zielszene entsteht als Kopie,
/// aus der herausgeloescht wird, was nicht hineingehoert. Das ist derselbe Weg
/// wie beim <see cref="TestSceneBuilder"/> und der sicherste: Tilemap-Daten,
/// Prefab-Verbindungen und Inspector-Werte bleiben unangetastet, statt sie
/// durch Umhaengen neu aufzubauen.
///
/// Der Lauf ist wiederholbar - vorhandene Map-Szenen und GameCore werden dabei
/// ERSETZT. Wer also spaeter von Hand etwas in eine Map-Szene baut, verliert es
/// beim naechsten Aufteilen. Gedacht ist das Werkzeug fuer die Umstellung, nicht
/// als Dauereinrichtung: sobald das neue System steht, ist Game.unity die
/// Altlast und dieses Skript darf mit weg.
///
/// Was der Split NICHT anfasst: Hub, UI und Ladeweg. Ob ein Lauf ueber
/// Game.unity oder ueber die Map-Szenen geht, entscheidet allein der Schalter in
/// <see cref="MapSceneSystem"/> (der Kasten unten links im Bild, F9).
/// </summary>
public static class MapSceneSplitter
{
    private const string SourceScenePath = "Assets/Scenes/Game.unity";
    private const string MapFolder = "Assets/Scenes/Maps";
    private const string CoreFolder = "Assets/Scenes/Core";
    private const string CoreScenePath = CoreFolder + "/GameCore.unity";

    private const string GridRootName = "Grid";
    private const string MapInfoName = "Map";

    /// <summary>
    /// Was in GameCore keinen Sinn mehr ergibt. Der WorldSelector schaltete die
    /// Welten in der gemeinsamen Szene frei - im neuen System IST die Map-Szene
    /// die Auswahl. Der TileMapGenerator ist ein Editor-Helfer, dessen Tilemap
    /// in einer Map-Szene liegt; er zieht mit ihr um (siehe RescueMapHelpers).
    /// </summary>
    private static readonly string[] CoreChildrenToDelete =
    {
        "Managers/World Manager",
        "Managers/TileMapGenerator1",
    };


    // ------------------------------------------------------------------ Menue

    [MenuItem("Tools/Maps/Game-Szene aufteilen (Map-Szenen + GameCore)", false, 0)]
    public static void BuildMenu()
    {
        bool go = EditorUtility.DisplayDialog(
            "Game-Szene aufteilen",
            "Aus Game.unity entstehen:\n\n" +
            "  Assets/Scenes/Core/GameCore.unity  (Player, UI, Manager, Spawner)\n" +
            "  Assets/Scenes/Maps/Map_WorldX.unity  (je eine Welt)\n\n" +
            "Game.unity bleibt unveraendert und weiter spielbar.\n\n" +
            "ACHTUNG: bereits vorhandene Map-Szenen und GameCore werden ersetzt.\n\n" +
            "Das dauert einen Moment - Game.unity ist gross und wird pro Welt einmal kopiert.",
            "Aufteilen", "Abbrechen");

        if (!go) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string report = Build();
        EditorUtility.DisplayDialog("Game-Szene aufteilen", report, "Ok");
    }

    /// <summary>Einstieg fuer den Batch-Mode (Unity -executeMethod).</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[MapSceneSplitter] " + Build());
    }

    // ------------------------------------------------------------------ Ablauf

    public static string Build()
    {
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            return SourceScenePath + " nicht gefunden - nichts geaendert.";
        }

        EnsureFolder(MapFolder);
        EnsureFolder(CoreFolder);

        List<string> worldNames = ReadWorldNames();
        if (worldNames.Count == 0)
        {
            return "Unter \"" + GridRootName + "\" in " + SourceScenePath + " steht keine Welt - nichts geaendert.";
        }

        var created = new List<string>();
        var problems = new List<string>();

        try
        {
            for (int i = 0; i < worldNames.Count; i++)
            {
                string world = worldNames[i];
                EditorUtility.DisplayProgressBar("Game-Szene aufteilen", "Map-Szene fuer " + world + " ...",
                                                 (float)i / (worldNames.Count + 1));

                string path = BuildMapScene(world, problems);
                if (path != null) created.Add(path);
            }

            EditorUtility.DisplayProgressBar("Game-Szene aufteilen", "GameCore ...",
                                             (float)worldNames.Count / (worldNames.Count + 1));

            string corePath = BuildCoreScene(problems);
            if (corePath != null) created.Add(corePath);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RegisterInBuildSettings(created);
        AssetDatabase.SaveAssets();

        var report = new StringBuilder();
        report.AppendLine(created.Count + " Szenen gebaut und in die Build Settings eingetragen:");
        foreach (string path in created) report.AppendLine("  " + path);

        if (problems.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Hinweise:");
            foreach (string problem in problems) report.AppendLine("  " + problem);
        }

        report.AppendLine();
        report.AppendLine("Game.unity ist unveraendert. Umschalten im Spiel: der Kasten unten links (F9).");

        string text = report.ToString();
        Debug.Log("[MapSceneSplitter]\n" + text);
        return text;
    }

    /// <summary>Die Welten sind die Kinder des Grid-Roots in Game.unity.</summary>
    private static List<string> ReadWorldNames()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

        Transform grid = FindRoot(scene, GridRootName);
        if (grid == null)
        {
            Debug.LogError("[MapSceneSplitter] Kein Root-Objekt \"" + GridRootName + "\" in " + SourceScenePath + ".");
            return new List<string>();
        }

        var names = new List<string>();
        for (int i = 0; i < grid.childCount; i++)
        {
            names.Add(grid.GetChild(i).name);
        }

        // Nach Namen sortieren, damit Map_World0 ... Map_World3 in derselben
        // Reihenfolge entstehen, in der die Level im Hub stehen.
        names.Sort(System.StringComparer.Ordinal);
        return names;
    }

    // ------------------------------------------------------------- Map-Szenen

    /// <summary>Baut eine Map-Szene aus genau einer Welt. Gibt den Pfad zurueck oder null.</summary>
    private static string BuildMapScene(string worldName, List<string> problems)
    {
        string path = MapFolder + "/Map_" + worldName + ".unity";

        if (!CopyFromGameScene(path)) return null;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        Transform grid = FindRoot(scene, GridRootName);
        Transform world = grid != null ? grid.Find(worldName) : null;
        if (grid == null || world == null)
        {
            Debug.LogError("[MapSceneSplitter] \"" + GridRootName + "/" + worldName + "\" in der Kopie nicht gefunden.");
            return null;
        }

        // Editor-Helfer, deren Ziel in dieser Welt liegt, vor dem Loeschen der
        // Managers auf Root-Ebene retten - sonst waere ihre Einstellung weg.
        List<GameObject> rescued = RescueMapHelpers(scene, world);

        // Alles ausser Grid (und den Geretteten) fliegt raus: Player, UI Canvas,
        // Managers, Audio Controller und EventSystem stehen ab jetzt in GameCore.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == grid.gameObject) continue;
            if (rescued.Contains(root)) continue;
            Object.DestroyImmediate(root);
        }

        // Unter Grid bleibt genau diese eine Welt stehen.
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            Transform child = grid.GetChild(i);
            if (child != world) Object.DestroyImmediate(child.gameObject);
        }

        // In Game.unity liegen die Welten deaktiviert herum, weil der
        // WorldSelector zur Laufzeit eine davon anschaltet. Hier ist sie die
        // einzige - also an.
        grid.gameObject.SetActive(true);
        world.gameObject.SetActive(true);

        CreateMapInfo(scene, worldName, problems);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    /// <summary>
    /// Holt Objekte aus den Managers, die in diese Welt gehoeren, auf die
    /// Root-Ebene der Map-Szene. Betrifft den TileMapGenerator: er zeigt auf
    /// eine Tilemap, die es nach dem Split nur noch in einer Map-Szene gibt -
    /// szenenuebergreifend haelt diese Referenz nicht.
    /// </summary>
    private static List<GameObject> RescueMapHelpers(Scene scene, Transform world)
    {
        var rescued = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TilemapGenerator generator in root.GetComponentsInChildren<TilemapGenerator>(true))
            {
                if (generator.transform.IsChildOf(world)) continue;   // liegt schon richtig
                if (generator.tilemap == null) continue;
                if (!generator.tilemap.transform.IsChildOf(world)) continue;

                generator.transform.SetParent(null, true);
                rescued.Add(generator.gameObject);
                Debug.Log("[MapSceneSplitter] \"" + generator.name + "\" nach " + scene.name +
                          " uebernommen - seine Tilemap liegt in " + world.name + ".");
            }
        }

        return rescued;
    }

    /// <summary>Das eine Logik-Objekt der Map-Szene: wer bin ich, und hol mir GameCore.</summary>
    private static void CreateMapInfo(Scene scene, string worldName, List<string> problems)
    {
        GameObject info = new GameObject(MapInfoName);
        SceneManager.MoveGameObjectToScene(info, scene);
        info.transform.SetAsFirstSibling();

        int mapId = ReadMapId(worldName);
        if (mapId < 0)
        {
            problems.Add(worldName + ": keine Nummer im Namen - Map-ID am Objekt \"" + MapInfoName +
                         "\" bitte von Hand eintragen.");
        }

        MapDefinition definition = info.AddComponent<MapDefinition>();
        definition.EditorSetup(worldName, mapId);

        info.AddComponent<MapBootstrap>();

        // Die Levelauswahl sucht die Szene ueber die Map-ID (Map_World<id>).
        // Weicht der Weltname davon ab, findet sie sie nicht.
        string expected = MapSceneSystem.SceneForMapId(mapId);
        if (mapId >= 0 && scene.name != expected)
        {
            problems.Add(scene.name + ": die Levelauswahl sucht \"" + expected + "\" - Szene umbenennen " +
                         "oder MapSceneSystem.ResolveScene anpassen.");
        }
    }

    /// <summary>"World2" -> 2. Dieselbe Nummer wie MapsManager.selectedMap.</summary>
    private static int ReadMapId(string worldName)
    {
        string digits = new string(worldName.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out int id) ? id : -1;
    }

    // --------------------------------------------------------------- GameCore

    /// <summary>Baut GameCore: Game.unity ohne Welten und ohne Welt-Auswahl.</summary>
    private static string BuildCoreScene(List<string> problems)
    {
        if (!CopyFromGameScene(CoreScenePath)) return null;

        Scene scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);

        Transform grid = FindRoot(scene, GridRootName);
        if (grid != null)
        {
            Object.DestroyImmediate(grid.gameObject);
        }

        foreach (string path in CoreChildrenToDelete)
        {
            Transform target = FindByPath(scene, path);
            if (target != null)
            {
                Object.DestroyImmediate(target.gameObject);
            }
            else
            {
                problems.Add("GameCore: \"" + path + "\" nicht gefunden - uebersprungen.");
            }
        }

        // Das Gegner-Spawnen macht ab jetzt der SpawnDirector. Der Installer
        // baut ihn ein, uebernimmt die Prefabs aus dem alten TimeWaveManager
        // und legt die alten Spawner stumm - sonst liefen zwei Systeme parallel.
        string installReport = SpawnDirectorInstaller.Install(scene);
        Debug.Log("[MapSceneSplitter] Spawn-Director:\n" + installReport);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, CoreScenePath);
        return CoreScenePath;
    }

    // ----------------------------------------------------------------- Kleines

    private static bool CopyFromGameScene(string targetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
        {
            AssetDatabase.DeleteAsset(targetPath);
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, targetPath))
        {
            Debug.LogError("[MapSceneSplitter] Konnte " + SourceScenePath + " nicht nach " + targetPath + " kopieren.");
            return false;
        }

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        return true;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>Hinten anhaengen, damit sich die Indizes bestehender Szenen nicht verschieben.</summary>
    private static void RegisterInBuildSettings(List<string> paths)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        bool changed = false;

        foreach (string path in paths)
        {
            if (scenes.Any(s => s.path == path)) continue;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            changed = true;
        }

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
        }
        return null;
    }

    private static Transform FindByPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        Transform current = FindRoot(scene, parts[0]);

        for (int i = 1; i < parts.Length && current != null; i++)
        {
            current = current.Find(parts[i]);
        }

        return current;
    }
}
