using UnityEngine;

public class ExperienceGain : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.experienceMultiplier += stats[weaponLevel].damage;
            applied += stats[weaponLevel].damage;
            UIController.Instance.UpdateHealthSlider();

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null) PlayerController.Instance.experienceMultiplier -= applied;
        applied = 0f;
    }
}
