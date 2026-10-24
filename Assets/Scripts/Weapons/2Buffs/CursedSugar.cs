using UnityEngine;

/// <summary>
/// Fluch-Buff: dreht den Druck hoch. Mehr Gegner, mehr Leben, mehr Schaden -
/// und ueber den Chaos-Regler auch mehr Erfahrung und Muenzen.
///
/// stats[level].damage = Fluch auf dieser Stufe, kommt auf das Chaos drauf
///                       (0.15 = Chaos +0.15, siehe <see cref="RunDifficulty"/>)
///
/// Absolut pro Stufe, nicht kumulativ. Gilt fuer alles, was ab dann spawnt;
/// Gegner, die schon stehen, behalten ihre Werte.
/// </summary>
public class CursedSugar : Weapon
{
    private int lastAppliedLevel = -2;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0 || weaponLevel >= stats.Count) return;

        RunDifficulty.Curse = stats[weaponLevel].damage;
        lastAppliedLevel = weaponLevel;
    }

    public override void RemoveEffect()
    {
        RunDifficulty.Curse = 0f;
    }
}
