using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Glutwurz - Endboss des Waldes (World2, Szene Map_World3). Ein
/// Baumriese, der Feuer speit. Bilder aus Tools/baumboss.py, der Flammenwurf
/// aus Tools/baumboss_feuer.py (<see cref="FlameBeam"/>), das Prefab baut
/// Tools -> Gegner -> Glutwurz bauen (GlutwurzBuilder).
///
/// Er hat eine Attacke, den Glutatem, und zieht sie in zwei Phasen auf:
///
///   Phase 1 - ueber der Haelfte seiner Leben
///     Glutatem : bleibt stehen und holt kurz Luft (die Glut wird
///                sichtbar ins Maul gesogen). Dabei liegt eine rote Bahn vom
///                Maul zum Spieler, die mitzielt und sich langsam fuellt.
///                Kurz vor Schluss friert sie ein, dann reisst er das Maul auf
///                und speit genau dort entlang Feuer.
///
///   Phase 2 - ab der Haelfte
///     Der Strahl bleibt nicht mehr stehen: er schwenkt dem Spieler hinterher,
///     langsamer als der Spieler um ihn herumlaufen kann. Und wo er gebrannt
///     hat, glimmt danach der Boden - ein paar Glutnester entlang der letzten
///     Linie, die kurz darauf aufplatzen.
///
///   Feuerkreis - egal in welcher Phase
///     Ist er gut 2 Sekunden nicht mehr im Bild (der Spieler laeuft weg),
///     springt er neben den Spieler, bruellt und zieht ein Oval aus Feuer um
///     beide (<see cref="FireArena"/>, 1.5 Kamerabilder breit und hoch). Der
///     Sprung selbst macht keinen Schaden. Das Feuer steht, bis er tot ist -
///     ein zweites Mal weglaufen geht nicht. Die Uhr laeuft erst, wenn man ihn
///     einmal gesehen hat (er kommt von ausserhalb des Bildes herein).
///
/// Drei Sachen sind Absicht (dieselben wie beim Keks-Koenig):
///
///   1. Der Aim-Lock (<see cref="AimLock"/>) liegt VOR dem Feuer. Die Bahn
///      zeigt, wo es brennt - wuerde er bis zuletzt nachzielen, waere das eine
///      Luege.
///   2. Der Schwenk in Phase 2 dreht mit <see cref="SweepTurn"/> Grad pro
///      Sekunde. Bei 6 Einheiten Abstand laeuft der Spieler (4/s) gut 38 Grad
///      pro Sekunde um ihn herum - 30 sind schlagbar, aber nur, wenn man
///      wirklich laeuft.
///   3. Nach jedem Feuer steht er still und schnauft (<see cref="Recover"/>).
///      Das ist das Fenster zum Draufhauen.
///
/// Die Zahlen stehen als Konstanten im Code statt im Inspector - aus
/// demselben Grund wie beim Keks-Koenig: ein Prefab-Neubau soll das Balancing
/// nicht mitnehmen.
///
/// Animation: kein Animator. Er hat acht Bildstreifen (laufen, Luft holen, Maul
/// auf, Feuer - je von vorn und von hinten) und spielt sie selbst ab - die
/// Laengen haengen an den Angriffszeiten, und ein Controller daneben waere
/// eine zweite Uhr. Haengt die Gegner-Werkstatt einen Animator dran, wird er
/// abgeschaltet.
///
/// Steht der Spieler ueber ihm, speit er von hinten gesehen - er dreht sich
/// fuer die Attacke NICHT zur Kamera um. Ob vorn oder hinten, entscheidet sich
/// waehrend des Zielens; ab dem Aim-Lock bleibt es dabei.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Enemy))]
public class EnemyGlutwurz : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float PhaseTwoAt = 0.5f;

    /// <summary>Schwer und langsam - der Spieler laeuft 4.</summary>
    private const float WalkSpeed = 2.2f;
    private const float WalkSpeedPhase2 = 2.9f;

    private const float EntryWalk = 2.5f;

    private const float AttackPause = 4f;
    private const float AttackPausePhase2 = 3f;

    // --- Glutatem
    /// <summary>Luft holen. Der Streifen "charge" (24 Bilder) wird auf genau diese Zeit gestreckt.</summary>
    private const float Windup = 1.3f;

    /// <summary>
    /// So lange vor dem Feuer steht die Bahn fest. Rauslaufen braucht
    /// (halbe Bahnbreite + Spielerradius) / 4 = gut 0.25s - der Rest ist
    /// Reaktionszeit.
    /// </summary>
    private const float AimLock = 0.6f;

    /// <summary>So schnell schiesst der Strahl auf volle Laenge heraus.</summary>
    private const float BeamGrow = 0.1f;

    /// <summary>Wie weit das Feuer reicht (Welt-Einheiten ab Maul).</summary>
    private const float BeamLength = 10f;

    private const float BreathTime = 1.7f;
    private const float BreathTimePhase2 = 2.6f;

    /// <summary>Grad pro Sekunde, mit denen der Strahl in Phase 2 nachschwenkt.</summary>
    private const float SweepTurn = 30f;

    /// <summary>Schaden je Brand-Tick. Der Spieler hat danach ohnehin kurz Schutz.</summary>
    private const float BeamDamage = 5f;
    private const float BeamTick = 0.25f;

    private const float Recover = 1.2f;

    // --- Glutnester (Phase 2)
    private const int NestCount = 4;
    private const float NestRadius = 1.25f;
    private const float NestWindup = 0.9f;
    private const float NestStagger = 0.18f;
    private const float NestDamage = 4f;

    // --- Phasenwechsel
    private const float PhaseTwoRoar = 1.3f;

    // --- Feuerkreis
    /// <summary>So lange darf er ausser Sicht sein, dann springt er.</summary>
    private const float LeapAfterOffscreen = 2f;

    /// <summary>Hat man ihn nie gesehen (Flucht ab Ankunft), zaehlt die Uhr ab dann trotzdem.</summary>
    private const float LeapUnseenAfter = 12f;

    /// <summary>Wie weit er aus dem Bild sein muss - halb drin zaehlt als gesehen.</summary>
    private const float OffscreenMargin = 1f;

    private const float LeapCrouch = 0.3f;
    private const float LeapTime = 0.9f;
    private const float LeapHeight = 3f;

    /// <summary>So weit neben dem Spieler landet er - weit genug, dass er ihn nicht beruehrt.</summary>
    private const float LeapSide = 3f;

    private const float LeapRoar = 1.1f;

    /// <summary>Das Oval ist so viele Kamerabilder breit und hoch.</summary>
    private const float ArenaScreens = 1.5f;

    private const float PlayerRadius = 0.45f;

    /// <summary>Maulmitte ueber dem Pivot (Fuesse) - gemessen von Tools/baumboss.py.</summary>
    private static readonly Vector2 MouthOffset = new Vector2(0f, 0.83f);

    private static readonly Color RageColor = new Color(1f, 0.82f, 0.7f, 1f);

    // ------------------------------------------------------------- Bilder

    [Header("Bilder (setzt GlutwurzBuilder)")]
    [SerializeField] private Sprite[] walkFront;
    [SerializeField] private Sprite[] walkBack;
    [SerializeField] private Sprite[] charge;
    [SerializeField] private Sprite[] roar;
    [SerializeField] private Sprite[] roarLoop;
    [SerializeField] private Sprite[] chargeBack;
    [SerializeField] private Sprite[] roarBack;
    [SerializeField] private Sprite[] roarLoopBack;
    [SerializeField] private FlameBeam beam;

    private const float Fps = 12f;

    // ---------------------------------------------------------------- Zustand

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Enemy enemy;

    private Color baseColor;
    private bool phaseTwo;
    private bool facingBack;

    private Sprite[] clip;
    private float clipFps;
    private bool clipLoop;
    private bool clipReverse;
    private float clipTime;

    private readonly List<BossTelegraphMarker> live = new List<BossTelegraphMarker>();

    private FireArena arena;
    private bool seen;
    private float alive;
    private float offscreen;
    private bool leaping;
    private bool leapRequested;

    public bool IsPhaseTwo => phaseTwo;

    /// <summary>Steht der Feuerkreis schon?</summary>
    public bool HasArena => arena != null;

    /// <summary>
    /// Nur fuer die Test-Szene: springt beim naechsten Halt zwischen zwei
    /// Attacken, auch wenn er gerade im Bild steht. Ein alter Kreis geht dabei aus.
    /// </summary>
    public void RequestLeap()
    {
        leapRequested = true;
    }

    // ------------------------------------------------------------------ Start

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        enemy = GetComponent<Enemy>();

        // Die Werkstatt haengt beim Neubau einen Animator mit dem Laufclip
        // dran - der wuerde jedes Bild ueberschreiben, das wir setzen.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        baseColor = sprite.color;
        if (beam != null) beam.gameObject.SetActive(false);

        Play(walkFront, Fps, true);
        StartCoroutine(Brain());
    }

    private void OnDisable()
    {
        foreach (BossTelegraphMarker marker in live)
        {
            if (marker != null) marker.Cancel();
        }
        live.Clear();
        if (beam != null) beam.gameObject.SetActive(false);

        if (arena != null) arena.Extinguish();
        arena = null;
    }

    private void Update()
    {
        WatchView();

        if (clip == null || clip.Length == 0) return;

        clipTime += Time.deltaTime;
        int i = Mathf.FloorToInt(clipTime * clipFps);
        i = clipLoop ? i % clip.Length : Mathf.Min(i, clip.Length - 1);
        sprite.sprite = clip[clipReverse ? clip.Length - 1 - i : i];
    }

    // ------------------------------------------------------------------ Kopf

    private IEnumerator Brain()
    {
        yield return Walk(EntryWalk);

        while (true)
        {
            if (ShouldLeap)
            {
                yield return Leap();
                continue;
            }

            if (!phaseTwo && enemy.HealthFraction <= PhaseTwoAt)
            {
                yield return EnterPhaseTwo();
            }

            yield return Breath();
            yield return Walk(phaseTwo ? AttackPausePhase2 : AttackPause);
        }
    }

    private IEnumerator EnterPhaseTwo()
    {
        phaseTwo = true;
        Stand();

        if (SpawnDirector.Active != null) SpawnDirector.Active.Say(Loc.Get("boss.glutwurz.phase2", "DIE GLUTWURZ ENTBRENNT!"));
        if (DamageNumberController.Instance != null)
        {
            DamageNumberController.Instance.CreateText(Loc.Get("boss.phase2", "PHASE 2!"), transform.position + Vector3.up * 3f);
        }

        // Bruellen ohne Feuer: Maul auf, die Zungen schlagen schon raus, und
        // das Holz glimmt ab jetzt nach. Kein Angriff - nur die Ansage.
        yield return Roar(PhaseTwoRoar, RageColor);
    }

    /// <summary>
    /// Maul auf, bruellen, Maul zu. Wie beim Feuer bleibt er in der Ansicht,
    /// in der er gerade steht. Mit <paramref name="tint"/> faerbt er sich dabei
    /// dauerhaft um.
    /// </summary>
    private IEnumerator Roar(float seconds, Color? tint = null)
    {
        Sprite[] open = facingBack ? Pick(roarBack, roar) : roar;
        Sprite[] loop = facingBack ? Pick(roarLoopBack, roarLoop) : roarLoop;
        Color from = baseColor;
        Play(open, Fps, false);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            if (t > open.Length / Fps && clip != loop) Play(loop, Fps, true);
            if (tint.HasValue) sprite.color = Color.Lerp(from, tint.Value, t / seconds);
            yield return null;
        }
        if (tint.HasValue) baseColor = tint.Value;

        Play(open, Fps, false, true);
        yield return Hold(open.Length / Fps);
    }

    // ------------------------------------------------------------ Feuerkreis

    private bool ShouldLeap =>
        !leaping && PlayerAlive && (leapRequested || (arena == null && offscreen >= LeapAfterOffscreen));

    /// <summary>
    /// Zaehlt mit, wie lange er ausser Sicht ist. Erst ab dem Moment, in dem
    /// man ihn einmal gesehen hat - er kommt ja von ausserhalb des Bildes.
    /// </summary>
    private void WatchView()
    {
        alive += Time.deltaTime;
        if (arena != null || leaping) return;

        if (OnScreen())
        {
            seen = true;
            offscreen = 0f;
        }
        else if (seen || alive >= LeapUnseenAfter)
        {
            offscreen += Time.deltaTime;
        }
    }

    private bool OnScreen()
    {
        if (!ViewBounds.TryGetWorldRect(out Rect view)) return true;

        // Gemessen an der Koerpermitte, nicht an den Fuessen
        Vector2 body = (Vector2)transform.position + Vector2.up * 1.25f * transform.lossyScale.y;
        view.xMin -= OffscreenMargin;
        view.yMin -= OffscreenMargin;
        view.xMax += OffscreenMargin;
        view.yMax += OffscreenMargin;
        return view.Contains(body);
    }

    /// <summary>
    /// In die Knie, Sprung neben den Spieler (er zielt im Flug nach, landet
    /// also wirklich daneben), Landung, Bruellen - und der Feuerkreis zieht
    /// sich um beide. Waehrend des Flugs ist er nicht anfassbar: der Sprung
    /// macht keinen Schaden.
    /// </summary>
    private IEnumerator Leap()
    {
        leaping = true;
        leapRequested = false;
        if (arena != null) arena.Extinguish();
        arena = null;

        Stand();
        Vector3 scale = transform.localScale;

        // In die Knie
        Play(Walk(facingBack), Fps * 0.3f, true);
        float t = 0f;
        while (t < LeapCrouch)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            float squash = Mathf.Sin(Mathf.Clamp01(t / LeapCrouch) * Mathf.PI * 0.5f) * 0.18f;
            transform.localScale = new Vector3(scale.x * (1f + squash * 0.5f), scale.y * (1f - squash), scale.z);
            yield return null;
        }

        // Landeseite: die, von der er kommt - so springt er nicht ueber den Spieler hinweg
        Vector2 from = rb.position;
        float side = from.x >= PlayerPos.x ? 1f : -1f;
        float height = LeapHeight + Vector2.Distance(from, PlayerPos) * 0.08f;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        Play(Walk(false), 0.01f, false);

        t = 0f;
        Vector2 to = from;
        while (t < LeapTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / LeapTime);
            if (PlayerAlive) to = PlayerPos + new Vector2(side * LeapSide, 0f);

            Vector2 ground = Vector2.Lerp(from, to, k);
            transform.position = ground + Vector2.up * (height * 4f * k * (1f - k));
            float stretch = Mathf.Sin(k * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(scale.x * (1f - stretch * 0.5f), scale.y * (1f + stretch), scale.z);
            yield return null;
        }

        // Landung
        transform.position = to;
        rb.position = to;
        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        facingBack = false;

        t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            float squash = Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI) * 0.15f;
            transform.localScale = new Vector3(scale.x * (1f + squash * 0.5f), scale.y * (1f - squash), scale.z);
            rb.linearVelocity = Vector2.zero;
            yield return null;
        }
        transform.localScale = scale;

        BuildArena(to);
        if (SpawnDirector.Active != null)
        {
            SpawnDirector.Active.Say(Loc.Get("boss.glutwurz.arena", "DIE GLUTWURZ LAESST DICH NICHT ENTKOMMEN!"));
        }

        yield return Roar(LeapRoar);

        offscreen = 0f;
        leaping = false;
    }

    private void BuildArena(Vector2 landing)
    {
        Vector2 center = PlayerAlive ? PlayerPos : landing;

        Vector2 halfAxes = new Vector2(15f, 8.4f);
        if (ViewBounds.TryGetWorldRect(out Rect view))
        {
            halfAxes = new Vector2(view.width, view.height) * (ArenaScreens * 0.5f);
        }

        Sprite[] flames = beam != null ? beam.EndFrames : null;
        // Order 0 wie der Spieler: dann sortiert das Feuer mit ihm nach der Hoehe
        arena = FireArena.Build(center, halfAxes, landing, flames, sprite.sortingLayerName, 0);
    }

    private IEnumerator Walk(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            // Weggelaufen: das Laufen bricht ab, Brain laesst ihn springen
            if (ShouldLeap) yield break;

            left -= Time.deltaTime;

            float speed = phaseTwo ? WalkSpeedPhase2 : WalkSpeed;
            if (PlayerAlive) MoveToward(PlayerPos, speed);
            else rb.linearVelocity = Vector2.zero;

            WalkVisual(speed);
            yield return null;
        }
    }

    // ------------------------------------------------------------- Glutatem

    private IEnumerator Breath()
    {
        if (!PlayerAlive) yield break;

        Stand();

        Vector2 mouth = Mouth;
        Vector2 dir = AimAt(mouth);
        float width = beam != null ? beam.HalfWidth * 2f : 1f;

        BossTelegraphMarker path = Track(BossTelegraph.Path(mouth, mouth + dir * BeamLength, width));

        // --- Luft holen: die Bahn zielt mit, bis der Aim-Lock greift
        bool back = AimsAway(dir, facingBack);
        Play(Charge(back), Charge(back).Length / Windup, false);
        float t = 0f;
        while (t < Windup)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            if (Windup - t > AimLock && PlayerAlive)
            {
                dir = AimAt(mouth);
                path.Aim(mouth, mouth + dir * BeamLength, width);

                // Laeuft der Spieler waehrenddessen ueber oder unter ihn, wechselt
                // die Ansicht im selben Bild weiter - danach steht sie fest
                bool want = AimsAway(dir, back);
                if (want != back)
                {
                    back = want;
                    Play(Charge(back), clipFps, false, false, true);
                }
            }
            path.SetProgress(t / Windup);
            yield return null;
        }
        facingBack = back;

        path.Impact(0.15f);
        Untrack(path);

        // --- Maul auf, Feuer
        Sprite[] open = back ? Pick(roarBack, roar) : roar;
        Sprite[] loop = back ? Pick(roarLoopBack, roarLoop) : roarLoop;
        Play(open, Fps, false);
        StartBeam(dir);

        float duration = phaseTwo ? BreathTimePhase2 : BreathTime;
        float tick = 0f;
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            if (clip == open && clipTime >= open.Length / Fps) Play(loop, Fps, true);

            if (phaseTwo && PlayerAlive)
            {
                // Schwenkt hinterher, aber gedeckelt - wer laeuft, entkommt
                Vector2 want = AimAt(mouth);
                float step = SweepTurn * Time.deltaTime;
                float angle = Mathf.Clamp(Vector2.SignedAngle(dir, want), -step, step);
                dir = Rotate(dir, angle);
                AimBeam(dir);
            }

            tick -= Time.deltaTime;
            if (tick <= 0f && PlayerAlive && beam != null && beam.Hits(PlayerPos, PlayerRadius))
            {
                Hit(BeamDamage);
                tick = BeamTick;
            }
            yield return null;
        }

        // --- Maul zu
        if (beam != null) beam.Stop();
        Play(open, Fps, false, true);

        if (phaseTwo) StartCoroutine(EmberNests(mouth, dir));

        yield return Hold(Recover);
    }

    /// <summary>
    /// Glutnester entlang der letzten Feuerlinie. Laufen nebenher, damit er
    /// waehrenddessen schon wieder losstapfen kann - der Spieler bekommt so
    /// keine Pause geschenkt, aber eine klare Reihenfolge (vom Maul weg).
    /// </summary>
    private IEnumerator EmberNests(Vector2 mouth, Vector2 dir)
    {
        var spots = new List<Vector2>(NestCount);
        var markers = new List<BossTelegraphMarker>(NestCount);
        Vector2 side = new Vector2(-dir.y, dir.x);

        for (int i = 0; i < NestCount; i++)
        {
            float along = BeamLength * (0.3f + 0.7f * i / Mathf.Max(1, NestCount - 1)) * 0.92f;
            Vector2 spot = mouth + dir * along + side * Random.Range(-0.6f, 0.6f);
            spots.Add(spot);
            markers.Add(Track(BossTelegraph.Zone(spot, NestRadius)));
        }

        int open = markers.Count;
        float t = 0f;
        while (open > 0)
        {
            t += Time.deltaTime;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] == null) continue;

                float progress = (t - i * NestStagger) / NestWindup;
                markers[i].SetProgress(progress);
                if (progress < 1f) continue;

                markers[i].Impact();
                Untrack(markers[i]);
                markers[i] = null;
                open--;

                if (PlayerAlive && Vector2.Distance(PlayerPos, spots[i]) <= NestRadius + PlayerRadius)
                {
                    Hit(NestDamage);
                }
            }
            yield return null;
        }
    }

    private void StartBeam(Vector2 dir)
    {
        if (beam == null) return;

        beam.transform.localPosition = MouthOffset;
        beam.Length = BeamLength;
        beam.GrowTime = BeamGrow;
        AimBeam(dir);
        beam.gameObject.SetActive(true);
    }

    private void AimBeam(Vector2 dir)
    {
        if (beam == null) return;

        beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // Nach oben speit er "in die Tiefe": der Strahl liegt dann hinter
        // ihm, sonst ginge er quer durch sein eigenes Gesicht und die Krone.
        bool behind = dir.y > 0.35f;
        beam.SetSorting(sprite.sortingLayerName, sprite.sortingOrder + (behind ? -3 : 1));
    }

    private Vector2 AimAt(Vector2 from)
    {
        Vector2 d = PlayerPos - from;
        return d.sqrMagnitude < 0.0001f ? Vector2.down : d.normalized;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private Vector2 Mouth => (Vector2)transform.position + MouthOffset * transform.lossyScale.y;

    // ------------------------------------------------------------- Werkzeug

    private IEnumerator Hold(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            sprite.color = Color.Lerp(sprite.color, baseColor, Time.deltaTime * 6f);

            // Ist das Maul zu, steht er schwer atmend da: der Laufzyklus in
            // Zeitlupe, die Fuesse heben dabei kaum ab
            if (!clipLoop && clipTime >= clip.Length / clipFps) Play(Walk(facingBack), Fps * 0.3f, true);
            yield return null;
        }
        Play(Walk(facingBack), Fps, true);
    }

    private void Stand()
    {
        rb.linearVelocity = Vector2.zero;
    }

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 delta = target - (Vector2)transform.position;
        rb.linearVelocity = delta.sqrMagnitude > 0.04f ? delta.normalized * speed : Vector2.zero;
    }

    private void Hit(float damage)
    {
        if (!PlayerAlive) return;
        PlayerController.Instance.TakeDamage(damage * RunDifficulty.DamageFactor);
    }

    private static bool PlayerAlive =>
        PlayerController.Instance != null && PlayerController.Instance.gameObject.activeSelf;

    private static Vector2 PlayerPos =>
        PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position
            : Vector2.zero;

    private BossTelegraphMarker Track(BossTelegraphMarker marker)
    {
        if (marker != null) live.Add(marker);
        return marker;
    }

    private void Untrack(BossTelegraphMarker marker)
    {
        live.Remove(marker);
    }

    // ------------------------------------------------------------ Bildwahl

    /// <summary>
    /// Laeuft er nach oben (vom Betrachter weg), sieht man ihn von hinten.
    /// Mit Abstand zwischen den beiden Schwellen, sonst flackert er beim
    /// schraegen Laufen zwischen vorn und hinten.
    /// </summary>
    private void WalkVisual(float speed)
    {
        Vector2 v = rb.linearVelocity;
        float up = v.magnitude > 0.05f ? v.y / v.magnitude : 0f;

        if (!facingBack && up > 0.45f) facingBack = true;
        else if (facingBack && up < 0.15f) facingBack = false;

        Sprite[] want = facingBack && walkBack != null && walkBack.Length > 0 ? walkBack : walkFront;

        // Schritttempo folgt dem Lauftempo - in Phase 2 stapft er schneller
        float fps = Fps * Mathf.Clamp(v.magnitude / WalkSpeed, 0.6f, 1.4f);
        if (clip != want) Play(want, fps, true, false, true);
        else clipFps = fps;

        sprite.color = Color.Lerp(sprite.color, baseColor, Time.deltaTime * 6f);
    }

    /// <summary>
    /// Zielt er vom Betrachter weg (nach oben)? Mit Abstand zwischen den
    /// Schwellen, sonst flackert die Ansicht, wenn der Spieler schraeg steht.
    /// </summary>
    private static bool AimsAway(Vector2 dir, bool wasBack)
    {
        return wasBack ? dir.y > 0.15f : dir.y > 0.4f;
    }

    private Sprite[] Charge(bool back) => back ? Pick(chargeBack, charge) : charge;
    private Sprite[] Walk(bool back) => back ? Pick(walkBack, walkFront) : walkFront;

    private static Sprite[] Pick(Sprite[] wanted, Sprite[] fallback)
    {
        return wanted != null && wanted.Length > 0 ? wanted : fallback;
    }

    /// <param name="keepPhase">Beim Wechsel vorn/hinten im selben Schritt weiterlaufen.</param>
    private void Play(Sprite[] frames, float fps, bool loop, bool reverse = false, bool keepPhase = false)
    {
        if (frames == null || frames.Length == 0) return;

        float phase = keepPhase && clip != null && clipFps > 0f ? clipTime : 0f;
        clip = frames;
        clipFps = Mathf.Max(0.01f, fps);
        clipLoop = loop;
        clipReverse = reverse;
        clipTime = phase;
        sprite.sprite = frames[reverse ? frames.Length - 1 : 0];
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer GlutwurzBuilder: Bilder und Flammenwurf eintragen.</summary>
    public void EditorBind(Sprite[] front, Sprite[] back, Sprite[] chargeFrames,
                           Sprite[] roarFrames, Sprite[] roarLoopFrames,
                           Sprite[] chargeBackFrames, Sprite[] roarBackFrames, Sprite[] roarLoopBackFrames,
                           FlameBeam flame)
    {
        walkFront = front;
        walkBack = back;
        charge = chargeFrames;
        roar = roarFrames;
        roarLoop = roarLoopFrames;
        chargeBack = chargeBackFrames;
        roarBack = roarBackFrames;
        roarLoopBack = roarLoopBackFrames;
        beam = flame;
    }
#endif
}
