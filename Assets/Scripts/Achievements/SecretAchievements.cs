using UnityEngine;

/// <summary>
/// Die geheimen Erfolge, die einen ganzen Lauf beobachten. Der Lauf meldet nur,
/// was passiert (Leben verloren, Herz gegessen, Boss gefallen) - entschieden
/// wird hier.
///
/// "Level geschafft" heisst: Story-Sieg (GameManager.EndRun) oder im Endless
/// der Endboss der Welt besiegt. Aufgeben zaehlt nie - sonst koennte man nach
/// fuenf Minuten ohne Treffer einfach gehen.
///
/// Nur auf echten Karten, nicht in der Test-Szene. Die Hub-Erfolge
/// (Kaffeepause) laufen ueber <see cref="HubTick"/>.
/// </summary>
public static class SecretAchievements
{
    /// <summary>So lange muss man ohne einen einzigen Kill durchhalten.</summary>
    public const float PacifistSeconds = 90f;

    /// <summary>Salzsaeule: so lange stillstehen ...</summary>
    public const float StatueSeconds = 60f;

    /// <summary>... und das innerhalb dieser ersten Sekunden des Laufs.</summary>
    public const float StatueWindow = 300f;

    /// <summary>Kurzer Prozess: Tod vor dieser Spielzeit.</summary>
    public const float ShortRunSeconds = 15f;

    /// <summary>Nur noch ein Kruemel: hoechstens so viel Leben beim Sieg.</summary>
    public const float LastCrumbHealth = 0.05f;

    /// <summary>Nachteule: Stunde der Systemuhr (3 = 3:00 bis 3:59).</summary>
    public const int NightOwlHour = 3;

    /// <summary>Kaffeepause: so lange im Hub keine Taste, keine Maus.</summary>
    public const float CoffeeBreakSeconds = 300f;

    private static bool healthLost;
    private static bool heartEaten;
    private static bool levelBeaten;
    private static float lastMoveTime;

    private static float lastHubInput;
    private static Vector3 lastMouse;

    private static bool OnMap => MapDefinition.Active != null;

    /// <summary>Vom GameManager beim Start eines Laufs.</summary>
    public static void RunStarted()
    {
        healthLost = false;
        heartEaten = false;
        levelBeaten = false;
        lastMoveTime = 0f;
    }

    /// <summary>Echtes Leben verloren - was der Schild abfaengt, zaehlt nicht.</summary>
    public static void HealthLost() => healthLost = true;

    /// <summary>Herz oder Goldherz eingesammelt.</summary>
    public static void HeartEaten() => heartEaten = true;

    /// <summary>Vom Spieler in jedem Frame, in dem er laeuft.</summary>
    public static void Moved(float gameTime) => lastMoveTime = gameTime;

    /// <summary>Vom GameManager einmal je Sekunde Spielzeit.</summary>
    public static void Tick(float gameTime, int kills)
    {
        if (!OnMap) return;

        if (gameTime >= PacifistSeconds && kills == 0 && !Ach.SecretPacifist.IsUnlocked)
            Achievements.Unlock(Ach.SecretPacifist);

        if (gameTime <= StatueWindow && gameTime - lastMoveTime >= StatueSeconds && !Ach.SecretStatue.IsUnlocked)
            Achievements.Unlock(Ach.SecretStatue);

        if (System.DateTime.Now.Hour == NightOwlHour && !Ach.SecretNightOwl.IsUnlocked)
            Achievements.Unlock(Ach.SecretNightOwl);
    }

    /// <summary>Der Lauf ist mit dem Tod zu Ende (nicht aufgegeben).</summary>
    public static void Died(float gameTime)
    {
        if (!OnMap) return;
        if (gameTime < ShortRunSeconds) Achievements.Unlock(Ach.SecretShortRun);
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

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (player.playerMaxHealth > 0f && player.playerHealth / player.playerMaxHealth <= LastCrumbHealth)
            Achievements.Unlock(Ach.SecretLastCrumb);

        // Der Tofu hat ohnehin nur seine Startwaffe - fuer ihn waere das geschenkt.
        if (!GameSession.IsEndless && !Characters.OnlyStartWeapon(Shop.RunSkinIndex) && OnlyStartWeapon(player))
            Achievements.Unlock(Ach.SecretMinimalist);
    }

    /// <summary>
    /// Hatte der Lauf nur die Startwaffe? Was eine Evo geschluckt hat, steht
    /// auf <see cref="Weapon.RemovedLevel"/> - war also auch mal da.
    /// </summary>
    private static bool OnlyStartWeapon(PlayerController player)
    {
        if (player.activeWeapon == null) return false;
        int start = Mathf.Clamp(Shop.RunStartWeapon, 0, player.activeWeapon.Length - 1);

        for (int i = 0; i < player.activeWeapon.Length; i++)
        {
            Weapon w = player.activeWeapon[i];
            if (i == start || w == null || w.IsCopy) continue;
            if (w.weaponLevel >= 0 || w.weaponLevel == Weapon.RemovedLevel || w.hasBeenRemoved) return false;
        }
        return true;
    }

    /// <summary>Von Weapon.LevelUP: Glaskanone und Verfluchter Zucker gleichzeitig auf Maximalstufe?</summary>
    public static void WeaponLevelled()
    {
        PlayerController player = PlayerController.Instance;
        if (!OnMap || player == null || player.activeBuffs == null || Ach.SecretSugarRush.IsUnlocked) return;

        bool glass = false, curse = false;
        foreach (Weapon b in player.activeBuffs)
        {
            if (b == null || b.maxweaponLevel <= 0 || b.weaponLevel < b.maxweaponLevel) continue;
            if (b.weaponID == "buff_glass_cannon") glass = true;
            else if (b.weaponID == "buff_curse") curse = true;
        }

        if (glass && curse) Achievements.Unlock(Ach.SecretSugarRush);
    }

    /// <summary>Hub ist (wieder) da - die Kaffeepause beginnt von vorn.</summary>
    public static void HubEntered()
    {
        lastHubInput = Time.unscaledTime;
        lastMouse = Input.mousePosition;
    }

    /// <summary>Vom HubUI in jedem Frame.</summary>
    public static void HubTick()
    {
        Vector3 mouse = Input.mousePosition;
        if (Input.anyKey || mouse != lastMouse || Input.mouseScrollDelta != Vector2.zero)
        {
            lastHubInput = Time.unscaledTime;
            lastMouse = mouse;
            return;
        }

        if (Time.unscaledTime - lastHubInput >= CoffeeBreakSeconds && !Ach.SecretCoffeeBreak.IsUnlocked)
            Achievements.Unlock(Ach.SecretCoffeeBreak);
    }
}
