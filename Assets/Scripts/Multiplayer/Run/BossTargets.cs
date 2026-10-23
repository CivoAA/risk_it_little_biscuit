using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hilfen fuer Bosse und Spezialgegner, die selbst zielen und treffen.
///
/// Allein ist "der Spieler" immer der eigene. Im Koop zielt ein Boss auf
/// den naechsten Spieler, der noch steht (<see cref="Nearest"/>), und seine
/// Treffer gelten fuer jeden, der in der Flaeche steht - nicht nur fuer den,
/// den er gerade anvisiert (<see cref="HurtInCircle"/>, <see cref="HurtOnSegment"/>).
/// Mitspieler bekommen den Schaden geschickt (<see cref="NetPlayers.Hurt"/>).
///
/// Damit ein Treffer, der mehrere Bilder lang prueft (eine Sprint-Spur, ein
/// Strahl), nicht in jedem Bild neu trifft, gibt es <see cref="HitSet"/>:
/// wer schon dran war, bleibt draussen.
/// </summary>
public static class BossTargets
{
    private static readonly List<NetPlayers.Target> targets = new List<NetPlayers.Target>();

    /// <summary>Wer schon getroffen wurde (Schluessel: Platz, allein -1).</summary>
    public sealed class HitSet
    {
        private readonly HashSet<int> hit = new HashSet<int>();
        public bool Contains(int slot) => hit.Contains(slot);
        public bool Add(int slot) => hit.Add(slot);
        public void Clear() => hit.Clear();
        public bool Any => hit.Count > 0;
    }

    /// <summary>Alle Spieler, die gerade angegriffen werden koennen (allein: der eigene, wenn er lebt).</summary>
    public static List<NetPlayers.Target> All()
    {
        NetPlayers.Collect(targets);
        return targets;
    }

    /// <summary>Gibt es ueberhaupt jemanden zum Angreifen?</summary>
    public static bool AnyAlive => NetPlayers.TryNearest(Vector2.zero, out _);

    /// <summary>Der naechste Spieler zu <paramref name="from"/>.</summary>
    public static bool Nearest(Vector2 from, out NetPlayers.Target target) => NetPlayers.TryNearest(from, out target);

    /// <summary>Position des naechsten Spielers, sonst die des eigenen (oder from).</summary>
    public static Vector2 NearestPosition(Vector2 from) => NetPlayers.NearestPosition(from);

    /// <summary>Jeder Spieler, dessen Mitte im Kreis liegt, nimmt Schaden. Zahl der Getroffenen.</summary>
    public static int HurtInCircle(Vector2 center, float radius, float damage, HitSet already = null)
    {
        int count = 0;
        float r2 = radius * radius;
        foreach (NetPlayers.Target t in All())
        {
            if (already != null && already.Contains(t.Slot)) continue;
            if ((t.Position - center).sqrMagnitude > r2) continue;
            already?.Add(t.Slot);
            NetPlayers.Hurt(t, damage);
            count++;
        }
        return count;
    }

    /// <summary>Steht irgendein Spieler im Kreis?</summary>
    public static bool AnyInCircle(Vector2 center, float radius)
    {
        float r2 = radius * radius;
        foreach (NetPlayers.Target t in All())
            if ((t.Position - center).sqrMagnitude <= r2) return true;
        return false;
    }

    /// <summary>Jeder Spieler naeher als <paramref name="radius"/> an der Strecke a-b nimmt Schaden.</summary>
    public static int HurtOnSegment(Vector2 a, Vector2 b, float radius, float damage, HitSet already = null)
    {
        int count = 0;
        foreach (NetPlayers.Target t in All())
        {
            if (already != null && already.Contains(t.Slot)) continue;
            if (DistanceToSegment(t.Position, a, b) > radius) continue;
            already?.Add(t.Slot);
            NetPlayers.Hurt(t, damage);
            count++;
        }
        return count;
    }

    /// <summary>Jeder Spieler, fuer den <paramref name="inside"/> ja sagt, nimmt Schaden.</summary>
    public static int HurtWhere(System.Func<Vector2, bool> inside, float damage, HitSet already = null)
    {
        int count = 0;
        foreach (NetPlayers.Target t in All())
        {
            if (already != null && already.Contains(t.Slot)) continue;
            if (!inside(t.Position)) continue;
            already?.Add(t.Slot);
            NetPlayers.Hurt(t, damage);
            count++;
        }
        return count;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.000001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
