using UnityEngine;

public class DodgeChance : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.dodgeChance += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}

