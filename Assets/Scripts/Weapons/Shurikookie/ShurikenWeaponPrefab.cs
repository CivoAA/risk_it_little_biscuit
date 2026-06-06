using UnityEngine;
using System.Collections.Generic;

public class ShurikenWeaponPrefab : MonoBehaviour
{
    public ShurikenWeapon weapon;
    private float timer;
    public List<Enemy> enemiesInRange;
    private float counter;

    void Start()
    {
        weapon = GameObject.Find("Shurikookie").GetComponent<ShurikenWeapon>();
    }

    void Update()
    {


    }
    
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
            Destroy(gameObject);
        }
    }
    
}
