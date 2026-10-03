using System.Collections.Generic;

/// <summary>
/// DIE STEAM-DEMO: alles, was in der Demo gesperrt oder fest vorgegeben ist,
/// steht hier an einer Stelle. Die Systeme fragen nur noch hier nach - wer die
/// Demo-Sperren aufheben will, stellt <see cref="Active"/> auf false, und das
/// Spiel verhaelt sich wieder wie die Vollversion.
///
/// Gesperrt heisst immer: sichtbar, anklickbar, mit Schloss - aber nicht
/// waehlbar. Die Spieler sollen sehen, was es in der Vollversion gibt.
///
/// WAS GESPERRT IST
///   * Charaktere: nur Keks (0), Onigiri (2) und Toast (3) sind waehlbar.
///   * Skilltree: die Kategorien Wissen und Glueck, bei jedem Charakter.
///   * Level: die Kueche (World1) steht auf Platz 2 und ist zu; der Wald
///     (World2, mit dem Demo-Wellenplan - siehe WavePlans.WaldPlan) ist Platz 1.
///   * Endless: in jedem Level.
///   * Hub: Shop, Teleporter zum Shop, Werkbank und Erfolge-Buch.
///   * Erfolge: komplett aus - kein Freischalten, nichts an Steam, keine
///     Anzeige am Laufende, kein Erfolge-Knopf im Hauptmenue.
///
/// WAS FEST VORGEGEBEN IST
///   * Der Verteiler (sonst Werkbank): genau die Waffen und Buffs unten. Die
///     Startwaffen der drei Charaktere gibt es nur beim jeweiligen Charakter.
///   * Slots: 2 Buff-Slots und 1 Evo-Slot, unabhaengig von Shop-Kaeufen.
/// </summary>
public static class Demo
{
    /// <summary>Hauptschalter. False = Vollversion.</summary>
    public static readonly bool Active = true;

    // ==================================================================
    //  Charaktere
    // ==================================================================

    /// <summary>Waehlbare Charaktere (Index wie in <see cref="Characters"/>).</summary>
    private static readonly int[] PlayableCharacters = { 0, 2, 3 };

    public static bool IsCharacterLocked(int skinIndex) =>
        Active && System.Array.IndexOf(PlayableCharacters, skinIndex) < 0;

    // ==================================================================
    //  Skilltree
    // ==================================================================

    public static bool IsSkillCategoryLocked(SkillCategory category) =>
        Active && (category == SkillCategory.Wissen || category == SkillCategory.Glueck);

    // ==================================================================
    //  Level
    // ==================================================================

    /// <summary>Wellenplan des Levels, das in der Demo ganz vorne steht.</summary>
    public const string FirstLevelPlan = "World2";

    private static readonly string[] LockedLevelPlans = { "World1" };

