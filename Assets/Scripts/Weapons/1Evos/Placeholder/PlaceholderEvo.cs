using System.Collections.Generic;

/// <summary>
/// Platzhalter-Evos fuer die Startwaffen, die noch keine eigene Evo haben
/// (Keks, Inari, Schleimkoenig). Angelegt am 12.10.2026,
/// damit die Evo des Tofus (<see cref="CrumbMayhemEvo"/>) etwas zu kopieren hat.
///
/// Jede ist schlicht die Startwaffe eine Stufe ueber ihrer Maximalstufe: eine
/// einzige Stufe aus den Werten der hoechsten, mit mehr Schaden, etwas
/// kuerzerem Cooldown und einem Schuss mehr. Die Tabellen neben den Werten
/// (Durchschlag, Spruenge ...) stehen ueber <see cref="Weapon.TableLevel"/> auf
/// der Hoechststufe. Ein Rezept hat noch keine - bis die richtige Evo kommt,
/// bekommt man sie nur ueber den Tofu.
/// </summary>
public static class PlaceholderEvo
{
    public const float DamageFactor = 1.5f;
    public const float CooldownFactor = 0.85f;
    public const int ExtraShots = 1;

    /// <summary>Eine Stufe aus der hoechsten Stufe von <paramref name="baseStats"/>.</summary>
    public static List<WeaponStats> From(List<WeaponStats> baseStats, string description)
    {
        WeaponStats max = baseStats != null && baseStats.Count > 0
            ? baseStats[baseStats.Count - 1].Clone()
            : new WeaponStats();

        max.damage *= DamageFactor;
        max.cooldown *= CooldownFactor;
        if (max.shots > 0f) max.shots += ExtraShots;
        max.description = description;

        return new List<WeaponStats> { max };
    }
}
