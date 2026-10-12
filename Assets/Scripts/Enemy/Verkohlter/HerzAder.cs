using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Schlagader des Herzens (Endkampf, <see cref="VerkohlterHerzFinale"/>):
/// solange das Herz gepanzert ist, brechen sie am Rand der Kammer aus dem
/// Boden. Jede haengt ueber eine gluehende Ader im Boden am Herzen und pumpt
/// bei jedem BUM Glut hinein - das Herz heilt sich. Wer den Panzer brechen
/// will, muss raus zu den Adern und sie zerschiessen.
///
/// Die Ader ist ein echter Gegner ohne Prefab (<see cref="Enemy.ConfigureRuntime"/>),
/// damit die Waffen sie anvisieren. Bei Spieler-Naehe spuckt sie beim bumm ein
/// paar Funken.
///
/// Bild: herz_ader (0-9 Ausbruch, 10-25 Pochen im Herzschlag, 26-35 Platzen).
/// </summary>
public class HerzAder : MonoBehaviour
{
    private const int EmergeFrames = 10, PulseFirst = 10, BurstFirst = 26, BurstFrames = 10;
    private const float Fps = 12f;
    private const float SpitRange = 7f;
    private const float SpitSpeed = 3.6f;

    /// <summary>Knoten (Glutsack) ueber dem Bodenpunkt - von hier pumpt die Glut los.</summary>
    private static readonly Vector2 KnotOffset = new Vector2(0f, 1.35f);

    private VerkohlterHerzFinale finale;
    private Enemy enemy;
    private SpriteRenderer body;
    private Sprite[] frames;
    private float age;
    private bool emerged, bursting;
    private float burstAge;
    private float spitDamage;

    // Die Ader im Boden: Pixel entlang einer geschwungenen Linie zum Herzen.
    private readonly List<SpriteRenderer> vein = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> veinEdge = new List<SpriteRenderer>();
    private readonly List<Vector2> veinPos = new List<Vector2>();
    private float pulse = -1f;

    /// <summary>Pfeil am Bildrand, solange die Ader ausserhalb des Bildes steht.</summary>
    private SpriteRenderer arrow;
    private static Sprite arrowSprite;
    private static readonly Color32 VeinEdge = new Color32(10, 6, 8, 255);

    public bool Alive => this != null && !bursting;
    public Enemy Enemy => enemy;

    public static HerzAder Spawn(VerkohlterHerzFinale finale, Vector2 at, Vector2 heart, float hp, float contactDamage,
                                 float spitDamage)
    {
        var go = new GameObject("Schlagader");
        go.SetActive(false);
        RunScene.Place(go, "Effekte");
        go.transform.position = VerkohlterArt.Snap(at);
        int layer = LayerMask.NameToLayer("Enemy");
        if (layer >= 0) go.layer = layer;
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.8f;
        col.offset = new Vector2(0f, 0.85f);
        col.enabled = false;

        var art = new GameObject("Bild");
        art.transform.SetParent(go.transform, false);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Objects";
        sr.sortingOrder = 1;

        Enemy e = go.AddComponent<Enemy>();
        e.ConfigureRuntime(hp, contactDamage, 0f, 40);

        var ader = go.AddComponent<HerzAder>();
        ader.finale = finale;
        ader.enemy = e;
        ader.body = sr;
        ader.spitDamage = spitDamage;
        ader.frames = VerkohlterArt.Strip("herz_ader");
        sr.sprite = VerkohlterArt.Frame(ader.frames, 0);
        ader.BuildVein(at, heart);

        go.SetActive(true);
        e.SelfSteered = true;
        e.Untouchable = true;
        e.HoldDeath = true;
        e.SkipDeathEffect = true;
        e.DeathHeld += ader.OnKilled;
        return ader;
    }

