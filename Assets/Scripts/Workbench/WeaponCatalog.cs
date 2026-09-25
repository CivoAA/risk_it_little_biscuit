using System.Collections.Generic;
using UnityEngine;

/// <summary>Was ein Eintrag im Verteiler ist.</summary>
public enum PoolKind
{
    Weapon = 0,
    Buff = 1,
    Evo = 2,
}

/// <summary>
/// Ein Eintrag des Werkbank-Katalogs: die unveraenderliche Beschreibung einer
/// Waffe, eines Buffs oder einer Evo.
///
/// Die Spielwerte stehen nicht hier, sondern wie immer am Weapon-Bauteil im
/// Player-Prefab. Dieser Katalog traegt nur, was die Werkbank braucht, um
/// ausserhalb der Spielszene etwas anzuzeigen: Id, Name, Art, Symbol.
/// </summary>
public sealed class WeaponDef
{
    /// <summary><c>Weapon.weaponID</c> - der Schluessel im Spielstand. Niemals aendern.</summary>
    public readonly string Id;

    /// <summary>Name des GameObjects am Player-Prefab. Rueckfallebene fuer die Uebersetzung.</summary>
    public readonly string NameEn;

    public readonly PoolKind Kind;

    /// <summary>Position im Katalog - entspricht der Reihenfolge in dieser Datei.</summary>
    public int Order { get; internal set; }

    public WeaponDef(string id, string nameEn, PoolKind kind)
    {
        Id = id;
        NameEn = nameEn;
        Kind = kind;
    }

    public string Name => Loc.Get($"weapon.{Id}.name", NameEn);
    public string Description => Loc.Get($"weapon.{Id}.desc", "");

    /// <summary>Symbol fuer eine 14x14-Kachel.</summary>
    public Sprite Icon => WorkbenchIcons.Get(Id, 14);

    /// <summary>Symbol fuer einen 10x10-Slot (Evo-Chip, Evo-Zeile).</summary>
    public Sprite IconSmall => WorkbenchIcons.Get(Id, 10);

    public override string ToString() => Id;
}

/// <summary>
/// Ein Evo-Rezept: zwei Zutaten ergeben eine dritte Sache. Welche zwei das
/// sind, steht im Player-Prefab unter <c>EvoCombinations</c> - hier stehen
/// dieselben Paare als Ids, damit die Werkbank sie ohne Spielszene kennt.
///
/// Zutaten koennen Waffen ODER Buffs sein: <c>evo_boomerang</c> braucht
/// Boomerang plus Extra Shot. Die Anzeige unterscheidet deshalb nicht
/// zwischen "Waffe" und "Buff", sondern nur zwischen erster und zweiter Zutat.
/// </summary>
public sealed class EvoDef
{
    public readonly string Id;
    public readonly string IngredientA;
    public readonly string IngredientB;

    public int Order { get; internal set; }

    public EvoDef(string id, string a, string b)
    {
        Id = id;
        IngredientA = a;
        IngredientB = b;
    }

    public WeaponDef Result => WeaponCatalog.Find(Id);
    public WeaponDef A => WeaponCatalog.Find(IngredientA);
    public WeaponDef B => WeaponCatalog.Find(IngredientB);

    public string Name => Result != null ? Result.Name : Id;

    public override string ToString() => Id;
}

/// <summary>
/// DER WERKBANK-KATALOG: alle Waffen, Buffs und Evos als Ids, plus die
/// Evo-Rezepte.
///
/// WOHER DIE ZEILEN KOMMEN
/// Sie sind eine Abschrift der Weapon-Bauteile am Player-Prefab
/// (Assets/Prefabs/Player.prefab) - dort liegen die Spielwerte, und dort
/// bleiben sie auch. Dieser Katalog existiert nur, weil der Hub das Prefab
/// nicht laedt: im Hub gibt es keinen PlayerController, also auch keine
/// activeWeapon-Liste, aus der sich die Werkbank bedienen koennte.
///
/// Damit die Abschrift nicht auseinanderlaeuft, gibt es
/// <c>Tools > Werkbank > Katalog gegen Player-Prefab prüfen</c>. Das Menue
/// liest das Prefab und meldet jede Abweichung. Nach jeder neuen Waffe: einmal
/// laufen lassen und die gemeldete Zeile hier eintragen.
///
/// Eine Waffe hinzufuegen:
///   1. Unten eine Zeile schreiben (Id exakt wie weaponID im Prefab).
///   2. Icons erzeugen lassen: Tools > Werkbank > Icons prüfen zeigt, welche
///      <id>_14.png / <id>_10.png in Assets/Resources/Workbench/ fehlen.
///   3. Gehoert sie zu einer Evo, das Rezept unten ergaenzen.
/// </summary>
public static class WeaponCatalog
{
    // Muss textlich vor allen Def(...)-Feldern stehen.
    private static readonly List<WeaponDef> Registry = new List<WeaponDef>();
    private static readonly List<EvoDef> EvoRegistry = new List<EvoDef>();
    private static readonly Dictionary<string, WeaponDef> ById = new Dictionary<string, WeaponDef>();

