using System.Collections.Generic;
using UnityEngine;

/// <summary>Was ein Beat auf einmal macht.</summary>
public enum BeatKind
{
    /// <summary>Ein Schwall auf einmal, nach einem Muster gesetzt.</summary>
    Burst,

    /// <summary>Kreis um den Spieler, optional mit Kaefig und einem Elite in der Mitte.</summary>
    Encirclement,

    /// <summary>Atempause: der Grunddruck faellt fuer eine Weile ab.</summary>
    Calm,

    /// <summary>
    /// Ein Bosskampf: der Endboss - oder ein Miniboss mitten im Level. Der
    /// Grunddruck geht dabei deutlich zurueck.
    /// </summary>
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
    /// Spieler. <paramref name="boss"/> (ein Elite oder Miniboss, genau einer)
    /// darf None sein - dann ist es nur der Ring. Als Ringgegner taugen Elites
    /// nicht, die setzt der Director dort nicht.
    ///
    /// Die letzten drei Werte haben Vorgaben, die fuer fast jeden Ring passen.
    /// Sie stehen trotzdem als Parameter da, damit die Wellenplan-Werkstatt
    /// einen Plan verlustfrei zurueckschreiben kann: was sie nicht als
    /// Parameter ausdruecken kann, wuerde sie beim Speichern verlieren.
    /// </summary>
    public Phase Encircle(float time, EnemyId boss, EnemyId ringEnemy, int ringCount,
                          float radius, bool cage = false, string announce = "",
                          float pressureScale = 0.35f, float holdTime = 25f,
                          float warnTime = 1.5f)
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
            PressureScale = pressureScale,   // waehrend des Kampfes tritt das Grundrauschen zurueck
            Duration = holdTime,
            WarnTime = warnTime,
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

    public Phase Boss(float time, EnemyId boss, string announce = "", float pressureScale = 0.4f)
    {
        Beats.Add(new Beat
        {
            Time = time,
            Kind = BeatKind.Boss,
            Enemy = boss,
            Pattern = Patterns.Scatter,
            PressureScale = pressureScale,
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

    /// <summary>
    /// Zieht einen Gegner aus dem Pool - gewichtet. Elites werden dabei
    /// uebergangen: die kommen nur einzeln ueber einen Beat, nie als Nachschub.
    /// </summary>
    public EnemyId PickEnemy()
    {
        if (weightSum <= 0f)
        {
            foreach (PoolEntry entry in Enemies)
            {
                if (!EnemyCatalog.IsElite(entry.Id)) weightSum += entry.Weight;
            }
        }

        if (weightSum <= 0f) return EnemyId.Marshmello;

        float roll = Random.Range(0f, weightSum);
        EnemyId last = EnemyId.Marshmello;
        foreach (PoolEntry entry in Enemies)
        {
            if (EnemyCatalog.IsElite(entry.Id)) continue;
            last = entry.Id;
            roll -= entry.Weight;
            if (roll <= 0f) return entry.Id;
        }

        return last;
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

    /// <summary>
    /// Kommt im Story-Modus, sobald der Boss gefallen ist, und beendet den
    /// Lauf (ein DeathBoss wie der Todes-Ramen). Weil der Boss da schon liegt,
    /// zaehlt der Tod als Sieg. None = nach dem Boss geht es endlos weiter.
    /// </summary>
    public EnemyId Finisher = EnemyId.None;

    public RunPlan(string id)
    {
        Id = id;
    }

    /// <summary>Setzt den <see cref="Finisher"/> - wer nach dem Boss den Lauf beendet.</summary>
    public void EndWith(EnemyId finisher)
    {
        Finisher = finisher;
    }

    /// <summary>Haengt eine Phase an und gibt sie zum Weiterbauen zurueck.</summary>
    public Phase Phase(float duration)
    {
        var phase = new Phase { Duration = duration };
        Phases.Add(phase);
        return phase;
    }

    /// <summary>Legt die Phase fuer "danach" an und gibt sie zum Weiterbauen zurueck.</summary>
    public Phase EndlessPhase()
    {
        Endless = new Phase { Duration = 120f };
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
