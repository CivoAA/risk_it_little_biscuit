using UnityEngine;

public class MoveSpeedBuff : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.moveSpeed += stats[weaponLevel].damage;
            applied += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null) PlayerController.Instance.moveSpeed -= applied;
        applied = 0f;
    }
}