    private static WeaponDef Def(string id, string nameEn, PoolKind kind)
    {
        WeaponDef def = new WeaponDef(id, nameEn, kind) { Order = Registry.Count };
        Registry.Add(def);
        ById[id] = def;
        return def;
    }

    private static EvoDef Evo(string id, string a, string b)
    {
        EvoDef def = new EvoDef(id, a, b) { Order = EvoRegistry.Count };
        EvoRegistry.Add(def);
        return def;
    }

    // ==================================================================
    //  Waffen
    // ==================================================================

    public static readonly WeaponDef CookieSaw     = Def("cookie_saw", "CookieSaw", PoolKind.Weapon);
    public static readonly WeaponDef Shurikookie   = Def("shurikookie", "Shurikookie", PoolKind.Weapon);
    public static readonly WeaponDef Butterblast   = Def("butterblast", "Butterblast", PoolKind.Weapon);
    public static readonly WeaponDef JamJar        = Def("jam_jar", "Throwing Jam Jar", PoolKind.Weapon);
    public static readonly WeaponDef CoffeePool    = Def("coffee_pool", "Coffe Pool", PoolKind.Weapon);
    public static readonly WeaponDef BobaGun       = Def("boba_gun", "Boba Gun", PoolKind.Weapon);
    public static readonly WeaponDef SpikeFork     = Def("spike_fork", "Spike Fork", PoolKind.Weapon);
    public static readonly WeaponDef Deathstrike   = Def("deathstrike", "Deathstrike", PoolKind.Weapon);
    public static readonly WeaponDef VoidSpike     = Def("void_spike", "Void Spike", PoolKind.Weapon);
    public static readonly WeaponDef CelestialStar = Def("celestial_star", "Celestial Star", PoolKind.Weapon);
    public static readonly WeaponDef FireBall      = Def("fire_ball", "Fire Ball", PoolKind.Weapon);
    public static readonly WeaponDef BladeSwarm    = Def("blade_swarm", "Blade Swarm", PoolKind.Weapon);
    public static readonly WeaponDef CandyBomb     = Def("candy_bomb", "Candy Bomb", PoolKind.Weapon);
    public static readonly WeaponDef Boomerang     = Def("boomerang", "Boomerang", PoolKind.Weapon);
    public static readonly WeaponDef TimeLaser     = Def("time_laser", "Time Laser", PoolKind.Weapon);
    public static readonly WeaponDef CrumbTrail    = Def("crumb_trail", "Crumb Trail", PoolKind.Weapon);
    public static readonly WeaponDef Vortex        = Def("vortex", "Vortex", PoolKind.Weapon);
    public static readonly WeaponDef Turret        = Def("turret", "Turret", PoolKind.Weapon);

    // ==================================================================
    //  Buffs
    // ==================================================================

