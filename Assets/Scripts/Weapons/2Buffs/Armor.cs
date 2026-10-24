using UnityEngine;

public class Armor : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Ruestung der aktuellen Stufe - fuer RemoveEffect
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
            applied = stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null) PlayerController.Instance.playerArmor -= applied;
        applied = 0f;
    }
}
