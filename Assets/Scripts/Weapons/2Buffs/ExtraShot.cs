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
            // eigener Topf: zaehlt bei jeder Waffe 1:1 (siehe Weapon.ExtraShotValue)
            PlayerController.Instance.buffShots += stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }
    }
}