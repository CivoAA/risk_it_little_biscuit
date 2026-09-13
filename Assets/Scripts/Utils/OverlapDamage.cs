using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aktive Trefferabfrage für Projektile, bei denen sich Unitys Trigger-Events
/// nicht verlassen lassen.
///
/// Zwei Fälle:
///
/// 1. Stillstehende Projektile werden als Kind unter den Spieler gespawnt und
///    haben kein eigenes Rigidbody2D – ihr Collider wird dadurch Teil des
///    Spieler-Rigidbody2D. Steht der Spieler (kein WASD), schläft dieser
///    Rigidbody2D ein und Unity meldet für bereits überlappende Gegner kein
///    OnTriggerEnter2D mehr. Dafür ist <see cref="FindEnemies"/> da.
///
/// 2. Schnelle Projektile legen pro Frame mehr Strecke zurück als ihr Collider
///    breit ist und tunneln dadurch durch Gegner hindurch, ohne dass je ein
///    Trigger-Event entsteht. Dafür ist <see cref="SweepEnemies"/> da, das den
///    gesamten zurückgelegten Weg prüft statt nur die Endposition.
/// </summary>
public static class OverlapDamage
{
    private static readonly List<Collider2D> overlapBuffer = new List<Collider2D>();
    private static readonly List<RaycastHit2D> castBuffer = new List<RaycastHit2D>();

    /// <summary>Weltposition der Collider-Mitte (inkl. Offset).</summary>
    public static Vector2 WorldCenter(BoxCollider2D box)
    {
        return box.transform.TransformPoint(box.offset);
    }

    /// <summary>Grösse des Colliders in Weltkoordinaten (inkl. Skalierung).</summary>
    public static Vector2 WorldSize(BoxCollider2D box)
    {
        Vector3 lossy = box.transform.lossyScale;
        return new Vector2(box.size.x * Mathf.Abs(lossy.x), box.size.y * Mathf.Abs(lossy.y));
    }

    /// <summary>
    /// Schreibt alle Gegner, die den Box-Collider aktuell überlappen, in <paramref name="result"/>.
    /// </summary>
    public static void FindEnemies(BoxCollider2D box, List<Enemy> result)
    {
        result.Clear();
        if (box == null || !box.enabled) return;

        overlapBuffer.Clear();
        Physics2D.OverlapBox(WorldCenter(box), WorldSize(box), box.transform.eulerAngles.z,
                             BuildFilter(box.gameObject.layer), overlapBuffer);

        for (int i = 0; i < overlapBuffer.Count; i++)
        {
            AddEnemy(overlapBuffer[i], result);
        }
    }

    /// <summary>
    /// Schreibt alle Gegner, die auf dem Weg von <paramref name="fromCenter"/> zur
    /// aktuellen Position des Colliders getroffen wurden, in <paramref name="result"/>.
    /// Verhindert Tunneling bei schnellen Projektilen.
    /// </summary>
    public static void SweepEnemies(BoxCollider2D box, Vector2 fromCenter, List<Enemy> result)
    {
        result.Clear();
        if (box == null || !box.enabled) return;

        Vector2 center = WorldCenter(box);
        Vector2 delta = center - fromCenter;
        float distance = delta.magnitude;

        // Praktisch stehen geblieben → normale Überlappung reicht
        if (distance < 0.0001f)
        {
            FindEnemies(box, result);
            return;
        }

        castBuffer.Clear();
        Physics2D.BoxCast(fromCenter, WorldSize(box), box.transform.eulerAngles.z,
                          delta / distance, BuildFilter(box.gameObject.layer),
                          castBuffer, distance);

        for (int i = 0; i < castBuffer.Count; i++)
        {
            AddEnemy(castBuffer[i].collider, result);
        }
    }

    private static ContactFilter2D BuildFilter(int layer)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(layer));
        return filter;
    }

    private static void AddEnemy(Collider2D hit, List<Enemy> result)
    {
        if (hit == null || !hit.CompareTag("Enemy")) return;

        Enemy enemy = hit.GetComponent<Enemy>();
        if (enemy != null && !result.Contains(enemy)) result.Add(enemy);
    }
}
