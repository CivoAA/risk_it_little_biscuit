using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gruppe eines Shop-Eintrags. Die Liste im Hub-Shop bleibt flach (Katalog-
/// reihenfolge), die Kategorie ist für Sortierung und spätere Überschriften da.
/// </summary>
public enum ShopCategory
{
    Upgrades,
    Slots,
    Weapons,
    Buffs,
    Companion,
}

/// <summary>
/// Die unveränderliche Beschreibung eines Shop-Eintrags: Name, Text, Preise,
/// Werte je Stufe, Symbol, Freischalt-Bedingung. Alles, was zum Spiel gehört -
/// im Gegensatz zur gekauften Stufe, die im Spielstand liegt.
///
/// Angelegt wird so ein Def ausschliesslich im Katalog <see cref="Shop"/>.
///
/// PREISE UND WERTE: <see cref="Costs"/> hat einen Eintrag pro Kauf,
/// <see cref="Values"/> einen pro Stufe - also immer genau einen mehr, weil
/// Stufe 0 (nichts gekauft) auch einen Wert hat. Beispiel:
///
///   costs:  [50, 100, 200]        -> drei Käufe möglich
///   values: [0, 0.1, 0.2, 0.35]   -> Stufe 0,1,2,3
/// </summary>
public sealed class ShopItemDef
{
    /// <summary>Stabiler Schlüssel im Spielstand. Niemals ändern.</summary>
    public readonly string Id;

    /// <summary>Englischer Originaltext, Rückfallebene für die Übersetzung.</summary>
    public readonly string NameEn;
    public readonly string DescEn;

    /// <summary>Dateiname in Assets/Resources/Shop/. Standard: die Id.</summary>
    public readonly string IconKey;

    /// <summary>Preis je Kauf. Länge = Anzahl möglicher Stufen.</summary>
    public readonly IReadOnlyList<int> Costs;

    /// <summary>Wert je Stufe, beginnend bei Stufe 0. Länge = Costs.Count + 1.</summary>
    public readonly IReadOnlyList<float> Values;

    public readonly ShopCategory Category;

    /// <summary>Unlock-ID, ohne die der Eintrag im Shop nicht auftaucht. Leer = immer sichtbar.</summary>
    public readonly string RequiredUnlock;

    /// <summary>
    /// Waffen- oder Buff-ID (<c>Weapon.weaponID</c>), die dieser Kauf im Spiel
    /// freischaltet. Leer bei reinen Zahlen-Upgrades.
    /// </summary>
    public readonly string UnlocksWeapon;

    /// <summary>Position im Katalog - entspricht der Reihenfolge in Shop.cs.</summary>
    public int Order { get; internal set; }

    public ShopItemDef(string id, string nameEn, string descEn, ShopCategory category,
                       int[] costs, float[] values, string iconKey,
                       string requiredUnlock, string unlocksWeapon)
    {
        Id             = id;
        NameEn         = nameEn;
        DescEn         = descEn;
        IconKey        = string.IsNullOrEmpty(iconKey) ? id : iconKey;
        Costs          = costs ?? new int[0];
        Values         = values ?? new float[] { 0f };
        Category       = category;
        RequiredUnlock = requiredUnlock;
        UnlocksWeapon  = unlocksWeapon;
    }

    /// <summary>Anzahl möglicher Käufe.</summary>
    public int MaxLevel => Costs.Count;

    /// <summary>Ein einzelner Kauf, der etwas freischaltet - kein mehrstufiges Upgrade.</summary>
    public bool IsSingleUnlock => Costs.Count == 1;

    public string Name => Loc.Get($"shop.{Id}.name", NameEn);
    public string Description => Loc.Get($"shop.{Id}.desc", DescEn);
    public Sprite Icon => ShopIcons.Get(IconKey);

    /// <summary>Wert auf einer bestimmten Stufe, sicher geklemmt.</summary>
    public float ValueAt(int level)
    {
        if (Values.Count == 0) return 0f;
        return Values[Mathf.Clamp(level, 0, Values.Count - 1)];
    }

    /// <summary>Gesamtpreis der ersten <paramref name="level"/> Käufe.</summary>
    public int TotalCostUpTo(int level)
    {
        int sum = 0;
        int n = Mathf.Clamp(level, 0, Costs.Count);
        for (int i = 0; i < n; i++) sum += Costs[i];
        return sum;
    }

    public override string ToString() => Id;
}