    public static bool IsLevelLocked(string planId)
    {
        if (!Active || string.IsNullOrWhiteSpace(planId)) return false;
        foreach (string id in LockedLevelPlans)
        {
            if (string.Equals(id, planId.Trim(), System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static bool EndlessLocked => Active;

    // ==================================================================
    //  Hub
    // ==================================================================

    public static bool ShopLocked => Active;
    public static bool WorkbenchLocked => Active;

    // ==================================================================
    //  Erfolge
    // ==================================================================

    /// <summary>
    /// Keine Erfolge in der Demo: nichts wird freigeschaltet oder gezaehlt,
    /// nichts geht an Steam, nichts erscheint am Ende eines Laufs. Der
    /// Erfolge-Knopf im Hauptmenue und das Erfolge-Buch im Hub sind weg bzw. zu.
    /// </summary>
    public static bool AchievementsOff => Active;

    // ==================================================================
    //  Verteiler
    // ==================================================================

    private static readonly HashSet<string> Weapons = new HashSet<string>
    {
        "blade_swarm",   // Kunai - Startwaffe Onigiri
        "shurikookie",   // Startwaffe Keks
        "butterblast",
        "void_spike",
        "salad_fan",     // Startwaffe Toast
        "cookie_saw",
        "crumb_trail",   // Cookie Crumble
        "vortex",
        "turret",
        "deathstrike",
    };

    private static readonly HashSet<string> Buffs = new HashSet<string>
    {
        "buff_move_speed",
        "buff_max_hp",
        "buff_armor",
        "buff_regeneration",
        "buff_luck",
    };

    /// <summary>
    /// Darf diese Waffe/dieser Buff beim gewaehlten Charakter im Lauf gezogen
    /// werden? Evos haengen nicht am Verteiler - die entstehen aus ihren Zutaten.
    /// Die Startwaffe eines waehlbaren Charakters gibt es nur bei ihm selbst.
    /// </summary>
    public static bool AllowsInRun(string weaponId, int character)
    {
        if (string.IsNullOrEmpty(weaponId)) return true;

        WeaponDef def = WeaponCatalog.Find(weaponId);
        if (def != null && def.Kind == PoolKind.Evo) return true;

        if (Buffs.Contains(weaponId)) return true;
        if (!Weapons.Contains(weaponId)) return false;

        foreach (int c in PlayableCharacters)
        {
            if (c != character && Characters.StartWeaponId(c) == weaponId) return false;
        }
        return true;
    }

    /// <summary>Liegt die Waffe/der Buff im Demo-Pool (egal bei welchem Charakter)?</summary>
    public static bool IsInPool(string weaponId) =>
        !string.IsNullOrEmpty(weaponId) && (Weapons.Contains(weaponId) || Buffs.Contains(weaponId));

    /// <summary>Kann diese Evo in der Demo entstehen? Beide Zutaten muessen im Pool liegen.</summary>
    public static bool IsEvoPossible(string ingredientA, string ingredientB) =>
        IsInPool(ingredientA) && IsInPool(ingredientB);

    public const int BuffSlots = 2;
    public const int EvoSlots = 1;

    // ==================================================================
    //  Erster Start
    // ==================================================================

    /// <summary>
    /// Merker, dass der Neustart schon gelaufen ist. Liegt in den PlayerPrefs
    /// und nicht im Spielstand - "Spielstand zuruecksetzen" in den Optionen
    /// loest ihn also nicht noch einmal aus. Wer ihn bewusst wiederholen will,
    /// aendert die Nummer am Ende.
    /// </summary>
    private const string FreshStartKey = "demo.fresh_start.v1";

    /// <summary>
    /// Einmalig beim allerersten Start der Demo: Keks-Muenzen auf 0, alle
    /// Charakter-XP (frueher Seelen) auf 0 - damit auch die gelernten Knoten,
    /// sonst staenden die Skillpunkte im Minus - und der Keks ist gewaehlt.
    /// Danach nie wieder.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void FreshStartOnce()
    {
        if (!Active || UnityEngine.PlayerPrefs.GetInt(FreshStartKey, 0) == 1) return;

        Shop.SetCurrency(0);
        Skills.ResetEverything();
        Shop.SkinIndex = 0;

        UnityEngine.PlayerPrefs.SetInt(FreshStartKey, 1);
        UnityEngine.PlayerPrefs.Save();
        UnityEngine.Debug.Log("[Demo] Erster Start: Muenzen und Charakter-XP auf 0, Keks gewaehlt.");
    }

    // ==================================================================
    //  Texte
    // ==================================================================

    /// <summary>Kurzer Hinweis auf Knoepfen und Schildern.</summary>
    public static string LockedLabel => Loc.Get("ui.demo.locked", "DEMO");

    /// <summary>Ein Satz fuer Beschreibungen und den [E]-Hinweis.</summary>
    public static string LockedHint => Loc.Get("ui.demo.lockedhint", "In der Demo gesperrt.");
}
