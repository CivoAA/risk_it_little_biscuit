using UnityEngine;

public class Armor : Weapon
{
    private int lastAppliedLevel = -2;
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            if (weaponLevel == 0)
            {
                PlayerController.Instance.playerArmor = PlayerController.Instance.playerArmor + stats[weaponLevel].damage;
            }
            else
            {
                PlayerController.Instance.playerArmor = PlayerController.Instance.playerArmor - stats[weaponLevel - 1].damage + stats[weaponLevel].damage;
            }
            lastAppliedLevel = weaponLevel;
        }
    }
}
