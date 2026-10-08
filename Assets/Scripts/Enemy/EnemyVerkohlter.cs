using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Verkohlte - ein im Ofen vergessener Keks. Bilder aus
/// Tools/verkohlter.py, die Prefabs baut Tools -> Gegner -> Verkohlter bauen
/// (VerkohlterBuilder). Ein Skript, zwei Gegner:
///
/// <b>Als Boss</b> (EnemyId.Verkohlter, Rolle Boss - vorerst nur in der Test-Szene)
///
///   Glutausbruch  bleibt stehen, die Kruste platzt auf, um ihn liegt die
///                 rote Zone. Voll = Explosion. Danach schnappt die Kruste
///                 zurueck - das Fenster zum Draufhauen.
///   Kruemelwurf   wirft verkohlte Brocken im Faecher. Wo sie landen, liegt
///                 vorher eine kleine Zone, danach brennt der Boden ein paar
///                 Sekunden (Glutfleck).
///   Aschesprung   zerfaellt zu Asche (dabei nicht zu treffen), setzt sich
///                 neben dem Spieler wieder zusammen und explodiert sofort mit
///                 kuerzerer Vorwarnung.
///   Phase 2       ab der Haelfte: Schattenteilung. Er zerfaellt und steht
///                 dreimal um den Spieler herum - einmal echt (Feuer), zweimal
///                 als Schatten (violett, nicht zu treffen). Alle drei
///                 explodieren gleichzeitig, die Schatten zerfallen danach.
///   Phase 3       bei 15 % Leben: er holt Luft und saugt den Spieler ein -
///                 nicht auszuweichen, eine Zwischensequenz (Gegner in der
///                 Naehe fliegen mit in den Schlund). Iris zu, und der Spieler
///                 faellt in die Herzkammer (<see cref="VerkohlterHerzkammer"/>).
///                 Dort ist der Boss nur noch sein schlagendes Herz in der
///                 Mitte - mit eigener, voller Lebensleiste (35 % seines
///                 Lebens, fuellt sich bei der Landung auf). Bis dahin kann er
///                 nicht sterben (<see cref="Enemy.MinHealthFraction"/>).
///                 Das Herz greift noch nicht an - das kommt spaeter.
///   Herz-Tod      der toedliche Treffer wird angehalten (Enemy.HoldDeath):
///                 das Herz ueberhitzt, ein Blitz, die Kruste bricht, Brocken
///                 fallen ins Becken, der Kern verglueht; die Kammer wird
///                 dunkel. Erst dann stirbt der Boss (Beute, Boss-Sieg) - im
///                 Lauf folgt der Sieg-Bildschirm, in der Test-Szene der Rueckweg.
///
/// <b>Als Tod</b> (EnemyId.VerkohlterTod, Rolle DeathBoss - Finisher der Demo)
///
///   Ansage "DER TOD WIRD DICH HOLEN!", dann setzt er sich IM BILD aus Asche
///   zusammen, laeuft ein paar Meter auf den Spieler zu und zuendet den
///   Glutausbruch. Die Zone waechst jeden Frame mit, bis sie den Spieler
///   umschliesst - egal wie schnell er ist. Beim Einschlag stirbt der Spieler
///   immer (<see cref="PlayerController.Execute"/>); weil der Boss schon liegt,
///   kommt der Sieg-Bildschirm. Er selbst ist die ganze Zeit nicht zu treffen.
///
/// Animation: kein Animator, er spielt seine Streifen selbst (wie Glutwurz und
/// Schleimkoenig) - die Laengen haengen an den Angriffszeiten. Die Zahlen
/// stehen als Konstanten im Code, damit ein Prefab-Neubau das Balancing nicht
/// mitnimmt.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemyVerkohlter : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float PhaseTwoAt = 0.5f;
    private const float PhaseThreeAt = 0.15f;

    // --- Phase 3: Einsaugen
    /// <summary>So lange zieht der Sog, bis der Spieler im Schlund ist.</summary>
    private const float SuckTime = 2.1f;
    /// <summary>Gegner in diesem Umkreis fliegen mit in den Schlund.</summary>
    private const float SuckEnemyRange = 14f;
    private const float SogFps = 16f;
    /// <summary>Ab diesem Bild des Schluckens zieht sich die Blende zu.</summary>
    private const int IrisCloseFrame = 3;
    private const float IrisCloseTime = 0.55f;
    private const float BlackHold = 0.4f;
    private const float IrisOpenTime = 0.9f;
    private const float FallHeight = 7.5f;
    private const float FallTime = 0.55f;
    private const float HeartHitRadius = 1.15f;

    /// <summary>Das Herz hat so viel Leben wie dieser Anteil des ganzen Verkohlten.</summary>
    private const float HeartHealthShare = 0.35f;
    /// <summary>So lange fuellt sich die Lebensleiste des Herzens bei der Landung.</summary>
    private const float HeartBarFill = 1.1f;

    // --- Herz-Tod (Bilder aus verkohlter_herz_tod)
    private const int HeartOverheatFrames = 10;
    private const int HeartFlashFrame = 10;
    private static readonly Color FlashColor = new Color(1f, 0.95f, 0.82f, 1f);

    private const float WalkSpeed = 1.9f;
    private const float WalkSpeedPhase2 = 2.4f;
    private const float WalkMin = 2.2f, WalkMax = 3.2f;
    private const float WalkMinPhase2 = 1.5f, WalkMaxPhase2 = 2.3f;
    private const float EntryWalk = 1.5f;

    /// <summary>Naeher laeuft er nicht ran - er soll den Spieler nicht wegschieben.</summary>
    private const float StopDistance = 1.3f;

    // --- Glutausbruch
    private const float BurstCharge = 1.1f;
    private const float BurstChargePhase2 = 0.9f;
    private const float BurstChargeAfterJump = 0.8f;
    private const float BurstRadius = 2.6f;
    private const float BurstDamageFactor = 2.5f;

    // --- Kruemelwurf
    private const int CrumbCount = 5, CrumbCountPhase2 = 7;
    private const float CrumbSpread = 75f;        // Grad, ganzer Faecher
    private const float CrumbMinRange = 1.6f, CrumbMaxRange = 4.2f;
    private const float CrumbFlight = 0.65f;
    private const float CrumbStagger = 0.07f;
    private const float CrumbArc = 1.4f;          // Wurfhoehe in Einheiten
    private const float CrumbRadius = 0.7f;
    private const float CrumbDamageFactor = 0.8f;
    private const float PatchTime = 3f;
    private const float PatchTick = 0.5f;
    private const float PatchRadius = 0.6f;
    private const float PatchDamageFactor = 0.3f;

    // --- Aschesprung
    private const float JumpDistance = 2.4f;
    private const float JumpZoneRadius = 1.4f;
    private const float JumpDamageFactor = 1.2f;

    // --- Schattenteilung
    private const float SplitDistance = 3f;
    private const float SplitBurstRadius = 2.2f;

    // --- Als Tod
    private const float DeathWarn = 1.8f;
    private const float DeathIdle = 0.4f;
    private const float DeathWalk = 1.4f;
    private const float DeathWalkStop = 1.6f;
    private const float DeathCharge = 1.8f;
    /// <summary>
    /// Die Zone des Todes deckt das ganze Bild ab (10 x 5.6 Einheiten, halbe
    /// Diagonale gut 5.7) - ihr Rand liegt weit draussen, man sieht ihn gar
    /// nicht erst. Laeuft der Spieler trotzdem Richtung Rand, waechst sie mit
    /// und haelt immer diesen Abstand zu ihm.
    /// </summary>
    private const float DeathRadiusMin = 14f;
    private const float DeathRadiusMargin = 6f;
    private const float DeathSpawnDistance = 4f;

    private const float PlayerRadius = 0.3f;

    // ------------------------------------------------- Werte aus dem Bildskript

    private const float Fps = 12f;

    /// <summary>Bilder 0-9 laden auf, Bild 10 ist der Blitz, danach Nachglühen.</summary>
    private const int BurstImpactFrame = 10;

    /// <summary>Ab diesem Bild des Zerfalls ist er weg (nicht zu treffen).</summary>
    private const int AshGoneFrame = 6;

    /// <summary>Ab diesem Bild des Zusammensetzens ist er wieder da.</summary>
    private const int AshBackFrame = 11;

    /// <summary>Wo der Ausbruch am Boden sitzt (ueber dem Pivot = Fuesse).</summary>
    private static readonly Vector2 GroundCenter = new Vector2(0f, 0.3f);

    /// <summary>Wurfhand/Koerpermitte ueber dem Pivot.</summary>
    private static readonly Vector2 BodyCenter = new Vector2(0f, 1.0f);

    // ------------------------------------------------------------- Bausteine

    [Header("Bilder (setzt VerkohlterBuilder)")]
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private Sprite[] idle;
    [SerializeField] private Sprite[] walk;
    [SerializeField] private Sprite[] burst;
    [SerializeField] private Sprite[] ashOut;
    [SerializeField] private Sprite[] ashIn;
    [SerializeField] private Sprite[] shadowIdle;
    [SerializeField] private Sprite[] shadowBurst;
    [SerializeField] private Sprite[] shadowAshOut;
    [SerializeField] private Sprite[] shadowAshIn;
    [SerializeField] private Sprite[] crumb;
    [SerializeField] private Sprite[] patch;

    // ---------------------------------------------------------------- Zustand

    private Enemy enemy;
    private Rigidbody2D rb;

    private Vector2 velocity;
    private Sprite[] loop;
    private float loopTime;
    private bool phaseTwo;

    private readonly List<BossTelegraphMarker> markers = new List<BossTelegraphMarker>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary>Der echte Verkohlte oder einer seiner Schatten.</summary>
    private class Actor
    {
        public Transform Root;
        public SpriteRenderer Renderer;
        public bool Shadow;
        public Vector2 Position => Root.position;
    }

    private Actor self;

    // --- Phase 3
    private bool entered;
    private bool phaseThree;
    private bool heartMode;
    private bool heartDying;
    private VerkohlterHerzkammer chamber;
    private PlayerPuppet puppet;
    private ScreenIris iris;
    private SpriteRenderer sog;
    private Sprite[] heartFrames;
    private readonly List<Enemy> swallowed = new List<Enemy>();

    public bool IsPhaseTwo => enemy != null && enemy.HealthFraction <= PhaseTwoAt;

    /// <summary>Phase 3 laeuft (Einsaugen oder schon im Herzen).</summary>
    public bool IsPhaseThree => phaseThree;

    private bool PhaseThreeDue => !IsDeath && enemy != null && enemy.HealthFraction <= PhaseThreeAt;

    /// <summary>Der Finisher der Demo statt des Bosskampfs.</summary>
    private bool IsDeath => enemy != null && enemy.Id == EnemyId.VerkohlterTod;

    // ---------------------------------------------------------------- Ablauf

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;

        // Haengt die Gegner-Werkstatt einen Animator dran: der wuerde die
        // Bilder ueberschreiben.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        if (body != null)
        {
            Animator bodyAnimator = body.GetComponent<Animator>();
            if (bodyAnimator != null) bodyAnimator.enabled = false;
        }

        self = new Actor { Root = transform, Renderer = body, Shadow = false };

        // Der Tod wird irgendwo am Rand gesetzt - bis er im Bild auftaucht,
        // ist er weder zu sehen noch zu treffen.
        if (IsDeath)
        {
            SetGone(true);
            if (body != null) body.enabled = false;
        }
        else
        {
            // Bis er den Spieler eingesaugt hat, stirbt er nicht.
            enemy.MinHealthFraction = PhaseThreeAt;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(IsDeath ? RunDeath() : RunBoss());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearAttacks();
        velocity = Vector2.zero;
        AbortSwallow();
    }

    /// <summary>Laufende Attacken weg: Warnungen, Brocken, Glutflecken, Schatten.</summary>
    private void ClearAttacks()
    {
        foreach (BossTelegraphMarker m in markers) if (m != null) m.Cancel();
        markers.Clear();
        foreach (GameObject go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }

    private void FixedUpdate()
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = velocity;
    }

    private void Update()
    {
        // Phase 3 bricht alles ab, auch mitten in einer Attacke.
        if (!phaseThree && entered && PhaseThreeDue && PlayerAlive)
        {
            phaseThree = true;
            StopAllCoroutines();
            StartCoroutine(PhaseThree());
            return;
        }

        if (heartMode)
        {
            AnimateHeart();
            return;
        }

        if (loop == null || loop.Length == 0 || body == null) return;
        loopTime += Time.deltaTime;
        body.sprite = loop[Mathf.FloorToInt(loopTime * Fps) % loop.Length];
    }

    // ============================================================== Bosskampf

    private IEnumerator RunBoss()
    {
        yield return null;
        yield return Walk(EntryWalk);
        entered = true;

        int step = 0;
        while (true)
        {
            if (!PlayerAlive)
            {
                Loop(idle);
                velocity = Vector2.zero;
                yield return null;
                continue;
            }

            // Phase 3 faengt Update ab - bis dahin hier nur warten.
            if (PhaseThreeDue)
            {
                velocity = Vector2.zero;
                Loop(idle);
                yield return null;
                continue;
            }

            if (!phaseTwo && IsPhaseTwo)
            {
                phaseTwo = true;
                if (SpawnDirector.Active != null)
                    SpawnDirector.Active.Say(Loc.Get("boss.verkohlter.phase2", "DER VERKOHLTE SPALTET SICH!"));
                yield return Split();
                step = 0;
                continue;
            }

            yield return Walk(phaseTwo ? Random.Range(WalkMinPhase2, WalkMaxPhase2)
                                       : Random.Range(WalkMin, WalkMax));
            if (!PlayerAlive) continue;

            if (!phaseTwo)
            {
                switch (step % 3)
                {
                    case 0: yield return BurstAll(Single(), BurstCharge, BurstRadius); break;
                    case 1: yield return CrumbThrow(CrumbCount); break;
                    default: yield return AshJump(); break;
                }
            }
            else
            {
                switch (step % 4)
                {
                    case 0: yield return CrumbThrow(CrumbCountPhase2); break;
                    case 1: yield return AshJump(); break;
                    case 2: yield return Split(); break;
                    default: yield return BurstAll(Single(), BurstChargePhase2, BurstRadius); break;
                }
            }
            step++;
        }
    }

    private IEnumerator Walk(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            if (!PlayerAlive)
            {
                velocity = Vector2.zero;
                Loop(idle);
                yield return null;
                continue;
            }

            Vector2 to = PlayerPos - rb.position;
            if (to.magnitude <= StopDistance)
            {
                velocity = Vector2.zero;
                Loop(idle);
            }
            else
            {
                float speed = (phaseTwo ? WalkSpeedPhase2 : WalkSpeed) * SlowFactor;
                velocity = to.normalized * speed;
                Loop(walk);
            }
            yield return null;
        }
        velocity = Vector2.zero;
    }

    /// <summary>Tempo aus dem Katalog relativ zum Grundtempo - Slows wirken also mit.</summary>
    private float SlowFactor
    {
        get
        {
            EnemyDef def = EnemyCatalog.Get(enemy.Id);
            return def != null && def.Speed > 0f ? Mathf.Clamp01(enemy.CurrentSpeed / def.Speed) : 1f;
        }
    }

    // ---------------------------------------------------------- Glutausbruch

    /// <summary>
    /// Alle Darsteller laden gleichzeitig auf und explodieren gleichzeitig -
    /// allein (Glutausbruch) oder mit seinen Schatten (Schattenteilung).
    /// </summary>
    private IEnumerator BurstAll(List<Actor> actors, float charge, float radius)
    {
        velocity = Vector2.zero;
        Loop(null);

        var zones = new List<BossTelegraphMarker>(actors.Count);
        foreach (Actor a in actors) zones.Add(Track(BossTelegraph.Zone(a.Position + GroundCenter, radius)));

        float t = 0f;
        while (t < charge)
        {
            t += Time.deltaTime;
            int frame = Mathf.Min(BurstImpactFrame - 1, Mathf.FloorToInt(t / charge * BurstImpactFrame));
            foreach (Actor a in actors) Show(a, a.Shadow ? shadowBurst : burst, frame);
            foreach (BossTelegraphMarker z in zones) if (z != null) z.SetProgress(t / charge);
            yield return null;
        }

        bool hit = false;
        for (int i = 0; i < actors.Count; i++)
        {
            Show(actors[i], actors[i].Shadow ? shadowBurst : burst, BurstImpactFrame);
            if (zones[i] != null) zones[i].Impact();
            Untrack(zones[i]);
            if (!hit && Inside(actors[i].Position + GroundCenter, radius))
            {
                hit = true;   // mehrere Zonen treffen nur einmal
                Hit(BurstDamageFactor);
            }
        }
        Boom(5f, 0.4f);

        yield return PlayRest(actors, BurstImpactFrame + 1);
    }

    private IEnumerator PlayRest(List<Actor> actors, int from)
    {
        int length = burst != null ? burst.Length : 0;
        for (int i = from; i < length; i++)
        {
            yield return new WaitForSeconds(1f / Fps);
            foreach (Actor a in actors) Show(a, a.Shadow ? shadowBurst : burst, i);
        }
        yield return new WaitForSeconds(1f / Fps);
    }

    // ------------------------------------------------------------ Kruemelwurf

    private IEnumerator CrumbThrow(int count)
    {
        velocity = Vector2.zero;
        Loop(null);

        // Ausholen: die ersten Bilder des Ausbruchs (die Glut zieht an)
        for (int i = 0; i < 5; i++)
        {
            Show(self, burst, i);
            yield return new WaitForSeconds(1f / Fps);
        }

        Vector2 origin = rb.position;
        Vector2 aim = PlayerPos - origin;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.down;

        for (int k = 0; k < count; k++)
        {
            float angle = Mathf.Lerp(-CrumbSpread * 0.5f, CrumbSpread * 0.5f, count > 1 ? k / (count - 1f) : 0.5f)
                          + Random.Range(-6f, 6f);
            float range = Random.Range(CrumbMinRange, CrumbMaxRange);
            Vector2 target = origin + (Vector2)(Quaternion.Euler(0f, 0f, angle) * aim) * range;
            StartCoroutine(Crumb(origin + BodyCenter, target, k * CrumbStagger));
        }

        // Zurueck in die Ruhe
        for (int i = 4; i >= 0; i--)
        {
            Show(self, burst, i);
            yield return new WaitForSeconds(1f / Fps);
        }
        Loop(idle);
        yield return new WaitForSeconds(CrumbFlight + count * CrumbStagger);
    }

    private IEnumerator Crumb(Vector2 from, Vector2 to, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        BossTelegraphMarker zone = Track(BossTelegraph.Zone(to, CrumbRadius));
        SpriteRenderer sr = NewSprite("Kohlebrocken", 5);
        sr.transform.position = from;

        float t = 0f;
        while (t < CrumbFlight)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / CrumbFlight);
            Vector2 p = Vector2.Lerp(from, to, k) + Vector2.up * (4f * k * (1f - k) * CrumbArc);
            sr.transform.position = Snap(p);
            if (crumb != null && crumb.Length > 0) sr.sprite = crumb[Mathf.FloorToInt(t * Fps * 1.5f) % crumb.Length];
            if (zone != null) zone.SetProgress(k);
            yield return null;
        }

        if (zone != null) zone.Impact();
        Untrack(zone);
        if (Inside(to, CrumbRadius)) Hit(CrumbDamageFactor);
        Despawn(sr.gameObject);

        // Der Boden brennt
        SpriteRenderer fire = NewSprite("Glutfleck", -1);
        fire.transform.position = Snap(to);
        float life = 0f, tick = 0f;
        while (life < PatchTime)
        {
            life += Time.deltaTime;
            tick -= Time.deltaTime;
            if (patch != null && patch.Length > 0) fire.sprite = patch[Mathf.FloorToInt(life * Fps) % patch.Length];

            Color c = fire.color;
            c.a = Mathf.Clamp01((PatchTime - life) / 0.3f);
            fire.color = c;

            if (tick <= 0f && Inside(to, PatchRadius))
            {
                tick = PatchTick;
                Hit(PatchDamageFactor);
            }
            yield return null;
        }
        Despawn(fire.gameObject);
    }

    // ------------------------------------------------------------- Aschesprung

    private IEnumerator AshJump()
    {
        yield return AshOut(Single());

        Vector2 target = PlayerAlive
            ? PlayerPos + Random.insideUnitCircle.normalized * JumpDistance
            : rb.position;
        Teleport(target);

        BossTelegraphMarker zone = Track(BossTelegraph.Zone(target + GroundCenter, JumpZoneRadius));
        yield return AshIn(Single(), zone);
        if (zone != null) zone.Impact();
        Untrack(zone);
        if (Inside(target + GroundCenter, JumpZoneRadius)) Hit(JumpDamageFactor);
        Boom(3f, 0.25f);

        yield return BurstAll(Single(), BurstChargeAfterJump, BurstRadius);
    }

    private IEnumerator AshOut(List<Actor> actors)
    {
        velocity = Vector2.zero;
        Loop(null);
        int length = ashOut != null ? ashOut.Length : 0;
        for (int i = 0; i < length; i++)
        {
            if (i == AshGoneFrame && actors.Contains(self)) SetGone(true);
            foreach (Actor a in actors) Show(a, a.Shadow ? shadowAshOut : ashOut, i);
            yield return new WaitForSeconds(1f / Fps);
        }
        if (actors.Contains(self)) SetGone(true);
    }

    private IEnumerator AshIn(List<Actor> actors, BossTelegraphMarker zone = null)
    {
        Loop(null);
        int length = ashIn != null ? ashIn.Length : 0;
        for (int i = 0; i < length; i++)
        {
            if (i == AshBackFrame && actors.Contains(self)) SetGone(false);
            foreach (Actor a in actors)
            {
                a.Renderer.enabled = true;
                Show(a, a.Shadow ? shadowAshIn : ashIn, i);
            }
            if (zone != null) zone.SetProgress((i + 1f) / length);
            yield return new WaitForSeconds(1f / Fps);
        }
        if (actors.Contains(self)) SetGone(false);
    }

    // -------------------------------------------------------- Schattenteilung

    private IEnumerator Split()
    {
        yield return AshOut(Single());

        Vector2 center = PlayerAlive ? PlayerPos : rb.position;
        float baseAngle = Random.Range(0f, 360f);
        int real = Random.Range(0, 3);

        var actors = new List<Actor>(3);
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = center + (Vector2)(Quaternion.Euler(0f, 0f, baseAngle + i * 120f) * Vector2.right) * SplitDistance;
            if (i == real)
            {
                Teleport(at);
                actors.Add(self);
            }
            else
            {
                actors.Add(NewShadow(at));
            }
        }

        yield return AshIn(actors);
        yield return BurstAll(actors, phaseTwo ? BurstChargePhase2 : BurstCharge, SplitBurstRadius);

        // Die Schatten zerfallen, er bleibt stehen
        var shadows = actors.FindAll(a => a.Shadow);
        Loop(idle);
        int length = shadowAshOut != null ? shadowAshOut.Length : 0;
        for (int i = 0; i < length; i++)
        {
            foreach (Actor a in shadows) Show(a, shadowAshOut, i);
            yield return new WaitForSeconds(1f / Fps);
        }
        foreach (Actor a in shadows) Despawn(a.Root.gameObject);
    }

    private Actor NewShadow(Vector2 at)
    {
        SpriteRenderer sr = NewSprite("Schatten", body != null ? body.sortingOrder : 1);
        sr.transform.position = Snap(at);
        sr.enabled = false;
        if (shadowIdle != null && shadowIdle.Length > 0) sr.sprite = shadowIdle[0];
        return new Actor { Root = sr.transform, Renderer = sr, Shadow = true };
    }

    // =============================================================== Phase 3

    /// <summary>
    /// Einsaugen und ab ins Herz. Der Spieler ist von Anfang an gefangen
    /// (<see cref="PlayerPuppet"/>) - das ist kein Angriff, dem man
    /// ausweichen kann, sondern der Uebergang in die letzte Phase.
    ///
    ///   0.0 s   Luft holen (Bilder 0-7), Boden grollt, Sog setzt ein
    ///   0.7 s   Sog (8-15 Schleife): der Spieler wirbelt in den Schlund,
    ///           Gegner in der Naehe fliegen mit
    ///   2.8 s   Schlucken (16-23): Maul zu, Schluck rutscht runter,
    ///           die Blende zieht sich um ihn zu
    ///   schwarz Kammer aufbauen, Boss wird zum Herzen, Kamera springt
    ///   +0.4 s  Blende geht ueber dem Herzen auf, der Spieler faellt von
    ///           oben in die Kammer und landet vor dem Becken
    ///   danach  Titel, Spieler frei, das Herz ist zu treffen
    /// </summary>
    private IEnumerator PhaseThree()
    {
        ClearAttacks();
        velocity = Vector2.zero;
        SetGone(true);
        if (body != null) body.enabled = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        Loop(null);

        // Waehrend er einsaugt, kommt nichts nach.
        if (SpawnDirector.Active != null)
        {
            SpawnDirector.Active.Suspended = true;
            SpawnDirector.Active.Say(Loc.Get("boss.verkohlter.phase3", "DER VERKOHLTE HOLT TIEF LUFT ..."));
        }

        puppet = new PlayerPuppet(PlayerController.Instance);
        puppet.UsePuppet(true);
        Vector2 startFeet = puppet.Position;
        puppet.Pose(startFeet, 1f, 0f, Vector2.one);

        Sprite[] inhale = VerkohlterArt.Strip("verkohlter_einsaugen");
        Sprite[] sogFrames = VerkohlterArt.Strip("verkohlter_sog");
        Vector2 maw = rb.position + VerkohlterArt.MawOffset;

        // --- 1. Luft holen
        ScreenShake.Kick(2f, 0.9f);
        VerkohlterSounds.Suck();
        for (int i = 0; i < VerkohlterArt.InhaleFrames; i++)
        {
            Show(self, inhale, i);
            yield return new WaitForSeconds(1f / Fps);
        }

        // --- 2. Sog
        sog = NewSprite("Sog", (body != null ? body.sortingOrder : 1) + 6);
        sog.transform.position = VerkohlterArt.Snap(maw);

        float side = startFeet.x < maw.x ? -1f : 1f;      // in welche Richtung er wirbelt
        Vector2 startCenter = startFeet + Vector2.up * 0.5f;
        Vector2 rel = startCenter - maw;
        float r0 = Mathf.Max(0.6f, rel.magnitude);
        float a0 = Mathf.Atan2(rel.y / 0.75f, rel.x);
        CollectSwallowed(maw);

        float t = 0f;
        while (t < SuckTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / SuckTime);

            Show(self, inhale, VerkohlterArt.SuckFirst + Mathf.FloorToInt(t * Fps) % VerkohlterArt.SuckFrames);
            if (sogFrames.Length > 0) sog.sprite = sogFrames[Mathf.FloorToInt(t * SogFps) % sogFrames.Length];
            sog.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.35f));
            ScreenShake.Kick(1f + 2f * u, 0.15f);

            // Die Puppe wirbelt spiralig rein, wird klein und dreht sich immer schneller.
            float e = Mathf.Pow(u, 2.3f);
            float r = r0 * (1f - e);
            float a = a0 + side * u * u * Mathf.PI * 2.2f;
            Vector2 center = maw + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
            float scale = Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(r / 1.8f));
            float spin = -side * Mathf.Pow(u, 2.5f) * 900f;
            puppet.Pose(center - Vector2.up * (0.5f * scale), scale, spin, Vector2.one);

            // Der echte Spieler (und damit die Kamera) gleitet ruhig zum Schlund -
            // eine wirbelnde Kamera waere zu viel.
            float s = u * u * (3f - 2f * u);
            puppet.MovePlayer(Vector2.Lerp(startFeet, maw - Vector2.up * 0.5f, s));

            PullSwallowed(maw, u, Time.deltaTime);
            yield return null;
        }

        // --- 3. Schlucken
        puppet.Pose(maw, 0f, 0f, Vector2.one);
        EatRemaining();
        Despawn(sog.gameObject);
        sog = null;
        VerkohlterSounds.StopAll();
        VerkohlterSounds.Gulp();
        Boom(6f, 0.5f);

        iris = ScreenIris.Create();
        Vector2 bodyCenter = rb.position + BodyCenter;
        for (int i = 0; i < VerkohlterArt.GulpFrames; i++)
        {
            Show(self, inhale, VerkohlterArt.GulpFirst + i);
            if (i >= IrisCloseFrame) break;
            yield return new WaitForSeconds(1f / Fps);
        }
        // Rest des Schluckens laeuft weiter, waehrend die Blende zugeht
        StartCoroutine(PlayRange(inhale, VerkohlterArt.GulpFirst + IrisCloseFrame + 1,
                                 VerkohlterArt.GulpFirst + VerkohlterArt.GulpFrames - 1));
        yield return VerkohlterHerzkammer.IrisMove(iris, () => VerkohlterHerzkammer.ViewportOf(bodyCenter),
                                                   0.7f, 0f, IrisCloseTime, true);
        iris.Close();

        // --- 4. Schwarz: rein ins Herz
        Vector2 returnPoint = rb.position;
        chamber = VerkohlterHerzkammer.Open(enemy, returnPoint);
        BecomeHeart();

        // Eigener Lebenspool fuer das Herz - die Leiste startet leer und
        // fuellt sich, wenn die Blende aufgeht.
        float heartPool = enemy.MaxHealth * HeartHealthShare;
        enemy.MinHealthFraction = 0f;
        enemy.ResetHealthPool(heartPool, 0f);

        Vector2 landing = chamber.LandingSpot;
        puppet.MovePlayer(landing, carryCompanions: true);
        chamber.FollowPlayer();
        Vector2 fallFrom = landing + Vector2.up * FallHeight;
        puppet.Pose(fallFrom, 1f, 0f, Vector2.one);

        yield return new WaitForSeconds(BlackHold);

        // --- 5. Blende auf, der Spieler faellt rein
        // Die Kamera springt erst im naechsten LateUpdate in die Kammer - die
        // Blende rechnet deshalb mit dem Kamerapunkt der Kammer.
        Vector2 heartView = chamber.Viewport(chamber.HeartPosition);
        StartCoroutine(VerkohlterHerzkammer.IrisMove(iris, () => heartView, 0f, 1.3f, IrisOpenTime, false));
        StartCoroutine(FillHeartBar(heartPool));
        yield return new WaitForSeconds(0.15f);

        t = 0f;
        while (t < FallTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / FallTime);
            Vector2 feet = Vector2.Lerp(fallFrom, landing, u * u);
            float stretch = 1f + 0.25f * u;
            puppet.Pose(feet, 1f, Mathf.Sin(u * Mathf.PI) * 25f, new Vector2(1f / stretch, stretch));
            yield return null;
        }

        // Landung: stauchen, Staub, Wumms
        Boom(4f, 0.3f);
        chamber.Puff(landing);
        float[] squashY = { 0.6f, 0.75f, 1.12f, 0.96f, 1f };
        foreach (float sy in squashY)
        {
            puppet.Pose(landing, 1f, 0f, new Vector2(1f / sy, sy));
            yield return new WaitForSeconds(0.06f);
        }
        puppet.Pose(landing, 1f, 0f, Vector2.one);

        if (SpawnDirector.Active != null)
            SpawnDirector.Active.Say(Loc.Get("boss.verkohlter.heart", "DAS HERZ DES VERKOHLTEN"));

        yield return new WaitForSeconds(0.35f);

        // --- 6. Los geht's
        puppet.Release();
        puppet = null;
        if (iris != null) Destroy(iris.gameObject);
        iris = null;

        enemy.ResetHealthPool(heartPool, 1f);
        enemy.HoldDeath = true;
        enemy.SkipDeathEffect = true;
        enemy.DeathHeld += OnHeartKilled;
        SetGone(false);
    }

    /// <summary>Die Leiste des Herzens laeuft von leer auf voll (es ist dabei noch nicht zu treffen).</summary>
    private IEnumerator FillHeartBar(float pool)
    {
        float t = 0f;
        while (t < HeartBarFill)
        {
            t += Time.deltaTime;
            float q = Mathf.Clamp01(t / HeartBarFill);
            enemy.ResetHealthPool(pool, 1f - (1f - q) * (1f - q));
            yield return null;
        }
        enemy.ResetHealthPool(pool, 1f);
    }

    // ------------------------------------------------------------- Herz-Tod

    private void OnHeartKilled()
    {
        if (heartDying) return;
        StopAllCoroutines();
        StartCoroutine(HeartDeath());
    }

    /// <summary>
    /// Das Herz zerfaellt (verkohlter_herz_tod, 37 Bilder): 0-9 ueberhitzen mit
    /// immer schnellerem Pochen, 10 Blitz + Bersten, dann fliegt die Kruste
    /// auseinander. Das letzte Bild (Brocken am Boden) bleibt in der Kammer
    /// liegen, dann stirbt der Boss wirklich.
    /// </summary>
    private IEnumerator HeartDeath()
    {
        heartMode = false;
        heartDying = true;
        SetGone(true);
        velocity = Vector2.zero;
        if (chamber != null) chamber.HeartDying();

        Sprite[] death = VerkohlterArt.Strip("verkohlter_herz_tod");
        if (body != null) body.transform.localPosition = Vector3.zero;
        iris = ScreenIris.Create();

        for (int i = 0; i < death.Length; i++)
        {
            if (body != null) body.sprite = death[i];

            if (i < HeartOverheatFrames)
            {
                // immer schnelleres Pochen, das Bild zittert mit
                if (i == 0 || i == 3 || i == 5 || i == 7 || i == 8 || i == 9) VerkohlterSounds.Lub();
                ScreenShake.Kick(1f + i * 0.35f, 0.12f);
            }
            else if (i == HeartFlashFrame)
            {
                VerkohlterSounds.Crack();
                Boom(8f, 0.8f);
                iris.Flash(FlashColor, 0.7f);
            }
            else if (i <= HeartFlashFrame + 4)
            {
                iris.Flash(FlashColor, 0.7f * (1f - (i - HeartFlashFrame) / 4f));
            }
            yield return new WaitForSeconds(1f / Fps);
        }

        if (iris != null) Destroy(iris.gameObject);
        iris = null;

        // Die Brocken bleiben liegen, der Boss geht.
        if (chamber != null && death.Length > 0 && body != null)
            chamber.LeaveRemains(death[death.Length - 1], body.transform.position);
        heartDying = false;
        enemy.FinishHeldDeath();
    }

    private IEnumerator PlayRange(Sprite[] strip, int from, int to)
    {
        for (int i = from; i <= to; i++)
        {
            Show(self, strip, i);
            yield return new WaitForSeconds(1f / Fps);
        }
    }

    /// <summary>Gegner, die mit in den Schlund fliegen (alle im Umkreis, ausser ihm selbst).</summary>
    private void CollectSwallowed(Vector2 maw)
    {
        swallowed.Clear();
        var list = new List<Enemy>(Enemy.Alive);
        foreach (Enemy e in list)
        {
            if (e == null || e == enemy) continue;
            if (Vector2.Distance(e.transform.position, maw) > SuckEnemyRange) continue;
            Rigidbody2D erb = e.GetComponent<Rigidbody2D>();
            if (erb != null) erb.simulated = false;
            swallowed.Add(e);
        }
    }

    private void PullSwallowed(Vector2 maw, float u, float dt)
    {
        float speed = 1.2f + 17f * u * u;
        for (int i = swallowed.Count - 1; i >= 0; i--)
        {
            Enemy e = swallowed[i];
            if (e == null)
            {
                swallowed.RemoveAt(i);
                continue;
            }
            Vector2 p = e.transform.position;
            Vector2 to = maw - p;
            float d = to.magnitude;
            if (d <= speed * dt + 0.1f)
            {
                swallowed.RemoveAt(i);
                Destroy(e.gameObject);      // gefressen - keine Beute
                continue;
            }
            // leicht spiralig, damit es nach Sog aussieht und nicht nach Magnet
            Vector2 dir = to / d;
            Vector2 swirl = new Vector2(-dir.y, dir.x) * 0.45f;
            p += (dir + swirl).normalized * speed * dt;
            e.transform.position = new Vector3(p.x, p.y, e.transform.position.z);
            float shrink = Mathf.Clamp(d / 1.6f, 0.15f, 1f);
            e.transform.localScale = new Vector3(Mathf.Sign(e.transform.localScale.x) * shrink, shrink, 1f);
        }
    }

    /// <summary>Was beim Schlucken noch unterwegs war, ist jetzt auch drin.</summary>
    private void EatRemaining()
    {
        foreach (Enemy e in swallowed) if (e != null) Destroy(e.gameObject);
        swallowed.Clear();
    }

    /// <summary>
    /// Ab hier ist der Boss sein Herz: mitten in der Kammer, eigenes Bild,
    /// runde Trefferflaeche um das Herz, steht fest (kinematisch).
    /// </summary>
    private void BecomeHeart()
    {
        heartFrames = VerkohlterArt.Strip("verkohlter_herz");
        heartMode = true;
        Loop(null);
        Teleport(chamber.HeartPosition);
        if (body != null)
        {
            body.enabled = true;
            body.flipX = false;
            body.transform.localPosition = Vector3.zero;
        }
        if (hitbox is CircleCollider2D circle)
        {
            circle.offset = Vector2.zero;
            circle.radius = HeartHitRadius;
        }
        AnimateHeart();
    }

    private void AnimateHeart()
    {
        if (body == null || heartFrames == null || heartFrames.Length == 0) return;
        int f = chamber != null ? chamber.Frame : 0;
        body.sprite = heartFrames[f % heartFrames.Length];
        // Schwebt: zwei Pixel auf und ab, ganzzahlig
        float bob = Mathf.Round(Mathf.Sin(Time.time * 1.7f) * 2f) / VerkohlterArt.PixelsPerUnit;
        body.transform.localPosition = new Vector3(0f, bob, 0f);
    }

    private void LateUpdate()
    {
        // Enemy dreht das Bild zum Spieler - das Herz hat keine Blickrichtung.
        if ((heartMode || heartDying) && body != null) body.flipX = false;
    }

    /// <summary>
    /// Der Boss verschwindet mitten in der Sequenz (Test-Szene "Boss weg",
    /// Lauf zu Ende): Spieler freigeben, Blende weg, Welt wieder an. Steht die
    /// Kammer schon, raeumt sie selbst auf, sobald sie merkt, dass er fehlt.
    /// </summary>
    private void AbortSwallow()
    {
        if (puppet != null && !puppet.Released) puppet.Release();
        puppet = null;
        if (iris != null) Destroy(iris.gameObject);
        iris = null;
        if (phaseThree && !heartMode)
        {
            VerkohlterSounds.StopAll();
            if (SpawnDirector.Active != null) SpawnDirector.Active.Suspended = false;
            foreach (Enemy e in swallowed)
            {
                if (e == null) continue;
                Rigidbody2D erb = e.GetComponent<Rigidbody2D>();
                if (erb != null) erb.simulated = true;
                e.transform.localScale = Vector3.one;
            }
            swallowed.Clear();
        }
    }

    // =================================================================== Tod

    private IEnumerator RunDeath()
    {
        yield return null;
        SetGone(true);
        velocity = Vector2.zero;

        // 1. Ansage, der Boden grollt
        if (SpawnDirector.Active != null)
            SpawnDirector.Active.Say(Loc.Get("boss.verkohlter.death", "DER TOD WIRD DICH HOLEN!"));
        ScreenShake.Kick(1.5f, DeathWarn);
        yield return new WaitForSeconds(DeathWarn);

        // 2. Im Bild aus der Asche aufbauen
        Teleport(DeathSpot());
        if (body != null) body.enabled = true;
        yield return AshIn(Single());
        SetGone(true);   // AshIn holt ihn zurueck - der Tod bleibt unberuehrbar
        Boom(3f, 0.25f);

        Loop(idle);
        yield return new WaitForSeconds(DeathIdle);

        // 3. Ein paar Meter auf den Spieler zu
        float t = 0f;
        while (t < DeathWalk && PlayerAlive && Vector2.Distance(PlayerPos, rb.position) > DeathWalkStop)
        {
            t += Time.deltaTime;
            velocity = (PlayerPos - rb.position).normalized * enemy.CurrentSpeed;
            Loop(walk);
            yield return null;
        }
        velocity = Vector2.zero;
        Loop(null);

        // 4. Glutausbruch - die Zone waechst mit, bis der Spieler drin ist
        Vector2 center = rb.position + GroundCenter;
        float radius = DeathRadiusMin;
        if (PlayerAlive) radius = Mathf.Max(radius, Vector2.Distance(PlayerPos, center) + DeathRadiusMargin);
        BossTelegraphMarker zone = Track(BossTelegraph.Zone(center, radius));

        t = 0f;
        while (t < DeathCharge)
        {
            t += Time.deltaTime;
            if (PlayerAlive)
            {
                float need = Vector2.Distance(PlayerPos, center) + DeathRadiusMargin;
                if (need > radius)
                {
                    radius = need;
                    if (zone != null) zone.Spread(center, radius);
                }
            }
            int frame = Mathf.Min(BurstImpactFrame - 1, Mathf.FloorToInt(t / DeathCharge * BurstImpactFrame));
            Show(self, burst, frame);
            if (zone != null) zone.SetProgress(t / DeathCharge);
            yield return null;
        }

        Show(self, burst, BurstImpactFrame);
        if (zone != null) zone.Impact(0.5f);
        Untrack(zone);
        Boom(8f, 0.7f);
        if (PlayerAlive) PlayerController.Instance.Execute();

        yield return PlayRest(Single(), BurstImpactFrame + 1);
        Loop(idle);
    }

    /// <summary>
    /// Wo der Tod auftaucht: neben dem Spieler auf der Seite mit mehr Platz im
    /// Bild, ganz im Bild (er ist gut 2 Einheiten hoch).
    /// </summary>
    private Vector2 DeathSpot()
    {
        Vector2 p = PlayerAlive ? PlayerPos : rb.position;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return p + Vector2.right * DeathSpawnDistance;

        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float roomRight = c.x + halfW - p.x;
        float roomLeft = p.x - (c.x - halfW);
        float side = roomRight >= roomLeft ? 1f : -1f;
        float room = Mathf.Max(roomRight, roomLeft) - 1.2f;
        float x = p.x + side * Mathf.Clamp(room, 2f, DeathSpawnDistance);
        float y = Mathf.Clamp(p.y - 0.5f, c.y - halfH + 0.3f, c.y + halfH - 2.4f);
        return new Vector2(x, y);
    }

    // ============================================================== Kleinkram

    private List<Actor> Single() => new List<Actor> { self };

    private void Loop(Sprite[] strip)
    {
        if (loop == strip) return;
        loop = strip;
        loopTime = 0f;
    }

    private void Show(Actor a, Sprite[] strip, int i)
    {
        if (a == self) loop = null;
        if (a.Renderer == null || strip == null || strip.Length == 0) return;
        a.Renderer.sprite = strip[Mathf.Clamp(i, 0, strip.Length - 1)];
    }

    /// <summary>Weg (Asche) = nicht zu treffen, kein Koerper.</summary>
    private void SetGone(bool gone)
    {
        bool reallyGone = gone || IsDeath;
        enemy.Untouchable = reallyGone;
        if (hitbox != null) hitbox.enabled = !reallyGone;
    }

    private void Teleport(Vector2 at)
    {
        at = Snap(at);
        rb.position = at;
        transform.position = new Vector3(at.x, at.y, transform.position.z);
    }

    private static Vector2 Snap(Vector2 p)
    {
        return new Vector2(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f);
    }

    private SpriteRenderer NewSprite(string name, int order)
    {
        var go = new GameObject("Verkohlter_" + name);
        RunScene.Place(go, "Effekte");
        var sr = go.AddComponent<SpriteRenderer>();
        if (body != null)
        {
            sr.sharedMaterial = body.sharedMaterial;
            sr.sortingLayerID = body.sortingLayerID;
        }
        sr.sortingOrder = order;
        spawned.Add(go);
        return sr;
    }

    private void Despawn(GameObject go)
    {
        spawned.Remove(go);
        if (go != null) Destroy(go);
    }

    private BossTelegraphMarker Track(BossTelegraphMarker m)
    {
        if (m != null) markers.Add(m);
        return m;
    }

    private void Untrack(BossTelegraphMarker m)
    {
        if (m != null) markers.Remove(m);
    }

    private static bool Inside(Vector2 center, float radius)
    {
        if (!PlayerAlive) return false;
        float reach = radius + PlayerRadius;
        return (PlayerPos - center).sqrMagnitude <= reach * reach;
    }

    private void Hit(float factor)
    {
        if (!PlayerAlive) return;
        PlayerController.Instance.TakeDamage(enemy.ContactDamage * factor);
    }

    private static void Boom(float pixels, float seconds)
    {
        ScreenShake.Kick(pixels, seconds);
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.EarthHit);
        }
    }

    private static bool PlayerAlive =>
        PlayerController.Instance != null && PlayerController.Instance.gameObject.activeSelf;

    private static Vector2 PlayerPos =>
        PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position
            : Vector2.zero;

#if UNITY_EDITOR
    /// <summary>Nur fuer VerkohlterBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Collider2D collider,
                           Sprite[] idleStrip, Sprite[] walkStrip, Sprite[] burstStrip,
                           Sprite[] ashOutStrip, Sprite[] ashInStrip,
                           Sprite[] shadowIdleStrip, Sprite[] shadowBurstStrip,
                           Sprite[] shadowAshOutStrip, Sprite[] shadowAshInStrip,
                           Sprite[] crumbStrip, Sprite[] patchStrip)
    {
        body = bodyRenderer;
        hitbox = collider;
        idle = idleStrip;
        walk = walkStrip;
        burst = burstStrip;
        ashOut = ashOutStrip;
        ashIn = ashInStrip;
        shadowIdle = shadowIdleStrip;
        shadowBurst = shadowBurstStrip;
        shadowAshOut = shadowAshOutStrip;
        shadowAshIn = shadowAshInStrip;
        crumb = crumbStrip;
        patch = patchStrip;
    }
#endif
}
