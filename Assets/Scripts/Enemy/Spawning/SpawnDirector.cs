using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setzt den Wellenplan der Karte um. Loest den alten TimeWaveManager und die
/// beiden EnemySpawner ab.
///
/// Der Unterschied zu frueher steckt in einem Satz: der Director spawnt keine
/// Stueckzahlen, er haelt einen DRUCK. Der Plan sagt "hier sollen 80
/// Bedrohungspunkte auf dem Feld sein", der Director zaehlt, was lebt, und
/// legt nach. Daraus folgt alles Weitere:
///
///   * Es gibt automatisch eine Obergrenze - das Feld laeuft nicht mehr voll,
///     weil jemand eine Zahl zu gross eingetragen hat.
///   * Ein staerkerer Spieler raeumt schneller und bekommt schneller Nachschub,
///     statt durch eine leere Karte zu laufen.
///   * Balancing ist eine Kurve je Phase statt 90 Einzelwerte.
///
/// Wo gespawnt wird, entscheidet ein <see cref="ISpawnPattern"/> - Rand,
/// Kreis, Bogen in Laufrichtung, Rudel, Hinterhalt. Was und wann, steht im
/// <see cref="RunPlan"/> aus <see cref="WavePlans"/>. Wie stark, kommt aus
/// <see cref="RunDifficulty"/>.
///
/// Das Nachziehen weit entfernter Gegner macht der Director gleich mit - dafuer
/// braucht es kein EnemyTeleport mehr an jedem Prefab: hier passiert es
/// gebuendelt, viermal pro Sekunde, und der Druck bleibt dabei richtig gezaehlt.
/// </summary>
[DisallowMultipleComponent]
public class SpawnDirector : MonoBehaviour
{
    public static SpawnDirector Active { get; private set; }

    [Header("Bausteine")]
    [Tooltip("Prefabs je Gegnerart. Fuellt der Installer aus dem alten TimeWaveManager.")]
    [SerializeField] private SpawnCatalog catalog;

    [Tooltip("Optional: Textfeld fuer Phase und Ansagen (das alte Wave-Feld).")]
    [SerializeField] private TMP_Text phaseText;

    [Tooltip("Optional: kurzer Ton als Vorwarnung vor einem Ring.")]
    [SerializeField] private AudioSource warningSound;

    [Header("Spawnbereich (um den Spieler, knapp ausserhalb des Bildes)")]
    [Tooltip("Uebernommen aus den alten minPos/maxPos des TimeWaveManagers.")]
    [SerializeField] private float spawnHalfWidth = 16.5f;
    [SerializeField] private float spawnHalfHeight = 14f;

    [Tooltip("Weiter weg wird ein Gegner nach vorn geholt statt stehen gelassen.")]
    [SerializeField] private float recycleDistance = 26f;

    [Header("Drosseln")]
    [Tooltip("Hoechstens so viele Gegner pro Nachschub-Takt - sonst gibt es Ruckler.")]
    [SerializeField] private int maxSpawnsPerTick = 8;

    [Tooltip("Harte Obergrenze an gleichzeitigen Gegnern, unabhaengig vom Druck.")]
    [SerializeField] private int maxAlive = 400;

    [Header("Plan")]
    [Tooltip("Leer = der Plan der geladenen Map-Szene (MapDefinition).")]
    [SerializeField] private string planOverride = "";

    [Tooltip("Nur zum Ausprobieren: groesser 0 setzt den Chaos-Wert fuer jeden Lauf. " +
             "1 = normal, 1.5 = deutlich haerter. 0 = die Auswahl aus dem Hub gilt.")]
    [SerializeField] private float chaosOverride = 0f;

    // ---------------------------------------------------------------- Zustand

    private RunPlan plan;
    private Phase phase;
    private int phaseIndex = -1;
    private int nextBeat;

    private float runTime;
    private float phaseTime;

    private float spawnTimer;
    private float maintenanceTimer;

    private const float SpawnTick = 0.1f;
    private const float MaintenanceTick = 0.25f;

    /// <summary>Daempfer auf den Grunddruck, waehrend ein Ring oder der Boss laeuft.</summary>
    private float pressureScale = 1f;
    private float pressureScaleUntil;

