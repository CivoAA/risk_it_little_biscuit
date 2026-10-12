using UnityEngine;

/// <summary>
/// Gewaehlte Schwierigkeit, eingestellt am Chili-Regal in Ricos Laden
/// (<see cref="DifficultyPanel"/>).
///
/// Bisher nur gespeichert - noch nichts im Spiel liest den Wert aus.
/// </summary>
public static class Difficulty
{
    public enum Level { Easy = 0, Normal = 1, Hard = 2 }

    private const string PrefsKey = "difficulty";

    public static Level Current
    {
        get => (Level)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)Level.Normal), 0, 2);
        set
        {
            if (Current == value) return;
            PlayerPrefs.SetInt(PrefsKey, (int)value);
            PlayerPrefs.Save();
        }
    }
}
