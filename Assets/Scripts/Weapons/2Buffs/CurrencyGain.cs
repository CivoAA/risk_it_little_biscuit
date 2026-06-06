using UnityEngine;

public class CurrencyGain : Weapon
{
    private int lastAppliedLevel = -2;
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            GameManager.Instance.currencyGainMultiplire += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
}
