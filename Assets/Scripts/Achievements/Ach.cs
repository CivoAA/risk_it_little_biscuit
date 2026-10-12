using System.Collections.Generic;

/// <summary>
/// DER KATALOG. Hier - und nur hier - werden Achievements angelegt.
///
/// Ein neues Achievement hinzufügen:
///   1. Eine Zeile in den passenden Abschnitt unten schreiben.
///   2. An der Stelle im Spiel, die es auslöst, Achievements.Unlock(Ach.MeinDing)
///      bzw. Achievements.Progress(Ach.MeinDing, 1f) aufrufen.
///   3. Ein PNG namens [IconKey].png nach Assets/Resources/Achievements/ legen.
///      Fehlt es, zeigt die Anzeige so lange den Platzhalter - es bricht nichts.
///   4. Für Steam: Tools -> Erfolge -> Steamworks-Liste ausgeben und die
///      Ausgabe im Steamworks-Backend eintragen. Ohne das funktioniert alles im
///      Spiel, es gibt nur kein Steam-Popup.
///
/// REIHENFOLGE: Statische Feld-Initialisierer laufen laut C#-Spezifikation in
/// Textreihenfolge. Die Reihenfolge der Zeilen in dieser Datei ist deshalb exakt
/// die Reihenfolge in der Anzeige. Zwei Achievements tauschen = zwei Zeilen
/// tauschen. Angezeigt wird zuerst nach Kategorie gruppiert, darin gilt dann
/// diese Reihenfolge.
///
/// IDs NIEMALS ÄNDERN. Die Id ist gleichzeitig der Schlüssel in der Save-Datei
/// und der API-Name bei Steam. Ein Umbenennen sieht für Spieler und für Steam
/// aus wie "altes Achievement weg, neues dazu".
/// </summary>
public static class Ach
{
    // Muss textlich vor allen Def(...)-Feldern stehen, sonst ist die Liste beim
    // Registrieren noch null.
    private static readonly List<AchievementDef> Registry = new List<AchievementDef>();

    // ==================================================================
    //  Fortschritt
    // ==================================================================

    public static readonly AchievementDef FirstGame = Def(
        "First_Game", "Play your first game", "Enter your first game.",
        AchievementCategory.Progression);

    public static readonly AchievementDef FirstWin = Def(
        "First_Win", "Congratulations!", "You have beaten a level for the first time!",
        AchievementCategory.Progression);

    public static readonly AchievementDef FirstDeath = Def(
        "First_Death", "DIE!!!!!!", "Die for the first time.",
        AchievementCategory.Progression);

    public static readonly AchievementDef Death = Def(
        "Death", "Death", "Be death itself!",
        AchievementCategory.Progression, grantsUnlock: "unlock_cursed_sugar");

    // Ein Endboss je Welt. Ausgeloest in Enemy.GrantRewards ueber BossVictory().

    public static readonly AchievementDef BossCookieKing = Def(
        "Boss_Cookie_King", "Crumbled Crown", "Defeat the Cookie King in the Kitchen.",
        AchievementCategory.Progression);

    public static readonly AchievementDef BossEmberroot = Def(
        "Boss_Emberroot", "Uprooted", "Defeat Emberroot in the Forest.",
        AchievementCategory.Progression);

    public static readonly AchievementDef BossIceEmperor = Def(
        "Boss_Ice_Emperor", "Meltdown", "Defeat the Ice Emperor on the Ice Cream Glacier.",
        AchievementCategory.Progression, grantsUnlock: "unlock_char_inari");

    public static readonly AchievementDef BossPhantom = Def(
        "Boss_Phantom", "Lights On", "Defeat the Phantom in the Haunted Forest.",
        AchievementCategory.Progression);

    // ==================================================================
    //  Kampf
    // ==================================================================

    public static readonly AchievementDef Kill100 = Def(
        "Kill_100", "Enemy Breaker I", "Defeat 100 enemies.",
        AchievementCategory.Combat, goal: 100f);

