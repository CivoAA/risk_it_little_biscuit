/// <summary>
/// Was ein Skill-Knoten bewirkt. Wer einen neuen Effekt braucht, hängt hier
/// einen Wert an und trägt ihn in <see cref="SkillBonuses"/> ein - an beiden
/// Stellen meckert der Compiler, wenn man die zweite vergisst.
///
/// Die Namen sind gleichzeitig die Dateinamen der Symbole:
/// Assets/Resources/Skills/[SkillType].png. Fehlt eine Datei, greift der
/// allgemeine Freigeschaltet-/Gesperrt-Look.
///
/// FESTE NUMMERN: Die Baum-Assets speichern den Effekt als Zahl ("stat: 14").
/// Darum steht jede Nummer ausdrücklich dran. Einen Effekt entfernen heißt:
/// Zeile löschen, Nummer NIE neu vergeben. Neue Effekte bekommen die nächste
/// freie Zahl am Ende.
///
/// Entfernt (Nummern gesperrt): 19 IncreaseWeaponSlots, 20 IncreaseBuffSlots,
/// 21 IncreaseEvoSlots, 22 UnlockEvoOrWeapon.
/// </summary>
public enum SkillType
{
    None                = 0,
    IncreaseMaxHealth   = 1,
    IncreaseSpeed       = 2,
    xpMultiplier        = 3,
    IncreaseHealthReg   = 4,
    IncreaseExtraShot   = 5,
    IncreaseArmor       = 6,
    IncreaseAOERange    = 7,
    IncreaseLifeSteal   = 8,
    IncreaseDamage      = 9,
    IncreaseCritDamage  = 10,
    IncreaseCritChance  = 11,
    IncreaseDodgeChance = 12,
    IncreasePickupRange = 13,
    IncreaseLuck        = 14,
    BanishAmount        = 15,
    RerollAmount        = 16,
    StartXPAmount       = 17,
    IncreaseShrinkSpeed = 18,

    // Glücks-Ast. Bewusst getrennt von Luck: Luck wirkt nur auf den Mixer,
    // diese Werte werden im Spiel nirgends als Stat angezeigt.
    IncreaseDropChance  = 23,   // +x% auf Magnet- und Herz-Chance
    LuckyXpChance       = 24,   // Chance, dass ein Gegner doppelte XP fallen lässt
    GoldenHeartChance   = 25,   // Anteil der Herzen, die golden sind (+Max-HP)
    LuckyReviveChance   = 26,   // einmal pro Lauf: Chance auf Wiederbelebung

    // Mixer. Werte in Prozent eingeben: 1 = 1 %.
    MoreMixers              = 27,   // +x % Mixer auf der Map
    MixerAllThreeChance     = 28,   // Chance, dass ein Mixer alle drei Stats gibt
    MixerAllLegendaryChance = 29,   // Chance, dass alle drei Stats eines Mixers legendär sind

    // Wissens-Ast. Werte in Prozent eingeben: 1 = 1 %.
    FreeRerollChance    = 30,   // Chance, dass ein Reroll nichts kostet
    LevelUpHealPercent  = 31,   // heilt x % der Max-HP bei jedem Level-Up
    CharXpBonusPercent  = 32,   // +x % Charakter-XP (ausserhalb des Laufs)
    BossHealthReduction = 33,   // Bosse und Minibosse spawnen mit x % weniger Leben
    StartWeaponLevel    = 34,   // Startwaffe beginnt x Stufen höher (ganze Zahl)

    // Geist-Ast. Werte in Prozent eingeben: 1 = 1 %.
    OverhealShield      = 35,   // Überheilung wird Schild, bis x % der Max-HP
    LastBreath          = 36,   // unter 30 % Leben: +x % Tempo, doppelte Regeneration
}
