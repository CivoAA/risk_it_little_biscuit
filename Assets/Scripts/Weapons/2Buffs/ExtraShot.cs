using UnityEngine;

public class ExtraShot : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxExtraShot);
        }
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            PlayerController.Instance.playerShots += stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }
    }
}