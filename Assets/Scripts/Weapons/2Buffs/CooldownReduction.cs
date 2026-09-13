using UnityEngine;

/// <summary>
/// Senkt den Cooldown aller Waffen und Evos ueber
/// <see cref="PlayerController.cooldownMultiplier"/>.
///
/// stats[level].damage = Gesamtreduktion auf dieser Stufe (0.15 = -15%).
/// Anders als die aelteren Buffs sind die Werte hier also ABSOLUT und nicht
/// kumulativ: Stufe 0 = 0.08, Stufe 1 = 0.15, Stufe 2 = 0.21 usw. Bei einem
/// Multiplikator ist das die einzige Schreibweise, bei der man im Inspector
/// direkt sieht, wo man landet.
/// </summary>
public class CooldownReduction : Weapon
{
    private int lastAppliedLevel = -2;
    private float appliedReduction;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0) return;
        if (PlayerController.Instance == null) return;

        float target = Mathf.Clamp01(stats[weaponLevel].damage);

        // Differenz zur bereits angewandten Reduktion buchen, damit ein
        // Level-Up nicht die Stufen aufaddiert.
        PlayerController.Instance.cooldownMultiplier += appliedReduction - target;
        appliedReduction = target;

        lastAppliedLevel = weaponLevel;

        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance?.UnlockAchievement("Max_Level_Cooldown");
        }
    }
}
