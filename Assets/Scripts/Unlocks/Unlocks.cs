using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Unlock: eine Sache, die im Spiel erst auftaucht, nachdem man etwas
/// geschafft hat. Unlocks schalten selbst nichts frei - sie sind eine Fahne, auf
/// die sich andere beziehen (heute vor allem Shop-Einträge über
/// <c>requiredUnlock</c>).
/// </summary>
public sealed class UnlockDef
{
    /// <summary>Stabiler Schlüssel im Spielstand. Niemals ändern.</summary>
    public readonly string Id;

    public readonly string NameEn;
    public readonly string DescEn;

    /// <summary>Dateiname in Assets/Resources/Unlocks/. Standard: die Id.</summary>
    public readonly string IconKey;

    /// <summary>Position im Katalog - entspricht der Reihenfolge in Unlocks.cs.</summary>
    public int Order { get; internal set; }

    public UnlockDef(string id, string nameEn, string descEn, string iconKey)
    {
        Id      = id;
        NameEn  = nameEn;
        DescEn  = descEn;
        IconKey = string.IsNullOrEmpty(iconKey) ? id : iconKey;
    }

    public bool IsUnlocked => Unlocks.IsUnlocked(this);

    public string Name => Loc.Get($"unlock.{Id}.name", NameEn);
    public string Description => Loc.Get($"unlock.{Id}.desc", DescEn);
    public Sprite Icon => UnlockIcons.Get(IconKey);

    public override string ToString() => Id;
}

/// <summary>
/// DER UNLOCK-KATALOG und die Schnittstelle dazu.
///
/// Einen Unlock hinzufügen:
///   1. Eine Zeile unten schreiben.
///   2. An der Stelle, die ihn verdient, Unlocks.Grant(Unlocks.MeinDing) aufrufen.
///   3. Wer ihn abfragt (z.B. ein Shop-Eintrag), nimmt die Id als requiredUnlock.
///   4. Ein PNG namens [IconKey].png nach Assets/Resources/Unlocks/ legen.
///
/// Die Reihenfolge der Zeilen ist die Reihenfolge in der Unlock-Anzeige.
/// IDs niemals ändern - sie sind die Schlüssel in unlocks.json.
/// </summary>
public static class Unlocks
{
    // Muss textlich vor allen Def(...)-Feldern stehen.
    private static readonly List<UnlockDef> Registry = new List<UnlockDef>();

    // ==================================================================
    //  Shop-Erweiterungen
    // ==================================================================

    public static readonly UnlockDef WeaponSlot = Def(
        "unlock_weapon_slot",
        "Weapon Slot",
        "You unlocked the Weapon Slot upgrade in the shop!",
        icon: "weapon_slot");

    public static readonly UnlockDef BuffSlot = Def(
        "unlock_buff_slot",
        "Buff Slot",
        "You unlocked the Buff Slot upgrade in the shop!",
        icon: "buff_slot");

    public static readonly UnlockDef ExtraShot = Def(
        "unlock_extra_shot",
        "Extra Shot",
        "The Extra Shot upgrade is now unlocked in the shop!",
        icon: "buff_extra_shot");

    // ==================================================================
    //  Waffen
    // ==================================================================

    public static readonly UnlockDef Shurikookie = Def(
        "unlock_shurikookie",
        "Shurikookie",
        "Shurikookie has been unlocked in the shop!",
        icon: "shurikookie");

    public static readonly UnlockDef BladeSwarm = Def(
        "unlock_blade_swarm",
        "Blade Swarm",
        "Blade Swarm can now be purchased in the shop!",
        icon: "blade_swarm");

    public static readonly UnlockDef SpikeFork = Def(
        "unlock_spike_fork",
        "Spike Fork",
        "Spike Fork is now available to buy!",
        icon: "spikefork");

    public static readonly UnlockDef BobaGun = Def(
        "unlock_boba_gun",
        "Boba Gun",
        "The Boba Gun is now available in the shop!",
        icon: "boba_gun");

