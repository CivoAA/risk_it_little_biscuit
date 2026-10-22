using UnityEngine;

/// <summary>
/// Der Chaos-Regler: eine Zahl, aus der alles folgt, was einen Lauf schwerer
/// macht.
///
/// Chaos 1 = die normale Schwierigkeit. Darunter wird es leichter, darueber
/// kommen mehr und zaehere Gegner - und es gibt mehr dafuer. Die Belohnung
/// steigt mit, sonst dreht niemand freiwillig hoch.
///
/// Gespeist wird der Wert aus <see cref="GameSession.Chaos"/> (Auswahl im Hub)
/// plus der Rampe im Endless-Modus. Spaeter koennen Fluch-Items oder der Shop
/// einfach oben drauf addieren - die Formeln stehen alle hier und nirgends
/// sonst.
/// </summary>
public static class RunDifficulty
{
    /// <summary>Wie schnell Chaos im Endless-Modus pro Minute steigt.</summary>
    public const float EndlessRampPerMinute = 0.08f;

    /// <summary>
    /// Endless: Anteil an Gold und Charakter-XP, der im Endlos-Teil (nach dem
    /// Story-Teil) noch zaehlt. 0.1 = ein Zehntel - sonst waere Endless die
    /// beste Farm im Spiel. Die XP im Lauf (Level-Ups) bleiben voll.
    /// </summary>
    public const float EndlessRewardShare = 0.1f;

    /// <summary>
    /// Endless: der Story-Teil ist vorbei (Boss der Welt gefallen oder Plan
    /// durch). Setzt der <see cref="SpawnDirector"/>.
    /// </summary>
    public static bool InEndlessPart { get; private set; }

    /// <summary>Anteil, mit dem Charakter-XP gerade zaehlen.</summary>
    public static float CharXpShare => InEndlessPart ? EndlessRewardShare : 1f;

    private static float chaos = 1f;

    /// <summary>1 = normal. Wird beim Start eines Laufs gesetzt.</summary>
    public static float Chaos
    {
        get { return chaos; }
        set { chaos = Mathf.Max(0.25f, value); }
    }

    private static float curse;

    /// <summary>
    /// Fluch-Buffs (<see cref="CursedSugar"/>) legen ihren Wert hier ab. Er
    /// kommt oben auf <see cref="Chaos"/> drauf und ueberlebt so die
    /// Endless-Rampe, die Chaos laufend neu setzt. Absolut, nicht kumulativ.
    /// </summary>
    public static float Curse
    {
        get { return curse; }
        set { curse = Mathf.Max(0f, value); }
    }

    /// <summary>Chaos plus Fluch - daraus rechnen alle Faktoren.</summary>
    public static float Effective => Mathf.Max(0.25f, chaos + curse);

    /// <summary>Mehr Gegner gleichzeitig auf dem Feld.</summary>
    public static float CountFactor => 1f + (Effective - 1f) * 0.8f;

    /// <summary>Zaeher. Waechst leicht ueberproportional, damit hohes Chaos beisst. Dazu die Schwierigkeit.</summary>
    public static float HealthFactor => Mathf.Pow(Effective, 1.4f) * WorldHealth * Difficulty.EnemyFactor(Level) * GlobalHealth;

    /// <summary>Leben aller Gegner in jedem Lauf (0.95 = 5 % weniger), auch Elites und Bosse.</summary>
    public const float GlobalHealth = 0.95f;

    /// <summary>Schwierigkeit des laufenden Laufs - beim Start aus dem Hub uebernommen.</summary>
    public static Difficulty.Level Level { get; private set; } = Difficulty.Level.Normal;

    /// <summary>
    /// Faktor auf jeden Schaden, den der Spieler nimmt (<see cref="PlayerController.TakeDamage"/>).
    /// Dort statt am Gegner, damit auch Boss-Attacken mit eigenem Schaden mitzaehlen.
    /// </summary>
    public static float DifficultyDamage => Difficulty.EnemyFactor(Level);

    /// <summary>Mehr Schaden - vorsichtiger als Leben, sonst wird es unfair statt schwer.</summary>
    public static float DamageFactor => (1f + (Effective - 1f) * 0.5f) * WorldDamage;

    // ------------------------------------------------------------ Weltstufe

