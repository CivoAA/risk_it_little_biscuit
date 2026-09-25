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

    /// <summary>Standard-Belohnung in Skill-Währung, wenn nichts anderes angegeben ist.</summary>
    private const int DefaultSouls = 5;

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
        "Defeat 10 <color=#FF4040>Mini Bosses</color>. Each <color=#FF4040>Mini Boss</color> " +
        "(marked with a crown) rewards <color=#00FAFF>+1 Skill Point!</color>",
        AchievementCategory.Combat, goal: 10f);

    public static readonly AchievementDef Kill100Miniboss = Def(
        "Kill_100_Miniboss", "Conqueror", "Defeat 100 <color=#FF4040>Mini Bosses</color>.",
        AchievementCategory.Combat, goal: 100f);

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
                                      int souls = DefaultSouls,
                                      string icon = null,
                                      string grantsUnlock = null,
                                      bool hidden = false,
                                      string steamApiName = null)
    {
        AchievementDef def = new AchievementDef(id, nameEn, descEn, icon, goal, souls,
                                               grantsUnlock, category, hidden, steamApiName)
        {
            Order = Registry.Count
        };
        Registry.Add(def);
        return def;
    }
}
