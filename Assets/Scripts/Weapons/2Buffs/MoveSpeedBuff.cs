using UnityEngine;

public class MoveSpeedBuff : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.moveSpeed += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
