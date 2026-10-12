using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein fliegendes Schokostueck der <see cref="ChocoChips"/>. Fliegt geradeaus,
/// trifft jeden Gegner hoechstens einmal und ist nach <c>pierce</c> Gegnern
/// oder am Ende seiner Flugweite weg. Dreht sich ueber seine Bilder (nicht per
/// Transform - gedrehte Pixelart wird matschig) und ploppt beim Start kurz auf.
///
/// Alle Werte kommen beim Abschuss mit, das Stueck fliegt also auch dann
/// sauber zu Ende, wenn die Waffe inzwischen weg ist. Getroffen wird per
/// CircleCast ueber den ganzen Weg des Frames, damit es nicht durch Gegner tunnelt.
/// </summary>
public class ChocoChipShot : MonoBehaviour
{
    private const float SpinFps = 18f;
    private const float PopTime = 0.07f;

    private Vector2 dir;
    private float speed;
    private float maxDistance;
    private float radius;
    private float damage;
    private int hitsLeft;
    private Sprite[] frames;

    private float travelled;
    private float age;
    private int frameOffset;

    /// <summary>
    /// Meldet die Stelle, an der das Stueck seinen letzten Gegner getroffen hat
    /// (Durchschlag aufgebraucht). Am Ende der Flugweite kommt nichts.
    /// </summary>
    public System.Action<Vector2> Spent;

    /// <summary>
    /// Skilltree "Abpraller": so oft springt das Stueck noch zum naechsten
    /// Gegner, wenn sein Durchschlag verbraucht ist. Jeder Sprung trifft einen
    /// weiteren Gegner; <see cref="Spent"/> kommt erst nach dem letzten.
    /// </summary>
    public int Bounces;

    /// <summary>Skilltree "Erster Bissen": jeder Treffer dieses Stuecks ist ein Crit.</summary>
    public bool AlwaysCrit;

    private SpriteRenderer sr;
    private readonly List<Enemy> hitBuffer = new List<Enemy>();
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    public void Launch(Vector2 direction, float speed, float maxDistance, float radius,
                       float damage, int pierce, Sprite[] frames)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.radius = radius;
        this.damage = damage;
        this.frames = frames;
        hitsLeft = Mathf.Max(1, pierce);

        sr = GetComponent<SpriteRenderer>();
        frameOffset = Random.Range(0, frames.Length);
        sr.sprite = frames[frameOffset];
        transform.localScale = Vector3.one * 0.5f;
    }

    void Update()
    {
        if (sr == null) return;

        age += Time.deltaTime;

        float step = Mathf.Min(speed * Time.deltaTime, maxDistance - travelled);
        Vector2 from = transform.position;
        Vector2 to = from + dir * step;

        OverlapDamage.SweepEnemies(from, to, radius, hitBuffer);
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null || !alreadyHit.Add(enemy)) continue;

            Enemy.ForceCrit = AlwaysCrit;
            enemy.TakeDamage(damage);
            Enemy.ForceCrit = false;
            hitsLeft--;

            if (hitsLeft <= 0 && Bounces > 0 && BounceFrom(from))
            {
                UpdateLook();
                return;
            }

            if (hitsLeft <= 0)
            {
                if (Spent != null) Spent(enemy != null ? (Vector2)enemy.transform.position : from);
                Destroy(gameObject);
                return;
            }
        }

        transform.position = to;
        travelled += step;

        UpdateLook();

        if (travelled >= maxDistance - 0.0001f) Destroy(gameObject);
    }

    /// <summary>
    /// Dreht das Stueck auf den naechsten noch nicht getroffenen Gegner und gibt
    /// ihm Flugweite fuer einen Treffer. False, wenn keiner in Reichweite ist.
    /// </summary>
    private bool BounceFrom(Vector2 at)
    {
        Enemy next = Ricochet.Nearest(at, alreadyHit);
        if (next == null) return false;

        Bounces--;
        hitsLeft = 1;
        Vector2 aim = Aim.PredictDirection(at, next, speed);
        if (aim.sqrMagnitude > 0.0001f) dir = aim.normalized;
        transform.position = at;
        travelled = 0f;
        maxDistance = Ricochet.Range;
        return true;
    }

    private void UpdateLook()
    {
        int frame = (frameOffset + (int)(age * SpinFps)) % frames.Length;
        sr.sprite = frames[frame];

        // Ploppt aus dem Keks: von halber auf volle Groesse.
        float pop = Mathf.Clamp01(age / PopTime);
        transform.localScale = Vector3.one * (pop < 1f ? Mathf.Lerp(0.5f, 1f, pop) : 1f);

        // Letztes Fuenftel der Flugweite ausblenden.
        float fade = Mathf.Clamp01((maxDistance - travelled) / (maxDistance * 0.2f));
        Color c = sr.color;
        c.a = fade;
        sr.color = c;
    }
}
