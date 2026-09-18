using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Haelt zusammen, woraus ein Lauf besteht.
///
/// Jede Welt hat ihre eigene Szene (Map_World0 ... Map_World3). Alles
/// Gemeinsame - Player, UI, Manager, Kamera, Spawner - steht genau einmal in
/// GameCore.unity. Eine Map-Szene laedt GameCore selbst nach
/// (<see cref="MapBootstrap"/>), darum muss beim Start eines Levels nur die
/// Map-Szene geladen werden - egal ob aus dem Hub oder direkt aus dem Editor.
///
/// Die alte Game.unity, in der alle Welten uebereinander lagen und ein
/// WorldSelector die richtige freischaltete, wird nicht mehr angesteuert. Sie
/// liegt nur noch als Vorlage fuer den <c>MapSceneSplitter</c> herum.
/// </summary>
public static class MapSceneSystem
{
    /// <summary>Die Szene mit allem Gemeinsamen. Liegt unter Assets/Scenes/Core.</summary>
    public const string CoreScene = "GameCore";

    /// <summary>Map-Szenen heissen "Map_World" + Map-ID - so wie die Welten in Game.unity.</summary>
    public const string MapScenePrefix = "Map_World";

    private static readonly System.Collections.Generic.HashSet<string> missingWarned =
        new System.Collections.Generic.HashSet<string>();

    /// <summary>
    /// Die Map-Szene, die gerade neben GameCore liegt. Traegt sich ueber
    /// <see cref="MapDefinition"/> selbst ein.
    /// </summary>
    public static string RunMapScene { get; private set; }

    /// <summary>Beim Betreten des Play-Modus faengt jede Buchhaltung bei null an.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        RunMapScene = null;
        missingWarned.Clear();
    }

    public static string SceneForMapId(int mapId)
    {
        return MapScenePrefix + mapId;
    }

    /// <summary>
    /// Welche Szene fuer diesen Level-Eintrag geladen wird: die Map-Szene zur
    /// Map-ID. Fehlt sie in den Build Settings, faellt es auf den Eintrag aus
    /// der Levelauswahl zurueck - so bleibt ein Level spielbar, dessen Welt es
    /// noch nicht als eigene Szene gibt.
    /// </summary>
    public static string ResolveScene(string fallbackScene, int mapId)
    {
        string mapScene = SceneForMapId(mapId);
        if (Application.CanStreamedLevelBeLoaded(mapScene)) return mapScene;

        // Die Levelauswahl fragt das beim Zeichnen - also pro Szene nur einmal
        // meckern, sonst laeuft die Konsole in jedem Frame voll.
        if (missingWarned.Add(mapScene))
        {
            Debug.LogWarning($"[MapSceneSystem] Map-Szene \"{mapScene}\" ist nicht in den Build Settings - " +
                             $"es laeuft weiter ueber \"{fallbackScene}\". Tools -> Maps -> Game-Szene aufteilen ausfuehren.");
        }

        return fallbackScene;
    }

    // ------------------------------------------------------------ Buchhaltung

    public static void SetRunMapScene(string sceneName)
    {
        RunMapScene = sceneName;
    }

    public static void ClearRunMapScene(string sceneName)
    {
        if (RunMapScene == sceneName) RunMapScene = null;
    }

    /// <summary>
    /// Legt die Map-Szene still - Gegenstueck zu MenuManager.DeactivateScene
    /// fuer GameCore.
    /// </summary>
    public static void DeactivateRunMap()
    {
        Scene scene = SceneManager.GetSceneByName(RunMapScene ?? "");
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            root.SetActive(false);
        }
    }

    /// <summary>
    /// Entlaedt die Map-Szene. Ein Lauf besteht aus zwei Szenen (GameCore +
    /// Map) - wer nur GameCore entlaedt, laesst die Welt stehen.
    /// </summary>
    public static void UnloadRunMap()
    {
        string sceneName = RunMapScene;
        if (string.IsNullOrEmpty(sceneName)) return;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }

        RunMapScene = null;
    }
}
