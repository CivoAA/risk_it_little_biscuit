/// <summary>
/// Die geheimen Erfolge, die einen ganzen Lauf beobachten. Der Lauf meldet nur,
/// was passiert (Leben verloren, Herz gegessen, Boss gefallen) - entschieden
/// wird hier.
///
/// "Level geschafft" heisst: Story-Sieg (GameManager.EndRun) oder im Endless
/// der Endboss der Welt besiegt. Aufgeben zaehlt nie - sonst koennte man nach
/// fuenf Minuten ohne Treffer einfach gehen.
///
/// Nur auf echten Karten, nicht in der Test-Szene.
/// </summary>
public static class SecretAchievements
{
    /// <summary>So lange muss man ohne einen einzigen Kill durchhalten.</summary>
    public const float PacifistSeconds = 90f;

    private static bool healthLost;
    private static bool heartEaten;
    private static bool levelBeaten;

    private static bool OnMap => MapDefinition.Active != null;

    /// <summary>Vom GameManager beim Start eines Laufs.</summary>
    public static void RunStarted()
    {
        healthLost = false;
        heartEaten = false;
        levelBeaten = false;
    }

    /// <summary>Echtes Leben verloren - was der Schild abfaengt, zaehlt nicht.</summary>
    public static void HealthLost() => healthLost = true;

    /// <summary>Herz oder Goldherz eingesammelt.</summary>
    public static void HeartEaten() => heartEaten = true;

    /// <summary>Vom GameManager einmal je Sekunde Spielzeit.</summary>
    public static void Tick(float gameTime, int kills)
    {
        if (gameTime < PacifistSeconds || kills > 0 || !OnMap) return;
        if (!Ach.SecretPacifist.IsUnlocked) Achievements.Unlock(Ach.SecretPacifist);
    }

    /// <summary>Ein Endboss (EnemyRole.Boss) ist gefallen.</summary>
    public static void BossDefeated(EnemyId boss)
    {
        if (!OnMap) return;

        if (boss == EnemyId.KeksKoenig && Shop.RunSkinIndex == 0)
            Achievements.Unlock(Ach.SecretFamilyReunion);

        // Im Story-Modus kommt der Sieg erst mit dem Abschluss (Herzkammer usw.).
        if (GameSession.IsEndless) LevelBeaten();
    }

    /// <summary>Level geschafft - prueft die "ohne"-Erfolge.</summary>
    public static void LevelBeaten()
    {
        if (levelBeaten || !OnMap) return;
        levelBeaten = true;

        if (!healthLost) Achievements.Unlock(Ach.SecretFlawless);
        if (!heartEaten) Achievements.Unlock(Ach.SecretDiet);
    }
}
