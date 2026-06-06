using UnityEngine;

public class Luck : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.luck += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}