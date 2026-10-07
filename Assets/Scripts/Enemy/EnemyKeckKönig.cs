using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Keks-Koenig - Endboss der Kueche (World0). Der Wald hat seit Oktober
/// 2026 einen eigenen, die Glutwurz (<see cref="EnemyGlutwurz"/>).
///
/// Er kaempft in zwei Phasen:
///
///   Phase 1 - ueber der Haelfte seiner Leben
///     Keks-Regen  : bleibt stehen, markiert mehrere Zonen rund um den Spieler,
///                   wirft einen Armvoll Kekse in den Himmel, und die fallen
///                   genau in die Zonen.
///     Keks-Charge : bleibt stehen, markiert eine Bahn und den Einschlagpunkt
///                   rot und sprintet dann einmal durch.
///     Welche der beiden kommt, wird gewuerfelt - aber hoechstens dreimal
///     dieselbe hintereinander, danach kommt zwingend die andere.
///
///   Phase 2 - ab der Haelfte
///     Er bricht auf (gluehende Risse, eigene Bildstreifen fuer alles).
///     Der Regen bleibt unveraendert - der traegt sich allein.
///     Aus der einen Charge werden drei am Stueck, jede mit eigener, kuerzerer
///     Vorwarnung und neu gesetztem Ziel.
///     Neu dazu: der Kronen-Bumerang. Er reisst sich die Krone vom Kopf,
///     die Wurfbahn leuchtet rot auf, und dann fliegt die Krone als
///     rotierende Goldsaege hinaus, schlaegt einen Bogen und kommt auf einem
///     anderen Weg zurueck. Solange sie unterwegs ist, steht er ohne Krone da
///     (Fenster zum Draufhauen), dann faengt er sie mit dem Kopf auf.
///     Gleich nach dem Wutanfall kommt immer zuerst der Bumerang.
///
/// Drei Sachen sind Absicht und sollten beim Balancing nicht verloren gehen:
///
///   1. Das Ziel der Charge friert VOR dem Losrennen ein, siehe
///      <see cref="ChargeAimLock"/>. Wuerde er bis zum letzten Moment
///      nachzielen, waere der Angriff nicht ausweichbar, sondern nur noch
///      aussitzbar - und die rote Bahn waere eine Luege.
///   2. Die Zeit ab Aim-Lock muss laenger sein als der Weg aus der Bahn
///      heraus. Faustregel: halbe Bahnbreite geteilt durch Spielertempo (4).
///      Bei Breite 5.5 sind das rund 0.7s, deshalb steht der Aim-Lock auf
///      0.85s. Wer die Bahn breiter macht, muss ihn mitziehen.
///   3. Nach jeder Attacke steht er kurz still. Das ist das Fenster, in dem
///      der Spieler Schaden macht - ohne das ist der Kampf ein reiner
///      Ausweich-Marathon, bei dem man nie zum Schiessen kommt.
///
/// Die Zahlen stehen als Konstanten im Code statt im Inspector: sonst liegt am
/// Prefab eine zweite Wahrheit daneben, und nach jedem Prefab-Neubau waere das
/// Balancing wieder weg. Das ist dieselbe Linie wie bei den Wellenplaenen.
///
/// Animation: kein Animator. Die Bilder kommen aus Tools/kekskoenig.py
/// (Assets/Art/Gegner/new/boss/kekskoenig_*.png, PPU 32, Pivot = Keksmitte)
/// und haengen als Streifen am Prefab. Dieses Skript spielt sie selbst ab:
/// laufen, stehen, ausholen + zittern, rennen, bremsen, werfen, Wutanfall.
/// Fuer Phase 2 gibt es jeden Streifen nochmal rissig (*P2). Nichts wird
/// skaliert oder eingefaerbt - das wuerde die Pixel verziehen.
///
/// Groesse: das Bildskript kann ihn gross (256er-Zellen) oder mit --klein
/// halb so gross (128er) zeichnen. Was am Koerper haengt - Collider,
/// Charge-Bahn, Kronenhoehe -, liest dieses Skript am Laufbild ab
/// (<see cref="size"/>), am Prefab muss dafuer nichts geaendert werden.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Enemy))]
public class EnemyKeckKönig : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    /// <summary>Ab wie viel Restleben Phase 2 laeuft.</summary>
    private const float PhaseTwoAt = 0.5f;

    private const float WalkSpeed = 3f;

    /// <summary>
    /// Knapp unter dem Spielertempo (4). Absichtlich nicht darueber: er soll
    /// draengen, aber Weglaufen darf nie voellig sinnlos werden.
    /// </summary>
    private const float WalkSpeedPhase2 = 3.8f;

    /// <summary>Laufen, bevor er ueberhaupt das erste Mal angreift.</summary>
    private const float EntryWalk = 2f;

    /// <summary>Pause zwischen zwei Attacken - das Fenster zum Draufhauen.</summary>
    private const float AttackPause = 4.5f;
    private const float AttackPausePhase2 = 3f;

    private const float PhaseTwoRoar = 1.1f;

    /// <summary>
    /// Wie oft dieselbe Attacke hoechstens hintereinander kommen darf.
    /// Danach kommt zwingend die andere.
    /// </summary>
    private const int MaxSameInARow = 3;

    // --- Charge
    private const float ChargeWindup = 1.3f;
    private const float ChargeWindupChain = 0.95f;
    private const float ChargeAimLock = 0.85f;
    private const float ChargeSpeed = 26f;

    /// <summary>
    /// So breit wie sein Koerper samt Armen - die Bahn soll nicht luegen.
    /// Gilt fuer die grosse Fassung, wird mit <see cref="size"/> mitskaliert.
    /// </summary>
    private const float ChargeWidth = 5.5f;

    private const float ChargeOvershoot = 4f;
    private const float ChargeMinLength = 7f;
    private const float ChargeMaxLength = 18f;

    /// <summary>Notbremse, falls er unterwegs an etwas haengen bleibt.</summary>
    private const float ChargeMaxTime = 1.6f;

    private const float ChargeDamage = 5f;
    private const float ChargeImpactRadius = 3.2f;
    private const float ChargeImpactDamage = 3f;
    private const float ChargeRecover = 0.7f;
    private const float ChargeTrailFade = 0.35f;

    private const int ChargeChain = 3;
    private const float ChargeChainGap = 0.3f;

    // --- Keks-Regen
    private const int RainCount = 5;
    private const float RainWindup = 1.25f;
    private const float RainRadius = 2f;
    private const float RainSpread = 5.5f;

    /// <summary>
    /// Mindestabstand zweier Zonen. Ohne den wachsen sie zu einer Wand
    /// zusammen - und genau die Luecke dazwischen IST die Attacke.
    /// </summary>
    private const float RainMinGap = RainRadius * 1.7f;

    private const float RainStagger = 0.14f;
    private const float RainDamage = 4f;
    private const float RainRecover = 0.8f;

    // --- Kronen-Bumerang (nur Phase 2)

    /// <summary>Ausholen bis zum Abwurf. Der Wurf-Streifen wird auf diese Zeit gestreckt.</summary>
    private const float CrownWindup = 1.0f;

    /// <summary>Wie bei der Charge: ab hier zielt er nicht mehr nach.</summary>
    private const float CrownAimLock = 0.55f;

    /// <summary>Hinflug (abbremsend) und Rueckflug im Bogen (beschleunigend).</summary>
    private const float CrownOut = 0.75f;
    private const float CrownBack = 0.95f;

    /// <summary>So weit fliegt sie ueber den Spieler hinaus, und so weit hoechstens/mindestens.</summary>
    private const float CrownOvershoot = 3f;
    private const float CrownMinRange = 6f;
    private const float CrownMaxRange = 13f;

    /// <summary>So weit weicht der Rueckweg seitlich aus - der Spieler muss zweimal ausweichen.</summary>
    private const float CrownCurve = 3.2f;

    /// <summary>Breite der roten Wurfbahn: etwas breiter als die Krone.</summary>
    private const float CrownLaneWidth = 1.9f;

    private const float CrownRadius = 0.8f;
    private const float CrownDamage = 4f;

    /// <summary>Hin- und Rueckweg duerfen beide treffen, aber nicht jeden Frame.</summary>
    private const float CrownHitCooldown = 0.6f;

    /// <summary>Kronenmitte ueber der Keksmitte (Einheiten) - von hier fliegt sie los.</summary>
    private const float CrownHeight = 2.1f;

    private const float CrownSpinFps = 20f;
    private const float CrownThrowFps = 14f;

    /// <summary>Im Wurf-Streifen: bis hierhin wird ausgeholt, in diesem Bild fliegt sie los.</summary>
    private const int CrownWindupFrames = 7;
    private const int CrownReleaseFrame = 8;

    /// <summary>Grober Spielerradius fuer die Treffertests.</summary>
    private const float PlayerRadius = 0.45f;

    // ------------------------------------------------------------- Darstellung

    /// <summary>Zellbreite der grossen Fassung (Pixel) - daran wird <see cref="size"/> gemessen.</summary>
    private const float FullCell = 256f;

    /// <summary>Koerperradius der grossen Fassung (Einheiten): der Keks ohne Arme.</summary>
    private const float BodyRadius = 1.95f;

    private const float Fps = 12f;

    /// <summary>Bei diesem Tempo passt der Lauf-Streifen mit 12 fps zu den Fuessen.</summary>
    private const float WalkFpsAtSpeed = 3f;

    /// <summary>Jeder wievielte Laufzyklus blinzelt.</summary>
    private const int BlinkEvery = 3;

    /// <summary>Ausholen-Streifen: so lange (hoechstens), danach wird gezittert.</summary>
    private const float WindupPose = 0.55f;
    private const float ShiverFps = 16f;
    private const float RunFps = 16f;

    /// <summary>So lange faellt ein Regen-Keks sichtbar herunter, bevor er einschlaegt.</summary>
    private const float CookieFall = 0.45f;

    /// <summary>Von so weit ueber der Zone (Einheiten) faellt er - ueber dem Bildrand.</summary>
    private const float CookieDrop = 7f;

    /// <summary>Kamerawackeln: Charge-Einschlag, Regen-Keks, Wutanfall (Pixel, Sekunden).</summary>
    private const float ShakeCharge = 3f, ShakeChargeTime = 0.3f;
    private const float ShakeRain = 1.5f, ShakeRainTime = 0.15f;
    private const float ShakeRoar = 2f, ShakeRoarTime = 0.7f;

    // ------------------------------------------------------------- Bildstreifen

    [Header("Phase 1")]
    [SerializeField] private Sprite[] walk;
    [SerializeField] private Sprite[] walkBlink;
    [SerializeField] private Sprite[] stand;
    [SerializeField] private Sprite[] windup;
    [SerializeField] private Sprite[] shiver;
    [SerializeField] private Sprite[] run;
    [SerializeField] private Sprite[] brake;
    [SerializeField] private Sprite[] toss;

    [Header("Phase 2 (rissig)")]
    [SerializeField] private Sprite[] walkP2;
    [SerializeField] private Sprite[] walkBlinkP2;
    [SerializeField] private Sprite[] standP2;
    [SerializeField] private Sprite[] windupP2;
    [SerializeField] private Sprite[] shiverP2;
    [SerializeField] private Sprite[] runP2;
    [SerializeField] private Sprite[] brakeP2;
    [SerializeField] private Sprite[] tossP2;

    [Header("Kronen-Bumerang (nur Phase 2)")]
    [SerializeField] private Sprite[] crownThrowP2;
    [SerializeField] private Sprite[] crownlessP2;
    [SerializeField] private Sprite[] crownCatchP2;
    [SerializeField] private Sprite[] crownSpin;

    [Header("Uebergang + Keks-Regen")]
    [SerializeField] private Sprite[] rage;
    [SerializeField] private Sprite[] rainCookie;
    [SerializeField] private Sprite[] rainImpact;

    // ---------------------------------------------------------------- Zustand

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Enemy enemy;
    private bool phaseTwo;

    /// <summary>1 = grosse Fassung, 0.5 = kleine (Tools/kekskoenig.py --klein).</summary>
    private float size = 1f;

    private readonly List<BossTelegraphMarker> live = new List<BossTelegraphMarker>();
    private readonly List<GameObject> liveFx = new List<GameObject>();

    // Was gerade laeuft: Schleife mit fps, oder einmal gestreckt auf eine Dauer
    private Sprite[] clip;
    private bool clipLoops;
    private float clipFps;
    private float clipDuration;
    private float clipTime;
    private bool walking;
    private int walkCycle;

    /// <summary>
    /// Waehrend Charge/Bremsen schaut er in Laufrichtung statt zum Spieler -
    /// sonst dreht er sich mitten im Sprint um, sobald er am Spieler vorbei ist.
    /// 0 = Enemy entscheidet (zum Spieler), -1/1 = fest links/rechts.
    /// </summary>
    private int faceLock;

    /// <summary>Laeuft schon die zweite Phase? Zeigt die Test-Szene an.</summary>
    public bool IsPhaseTwo
    {
        get { return phaseTwo; }
    }

    // ------------------------------------------------------------------ Start

    private void Awake()
    {
        // Das alte Prefab hatte einen Animator mit einem Zustand - der wuerde
        // die Bilder jeden Frame ueberschreiben.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        enemy = GetComponent<Enemy>();

        // Die Physik laeuft mit 50 Hz, der Bildschirm oft mit 144. Ohne
        // Interpolation springt er nur alle paar Bilder weiter, waehrend die
        // Kamera (am interpolierten Spieler) dazwischen weitergleitet - bei
        // seiner Groesse und 26 Einheiten/s im Sprint sieht das verwischt und
        // doppelt aus. Der Spieler hat sie aus demselben Grund an.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Groesse am Bild ablesen und den Koerper passend machen
        if (walk != null && walk.Length > 0 && walk[0] != null) size = walk[0].rect.width / FullCell;
        CircleCollider2D hitbox = GetComponent<CircleCollider2D>();
        if (hitbox != null)
        {
            hitbox.radius = BodyRadius * size;
            hitbox.offset = Vector2.zero;
        }

        StartCoroutine(Brain());
    }

    private void OnDisable()
    {
        // Stirbt er mitten in einer Vorwarnung, ist die Coroutine weg - die
        // Markierungen sind aber eigene GameObjects und blieben sonst als rote
        // Flecken liegen, in denen nie etwas einschlaegt. Danach glaubt einem
        // der Spieler keine Warnung mehr. Dasselbe gilt fuer fallende Kekse.
        foreach (BossTelegraphMarker marker in live)
        {
            if (marker != null) marker.Cancel();
        }
        live.Clear();

        foreach (GameObject fx in liveFx)
        {
            if (fx != null) Destroy(fx);
        }
        liveFx.Clear();
    }

    // ------------------------------------------------------------- Abspielen

    private void Update()
    {
        if (clip == null || clip.Length == 0 || sprite == null) return;

        clipTime += Time.deltaTime;

        int frame;
        if (clipLoops)
        {
            int total = Mathf.FloorToInt(clipTime * clipFps);
            frame = total % clip.Length;

            // Beim Laufen jeden dritten Zyklus blinzeln - umgeschaltet wird nur
            // an der Zyklusgrenze, beide Streifen haben dieselben Fuesse.
            if (walking)
            {
                int cycle = total / clip.Length;
                if (cycle != walkCycle)
                {
                    walkCycle = cycle;
                    Sprite[] wanted = cycle % BlinkEvery == BlinkEvery - 1 ? Pick(walkBlink, walkBlinkP2) : Pick(walk, walkP2);
                    if (wanted != null && wanted.Length == clip.Length) clip = wanted;
                }
            }
        }
        else
        {
            frame = Mathf.Min(clip.Length - 1, Mathf.FloorToInt(clipTime / Mathf.Max(0.01f, clipDuration) * clip.Length));
        }

        sprite.sprite = clip[Mathf.Clamp(frame, 0, clip.Length - 1)];
    }

    private void LateUpdate()
    {
        // Enemy dreht im FixedUpdate zum Spieler - hier gewinnt die Laufrichtung.
        // Gezeichnet ist er nach links schauend.
        if (faceLock != 0 && sprite != null) sprite.flipX = faceLock > 0;
    }

    private void Loop(Sprite[] p1, Sprite[] p2, float fps)
    {
        Sprite[] next = Pick(p1, p2);
        walking = p1 == walk;
        if (next == clip && clipLoops)
        {
            clipFps = fps;
            return;
        }
        if (walking && clip != null && (clip == walkBlink || clip == walkBlinkP2) && clipLoops)
        {
            clipFps = fps;
            return;
        }

        clip = next;
        clipLoops = true;
        clipFps = fps;
        clipTime = 0f;
        walkCycle = 0;
    }

    private void Once(Sprite[] p1, Sprite[] p2, float seconds)
    {
        clip = Pick(p1, p2);
        clipLoops = false;
        clipDuration = seconds;
        clipTime = 0f;
        walking = false;
    }

    private Sprite[] Pick(Sprite[] p1, Sprite[] p2)
    {
        if (phaseTwo && p2 != null && p2.Length > 0) return p2;
        return p1;
    }

    private void FaceToward(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.05f) return;
        faceLock = direction.x > 0f ? 1 : -1;
    }

    // ------------------------------------------------------------------ Kopf

    private IEnumerator Brain()
    {
        // Erst ankommen: der Director setzt ihn ausserhalb des Bildes ab, und
        // wer sofort chargt, chargt aus dem Nichts.
        yield return Walk(EntryWalk);

        // Der Regen faengt an. Er bringt dem Spieler die Lesart
        // "Fuellung voll = Einschlag" bei, ohne ihn dafuer zu ueberfahren -
        // die Charge baut dann auf derselben Sprache auf.
        Attack attack = Attack.Rain;
        int sameInARow = 0;

        while (true)
        {
            if (!phaseTwo && enemy.HealthFraction <= PhaseTwoAt)
            {
                yield return EnterPhaseTwo();

                // Die neue Attacke zeigt sich sofort - das ist der Moment, in
                // dem der Spieler merkt, dass sich der Kampf geaendert hat.
                attack = Attack.Crown;
                sameInARow = 0;
            }

            switch (attack)
            {
                case Attack.Charge: yield return ChargeAttack(); break;
                case Attack.Crown: yield return CrownAttack(); break;
                default: yield return RainAttack(); break;
            }
            sameInARow++;

            yield return Walk(phaseTwo ? AttackPausePhase2 : AttackPause);

            // Gewuerfelt statt stur abgewechselt: wer den Rhythmus einmal raus
            // hat, laeuft den Kampf blind. Die Bremse danach verhindert die
            // andere Richtung - ohne sie kaeme irgendwann die fuenfte Charge am
            // Stueck, und das ist kein Kampf mehr, sondern Pech.
            Attack next = PickAttack(sameInARow >= MaxSameInARow ? attack : (Attack?)null);

            if (next != attack) sameInARow = 0;
            attack = next;
        }
    }

    private enum Attack { Rain, Charge, Crown }

    /// <summary>Gleich verteilt ueber alle Attacken der Phase, ohne die gesperrte.</summary>
    private Attack PickAttack(Attack? not)
    {
        var pool = new List<Attack> { Attack.Rain, Attack.Charge };
        if (phaseTwo && crownThrowP2 != null && crownThrowP2.Length > 0) pool.Add(Attack.Crown);
        if (not.HasValue) pool.Remove(not.Value);
        return pool[Random.Range(0, pool.Count)];
    }

    private IEnumerator EnterPhaseTwo()
    {
        if (SpawnDirector.Active != null) SpawnDirector.Active.Say(Loc.Get("boss.kekskoenig.phase2", "DER KÖNIG WIRD WÜTEND!"));
        if (DamageNumberController.Instance != null)
        {
            DamageNumberController.Instance.CreateText(Loc.Get("boss.phase2", "PHASE 2!"), transform.position);
        }

        // Der Wutanfall ist ausdruecklich kein Angriff: er steht, zittert, die
        // Risse brechen auf, er bruellt. Der Spieler soll den Wechsel sehen,
        // bevor ihn die erste Dreierkette trifft.
        faceLock = 0;
        Once(rage, null, PhaseTwoRoar);

        bool roared = false;
        float t = 0f;
        while (t < PhaseTwoRoar)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            // Ab dem Gebruell (Bild 8 von 16) ist er rissig
            if (!roared && t >= PhaseTwoRoar * 0.5f)
            {
                roared = true;
                phaseTwo = true;
                ScreenShake.Kick(ShakeRoar, ShakeRoarTime);
            }
            yield return null;
        }
        phaseTwo = true;
    }

    private IEnumerator Walk(float seconds)
    {
        faceLock = 0;
        float speed = phaseTwo ? WalkSpeedPhase2 : WalkSpeed;
        Loop(walk, walkP2, Fps * speed / WalkFpsAtSpeed);

        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;

            if (PlayerAlive) MoveToward(PlayerPos, speed);
            else rb.linearVelocity = Vector2.zero;

            yield return null;
        }
    }

    // ---------------------------------------------------------- Keks-Charge

    private IEnumerator ChargeAttack()
    {
        int times = phaseTwo ? ChargeChain : 1;

        for (int i = 0; i < times; i++)
        {
            if (!PlayerAlive) yield break;

            // Die erste Charge bekommt immer die volle Vorwarnung, erst die
            // Wiederholungen ziehen an. So bleibt die Kette lesbar, statt aus
            // dem Nichts zu kommen.
            float windupTime = i == 0 ? ChargeWindup : ChargeWindupChain;
            bool last = i == times - 1;

            yield return Charge(windupTime, last ? ChargeRecover : ChargeChainGap);
        }

        faceLock = 0;
    }

    private IEnumerator Charge(float windupTime, float recover)
    {
        Vector2 origin = transform.position;
        Vector2 aim = ChargeTarget(origin);

        BossTelegraphMarker path = Track(BossTelegraph.Path(origin, aim, ChargeWidth * size));
        BossTelegraphMarker spot = Track(BossTelegraph.Zone(aim, ChargeImpactRadius));

        // --- ausholen: zuruecklehnen, dann mit dem Fuss scharren und dampfen
        float pose = Mathf.Min(WindupPose, windupTime * 0.45f);
        Once(windup, windupP2, pose);
        bool shivering = false;

        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            // Solange mehr als der Aim-Lock uebrig ist, zielt er nach. Danach
            // steht die Bahn fest - das ist das Fenster, in dem der Spieler
            // seitlich rauslaufen kann.
            if (windupTime - t > ChargeAimLock && PlayerAlive)
            {
                origin = transform.position;
                aim = ChargeTarget(origin);
                path.Aim(origin, aim, ChargeWidth * size);
                spot.MoveTo(aim);
            }
            FaceToward(aim - origin);

            if (!shivering && t >= pose)
            {
                shivering = true;
                Loop(shiver, shiverP2, ShiverFps);
            }

            float progress = t / windupTime;
            path.SetProgress(progress);
            spot.SetProgress(progress);
            yield return null;
        }

        // --- losrennen
        // Die Bahn bleibt noch kurz als Schleifspur liegen, statt im selben
        // Frame zu verschwinden - sonst sieht es aus, als waere sie weg,
        // bevor er ueberhaupt losgelaufen ist.
        path.Impact(ChargeTrailFade);
        Untrack(path);

        Vector2 direction = aim - rb.position;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();
        FaceToward(direction);
        Loop(run, runP2, RunFps);

        bool hitAlongPath = false;
        float running = 0f;
        Vector2 previous = rb.position;

        while (running < ChargeMaxTime)
        {
            running += Time.deltaTime;
            rb.linearVelocity = direction * ChargeSpeed;

            yield return null;

            Vector2 now = rb.position;

            // Die Strecke pruefen, nicht nur die Position: bei 26 Einheiten
            // pro Sekunde liegen zwischen zwei Frames gut 0.4 Einheiten, und
            // ein reiner Abstandstest wuerde den Spieler durchrutschen lassen.
            if (!hitAlongPath && SegmentHitsPlayer(previous, now, ChargeWidth * size * 0.5f))
            {
                hitAlongPath = true;
                Hit(ChargeDamage);
            }
            previous = now;

            // Ziel erreicht oder daran vorbei
            if (Vector2.Dot(aim - now, direction) <= 0f) break;
        }

        rb.linearVelocity = Vector2.zero;

        // --- Einschlag am Ende der Bahn: er rutscht, staucht, richtet sich auf
        spot.Impact();
        Untrack(spot);
        ScreenShake.Kick(ShakeCharge, ShakeChargeTime);

        if (PlayerAlive && Vector2.Distance(PlayerPos, transform.position) <= ChargeImpactRadius + PlayerRadius)
        {
            Hit(ChargeImpactDamage);
        }

        // In der Kette ist die Pause kurz - dann nur der Anfang vom Bremsen,
        // schneller abgespielt, und gleich wieder ausholen.
        Once(brake, brakeP2, recover >= ChargeRecover ? recover : recover * 2.5f);
        yield return Hold(recover);
    }

    private Vector2 ChargeTarget(Vector2 origin)
    {
        Vector2 direction = PlayerPos - origin;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        // Er rennt ueber den Spieler hinaus. Wer genau auf ihm stehen bleibt,
        // soll nicht damit belohnt werden, dass der Boss davor abbremst.
        float length = Mathf.Clamp(
            Vector2.Distance(PlayerPos, origin) + ChargeOvershoot,
            ChargeMinLength,
            ChargeMaxLength);

        return origin + direction * length;
    }

    // ------------------------------------------------------ Kronen-Bumerang

    private IEnumerator CrownAttack()
    {
        rb.linearVelocity = Vector2.zero;
        if (!PlayerAlive || crownThrowP2 == null || crownThrowP2.Length <= CrownReleaseFrame) yield break;

        Vector2 head = (Vector2)transform.position + Vector2.up * (CrownHeight * size);
        Vector2 far = CrownTarget(head);
        BossTelegraphMarker lane = Track(BossTelegraph.Path(head, far, CrownLaneWidth * size));

        // --- Krone abnehmen und hinter dem Kopf ausholen (Bilder 0-6)
        Once(SubStrip(crownThrowP2, 0, CrownWindupFrames), null, CrownWindup);

        float t = 0f;
        while (t < CrownWindup)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            // Wie bei der Charge: erst nachzielen, dann steht die Bahn fest
            if (CrownWindup - t > CrownAimLock && PlayerAlive)
            {
                far = CrownTarget(head);
                lane.Aim(head, far, CrownLaneWidth * size);
            }
            FaceToward(far - head);
            lane.SetProgress(t / CrownWindup);
            yield return null;
        }

        // --- Wurf: Rest des Streifens, im Abwurfbild geht sie los
        lane.Impact(ChargeTrailFade);
        Untrack(lane);
        Sprite[] follow = SubStrip(crownThrowP2, CrownWindupFrames, crownThrowP2.Length - CrownWindupFrames);
        Once(follow, null, follow.Length / CrownThrowFps);
        float release = (CrownReleaseFrame - CrownWindupFrames) / CrownThrowFps;
        yield return Hold(release);

        // Bogen zufaellig links- oder rechtsherum
        Vector2 along = (far - head).normalized;
        Vector2 side = new Vector2(-along.y, along.x) * (Random.value < 0.5f ? 1f : -1f);

        SpriteRenderer crown = SpawnFx("Kronen-Bumerang", head, 25);
        float flight = 0f;
        float lastHit = -99f;
        bool waiting = false;
        float followRest = follow.Length / CrownThrowFps - release;

        while (flight < CrownOut + CrownBack)
        {
            flight += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            if (!waiting && flight >= followRest)
            {
                waiting = true;
                Loop(crownlessP2, null, Fps * 0.75f);
            }

            Vector2 pos;
            if (flight < CrownOut)
            {
                // raus: schnell los, zum Wendepunkt hin langsam
                float k = flight / CrownOut;
                pos = Vector2.Lerp(head, far, 1f - (1f - k) * (1f - k));
            }
            else
            {
                // zurueck: im Bogen und immer schneller - er steht still, Ziel ist sein Kopf
                float k = Mathf.Clamp01((flight - CrownOut) / CrownBack);
                float e = k * k;
                pos = Vector2.Lerp(far, head, e) + side * (CrownCurve * Mathf.Sin(e * Mathf.PI));
            }

            if (crown != null)
            {
                crown.transform.position = SnapToPixels(pos);
                if (crownSpin != null && crownSpin.Length > 0)
                {
                    crown.sprite = crownSpin[Mathf.FloorToInt(flight * CrownSpinFps) % crownSpin.Length];
                }
            }

            if (PlayerAlive && flight - lastHit >= CrownHitCooldown
                && Vector2.Distance(PlayerPos, pos) <= CrownRadius * size + PlayerRadius)
            {
                lastHit = flight;
                Hit(CrownDamage);
            }

            yield return null;
        }

        if (crown != null)
        {
            liveFx.Remove(crown.gameObject);
            Destroy(crown.gameObject);
        }

        // --- auffangen: die Krone faellt ihm von oben auf den Kopf
        float catchTime = crownCatchP2 != null && crownCatchP2.Length > 0 ? crownCatchP2.Length / Fps : 0.5f;
        Once(crownCatchP2, null, catchTime);
        yield return Hold(catchTime);

        Loop(stand, standP2, Fps * 0.75f);
        yield return Hold(RainRecover);
        faceLock = 0;
    }

    private static Vector2 CrownTarget(Vector2 from)
    {
        Vector2 direction = PlayerPos - from;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.left;

        // Ueber den Spieler hinaus, wie die Charge: Stehenbleiben ist falsch.
        float range = Mathf.Clamp(direction.magnitude + CrownOvershoot, CrownMinRange, CrownMaxRange);
        return from + direction.normalized * range;
    }

    private static Sprite[] SubStrip(Sprite[] strip, int start, int count)
    {
        if (strip == null) return null;
        start = Mathf.Clamp(start, 0, strip.Length);
        count = Mathf.Clamp(count, 0, strip.Length - start);
        var part = new Sprite[count];
        System.Array.Copy(strip, start, part, 0, count);
        return part;
    }

    // ----------------------------------------------------------- Keks-Regen

    private IEnumerator RainAttack()
    {
        rb.linearVelocity = Vector2.zero;
        faceLock = 0;

        // Werfen: in die Knie, Arme hoch, ein Keks backt ueber der Krone heran,
        // ab damit in den Himmel - und dann ein selbstgefaelliges Lachen.
        Once(toss, tossP2, RainWindup);

        List<Vector2> spots = PickRainSpots(PlayerPos);
        var markers = new List<BossTelegraphMarker>(spots.Count);
        var cookies = new List<SpriteRenderer>(spots.Count);
        foreach (Vector2 spot in spots)
        {
            markers.Add(Track(BossTelegraph.Zone(spot, RainRadius)));
            cookies.Add(null);
        }

        int open = markers.Count;
        float t = 0f;
        bool standing = false;

        while (open > 0)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            if (!standing && t >= RainWindup)
            {
                standing = true;
                Loop(stand, standP2, Fps * 0.75f);
            }

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] == null) continue;

                // Versetzt: die Zonen platzen nacheinander statt alle auf
                // einmal. Das gibt dem Spieler eine Reihenfolge zum Lesen,
                // und das Feld wird nie fuer einen Frame komplett toedlich.
                float hitAt = RainWindup + i * RainStagger;
                float progress = (t - i * RainStagger) / RainWindup;
                markers[i].SetProgress(progress);

                // Der Keks faellt sichtbar in die Zone - landet genau beim Einschlag
                float fall = 1f - (hitAt - t) / CookieFall;
                if (fall >= 0f && fall < 1f)
                {
                    if (cookies[i] == null) cookies[i] = SpawnFx("Regen-Keks", spots[i], 20);
                    ShowCookie(cookies[i], spots[i], fall, t);
                }

                if (progress < 1f) continue;

                markers[i].Impact();
                Untrack(markers[i]);
                markers[i] = null;
                open--;

                if (cookies[i] != null)
                {
                    liveFx.Remove(cookies[i].gameObject);
                    Destroy(cookies[i].gameObject);
                    cookies[i] = null;
                }
                StartCoroutine(PlayImpact(spots[i]));
                ScreenShake.Kick(ShakeRain, ShakeRainTime);

                if (PlayerAlive && Vector2.Distance(PlayerPos, spots[i]) <= RainRadius + PlayerRadius)
                {
                    Hit(RainDamage);
                }
            }

            yield return null;
        }

        if (!standing) Loop(stand, standP2, Fps * 0.75f);
        yield return Hold(RainRecover);
    }

    private static List<Vector2> PickRainSpots(Vector2 center)
    {
        var spots = new List<Vector2>(RainCount);

        // Die erste Zone liegt fast auf dem Spieler: Stehenbleiben soll immer
        // die falsche Antwort sein.
        spots.Add(center + Random.insideUnitCircle.normalized * (RainRadius * 0.3f));

        int guard = 0;
        while (spots.Count < RainCount && guard++ < 300)
        {
            Vector2 candidate = center + Random.insideUnitCircle * RainSpread;

            bool free = true;
            foreach (Vector2 other in spots)
            {
                if (Vector2.Distance(candidate, other) >= RainMinGap) continue;
                free = false;
                break;
            }

            if (free) spots.Add(candidate);
        }

        return spots;
    }

    /// <summary>Faellt beschleunigt (quadratisch) und dreht sich dabei.</summary>
    private void ShowCookie(SpriteRenderer cookie, Vector2 spot, float fall, float time)
    {
        if (cookie == null) return;

        float height = CookieDrop * (1f - fall * fall);
        cookie.transform.position = SnapToPixels(spot + Vector2.up * height);
        if (rainCookie != null && rainCookie.Length > 0)
        {
            cookie.sprite = rainCookie[Mathf.FloorToInt(time * 16f) % rainCookie.Length];
        }
    }

    private IEnumerator PlayImpact(Vector2 spot)
    {
        if (rainImpact == null || rainImpact.Length == 0) yield break;

        // Unter dem Boss, aber ueber dem Boden: sonst laege der Staub auf ihm,
        // wenn eine Zone direkt neben ihm platzt.
        SpriteRenderer fx = SpawnFx("Keks-Einschlag", spot, -1);
        fx.transform.position = SnapToPixels(spot);

        float frameTime = 1f / Fps;
        for (int i = 0; i < rainImpact.Length; i++)
        {
            if (fx == null) yield break;
            fx.sprite = rainImpact[i];
            yield return new WaitForSeconds(frameTime);
        }

        if (fx != null)
        {
            liveFx.Remove(fx.gameObject);
            Destroy(fx.gameObject);
        }
    }

    private SpriteRenderer SpawnFx(string label, Vector2 at, int orderOffset)
    {
        var go = new GameObject(label);
        go.transform.position = at;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = sprite.sharedMaterial;
        renderer.sortingLayerID = sprite.sortingLayerID;
        renderer.sortingOrder = sprite.sortingOrder + orderOffset;
        liveFx.Add(go);
        return renderer;
    }

    private static Vector3 SnapToPixels(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, 0f);
    }

    // ------------------------------------------------------------- Werkzeug

    /// <summary>
    /// Stillstehen - das Fenster, in dem der Spieler Schaden macht. Das Bild
    /// setzt der Aufrufer (Bremsen, Stehen).
    /// </summary>
    private IEnumerator Hold(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            yield return null;
        }
    }

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 delta = target - (Vector2)transform.position;
        rb.linearVelocity = delta.sqrMagnitude > 0.04f ? delta.normalized * speed : Vector2.zero;
    }

    private static bool SegmentHitsPlayer(Vector2 from, Vector2 to, float radius)
    {
        if (!PlayerAlive) return false;

        Vector2 player = PlayerPos;
        Vector2 along = to - from;
        float lengthSquared = along.sqrMagnitude;

        float t = lengthSquared < 0.0001f
            ? 0f
            : Mathf.Clamp01(Vector2.Dot(player - from, along) / lengthSquared);

        return Vector2.Distance(player, from + along * t) <= radius + PlayerRadius;
    }

    private void Hit(float damage)
    {
        if (!PlayerAlive) return;

        // Wie bei den Gegnern haengt auch der Boss am Chaos-Regler, sonst
        // waere er in einem harten Lauf ploetzlich der harmloseste Teil.
        PlayerController.Instance.TakeDamage(damage * RunDifficulty.DamageFactor);
    }

    private static bool PlayerAlive
    {
        get
        {
            return PlayerController.Instance != null
                && PlayerController.Instance.gameObject.activeSelf;
        }
    }

    private static Vector2 PlayerPos
    {
        get
        {
            return PlayerController.Instance != null
                ? (Vector2)PlayerController.Instance.transform.position
                : Vector2.zero;
        }
    }

    private BossTelegraphMarker Track(BossTelegraphMarker marker)
    {
        if (marker != null) live.Add(marker);
        return marker;
    }

    private void Untrack(BossTelegraphMarker marker)
    {
        live.Remove(marker);
    }
}