    public static readonly AchievementDef Kill1000 = Def(
        "Kill_1000", "Enemy Breaker II", "Defeat 1,000 enemies.",
        AchievementCategory.Combat, goal: 1000f);

    public static readonly AchievementDef Kill10000 = Def(
        "Kill_10000", "Enemy Breaker III", "Defeat 10,000 enemies and prove your might.",
        AchievementCategory.Combat, goal: 10000f);

    public static readonly AchievementDef Kill10Miniboss = Def(
        "Kill_10_Miniboss", "Challenger",
        "Defeat 10 <color=#FFD040>Elite</color> enemies - you will know them by their glow.",
        AchievementCategory.Combat, goal: 10f);

    public static readonly AchievementDef Kill100Miniboss = Def(
        "Kill_100_Miniboss", "Conqueror", "Defeat 100 <color=#FFD040>Elite</color> enemies.",
        AchievementCategory.Combat, goal: 100f);

    public static readonly AchievementDef KillSlimeKing = Def(
        "Kill_Slime_King", "Dethroned", "Defeat the Slime King.",
        AchievementCategory.Combat);

    /// <summary>Zaehlt in Enemy.GrantRewards (Zwischenboss Schleimkoenig) mit.</summary>
    public static readonly AchievementDef KillSlimeKing10 = Def(
        "Kill_Slime_King_10", "Long Live the King", "Defeat the Slime King 10 times.",
        AchievementCategory.Combat, goal: 10f, grantsUnlock: "unlock_char_slime_king");

    // Zwischenbosse und Endbosse insgesamt, egal welche. Zaehlen in
    // Enemy.GrantRewards (Rolle MiniBoss bzw. Boss).

    public static readonly AchievementDef Midbosses5 = Def(
        "Midbosses_5", "Midboss Hunter I", "Defeat 5 midbosses.",
        AchievementCategory.Combat, goal: 5f);

    public static readonly AchievementDef Midbosses20 = Def(
        "Midbosses_20", "Midboss Hunter II", "Defeat 20 midbosses.",
        AchievementCategory.Combat, goal: 20f);

    public static readonly AchievementDef Midbosses45 = Def(
        "Midbosses_45", "Midboss Hunter III", "Defeat 45 midbosses.",
        AchievementCategory.Combat, goal: 45f);

    public static readonly AchievementDef Bosses5 = Def(
        "Bosses_5", "Boss Slayer I", "Defeat 5 bosses.",
        AchievementCategory.Combat, goal: 5f);

    public static readonly AchievementDef Bosses20 = Def(
        "Bosses_20", "Boss Slayer II", "Defeat 20 bosses.",
        AchievementCategory.Combat, goal: 20f);

    public static readonly AchievementDef Bosses45 = Def(
        "Bosses_45", "Boss Slayer III", "Defeat 45 bosses.",
        AchievementCategory.Combat, goal: 45f);

    // ==================================================================
    //  Evolutionen
    // ==================================================================

    public static readonly AchievementDef BladeSwarmEvo = Def(
        "Blade_Swarm_Evo", "Blade Swarm Evo", "Obtained the Blade Storm Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef BloodyForkEvo = Def(
        "Bloody_Fork_Evo", "Bloody Fork Evo", "Obtained the Bloody Fork Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef ExplosiveStarEvo = Def(
        "Explosive_Star_Evo", "Explosive Star Evo", "Obtained the Explosive Star Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef ShuriBlastEvo = Def(
        "Shuri_Blast_Evo", "Shuri Blast Evo", "Obtained the Shuri Blast Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef BobaSawEvo = Def(
        "Boba_Saw_Evo", "Boba Saw Evo", "Obtained the Boba Saw Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef BoomerangEvo = Def(
        "Boomerang_Evo", "Boomerang Evo", "Obtained the Boomerang Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef BombSawEvo = Def(
        "BombSaw_Evo", "Bomb Saw Evo", "Obtained the Bomb Saw Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef StickyShatterEvo = Def(
        "Sticky_Shatter_Evo", "Sticky Shatter Evo", "Obtained the Sticky Shatter Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef TyphoonFanEvo = Def(
        "Typhoon_Fan_Evo", "Typhoon Fan Evo", "Obtained the Typhoon Fan Evo for the first time!",
        AchievementCategory.Evolutions);

