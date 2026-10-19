using UnityEngine;

/// <summary>
/// Der Punktestand eines Endless-Laufs - das, was ins Steam-Scoreboard geht
/// (<see cref="SteamScoreboard"/>).
///
/// Punkte gibt es fuer:
///   - jeden besiegten Gegner, je nach Rolle (<see cref="KillPoints"/>)
///   - jeden benutzten Mixer (<see cref="MixerPoints"/>)
///   - jede ueberlebte Sekunde (<see cref="PointsPerSecond"/>)
///   - jedes erreichte Lauf-Level (<see cref="PointsPerLevel"/>)
///
/// Gegner und Mixer zaehlen mit dem Fortschritt (<see cref="Progress"/>) mal:
/// je laenger der Lauf schon geht, desto mehr ist jeder Kill wert. Am Ende kommt
/// die Schwierigkeit oben drauf (<see cref="DifficultyFactor"/>): Leicht x0,5,
/// Schwer x5 - auf alles.
///
/// Gezaehlt wird in jedem Lauf auf einer echten Karte, eingereicht nur, wenn
/// er als Endless endet - auch nach "Endless weiterspielen", dann zaehlt der
/// Story-Teil mit (wie in einem echten Endless-Lauf, der ja auch mit dem Plan
/// der Welt beginnt). Alle Zahlen stehen hier und nirgends sonst.
/// </summary>
public static class EndlessScore
{
    // ==================================================================
    //  Regeln
    // ==================================================================

    /// <summary>Punkte je ueberlebter Sekunde (ohne Fortschritt, mit Schwierigkeit).</summary>
    public const float PointsPerSecond = 20f;

    /// <summary>Punkte je erreichtem Lauf-Level ueber 1 (ohne Fortschritt, mit Schwierigkeit).</summary>
    public const float PointsPerLevel = 500f;

    /// <summary>Grundwert je benutztem Mixer, mal <see cref="Progress"/>.</summary>
    public const float MixerPoints = 500f;

    /// <summary>Nach so vielen Sekunden ist ein Kill doppelt so viel wert, nach der doppelten Zeit dreimal usw.</summary>
    public const float ProgressStep = 600f;

    /// <summary>Grundwert je besiegtem Gegner, mal <see cref="Progress"/>.</summary>
    public static float KillPoints(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.Elite: return 150f;
            case EnemyRole.MiniBoss: return 1000f;
            case EnemyRole.Boss: return 3000f;
            default: return 10f;
        }
    }

    /// <summary>Leicht x0,5, Normal x1, Schwer x5.</summary>
    public static float DifficultyFactor(Difficulty.Level level)
    {
        switch (level)
        {
            case Difficulty.Level.Easy: return 0.5f;
            case Difficulty.Level.Hard: return 5f;
            default: return 1f;
        }
    }

    // ==================================================================
    //  Zaehler des laufenden Laufs
    // ==================================================================

    private static bool counting;
    private static float earned;

    /// <summary>Besiegte Gegner und benutzte Mixer - gehen als Details mit ans Scoreboard.</summary>
    public static int Kills { get; private set; }
    public static int Mixers { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        counting = false;
        earned = 0f;
        Kills = Mixers = 0;
        LastScore = 0;
    }

    /// <summary>Fortschritt des Laufs: 1 am Anfang, +1 je <see cref="ProgressStep"/> Sekunden.</summary>
    public static float Progress
    {
        get
        {
            float t = GameManager.Instance != null ? GameManager.Instance.gameTime : 0f;
            return 1f + t / ProgressStep;
        }
    }

    /// <summary>Vom GameManager beim Start eines Laufs. Die Test-Szene zaehlt nicht.</summary>
    public static void RunStarted()
    {
        counting = MapDefinition.Active != null;
        earned = 0f;
        Kills = Mixers = 0;
        LastScore = 0;
    }

    /// <summary>Ein Gegner ist gefallen (<see cref="Enemy"/>).</summary>
    public static void AddKill(EnemyRole role)
    {
        if (!counting) return;
        Kills++;
        earned += KillPoints(role) * Progress;
    }

    /// <summary>Ein Mixer wurde benutzt (<see cref="MixerObject"/>).</summary>
    public static void AddMixer()
    {
        if (!counting) return;
        Mixers++;
        earned += MixerPoints * Progress;
    }

    /// <summary>Lauf-Level des Spielers gerade (1 am Start).</summary>
    private static int PlayerLevel =>
        PlayerController.Instance != null ? Mathf.Max(1, PlayerController.Instance.currentLevel) : 1;

    /// <summary>
    /// Punktestand nach <paramref name="seconds"/> Laufzeit beim Lauf-Level
    /// <paramref name="level"/>, mit Schwierigkeit - so geht er ans Scoreboard.
    /// </summary>
    public static int Total(float seconds, int level)
    {
        double raw = earned
                   + Mathf.Max(0f, seconds) * PointsPerSecond
                   + Mathf.Max(0, level - 1) * PointsPerLevel;
        raw *= DifficultyFactor(RunDifficulty.Level);
        // Steam speichert int32.
        return (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, System.Math.Round(raw)));
    }

    /// <summary>
    /// Vom GameManager am Ende eines Laufs: Endless-Laeufe gehen ans
    /// Scoreboard ihrer Welt. Ein Lauf wird nur einmal eingereicht.
    /// </summary>
    public static void RunEnded(float seconds)
    {
        if (!counting || !GameSession.IsEndless) return;

        // Der Director kann beim Tod schon weg sein - die Karte weiss den Plan auch.
        string plan = SpawnDirector.Active != null ? SpawnDirector.Active.PlanId
                    : MapDefinition.Active != null ? MapDefinition.Active.PlanId : null;
        if (string.IsNullOrEmpty(plan)) return;

        int level = PlayerLevel;
        int score = Total(seconds, level);
        counting = false;
        Debug.Log($"[EndlessScore] {plan}: {score} Punkte ({Mathf.FloorToInt(seconds)} s, {Kills} Kills, " +
                  $"{Mixers} Mixer, Level {level}, {RunDifficulty.Level}).");
        LevelRecords.ReportScore(LevelRecords.CurrentMapId(), score);
        SteamScoreboard.Submit(plan, score, new[]
        {
            Mathf.FloorToInt(seconds),
            Kills,
            Mixers,
            (int)RunDifficulty.Level,
            Shop.RunSkinIndex,
            level,
        });
        LastScore = score;
    }

    /// <summary>Was der letzte Endless-Lauf eingebracht hat (fuer den Abschluss-Bildschirm).</summary>
    public static int LastScore { get; private set; }
}
