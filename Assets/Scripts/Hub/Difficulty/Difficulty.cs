using UnityEngine;

/// <summary>
/// Gewaehlte Schwierigkeit, eingestellt am Chili-Regal in Ricos Laden
/// (<see cref="DifficultyPanel"/>). Laesst sich im Hub jederzeit umstellen;
/// ein Lauf merkt sich beim Start, mit welcher Stufe er laeuft
/// (<see cref="RunDifficulty.Level"/>).
///
///   Leicht  Gegner haben weniger Leben und machen weniger Schaden
///   Normal  so, wie das Spiel gebaut ist
///   Schwer  Gegner haben deutlich mehr Leben und machen deutlich mehr Schaden
///
/// Leben wirkt beim Spawnen (<see cref="RunDifficulty.HealthFactor"/>), Schaden
/// beim Spieler (<see cref="PlayerController.TakeDamage"/>) - so zaehlen auch
/// Boss-Attacken und Geschosse mit, die ihren eigenen Schaden haben.
///
/// Ein Story-Sieg bringt Sterne in der Levelauswahl (<see cref="LevelRecords.Stars"/>):
/// Leicht 1, Normal 2, Schwer 3.
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

    /// <summary>Leben und Schaden der Gegner: Leicht ein Viertel weniger, Schwer das 2,5-Fache.</summary>
    public static float EnemyFactor(Level level)
    {
        switch (level)
        {
            case Level.Easy: return 0.75f;
            case Level.Hard: return 2.5f;
            default: return 1f;
        }
    }

    /// <summary>Sterne fuer einen Story-Sieg auf dieser Stufe (1-3).</summary>
    public static int Stars(Level level) => (int)level + 1;
}
