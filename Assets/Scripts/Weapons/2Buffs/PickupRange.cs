using UnityEngine;

public class PickupRange : Weapon
{
    private int lastAppliedLevel = -2;
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.pickupRange += stats[weaponLevel].damage;

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
