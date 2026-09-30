using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine einzelne Luftdruckwelle des <see cref="SaladFan"/>. Fliegt geradeaus,
/// trifft jeden Gegner hoechstens einmal und ist nach <c>maxHits</c> Gegnern
/// oder am Ende ihrer Flugweite weg.
///
/// Nach jedem Treffer behaelt die Welle nur noch <c>keep</c> ihrer Staerke -
/// der naechste Gegner bekommt entsprechend weniger Schaden (nie unter
/// <c>minDamage</c>), Rueckstoss und Slow. Die Welle wird dabei sichtbar blasser.
///
/// Alle Werte werden beim Abschuss mitgegeben, die Welle fliegt also auch
/// dann sauber zu Ende, wenn der Faecher in der Zwischenzeit durch eine Evo
/// ersetzt wird. Getroffen wird per CircleCast ueber den ganzen Weg des
/// Frames, damit sie bei Tempo nicht durch Gegner tunnelt.
/// </summary>
public class SaladFanWave : MonoBehaviour
{
    private Vector2 dir;
    private float speed;
    private float maxDistance;
    private float radius;
    private float damage;
    private float minDamage;
    private float keep;
    private int hitsLeft;
    private float baseSlow;
    private float baseKnockback;

    private float travelled;
    private float strength = 1f;

    private SpriteRenderer sr;
    private readonly List<Enemy> hitBuffer = new List<Enemy>();
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    public void Launch(Vector2 direction, float speed, float maxDistance, float radius,
                       float damage, float minDamage, float keep, int maxHits,
                       float baseSlow, float baseKnockback)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.radius = radius;
        this.damage = damage;
        this.minDamage = minDamage;
        this.keep = keep;
        this.baseSlow = baseSlow;
        this.baseKnockback = baseKnockback;
        hitsLeft = maxHits;

        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float step = Mathf.Min(speed * Time.deltaTime, maxDistance - travelled);
        Vector2 from = transform.position;
        Vector2 to = from + dir * step;

        OverlapDamage.SweepEnemies(from, to, radius, hitBuffer);
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null || !alreadyHit.Add(enemy)) continue;

            Hit(enemy);

            if (hitsLeft <= 0)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.position = to;
        travelled += step;

        UpdateLook();

        if (travelled >= maxDistance - 0.0001f) Destroy(gameObject);
    }

    private void Hit(Enemy enemy)
    {
        // Slow schwaecht sich mit ab: bei voller Staerke baseSlow, bei 0 gar keiner.
        float slow = 1f - (1f - baseSlow) * strength;
        enemy.TakeDamage(Mathf.Max(minDamage, damage * strength), slow, baseKnockback * strength);

        strength *= keep;
        hitsLeft--;
    }

    /// <summary>Blasser mit jedem Treffer, auf dem letzten Viertel ausblenden.</summary>
    private void UpdateLook()
    {
        if (sr == null) return;

        float fade = Mathf.Clamp01((maxDistance - travelled) / (maxDistance * 0.25f));
        Color c = sr.color;
        c.a = (0.35f + 0.65f * strength) * fade;
        sr.color = c;
    }
}
