using UnityEngine;

public class CurrencyGain : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect
    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            GameManager.Instance.currencyGainMultiplire += stats[weaponLevel].damage;
            applied += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (GameManager.Instance != null) GameManager.Instance.currencyGainMultiplire -= applied;
        applied = 0f;
    }
}
