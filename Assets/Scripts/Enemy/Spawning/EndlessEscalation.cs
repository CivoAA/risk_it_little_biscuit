using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Endlos-Teil eines Endless-Laufs: was nach der Story kommt.
///
/// Ein Endless-Lauf spielt zuerst den normalen Plan der Welt (Miniboss um
/// 10:00, Boss um 20:00). Der Story-Teil endet, sobald der Boss der Welt
/// faellt - oder der Plan durch ist, falls er nie faellt. Ab da laeuft die
/// Endlos-Phase des Plans, und es zaehlen Gold und Charakter-XP nur noch zu
/// <see cref="RunDifficulty.EndlessRewardShare"/>.
///
/// Die Gegner werden ab da nicht nur zaeher (Chaos-Rampe in
/// <see cref="RunDifficulty"/>), es kommen auch immer staerkere dazu:
///
///   Elite-Wellen   alle <see cref="EliteInterval"/> s, jedes Mal einer mehr
///                  (2, 3, 4 ... bis <see cref="MaxElites"/>), gemischt aus
///                  den Elites der Welt
///   Boss-Wellen    1 Miniboss + Boss, dann 2 Minibosse + Boss, dann 3 ...
///                  alle <see cref="BossInterval"/> s, egal ob die letzte
///                  schon geraeumt ist - wer nicht hinterherkommt, hat bald
///                  mehrere Bosse gleichzeitig. (Der Verkohlte holt in den
///                  Boss-Wellen niemanden ins Herz, zwei davon sind also ok.)
///
/// Bis der Spieler stirbt. Wer Elite, Miniboss und Boss einer Welt ist, liest
/// die Klasse aus dem Wellenplan selbst (Rollen aus dem <see cref="EnemyCatalog"/>)
/// - fuer eine neue Welt muss hier nichts eingetragen werden.
/// </summary>
public class EndlessEscalation
{
    /// <summary>Erste Elite-Welle, Sekunden nach Beginn des Endlos-Teils.</summary>
    public const float FirstEliteWave = 45f;

    /// <summary>Abstand zwischen zwei Elite-Wellen.</summary>
    public const float EliteInterval = 90f;

    /// <summary>So viele Elites bringt die erste Welle, jede weitere einen mehr.</summary>
    public const int FirstEliteCount = 2;
    public const int MaxElites = 8;

    /// <summary>Erste Boss-Welle, Sekunden nach Beginn des Endlos-Teils.</summary>
    public const float FirstBossWave = 150f;

    /// <summary>Abstand zwischen zwei Boss-Wellen - die naechste wartet nicht auf die letzte.</summary>
    public const float BossInterval = 150f;

    public const int MaxMiniBosses = 4;

    /// <summary>Grunddruck waehrend einer Boss-Welle (1 = normal).</summary>
    public const float BossPressureScale = 0.5f;

    private readonly List<EnemyId> elites = new List<EnemyId>();
    private readonly List<EnemyId> miniBosses = new List<EnemyId>();
    private EnemyId boss = EnemyId.None;

    private float time;
    private float nextElite = FirstEliteWave;
    private int eliteWaves;

    private float nextBoss = FirstBossWave;
    private int bossWaves;
    private readonly List<Enemy> bossWave = new List<Enemy>();

    public EndlessEscalation(RunPlan plan)
    {
        foreach (Phase phase in plan.Phases) Collect(phase);
        Collect(plan.Endless);

        Debug.Log($"[EndlessEscalation] Elites: {string.Join(", ", elites)} | " +
                  $"Minibosse: {string.Join(", ", miniBosses)} | Boss: {boss}");
    }

    private void Collect(Phase phase)
    {
        if (phase == null) return;

        foreach (Beat beat in phase.Beats)
        {
            EnemyDef def = EnemyCatalog.Get(beat.Enemy);
            if (def == null || def.Archived) continue;

            switch (def.Role)
            {
                case EnemyRole.Elite:
                    if (!elites.Contains(beat.Enemy)) elites.Add(beat.Enemy);
                    break;
                case EnemyRole.MiniBoss:
                    if (!miniBosses.Contains(beat.Enemy)) miniBosses.Add(beat.Enemy);
                    break;
                case EnemyRole.Boss:
                    boss = beat.Enemy;
                    break;
            }
        }
    }

    /// <summary>Laeuft nur, solange der Director laeuft (nicht in der Herzkammer).</summary>
    public void Tick(float dt, SpawnDirector director)
    {
        time += dt;

        // Alle Bosse geraeumt: der Nachschub laeuft wieder voll.
        // (Unity-Vergleich: tote Gegner sind hier == null.)
        if (bossWave.Count > 0 && bossWave.TrueForAll(e => e == null))
        {
            bossWave.Clear();
            director.ReleasePressure();
        }

        if (time >= nextElite && elites.Count > 0)
        {
            int count = Mathf.Min(FirstEliteCount + eliteWaves, MaxElites);
            eliteWaves++;
            nextElite = time + EliteInterval;

            director.SayPlan("ELITE-ALARM!");
            for (int i = 0; i < count; i++)
            {
                director.SpawnEndlessElite(elites[Random.Range(0, elites.Count)]);
            }
        }

        if (time >= nextBoss && (boss != EnemyId.None || miniBosses.Count > 0))
        {
            bossWaves++;
            nextBoss = time + BossInterval;
            bossWave.RemoveAll(e => e == null);
            int minis = miniBosses.Count > 0 ? Mathf.Min(bossWaves, MaxMiniBosses) : 0;

            director.SayPlan("BOSS-WELLE!");
            director.DampenForBossWave(BossPressureScale);

            for (int i = 0; i < minis; i++)
            {
                Add(director.SpawnEndlessBoss(miniBosses[i % miniBosses.Count]));
            }
            Add(director.SpawnEndlessBoss(boss));

            // Nichts gesetzt (Prefab fehlt): nicht gedaempft stehen bleiben.
            if (bossWave.Count == 0) director.ReleasePressure();
        }
    }

    private void Add(Enemy enemy)
    {
        if (enemy != null) bossWave.Add(enemy);
    }
}
