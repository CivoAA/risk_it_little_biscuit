using UnityEngine;

/// <summary>
/// Prueft fuer die Zufalls-Spawner, ob ein Mixer an einer Stelle stehen darf:
///  - sein Feld (der Ring = Trigger-Kreis) haelt Abstand zu allen anderen Mixer-Feldern,
///  - und beruehrt keinen <see cref="MixerBlocker"/> (Baeume).
///
/// Auf der 3x3-Map wandern die Chunks mit dem Spieler mit. Ein Baum taucht
/// darum alle drei Chunks wieder auf - der Abstand wird gegen die naechste
/// dieser Wiederholungen gemessen, nicht nur gegen die aktuelle Position.
/// </summary>
public static class MixerPlacement
{
    /// <summary>So oft wird pro Mixer neu gewuerfelt, bevor er ausfaellt.</summary>
    public const int Attempts = 12;

    /// <summary>Radius des Mixer-Felds in Welt-Einheiten (Trigger-Kreis am Prefab).</summary>
    public static float FieldRadius(GameObject mixer)
    {
        CircleCollider2D circle = mixer.GetComponent<CircleCollider2D>();
        float scale = Mathf.Abs(mixer.transform.lossyScale.x);
        return circle != null ? circle.radius * scale : 4f;
    }

    /// <summary>Mitte des Mixer-Felds, wenn der Mixer bei <paramref name="position"/> steht.</summary>
    public static Vector2 FieldCenter(GameObject mixer, Vector2 position)
    {
        CircleCollider2D circle = mixer.GetComponent<CircleCollider2D>();
        if (circle == null) return position;
        return position + Vector2.Scale(circle.offset, mixer.transform.lossyScale);
    }

    /// <param name="gapInFields">Luecke zwischen zwei Mixer-Feldern, in Feld-Durchmessern.</param>
    public static bool IsFree(GameObject mixerPrefab, Vector2 position, float gapInFields)
    {
        float radius = FieldRadius(mixerPrefab);
        Vector2 center = FieldCenter(mixerPrefab, position);
        float gap = radius * 2f * gapInFields;

        var mixers = MixerObject.All;
        for (int i = 0; i < mixers.Count; i++)
        {
            MixerObject other = mixers[i];
            if (other == null) continue;

            GameObject go = other.gameObject;
            Vector2 otherCenter = FieldCenter(go, go.transform.position);
            float minDistance = radius + FieldRadius(go) + gap;
            if ((center - otherCenter).sqrMagnitude < minDistance * minDistance)
                return false;
        }

        Vector2 period = WorldManager3x3.Current != null ? WorldManager3x3.Current.WrapPeriod : Vector2.zero;

        var blockers = MixerBlocker.All;
        for (int i = 0; i < blockers.Count; i++)
        {
            MixerBlocker blocker = blockers[i];
            if (blocker == null || !blocker.TryGetBounds(out Bounds b)) continue;

            Vector2 delta = Wrap(center - (Vector2)b.center, period);
            Vector2 closest = new Vector2(
                Mathf.Clamp(delta.x, -b.extents.x, b.extents.x),
                Mathf.Clamp(delta.y, -b.extents.y, b.extents.y));

            if ((delta - closest).sqrMagnitude < radius * radius)
                return false;
        }

        return true;
    }

    private static Vector2 Wrap(Vector2 delta, Vector2 period)
    {
        if (period.x > 0f) delta.x -= period.x * Mathf.Round(delta.x / period.x);
        if (period.y > 0f) delta.y -= period.y * Mathf.Round(delta.y / period.y);
        return delta;
    }
}
