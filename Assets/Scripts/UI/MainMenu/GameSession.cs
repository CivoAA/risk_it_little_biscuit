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
    /// den Hub, die World Map sich selbst. Steht nichts drin, gilt der Hub - die
    /// World Map wird nicht mehr angesteuert, sie läuft nur noch, wenn man sie
    /// direkt startet.
    /// </summary>
    public static string ReturnScene { get; set; } = HubScene;
}
