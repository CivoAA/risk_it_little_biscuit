using UnityEngine;
using System.Collections.Generic;

public class KeckssaegeWeaponPrefab : MonoBehaviour
{
    public KeckssaegeWeapon weapon;
    private List<Enemy> enemiesInRange = new List<Enemy>();
    private float counter;

    void Start()
    {
        weapon = GameObject.Find("CookieSaw").GetComponent<KeckssaegeWeapon>();
        counter = weapon.stats[weapon.weaponLevel].AttackSpeed;
        // AudioController.Instance.PalySound(AudioController.Instance.saege);
    }

    void Update()
    {
        // Zählt runter bis zum nächsten Schadenstik
        counter -= Time.deltaTime;
        if (counter <= 0f)
        {
            counter = weapon.stats[weapon.weaponLevel].AttackSpeed;

            // Alle Gegner in Reichweite Schaden zufügen
            for (int i = 0; i < enemiesInRange.Count; i++)
            {
                if (enemiesInRange[i] != null)
                {
                    enemiesInRange[i].TakeDamage(weapon.stats[weapon.weaponLevel].damage);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            if (enemy != null && !enemiesInRange.Contains(enemy))
            {
                enemiesInRange.Add(enemy);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemiesInRange.Remove(enemy);
            }
        }
    }
}
