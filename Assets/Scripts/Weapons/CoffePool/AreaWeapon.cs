using UnityEngine;

public class AreaWeapon : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;

    // Update is called once per frame
    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_CoffePool");
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = stats[weaponLevel].cooldown;
                Instantiate(prefab, transform.position, transform.rotation, transform);
                // transform weglassen um object an stelle leigen zu lassen 
            }
        }
    }
}
