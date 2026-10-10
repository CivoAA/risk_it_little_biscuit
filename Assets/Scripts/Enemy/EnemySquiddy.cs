using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Das Gespenst / Squiddy - Endboss des Lebkuchen-Geisterwalds. Bilder aus
/// Tools/squiddy.py + Tools/squiddy_fx.py, das Prefab baut
/// Tools -> Gegner -> Squiddy bauen.
///
/// PHASE 1 - DAS GESPENST: ein grosses Laken-Gespenst, das im dunklen Wald
/// selbst leuchtet. Ab und zu lugt unten eine lila Tentakelspitze heraus ...
///
///   Buh!           taucht in den Boden ab. Sein Schatten mit glimmenden Augen
///                  jagt den Spieler, bleibt stehen (rote Zone), und das
///                  Gespenst schiesst mit einem Schrei heraus - Druckwelle.
///   Geisterreigen  Arme hoch, es wirbelt; drei Spiralarme aus kleinen,
///                  wuetenden Zuckergeistern fliegen nach aussen (rote Bahnen
///                  zeigen, wo die Arme anfangen). Zwischen den Geistern ist Platz.
///
/// ENTHUELLUNG (bei halbem Leben, unverwundbar): die Welt dunkelt ab wie eine
/// Buehne, das Gespenst zittert, Beulen wandern unter dem Laken, Tentakel
/// kriechen unter dem Saum hervor, packen zu, reissen das Laken hoch und
/// schleudern es weg (es segelt davon und bleibt liegen). Darunter: Squiddy.
/// Der Heiligenschein senkt sich - PLING, Engelschor, Licht an, Name wechselt.
///
/// PHASE 2 - SQUIDDY (Traubengelee-Qualle): schwimmt im Rueckstoss.
///
///   Nessel-Stern   Tentakel hoch, sechs rote Bahnen; sie rammt die Tentakel in
///                  den Boden, entlang der Bahnen brechen elektrische Tentakel aus.
///   Quallen-Brut   zieht sich zusammen - PLOPP - Baby-Squiddies (echte Gegner)
///                  schwimmen auf den Spieler zu und entladen sich bei Beruehrung.
///   Hochspannung   laedt sich auf (grosse rote Zone um sie), der Strom springt
///                  auf jedes Baby ueber (eigene Zone) - dann entlaedt sich alles.
///   Ab 25 %: schneller, doppelter Nessel-Stern, mehr Babys.
///
/// TOD: Squiddy wird friedlich, winkt und faehrt im Lichtstrahl auf.
///
/// Animation ohne Animator: dieses Skript spielt die Streifen selbst ab.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemySquiddy : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float Fps = 12f;
    private const float PhaseTwoAt = 0.5f;
    private const float RageAt = 0.25f;

    private const float FloatMin = 2.2f, FloatMax = 3.2f;
    private const float SwimMin = 1.6f, SwimMax = 2.4f;
    private const float SwimMinRage = 1.0f, SwimMaxRage = 1.6f;

    // Buh!
    private const float ShadowChase = 1.7f;
    private const float ShadowSpeed = 5.2f;          // etwas schneller als der Spieler (4)
    private const float BuhLock = 0.95f;             // Zone steht so lange, dann BUH
    private const float BuhRadius = 3.1f;
    private const float BuhDamage = 1.2f;

    // Geisterreigen
    private const int ReigenArms = 3;
    private const float ReigenTime = 2.6f;
    private const float ReigenInterval = 0.32f;
    private const float ReigenTurn = 70f;            // Grad pro Sekunde, um die sich die Arme drehen
    private const float GhostSpeed = 3.3f;
    private const float GhostSpin = 18f;
    private const float GhostRange = 11f;
    private const float GhostDamage = 0.45f;

    // Nessel-Stern
    private const int StingRays = 6;
    private const float StingWind = 1.1f, StingWindRage = 0.85f;
    private const float StingLength = 8.5f, StingWidth = 1.05f;
    private const float StingStart = 1.3f, StingStep = 0.78f, StingWave = 15f;
    private const float StingDamage = 0.9f;

    // Quallen-Brut
    private const int BroodCount = 3, BroodCountRage = 4, MaxBabies = 6;
    private const float BroodCooldown = 10f;

    // Hochspannung
    private const float ChargeTime = 1.75f, ChargeTimeRage = 1.4f;
    private const float DischargeRadius = 5f;
    private const float DischargeDamage = 1.4f;
    private const float BabyZoneRadius = 1.7f;
    private const float BabyZoneDamage = 0.8f;

    /// <summary>Boss-Mitte ueber dem Pivot (Bildhoehe 160 px, Glocke/Kopf um 2.3 Einheiten).</summary>
    private static readonly Vector2 BodyCenter = new Vector2(0f, 2.2f);
    /// <summary>Wo das geraffte Laken in Bild 29 der Enthuellung sitzt (aus Tools/squiddy.py).</summary>
    private static readonly Vector2 BundleOffset = new Vector2(-0.3f, 4.05f);

    private static readonly Color GhostLight = new Color(0.78f, 0.76f, 1f);
    private static readonly Color JellyLight = new Color(0.72f, 0.38f, 1f);
    private static readonly Color HaloLight = new Color(1f, 0.85f, 0.5f);

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D hitbox;
    [Header("Gespenst")]
    [SerializeField] private Sprite[] ghostFloat;
    [SerializeField] private Sprite[] ghostPeek;
    [SerializeField] private Sprite[] ghostSink;
    [SerializeField] private Sprite[] ghostBuh;
    [SerializeField] private Sprite[] ghostSpin;
    [Header("Enthuellung")]
    [SerializeField] private Sprite[] reveal;
    [Header("Squiddy")]
    [SerializeField] private Sprite[] swim;
    [SerializeField] private Sprite[] sting;
    [SerializeField] private Sprite[] brood;
    [SerializeField] private Sprite[] charge;
    [SerializeField] private Sprite[] death;
    [Header("Effekte")]
    [SerializeField] private Sprite[] shadowFx;
    [SerializeField] private Sprite[] buhWave;
    [SerializeField] private Sprite[] miniGhost;
    [SerializeField] private Sprite[] sheetFly;
    [SerializeField] private Sprite[] sheetLand;
    [SerializeField] private Sprite[] nesselFx;
    [SerializeField] private Sprite[] dischargeFx;
    [SerializeField] private GameObject babyPrefab;
    [Tooltip("Optional: Musik fuer Phase 2 (sonst laeuft das Stueck der Karte von vorn).")]
    [SerializeField] private AudioClip phaseTwoMusic;

    // ---------------------------------------------------------------- Zustand

    private enum Phase { Ghost, Reveal, Squid, Dying }
    private enum Attack { None, Buh, Reigen, Sting, Brood, Charge }

    private Enemy enemy;
    private Rigidbody2D rb;
    private Vector2 velocity;
    private Phase phase = Phase.Ghost;
    private bool rage;
    private float lastBrood = -99f;

    private Sprite[] clip;
    private float clipTime, clipFps;
    private bool clipLoop;
    private int lastLoop;

    private Light2D glow;
    private float glowBase = 0.85f;
    private float flare;
    private Color flareColor;

    private readonly List<BossTelegraphMarker> markers = new List<BossTelegraphMarker>();
    private readonly List<GameObject> temp = new List<GameObject>();
    private readonly List<SquiddyBaby> babies = new List<SquiddyBaby>();
    private SquiddyLaken sheet;
    private bool submerged;                 // Buh!: steckt gerade im Boden
    private ScreenIris iris;

    /// <summary>Fuer die Test-Szene: laeuft schon Phase 2?</summary>
    public bool IsPhaseTwo => phase == Phase.Squid || phase == Phase.Dying;

    // ---------------------------------------------------------------- Ablauf

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;
        enemy.HoldDeath = true;
        enemy.SkipDeathEffect = true;
        enemy.DeathHeld += OnDeathHeld;

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        glow = SquiddyFx.NewLight(transform, BodyCenter, GhostLight, glowBase, 5.5f);
    }

    private void OnDestroy()
    {
        if (enemy != null) enemy.DeathHeld -= OnDeathHeld;
    }

    private void OnEnable()
    {
        phase = Phase.Ghost;
        // Phase 1 faellt nicht unter die Haelfte - erst muss das Laken runter.
        enemy.MinHealthFraction = PhaseTwoAt;
        enemy.DisplayName = Loc.Get("boss.squiddy.ghost", "Das Gespenst");
        StartCoroutine(BrainGhost());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Cleanup(true);
        SquiddyBuehne.Reset();
        velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = velocity;
    }

    private void Update()
    {
        // Phasenwechsel: sofort, auch mitten in einer Attacke
        if (phase == Phase.Ghost && enemy.HealthFraction <= PhaseTwoAt + 0.0001f)
        {
            StopAllCoroutines();
            Cleanup(false);
            StartCoroutine(Reveal());
        }

        if (clip != null && clip.Length > 0 && body != null)
        {
            clipTime += Time.deltaTime;
            int raw = Mathf.FloorToInt(clipTime * clipFps);
            int i = clipLoop ? raw % clip.Length : Mathf.Min(raw, clip.Length - 1);
            // Gespenst: nach jeder Schleife manchmal die Variante mit der Tentakelspitze
            if (clipLoop && (clip == ghostFloat || clip == ghostPeek))
            {
                int loop = raw / clip.Length;
                if (loop != lastLoop)
                {
                    lastLoop = loop;
                    Sprite[] next = ghostPeek != null && ghostPeek.Length > 0 && Random.value < 0.22f ? ghostPeek : ghostFloat;
                    if (next != clip)
                    {
                        clip = next;
                        clipTime = 0f;
                        lastLoop = 0;
                        i = 0;
                    }
                }
            }
            body.sprite = clip[i];
        }

        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glow == null) return;
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.6f);
        Color baseCol = phase == Phase.Ghost || phase == Phase.Reveal ? GhostLight : JellyLight;
        float pulse = 0f;
        if (phase == Phase.Squid && clip == swim) pulse = 0.18f * Mathf.Sin(clipTime * Fps / swim.Length * Mathf.PI * 2f);
        glow.color = Color.Lerp(baseCol, flareColor, Mathf.Clamp01(flare));
        glow.intensity = glowBase + pulse + flare * 1.6f;
    }

    private void Flare(Color color, float amount)
    {
        flareColor = color;
        flare = Mathf.Max(flare, amount);
    }

    // ============================================================ PHASE 1

    private IEnumerator BrainGhost()
    {
        yield return Intro();
        yield return Float(1.6f);

        Attack last = Attack.None;
        while (true)
        {
            Attack next = last == Attack.Buh ? Attack.Reigen : Attack.Buh;
            if (next == Attack.Buh)
            {
                yield return Buh();
                // spaeter manchmal zweimal hintereinander
                if (enemy.HealthFraction < 0.75f && Random.value < 0.4f)
                {
                    yield return Float(0.5f);
                    yield return Buh();
                }
            }
            else
            {
                yield return Reigen();
            }
            last = next;
            yield return Float(Random.Range(FloatMin, FloatMax));
        }
    }

    /// <summary>Taucht aus dem Boden auf (ohne Schrei).</summary>
    private IEnumerator Intro()
    {
        SetTouchable(false);
        SquiddySounds.Emerge();
        yield return PlayRange(ghostBuh, 0, 4);
        SquiddySounds.Wooo();
        yield return PlayRange(ghostBuh, 9, 13);
        SetTouchable(true);
    }

    private IEnumerator Float(float seconds)
    {
        if (clip != ghostFloat && clip != ghostPeek) Play(ghostFloat, Fps, true);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            PlayerController player = Player();
            if (player != null)
            {
                Vector2 to = (Vector2)player.transform.position - rb.position;
                // schwebt nicht stur geradeaus, sondern in sanften Schlangenlinien
                Vector2 side = new Vector2(-to.y, to.x).normalized * Mathf.Sin(Time.time * 1.3f) * 0.35f;
                velocity = to.sqrMagnitude > 0.04f ? (to.normalized + side).normalized * enemy.CurrentSpeed : Vector2.zero;
            }
            else velocity = Vector2.zero;
            yield return null;
        }
        velocity = Vector2.zero;
    }

    // ----------------------------------------------------------------- Buh!

    private IEnumerator Buh()
    {
        velocity = Vector2.zero;
        SquiddySounds.Wooo();
        yield return PlayRange(ghostSink, 0, 3);

        // abtauchen
        SetTouchable(false);
        SquiddySounds.Sink();
        yield return PlayRange(ghostSink, 4, 9);
        body.enabled = false;
        submerged = true;

        // der Schatten jagt den Spieler
        Vector2 pos = rb.position;
        SpriteRenderer shadow = SquiddyFx.NewSprite("Spukschatten", pos, -1);
        temp.Add(shadow.gameObject);
        float t = 0f;
        while (t < ShadowChase)
        {
            t += Time.deltaTime;
            PlayerController player = Player();
            if (player != null)
                pos = Vector2.MoveTowards(pos, player.transform.position, ShadowSpeed * Time.deltaTime);
            shadow.transform.position = SquiddyFx.Snap(pos);
            if (shadowFx != null && shadowFx.Length > 0) shadow.sprite = shadowFx[Mathf.FloorToInt(t * 10f) % shadowFx.Length];
            rb.position = pos;
            yield return null;
        }

        // Zone steht - gleich kommt er raus
        BossTelegraphMarker zone = BossTelegraph.Zone(pos, BuhRadius);
        markers.Add(zone);
        float emergeAt = BuhLock - 5f / Fps;
        bool emerged = false;
        t = 0f;
        while (t < BuhLock)
        {
            t += Time.deltaTime;
            zone.SetProgress(t / BuhLock);
            if (!emerged && t >= emergeAt)
            {
                emerged = true;
                rb.position = pos;
                transform.position = pos;
                body.enabled = true;
                submerged = false;
                clip = null;
                if (shadow != null) Destroy(shadow.gameObject);
                SquiddySounds.Emerge();
            }
            if (emerged)
            {
                body.sprite = ghostBuh[Mathf.Min(4, Mathf.FloorToInt((t - emergeAt) * Fps))];
            }
            else if (shadow != null && shadowFx != null && shadowFx.Length > 0)
            {
                shadow.sprite = shadowFx[Mathf.FloorToInt(t * 14f) % shadowFx.Length];
            }
            yield return null;
        }

        // BUH!
        zone.Impact();
        markers.Remove(zone);
        body.sprite = ghostBuh[5];
        SetTouchable(true);
        SquiddySounds.Buh();
        ScreenShake.Kick(5f, 0.4f);
        Flare(GhostLight, 1f);
        SquiddyFx.OneShot("BuhWelle", buhWave, pos + Vector2.up * 0.3f, 16f, 30);
        PlayerController victim = Player();
        if (victim != null && ((Vector2)victim.transform.position - pos).sqrMagnitude <= BuhRadius * BuhRadius)
            victim.TakeDamage(enemy.ContactDamage * BuhDamage);

        yield return PlayRange(ghostBuh, 6, 8);
        yield return PlayRange(ghostBuh, 9, 13);
    }

    // -------------------------------------------------------- Geisterreigen

    private IEnumerator Reigen()
    {
        velocity = Vector2.zero;
        PlayerController player = Player();
        Vector2 center = rb.position + BodyCenter * 0.8f;
        float baseAngle = player != null
            ? Mathf.Atan2(player.transform.position.y - center.y, player.transform.position.x - center.x) * Mathf.Rad2Deg
            : 0f;
        float turn = Random.value < 0.5f ? 1f : -1f;

        // Ausholen: rote Bahnen zeigen, wo die Arme starten
        var arms = new BossTelegraphMarker[ReigenArms];
        for (int i = 0; i < ReigenArms; i++)
        {
            float a = (baseAngle + i * 360f / ReigenArms) * Mathf.Deg2Rad;
            arms[i] = BossTelegraph.Path(center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f, 0.8f);
            markers.Add(arms[i]);
        }
        SquiddySounds.Wooo();
        float wind = 6f / Fps + 0.25f;
        float t = 0f;
        clip = null;
        while (t < wind)
        {
            t += Time.deltaTime;
            body.sprite = ghostSpin[Mathf.Min(5, Mathf.FloorToInt(t / wind * 6f))];
            foreach (BossTelegraphMarker m in arms) if (m != null) m.SetProgress(t / wind);
            yield return null;
        }
        foreach (BossTelegraphMarker m in arms)
        {
            if (m == null) continue;
            m.Impact();
            markers.Remove(m);
        }

        // Wirbeln: Spiralarme aus Mini-Geistern
        t = 0f;
        float nextShot = 0f;
        while (t < ReigenTime)
        {
            t += Time.deltaTime;
            body.sprite = ghostSpin[6 + Mathf.FloorToInt(t * Fps) % 6];
            if (t >= nextShot)
            {
                nextShot += ReigenInterval;
                float rot = baseAngle + turn * ReigenTurn * t;
                for (int i = 0; i < ReigenArms; i++)
                {
                    float a = (rot + i * 360f / ReigenArms) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    SquiddySpukgeist g = SquiddySpukgeist.Launch(miniGhost, center + dir * 0.9f, dir, GhostSpeed,
                                                                 turn * GhostSpin, GhostRange, enemy.ContactDamage * GhostDamage);
                    temp.Add(g.gameObject);
                }
                SquiddySounds.Spit();
            }
            yield return null;
        }
        temp.RemoveAll(go => go == null);
        yield return PlayRange(ghostSpin, 12, 17);
    }

    // ============================================================ ENTHUELLUNG

    private IEnumerator Reveal()
    {
        phase = Phase.Reveal;
        velocity = Vector2.zero;
        SetTouchable(false);
        enemy.Untouchable = true;

        // Steckte er gerade im Boden (Buh!), taucht er erst auf - ohne Schrei
        if (submerged)
        {
            submerged = false;
            body.enabled = true;
            SquiddySounds.Emerge();
            yield return PlayRange(ghostBuh, 0, 4);
            yield return PlayRange(ghostBuh, 10, 13);
        }
        body.enabled = true;

        AudioController audio = AudioController.Instance;
        AudioClip mapMusic = audio != null ? audio.CurrentRunClip : null;
        if (audio != null) audio.FadeRunMusic(1.4f);
        SquiddySounds.Rumble();

        StartCoroutine(Dim(0f, 0.55f, 0.7f));

        // Bilddauern: Zittern zuegig, Tentakel etwas ruhiger, Wurf mit Ausholpause
        clip = null;
        for (int f = 0; f < reveal.Length; f++)
        {
            body.sprite = reveal[f];
            float hold = 1f / Fps;
            if (f >= 16 && f < 22) hold = 1f / 10f;
            if (f == 21) hold = 0.22f;                         // packt zu ... Pause
            if (f == 29) hold = 0.28f;                         // Ausholen vor dem Wurf
            if (f == 30) hold = 0.16f;
            if (f == 40) hold = 0.32f;                         // PLING

            switch (f)
            {
                case 8: case 10: case 12: case 14: case 18:
                    SquiddySounds.Blub();
                    break;
                case 4:
                    SquiddySounds.Rumble();
                    break;
                case 21:
                    ScreenShake.Kick(2f, 0.2f);
                    break;
                case 22:
                    SquiddySounds.Rip();
                    break;
                case 30:
                    ThrowSheet();
                    break;
                case 40:
                    Halo(mapMusic);
                    break;
                case 44:
                    StartCoroutine(Dim(0.55f, 0f, 0.9f));
                    break;
            }
            yield return new WaitForSeconds(hold);
        }

        phase = Phase.Squid;
        enemy.MinHealthFraction = 0f;
        enemy.Untouchable = false;
        SetTouchable(true);
        StartCoroutine(BrainSquid());
    }

    private IEnumerator Dim(float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            SquiddyBuehne.SetDim(Mathf.Lerp(from, to, k));
            yield return null;
        }
        if (to <= 0f) SquiddyBuehne.Reset();
        else SquiddyBuehne.SetDim(to);
    }

    /// <summary>Bild 30: das Laken fliegt weg, Squiddy steht da.</summary>
    private void ThrowSheet()
    {
        SquiddySounds.Whoosh();
        ScreenShake.Kick(4f, 0.35f);
        Flare(JellyLight, 1.2f);
        glowBase = 1.0f;

        Vector2 at = rb.position;
        sheet = SquiddyLaken.Throw(sheetFly, sheetLand, at + new Vector2(BundleOffset.x, 0f), BundleOffset.y,
                                   new Vector2(-3.4f, 0.5f), 1.1f);

        // Die Horde wird weggepustet - die Buehne gehoert Squiddy
        SquiddyFx.OneShot("Enthuellungswelle", buhWave, at + Vector2.up * 0.3f, 14f, 30, new Color(0.85f, 0.6f, 1f, 1f));
        foreach (Enemy e in new List<Enemy>(Enemy.Alive))
        {
            if (e == null || e == enemy || e.IsBoss) continue;
            Vector2 d = (Vector2)e.transform.position - at;
            if (d.sqrMagnitude > 64f) continue;
            e.ApplyPull(d.normalized * Mathf.Lerp(9f, 4f, d.magnitude / 8f), 0.35f);
        }
    }

    /// <summary>Bild 40: der Heiligenschein sitzt. PLING.</summary>
    private void Halo(AudioClip mapMusic)
    {
        SquiddySounds.Pling();
        Flare(HaloLight, 1.3f);
        ScreenShake.Kick(2f, 0.2f);
        enemy.DisplayName = Loc.Get("boss.squiddy.name", "Squiddy");
        if (SpawnDirector.Active != null)
            SpawnDirector.Active.Say(Loc.Get("boss.squiddy.reveal", "ES WAR DIE GANZE ZEIT SQUIDDY!"));

        AudioController audio = AudioController.Instance;
        AudioClip next = phaseTwoMusic != null ? phaseTwoMusic : mapMusic;
        if (audio != null && next != null) audio.SwapRunMusic(next, 0f, phaseTwoMusic != null ? 0.4f : 1.5f);
    }

    // ============================================================ PHASE 2

    private IEnumerator BrainSquid()
    {
        yield return Swim(0.8f);
        yield return Brood();
        Attack last = Attack.Brood;

        while (true)
        {
            yield return Swim(rage ? Random.Range(SwimMinRage, SwimMaxRage) : Random.Range(SwimMin, SwimMax));

            if (!rage && enemy.HealthFraction <= RageAt)
            {
                rage = true;
                if (SpawnDirector.Active != null)
                    SpawnDirector.Active.Say(Loc.Get("boss.squiddy.rage", "SQUIDDY STEHT UNTER STROM!"));
                Flare(JellyLight, 1f);
            }

            int alive = AliveBabies();
            Attack next;
            if (alive < 2 && Time.time - lastBrood > BroodCooldown && last != Attack.Brood) next = Attack.Brood;
            else if (alive >= 2 && last != Attack.Charge) next = Attack.Charge;
            else next = last == Attack.Sting ? Attack.Charge : Attack.Sting;

            switch (next)
            {
                case Attack.Brood: yield return Brood(); break;
                case Attack.Charge: yield return Charge(); break;
                default: yield return Sting(); break;
            }
            last = next;
        }
    }

    private IEnumerator Swim(float seconds)
    {
        Play(swim, Fps, true);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            PlayerController player = Player();
            if (player != null)
            {
                // Rueckstoss: Schub in den ersten Bildern des Pulses, dann gleiten
                float phaseT = (clipTime * Fps / swim.Length) % 1f;
                float thrust = phaseT < 0.3f ? Mathf.Sin(phaseT / 0.3f * Mathf.PI * 0.5f) : Mathf.Cos((phaseT - 0.3f) / 0.7f * Mathf.PI * 0.5f);
                Vector2 to = (Vector2)player.transform.position + Vector2.down * 1.2f - rb.position;
                float speed = enemy.CurrentSpeed * (0.35f + 1.3f * thrust) * (rage ? 1.2f : 1f);
                velocity = to.sqrMagnitude > 0.04f ? to.normalized * speed : Vector2.zero;
            }
            else velocity = Vector2.zero;
            yield return null;
        }
        velocity = Vector2.zero;
    }

    // ---------------------------------------------------------- Nessel-Stern

    private IEnumerator Sting()
    {
        velocity = Vector2.zero;
        yield return StingVolley(0f, rage ? StingWindRage : StingWind, true);
        if (rage)
        {
            // zweite Salve dazwischen - die Bahnen erscheinen, waehrend die erste noch tobt
            yield return StingVolley(180f / StingRays, 0.65f, false);
        }
        yield return PlayRange(sting, 10, 13);
    }

    private IEnumerator StingVolley(float offset, float wind, bool windUpFrames)
    {
        Vector2 center = rb.position;
        PlayerController player = Player();
        float baseAngle = player != null
            ? Mathf.Atan2(player.transform.position.y - center.y, player.transform.position.x - center.x) * Mathf.Rad2Deg
            : Random.value * 360f;
        baseAngle += offset;

        var dirs = new Vector2[StingRays];
        var paths = new BossTelegraphMarker[StingRays];
        for (int i = 0; i < StingRays; i++)
        {
            float a = (baseAngle + i * 360f / StingRays) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            paths[i] = BossTelegraph.Path(center, center + dirs[i] * StingLength, StingWidth);
            markers.Add(paths[i]);
        }
        SquiddySounds.Zap();
        float t = 0f;
        clip = null;
        float nextZap = 0.25f;
        while (t < wind)
        {
            t += Time.deltaTime;
            if (windUpFrames) body.sprite = sting[Mathf.Min(4, Mathf.FloorToInt(t / wind * 5f))];
            else body.sprite = sting[5 + Mathf.FloorToInt(t * Fps) % 5];
            foreach (BossTelegraphMarker m in paths) if (m != null) m.SetProgress(t / wind);
            if (t >= nextZap)
            {
                nextZap += 0.3f;
                SquiddySounds.Zap();
            }
            yield return null;
        }
        foreach (BossTelegraphMarker m in paths)
        {
            if (m == null) continue;
            m.Impact();
            markers.Remove(m);
        }

        // rein in den Boden - entlang der Bahnen bricht es aus
        body.sprite = sting[5];
        ScreenShake.Kick(4f, 0.3f);
        Flare(JellyLight, 0.8f);
        SquiddySounds.Erupt();
        float dmg = enemy.ContactDamage * StingDamage;
        for (int i = 0; i < StingRays; i++)
        {
            for (float d = StingStart; d <= StingLength; d += StingStep)
            {
                SquiddyNessel n = SquiddyNessel.Spawn(nesselFx, center + dirs[i] * d, (d - StingStart) / StingWave, dmg);
                temp.Add(n.gameObject);
            }
        }
        temp.RemoveAll(go => go == null);
        yield return PlayRange(sting, 6, 9);
    }

    // ---------------------------------------------------------- Quallen-Brut

    private IEnumerator Brood()
    {
        velocity = Vector2.zero;
        lastBrood = Time.time;
        yield return PlayRange(brood, 0, 3);

        body.sprite = brood[4];
        SquiddySounds.Plop();
        Flare(JellyLight, 0.6f);
        int count = Mathf.Min(rage ? BroodCountRage : BroodCount, MaxBabies - AliveBabies());
        PlayerController player = Player();
        Vector2 aim = player != null ? ((Vector2)player.transform.position - rb.position).normalized : Vector2.down;
        if (aim.sqrMagnitude < 0.01f) aim = Vector2.down;
        for (int i = 0; i < count && babyPrefab != null; i++)
        {
            float spread = count > 1 ? Mathf.Lerp(-70f, 70f, i / (float)(count - 1)) : 0f;
            Vector2 dir = Quaternion.Euler(0f, 0f, spread) * aim;
            GameObject go = Instantiate(babyPrefab, rb.position + Vector2.up * 0.9f + dir * 0.6f, Quaternion.identity);
            RunScene.Place(go, "Gegner");
            SquiddyBaby b = go.GetComponent<SquiddyBaby>();
            if (b != null)
            {
                b.Push(dir * 5f);
                babies.Add(b);
            }
        }
        yield return new WaitForSeconds(1f / Fps);
        yield return PlayRange(brood, 5, 11);
    }

    private int AliveBabies()
    {
        babies.RemoveAll(b => b == null || !b.Alive);
        return babies.Count;
    }

    // ----------------------------------------------------------- Hochspannung

    private IEnumerator Charge()
    {
        velocity = Vector2.zero;
        Vector2 center = rb.position;
        BossTelegraphMarker zone = BossTelegraph.Zone(center, DischargeRadius);
        markers.Add(zone);

        // Strom springt auf die Babys: sie stehen still, jedes bekommt eine Zone
        AliveBabies();
        var babyZones = new List<BossTelegraphMarker>();
        var chains = new List<SquiddyBlitz>();
        foreach (SquiddyBaby b in babies)
        {
            b.Charged = true;
            BossTelegraphMarker bz = BossTelegraph.Zone(b.transform.position, BabyZoneRadius);
            babyZones.Add(bz);
            markers.Add(bz);
            SquiddyBlitz chain = SquiddyBlitz.Between(transform, (Vector3)(BodyCenter + Vector2.down * 0.6f), b.transform, Vector3.up * 0.5f);
            chains.Add(chain);
            temp.Add(chain.gameObject);
        }

        SquiddySounds.Charge();
        float dur = rage ? ChargeTimeRage : ChargeTime;
        float rampEnd = dur * 0.4f;
        float t = 0f;
        clip = null;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            body.sprite = t < rampEnd ? charge[Mathf.Min(5, Mathf.FloorToInt(t / rampEnd * 6f))]
                                      : charge[6 + Mathf.FloorToInt((t - rampEnd) * Fps) % 6];
            zone.SetProgress(k);
            foreach (BossTelegraphMarker bz in babyZones) if (bz != null) bz.SetProgress(k);
            Flare(JellyLight, 0.3f + 0.7f * k);
            yield return null;
        }

        // ENTLADUNG
        body.sprite = charge[12];
        zone.Impact();
        foreach (BossTelegraphMarker bz in babyZones) if (bz != null) bz.Impact();
        markers.Clear();
        foreach (SquiddyBlitz c in chains) if (c != null) Destroy(c.gameObject);
        SquiddySounds.Discharge();
        ScreenShake.Kick(7f, 0.5f);
        Flare(Color.white, 1.4f);
        SquiddyFx.OneShot("Entladung", dischargeFx, center, 16f, 32);
        StartCoroutine(FlashScreen(new Color(0.85f, 0.75f, 1f), 0.55f, 0.3f));

        PlayerController player = Player();
        Vector2 pp = player != null ? (Vector2)player.transform.position : Vector2.one * 99999f;
        bool hit = (pp - center).sqrMagnitude <= DischargeRadius * DischargeRadius;
        foreach (SquiddyBaby b in babies)
        {
            if (b == null) continue;
            if (!hit && (pp - (Vector2)b.transform.position).sqrMagnitude <= BabyZoneRadius * BabyZoneRadius) hit = true;
            b.Overcharge();
        }
        if (hit && player != null) player.TakeDamage(enemy.ContactDamage * DischargeDamage);

        yield return PlayRange(charge, 13, 17);
    }

    private IEnumerator FlashScreen(Color color, float alpha, float seconds)
    {
        if (iris == null) iris = ScreenIris.Create();
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            iris.Flash(color, alpha * (1f - t / seconds));
            yield return null;
        }
        iris.Flash(color, 0f);
    }

    // ============================================================ TOD

    private void OnDeathHeld()
    {
        if (phase == Phase.Dying) return;
        phase = Phase.Dying;
        StopAllCoroutines();
        Cleanup(false);
        StartCoroutine(Ascend());
    }

    private IEnumerator Ascend()
    {
        velocity = Vector2.zero;
        SetTouchable(false);
        enemy.Untouchable = true;
        body.enabled = true;
        foreach (SquiddyBaby b in babies) if (b != null) b.Overcharge();

        yield return PlayRange(death, 0, 3);
        SquiddySounds.Ascend();
        Flare(HaloLight, 1.2f);
        SquiddyLichtstrahl.Spawn(rb.position, 5.2f);
        yield return PlayRange(death, 4, 9);

        // winkt und schwebt im Lichtstrahl nach oben
        Transform img = body.transform;
        Vector3 start = img.localPosition;
        float t = 0f, dur = 2.8f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            body.sprite = death[10 + Mathf.FloorToInt(t * Fps) % 10];
            img.localPosition = start + Vector3.up * Mathf.Round(k * k * 6f * SquiddyFx.Ppu) / SquiddyFx.Ppu;
            body.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k)));
            if (glow != null) glow.transform.localPosition = (Vector3)BodyCenter + (img.localPosition - start);
            Flare(HaloLight, 0.8f * (1f - k));
            yield return null;
        }
        if (sheet != null) sheet.FadeAway();
        SquiddyBuehne.Reset();
        enemy.FinishHeldDeath();
    }

    // ============================================================ Kleinkram

    private void SetTouchable(bool on)
    {
        enemy.Untouchable = !on;
        if (hitbox != null) hitbox.enabled = on;
    }

    /// <summary>Raeumt laufende Attacken weg. all: auch Laken und Babys (Kampf vorbei).</summary>
    private void Cleanup(bool all)
    {
        foreach (BossTelegraphMarker m in markers) if (m != null) m.Cancel();
        markers.Clear();
        foreach (GameObject go in temp)
        {
            if (go == null) continue;
            // Mini-Geister duerfen davonfliegen, Schatten/Blitze nicht
            if (!all && go.GetComponent<SquiddySpukgeist>() != null) continue;
            if (!all && go.GetComponent<SquiddyNessel>() != null) continue;
            Destroy(go);
        }
        temp.Clear();
        foreach (SquiddyBaby b in babies) if (b != null) b.Charged = false;
        if (body != null) body.enabled = true;
        if (all && sheet != null) sheet.FadeAway();
        if (iris != null)
        {
            Destroy(iris.gameObject);
            iris = null;
        }
    }

    private void Play(Sprite[] strip, float fps, bool loop)
    {
        clip = strip;
        clipFps = fps;
        clipLoop = loop;
        clipTime = 0f;
        lastLoop = 0;
        if (strip != null && strip.Length > 0 && body != null) body.sprite = strip[0];
    }

    /// <summary>Spielt die Bilder first..last einmal ab und wartet darauf.</summary>
    private IEnumerator PlayRange(Sprite[] strip, int first, int last)
    {
        clip = null;
        if (strip == null) yield break;
        for (int i = first; i <= last && i < strip.Length; i++)
        {
            body.sprite = strip[i];
            yield return new WaitForSeconds(1f / Fps);
        }
    }

    private static PlayerController Player()
    {
        PlayerController p = PlayerController.Instance;
        return p != null && p.gameObject.activeSelf ? p : null;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer SquiddyBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Collider2D collider, Dictionary<string, Sprite[]> s, GameObject baby)
    {
        body = bodyRenderer;
        hitbox = collider;
        ghostFloat = s["geist_schweben"];
        ghostPeek = s["geist_spaeher"];
        ghostSink = s["geist_abtauchen"];
        ghostBuh = s["geist_buh"];
        ghostSpin = s["geist_reigen"];
        reveal = s["enthuellung"];
        swim = s["schwimm"];
        sting = s["nessel"];
        brood = s["brut"];
        charge = s["spannung"];
        death = s["tod"];
        shadowFx = s["schatten"];
        buhWave = s["buh_welle"];
        miniGhost = s["spukgeist"];
        sheetFly = s["laken_flug"];
        sheetLand = s["laken_boden"];
        nesselFx = s["nessel_tentakel"];
        dischargeFx = s["entladung"];
        babyPrefab = baby;
    }

    public void EditorSetMusic(AudioClip clip) => phaseTwoMusic = clip;
#endif
}