    // Evos der Startwaffen, die es noch nicht gibt (Platzhalter in
    // Characters.StartEvoIdByskin). Ausgeloest in LevelUpButton ueber EvoFor() -
    // sobald die Evo ein Rezept hat, laeuft das von allein.

    public static readonly AchievementDef ChocoChipsEvo = Def(
        "Choco_Chips_Evo", "Choco Chips Evo", "Obtained the Choco Chips Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef SwordSlashEvo = Def(
        "Sword_Slash_Evo", "Sword Slash Evo", "Obtained the Sword Slash Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef MochiMelodyEvo = Def(
        "Mochi_Melody_Evo", "Mochi Melody Evo", "Obtained the Mochi Melody Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef FoxfireEvo = Def(
        "Foxfire_Evo", "Foxfire Evo", "Obtained the Foxfire Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef RoyalSplatEvo = Def(
        "Royal_Splat_Evo", "Royal Splat Evo", "Obtained the Royal Splat Evo for the first time!",
        AchievementCategory.Evolutions);

    public static readonly AchievementDef CrumbMayhemEvo = Def(
        "Crumb_Mayhem_Evo", "Crumb Mayhem Evo", "Obtained the Crumb Mayhem Evo for the first time!",
        AchievementCategory.Evolutions);

    // ==================================================================
    //  Charaktere
    // ==================================================================
    // Ein Sieg je Charakter, ausgeloest in GameManager.GameOver ueber
    // CharacterWin(). Zusammen mit den Startwaffen-Evos zaehlen sie auf
    // AllCharacters - das schaltet den Tofu frei (Unlocks.CheckAllCharacters).

    public static readonly AchievementDef WinCookie = Def(
        "Win_Cookie", "Fresh from the Oven", "Beat a level with the Cookie.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinJam = Def(
        "Win_Jam", "Sticky Victory", "Beat a level with Jam.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinOnigiri = Def(
        "Win_Onigiri", "Rice and Shine", "Beat a level with Onigiri.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinToast = Def(
        "Win_Toast", "A Toast!", "Beat a level with Toast.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinOnionKnight = Def(
        "Win_Onion_Knight", "Tears of Joy", "Beat a level with the Onion Knight.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinMochi = Def(
        "Win_Mochi", "Soft Power", "Beat a level with Mochi.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinInari = Def(
        "Win_Inari", "Sly Fox", "Beat a level with Inari.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinSlimeKing = Def(
        "Win_Slime_King", "Royal Decree", "Beat a level with the Slime King.",
        AchievementCategory.Characters);

    public static readonly AchievementDef WinTofu = Def(
        "Win_Tofu", "Unflavored", "Beat a level with Tofu.",
        AchievementCategory.Characters);

    /// <summary>
    /// Mit jedem Charakter (ausser dem Tofu) einmal gewonnen und einmal seine
    /// Startwaffen-Evo bekommen: 8 Siege + 8 Evos = 16. Der Zaehler wird nicht
    /// hochgezaehlt, sondern nachgerechnet (Unlocks.CheckAllCharacters).
    /// </summary>
    public static readonly AchievementDef AllCharacters = Def(
        "All_Characters", "Full Bento",
        "Beat a level and obtain the starting weapon Evo with every character except Tofu.",
        AchievementCategory.Characters, goal: 16f, grantsUnlock: "unlock_char_tofu");

    // ==================================================================
    //  Waffen auf Maximalstufe
    // ==================================================================

