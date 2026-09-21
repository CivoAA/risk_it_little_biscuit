using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Welchen Modus der Spieler im Hauptmenü gewählt hat.</summary>
public enum GameMode
{
    Story,
    Endless
}

/// <summary>
/// Überlebt Szenenwechsel (static) und sagt dem Rest des Spiels,
/// ob gerade Story oder Endless läuft - und wohin es nach dem Lauf zurückgeht.
/// </summary>
public static class GameSession
{
    /// <summary>Die Szene, die Start und Ziel eines Laufs ist.</summary>
    public const string HubScene = "hub";

    /// <summary>Das Hauptmenü.</summary>
    public const string MainMenuScene = "Main Menu";

    public static GameMode SelectedMode { get; set; } = GameMode.Story;

    /// <summary>
    /// Wie hart der nächste Lauf wird. 1 = normal, darüber kommen mehr und
    /// zähere Gegner - und es gibt mehr dafür. Wer ein Level startet, setzt den
    /// Wert; steht nichts drin, läuft es normal. Ausgewertet wird er in
    /// <see cref="RunDifficulty"/>.
    /// </summary>
    public static float Chaos { get; set; } = 1f;

    public static bool IsEndless => SelectedMode == GameMode.Endless;

    /// <summary>
    /// Szene, in die der Spieler nach Sieg oder Niederlage zurückkehrt. Wer ein
    /// Level startet, trägt hier seine eigene Szene ein: die Hub-Levelauswahl
    /// den Hub, die World Map sich selbst.
    ///
    /// <b>Leer heißt: dieser Lauf wurde nicht von irgendwo aus gestartet.</b>
    /// Das ist der Fall, wenn jemand die Test-Szene oder eine Map-Szene direkt
    /// aus dem Editor startet - dann gibt es kein Zurück und
    /// <see cref="GameManager.Restart"/> lädt einfach neu. Stünde hier
    /// vorbelegt der Hub, würde die Test-Szene in den Hub springen.
    /// </summary>
    public static string ReturnScene { get; set; }

    /// <summary>
    /// Statics überleben in Unity das Verlassen des Play-Modus, wenn "Reload
    /// Domain" aus ist. Ohne das Zurücksetzen schleppt die Test-Szene das
    /// Rückreiseziel des vorherigen Laufs mit.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ReturnScene = null;
        SelectedMode = GameMode.Story;
        Chaos = 1f;
    }

    /// <summary>
    /// Zurück ins Hauptmenü - von überall, egal wie viele Szenen gerade
    /// nebeneinander liegen. Bewusst hart mit Single: das entlädt Level,
    /// Map-Szene und Hub in einem Rutsch. Die DontDestroyOnLoad-Manager
    /// (AudioController, MenuManager, SteamManager) überleben das, genauso wie
    /// auf dem Weg Hauptmenü -> Hub.
    /// </summary>
    public static void LoadMainMenu()
    {
        Time.timeScale = 1f;
        ReturnScene = null;

        if (AudioController.Instance != null) AudioController.Instance.SwitchMusic("Main Menu");
        SceneManager.LoadScene(MainMenuScene, LoadSceneMode.Single);
    }
}
