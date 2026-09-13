using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zielhilfe fuer alle Schuetzen, die ein Projektil losschicken statt es am
/// Spieler kleben zu lassen (Begleiter und Turret).
///
/// Ohne Vorhalt zielt man dorthin, wo der Gegner beim Abdruecken STAND. Da
/// jeder Gegner permanent auf den Spieler zulaeuft, ist er nach der Flugzeit
/// laengst woanders - das war der Grund fuer die vielen Fehlschuesse.
/// </summary>
public static class Aim
{
    private static readonly List<Collider2D> overlapBuffer = new List<Collider2D>();
    private static ContactFilter2D filter;
    private static bool filterReady;

    /// <summary>Nimmt alles mit, auch Trigger - gefiltert wird ueber das Tag.</summary>
    private static ContactFilter2D Filter
    {
        get
        {
            if (!filterReady)
            {
                filter = ContactFilter2D.noFilter;
                filter.useTriggers = true;
                filterReady = true;
            }

            return filter;
        }
    }

    /// <summary>Naechster Gegner im Radius, oder null.</summary>
    public static Enemy FindClosestEnemy(Vector2 origin, float radius)
    {
        overlapBuffer.Clear();
        Physics2D.OverlapCircle(origin, radius, Filter, overlapBuffer);

        Enemy closest = null;
        float closestSqr = Mathf.Infinity;

        foreach (Collider2D hit in overlapBuffer)
        {
            if (hit == null || !hit.CompareTag("Enemy")) continue;

            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = enemy;
            }
        }

        return closest;
    }

    /// <summary>
    /// Richtung, in die geschossen werden muss, damit ein Projektil mit
    /// <paramref name="projectileSpeed"/> den weiterlaufenden Gegner trifft.
    ///
    /// Gesucht ist die Flugzeit t, nach der Projektil und Gegner am selben
    /// Punkt sind: |toTarget + v*t| = speed * t. Quadriert ergibt das
    /// (v² - speed²)·t² + 2·(toTarget·v)·t + toTarget² = 0.
    ///
    /// Gibt es keine Loesung (Gegner schneller als das Projektil und auf dem
    /// Weg weg), wird direkt auf den Gegner gezielt - die Nachfuehrung im
    /// Projektil holt den Rest.
    /// </summary>
    public static Vector2 PredictDirection(Vector2 origin, Enemy target, float projectileSpeed)
    {
        if (target == null) return Vector2.right;

        Vector2 toTarget = (Vector2)target.transform.position - origin;
        Vector2 direct = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;

        if (projectileSpeed <= 0.01f) return direct;

        Vector2 velocity = target.Velocity;

        float a = velocity.sqrMagnitude - projectileSpeed * projectileSpeed;
        float b = 2f * Vector2.Dot(toTarget, velocity);
        float c = toTarget.sqrMagnitude;

        float t;

        if (Mathf.Abs(a) < 0.0001f)
        {
            // Gegner exakt so schnell wie das Projektil: der quadratische Term
            // faellt weg, uebrig bleibt eine lineare Gleichung.
            if (Mathf.Abs(b) < 0.0001f) return direct;
            t = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return direct;

            float root = Mathf.Sqrt(discriminant);
            float t1 = (-b + root) / (2f * a);
            float t2 = (-b - root) / (2f * a);

            // Die kleinste positive Flugzeit ist der frueheste Treffer.
            t = Mathf.Min(t1, t2);
            if (t < 0f) t = Mathf.Max(t1, t2);
        }

        if (t <= 0f) return direct;

        Vector2 aimPoint = (Vector2)target.transform.position + velocity * t;
        Vector2 lead = aimPoint - origin;

        return lead.sqrMagnitude > 0.0001f ? lead.normalized : direct;
    }
}