    public static readonly UnlockDef CelestialStar = Def(
        "unlock_celestial_star",
        "Celestial Star",
        "You unlocked the Celestial Star in the shop!",
        icon: "celestial_star");

    public static readonly UnlockDef CandyBomb = Def(
        "unlock_candy_bomb",
        "Candy Bomb",
        "Candy Bomb is now unlockable in the shop!",
        icon: "candy_bomb");

    public static readonly UnlockDef TimeLaser = Def(
        "unlock_time_laser",
        "Time Laser",
        "Time Laser is now available in the shop!",
        icon: "time_laser");

    // Hinweis: "unlock_evo_slot" gibt es nicht mehr - der Evo-Slot ist von Anfang
    // an im Shop sichtbar. Ein alter Spielstand, der ihn noch enthält, behält den
    // Eintrag unangetastet; er wird nur nirgends mehr abgefragt.

    // ==================================================================
    //  Ab hier nur noch Mechanik.
    // ==================================================================

    public static IReadOnlyList<UnlockDef> All => Registry;

    /// <summary>Feuert genau einmal je Unlock, wenn er aufgeht.</summary>
    public static event Action<UnlockDef> Granted;

    private static readonly Dictionary<string, UnlockDef> ById = new Dictionary<string, UnlockDef>();

    private static UnlockStore store;
    private static bool initialized;
    private static bool sandbox;

    /// <summary>Sandbox (Test-Szene): schaltet im Speicher frei, schreibt nichts.</summary>
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

        ById.Clear();
        foreach (UnlockDef def in Registry)
        {
            if (!ById.ContainsKey(def.Id)) ById.Add(def.Id, def);
        }

        store = new UnlockStore { ReadOnly = sandbox };
        store.Load();
    }

    private static UnlockStore Store
    {
        get
        {
            if (!initialized) Init();
            return store;
        }
    }

    public static UnlockDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Init();
        return ById.TryGetValue(id, out UnlockDef def) ? def : null;
    }

    // ==================================================================

    public static bool IsUnlocked(UnlockDef def) => def != null && Store.IsUnlocked(def.Id);

    /// <summary>
    /// Abfrage über die Id. Eine unbekannte Id gilt als nicht freigeschaltet -
    /// damit versteckt sich ein Tippfehler nicht hinter "ist halt offen".
    /// </summary>
    public static bool IsUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return Store.IsUnlocked(id);
    }

    /// <summary>Schaltet frei. Ist er schon offen, passiert nichts.</summary>
    public static void Grant(UnlockDef def)
    {
        if (def == null) return;

        UnlockStore s = Store;
        if (s.IsUnlocked(def.Id)) return;

        s.SetUnlocked(def.Id, true);
        s.Save();

        try
        {
            Granted?.Invoke(def);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Unlocks] Fehler im Granted-Handler für '{def.Id}': {e}");
        }

        Debug.Log($"[Unlocks] Freigeschaltet: {def.Id}");
    }

    /// <summary>Freischalten über die Id - für Konsole und Cheats.</summary>
    public static void Grant(string id)
    {
        UnlockDef def = Find(id);

        if (def == null)
        {
            Debug.LogWarning($"[Unlocks] Unbekannte Id '{id}' - steht sie im Katalog (Unlocks.cs)?");
            return;
        }

        Grant(def);
    }

    public static void GrantAll()
    {
        foreach (UnlockDef def in Registry) Grant(def);
    }

    public static void ResetAll()
    {
        Store.ResetAll();
        Debug.Log("[Unlocks] Alle Unlocks zurückgesetzt.");
    }

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (UnlockDef def in Registry)
            {
                if (IsUnlocked(def)) n++;
            }
            return n;
        }
    }

    public static int TotalCount => Registry.Count;

    public static string SavePath => Store.SavePath;

    private static UnlockDef Def(string id, string nameEn, string descEn, string icon = null)
    {
        UnlockDef def = new UnlockDef(id, nameEn, descEn, icon) { Order = Registry.Count };
        Registry.Add(def);
        return def;
    }
}
