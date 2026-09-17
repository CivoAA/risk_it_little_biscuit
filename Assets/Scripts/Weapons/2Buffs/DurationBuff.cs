using UnityEngine;

/// <summary>
/// Verlaengert die Wirkdauer aller Flaechen- und Orbit-Waffen ueber
/// <see cref="PlayerController.durationMultiplier"/>.
///
/// stats[level].damage = Gesamtbonus auf dieser Stufe (0.25 = +25%), absolut
/// und nicht kumulativ (siehe <see cref="CooldownReduction"/>).
///
/// Wirkt bewusst nicht auf Void Spike und Blade Swarm: dort ist duration der
/// Abstand zwischen zwei Spawns, ein Bonus waere dort ein Malus.
/// </summary>
public class DurationBuff : Weapon
{
    private int lastAppliedLevel = -2;
    private float appliedBonus;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0) return;
        if (PlayerController.Instance == null) return;

        float target = Mathf.Max(0f, stats[weaponLevel].damage);

        PlayerController.Instance.durationMultiplier += target - appliedBonus;
        appliedBonus = target;

        lastAppliedLevel = weaponLevel;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxDuration);
        }
    }
}
