using UnityEngine;

public class CritChance : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.critChance += stats[weaponLevel].damage;

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
