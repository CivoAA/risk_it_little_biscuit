using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeathstrikePrefab : MonoBehaviour
{
    public Deathstrike weapon;

    private Dictionary<Enemy, float> lastHitTimes = new Dictionary<Enemy, float>();
    private float hitCooldown = 1f;
    private float lifeTime = 0.75f;

    void Start()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Enemy")) return;
        if (weapon == null || !weapon.IsActive) return;

        Enemy enemy = col.GetComponent<Enemy>();
        if (enemy == null) return;

        if (lastHitTimes.TryGetValue(enemy, out float lastHit))
        {
            if (Time.time - lastHit < hitCooldown)
                return;
        }

        enemy.TakeDamage(weapon.CurrentStats.damage);
        lastHitTimes[enemy] = Time.time;
    }
}
