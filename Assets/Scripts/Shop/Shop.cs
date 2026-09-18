using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DER SHOP-KATALOG und gleichzeitig die Schnittstelle zum Rest des Spiels.
///
/// Einen Eintrag hinzufügen:
///   1. Eine Zeile in den passenden Abschnitt unten schreiben.
///   2. Wenn der Kauf etwas im Spiel bewirken soll, das an der passenden Stelle
///      abfragen - z.B. in PlayerController.StartStats:
///         BuffSlots += Shop.GetInt(Shop.BuffSlot);
///      Schaltet er eine Waffe frei, reicht unlocksWeapon: "meine_waffe" - der
///      Rest passiert von allein.
///   3. Ein PNG namens [IconKey].png nach Assets/Resources/Shop/ legen. Fehlt es,
///      zeigt der Shop den Platzhalter.
///
/// REIHENFOLGE: Die Zeilenreihenfolge in dieser Datei ist die Reihenfolge im
/// Shop. Zwei Einträge tauschen = zwei Zeilen tauschen. Einen Eintrag entfernen
/// = Zeile löschen; bereits gekaufte Stufen bleiben im Spielstand liegen und
/// kommen zurück, falls der Eintrag zurückkommt.
///
/// IDs NIEMALS ÄNDERN - sie sind die Schlüssel im Spielstand.
///
/// Wer die gekaufte Stufe im Spiel braucht, nimmt die Run-Abfragen weiter unten
/// (<see cref="Get"/>, <see cref="GetInt"/>, <see cref="IsBought"/>). Die lesen
/// den Stand, der beim Start des Laufs eingefroren wurde.
/// </summary>
public static class Shop
{
    // Muss textlich vor allen Def(...)-Feldern stehen.
    private static readonly List<ShopItemDef> Registry = new List<ShopItemDef>();

    // ==================================================================
    //  Upgrades
    // ==================================================================

    public static readonly ShopItemDef CurrencyGain = Def(
        "currency_gain", "Currency Gain", "Increase your Currency gain by",
        ShopCategory.Upgrades,
        costs:  new[] { 50, 100, 200, 500 },
        values: new[] { 0f, 0.1f, 0.2f, 0.35f, 0.5f });

    public static readonly ShopItemDef Rerolls = Def(
        "rerolls", "Rerolls", "Increase your Rerolls by",
        ShopCategory.Upgrades,
        costs:  new[] { 10, 50, 100, 250, 500 },
        values: new[] { 0f, 1f, 2f, 3f, 4f, 5f });

    public static readonly ShopItemDef Banish = Def(
        "banish", "Banish", "Increase your Banishs by",
        ShopCategory.Upgrades,
        costs:  new[] { 10, 50, 100, 250, 500 },
        values: new[] { 0f, 1f, 2f, 3f, 4f, 5f });

    public static readonly ShopItemDef StartXp = Def(
        "start_xp", "Start XP", "Increase your Start XP by",
        ShopCategory.Upgrades,
        costs:  new[] { 500, 750, 1000, 1000 },
        values: new[] { 0f, 3f, 13f, 33f, 63f });

    public static readonly ShopItemDef ShrinkSpeed = Def(
        "shrink_speed", "Shrink Speed", "Increase your Shrink Speed by",
        ShopCategory.Upgrades,
        costs:  new[] { 150, 250, 500, 600 },
        values: new[] { 0f, 0.1f, 0.2f, 0.3f, 0.4f });

    // ==================================================================
    //  Slots
    // ==================================================================

    public static readonly ShopItemDef BuffSlot = Def(
        "buff_slot", "Buff Slot", "Increases your Buff Slots by",
        ShopCategory.Slots,
        costs:  new[] { 800, 1000, 2000 },
        values: new[] { 0f, 1f, 2f, 3f },
        requiredUnlock: "unlock_buff_slot");

    public static readonly ShopItemDef WeaponSlot = Def(
        "weapon_slot", "Weapon Slot", "Increases your Weapon Slots by",
        ShopCategory.Slots,
        costs:  new[] { 1000, 1500, 2000 },
        values: new[] { 0f, 1f, 2f, 3f },
        requiredUnlock: "unlock_weapon_slot");

    public static readonly ShopItemDef EvoSlot = Def(
        "evo_slot", "Evo Slot", "Increase your Evo Slots by",
        ShopCategory.Slots,
        costs:  new[] { 100, 1000, 2000, 2000, 5000 },
        values: new[] { 0f, 1f, 2f, 3f, 4f, 5f });

