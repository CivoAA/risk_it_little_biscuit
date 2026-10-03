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

    public bool IsPhaseTwo => enemy != null && enemy.HealthFraction <= PhaseTwoAt;

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
    }

    private void OnEnable()
    {
        StartCoroutine(IsDeath ? RunDeath() : RunBoss());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (BossTelegraphMarker m in markers) if (m != null) m.Cancel();
        markers.Clear();
        foreach (GameObject go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = velocity;
    }

    private void Update()
    {
        if (loop == null || loop.Length == 0 || body == null) return;
        loopTime += Time.deltaTime;
        body.sprite = loop[Mathf.FloorToInt(loopTime * Fps) % loop.Length];
    }

    // ============================================================== Bosskampf

    private IEnumerator RunBoss()
    {
        yield return null;
        yield return Walk(EntryWalk);

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