    /// <summary>
    /// Wie viel zaeher und haerter die Gegner einer Welt sind - auf alles, auch
    /// Elites und Bosse, unabhaengig von Chaos. Jede Welt der Route liegt ueber
    /// der davor, weil der Spieler ueber Skilltree und Shop mitwaechst. Den Druck
    /// (wie viele) regeln die Plaene selbst (siehe Kopf von <see cref="WavePlans"/>).
    /// Xp ist ein Faktor auf die XP, die Gegner fallen lassen.
    /// Unbekannte Plan-Id = 1.
    /// </summary>
    private static (float Health, float Damage, float Xp) WorldTier(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world2": return (1.0f, 1.0f, 1.0f);    // Welt 1 Wald - Massstab
            case "world1": return (1.6f, 1.1f, 1.05f);   // Welt 2 Kueche
            case "world5": return (2.1f, 1.2f, 1.05f);   // Welt 3 Eisgletscher
            case "world6": return (2.7f, 1.35f, 1.05f);  // Welt 4 Geisterwald
            case "world4": return (3.5f, 1.5f, 1.05f);   // Welt 5 Vulkan
            default: return (1f, 1f, 1f);
        }
    }

    /// <summary>Leben-Faktor der laufenden Welt (<see cref="WorldTier"/>).</summary>
    public static float WorldHealth { get; private set; } = 1f;

    /// <summary>Schaden-Faktor der laufenden Welt (<see cref="WorldTier"/>).</summary>
    public static float WorldDamage { get; private set; } = 1f;

    /// <summary>XP-Faktor der laufenden Welt (<see cref="WorldTier"/>) - rechnet <see cref="Enemy"/> beim Drop ein.</summary>
    public static float WorldXp { get; private set; } = 1f;

    /// <summary>
    /// Ziel-Level der Map fuer den Nachhol-Bonus (<see cref="CharLevel.CatchUpFactor"/>).
    /// Unbekannte Plan-Id = 0 (kein Bonus).
    /// </summary>
    private static int CatchUpTargetFor(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world2": return 9;    // Welt 1 Wald
            case "world1": return 17;   // Welt 2 Kueche
            case "world5": return 25;   // Welt 3 Eisgletscher
            case "world6": return 30;   // Welt 4 Geisterwald
            case "world4": return 35;   // Welt 5 Vulkan
            default: return 0;
        }
    }

    /// <summary>Ziel-Level der laufenden Map (<see cref="CatchUpTargetFor"/>).</summary>
    public static int CatchUpTarget { get; private set; }

    /// <summary>Nachhol-Bonus auf die Charakter-XP, solange der Charakter unter dem Ziel-Level liegt.</summary>
    public static float CharXpCatchUp => CharLevel.CatchUpFactor(Skills.Level, CatchUpTarget);

    /// <summary>Erfahrung und Waehrung. Muss ueber 1 bleiben, sonst lohnt Chaos nicht.</summary>
    public static float RewardFactor => Mathf.Pow(Effective, 1.2f);

    /// <summary>
    /// Setzt den Regler auf den Stand, mit dem der Lauf startet. Ruft der
    /// <see cref="SpawnDirector"/> beim Betreten einer Map auf.
    /// </summary>
    public static void BeginRun(string planId)
    {
        Chaos = GameSession.Chaos;
        Curse = 0f;
        Level = Difficulty.Current;
        InEndlessPart = false;
        (WorldHealth, WorldDamage, WorldXp) = WorldTier(planId);
        CatchUpTarget = CatchUpTargetFor(planId);
        Debug.Log($"[RunDifficulty] Lauf ({planId}) startet auf {Level} mit Chaos {Chaos:0.00} " +
                  $"(Gegner x{CountFactor:0.00}, Leben x{HealthFactor:0.00}, Belohnung x{RewardFactor:0.00}).");
    }

    /// <summary>
    /// Endless: Chaos steigt mit der Laufzeit. Im Story-Modus passiert nichts -
    /// dort macht der Plan die Steigerung ueber den Druck.
    /// </summary>
    public static void UpdateEndlessRamp(float runTime)
    {
        if (!GameSession.IsEndless) return;

        Chaos = GameSession.Chaos + (runTime / 60f) * EndlessRampPerMinute;
    }

    /// <summary>Endless: ab hier zaehlen Gold und Charakter-XP nur noch anteilig.</summary>
    public static void BeginEndlessPart()
    {
        InEndlessPart = true;
    }

    /// <summary>
    /// Extra-Leben fuer Zwischenbosse und Bosse, oben auf Chaos und Weltstufe.
    /// Nur Leben - Schaden und Belohnung bleiben wie sie sind.
    /// </summary>
    public const float BossHealthBonus = 2f;

    /// <summary>Zwischenbosse zusaetzlich (0.95 = 5 % weniger Leben), Bosse nicht.</summary>
    public const float MiniBossHealth = 0.95f;

    /// <summary>Haengt die aktuellen Faktoren an einen frisch erzeugten Gegner.</summary>
    public static void Apply(Enemy enemy)
    {
        if (enemy == null) return;
        bool boss = enemy.Role == EnemyRole.Boss || enemy.Role == EnemyRole.MiniBoss;
        float health = HealthFactor * (boss ? BossHealthBonus : 1f);
        if (enemy.Role == EnemyRole.MiniBoss) health *= MiniBossHealth;
        enemy.ApplyRunScaling(health, DamageFactor, RewardFactor);
    }
}