    public static readonly AchievementDef MaxCoffeePool = Def(
        "Max_Level_CoffePool", "Max Level: Coffee Pool", "Reach the maximum level of the Coffee Pool weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxCookiesaw = Def(
        "Max_Level_Cookiesaw", "Max Level: Cookiesaw", "Reach the maximum level of the Cookiesaw weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxButterblast = Def(
        "Max_Level_Butterblast", "Max Level: Butterblast", "Reach the maximum level of the Butterblast weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxThrowingJamJar = Def(
        "Max_Level_ThrowingJamJar", "Max Level: Throwing Jam Jar", "Reach the maximum level of the Throwing Jam Jar weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxVoidSpike = Def(
        "Max_Level_VoidSpike", "Max Level: Void Spike", "Reach the maximum level of the Void Spike weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxFireBall = Def(
        "Max_Level_FireBall", "Max Level: Fireball", "Reach the maximum level of the Fireball weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxBoomerang = Def(
        "Max_Level_Boomerang", "Max Level: Boomerang", "Reach the maximum level of the Boomerang weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxCrumbTrail = Def(
        "Max_Level_CrumbTrail", "Max Level: Crumb Trail", "Reach the maximum level of the Crumb Trail weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxVortex = Def(
        "Max_Level_Vortex", "Max Level: Vortex", "Reach the maximum level of the Vortex weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxTurret = Def(
        "Max_Level_Turret", "Max Level: Turret", "Reach the maximum level of the Turret weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxBobaGun = Def(
        "Max_Level_BobaGun", "Max Level: Boba Gun", "Reach the maximum level of the BobaGun weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxShurikookie = Def(
        "Max_Level_Shurikookie", "Max Level: Shurikookie", "Reach the maximum level of the Shurikookie weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxSpikeFork = Def(
        "Max_Level_SpikeFork", "Max Level: Spike Fork", "Reach the maximum level of the Spike Fork weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxDeathstrike = Def(
        "Max_Level_Deathstrike", "Max Level: Deathstrike", "Reach the maximum level of the Deathstrike weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxCelestialStar = Def(
        "Max_Level_CelestialStar", "Max Level: Celestial Star", "Reach the maximum level of the Celestial Star weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxBladeSwarm = Def(
        "Max_Level_BladeSwarm", "Max Level: Blade Swarm", "Reach the maximum level of the Blade Swarm weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxCandyBomb = Def(
        "Max_Level_CandyBomb", "Max Level: Candy Bomb", "Reach the maximum level of the Candy Bomb weapon.",
        AchievementCategory.Weapons);

    // Ab hier ohne eigene Abfrage in der Waffe: Weapon.LevelUP fragt MaxLevelFor().

    public static readonly AchievementDef MaxTimeLaser = Def(
        "Max_Level_TimeLaser", "Max Level: Time Laser", "Reach the maximum level of the Time Laser weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxSwordSlash = Def(
        "Max_Level_SwordSlash", "Max Level: Sword Slash", "Reach the maximum level of the Sword Slash weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxSaladFan = Def(
        "Max_Level_SaladFan", "Max Level: Salad Fan", "Reach the maximum level of the Salad Fan weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxChocoChips = Def(
        "Max_Level_ChocoChips", "Max Level: Choco Chips", "Reach the maximum level of the Choco Chips weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxMilkDunk = Def(
        "Max_Level_MilkDunk", "Max Level: Milk Dunk", "Reach the maximum level of the Milk Dunk weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxFoxfire = Def(
        "Max_Level_Foxfire", "Max Level: Foxfire", "Reach the maximum level of the Foxfire weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxRoyalSplat = Def(
        "Max_Level_RoyalSplat", "Max Level: Royal Splat", "Reach the maximum level of the Royal Splat weapon.",
        AchievementCategory.Weapons);

    public static readonly AchievementDef MaxMochiMelody = Def(
        "Max_Level_MochiMelody", "Max Level: Mochi Melody", "Reach the maximum level of the Mochi Melody weapon.",
        AchievementCategory.Weapons);

