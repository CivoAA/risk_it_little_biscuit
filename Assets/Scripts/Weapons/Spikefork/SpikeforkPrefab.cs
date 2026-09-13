using UnityEngine;
using System.Collections.Generic;

public class SpikeforkPrefab : MonoBehaviour
{
    public Spikefork weapon;
    private float timer;
    public List<Enemy> enemiesInRange;
    private float counter;

    void Start()
    {
        weapon = WeaponFinder.Find<Spikefork>("Spike Fork");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null) return;
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
