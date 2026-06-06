using UnityEngine;

public class Buffs : Weapon
{
    private int lastAppliedLevel = -2;

    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.playerMaxHealth += stats[weaponLevel].damage;
            PlayerController.Instance.playerHealth += PlayerController.Instance.playerMaxHealth / 3;
            UIController.Instance.UpdateHealthSlider();

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
