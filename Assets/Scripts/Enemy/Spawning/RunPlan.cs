using System.Collections.Generic;
using UnityEngine;

/// <summary>Was ein Beat auf einmal macht.</summary>
public enum BeatKind
{
    /// <summary>Ein Schwall auf einmal, nach einem Muster gesetzt.</summary>
    Burst,

    /// <summary>Kreis um den Spieler, optional mit Kaefig und Miniboss in der Mitte.</summary>
    Encirclement,

    /// <summary>Atempause: der Grunddruck faellt fuer eine Weile ab.</summary>
    Calm,

    /// <summary>Der Endboss. Der Grunddruck geht dabei deutlich zurueck.</summary>
    Boss,
}

/// <summary>Ein fester Zeitpunkt in einer Phase, an dem etwas passiert.</summary>
public class Beat
{
    public float Time;                 // Sekunden ab Phasenbeginn
    public BeatKind Kind = BeatKind.Burst;

    public EnemyId Enemy = EnemyId.None;
    public float Threat;               // wie viel Bedrohung der Einwurf mitbringt
    public ISpawnPattern Pattern;
    public float Radius;

    // Encirclement
    public EnemyId RingEnemy = EnemyId.None;
    public int RingCount;
    public bool Cage;                  // zusaetzlich ein Blocker-Kaefig
    public float WarnTime = 1.5f;      // Vorwarnung, bevor es zugeht

    // Calm / Encirclement / Boss: Grunddruck waehrenddessen (1 = normal)
    public float PressureScale = 1f;
    public float Duration;

    public string Announce;            // Text im Wave-Feld, leer = nichts
}

/// <summary>Ein Gegner im Pool einer Phase, mit Gewicht.</summary>
public struct PoolEntry
{
    public EnemyId Id;
    public float Weight;
}

/// <summary>
/// Ein Abschnitt des Laufs: woraus der Nachschub besteht, wie viel Druck
/// anliegen soll und was an festen Zeitpunkten passiert.
/// </summary>
public class Phase
{
    public string Name = "";
    public float Duration = 300f;

    /// <summary>Bedrohungssumme, die zu Beginn bzw. am Ende der Phase anliegen soll.</summary>
    public float PressureStart = 10f;
    public float PressureEnd = 40f;

    /// <summary>Muster fuer den laufenden Nachschub.</summary>
    public ISpawnPattern BasePattern = Patterns.Scatter;

    public readonly List<PoolEntry> Enemies = new List<PoolEntry>();
    public readonly List<Beat> Beats = new List<Beat>();

    private float weightSum;

    // ------------------------------------------------------------ Bauen

    public Phase Pool(EnemyId id, float weight)
    {
        Enemies.Add(new PoolEntry { Id = id, Weight = Mathf.Max(0.01f, weight) });
        weightSum = 0f;
        return this;
    }

    public Phase Pressure(float from, float to)
    {
        PressureStart = from;
        PressureEnd = to;
        return this;
    }

    public Phase Base(ISpawnPattern pattern)
    {
        BasePattern = pattern;
        return this;
    }

    /// <summary>Ein Schwall zu einem Zeitpunkt (Sekunden ab Phasenbeginn).</summary>
    public Phase Burst(float time, EnemyId enemy, float threat, ISpawnPattern pattern, float radius = 0f)
    {
        Beats.Add(new Beat
        {
            Time = time,
            Kind = BeatKind.Burst,
            Enemy = enemy,
            Threat = threat,
            Pattern = pattern,
            Radius = radius,
        });
        return this;
    }

    /// <summary>
    /// Der Kreis-Moment: Vorwarnung, dann schliesst sich ein Ring um den
    /// Spieler. <paramref name="boss"/> darf None sein - dann ist es nur der
    /// Ring ohne Miniboss.
    /// </summary>
    public Phase Encircle(float time, EnemyId boss, EnemyId ringEnemy, int ringCount,
                          float radius, bool cage = false, string announce = "")
    {
        Beats.Add(new Beat
        {
            Time = time,
            Kind = BeatKind.Encirclement,
            Enemy = boss,
            RingEnemy = ringEnemy,
            RingCount = ringCount,
            Radius = radius,
            Cage = cage,
            Pattern = Patterns.Ring,
            PressureScale = 0.35f,   // waehrend des Kampfes tritt das Grundrauschen zurueck
            Duration = 25f,
            Announce = announce,
        });
        return this;
    }

    /// <summary>Atempause - ohne Taeler gibt es keine Spitzen.</summary>
    public Phase Calm(float time, float seconds, float scale = 0.15f)
    {
        Beats.Add(new Beat
        {
            Time = time,
            Kind = BeatKind.Calm,
            Duration = seconds,
            PressureScale = scale,
        });
        return this;
    }

    public Phase Boss(float time, EnemyId boss, string announce = "")
    {
        Beats.Add(new Beat
        {
            Time = time,
            Kind = BeatKind.Boss,
            Enemy = boss,
            Pattern = Patterns.Scatter,
            PressureScale = 0.4f,
            Duration = 999f,
            Announce = announce,
        });
        return this;
    }

    // ------------------------------------------------------------ Abfragen

    public float PressureAt(float timeInPhase)
    {
        float t = Duration <= 0f ? 1f : Mathf.Clamp01(timeInPhase / Duration);
        return Mathf.Lerp(PressureStart, PressureEnd, t);
    }

    /// <summary>Zieht einen Gegner aus dem Pool - gewichtet.</summary>
    public EnemyId PickEnemy()
    {
        if (Enemies.Count == 0) return EnemyId.Marshmello;

        if (weightSum <= 0f)
        {
            foreach (PoolEntry entry in Enemies) weightSum += entry.Weight;
        }

        float roll = Random.Range(0f, weightSum);
        foreach (PoolEntry entry in Enemies)
        {
            roll -= entry.Weight;
            if (roll <= 0f) return entry.Id;
        }

        return Enemies[Enemies.Count - 1].Id;
    }
}

/// <summary>
/// Der Ablauf eines Laufs auf einer Karte: eine Kette von Phasen, dazu eine
/// Phase fuer alles danach.
///
/// Die Plaene stehen als Code in <see cref="WavePlans"/> - wie Achievements,
/// Shop und Unlocks auch. Damit haengt kein Balancing mehr im Szenen-YAML.
/// </summary>
public class RunPlan
{
    public readonly string Id;
    public readonly List<Phase> Phases = new List<Phase>();

    /// <summary>
    /// Laeuft, wenn alle Phasen durch sind - im Story-Modus nach dem Boss, im
    /// Endless-Modus fuer immer. Null = die letzte Phase laeuft ohne Beats
    /// weiter.
    /// </summary>
    public Phase Endless;

    public RunPlan(string id)
    {
        Id = id;
    }

    /// <summary>Haengt eine Phase an und gibt sie zum Weiterbauen zurueck.</summary>
    public Phase Phase(string name, float duration)
    {
        var phase = new Phase { Name = name, Duration = duration };
        Phases.Add(phase);
        return phase;
    }

    /// <summary>Legt die Phase fuer "danach" an und gibt sie zum Weiterbauen zurueck.</summary>
    public Phase EndlessPhase(string name)
    {
        Endless = new Phase { Name = name, Duration = 120f };
        return Endless;
    }

    public float TotalDuration
    {
        get
        {
            float total = 0f;
            foreach (Phase phase in Phases) total += phase.Duration;
            return total;
        }
    }
}
