using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein einzelner Kruemel der <see cref="CrumbTrail"/>. Bleibt liegen, tickt auf
/// alle Gegner in seinem Trigger und raeumt sich nach Ablauf der Wirkdauer
/// selbst ab.
/// </summary>
public class CrumbTrailPrefab : MonoBehaviour
{
    public CrumbTrail weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private float lifeTimer;
    private float tickCounter;

    void Start()
    {
        if (weapon == null)
        {
            weapon = WeaponFinder.Find<CrumbTrail>("Crumb Trail");
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
    }

    void Update()
    {
        // Die Waffe kann waehrend der Lebenszeit durch eine Evo ersetzt werden.
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

            enemiesInRange[i].TakeDamage(weapon.CurrentStats.damage);
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
