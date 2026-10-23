using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Das Knusperhaus - Zwischenboss des Geisterwalds. Ein Lebkuchenhaus auf zwei
/// Haehnchenkeulen (Baba Jaga trifft Haensel und Gretel), die Fenster sind
/// gluehende Augen, die Tuer ein Maul, aus dem Schornstein steigt
/// Geisterrauch. Leuchtet im Dunkeln ueber eine EnemyGlowLayer. Prefab:
/// Tools -> Gegner -> Zwischenbosse bauen (ZwischenbossBuilder).
///
///   Spukspur  es geht in die Hocke, die Tuer reisst auf, lila Licht, es heult -
///             und ruft den GEIST DES SPIELERS: ein lila Abbild seiner eigenen
///             Figur, das genau seinen Weg nachlaeuft, ein paar Sekunden
///             versetzt. Leuchtende Fussspuren zeigen, wo der Geist gleich
///             entlangkommt. Wer stehen bleibt oder im Kreis laeuft, holt
///             sich selbst ein.
///
/// Das Haus merkt sich dafuer die ganze Zeit, wo der Spieler die letzten
/// Sekunden war (Ringpuffer). Ab der Haelfte kommt ein zweiter Geist hinterher.
/// </summary>
public class EnemyKnusperhaus : SelfAnimatedBoss
{
    // ------------------------------------------------------------- Balancing

    private const float FirstWalk = 4f;
    private const float WalkMin = 6.5f, WalkMax = 8.5f;
    private const float WalkMinPhase2 = 5f, WalkMaxPhase2 = 6.5f;

    /// <summary>So weit liegt der Geist hinter dem Spieler (Sekunden Weg).</summary>
    private const float GhostDelay = 1.6f;

    /// <summary>Der zweite Geist ab der Haelfte.</summary>
    private const float SecondGhostDelay = 3.0f;

    /// <summary>So lange spukt ein Geist.</summary>
    private const float GhostTime = 6.5f;

    private const float GhostHitRadius = 0.5f;
    private const float GhostDamageFactor = 1.2f;

    /// <summary>Alle so viele Sekunden Weg eine Fussspur.</summary>
    private const float StepEvery = 0.22f;

    /// <summary>Aufzeichnung: Bilder pro Sekunde und wie lange zurueck.</summary>
    private const float RecordRate = 30f;
    private const float RecordSeconds = 4f;

    /// <summary>Im Ruf-Streifen: ab diesem Bild ist die Tuer offen, die Geister kommen.</summary>
    private const int SummonFrame = 4;

    private static readonly Color GhostColor = new Color(0.78f, 0.55f, 1f, 0.72f);

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private Sprite[] walk;
    [SerializeField] private Sprite[] call;
    [SerializeField] private Sprite[] step;
    [SerializeField] private Sprite[] wisp;
    [SerializeField] private Material glowMaterial;

    protected override Sprite[] WalkStrip => walk;

    private struct Snapshot
    {
        public float time;
        public Vector2 pos;        // Spieler (Treffer)
        public Vector2 spritePos;  // sein Bild (darf versetzt sein)
        public Sprite sprite;
        public bool flipX;
    }

    private Snapshot[] ring;
    private int ringHead;
    private int ringCount;
    private float recordClock;
    private SpriteRenderer playerSprite;

    private class Ghost
    {
        public SpriteRenderer r;
        public float delay;
        public float age;
        public float wispClock;
    }

    private class Footprint
    {
        public SpriteRenderer r;
        public float time;     // wann der Spieler hier war
        public float fade = -1f;
        public bool live;
    }

    private class Wisp
    {
        public SpriteRenderer r;
        public float age;
        public bool live;
        public Vector2 pos;
    }

    private readonly List<Ghost> ghosts = new List<Ghost>();
    private readonly List<Footprint> prints = new List<Footprint>();
    private readonly List<Wisp> wisps = new List<Wisp>();
    private float hauntUntil;
    private float lastStepTime;
    private int stepSide;
    private Vector2 lastPrintPos = new Vector2(1e6f, 1e6f);

    // ---------------------------------------------------------------- Ablauf

    protected override void Awake()
    {
        base.Awake();
        ring = new Snapshot[Mathf.CeilToInt(RecordRate * RecordSeconds) + 2];
    }

    protected override IEnumerator Run()
    {
        yield return Walk(FirstWalk);
        while (true)
        {
            if (Player != null) yield return Haunt();
            bool p2 = IsPhaseTwo;
            yield return Walk(p2 ? Random.Range(WalkMinPhase2, WalkMaxPhase2) : Random.Range(WalkMin, WalkMax));
        }
    }

