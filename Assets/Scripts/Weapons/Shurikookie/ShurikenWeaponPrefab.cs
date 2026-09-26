using UnityEngine;
using System.Collections.Generic;

public class ShurikenWeaponPrefab : MonoBehaviour
{
    public ShurikenWeapon weapon;
    private float timer;
    public List<Enemy> enemiesInRange;
    private float counter;

    // Destroy wirkt erst am Ende des Frames. Stehen mehrere Gegner
    // uebereinander, kommen ihre Trigger alle im selben Physikschritt an -
    // ohne den Schalter trafe ein Shuriken jeden davon.
    private bool hasHit;

    void Start()
    {
        weapon = WeaponFinder.Find<ShurikenWeapon>("Shurikookie");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (hasHit) return;

        if (collider.CompareTag("Enemy"))
        {
            hasHit = true;
            if (weapon != null && weapon.IsActive)
            {
                collider.GetComponent<Enemy>()?.TakeDamage(weapon.CurrentStats.damage);
            }
            Destroy(gameObject);
        }
    }
    
}