    // ==================================================================
    //  Buffs auf Maximalstufe
    // ==================================================================

    public static readonly AchievementDef MaxExtraShot = Def(
        "Max_Level_ExtraShot", "Max Level: Extra Shot", "Reach the maximum level of the Extra Shot buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxCooldown = Def(
        "Max_Level_Cooldown", "Max Level: Cooldown", "Reach the maximum level of the Cooldown buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxDuration = Def(
        "Max_Level_Duration", "Max Level: Duration", "Reach the maximum level of the Duration buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxGlassCannon = Def(
        "Max_Level_GlassCannon", "Max Level: Glass Cannon", "Reach the maximum level of the Glass Cannon buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxSecondChance = Def(
        "Max_Level_SecondChance", "Max Level: Second Chance", "Reach the maximum level of the Second Chance buff.",
        AchievementCategory.Buffs);

    // Ab hier ohne eigene Abfrage im Buff: Weapon.LevelUP fragt MaxLevelFor().

    public static readonly AchievementDef MaxDamage = Def(
        "Max_Level_Damage", "Max Level: Damage", "Reach the maximum level of the Damage buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxCritChance = Def(
        "Max_Level_CritChance", "Max Level: Crit Chance", "Reach the maximum level of the Crit Chance buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxCritDamage = Def(
        "Max_Level_CritDamage", "Max Level: Crit Damage", "Reach the maximum level of the Crit Damage buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxAoeRange = Def(
        "Max_Level_AoeRange", "Max Level: AOE Range", "Reach the maximum level of the AOE Range buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxMaxHp = Def(
        "Max_Level_MaxHp", "Max Level: Max HP", "Reach the maximum level of the Max HP buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxRegeneration = Def(
        "Max_Level_Regeneration", "Max Level: HP Regeneration", "Reach the maximum level of the HP Regeneration buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxArmor = Def(
        "Max_Level_Armor", "Max Level: Armor", "Reach the maximum level of the Armor buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxDodge = Def(
        "Max_Level_Dodge", "Max Level: Dodge Chance", "Reach the maximum level of the Dodge Chance buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxLifeSteal = Def(
        "Max_Level_LifeSteal", "Max Level: Life Steal", "Reach the maximum level of the Life Steal buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxMoveSpeed = Def(
        "Max_Level_MoveSpeed", "Max Level: Move Speed", "Reach the maximum level of the Move Speed buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxPickupRange = Def(
        "Max_Level_PickupRange", "Max Level: Pickup Range", "Reach the maximum level of the Pickup Range buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxXpGain = Def(
        "Max_Level_XpGain", "Max Level: Experience Gain", "Reach the maximum level of the Experience Gain buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxCurrency = Def(
        "Max_Level_Currency", "Max Level: Currency Gain", "Reach the maximum level of the Currency Gain buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxLuck = Def(
        "Max_Level_Luck", "Max Level: Luck", "Reach the maximum level of the Luck buff.",
        AchievementCategory.Buffs);

    public static readonly AchievementDef MaxCursedSugar = Def(
        "Max_Level_CursedSugar", "Max Level: Cursed Sugar", "Reach the maximum level of the Cursed Sugar buff.",
        AchievementCategory.Buffs);

    // ==================================================================
    //  Geheim
    // ==================================================================
    // Beispiel - hidden: true zeigt in der Liste "???", bis es freigeschaltet ist:
    //
    // public static readonly AchievementDef SecretRamen = Def(
    //     "Secret_Ramen", "UwU", "You found the ramen.",
    //     AchievementCategory.Secrets, hidden: true);

    // ==================================================================
    //  Ab hier nur noch Mechanik - nichts, was man zum Anlegen braucht.
    // ==================================================================

    /// <summary>Alle Achievements in Katalogreihenfolge.</summary>
    public static IReadOnlyList<AchievementDef> All => Registry;

