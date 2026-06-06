using UnityEngine;

public class CurrencyReward : Weapon
{
    private int lastLevel = -1;

    private void Awake()
    {
        if (stats == null)
        {
            stats = new System.Collections.Generic.List<WeaponStats>();
        }
        stats.Clear();
        for (int level = 0; level <= maxweaponLevel; level++)
        {
            stats.Add(new WeaponStats());
        }
    }
    private void Update()
    {
        if (weaponLevel != lastLevel)
        {
            if (weaponLevel > lastLevel)
            {
                SaveGame.Instance.AddCurrency(25);
            }
            else if (weaponLevel < lastLevel)
            {
                lastLevel = weaponLevel;
            }
            lastLevel++;
        }
    }
}