    // ==================================================================
    //  Waffen
    // ==================================================================

    public static readonly ShopItemDef BobaGun = Def(
        "boba_gun", "Boba Gun", "Unlocks Boba Gun", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_boba_gun", unlocksWeapon: "boba_gun");

    public static readonly ShopItemDef Shurikookie = Def(
        "shurikookie", "Shurikookie", "Unlocks Shurikookie", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_shurikookie", unlocksWeapon: "shurikookie");

    public static readonly ShopItemDef Spikefork = Def(
        "spikefork", "Spikefork", "Unlocks Spikefork", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_spike_fork", unlocksWeapon: "spike_fork");

    public static readonly ShopItemDef Deathstrike = Def(
        "deathstrike", "Deathstrike", "Unlocks Deathstrike", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        unlocksWeapon: "deathstrike");

    public static readonly ShopItemDef CelestialStar = Def(
        "celestial_star", "Celestial Star", "Unlocks Celestial Star", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_celestial_star", unlocksWeapon: "celestial_star");

    public static readonly ShopItemDef BladeSwarm = Def(
        "blade_swarm", "Blade Swarm", "Unlocks Blade Swarm", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_blade_swarm", unlocksWeapon: "blade_swarm");

    public static readonly ShopItemDef CandyBomb = Def(
        "candy_bomb", "Candy Bomb", "Unlocks Candy Bomb", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_candy_bomb", unlocksWeapon: "candy_bomb");

    public static readonly ShopItemDef TimeLaser = Def(
        "time_laser", "Time Laser", "Unlocks Time Laser", ShopCategory.Weapons,
        costs: new[] { 1000 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_time_laser", unlocksWeapon: "time_laser");

    // ==================================================================
    //  Buffs
    // ==================================================================

    public static readonly ShopItemDef BuffXpGain = Def(
        "buff_xp_gain", "XP Gain Buff", "Unlocks XP Gain Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_xp_gain");

    public static readonly ShopItemDef BuffCurrency = Def(
        "buff_currency", "Currency Buff", "Unlocks Currency Gain Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f },
        icon: "currency_gain", // teilt sich die Grafik mit dem Waehrungs-Upgrade
        unlocksWeapon: "buff_currency");

    public static readonly ShopItemDef BuffLifeSteal = Def(
        "buff_life_steal", "Life Steal", "Unlocks Life Steal Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_life_steal");

    public static readonly ShopItemDef BuffLuck = Def(
        "buff_luck", "Luck", "Unlocks Luck Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_luck");

    public static readonly ShopItemDef BuffExtraShot = Def(
        "buff_extra_shot", "Extra Shot", "Unlocks Extra Shot Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f },
        requiredUnlock: "unlock_extra_shot", unlocksWeapon: "buff_extra_shot");

    public static readonly ShopItemDef BuffAoeRange = Def(
        "buff_aoe_range", "AOE Range", "Unlocks AOE Range Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_aoe_range");

    public static readonly ShopItemDef BuffDamage = Def(
        "buff_damage", "Damage", "Unlocks Damage Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_damage");

    public static readonly ShopItemDef BuffCritChance = Def(
        "buff_crit_chance", "Crit Chance", "Unlocks Crit Chance Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_crit_chance");

    public static readonly ShopItemDef BuffCritDamage = Def(
        "buff_crit_damage", "Crit Damage", "Unlocks Crit Damage Buff", ShopCategory.Buffs,
        costs: new[] { 800 }, values: new[] { 0f, 1f }, unlocksWeapon: "buff_crit_damage");

    // ==================================================================
    //  Begleiter
    // ==================================================================

    public static readonly ShopItemDef Companion = Def(
        "companion", "Begleiter", "Unlocks Begleiter", ShopCategory.Companion,
        costs:  new[] { 4000, 4000, 4000 },
        values: new[] { 0f, 1f, 2f, 3f });

    // ==================================================================
    //  Ab hier nur noch Mechanik.
    // ==================================================================

    /// <summary>Alle Einträge in Katalogreihenfolge.</summary>
    public static IReadOnlyList<ShopItemDef> All => Registry;

    /// <summary>Feuert nach jedem Kauf, Reset oder Währungswechsel - für UI-Aktualisierung.</summary>
    public static event Action Changed;

    private static readonly Dictionary<string, ShopItemDef> ById = new Dictionary<string, ShopItemDef>();
    private static readonly Dictionary<string, ShopItemDef> ByWeapon = new Dictionary<string, ShopItemDef>();

    private static ShopStore store;
    private static bool initialized;

    private static bool sandbox;

    /// <summary>
    /// Sandbox (Test-Szene): Käufe wirken im Speicher, landen aber nie in save.json.
    /// </summary>
    public static bool SandboxMode
    {
        get => sandbox;
        set
        {
            sandbox = value;
            if (store != null) store.ReadOnly = value;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => Init();

    public static void Init()
    {
        if (initialized) return;
        initialized = true;

        BuildIndex();

        store = new ShopStore { ReadOnly = sandbox };
        store.Load();
    }

    private static ShopStore Store
    {
        get
        {
            if (!initialized) Init();
            return store;
        }
    }

    private static void BuildIndex()
    {
        ById.Clear();
        ByWeapon.Clear();

        foreach (ShopItemDef def in Registry)
        {
            if (!ById.ContainsKey(def.Id)) ById.Add(def.Id, def);

            if (!string.IsNullOrEmpty(def.UnlocksWeapon) && !ByWeapon.ContainsKey(def.UnlocksWeapon))
                ByWeapon.Add(def.UnlocksWeapon, def);
        }
    }

    public static ShopItemDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Init();
        return ById.TryGetValue(id, out ShopItemDef def) ? def : null;
    }

    // ==================================================================
    //  Laden / Kaufen  (aktueller Stand, für die Shop-Oberfläche)
    // ==================================================================

    public static int Currency => Store.Currency;

    public static void AddCurrency(int amount)
    {
        Store.Currency += amount;
        Store.Flush();
        RaiseChanged();
    }

    public static void RemoveCurrency(int amount) => AddCurrency(-amount);

    /// <summary>Gewählter Charakter. Liegt im selben Spielstand wie der Shop.</summary>
    public static int SkinIndex
    {
        get => Store.SkinIndex;
        set
        {
            if (Store.SkinIndex == value) return;
            Store.SkinIndex = value;
            Store.Flush();

            // Jeder Charakter hat seinen eigenen Skilltree - der Wechsel zieht
            // also auch die Boni und die freigeschalteten Schalter mit.
            Skills.SetActiveTreeForCharacter(value);

            RaiseChanged();
        }
    }

    public static int LevelOf(ShopItemDef def) => def == null ? 0 : Store.LevelOf(def.Id);

    public static bool IsMaxed(ShopItemDef def) => def == null || LevelOf(def) >= def.MaxLevel;

    /// <summary>Preis des nächsten Kaufs. 0, wenn schon MAX.</summary>
    public static int NextPrice(ShopItemDef def)
    {
        if (def == null) return 0;
        int level = LevelOf(def);
        return level >= def.Costs.Count ? 0 : def.Costs[level];
    }

    public static float CurrentValue(ShopItemDef def) => def == null ? 0f : def.ValueAt(LevelOf(def));

    public static float NextValue(ShopItemDef def) => def == null ? 0f : def.ValueAt(LevelOf(def) + 1);

    /// <summary>Taucht der Eintrag im Shop auf? Hängt an der Unlock-Bedingung.</summary>
    public static bool IsVisible(ShopItemDef def)
    {
        if (def == null) return false;
        if (string.IsNullOrWhiteSpace(def.RequiredUnlock)) return true;
        return Unlocks.IsUnlocked(def.RequiredUnlock);
    }

    public static bool CanBuy(ShopItemDef def) => def != null && !IsMaxed(def) && NextPrice(def) <= Currency;

    /// <summary>Kauft die nächste Stufe. Gibt false zurück, wenn MAX oder zu teuer.</summary>
    public static bool TryBuy(ShopItemDef def)
    {
        if (!CanBuy(def)) return false;

        int price = NextPrice(def);

        Store.Currency -= price;
        Store.SetLevel(def.Id, LevelOf(def) + 1);
        Store.AddSpent(def.Id, price);
        Store.Flush();

        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Alle Käufe zurücknehmen und das Ausgegebene erstatten. Gibt zurück,
    /// wie viel erstattet wurde.
    /// </summary>
    public static int ResetAllUpgrades()
    {
        int refund = Store.RefundEverything();
        Store.Flush();

        RaiseChanged();
        Debug.Log($"[Shop] Alle Upgrades zurückgesetzt, {refund} erstattet. Neuer Stand: {Currency}");
        return refund;
    }

    public static void SetCurrency(int amount)
    {
        Store.Currency = amount;
        Store.Flush();
        RaiseChanged();
    }

    private static void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Shop] Fehler im Changed-Handler: {e}");
        }
    }

    // ==================================================================
    //  Lauf  (eingefrorener Stand, für alles in der Game-Szene)
    // ==================================================================

    private static Dictionary<string, float> runValues;
    private static int runSkinIndex;
    private static int runStartWeaponOverride = -1;

    /// <summary>
    /// Friert den aktuellen Shop-Stand für einen Lauf ein. Wird beim Betreten
    /// einer Karte aufgerufen. Danach ändert ein Kauf den laufenden Run nicht mehr.
    /// </summary>
    public static void CaptureRun()
    {
        Init();

        runValues = new Dictionary<string, float>(Registry.Count);
        foreach (ShopItemDef def in Registry) runValues[def.Id] = CurrentValue(def);

        runSkinIndex = SkinIndex;
        runStartWeaponOverride = -1;
        Debug.Log($"[Shop] Lauf-Stand eingefroren ({runValues.Count} Einträge, Skin {runSkinIndex}).");
    }

    /// <summary>
    /// Für die Test-Szene: alles, was ein einzelner Freischalt-Kauf ist (Waffen
    /// und Buffs), gilt als gekauft; mehrstufige Upgrades bleiben auf 0, damit
    /// die Werte vergleichbar bleiben. Die Startwaffe lässt sich unabhängig vom
    /// Charakter setzen (-1 = aus dem Charakter ableiten).
    /// </summary>
    public static void CaptureRunUnlockAll(int skinIndex = 0, int startWeaponIndex = -1)
    {
        Init();

        runValues = new Dictionary<string, float>(Registry.Count);
        foreach (ShopItemDef def in Registry)
            runValues[def.Id] = def.IsSingleUnlock ? def.ValueAt(def.MaxLevel) : 0f;

        runSkinIndex = skinIndex;
        runStartWeaponOverride = startWeaponIndex;
    }

    /// <summary>Gewählter Charakter für diesen Lauf.</summary>
    public static int RunSkinIndex => runValues != null ? runSkinIndex : SkinIndex;

    /// <summary>
    /// Startwaffe für diesen Lauf - normalerweise aus dem Charakter abgeleitet,
    /// in der Test-Szene frei setzbar.
    /// </summary>
    public static int RunStartWeapon =>
        runStartWeaponOverride >= 0 ? runStartWeaponOverride : Characters.StartWeaponIndex(RunSkinIndex);

    /// <summary>
    /// Wert eines Eintrags für den laufenden Run. Ohne eingefrorenen Stand (z.B.
    /// beim direkten Szenenstart im Editor) wird der aktuelle Spielstand benutzt,
    /// damit auch das funktioniert.
    /// </summary>
    public static float Get(ShopItemDef def)
    {
        if (def == null) return 0f;
        if (runValues != null && runValues.TryGetValue(def.Id, out float v)) return v;
        return CurrentValue(def);
    }

    public static int GetInt(ShopItemDef def) => Mathf.RoundToInt(Get(def));

    /// <summary>Mindestens einmal gekauft.</summary>
    public static bool IsBought(ShopItemDef def) => Get(def) >= 1f;

    /// <summary>
    /// Ist diese Waffe bzw. dieser Buff über den Shop freigeschaltet? Die
    /// Zuordnung steht am Eintrag selbst (unlocksWeapon), nicht in einer
    /// zweiten Tabelle im PlayerController.
    /// </summary>
    public static bool IsWeaponUnlocked(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return false;
        Init();
        return ByWeapon.TryGetValue(weaponId, out ShopItemDef def) && IsBought(def);
    }

    public static string SavePath => Store.SavePath;

    // ==================================================================

    private static ShopItemDef Def(string id, string nameEn, string descEn, ShopCategory category,
                                   int[] costs, float[] values,
                                   string icon = null,
                                   string requiredUnlock = null,
                                   string unlocksWeapon = null)
    {
        ShopItemDef def = new ShopItemDef(id, nameEn, descEn, category, costs, values,
                                          icon, requiredUnlock, unlocksWeapon)
        {
            Order = Registry.Count
        };
        Registry.Add(def);
        return def;
    }
}