    private static readonly Dictionary<string, AchievementDef> ById =
        new Dictionary<string, AchievementDef>();

    /// <summary>Sucht ein Def über seine Id. Null, wenn es die Id nicht (mehr) gibt.</summary>
    public static AchievementDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureIndex();
        return ById.TryGetValue(id, out AchievementDef def) ? def : null;
    }

    /// <summary>
    /// Das Achievement, das diesen Unlock mitvergibt (<see cref="AchievementDef.GrantsUnlock"/>).
    /// Null, wenn der Unlock nicht an einem Achievement hängt - die meisten
    /// fallen im Spiel an, nicht an der Achievement-Liste. Die Unlock-Anzeige
    /// nennt damit die Bedingung, statt nur "???" zu zeigen.
    /// </summary>
    public static AchievementDef FindByUnlock(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId)) return null;

        foreach (AchievementDef def in Registry)
        {
            if (def.GrantsUnlock == unlockId) return def;
        }

        return null;
    }

    /// <summary>
    /// Weapon.weaponID -> "Maximalstufe"-Erfolg. Weapon.LevelUP schaltet darueber
    /// frei, damit nicht jede Waffe die Abfrage selbst tragen muss. Die alten
    /// Waffen fragen zusaetzlich noch in ihrem Update - doppelt schadet nicht.
    /// </summary>
    private static readonly Dictionary<string, AchievementDef> MaxLevelByWeapon =
        new Dictionary<string, AchievementDef>
    {
        { "coffee_pool",    MaxCoffeePool },     { "cookie_saw",    MaxCookiesaw },
        { "butterblast",    MaxButterblast },    { "jam_jar",       MaxThrowingJamJar },
        { "void_spike",     MaxVoidSpike },      { "fire_ball",     MaxFireBall },
        { "boomerang",      MaxBoomerang },      { "crumb_trail",   MaxCrumbTrail },
        { "vortex",         MaxVortex },         { "turret",        MaxTurret },
        { "boba_gun",       MaxBobaGun },        { "shurikookie",   MaxShurikookie },
        { "spike_fork",     MaxSpikeFork },      { "deathstrike",   MaxDeathstrike },
        { "celestial_star", MaxCelestialStar },  { "blade_swarm",   MaxBladeSwarm },
        { "candy_bomb",     MaxCandyBomb },      { "time_laser",    MaxTimeLaser },
        { "sword_slash",    MaxSwordSlash },     { "salad_fan",     MaxSaladFan },
        { "choco_chips",    MaxChocoChips },     { "milk_dunk",     MaxMilkDunk },
        { "foxfire",        MaxFoxfire },        { "royal_splat",   MaxRoyalSplat },
        { "mochi_melody",   MaxMochiMelody },

        { "buff_extra_shot",    MaxExtraShot },     { "buff_cooldown",     MaxCooldown },
        { "buff_duration",      MaxDuration },      { "buff_glass_cannon", MaxGlassCannon },
        { "buff_second_chance", MaxSecondChance },  { "buff_damage",       MaxDamage },
        { "buff_crit_chance",   MaxCritChance },    { "buff_crit_damage",  MaxCritDamage },
        { "buff_aoe_range",     MaxAoeRange },      { "buff_max_hp",       MaxMaxHp },
        { "buff_regeneration",  MaxRegeneration },  { "buff_armor",        MaxArmor },
        { "buff_dodge",         MaxDodge },         { "buff_life_steal",   MaxLifeSteal },
        { "buff_move_speed",    MaxMoveSpeed },     { "buff_pickup_range", MaxPickupRange },
        { "buff_xp_gain",       MaxXpGain },        { "buff_currency",     MaxCurrency },
        { "buff_luck",          MaxLuck },          { "buff_curse",        MaxCursedSugar },
    };

    /// <summary>Der "Maximalstufe"-Erfolg zu einer Waffe/einem Buff, oder null.</summary>
    public static AchievementDef MaxLevelFor(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return null;
        return MaxLevelByWeapon.TryGetValue(weaponId, out AchievementDef def) ? def : null;
    }

    /// <summary>Sieg je Charakter - Index wie in <see cref="Characters"/>.</summary>
    private static readonly AchievementDef[] WinBySkin =
    {
        WinCookie, WinJam, WinOnigiri, WinToast, WinOnionKnight, WinMochi, WinInari, WinSlimeKing, WinTofu,
    };

    /// <summary>Evo der Startwaffe je Charakter - passt zu Characters.StartEvoIdByskin.</summary>
    private static readonly AchievementDef[] StartEvoBySkin =
    {
        ChocoChipsEvo, StickyShatterEvo, BladeSwarmEvo, TyphoonFanEvo, SwordSlashEvo,
        MochiMelodyEvo, FoxfireEvo, RoyalSplatEvo, CrumbMayhemEvo,
    };

    /// <summary>Der Sieg-Erfolg eines Charakters, oder null.</summary>
    public static AchievementDef CharacterWin(int skinIndex) =>
        skinIndex >= 0 && skinIndex < WinBySkin.Length ? WinBySkin[skinIndex] : null;

    /// <summary>Der Erfolg fuer die Startwaffen-Evo eines Charakters, oder null.</summary>
    public static AchievementDef StartEvo(int skinIndex) =>
        skinIndex >= 0 && skinIndex < StartEvoBySkin.Length ? StartEvoBySkin[skinIndex] : null;

    /// <summary>Evo-weaponID (Characters.StartEvoId) -> ihr Erfolg, oder null.</summary>
    public static AchievementDef EvoFor(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return null;
        for (int skin = 0; skin < Characters.Count; skin++)
        {
            if (Characters.StartEvoId(skin) == weaponId) return StartEvo(skin);
        }
        return null;
    }

    /// <summary>
    /// Stand fuer <see cref="AllCharacters"/>: Siege und Startwaffen-Evos aller
    /// Charaktere ausser dem Tofu.
    /// </summary>
    public static int AllCharactersDone()
    {
        int done = 0;
        for (int skin = 0; skin < Characters.Count; skin++)
        {
            if (skin == Characters.Tofu) continue;
            if (Achievements.IsUnlocked(CharacterWin(skin))) done++;
            if (Achievements.IsUnlocked(StartEvo(skin))) done++;
        }
        return done;
    }

    /// <summary>EnemyId-Name -> Boss-Erfolg (Endbosse der Welten und der Schleimkoenig).</summary>
    private static readonly Dictionary<string, AchievementDef> ByBoss = new Dictionary<string, AchievementDef>
    {
        { "KeksKoenig",    BossCookieKing },
        { "Glutwurz",      BossEmberroot },
        { "Eiskaiser",     BossIceEmperor },
        { "Squiddy",       BossPhantom },
        { "Schleimkoenig", KillSlimeKing },
    };

    /// <summary>Der Erfolg fuer einen besiegten Boss (Schluessel = EnemyId-Name), oder null.</summary>
    public static AchievementDef BossVictory(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return null;
        return ByBoss.TryGetValue(enemyId, out AchievementDef def) ? def : null;
    }

    private static void EnsureIndex()
    {
        if (ById.Count == Registry.Count) return;
        ById.Clear();
        foreach (AchievementDef d in Registry)
        {
            if (!ById.ContainsKey(d.Id)) ById.Add(d.Id, d);
        }
    }

    private static AchievementDef Def(string id, string nameEn, string descEn,
                                      AchievementCategory category,
                                      float goal = 1f,
                                      string icon = null,
                                      string grantsUnlock = null,
                                      bool hidden = false,
                                      string steamApiName = null)
    {
        AchievementDef def = new AchievementDef(id, nameEn, descEn, icon, goal,
                                               grantsUnlock, category, hidden, steamApiName)
        {
            Order = Registry.Count
        };
        Registry.Add(def);
        return def;
    }
}
