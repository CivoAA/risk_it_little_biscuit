using UnityEngine;

/// <summary>
/// Baut den Beschreibungstext eines Skills aus Effekt und Wert. Früher lag das
/// als Textvorlage mit einem "X" darin im Inspector des SkillManagers und die
/// Formatierung in einem switch daneben.
///
/// Übersetzbar: jede Zeile hängt an einem Schlüssel skill.effect.[Effekt], in den
/// der formatierte Wert als {0} eingesetzt wird. Fehlt die Übersetzung, greift
/// der englische Text hier.
/// </summary>
public static class SkillText
{
    public static string Describe(SkillType effect, float value)
    {
        string number = Format(effect, value);
        return string.Format(Loc.Get($"skill.effect.{effect}", Fallback(effect)), number);
    }

    /// <summary>Prozent, ganze Zahl oder Kommazahl - je nachdem, was der Effekt ist.</summary>
    public static string Format(SkillType effect, float value)
    {
        switch (effect)
        {
            // 0.03 wird zu "3"
            case SkillType.IncreaseCritChance:
            case SkillType.IncreaseCritDamage:
            case SkillType.IncreaseDamage:
            case SkillType.IncreaseDodgeChance:
            case SkillType.xpMultiplier:
            case SkillType.IncreasePickupRange:
            case SkillType.IncreaseAOERange:
            case SkillType.IncreaseDropChance:
            case SkillType.LuckyXpChance:
            case SkillType.GoldenHeartChance:
            case SkillType.LuckyReviveChance:
                return (value * 100f).ToString("0.#");

            // Schon in Prozent eingetragen (1 = 1 %).
            case SkillType.MoreMixers:
            case SkillType.MixerAllThreeChance:
            case SkillType.MixerAllLegendaryChance:
            case SkillType.FreeRerollChance:
            case SkillType.LevelUpHealPercent:
            case SkillType.SoulBonusPercent:
            case SkillType.BossHealthReduction:
            case SkillType.OverhealShield:
            case SkillType.LastBreath:
                return value.ToString("0.#");

            case SkillType.IncreaseSpeed:
            case SkillType.IncreaseLifeSteal:
                return value.ToString("0.##");

            case SkillType.IncreaseLuck:
            case SkillType.IncreaseExtraShot:
            case SkillType.BanishAmount:
            case SkillType.RerollAmount:
            case SkillType.StartWeaponLevel:
                return Mathf.RoundToInt(value).ToString();

            default:
                return value.ToString("0.##");
        }
    }

    private static string Fallback(SkillType effect)
    {
        switch (effect)
        {
            case SkillType.IncreaseMaxHealth:   return "+{0} Max Health";
            case SkillType.IncreaseSpeed:       return "+{0} Move Speed";
            case SkillType.xpMultiplier:        return "+{0}% XP";
            case SkillType.IncreaseHealthReg:   return "+{0} Health Regeneration";
            case SkillType.IncreaseExtraShot:   return "+{0} Extra Shot";
            case SkillType.IncreaseArmor:       return "+{0} Armor";
            case SkillType.IncreaseAOERange:    return "+{0}% AOE Range";
            case SkillType.IncreaseLifeSteal:   return "+{0} Life Steal";
            case SkillType.IncreaseDamage:      return "+{0}% Damage";
            case SkillType.IncreaseCritDamage:  return "+{0}% Crit Damage";
            case SkillType.IncreaseCritChance:  return "+{0}% Crit Chance";
            case SkillType.IncreaseDodgeChance: return "+{0}% Dodge Chance";
            case SkillType.IncreasePickupRange: return "+{0}% Pickup Range";
            case SkillType.IncreaseLuck:        return "+{0} Luck";
            case SkillType.BanishAmount:        return "+{0} Banish";
            case SkillType.RerollAmount:        return "+{0} Reroll";
            case SkillType.StartXPAmount:       return "+{0} Start XP";
            case SkillType.IncreaseShrinkSpeed: return "+{0} Shrink Speed";
            case SkillType.IncreaseDropChance:  return "+{0}% Drop Chance";
            case SkillType.LuckyXpChance:       return "{0}% Chance for double XP";
            case SkillType.GoldenHeartChance:   return "{0}% of hearts are golden (+Max Health)";
            case SkillType.LuckyReviveChance:   return "{0}% Chance to revive once";
            case SkillType.MoreMixers:              return "+{0}% Mixers on the map";
            case SkillType.MixerAllThreeChance:     return "{0}% Chance a mixer gives all 3 stats";
            case SkillType.MixerAllLegendaryChance: return "{0}% Chance a mixer rolls all 3 legendary";
            case SkillType.FreeRerollChance:        return "{0}% Chance for a free reroll";
            case SkillType.LevelUpHealPercent:      return "Heal {0}% health on level up";
            case SkillType.SoulBonusPercent:        return "+{0}% Souls from bosses";
            case SkillType.BossHealthReduction:     return "Bosses spawn with {0}% less health";
            case SkillType.StartWeaponLevel:        return "+{0} Start weapon level";
            case SkillType.OverhealShield:          return "Overheal becomes a shield (max {0}% health)";
            case SkillType.LastBreath:              return "Below 30% health: +{0}% speed, double regen";
            default:                            return "{0}";
        }
    }
}
