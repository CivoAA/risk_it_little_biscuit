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

    /// <summary>Gilt je Profil - siehe <see cref="SaveProfiles.PrefKey"/>.</summary>
    public const string PrefsKey = "difficulty";

    public static Level Current
    {
        get => (Level)Mathf.Clamp(PlayerPrefs.GetInt(SaveProfiles.PrefKey(PrefsKey), (int)Level.Normal), 0, 2);
        set
        {
            if (Current == value) return;
            PlayerPrefs.SetInt(SaveProfiles.PrefKey(PrefsKey), (int)value);
            PlayerPrefs.Save();
        }
    }
}
