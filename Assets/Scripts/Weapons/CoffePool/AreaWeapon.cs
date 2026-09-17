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
            Achievements.Unlock(Ach.MaxCoffeePool);
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = CurrentCooldown;
                Instantiate(prefab, transform.position, transform.rotation, transform);
                // transform weglassen um object an stelle leigen zu lassen 
            }
        }
    }
}