    public static readonly WeaponDef BuffDamage       = Def("buff_damage", "Damage", PoolKind.Buff);
    public static readonly WeaponDef BuffCritChance   = Def("buff_crit_chance", "Crit Chance", PoolKind.Buff);
    public static readonly WeaponDef BuffCritDamage   = Def("buff_crit_damage", "Crit Damage", PoolKind.Buff);
    public static readonly WeaponDef BuffCooldown     = Def("buff_cooldown", "Cooldown", PoolKind.Buff);
    public static readonly WeaponDef BuffDuration     = Def("buff_duration", "Duration", PoolKind.Buff);
    public static readonly WeaponDef BuffAoeRange     = Def("buff_aoe_range", "AOE Range", PoolKind.Buff);
    public static readonly WeaponDef BuffExtraShot    = Def("buff_extra_shot", "Extra Shot", PoolKind.Buff);
    public static readonly WeaponDef BuffMaxHp        = Def("buff_max_hp", "Max HP", PoolKind.Buff);
    public static readonly WeaponDef BuffRegeneration = Def("buff_regeneration", "HP Regeneration", PoolKind.Buff);
    public static readonly WeaponDef BuffArmor        = Def("buff_armor", "Armor", PoolKind.Buff);
    public static readonly WeaponDef BuffDodge        = Def("buff_dodge", "Dodge Chance", PoolKind.Buff);
    public static readonly WeaponDef BuffSecondChance = Def("buff_second_chance", "Second Chance", PoolKind.Buff);
    public static readonly WeaponDef BuffLifeSteal    = Def("buff_life_steal", "Life Steal", PoolKind.Buff);
    public static readonly WeaponDef BuffGlassCannon  = Def("buff_glass_cannon", "Glass Cannon", PoolKind.Buff);
    public static readonly WeaponDef BuffMoveSpeed    = Def("buff_move_speed", "Move Speed", PoolKind.Buff);
    public static readonly WeaponDef BuffPickupRange  = Def("buff_pickup_range", "Pickup Range", PoolKind.Buff);
    public static readonly WeaponDef BuffXpGain       = Def("buff_xp_gain", "Experience Gain", PoolKind.Buff);
    public static readonly WeaponDef BuffCurrency     = Def("buff_currency", "Currency Gain", PoolKind.Buff);
    public static readonly WeaponDef BuffLuck         = Def("buff_luck", "Luck", PoolKind.Buff);

    // ==================================================================
    //  Evos
    // ==================================================================

    public static readonly WeaponDef EvoBobaSaw        = Def("evo_boba_saw", "Boba Saw Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoShurilBlast    = Def("evo_shuril_blast", "Shuri Blast Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoExplosiveStar  = Def("evo_explosive_star", "Explosive Star Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoBloodyFork     = Def("evo_bloody_fork", "Bloody Fork Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoBladeStorm     = Def("evo_blade_storm", "Blade Storm Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoBoomerang      = Def("evo_boomerang", "Boomerang Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoBombSwarm      = Def("evo_bomb_swarm", "Bomb Saw Evo", PoolKind.Evo);
    public static readonly WeaponDef EvoStickyShatter  = Def("evo_sticky_shatter", "Sticky Shatter Evo", PoolKind.Evo);

    // ==================================================================
    //  Rezepte - Reihenfolge wie im Player-Prefab
    // ==================================================================

    public static readonly EvoDef RecipeBobaSaw       = Evo("evo_boba_saw", "cookie_saw", "boba_gun");
    public static readonly EvoDef RecipeShurilBlast   = Evo("evo_shuril_blast", "shurikookie", "butterblast");
    public static readonly EvoDef RecipeExplosiveStar = Evo("evo_explosive_star", "celestial_star", "fire_ball");
    public static readonly EvoDef RecipeBloodyFork    = Evo("evo_bloody_fork", "spike_fork", "coffee_pool");
    public static readonly EvoDef RecipeBladeStorm    = Evo("evo_blade_storm", "blade_swarm", "void_spike");
    public static readonly EvoDef RecipeBoomerang     = Evo("evo_boomerang", "boomerang", "buff_extra_shot");
    public static readonly EvoDef RecipeBombSwarm     = Evo("evo_bomb_swarm", "candy_bomb", "cookie_saw");
    public static readonly EvoDef RecipeStickyShatter = Evo("evo_sticky_shatter", "jam_jar", "buff_aoe_range");

    // ==================================================================
    //  Zugriff
    // ==================================================================

    public static IReadOnlyList<WeaponDef> All => Registry;
    public static IReadOnlyList<EvoDef> Evos => EvoRegistry;

    public static WeaponDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return ById.TryGetValue(id, out WeaponDef def) ? def : null;
    }

    /// <summary>Alle Eintraege einer Art, in Katalogreihenfolge.</summary>
    public static List<WeaponDef> OfKind(PoolKind kind)
    {
        List<WeaponDef> list = new List<WeaponDef>();
        foreach (WeaponDef def in Registry)
        {
            if (def.Kind == kind) list.Add(def);
        }
        return list;
    }

    public static int CountOfKind(PoolKind kind)
    {
        int n = 0;
        foreach (WeaponDef def in Registry)
        {
            if (def.Kind == kind) n++;
        }
        return n;
    }

    /// <summary>Alle Rezepte, in denen diese Id als Zutat vorkommt.</summary>
    public static List<EvoDef> RecipesUsing(string id)
    {
        List<EvoDef> list = new List<EvoDef>();
        if (string.IsNullOrEmpty(id)) return list;

        foreach (EvoDef evo in EvoRegistry)
        {
            if (evo.IngredientA == id || evo.IngredientB == id) list.Add(evo);
        }
        return list;
    }
}