    private IEnumerator Haunt()
    {
        FacePlayer();
        yield return PlayOnce(call, 0, SummonFrame - 1);

        SquiddySounds.Wooo();
        SpawnGhost(GhostDelay);
        if (IsPhaseTwo) SpawnGhost(SecondGhostDelay);
        hauntUntil = Time.time + GhostTime + SecondGhostDelay;
        Backfill(Time.time - (IsPhaseTwo ? SecondGhostDelay : GhostDelay));

        yield return PlayOnce(call, SummonFrame);
    }

    protected override void Update()
    {
        base.Update();
        Record();
        UpdateGhosts();
        UpdatePrints();
        UpdateWisps();
    }

    // ------------------------------------------------------------ Aufzeichnen

    private void Record()
    {
        Transform player = Player;
        if (player == null) return;
        // Koop: anderer Spieler am naechsten - dessen Bild aufzeichnen.
        if (playerSprite != null && !playerSprite.transform.IsChildOf(player)) playerSprite = null;

        recordClock += Time.deltaTime;
        if (recordClock < 1f / RecordRate) return;
        recordClock = 0f;

        if (playerSprite == null) playerSprite = FindPlayerSprite(player);

        var s = new Snapshot
        {
            time = Time.time,
            pos = player.transform.position,
            spritePos = playerSprite != null ? (Vector2)playerSprite.transform.position : (Vector2)player.transform.position,
            sprite = playerSprite != null ? playerSprite.sprite : null,
            flipX = playerSprite != null && playerSprite.flipX,
        };
        ringHead = (ringHead + 1) % ring.Length;
        ring[ringHead] = s;
        ringCount = Mathf.Min(ringCount + 1, ring.Length);

        // Waehrend es spukt: Fussspuren auf den Weg, den die Geister gleich gehen
        // (steht er still, bleibt es bei einer Spur)
        if (Time.time < hauntUntil && Time.time - lastStepTime >= StepEvery && (s.pos - lastPrintPos).sqrMagnitude > 0.04f)
        {
            lastStepTime = Time.time;
            stepSide = 1 - stepSide;
            Vector2 side = new Vector2(0f, stepSide == 0 ? 0.09f : -0.09f);
            SpawnPrint(s.pos + side, s.time);
        }
    }

    /// <summary>Spuren fuer den Weg, den der Spieler schon hinter sich hat - da laeuft der Geist zuerst.</summary>
    private void Backfill(float since)
    {
        float next = since;
        for (int k = ringCount - 1; k >= 0; k--)
        {
            Snapshot s = ring[(ringHead - k + ring.Length) % ring.Length];
            if (s.time < next) continue;
            next = s.time + StepEvery;
            stepSide = 1 - stepSide;
            SpawnPrint(s.pos + new Vector2(0f, stepSide == 0 ? 0.09f : -0.09f), s.time);
        }
        lastStepTime = Time.time;
    }

    /// <summary>Wo war der Spieler zum Zeitpunkt t? Nimmt den naechstaelteren Eintrag.</summary>
    private bool Lookup(float t, out Snapshot snap)
    {
        snap = default;
        if (ringCount == 0) return false;
        for (int k = 0; k < ringCount; k++)
        {
            Snapshot s = ring[(ringHead - k + ring.Length) % ring.Length];
            if (s.time <= t)
            {
                snap = s;
                return true;
            }
            snap = s; // aeltester bekannter Stand, falls t davor liegt
        }
        return true;
    }

    private static SpriteRenderer FindPlayerSprite(Transform player)
    {
        PlayerHitFeedback fb = player.GetComponent<PlayerHitFeedback>();
        if (fb != null && fb.playerSpriteRenderer != null) return fb.playerSpriteRenderer;
        return player.GetComponentInChildren<SpriteRenderer>();
    }

    // ---------------------------------------------------------------- Geister

    private void SpawnGhost(float delay)
    {
        var g = new Ghost { r = NewEffectRenderer("Spukgeist", 14, glowMaterial), delay = delay };
        g.r.color = new Color(GhostColor.r, GhostColor.g, GhostColor.b, 0f);
        ghosts.Add(g);
    }

