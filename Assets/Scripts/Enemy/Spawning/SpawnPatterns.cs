using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Alles, was ein Muster wissen muss, um Punkte zu setzen: wo der Spieler
/// steht, wohin er laeuft und wie gross das Rechteck knapp ausserhalb des
/// Bildes ist.
/// </summary>
public struct SpawnContext
{
    public Vector2 PlayerPos;

    /// <summary>Laufrichtung des Spielers (normalisiert). Nullvektor, wenn er steht.</summary>
    public Vector2 PlayerDir;

    /// <summary>Halbe Breite/Hoehe des Spawn-Rechtecks um den Spieler herum.</summary>
    public float HalfWidth;
    public float HalfHeight;
}

/// <summary>
/// Das "Wo" des Spawnens. Ein Muster bekommt einen Kontext und schreibt
/// Positionen in eine Liste - mehr nicht. Neue Muster sind eine neue Klasse
/// hier drin, der <see cref="SpawnDirector"/> aendert sich dafuer nicht.
/// </summary>
public interface ISpawnPattern
{
    /// <summary>Haengt <paramref name="count"/> Positionen an <paramref name="into"/> an.</summary>
    void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius);
}

/// <summary>Die fertigen Muster. Plaene greifen sie ueber diese Felder ab.</summary>
public static class Patterns
{
    /// <summary>Zufaellig auf dem Rand des Rechtecks - das bisherige Verhalten.</summary>
    public static readonly ISpawnPattern Scatter = new ScatterPattern();

    /// <summary>Geschlossener Kreis um den Spieler. Gegner laufen von selbst nach innen.</summary>
    public static readonly ISpawnPattern Ring = new RingPattern();

    /// <summary>Halbkreis in Laufrichtung - man rennt hinein.</summary>
    public static readonly ISpawnPattern Arc = new ArcPattern();

    /// <summary>Kolonne von einer zufaelligen Seite, schiebt aus der Ecke.</summary>
    public static readonly ISpawnPattern Column = new ColumnPattern();

    /// <summary>Rudel an einem Punkt statt gleichmaessig verteilt.</summary>
    public static readonly ISpawnPattern Cluster = new ClusterPattern();

    /// <summary>Dort, wo der Spieler gleich sein wird.</summary>
    public static readonly ISpawnPattern Ambush = new AmbushPattern();
}

// ---------------------------------------------------------------- Umsetzungen

/// <summary>
/// Zufaelliger Punkt auf dem Rand des Rechtecks. Genau das, was der alte
/// TimeWaveManager gemacht hat - Grundrauschen ohne Aussage.
/// </summary>
public class ScatterPattern : ISpawnPattern
{
    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 point;

            if (Random.value > 0.5f)
            {
                point.x = Random.Range(-ctx.HalfWidth, ctx.HalfWidth);
                point.y = Random.value > 0.5f ? -ctx.HalfHeight : ctx.HalfHeight;
            }
            else
            {
                point.y = Random.Range(-ctx.HalfHeight, ctx.HalfHeight);
                point.x = Random.value > 0.5f ? -ctx.HalfWidth : ctx.HalfWidth;
            }

            into.Add(ctx.PlayerPos + point);
        }
    }
}

/// <summary>
/// Gleichmaessiger Kreis um den Spieler. Die Gegner laufen ohnehin auf ihn zu -
/// der Kreis zieht sich also von selbst zu. Ein kleiner Zufallsanteil am Radius
/// verhindert, dass alles gleichzeitig ankommt.
/// </summary>
public class RingPattern : ISpawnPattern
{
    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        if (count <= 0) return;

        float r = radius > 0f ? radius : ctx.HalfHeight;
        float start = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float angle = start + i * Mathf.PI * 2f / count;
            float jitter = r * Random.Range(0.95f, 1.1f);
            into.Add(ctx.PlayerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * jitter);
        }
    }
}

/// <summary>
/// Halbkreis in Laufrichtung. Steht der Spieler, wird eine Richtung gewuerfelt -
/// sonst haette das Muster keinen Bezugspunkt.
/// </summary>
public class ArcPattern : ISpawnPattern
{
    private const float Spread = 110f;   // Grad, gesamte Oeffnung

    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        if (count <= 0) return;

        Vector2 dir = ctx.PlayerDir.sqrMagnitude > 0.01f ? ctx.PlayerDir : Random.insideUnitCircle.normalized;
        float center = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float r = radius > 0f ? radius : ctx.HalfHeight * 1.5f;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = (center - Spread * 0.5f + Spread * t) * Mathf.Deg2Rad;
            float jitter = r * Random.Range(0.9f, 1.15f);
            into.Add(ctx.PlayerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * jitter);
        }
    }
}

/// <summary>
/// Eine Seite des Rechtecks, dicht besetzt. Wirkt wie eine heranrueckende
/// Front und draengt den Spieler in eine Richtung.
/// </summary>
public class ColumnPattern : ISpawnPattern
{
    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        if (count <= 0) return;

        bool vertical = Random.value > 0.5f;
        float sign = Random.value > 0.5f ? 1f : -1f;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);

            if (vertical)
            {
                float y = Mathf.Lerp(-ctx.HalfHeight, ctx.HalfHeight, t);
                float x = sign * ctx.HalfWidth * Random.Range(0.95f, 1.15f);
                into.Add(ctx.PlayerPos + new Vector2(x, y));
            }
            else
            {
                float x = Mathf.Lerp(-ctx.HalfWidth, ctx.HalfWidth, t);
                float y = sign * ctx.HalfHeight * Random.Range(0.95f, 1.15f);
                into.Add(ctx.PlayerPos + new Vector2(x, y));
            }
        }
    }
}

/// <summary>
/// Ein Rudel: ein Punkt auf dem Rand, alle drumherum. Trifft den Spieler als
/// Block statt als Regen.
/// </summary>
public class ClusterPattern : ISpawnPattern
{
    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        if (count <= 0) return;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = radius > 0f ? radius : ctx.HalfHeight * 1.4f;
        Vector2 center = ctx.PlayerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

        float spread = Mathf.Max(1.5f, Mathf.Sqrt(count) * 0.6f);
        for (int i = 0; i < count; i++)
        {
            into.Add(center + Random.insideUnitCircle * spread);
        }
    }
}

/// <summary>
/// Vor den Spieler gesetzt: dorthin, wo er in ein paar Sekunden ist. Steht er,
/// ist das ein enger Kreis - dann hat der Hinterhalt keinen Vorteil, und das
/// ist auch richtig so.
/// </summary>
public class AmbushPattern : ISpawnPattern
{
    private const float LookAhead = 6f;

    public void Fill(List<Vector2> into, in SpawnContext ctx, int count, float radius)
    {
        Vector2 center = ctx.PlayerPos + ctx.PlayerDir * LookAhead;
        float spread = radius > 0f ? radius : 4f;

        for (int i = 0; i < count; i++)
        {
            into.Add(center + Random.insideUnitCircle * spread);
        }
    }
}
