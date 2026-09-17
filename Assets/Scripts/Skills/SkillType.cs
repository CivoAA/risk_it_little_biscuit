/// <summary>
/// Was ein Skill-Knoten bewirkt. Wer einen neuen Effekt braucht, hängt hier
/// einen Wert an und trägt ihn in <see cref="SkillBonuses"/> ein - an beiden
/// Stellen meckert der Compiler, wenn man die zweite vergisst.
///
/// Die Namen sind gleichzeitig die Dateinamen der Symbole:
/// Assets/Resources/Skills/[SkillType].png. Fehlt eine Datei, greift der
/// allgemeine Freigeschaltet-/Gesperrt-Look.
///
/// ANHÄNGEN, NICHT UMSORTIEREN: Die Reihenfolge ist zwar nicht mehr im
/// Spielstand (dort stehen Text-IDs), aber Umsortieren macht jeden Verweis
/// in alten Notizen und Screenshots wertlos.
/// </summary>
public enum SkillType
{
    None,
    IncreaseMaxHealth,
    IncreaseSpeed,
    xpMultiplier,
    IncreaseHealthReg,
    IncreaseExtraShot,
    IncreaseArmor,
    IncreaseAOERange,
    IncreaseLifeSteal,
    IncreaseDamage,
    IncreaseCritDamage,
    IncreaseCritChance,
    IncreaseDodgeChance,
    IncreasePickupRange,
    IncreaseLuck,
    BanishAmount,
    RerollAmount,
    StartXPAmount,
    IncreaseShrinkSpeed,
    IncreaseWeaponSlots,
    IncreaseBuffSlots,
    IncreaseEvoSlots,
    UnlockEvoOrWeapon,
}
