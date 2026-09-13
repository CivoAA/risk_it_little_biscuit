/// <summary>Welchen Modus der Spieler im Hauptmenü gewählt hat.</summary>
public enum GameMode
{
    Story,
    Endless
}

/// <summary>
/// Überlebt Szenenwechsel (static) und sagt dem Rest des Spiels,
/// ob gerade Story oder Endless läuft.
/// </summary>
public static class GameSession
{
    public static GameMode SelectedMode { get; set; } = GameMode.Story;

    public static bool IsEndless => SelectedMode == GameMode.Endless;
}
