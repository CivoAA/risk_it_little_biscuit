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
                return (value * 100f).ToString("0.#");

            case SkillType.IncreaseSpeed:
            case SkillType.IncreaseLifeSteal:
                return value.ToString("0.##");

            case SkillType.IncreaseLuck:
            case SkillType.IncreaseExtraShot:
            case SkillType.BanishAmount:
            case SkillType.RerollAmount:
            case SkillType.IncreaseWeaponSlots:
            case SkillType.IncreaseBuffSlots:
            case SkillType.IncreaseEvoSlots:
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
            case SkillType.IncreaseWeaponSlots: return "+{0} Weapon Slot";
            case SkillType.IncreaseBuffSlots:   return "+{0} Buff Slot";
            case SkillType.IncreaseEvoSlots:    return "+{0} Evo Slot";
            case SkillType.UnlockEvoOrWeapon:   return "Unlocks an Evo or Weapon";
            default:                            return "{0}";
        }
    }
}
