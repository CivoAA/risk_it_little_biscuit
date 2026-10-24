using UnityEngine;

public class ExtraShot : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect
    
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
            applied += stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null) PlayerController.Instance.buffShots -= applied;
        applied = 0f;
    }
}
