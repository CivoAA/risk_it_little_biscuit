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

    /// <summary>Gilt je Profil - siehe <see cref="ProfilePrefs"/>.</summary>
    public const string PrefsKey = "difficulty";

    public static Level Current
    {
        get => (Level)Mathf.Clamp(ProfilePrefs.GetInt(PrefsKey, (int)Level.Normal), 0, 2);
        set => ProfilePrefs.SetInt(PrefsKey, (int)value);
    }
}