    private string announce;
    private float announceUntil;
    private string lastShownText;

    private Vector2 lastPlayerPos;
    private Vector2 playerDir;

    private bool timeLaserUnlocked;

    private readonly List<Vector2> points = new List<Vector2>(64);

    // ------------------------------------------------------------------ Start

    void Awake()
    {
        Active = this;

        if (catalog == null) catalog = GetComponent<SpawnCatalog>();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    void Start()
    {
        // Zum Ausprobieren, solange es im Hub noch keine Schwierigkeitswahl gibt.
        if (chaosOverride > 0f) GameSession.Chaos = chaosOverride;

        RunDifficulty.BeginRun();

        string planId = !string.IsNullOrWhiteSpace(planOverride)
            ? planOverride
            : (MapDefinition.Active != null ? MapDefinition.Active.PlanId : "");

        plan = WavePlans.For(planId);

        foreach (Phase p in plan.Phases) p.Beats.Sort((a, b) => a.Time.CompareTo(b.Time));
        if (plan.Endless != null) plan.Endless.Beats.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (PlayerController.Instance != null)
        {
            lastPlayerPos = PlayerController.Instance.transform.position;
        }

        EnterPhase(0);

        Debug.Log($"[SpawnDirector] Plan \"{plan.Id}\" mit {plan.Phases.Count} Phasen " +
                  $"({plan.TotalDuration:0}s) gestartet.");
    }

    // ----------------------------------------------------------------- Update

    void Update()
    {
        if (!RunIsActive()) return;

        float dt = Time.deltaTime;
        runTime += dt;
        phaseTime += dt;

        RunDifficulty.UpdateEndlessRamp(runTime);

        AdvancePhase();
        FireDueBeats();

        maintenanceTimer += dt;
        if (maintenanceTimer >= MaintenanceTick)
        {
            maintenanceTimer = 0f;
            TrackPlayer();
            RecycleStragglers();
        }

        spawnTimer += dt;
        if (spawnTimer >= SpawnTick)
        {
            spawnTimer = 0f;
            TopUpPressure();
        }

        UpdateText();
        CheckTimeLaser();
    }

    private bool RunIsActive()
    {
        if (plan == null || catalog == null) return false;
        if (PlayerController.Instance == null) return false;
        if (!PlayerController.Instance.gameObject.activeSelf) return false;
        if (GameManager.Instance != null && !GameManager.Instance.gameActiv) return false;
        return true;
    }

    // ----------------------------------------------------------------- Phasen

    private void AdvancePhase()
    {
        if (phase == null || phaseTime < phase.Duration) return;

        if (phaseIndex + 1 < plan.Phases.Count)
        {
            EnterPhase(phaseIndex + 1);
            return;
        }

        // Alles durch: die Endlos-Phase uebernimmt und wiederholt sich. Im
        // Story-Modus ist das der Nachschub nach dem Boss, im Endless-Modus
        // der Rest des Laufs.
        phase = plan.Endless ?? phase;
        phaseIndex = plan.Phases.Count;
        phaseTime = 0f;
        nextBeat = 0;
    }

    private void EnterPhase(int index)
    {
        if (index < 0 || index >= plan.Phases.Count) return;

        phaseIndex = index;
        phase = plan.Phases[index];
        phaseTime = 0f;
        nextBeat = 0;

        Debug.Log($"[SpawnDirector] Phase {index + 1}/{plan.Phases.Count}: \"{phase.Name}\" " +
                  $"({phase.Duration:0}s, Druck {phase.PressureStart:0} -> {phase.PressureEnd:0})");
    }

    // ------------------------------------------------------------------ Beats

    private void FireDueBeats()
    {
        while (phase != null && nextBeat < phase.Beats.Count && phaseTime >= phase.Beats[nextBeat].Time)
        {
            Fire(phase.Beats[nextBeat]);
            nextBeat++;
        }
    }

    private void Fire(Beat beat)
    {
        switch (beat.Kind)
        {
            case BeatKind.Burst:
                SpawnThreat(beat.Enemy, beat.Threat, beat.Pattern ?? phase.BasePattern, beat.Radius, true);
                break;

            case BeatKind.Encirclement:
                StartCoroutine(RunEncirclement(beat));
                break;

            case BeatKind.Calm:
                DampenPressure(beat.PressureScale, beat.Duration);
                break;

            case BeatKind.Boss:
                DampenPressure(beat.PressureScale, beat.Duration);
                Announce(beat.Announce);
                SpawnBoss(beat.Enemy);
                break;
        }
    }

    /// <summary>
    /// Der Ring: erst eine Vorwarnung, dann schliesst sich der Kreis. Der
    /// Grunddruck faellt dabei ab - der Moment soll sich wie ein Kampf anfuehlen
    /// und nicht wie "jetzt ist es halt noch voller".
    /// </summary>
    private IEnumerator RunEncirclement(Beat beat)
    {
        Announce(string.IsNullOrEmpty(beat.Announce) ? "ACHTUNG!" : beat.Announce);
        if (warningSound != null) warningSound.Play();

        DampenPressure(beat.PressureScale, beat.Duration + beat.WarnTime);

        yield return new WaitForSeconds(beat.WarnTime);

        SpawnContext ctx = Context();

        // Der Kaefig: dichte Blocker-Wand etwas ausserhalb des Rings. Sie
        // verschwindet, sobald der Miniboss faellt (siehe Enemy: alles auf dem
        // Layer Enemy_barrier wird dann geraeumt) - ohne Miniboss stuende sie
        // fuer immer, deshalb nur zusammen mit einem.
        if (beat.Cage && beat.Enemy != EnemyId.None)
        {
            float cageRadius = beat.Radius * 1.35f;
            int cageCount = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * cageRadius / 0.9f), 40, 160);

            points.Clear();
            Patterns.Ring.Fill(points, ctx, cageCount, cageRadius);
            foreach (Vector2 point in points) Spawn(EnemyId.Blocker, point, false);
        }

