using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Flugbahn fuer den Skilltree "Schattenschwarm"
/// (<see cref="SkillGrants.Schattenschwarm"/>), gemeinsam fuer Blade Swarm
/// (<see cref="BladeSwarmPrefab"/>) und Blade Storm (<see cref="BladeStormEvoPrefab"/>).
///
/// Der Kunai fliegt durch sein Ziel hindurch bis ein Stueck dahinter, wendet
/// und fliegt wieder hindurch - jedes Mal leicht versetzt, damit er auf dem
/// Weg zufaellig auch andere Gegner neben dem Ziel erwischt. Das geht so
/// lange, bis das Ziel tot ist (oder <see cref="MaxTime"/> um ist).
///
/// Hier stehen nur die Werte und die Geometrie. Bewegen und Schaden machen
/// die Kunai selbst - die beiden Waffen treffen auf unterschiedliche Weise.
/// </summary>
public static class ShadowSwoop
{
    /// <summary>Fluggeschwindigkeit beim Durchfliegen (Einheiten pro Sekunde).</summary>
    public const float Speed = 14f;

    /// <summary>So weit fliegt der Kunai hinter dem Ziel heraus, bevor er wendet.</summary>
    public const float Overshoot = 1.8f;

    /// <summary>Streuung beim Wenden in Grad (plus/minus). 0 = immer exakt zurueck.</summary>
    public const float TurnSpread = 55f;

    /// <summary>
    /// Sicherung: so lange (Sekunden) haelt ein Kunai hoechstens an einem Ziel
    /// fest. Ohne sie wuerden sich an einem Boss mit jeder Salve mehr Kunai
    /// sammeln, die nie verschwinden. 0 = keine Grenze.
    /// </summary>
    public const float MaxTime = 5f;

    /// <summary>Lebt das Ziel noch?</summary>
    public static bool IsAlive(Enemy target)
    {
        return target != null && target.isActiveAndEnabled;
    }

    /// <summary>Ist die Zeit fuer diesen Kunai um?</summary>
    public static bool TimedOut(float elapsed)
    {
        return MaxTime > 0f && elapsed > MaxTime;
    }

    /// <summary>Ein zufaelliger, noch lebender Gegner aus der Liste - fuer Kunai, deren Ziel schon weg ist.</summary>
    public static Enemy AnyAlive(List<Enemy> pool)
    {
        if (pool == null) return null;
        pool.RemoveAll(e => !IsAlive(e));
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    /// <summary>Erste Richtung: vom Kunai aus aufs Ziel.</summary>
    public static Vector2 FirstDirection(Vector2 from, Enemy target)
    {
        Vector2 d = (Vector2)target.transform.position - from;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitCircle.normalized;
    }

    /// <summary>Nach dem Wenden: zurueck durchs Ziel, leicht versetzt.</summary>
    public static Vector2 TurnDirection(Vector2 previous)
    {
        float angle = Random.Range(-TurnSpread, TurnSpread);
        return Quaternion.Euler(0f, 0f, angle) * -previous;
    }

    /// <summary>Wo der Kunai diesmal wendet: hinter dem Ziel in Flugrichtung.</summary>
    public static Vector2 ExitPoint(Enemy target, Vector2 direction)
    {
        return (Vector2)target.transform.position + direction * Overshoot;
    }
}
