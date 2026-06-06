using UnityEngine;

public class ExperienceGain : Weapon
{
    private int lastAppliedLevel = -2;
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.experienceMultiplier += stats[weaponLevel].damage;
            UIController.Instance.UpdateHealthSlider();

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
