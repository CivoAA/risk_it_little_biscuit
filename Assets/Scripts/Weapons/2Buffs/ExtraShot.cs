using UnityEngine;

public class ExtraShot : Weapon
{
    private int lastAppliedLevel = -2;
    
    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_ExtraShot");
        }
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            PlayerController.Instance.playerShots += stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }
    }
}