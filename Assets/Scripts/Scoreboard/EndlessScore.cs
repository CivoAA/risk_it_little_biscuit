using UnityEngine;

/// <summary>
/// Der Punktestand eines Laufs - das, was ins Steam-Scoreboard geht
/// (<see cref="SteamScoreboard"/>). Story und Endless landen auf demselben
/// Board der Welt.
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
/// Story-Laeufe verlieren Punkte, je laenger sie ueber <see cref="StoryPar"/>
/// brauchen (<see cref="StoryTimeFactor"/>): im Story geht es darum, den Boss
/// schnell zu legen, im Endless ums Durchhalten - ein Endless-Lauf liegt so
/// immer vorne.
///
/// Gezaehlt wird in jedem Lauf auf einer echten Karte. Nach "Endless
/// weiterspielen" zaehlt der Story-Teil im Endless mit (wie in einem echten
/// Endless-Lauf, der ja auch mit dem Plan der Welt beginnt), dann ohne
/// Zeitabzug. Alle Zahlen stehen hier und nirgends sonst.
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

    /// <summary>Story: bis hierhin (Sekunden) gibt es die vollen Punkte.</summary>
    public const float StoryPar = 20f * 60f;

    /// <summary>Story: je Minute ueber <see cref="StoryPar"/> bleibt nur dieser Anteil uebrig (x0,9, x0,81, ...).</summary>
    public const float StoryKeepPerMinute = 0.9f;

    /// <summary>Story: so viel bleibt mindestens (hoechstens 95 % Abzug).</summary>
    public const float StoryMinFactor = 0.05f;

    /// <summary>
    /// Story-Anteil der Punkte nach <paramref name="seconds"/> Laufzeit: 1 bis
    /// <see cref="StoryPar"/>, danach je Minute x<see cref="StoryKeepPerMinute"/>
    /// (gleitend, 21 min = 90 %, 22 min = 81 %), nie unter <see cref="StoryMinFactor"/>.
    /// </summary>
    public static float StoryTimeFactor(float seconds)
    {
        float over = Mathf.Max(0f, seconds - StoryPar) / 60f;
        return Mathf.Max(StoryMinFactor, Mathf.Pow(StoryKeepPerMinute, over));
    }

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
    /// <paramref name="level"/>, mit Schwierigkeit (und im Story mit
    /// <see cref="StoryTimeFactor"/>) - so geht er ans Scoreboard.
    /// </summary>
    public static int Total(float seconds, int level, bool endless)
    {
        double raw = earned
                   + Mathf.Max(0f, seconds) * PointsPerSecond
                   + Mathf.Max(0, level - 1) * PointsPerLevel;
        raw *= DifficultyFactor(RunDifficulty.Level);
        if (!endless) raw *= StoryTimeFactor(seconds);
        // Steam speichert int32.
        return (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, System.Math.Round(raw)));
    }

    /// <summary>
    /// Vom GameManager am Ende eines Laufs (Tod, Aufgeben, Story-Sieg): der
    /// Punktestand geht ans Scoreboard seiner Welt. Ein Lauf wird nur einmal
    /// eingereicht - ausser er geht als Endless weiter (<see cref="ContinuedAsEndless"/>).
    /// </summary>
    public static void RunEnded(float seconds)
    {
        if (!counting) return;
        bool endless = GameSession.IsEndless;

        // Der Director kann beim Tod schon weg sein - die Karte weiss den Plan auch.
        string plan = SpawnDirector.Active != null ? SpawnDirector.Active.PlanId
                    : MapDefinition.Active != null ? MapDefinition.Active.PlanId : null;
        if (string.IsNullOrEmpty(plan)) return;

        int level = PlayerLevel;
        int score = Total(seconds, level, endless);
        counting = false;
        Debug.Log($"[EndlessScore] {plan} ({(endless ? "Endless" : "Story")}): {score} Punkte " +
                  $"({Mathf.FloorToInt(seconds)} s, {Kills} Kills, {Mixers} Mixer, Level {level}, {RunDifficulty.Level}).");
        if (endless)
        {
            // Bestwert der Levelauswahl und der Erfolg gelten nur fuer Endless.
            LevelRecords.ReportScore(LevelRecords.CurrentMapId(), score);
            if (score >= 10000000 && !ScoreIntegrity.BlockedForPlayer) Achievements.Unlock(Ach.EndlessScore10M);
        }
        SteamScoreboard.Submit(plan, score, new[]
        {
            Mathf.FloorToInt(seconds),
            Kills,
            Mixers,
            (int)RunDifficulty.Level,
            Shop.RunSkinIndex,
            level,
            endless ? 1 : 0,
        });
        LastScore = score;
    }

    /// <summary>
    /// "Endless weiterspielen" nach dem Story-Sieg: der Sieg ist schon
    /// eingereicht, es wird weitergezaehlt (Kills und Mixer bleiben), und der
    /// Tod am Ende reicht den ganzen Lauf als Endless ein.
    /// </summary>
    public static void ContinuedAsEndless()
    {
        counting = MapDefinition.Active != null;
    }

    /// <summary>Was der letzte Lauf eingebracht hat (fuer den Abschluss-Bildschirm).</summary>
    public static int LastScore { get; private set; }
}
