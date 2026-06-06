using UnityEngine;

public class CritDamage : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.critDamage += stats[weaponLevel].damage;

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