        if (beat.RingEnemy != EnemyId.None && beat.RingCount > 0)
        {
            points.Clear();
            Patterns.Ring.Fill(points, ctx, beat.RingCount, beat.Radius);
            foreach (Vector2 point in points) Spawn(beat.RingEnemy, point, false);
        }

        if (beat.Enemy != EnemyId.None)
        {
            points.Clear();
            Patterns.Ring.Fill(points, ctx, 1, beat.Radius * 0.55f);
            if (points.Count > 0) Spawn(beat.Enemy, points[0], false);
        }
    }

    private void SpawnBoss(EnemyId id)
    {
        if (id == EnemyId.None) return;

        // Der Keks-Koenig laeuft nicht selbst (siehe Enemy.FixedUpdate, BossBoss)
        // - er darf also nicht am aeussersten Rand stehen, sonst findet ihn
        // niemand.
        points.Clear();
        Patterns.Ring.Fill(points, Context(), 1, 10f);
        if (points.Count > 0) Spawn(id, points[0], false);
    }

    private void DampenPressure(float scale, float seconds)
    {
        pressureScale = Mathf.Clamp01(scale);
        pressureScaleUntil = runTime + Mathf.Max(0.5f, seconds);
    }

    // ------------------------------------------------------------- Nachschub

    private void TopUpPressure()
    {
        if (phase == null) return;

        if (runTime >= pressureScaleUntil) pressureScale = 1f;

        float target = phase.PressureAt(phaseTime) * RunDifficulty.CountFactor * pressureScale;
        float current = CurrentThreat(out int aliveCount);

        if (current >= target || aliveCount >= maxAlive) return;

        int spawned = 0;
        while (current < target && spawned < maxSpawnsPerTick && aliveCount + spawned < maxAlive)
        {
            EnemyId id = phase.PickEnemy();

            points.Clear();
            phase.BasePattern.Fill(points, Context(), 1, 0f);
            if (points.Count == 0) break;

            if (Spawn(id, points[0], true) == null) break;

            current += SpawnCatalog.Threat(id);
            spawned++;
        }
    }

    /// <summary>
    /// Was gerade an Bedrohung auf dem Feld steht. Zaehlt alle Gegner, auch die
    /// aus anderen Quellen (Mini-Muffins, Begleiter-Spawns) - Bosse und Kaefig
    /// bleiben draussen, sonst wuerde der Nachschub waehrend eines Bosskampfs
    /// komplett versiegen.
    /// </summary>
    private float CurrentThreat(out int aliveCount)
    {
        float sum = 0f;
        aliveCount = 0;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null) continue;

            aliveCount++;
            if (enemy.IsBoss) continue;

            sum += enemy.RunThreat;
        }

        return sum;
    }

    // ------------------------------------------------------------- Aufraeumen

    private void TrackPlayer()
    {
        if (PlayerController.Instance == null) return;

        Vector2 now = PlayerController.Instance.transform.position;
        Vector2 delta = now - lastPlayerPos;

        if (delta.sqrMagnitude > 0.0025f) playerDir = delta.normalized;

        lastPlayerPos = now;
    }

    /// <summary>
    /// Wer zu weit weg ist, wird nach vorn geholt - ueber das aktuelle Muster,
    /// nicht an eine zufaellige Stelle neben dem Spieler. Das ersetzt
    /// EnemyTeleport und haelt nebenbei den Druck stabil: der Gegner bleibt am
    /// Leben und zaehlt weiter.
    /// </summary>
    private void RecycleStragglers()
    {
        if (phase == null || PlayerController.Instance == null) return;

        Vector2 player = PlayerController.Instance.transform.position;
        float limitSqr = recycleDistance * recycleDistance;

        SpawnContext ctx = Context();
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.IsBoss || !enemy.CanRecycle) continue;

            Vector2 pos = enemy.transform.position;
            if ((pos - player).sqrMagnitude < limitSqr) continue;

            points.Clear();
            phase.BasePattern.Fill(points, ctx, 1, 0f);
            if (points.Count > 0) enemy.transform.position = points[0];
        }
    }

    // ---------------------------------------------------------------- Spawnen

    private SpawnContext Context()
    {
        return new SpawnContext
        {
            PlayerPos = PlayerController.Instance != null
                ? (Vector2)PlayerController.Instance.transform.position
                : lastPlayerPos,
            PlayerDir = playerDir,
            HalfWidth = spawnHalfWidth,
            HalfHeight = spawnHalfHeight,
        };
    }

    /// <summary>Setzt eine bestimmte Bedrohungsmenge auf einmal - fuer Bursts.</summary>
    private void SpawnThreat(EnemyId id, float threat, ISpawnPattern pattern, float radius, bool recyclable)
    {
        if (id == EnemyId.None || threat <= 0f) return;

        float each = Mathf.Max(0.1f, SpawnCatalog.Threat(id));
        int count = Mathf.Clamp(Mathf.RoundToInt(threat * RunDifficulty.CountFactor / each), 1, 120);

        points.Clear();
        pattern.Fill(points, Context(), count, radius);

        foreach (Vector2 point in points) Spawn(id, point, recyclable);
    }

    private GameObject Spawn(EnemyId id, Vector2 position, bool recyclable)
    {
        GameObject prefab = catalog.Prefab(id);
        if (prefab == null)
        {
            Debug.LogWarning($"[SpawnDirector] Kein Prefab fuer {id} im Katalog - uebersprungen.");
            return null;
        }

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity);

        Scene runScene = RunScene.Current;
        if (runScene.IsValid() && runScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(spawned, runScene);
        }

        Enemy enemy = spawned.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.RunThreat = SpawnCatalog.Threat(id);
            enemy.CanRecycle = recyclable;
            RunDifficulty.Apply(enemy);
        }

        return spawned;
    }

    // -------------------------------------------------------------------- UI

    private void Announce(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        announce = text;
        announceUntil = runTime + 3f;
    }

    private void UpdateText()
    {
        if (phaseText == null) return;

        string text = runTime < announceUntil ? announce : (phase != null ? phase.Name : "");
        if (text == lastShownText) return;

        lastShownText = text;
        phaseText.text = text;
    }

    /// <summary>
    /// Stand aus dem alten TimeWaveManager: wer lange genug durchhaelt, schaltet
    /// den Time Laser frei.
    /// </summary>
    private void CheckTimeLaser()
    {
        if (timeLaserUnlocked || runTime < 666f) return;

        timeLaserUnlocked = true;
        Unlocks.Grant(Unlocks.TimeLaser);
    }
}
