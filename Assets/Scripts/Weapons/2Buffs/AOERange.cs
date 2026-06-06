using UnityEngine;

public class AOERange : Weapon
{
    private int lastAppliedLevel = -2;
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.AOERange += stats[weaponLevel].damage;

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}