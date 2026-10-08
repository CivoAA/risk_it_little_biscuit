using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Milchring des <see cref="MilkDunk"/>. Bleibt um den Spieler zentriert
/// (er laeuft mit), waechst ueber <see cref="MilkDunk.WaveTime"/> nach
/// <see cref="MilkDunk.RingCurve"/> auf den vollen Radius und trifft jeden
/// Gegner einmal, sobald die Ringkante ihn erreicht.
///
/// Das Bild ist ein Streifen mit fester Groesse; skaliert wird nur so weit,
/// dass sein letzter Ring auf dem Radius der Stufe liegt.
/// </summary>
public class MilkWave : MonoBehaviour
{
    /// <summary>Gegner werden schon getroffen, wenn ihr Mittelpunkt so nah an der Kante ist.</summary>
    private const float EdgeSlack = 0.3f;

    private Transform follow;
    private Vector2 offset;
    private float radius;
    private float damage;
    private Sprite[] frames;

    private float age;
    private SpriteRenderer sr;
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    public void Launch(Transform follow, Vector2 offset, float radius, float damage, Sprite[] frames)
    {
        this.follow = follow;
        this.offset = offset;
        this.radius = radius;
        this.damage = damage;
        this.frames = frames;

        sr = GetComponent<SpriteRenderer>();
        sr.sprite = frames[0];

        float scale = radius / MilkDunk.SpriteRadius;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    void Update()
    {
        if (sr == null) return;

        age += Time.deltaTime;
        float t = age / MilkDunk.WaveTime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (follow != null) transform.position = (Vector2)follow.position + offset;

        float curve = Sample(t);
        HitUpTo(curve * radius + EdgeSlack);

        if (frames.Length > 1)
        {
            int frame = Mathf.Min(frames.Length - 1, (int)(t * frames.Length));
            sr.sprite = frames[frame];
        }
        else
        {
            // Rueckfall ohne Streifen: Klecks waechst mit.
            float s = curve * radius / MilkDunk.SpriteRadius;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    private void HitUpTo(float reach)
    {
        Vector2 center = transform.position;
        float reachSqr = reach * reach;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        // Rueckwaerts: TakeDamage kann einen Gegner toeten und aus der Liste nehmen.
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (i >= alive.Count) continue;
            Enemy enemy = alive[i];
            if (enemy == null || alreadyHit.Contains(enemy)) continue;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude > reachSqr) continue;

            alreadyHit.Add(enemy);
            enemy.TakeDamage(damage, MilkDunk.Slow, MilkDunk.Knockback);
        }
    }

    /// <summary>Ringkurve an der Stelle t (0..1), zwischen den Bildern weich verbunden.</summary>
    private static float Sample(float t)
    {
        float[] c = MilkDunk.RingCurve;
        float f = Mathf.Clamp01(t) * (c.Length - 1);
        int i = Mathf.Min(c.Length - 2, (int)f);
        return Mathf.Lerp(c[i], c[i + 1], f - i);
    }
}
