using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Marmeladenlache der <see cref="StickyShatterEvo"/>. Tickt Schaden und
/// uebergibt dabei den Slow-Multiplikator an <see cref="Enemy.TakeDamage"/> -
/// derselbe Weg, den auch der Time Laser nutzt.
/// </summary>
public class StickyShatterEvoPrefab : MonoBehaviour
{
    public StickyShatterEvo weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private float lifeTimer;
    private float tickCounter;

    void Start()
    {
        if (weapon == null)
        {
            weapon = WeaponFinder.Find<StickyShatterEvo>("Sticky Shatter Evo");
        }

        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        lifeTimer = weapon.CurrentDuration;
        transform.localScale = Vector3.one
            * weapon.CurrentStats.range
            * PlayerController.Instance.AOERange;

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.areaWeaponSpawn, 0.5f);
        }
    }

    void Update()
    {
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        tickCounter -= Time.deltaTime;
        if (tickCounter > 0f) return;

        tickCounter = Mathf.Max(0.05f, weapon.CurrentStats.AttackSpeed);

        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (enemiesInRange[i] == null)
            {
                enemiesInRange.RemoveAt(i);
                continue;
            }

            enemiesInRange[i].TakeDamage(weapon.CurrentStats.damage, weapon.SlowMultiplier);
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null && !enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null) enemiesInRange.Remove(enemy);
    }
}
