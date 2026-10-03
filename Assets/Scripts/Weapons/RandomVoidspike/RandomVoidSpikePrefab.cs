using UnityEngine;
using System.Collections.Generic;

public class RandomVoidSpikePrefab : MonoBehaviour
{
    public RandomVoidSpike weapon;

    // Speichert pro Gegner den Zeitpunkt des letzten Treffers
    private Dictionary<Enemy, float> lastHitTimes = new Dictionary<Enemy, float>();
    private float hitCooldown = 1f; // 1 Sekunde Abklingzeit

    // Trefferfenster passend zu Voidspike.anim: erst oeffnet sich das Portal
    // (Vorwarnung), ab 0.13 s steht der Stachel, ab ~0.6 s ist er wieder weg.
    // Beim Einschalten meldet Unity Gegner, die schon drinstehen, per OnTriggerEnter2D.
    private const float HitFrom = 0.13f;
    private const float HitUntil = 0.58f;
    private Collider2D hitbox;
    private float spawnTime;

    void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.enabled = false;
        spawnTime = Time.time;
    }

    void Start()
    {
        weapon = WeaponFinder.Find<RandomVoidSpike>("Void Spike");
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        bool on = age >= HitFrom && age < HitUntil;
        if (hitbox.enabled != on) hitbox.enabled = on;
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
