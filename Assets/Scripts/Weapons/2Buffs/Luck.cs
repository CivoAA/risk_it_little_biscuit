using UnityEngine;

public class Luck : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect
    
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.luck += stats[weaponLevel].damage;
            applied += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null) PlayerController.Instance.luck -= applied;
        applied = 0f;
    }
}
