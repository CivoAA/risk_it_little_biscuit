using UnityEngine;
using System.Collections.Generic;

public class RandomVoidSpikePrefab : MonoBehaviour
{
    public RandomVoidSpike weapon;

    // Speichert pro Gegner den Zeitpunkt des letzten Treffers
    private Dictionary<Enemy, float> lastHitTimes = new Dictionary<Enemy, float>();
    private float hitCooldown = 1f; // 1 Sekunde Abklingzeit

    void Start()
    {
        weapon = WeaponFinder.Find<RandomVoidSpike>("Void Spike");
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null || !weapon.IsActive) return;
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy == null) return;

        float lastHitTime;
        if (lastHitTimes.TryGetValue(enemy, out lastHitTime))
        {
            // Prüfen, ob genug Zeit seit letztem Hit vergangen ist
            if (Time.time - lastHitTime < hitCooldown)
                return; // Noch im Cooldown → kein Schaden
        }

        // Schaden zufügen
        enemy.TakeDamage(weapon.CurrentStats.damage);

        // Zeitpunkt merken
        lastHitTimes[enemy] = Time.time;
    }
}