    private void BuildVein(Vector2 from, Vector2 heart)
    {
        // Die Ader laeuft unter dem Becken her zum Herzen - endet am Beckenrand.
        Vector2 to = heart + (from - heart).normalized * 1.6f + Vector2.down * 1.2f;
        Vector2 side = Vector2.Perpendicular(to - from).normalized * Random.Range(-1.6f, 1.6f);
        Vector2 ctrl = (from + to) * 0.5f + side;
        float len = Vector2.Distance(from, to);
        // Alle 2 Bildpixel ein Stueck: 3 px heller Kern auf 5 px dunkler Kontur -
        // eine durchgehende Linie, die sich vom Boden mit den orangen Rissen abhebt.
        int n = Mathf.Max(8, Mathf.RoundToInt(len * 32f / 2f));
        int background = SortingLayer.NameToID("Background");
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector2 p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * ctrl + t * t * to;
            p += Vector2.Perpendicular(to - from).normalized * Mathf.Sin(t * 19f) * 0.06f;
            veinEdge.Add(VeinPixel(background, -41, 5f));
            vein.Add(VeinPixel(background, -40, 3f));
            veinPos.Add(p);
        }
    }

    private SpriteRenderer VeinPixel(int layer, int order, float size)
    {
        var go = new GameObject("Ader");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = VerkohlterArt.Pixel;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.transform.localScale = new Vector3(size, size, 1f);
        sr.enabled = false;
        return sr;
    }

    /// <summary>Pixel-Sprite hat den Pivot unten links - um die halbe Groesse zurueck, damit Kern und Kontur mittig liegen.</summary>
    private void PlaceVein(int i)
    {
        Vector2 p = VerkohlterArt.Snap(veinPos[i]);
        veinEdge[i].transform.position = p - Vector2.one * (2f / VerkohlterArt.PixelsPerUnit);
        vein[i].transform.position = p - Vector2.one * (1f / VerkohlterArt.PixelsPerUnit);
    }

    /// <summary>BUM: Glut laeuft die Ader entlang zum Herzen.</summary>
    public void Pump()
    {
        if (emerged && !bursting) pulse = 0f;
    }

    /// <summary>bumm: ist der Spieler nah, spuckt die Ader drei Funken nach ihm.</summary>
    public void Spit(HerzGeschosse shots)
    {
        if (!emerged || bursting || shots == null) return;
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeSelf) return;
        Vector2 knot = (Vector2)transform.position + KnotOffset;
        Vector2 to = (Vector2)pc.transform.position - knot;
        if (to.magnitude > SpitRange) return;
        float a = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        for (int k = -1; k <= 1; k++)
            shots.Fire(knot, HerzGeschosse.Dir(a + k * 16f) * SpitSpeed, HerzGeschosse.Art.Funke, spitDamage);
        VerkohlterSounds.Shot();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (bursting)
        {
            burstAge += Time.deltaTime;
            int f = Mathf.FloorToInt(burstAge * Fps);
            if (f >= BurstFrames)
            {
                if (enemy != null) enemy.FinishHeldDeath();
                else Destroy(gameObject);
                return;
            }
            body.sprite = VerkohlterArt.Frame(frames, BurstFirst + f);
            FadeVein(1f - burstAge * Fps / BurstFrames);
            return;
        }

        if (!emerged)
        {
            int f = Mathf.FloorToInt(age * Fps);
            body.sprite = VerkohlterArt.Frame(frames, Mathf.Min(f, EmergeFrames - 1));
            // Die Ader im Boden glueht von der Schlagader zum Herzen hin auf.
            float grow = Mathf.Clamp01(age * Fps / EmergeFrames);
            for (int i = 0; i < vein.Count; i++)
            {
                bool on = i <= grow * vein.Count;
                vein[i].enabled = on;
                veinEdge[i].enabled = on;
                if (on)
                {
                    vein[i].color = VerkohlterArt.Fire[4];
                    veinEdge[i].color = VeinEdge;
                }
                PlaceVein(i);
            }
            if (f >= EmergeFrames)
            {
                emerged = true;
                enemy.Untouchable = false;
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = true;
            }
            return;
        }

        int beat = finale != null ? finale.BeatFrame : Mathf.FloorToInt(age * Fps) % 16;
        body.sprite = VerkohlterArt.Frame(frames, PulseFirst + beat);
        DrawVein();
        PointArrow();
    }

    /// <summary>Steht die Ader ausserhalb des Bildes, zeigt ein blinkender Pfeil am Rand zu ihr.</summary>
    private void PointArrow()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        if (arrow == null)
        {
            var go = new GameObject("AderPfeil");
            go.transform.SetParent(transform, false);
            arrow = go.AddComponent<SpriteRenderer>();
            arrow.sprite = ArrowSprite;
            arrow.sortingLayerName = "Foreground";
            arrow.sortingOrder = 60;
        }
        Vector2 c = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector2 target = (Vector2)transform.position + KnotOffset * 0.6f;
        Vector2 d = target - c;
        bool outside = Mathf.Abs(d.x) > halfW - 0.3f || Mathf.Abs(d.y) > halfH - 0.3f;
        arrow.enabled = outside && Mathf.Repeat(Time.time * 3f, 1f) < 0.75f;
        if (!outside) return;
        float inX = halfW - 0.55f, inY = halfH - 0.55f;
        float k = Mathf.Min(inX / Mathf.Max(0.001f, Mathf.Abs(d.x)), inY / Mathf.Max(0.001f, Mathf.Abs(d.y)));
        arrow.transform.position = VerkohlterArt.Snap(c + d * k);
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    /// <summary>Kleiner Glutpfeil (11x11, zeigt nach rechts), dunkel umrandet.</summary>
    private static Sprite ArrowSprite
    {
        get
        {
            if (arrowSprite != null) return arrowSprite;
            const int n = 11;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "HerzAderPfeil",
            };
            var px = new Color32[n * n];
            Color32 fill = VerkohlterArt.Fire[5], hot = VerkohlterArt.Fire[6];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int dy = Mathf.Abs(y - 5);
                    bool head = x >= 4 && x <= 9 && dy <= (9 - x) + 0.5f;
                    bool shaft = x >= 1 && x < 5 && dy <= 1;
                    px[y * n + x] = head || shaft ? (dy == 0 && x < 8 ? hot : fill) : new Color32(0, 0, 0, 0);
                }
            }
            var outl = (Color32[])px.Clone();
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (px[y * n + x].a > 0) continue;
                    bool near = false;
                    for (int oy = -1; oy <= 1 && !near; oy++)
                        for (int ox = -1; ox <= 1 && !near; ox++)
                        {
                            int xx = x + ox, yy = y + oy;
                            if (xx >= 0 && xx < n && yy >= 0 && yy < n && px[yy * n + xx].a > 0) near = true;
                        }
                    if (near) outl[y * n + x] = VeinEdge;
                }
            }
            tex.SetPixels32(outl);
            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), VerkohlterArt.PixelsPerUnit);
            return arrowSprite;
        }
    }

    private void DrawVein()
    {
        if (pulse >= 0f) pulse += Time.deltaTime * 1.4f;
        if (pulse > 1.3f) pulse = -1f;
        Color32[] fire = VerkohlterArt.Fire;
        for (int i = 0; i < vein.Count; i++)
        {
            float t = i / (float)Mathf.Max(1, vein.Count - 1);
            int k = 3 + ((i / 4) % 5 == 0 ? 1 : 0);
            if (pulse >= 0f)
            {
                float d = Mathf.Abs(t - pulse);
                if (d < 0.04f) k = 6;
                else if (d < 0.09f) k = 5;
                else if (d < 0.16f) k = 3;
            }
            vein[i].color = fire[k];
            PlaceVein(i);
        }
    }

    private void FadeVein(float a)
    {
        if (arrow != null) arrow.enabled = false;
        for (int i = 0; i < vein.Count; i++)
        {
            Color c = VerkohlterArt.Fire[1];
            c.a = Mathf.Clamp01(a);
            if (vein[i] != null) vein[i].color = c;
            Color e = VeinEdge;
            e.a = Mathf.Clamp01(a);
            if (veinEdge[i] != null) veinEdge[i].color = e;
        }
    }

    private void OnKilled()
    {
        if (bursting) return;
        bursting = true;
        burstAge = 0f;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        VerkohlterSounds.Pop();
        ScreenShake.Kick(3f, 0.25f);
        if (finale != null) finale.OnArteryBurst(this);
    }

    /// <summary>Ohne Beute weg (Herz tot, Kampf vorbei).</summary>
    public void Remove()
    {
        if (enemy != null) enemy.DeathHeld -= OnKilled;
        Destroy(gameObject);
    }
}
