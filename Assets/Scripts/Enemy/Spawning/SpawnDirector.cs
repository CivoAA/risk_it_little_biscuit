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
    [Tooltip("Prefabs je Gegnerart. Fuellt die Gegner-Werkstatt.")]
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

    [Tooltip("So weit ausserhalb des sichtbaren Bildes muss ein Gegner mindestens erscheinen.")]
    [SerializeField] private float offScreenMargin = 1.5f;

    [Tooltip("So lange muss ein Gegner im Stau stehen (zu viele Gegner zwischen ihm und dem Spieler), " +
             "bevor er ausserhalb des Bildes neu angesetzt wird.")]
    [SerializeField] private float blockedRecycleDelay = 1f;

    [Header("Nachschub")]
    [Tooltip("Unter dem Ziel-Druck: Anteil des Ziels, der pro Sekunde nachkommt. " +
             "0.25 = in 4 s von leer auf voll - statt alles auf einmal.")]
    [SerializeField] private float refillPerSecond = 0.25f;

    [Tooltip("Auch AM Ziel kommt immer etwas nach: Anteil des Ziels pro Sekunde. " +
             "Wer nicht hinterherkommt, wird ueberrannt, statt Kreise zu laufen.")]
    [SerializeField] private float tricklePerSecond = 0.04f;

    [Tooltip("Mindestens so viel Bedrohung pro Sekunde fliesst nach, auch bei kleinem Ziel.")]
    [SerializeField] private float minTricklePerSecond = 0.75f;

    [Tooltip("Bis zu diesem Vielfachen des Ziel-Drucks laeuft der Dauer-Nachschub weiter.")]
    [SerializeField] private float overflowCap = 2f;

    [Header("Drosseln")]
    [Tooltip("Hoechstens so viele Gegner pro Nachschub-Takt - sonst gibt es Ruckler.")]
    [SerializeField] private int maxSpawnsPerTick = 8;

    [Tooltip("Harte Obergrenze an gleichzeitigen Gegnern, unabhaengig vom Druck.")]
    [SerializeField] private int maxAlive = 400;

    [Tooltip("Hoechstens so viele Gegner aus Bursts und Ringen pro Frame. Ein Ring mit Kaefig " +
             "sind fast 200 Gegner - auf einen Schlag gab das einen spuerbaren Haenger.")]
    [SerializeField] private int maxQueuedSpawnsPerFrame = 25;

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

    /// <summary>Angesparte Bedrohung, die der Nachschub setzen darf.</summary>
    private float spawnBudget;

    /// <summary>Der naechste Nachschub-Gegner - bleibt stehen, bis das Budget fuer ihn reicht.</summary>
    private EnemyId pendingEnemy = EnemyId.None;

    private const float SpawnTick = 0.1f;
    private const float MaintenanceTick = 0.25f;

    /// <summary>Daempfer auf den Grunddruck, waehrend ein Ring oder der Boss laeuft.</summary>
    private float pressureScale = 1f;
    private float pressureScaleUntil;

    /// <summary>Der laufende Miniboss eines Boss-Beats - solange er lebt, bleibt der Nachschub gedaempft.</summary>
    private Enemy activeMiniBoss;

    /// <summary>Der Boss aus dem Boss-Beat. Faellt er, kommt der Finisher des Plans.</summary>
    private Enemy activeBoss;
    private bool finisherSpawned;

    private string announce;
    private float announceUntil;
    private string lastShownText;

    private Vector2 lastPlayerPos;
    private Vector2 playerDir;

    private bool timeLaserUnlocked;

    private readonly List<Vector2> points = new List<Vector2>(64);

    private struct QueuedSpawn
    {
        public EnemyId Id;
        public Vector2 Position;
        public bool Recyclable;
    }

    /// <summary>Gegner aus Bursts und Ringen, die noch auf ihren Frame warten.</summary>
    private readonly Queue<QueuedSpawn> spawnQueue = new Queue<QueuedSpawn>(256);

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

        plan = WavePlans.ForMap(planId);

        foreach (Phase p in plan.Phases) p.Beats.Sort((a, b) => a.Time.CompareTo(b.Time));
        if (plan.Endless != null) plan.Endless.Beats.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (PlayerController.Instance != null)
        {
            lastPlayerPos = PlayerController.Instance.transform.position;
        }

        EnterPhase(0);
        PrewarmDeathEffects();

        Debug.Log($"[SpawnDirector] Plan \"{plan.Id}\" mit {plan.Phases.Count} Phasen " +
                  $"({plan.TotalDuration:0}s) gestartet.");
    }

    /// <summary>
    /// Todeseffekte vorab anlegen (<see cref="RunPool"/>): der erste grosse
    /// Haufen, der auf einmal faellt, wuerde sie sonst alle im selben Frame
    /// neu erzeugen. Praktisch teilen sich alle Gegner denselben Effekt.
    /// </summary>
    private void PrewarmDeathEffects()
    {
        if (catalog == null) return;

        var effects = new HashSet<GameObject>();
        foreach (EnemyId id in System.Enum.GetValues(typeof(EnemyId)))
        {
            if (id == EnemyId.None) continue;

            GameObject prefab = catalog.Prefab(id);
            Enemy enemy = prefab != null ? prefab.GetComponent<Enemy>() : null;
            if (enemy != null && enemy.DeathEffect != null) effects.Add(enemy.DeathEffect);
        }

        foreach (GameObject effect in effects) RunPool.Prewarm(effect, 80, "Effekte");
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
        DrainSpawnQueue();
        CheckFinisher();

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
        pendingEnemy = EnemyId.None;
    }

    private void EnterPhase(int index)
    {
        if (index < 0 || index >= plan.Phases.Count) return;

        phaseIndex = index;
        phase = plan.Phases[index];
        phaseTime = 0f;
        nextBeat = 0;
        pendingEnemy = EnemyId.None;

        Debug.Log($"[SpawnDirector] Phase {index + 1}/{plan.Phases.Count} " +
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
                Announce(PlanText(beat.Announce));
                GameObject boss = SpawnBoss(beat.Enemy);

                // Ein Miniboss ist ein Kampf mitten im Level: der Nachschub
                // bleibt nur gedaempft, solange er lebt (siehe TopUpPressure).
                Enemy bossEnemy = boss != null ? boss.GetComponent<Enemy>() : null;
                if (bossEnemy != null && bossEnemy.Role == EnemyRole.MiniBoss) activeMiniBoss = bossEnemy;
                if (bossEnemy != null && bossEnemy.Role == EnemyRole.Boss) activeBoss = bossEnemy;
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
        Announce(string.IsNullOrEmpty(beat.Announce) ? Loc.Get("wave.announce.achtung", "ACHTUNG!")
                                                     : PlanText(beat.Announce));
        if (warningSound != null) warningSound.Play();

        DampenPressure(beat.PressureScale, beat.Duration + beat.WarnTime);

        yield return new WaitForSeconds(beat.WarnTime);

        SpawnContext ctx = Context();

        // Der Kaefig: dichte Blocker-Wand etwas ausserhalb des Rings. Sie
        // verschwindet, sobald der Gegner in der Mitte faellt (siehe Enemy:
        // alles auf dem Layer Enemy_barrier wird dann geraeumt) - ohne ihn
        // stuende sie fuer immer, deshalb nur zusammen mit einem.
        if (beat.Cage && beat.Enemy != EnemyId.None)
        {
            float cageRadius = beat.Radius * 1.35f;
            int cageCount = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * cageRadius / 0.9f), 40, 160);

            points.Clear();
            Patterns.Ring.Fill(points, ctx, cageCount, cageRadius);
            foreach (Vector2 point in points) Enqueue(EnemyId.Blocker, point, false);
        }

        // Elites kommen nur einzeln - als Ringfueller wuerde ein ganzer Kreis
        // davon stehen.
        if (beat.RingEnemy != EnemyId.None && beat.RingCount > 0 && EnemyCatalog.IsElite(beat.RingEnemy))
        {
            Debug.LogWarning($"[SpawnDirector] {beat.RingEnemy} ist ein Elite und taugt nicht als Ringgegner - Ring ohne Fueller.");
        }
        else if (beat.RingEnemy != EnemyId.None && beat.RingCount > 0)
        {
            points.Clear();
            Patterns.Ring.Fill(points, ctx, beat.RingCount, beat.Radius);
            foreach (Vector2 point in points) Enqueue(beat.RingEnemy, point, false);
        }

        if (beat.Enemy != EnemyId.None)
        {
            points.Clear();
            Patterns.Ring.Fill(points, ctx, 1, beat.Radius * 0.55f);
            if (points.Count > 0) Enqueue(beat.Enemy, points[0], false);
        }
    }

    /// <summary>
    /// Boss gefallen: im Story-Modus kommt jetzt der Finisher des Plans (z.B.
    /// der Todes-Ramen) und beendet den Lauf. Der Boss-Tod hat da schon
    /// <c>GameManager.bossSpawned</c> gesetzt - der Tod durch den Finisher
    /// zeigt deshalb den Sieg-Bildschirm. Im Endless-Modus geht es weiter.
    /// </summary>
    private void CheckFinisher()
    {
        if (finisherSpawned || plan.Finisher == EnemyId.None || GameSession.IsEndless) return;

        // Unity-Vergleich: ein zerstoerter Boss ist hier == null.
        if (ReferenceEquals(activeBoss, null) || activeBoss != null) return;

        finisherSpawned = true;
        activeBoss = null;

        points.Clear();
        Patterns.Ring.Fill(points, Context(), 1, 12f);
        if (points.Count > 0) Spawn(plan.Finisher, points[0], false);
    }

    private GameObject SpawnBoss(EnemyId id)
    {
        if (id == EnemyId.None) return null;

        // Der Keks-Koenig laeuft nicht selbst (siehe Enemy.FixedUpdate, BossBoss)
        // - er darf also nicht am aeussersten Rand stehen, sonst findet ihn
        // niemand.
        points.Clear();
        Patterns.Ring.Fill(points, Context(), 1, 10f);
        return points.Count > 0 ? Spawn(id, points[0], false) : null;
    }

    private void DampenPressure(float scale, float seconds)
    {
        pressureScale = Mathf.Clamp01(scale);
        pressureScaleUntil = runTime + Mathf.Max(0.5f, seconds);
    }

    // ------------------------------------------------------------- Nachschub

    /// <summary>
    /// Der laufende Nachschub. Frueher: bis zum Ziel auffuellen (80 Gegner pro
    /// Sekunde moeglich) und dann gar nichts mehr, bis wieder etwas stirbt.
    /// Kam der Spieler nicht hinterher - oder hatte ein Burst das Feld ueber
    /// das Ziel gehoben -, stand der Nachschub still, und man konnte in Ruhe
    /// Kreise um den Haufen laufen.
    ///
    /// Jetzt fliesst es: unter dem Ziel zuegig, aber verteilt ueber ein paar
    /// Sekunden; am Ziel und darueber in einem duennen, steten Strom bis zum
    /// <see cref="overflowCap"/>. Wer nicht raeumt, wird langsam ueberrannt.
    /// </summary>
    private void TopUpPressure()
    {
        if (phase == null) return;

        // Miniboss gefallen: die Daempfung seines Boss-Beats endet sofort.
        // (Unity-Vergleich: ein zerstoerter Gegner ist hier == null.)
        if (!ReferenceEquals(activeMiniBoss, null) && activeMiniBoss == null)
        {
            activeMiniBoss = null;
            pressureScaleUntil = runTime;
        }

        if (runTime >= pressureScaleUntil) pressureScale = 1f;

        float target = phase.PressureAt(phaseTime) * RunDifficulty.CountFactor * pressureScale;
        float current = CurrentThreat(out int aliveCount);

        float rate;
        if (current < target) rate = Mathf.Max(target * refillPerSecond, minTricklePerSecond);
        else if (current < target * overflowCap) rate = Mathf.Max(target * tricklePerSecond, minTricklePerSecond * pressureScale);
        else rate = 0f;

        if (rate <= 0f || aliveCount >= maxAlive)
        {
            spawnBudget = 0f;
            return;
        }

        if (pendingEnemy == EnemyId.None) pendingEnemy = phase.PickEnemy();
        float nextThreat = Mathf.Max(0.1f, SpawnCatalog.Threat(pendingEnemy));

        // Nicht endlos ansparen: hoechstens eine Sekunde Nachschub oder genau
        // der naechste schwere Gegner, sonst kommt nach einer Pause doch
        // wieder alles auf einmal.
        spawnBudget = Mathf.Min(spawnBudget + rate * SpawnTick, Mathf.Max(rate, nextThreat));

        int spawned = 0;
        while (spawnBudget >= nextThreat && spawned < maxSpawnsPerTick && aliveCount + spawned < maxAlive)
        {
            points.Clear();
            phase.BasePattern.Fill(points, Context(), 1, 0f);
            if (points.Count == 0) break;

            if (Spawn(pendingEnemy, points[0], true) == null)
            {
                pendingEnemy = EnemyId.None;
                break;
            }

            spawnBudget -= nextThreat;
            spawned++;

            pendingEnemy = phase.PickEnemy();
            nextThreat = Mathf.Max(0.1f, SpawnCatalog.Threat(pendingEnemy));
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
    ///
    /// Dazu kommen die Gegner im Stau (<see cref="Enemy.IsBlocked"/>): wer
    /// hinten am Haufen haengt und aus dem Bild gefallen ist, kommt von
    /// anderer Seite wieder.
    /// Einfach nur ausserhalb des Bildes zu sein reicht dafuer NICHT - wer
    /// freie Bahn hat, laeuft weiter auf den Spieler zu.
    /// Festgeklebte Gegner (Klebreis) bleiben liegen, bis <see cref="Enemy.RecycleHeld"/>
    /// ablaeuft - sonst waeren sie beim Weglaufen sofort wieder vorn.
    /// </summary>
    private void RecycleStragglers()
    {
        if (phase == null || PlayerController.Instance == null) return;

        Vector2 player = PlayerController.Instance.transform.position;
        float limitSqr = recycleDistance * recycleDistance;

        bool hasView = ViewBounds.TryGetWorldRect(out Rect view);
        Rect visible = new Rect(view.xMin - offScreenMargin, view.yMin - offScreenMargin,
                                view.width + offScreenMargin * 2f, view.height + offScreenMargin * 2f);

        SpawnContext ctx = Context();
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.IsBoss || !enemy.CanRecycle || enemy.RecycleHeld) continue;

            Vector2 pos = enemy.transform.position;
            bool tooFar = (pos - player).sqrMagnitude >= limitSqr;
            bool stuckOffScreen = hasView
                && enemy.BlockedFor >= blockedRecycleDelay
                && !visible.Contains(pos);
            if (!tooFar && !stuckOffScreen) continue;

            points.Clear();
            phase.BasePattern.Fill(points, ctx, 1, 0f);
            if (points.Count == 0) continue;

            enemy.transform.position = KeepOffScreen(points[0]);
            enemy.ResetBlocked();
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

    /// <summary>
    /// Kein Gegner taucht mitten im Bild auf. Das Spawn-Rechteck allein reicht
    /// dafuer nicht: Hinterhalt, Elite im Ring und Rudel setzen ihre Punkte
    /// naeher an den Spieler, und die Kamera laeuft dem Spieler hinterher.
    /// Liegt ein Punkt im Bild (plus Rand), wird er von der Bildmitte aus in
    /// derselben Richtung bis knapp hinter den Rand geschoben - die Absicht des
    /// Musters (vorn, links, im Ring) bleibt dabei erhalten.
    /// </summary>
    private Vector2 KeepOffScreen(Vector2 point)
    {
        if (!ViewBounds.TryGetWorldRect(out Rect view)) return point;

        Rect safe = new Rect(view.xMin - offScreenMargin, view.yMin - offScreenMargin,
                             view.width + offScreenMargin * 2f, view.height + offScreenMargin * 2f);
        if (!safe.Contains(point)) return point;

        Vector2 center = safe.center;
        Vector2 dir = point - center;
        if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle.normalized;

        float tx = Mathf.Abs(dir.x) > 0.0001f ? (safe.width * 0.5f) / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? (safe.height * 0.5f) / Mathf.Abs(dir.y) : float.MaxValue;

        return center + dir * Mathf.Min(tx, ty);
    }

    /// <summary>Setzt eine bestimmte Bedrohungsmenge auf einmal - fuer Bursts.</summary>
    private void SpawnThreat(EnemyId id, float threat, ISpawnPattern pattern, float radius, bool recyclable)
    {
        if (id == EnemyId.None || threat <= 0f) return;

        float each = Mathf.Max(0.1f, SpawnCatalog.Threat(id));
        int count = Mathf.Clamp(Mathf.RoundToInt(threat * RunDifficulty.CountFactor / each), 1, 120);

        // Elites kommen immer einzeln, egal wie viel Druck im Plan steht.
        if (EnemyCatalog.IsElite(id)) count = 1;

        points.Clear();
        pattern.Fill(points, Context(), count, radius);

        foreach (Vector2 point in points) Enqueue(id, point, recyclable);
    }

    /// <summary>
    /// Burst- und Ring-Gegner kommen nicht alle im selben Frame, sondern
    /// hoechstens <see cref="maxQueuedSpawnsPerFrame"/> pro Frame. Bei 25 pro
    /// Frame steht auch ein voller Kaefig nach gut einer Zehntelsekunde - mit
    /// blossem Auge nicht zu sehen, aber ohne den Haenger.
    /// </summary>
    private void Enqueue(EnemyId id, Vector2 position, bool recyclable)
    {
        spawnQueue.Enqueue(new QueuedSpawn { Id = id, Position = position, Recyclable = recyclable });
    }

    private void DrainSpawnQueue()
    {
        int budget = Mathf.Max(1, maxQueuedSpawnsPerFrame);
        while (spawnQueue.Count > 0 && budget-- > 0)
        {
            QueuedSpawn next = spawnQueue.Dequeue();
            Spawn(next.Id, next.Position, next.Recyclable);
        }
    }

    private GameObject Spawn(EnemyId id, Vector2 position, bool recyclable)
    {
        GameObject prefab = catalog.Prefab(id);
        if (prefab == null)
        {
            Debug.LogWarning($"[SpawnDirector] Kein Prefab fuer {id} im Katalog - uebersprungen.");
            return null;
        }

        position = KeepOffScreen(position);

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity);

        RunScene.Place(spawned, "Gegner");

        Enemy enemy = spawned.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.RunThreat = SpawnCatalog.Threat(id);
            enemy.CanRecycle = recyclable;
            enemy.SpawnedAs = id;
            RunDifficulty.Apply(enemy);
        }

        return spawned;
    }

    // -------------------------------------------------------------------- UI

    // Fuer das GameHud: dieselben Daten, die UpdateText ins alte Wave-Feld
    // schreibt. Phasen haben keine Namen mehr - es gibt nur noch Ansagen.

    /// <summary>Was gerade im Wellenfeld stuende: die laufende Ansage, sonst nichts.</summary>
    public string CurrentLabel => IsAnnouncing ? announce : "";

    public bool IsAnnouncing => runTime < announceUntil && !string.IsNullOrEmpty(announce);

    /// <summary>
    /// Text ins Wellenfeld schreiben, der nicht aus dem Plan kommt. Der
    /// Keks-Koenig meldet damit seinen Phasenwechsel - das gehoert in
    /// dieselbe Zeile wie "KEKS-KOENIG", nicht in ein zweites Feld daneben.
    /// </summary>
    public void Say(string text)
    {
        Announce(text);
    }

    /// <summary>
    /// Ansage aus dem Wellenplan uebersetzen: "KEKS-KOENIG" sucht unter
    /// wave.announce.kekskoenig (nur Buchstaben und Ziffern, klein). Fehlt der
    /// Eintrag, bleibt der Text aus dem Plan stehen.
    /// </summary>
    private static string PlanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var key = new System.Text.StringBuilder("wave.announce.");
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) key.Append(c);
        }
        return Loc.Get(key.ToString(), text);
    }

    private void Announce(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        announce = text;
        announceUntil = runTime + 3f;
    }

    private void UpdateText()
    {
        if (phaseText == null) return;

        string text = runTime < announceUntil ? announce : "";
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
