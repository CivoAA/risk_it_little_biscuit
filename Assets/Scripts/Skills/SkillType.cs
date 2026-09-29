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

    // Glücks-Ast. Bewusst getrennt von Luck: Luck wirkt nur auf den Mixer,
    // diese Werte werden im Spiel nirgends als Stat angezeigt.
    IncreaseDropChance,     // +x% auf Magnet- und Herz-Chance
    LuckyXpChance,          // Chance, dass ein Gegner doppelte XP fallen lässt
    GoldenHeartChance,      // Anteil der Herzen, die golden sind (+Max-HP)
    LuckyReviveChance,      // einmal pro Lauf: Chance auf Wiederbelebung

    // Mixer. Werte in Prozent eingeben: 1 = 1 %.
    MoreMixers,             // +x % Mixer auf der Map
    MixerAllThreeChance,    // Chance, dass ein Mixer alle drei Stats gibt
    MixerAllLegendaryChance,// Chance, dass alle drei Stats eines Mixers legendär sind

    // Wissens-Ast. Werte in Prozent eingeben: 1 = 1 %.
    FreeRerollChance,       // Chance, dass ein Reroll nichts kostet
    LevelUpHealPercent,     // heilt x % der Max-HP bei jedem Level-Up
    SoulBonusPercent,       // +x % Seelen von Minibossen und Bossen
    BossHealthReduction,    // Bosse und Minibosse spawnen mit x % weniger Leben
    StartWeaponLevel,       // Startwaffe beginnt x Stufen höher (ganze Zahl)
}
