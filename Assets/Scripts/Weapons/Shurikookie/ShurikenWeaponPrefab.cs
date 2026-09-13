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
        weapon = WeaponFinder.Find<ShurikenWeapon>("Shurikookie");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            if (weapon != null && weapon.IsActive)
            {
                collider.GetComponent<Enemy>()?.TakeDamage(weapon.CurrentStats.damage);
            }
            Destroy(gameObject);
        }
    }
    
}
