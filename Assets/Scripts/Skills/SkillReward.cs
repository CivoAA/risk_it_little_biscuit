using UnityEngine;

/// <summary>Welche Sorte Belohnung an einem Knoten haengt.</summary>
public enum SkillRewardKind
{
    /// <summary>Ein Wert aus <see cref="SkillType"/>, z.B. "+10 Leben".</summary>
    Wert,

    /// <summary>Eine Freischaltung aus <see cref="SkillGrants"/>, z.B. ein Waffen-Upgrade.</summary>
    Freischalten,
}

/// <summary>
/// Eine einzelne Belohnung eines Knotens. Ein Knoten darf mehrere haben - so
/// kann ein Endknoten gleichzeitig "+20 Leben" geben UND ein Waffen-Upgrade
/// freischalten.
///
/// Unveraenderlich; gebaut wird sie aus den Asset-Daten
/// (<see cref="SkillRewardData"/>) beim Laden des Baums.
/// </summary>
public sealed class SkillReward
{
    public readonly SkillRewardKind Kind;

    /// <summary>Nur bei <see cref="SkillRewardKind.Wert"/> gefuellt.</summary>
    public readonly SkillType Stat;

    /// <summary>Nur bei <see cref="SkillRewardKind.Wert"/> gefuellt.</summary>
    public readonly float Value;

    /// <summary>Nur bei <see cref="SkillRewardKind.Freischalten"/> gefuellt.</summary>
    public readonly string GrantId;

    private readonly string descriptionOverride;

    public SkillReward(SkillType stat, float value, string description = null)
    {
        Kind                = SkillRewardKind.Wert;
        Stat                = stat;
        Value               = value;
        GrantId             = null;
        descriptionOverride = description;
    }

    public SkillReward(string grantId, string description = null)
    {
        Kind                = SkillRewardKind.Freischalten;
        Stat                = SkillType.None;
        Value               = 0f;
        GrantId             = grantId;
        descriptionOverride = description;
    }

    public bool IsStat    => Kind == SkillRewardKind.Wert;
    public bool IsGrant   => Kind == SkillRewardKind.Freischalten;

    /// <summary>Eine Zeile fuer die Beschreibungskarte.</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(descriptionOverride)) return descriptionOverride;

        return IsStat
            ? SkillText.Describe(Stat, Value)
            : SkillGrants.NameOf(GrantId);
    }

    public override string ToString() =>
        IsStat ? $"{Stat} {Value}" : $"grant:{GrantId}";
}

/// <summary>
/// Sinnvolle Startwerte je Effekt. Der Skilltree-Editor traegt sie ein, sobald
/// man einen Effekt im Aufklappmenue auswaehlt - ueberschreiben darf man sie
/// danach jederzeit im Feld daneben.
///
/// Wer einen neuen <see cref="SkillType"/> anlegt, ergaenzt hier eine Zeile.
/// Fehlt sie, startet der Wert bei 1.
/// </summary>
public static class SkillDefaults
{
    public static float ValueFor(SkillType effect)
    {
        switch (effect)
        {
            case SkillType.IncreaseMaxHealth:   return 10f;
            case SkillType.IncreaseSpeed:       return 0.1f;
            case SkillType.xpMultiplier:        return 0.1f;
            case SkillType.IncreaseHealthReg:   return 1f;
            case SkillType.IncreaseExtraShot:   return 1f;
            case SkillType.IncreaseArmor:       return 0.25f;
            case SkillType.IncreaseAOERange:    return 0.1f;
            case SkillType.IncreaseLifeSteal:   return 0.2f;
            case SkillType.IncreaseDamage:      return 0.1f;
            case SkillType.IncreaseCritDamage:  return 0.25f;
            case SkillType.IncreaseCritChance:  return 0.05f;
            case SkillType.IncreaseDodgeChance: return 0.03f;
            case SkillType.IncreasePickupRange: return 0.2f;
            case SkillType.IncreaseLuck:        return 5f;
            case SkillType.BanishAmount:        return 1f;
            case SkillType.RerollAmount:        return 1f;
            case SkillType.StartXPAmount:       return 10f;
            case SkillType.IncreaseShrinkSpeed: return 0.1f;
            case SkillType.IncreaseDropChance:  return 0.25f;
            case SkillType.LuckyXpChance:       return 0.05f;
            case SkillType.GoldenHeartChance:   return 0.1f;
            case SkillType.LuckyReviveChance:   return 0.5f;
            // Die Mixer-Werte stehen direkt in Prozent (1 = 1 %).
            case SkillType.MoreMixers:              return 25f;
            case SkillType.MixerAllThreeChance:     return 2.5f;
            case SkillType.MixerAllLegendaryChance: return 1f;
            // Wissen, ebenfalls in Prozent.
            case SkillType.FreeRerollChance:        return 20f;
            case SkillType.LevelUpHealPercent:      return 5f;
            case SkillType.CharXpBonusPercent:      return 10f;
            case SkillType.BossHealthReduction:     return 5f;
            case SkillType.StartWeaponLevel:        return 1f;
            // Geist, in Prozent.
            case SkillType.OverhealShield:          return 20f;
            case SkillType.LastBreath:              return 20f;
            case SkillType.None:               return 0f;
            default:                            return 1f;
        }
    }

    /// <summary>Preisvorschlag fuer einen neuen Knoten: 1 Skillpunkt (= 1 Charakter-Level).</summary>
    public static int PriceForStep(int step) => 1;
}
