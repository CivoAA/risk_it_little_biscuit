using System.Collections;
using UnityEngine;

/// <summary>
/// Der Eiskaiser - Endboss der Eiswelt (Eisgletscher). Ein Kaiserpinguin aus
/// Eiscreme mit Waffelkrone und Umhang aus Erdbeersosse. Bilder aus
/// Tools/eiskaiser.py, das Prefab baut Tools -> Gegner -> Eiskaiser bauen.
///
///   Watscheln     laeuft auf den Spieler zu, Umhang weht.
///
///   Kaiser-Kick   er formt einen Schneeball vor seinem Fuss, holt aus (rote
///                 Bahn zeigt die Richtung) und tritt ihn los. Die
///                 Lawinenkugel (<see cref="EiskaiserLawinenkugel"/>) rollt dem
///                 Spieler nach, FRISST unterwegs die Horde und waechst dabei.
///                 Zerschossen reisst sie die Gegner drumherum mit, trifft sie
///                 den Spieler, tut es weh - je groesser, desto mehr.
///
///   Kaiserlicher Frostring
///                 Flossen hoch, die Krone laedt, hellblaue Bahnen zeigen die
///                 Luecken. Beim Stampfer rast ein Ring aus Eiskristallen nach
///                 aussen (<see cref="EiskaiserFrostRing"/>). Wer nicht in einer
///                 Luecke steht, wird eingefroren (<see cref="PlayerIceBlock"/>).
///
///   Ab der Haelfte: kuerzeres Ausholen, zwei Kugeln im V, zwei Ringe
///   hintereinander (der zweite mit versetzten Luecken), und manchmal folgt
///   der Ring direkt auf den Kick.
///
/// Animation ohne Animator: die Streifen spielt dieses Skript selbst ab.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemyEiskaiser : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float PhaseTwoAt = 0.5f;
    private const float Fps = 12f;

    private const float EntryWalk = 2.0f;
    private const float WalkMin = 2.4f, WalkMax = 3.4f;
    private const float WalkMinP2 = 1.5f, WalkMaxP2 = 2.3f;

    // Kaiser-Kick
    private const float KickWindUp = 0.85f, KickWindUpP2 = 0.6f;
    private const float KickSpread = 24f;           // Phase 2: zwei Kugeln, so weit auseinander (Grad)
    private const float PathLength = 7f, PathWidth = 1.1f;
    private const int KickHit = 4;                  // Bild im Kick-Streifen, in dem der Fuss trifft

    // Frostring
    // Phase 2 holt LAENGER aus als Phase 1: dort jagen gleichzeitig die
    // Lawinenkugeln - man braucht Zeit, sich zur Luecke durchzuschlagen.
    private const float ChargeTime = 0.95f, ChargeTimeP2 = 1.4f;
    private const float HoldTime = 0.45f, HoldTimeP2 = 0.6f;
    private const float RingSpeed = 6.5f, RingSpeedP2 = 6.8f;
    private const float RingRadius = 16f;
    private const int RingGaps = 3;
    private const float GapLength = 2.6f, GapLengthP2 = 2.2f;
    private const float RingDamageFactor = 1.3f;
    private const float FreezeTime = 1.2f, FreezeTimeP2 = 1.4f;
    /// <summary>
    /// Zweiter Ring in Phase 2: erst wenn der erste weit genug draussen ist, dass
    /// man sich zur naechsten Luecke umsetzen kann (vorher war er nicht zu schaffen).
    /// </summary>
    private const float SecondRingDelay = 1.8f;
    private const float SecondLanesAfter = 0.7f;    // Bahnen des zweiten Rings erst dann zeigen
    private const float ComboChanceP2 = 0.25f;

    /// <summary>Bilder im Zauber-Streifen: 0-6 laden, 7-9 halten, 10 Stampfer, 11-13 erholen.</summary>
    private const int CastHoldFirst = 7, CastHoldLast = 9, CastStomp = 10;

    private static readonly Color RageTint = new Color(0.9f, 0.96f, 1f);

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private Sprite[] walk;
    [SerializeField] private Sprite[] kick;
    [SerializeField] private Sprite[] cast;
    [SerializeField] private Sprite[] crystal;
    [SerializeField] private Sprite[] iceBlock;
    [SerializeField] private GameObject snowball;

    // ---------------------------------------------------------------- Zustand

    private Enemy enemy;
    private Rigidbody2D rb;
    private Vector2 velocity;
    private bool phaseTwo;

    private Sprite[] clip;
    private float clipTime, clipFps;
    private bool clipLoop;

    private BossTelegraphMarker marker;
    private readonly System.Collections.Generic.List<EiskaiserLawinenkugel> forming =
        new System.Collections.Generic.List<EiskaiserLawinenkugel>();
    private EiskaiserFrostRing pendingRing;

    private enum Attack { None, Kick, Ring }
    private Attack last = Attack.None;

    // ---------------------------------------------------------------- Ablauf

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
    }

    private void OnEnable()
    {
        StartCoroutine(Brain());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        // Noch nicht losgetretene Kugeln gehen mit dem Kaiser
        foreach (EiskaiserLawinenkugel b in forming) if (b != null) Destroy(b.gameObject);
        forming.Clear();
        if (marker != null) marker.Cancel();
        marker = null;
        if (pendingRing != null) pendingRing.Abort();
        pendingRing = null;
        velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = velocity;
    }

    private void Update()
    {
        if (clip == null || clip.Length == 0 || body == null) return;
        clipTime += Time.deltaTime;
        int i = Mathf.FloorToInt(clipTime * clipFps);
        i = clipLoop ? i % clip.Length : Mathf.Min(i, clip.Length - 1);
        body.sprite = clip[i];
    }

    private IEnumerator Brain()
    {
        yield return Walk(EntryWalk);

        while (true)
        {
            if (!phaseTwo && enemy.HealthFraction <= PhaseTwoAt) yield return EnterPhaseTwo();

            Attack next = last == Attack.Kick ? Attack.Ring : Attack.Kick;
            if (last == Attack.None) next = Attack.Kick;
            if (phaseTwo && Random.value < 0.25f) next = last;          // mal zweimal dasselbe

            if (next == Attack.Kick)
            {
                yield return KaiserKick();
                last = Attack.Kick;

                // Phase 2: manchmal kommt der Ring direkt hinterher
                if (phaseTwo && Random.value < ComboChanceP2)
                {
                    yield return FrostRing();
                    last = Attack.Ring;
                }
            }
            else
            {
                yield return FrostRing();
                last = Attack.Ring;
            }

            yield return Walk(phaseTwo ? Random.Range(WalkMinP2, WalkMaxP2) : Random.Range(WalkMin, WalkMax));
        }
    }

    // ---------------------------------------------------------------- Laufen

    private IEnumerator Walk(float seconds)
    {
        Play(walk, Fps, true);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            PlayerController player = Player();
            if (player != null)
            {
                Vector2 to = (Vector2)player.transform.position - rb.position;
                velocity = to.sqrMagnitude > 0.04f ? to.normalized * enemy.CurrentSpeed : Vector2.zero;
                Face(to.x);
            }
            else
            {
                velocity = Vector2.zero;
            }
            yield return null;
        }
        velocity = Vector2.zero;
    }

    private IEnumerator EnterPhaseTwo()
    {
        phaseTwo = true;
        velocity = Vector2.zero;
        if (SpawnDirector.Active != null)
            SpawnDirector.Active.Say(Loc.Get("boss.eiskaiser.phase2", "DER EISKAISER TOBT!"));
        if (DamageNumberController.Instance != null)
            DamageNumberController.Instance.CreateText(Loc.Get("boss.phase2", "PHASE 2!"), transform.position + Vector3.up * 3.5f);

        EiskaiserSounds.Charge();
        Color from = body.color;
        float t = 0f, dur = 1.1f;
        Play(cast, Fps, false);
        while (t < dur)
        {
            t += Time.deltaTime;
            if (clipTime * Fps > CastHoldLast + 1) clipTime = CastHoldFirst / Fps;       // Krone gluehen lassen
            body.color = Color.Lerp(from, RageTint, t / dur);
            yield return null;
        }
        body.color = RageTint;
        ScreenShake.Kick(4f, 0.35f);
        EiskaiserSounds.Stomp();
        yield return PlayRange(cast, CastStomp, cast.Length - 1);
    }

    // ----------------------------------------------------------- Kaiser-Kick

    private IEnumerator KaiserKick()
    {
        velocity = Vector2.zero;
        PlayerController player = Player();
        if (player == null || snowball == null) yield break;

        Vector2 to = (Vector2)player.transform.position - rb.position;
        Face(to.x);
        float side = body.flipX ? -1f : 1f;
        Vector2 foot = rb.position + new Vector2(side * 0.95f, -0.05f);

        // 1. Kugel formen + ausholen, rote Bahn zeigt, wohin es geht
        forming.Clear();
        int count = phaseTwo ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(snowball, foot + new Vector2(0f, i * 0.05f), Quaternion.identity);
            RunScene.Place(go, "Gegner");
            forming.Add(go.GetComponent<EiskaiserLawinenkugel>());
        }
        Vector2 aim = ((Vector2)player.transform.position - foot);
        aim = aim.sqrMagnitude > 0.01f ? aim.normalized : new Vector2(side, 0f);
        Vector2[] dirs = count == 1
            ? new[] { aim }
            : new[] { (Vector2)(Quaternion.Euler(0f, 0f, KickSpread) * aim), (Vector2)(Quaternion.Euler(0f, 0f, -KickSpread) * aim) };
        var markers = new BossTelegraphMarker[dirs.Length];
        for (int i = 0; i < dirs.Length; i++) markers[i] = BossTelegraph.Path(foot, foot + dirs[i] * PathLength, PathWidth);

        float wind = phaseTwo ? KickWindUpP2 : KickWindUp;
        float t = 0f;
        clip = null;
        while (t < wind)
        {
            t += Time.deltaTime;
            body.sprite = kick[Mathf.Min(KickHit - 1, Mathf.FloorToInt(t / wind * KickHit))];
            foreach (BossTelegraphMarker m in markers) if (m != null) m.SetProgress(t / wind);
            yield return null;
        }
        foreach (BossTelegraphMarker m in markers) if (m != null) m.Impact();

        // 2. Tritt
        body.sprite = kick[KickHit];
        for (int i = 0; i < forming.Count; i++)
        {
            if (forming[i] != null) forming[i].Launch(dirs[Mathf.Min(i, dirs.Length - 1)], phaseTwo);
        }
        forming.Clear();
        ScreenShake.Kick(3f, 0.2f);
        EiskaiserSounds.Kick();

        // 3. Ausschwingen
        yield return PlayRange(kick, KickHit, kick.Length - 1);
    }

    // ------------------------------------------------------------- Frostring

    private IEnumerator FrostRing()
    {
        velocity = Vector2.zero;
        PlayerController player = Player();
        if (player != null) Face(player.transform.position.x - transform.position.x);

        bool second = phaseTwo;
        float gap = phaseTwo ? GapLengthP2 : GapLength;
        float speed = phaseTwo ? RingSpeedP2 : RingSpeed;
        float damage = enemy.ContactDamage * RingDamageFactor;
        float freeze = phaseTwo ? FreezeTimeP2 : FreezeTime;

        // Luecken: gleichmaessig verteilt, Drehung zufaellig
        float rot = Random.value * Mathf.PI * 2f;
        float[] gaps = new float[RingGaps];
        for (int i = 0; i < RingGaps; i++) gaps[i] = rot + i * Mathf.PI * 2f / RingGaps;

        // 1. Laden: Flossen hoch, Krone glueht, sichere Bahnen erscheinen
        Vector2 center = rb.position;
        pendingRing = EiskaiserFrostRing.Prepare(center, gaps, gap, RingRadius, speed, crystal, iceBlock, damage, freeze);
        EiskaiserSounds.Charge();
        float charge = phaseTwo ? ChargeTimeP2 : ChargeTime;
        float t = 0f;
        while (t < charge)
        {
            t += Time.deltaTime;
            body.sprite = cast[Mathf.Min(CastHoldFirst - 1, Mathf.FloorToInt(t / charge * CastHoldFirst))];
            clip = null;
            yield return null;
        }
        float hold = phaseTwo ? HoldTimeP2 : HoldTime;
        Play(cast, Fps, false);
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            body.sprite = cast[CastHoldFirst + Mathf.FloorToInt(t * Fps) % (CastHoldLast - CastHoldFirst + 1)];
            clip = null;
            yield return null;
        }

        // 2. Stampfer
        Stomp(pendingRing);
        pendingRing = null;

        if (second)
        {
            // Zweiter Ring: Luecken genau dazwischen - stehen bleiben reicht nicht
            float[] gaps2 = new float[RingGaps];
            for (int i = 0; i < RingGaps; i++) gaps2[i] = gaps[i] + Mathf.PI / RingGaps;
            yield return PlayRange(cast, CastStomp, CastStomp + 1);
            float w = 0f;
            while (w < SecondRingDelay)
            {
                w += Time.deltaTime;
                if (pendingRing == null && w >= SecondLanesAfter)
                {
                    pendingRing = EiskaiserFrostRing.Prepare(center, gaps2, gap, RingRadius, speed, crystal, iceBlock, damage, freeze);
                    EiskaiserSounds.Charge();
                }
                // Flossen wieder hoch: die Ansage fuer den zweiten Stampfer
                int f = Mathf.Min(CastHoldFirst - 1, Mathf.FloorToInt(w / SecondRingDelay * CastHoldFirst));
                body.sprite = w < SecondRingDelay - 0.3f ? cast[f] : cast[CastHoldFirst + Mathf.FloorToInt(w * Fps) % 3];
                clip = null;
                yield return null;
            }
            Stomp(pendingRing);
            pendingRing = null;
        }

        yield return PlayRange(cast, CastStomp, cast.Length - 1);
    }

    private void Stomp(EiskaiserFrostRing ring)
    {
        body.sprite = cast[CastStomp];
        if (ring != null) ring.Release();
        ScreenShake.Kick(5f, 0.4f);
        EiskaiserSounds.Stomp();
    }

    // ---------------------------------------------------------- Kleinkram

    private void Play(Sprite[] strip, float fps, bool loop)
    {
        clip = strip;
        clipFps = fps;
        clipLoop = loop;
        clipTime = 0f;
        if (strip != null && strip.Length > 0 && body != null) body.sprite = strip[0];
    }

    /// <summary>Spielt die Bilder first..last einmal ab und wartet darauf.</summary>
    private IEnumerator PlayRange(Sprite[] strip, int first, int last)
    {
        clip = null;
        for (int i = first; i <= last && i < strip.Length; i++)
        {
            body.sprite = strip[i];
            yield return new WaitForSeconds(1f / Fps);
        }
    }

    /// <summary>Das Bild schaut nach rechts.</summary>
    private void Face(float dx)
    {
        if (body != null && Mathf.Abs(dx) > 0.05f) body.flipX = dx < 0f;
    }

    private static PlayerController Player()
    {
        PlayerController p = PlayerController.Instance;
        return p != null && p.gameObject.activeSelf ? p : null;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer EiskaiserBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Collider2D collider,
                           Sprite[] walkStrip, Sprite[] kickStrip, Sprite[] castStrip,
                           Sprite[] crystalStrip, Sprite[] blockStrip, GameObject snowballPrefab)
    {
        body = bodyRenderer;
        hitbox = collider;
        walk = walkStrip;
        kick = kickStrip;
        cast = castStrip;
        crystal = crystalStrip;
        iceBlock = blockStrip;
        snowball = snowballPrefab;
    }
#endif
}