    private void UpdateGhosts()
    {
        Transform player = Player;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            g.age += Time.deltaTime;

            // Ein- und ausblenden
            float a = Mathf.Clamp01(g.age / 0.35f) * Mathf.Clamp01((GhostTime - g.age) / 0.45f);
            if (g.age >= GhostTime || player == null)
            {
                for (int k = 0; k < 6; k++) SpawnWisp((Vector2)g.r.transform.position + Random.insideUnitCircle * 0.4f);
                Kill(g.r);
                ghosts.RemoveAt(i);
                continue;
            }

            if (!Lookup(Time.time - g.delay, out Snapshot s) || s.sprite == null) continue;

            g.r.enabled = true;
            g.r.sprite = s.sprite;
            g.r.flipX = s.flipX;
            // leichtes Schweben
            Vector2 at = s.spritePos + new Vector2(0f, 0.06f * Mathf.Sin(g.age * 6f));
            g.r.transform.position = Snap(at);
            Color c = GhostColor;
            c.a *= a * (0.85f + 0.15f * Mathf.Sin(g.age * 13f));
            g.r.color = c;

            g.wispClock += Time.deltaTime;
            if (g.wispClock > 0.09f)
            {
                g.wispClock = 0f;
                SpawnWisp(at + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.1f, 0.3f)));
            }

            // Treffer erst, wenn er richtig da ist
            // Jeder Spieler, der dem Geist in die Quere kommt (im Koop auch Mitspieler).
            if (a > 0.6f) BossTargets.HurtInCircle(s.pos, GhostHitRadius, enemy.ContactDamage * GhostDamageFactor);
        }
    }

    // ---------------------------------------------------------------- Fussspuren

    private void SpawnPrint(Vector2 at, float time)
    {
        if (step == null || step.Length == 0) return;
        Footprint p = null;
        foreach (Footprint x in prints) if (!x.live && x.r != null) { p = x; break; }
        if (p == null)
        {
            p = new Footprint { r = NewEffectRenderer("Spukspur", -1, glowMaterial) };
            prints.Add(p);
        }
        p.time = time;
        p.fade = -1f;
        p.live = true;
        p.r.enabled = true;
        p.r.sprite = step[0];
        p.r.transform.position = Snap(at);
        lastPrintPos = at;
    }

    private void UpdatePrints()
    {
        // Eine Spur verblasst, sobald der langsamste Geist sie erreicht hat.
        float slowest = float.MaxValue;
        foreach (Ghost g in ghosts) slowest = Mathf.Min(slowest, Time.time - g.delay);
        if (ghosts.Count == 0) slowest = float.MaxValue;

        foreach (Footprint p in prints)
        {
            if (!p.live) continue;
            if (p.fade < 0f && (p.time <= slowest || Time.time > hauntUntil)) p.fade = 0f;
            if (p.fade < 0f) continue;
            p.fade += Time.deltaTime;
            int i = 1 + Mathf.FloorToInt(p.fade * 14f);
            if (i >= step.Length)
            {
                p.live = false;
                p.r.enabled = false;
                continue;
            }
            p.r.sprite = step[i];
        }
    }

    // ---------------------------------------------------------------- Irrlichter

    private void SpawnWisp(Vector2 at)
    {
        if (wisp == null || wisp.Length == 0) return;
        Wisp w = null;
        foreach (Wisp x in wisps) if (!x.live && x.r != null) { w = x; break; }
        if (w == null)
        {
            w = new Wisp { r = NewEffectRenderer("Irrlicht", 13, glowMaterial) };
            wisps.Add(w);
        }
        w.age = 0f;
        w.live = true;
        w.pos = at;
        w.r.enabled = true;
        w.r.sprite = wisp[0];
        w.r.transform.position = Snap(at);
    }

    private void UpdateWisps()
    {
        foreach (Wisp w in wisps)
        {
            if (!w.live) continue;
            w.age += Time.deltaTime;
            int i = Mathf.FloorToInt(w.age * 12f);
            if (i >= wisp.Length)
            {
                w.live = false;
                w.r.enabled = false;
                continue;
            }
            w.pos += Vector2.up * 0.5f * Time.deltaTime;
            w.r.transform.position = Snap(w.pos);
            w.r.sprite = wisp[i];
        }
    }

    protected override void Cleanup()
    {
        foreach (Ghost g in ghosts) Kill(g.r);
        foreach (Footprint p in prints) Kill(p.r);
        foreach (Wisp w in wisps) Kill(w.r);
        ghosts.Clear();
        prints.Clear();
        wisps.Clear();
        hauntUntil = 0f;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer ZwischenbossBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Sprite[] walkStrip, Sprite[] callStrip, Sprite[] stepStrip,
                           Sprite[] wispStrip, Material unlit)
    {
        body = bodyRenderer;
        walk = walkStrip;
        call = callStrip;
        step = stepStrip;
        wisp = wispStrip;
        glowMaterial = unlit;
    }
#endif
}
