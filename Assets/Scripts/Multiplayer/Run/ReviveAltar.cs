using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aussehen des Rettungs-Altars (<see cref="NetRevive"/>): ein grosses
/// Keksglas auf einem Holzuntersetzer, Deckel wie ein Keks, um den Hals eine
/// Schleife in der Farbe des Toten. Im Glas schwebt seine echte Figur (die
/// Lauf-Animation, kein Portraet), drumherum steigen Blasen auf, unten
/// leuchtet ein Lichthof. Um das Glas liegt der grosse Kreis, der sich mit
/// der Rettung fuellt - ebenfalls in seiner Farbe.
///
/// Je weiter die Rettung, desto heller das Glas; bei einem Drittel und zwei
/// Dritteln springt es. Steht jemand im Kreis, ruckelt es und die Blasen
/// sprudeln. Geschafft: der Deckel fliegt ab, das Glas zerspringt in
/// Scherben, die Figur huepft heraus (und der echte Spieler steht an ihrer
/// Stelle). Geht der Spieler ganz, verblasst alles.
///
/// Alles per Code gemalt, PPU 32. Die Mechanik steht in <see cref="NetRevive"/>.
/// </summary>
public class ReviveAltar : MonoBehaviour
{
    private const float PPU = 32f;
    private const string SortingLayer = "Objects";
    private const int JarW = 40, JarH = 58;
    private const float FeetY = 9f / PPU;        // Boden im Glas
    private const float MaxBody = 30f / PPU;     // so hoch darf die Figur im Glas hoechstens sein
    private const float Bob = 1.5f / PPU;
    private const float ShatterTime = 1.1f;
    private const float FadeTime = 0.9f;

    private SpriteRenderer glow, back, figure, front, ribbon, lid, crack1, crack2, field;
    private FieldRing ring;
    private Animator anim;
    private readonly List<SpriteRenderer> bubbles = new List<SpriteRenderer>();
    private readonly List<Vector4> bubbleState = new List<Vector4>();   // x, Startphase, Tempo, Wackeln
    private readonly List<SpriteRenderer> shards = new List<SpriteRenderer>();
    private readonly List<Vector4> shardState = new List<Vector4>();    // vx, vy, Drehung, -
    private Sprite[] portrait;
    private Color accent;
    private float progress;
    private bool active;
    private float releasedAt = -1f;
    private bool rescued;
    private float figureScale = 1f;
    private int footRows;

    public static ReviveAltar Create(Vector2 position, float radius, int character)
    {
        var go = new GameObject("ReviveAltar");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        ReviveAltar altar = go.AddComponent<ReviveAltar>();
        altar.Build(radius, character);
        RunScene.Place(go, "Mixer");
        return altar;
    }

    private void Build(float radius, int character)
    {
        accent = CharacterLooks.AccentFor(character);
        footRows = CharacterLooks.FootRowsFor(character);
        Color light = Color.Lerp(accent, Color.white, 0.5f);

        field = NewPart("Field", -3);
        ring = new FieldRing(field, radius, 5)
        {
            Empty = new Color32(0x22, 0x1a, 0x2a, 150),
            Full = WithAlpha(accent, 235),
            Hot = WithAlpha(light, 255),
            Inner = WithAlpha(accent, 20),
        };
        ring.Paint(0f, true);

        glow = NewPart("Glow", -2);
        glow.sprite = GlowSprite();
        glow.transform.localPosition = new Vector3(0f, 2f / PPU, 0f);

        back = NewPart("JarBack", 0);
        back.sprite = Jar.Back;

        figure = NewPart("Figure", 1);
        RuntimeAnimatorController controller = CharacterLooks.AnimatorFor(character);
        if (controller != null)
        {
            anim = figure.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else portrait = CharacterLooks.PortraitFor(character);

        PlayerSkinSwitcher skin = PlayerSkinSwitcher.Instance;
        figureScale = skin != null && skin.animator != null ? Mathf.Abs(skin.animator.transform.lossyScale.x) : 1f;
        if (figureScale < 0.01f) figureScale = 1f;

        for (int i = 0; i < 6; i++)
        {
            SpriteRenderer b = NewPart("Bubble" + i, 2);
            b.sprite = i % 2 == 0 ? Jar.BubbleBig : Jar.BubbleSmall;
            bubbles.Add(b);
            bubbleState.Add(new Vector4(Random.Range(-11f, 11f), Random.value, Random.Range(0.6f, 1.2f), Random.Range(0f, 6.3f)));
        }

        front = NewPart("JarFront", 3);
        front.sprite = Jar.Front;
        crack1 = NewPart("Crack1", 4);
        crack1.sprite = Jar.Crack1;
        crack1.enabled = false;
        crack2 = NewPart("Crack2", 4);
        crack2.sprite = Jar.Crack2;
        crack2.enabled = false;

        ribbon = NewPart("Ribbon", 5);
        ribbon.sprite = Jar.Ribbon;
        ribbon.color = accent;

        lid = NewPart("Lid", 5);
        lid.sprite = Jar.Lid;
        lid.transform.localPosition = new Vector3(0f, 47f / PPU, 0f);
    }

    private SpriteRenderer NewPart(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sortingLayerName = SortingLayer;
        r.sortingOrder = order;
        return r;
    }

    private static Color32 WithAlpha(Color c, byte a)
    {
        Color32 c32 = c;
        c32.a = a;
        return c32;
    }

    private void OnDestroy() => ring?.Destroy();

    /// <summary>Fortschritt 0..1, und ob gerade jemand im Kreis steht.</summary>
    public void SetProgress(float value, bool someoneInside)
    {
        progress = Mathf.Clamp01(value);
        active = someoneInside;
    }

    /// <summary>Glas wird frei: gerettet (zerspringt) oder verlassen (verblasst).</summary>
    public void Release(bool wasRescued)
    {
        if (releasedAt >= 0f) return;
        releasedAt = Time.unscaledTime;
        rescued = wasRescued;
        if (!wasRescued) return;

        progress = 1f;
        // Scherben fliegen aus dem Glas.
        for (int i = 0; i < 14; i++)
        {
            SpriteRenderer s = NewPart("Shard" + i, 6);
            s.sprite = Jar.Shards[i % Jar.Shards.Length];
            float ang = (i / 14f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector2 at = new Vector2(Mathf.Cos(ang) * 12f, 26f + Mathf.Sin(ang) * 16f) / PPU;
            s.transform.localPosition = at;
            shards.Add(s);
            shardState.Add(new Vector4(Mathf.Cos(ang) * Random.Range(2.5f, 4.5f), Random.Range(2f, 5f), Random.Range(-540f, 540f), 0f));
        }
        back.enabled = front.enabled = crack1.enabled = crack2.enabled = false;
        foreach (SpriteRenderer b in bubbles) b.enabled = false;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (releasedAt >= 0f)
        {
            if (rescued) UpdateShatter(now - releasedAt, dt);
            else UpdateFade(now - releasedAt);
            return;
        }

        ring.Paint(progress);

        // Glas: ruckelt, wenn jemand im Kreis steht; springt bei 1/3 und 2/3.
        int shake = active && Mathf.Repeat(now * 2.3f, 1f) < 0.08f ? (Mathf.FloorToInt(now * 40f) % 2 == 0 ? 1 : -1) : 0;
        Vector3 jarPos = new Vector3(shake / PPU, 0f, 0f);
        back.transform.localPosition = front.transform.localPosition = ribbon.transform.localPosition = jarPos;
        crack1.transform.localPosition = crack2.transform.localPosition = jarPos;
        crack1.enabled = progress >= 0.34f;
        crack2.enabled = progress >= 0.67f;

        // Der Deckel klappert, je naeher die Rettung.
        float rattle = active && progress > 0.5f && Mathf.Repeat(now * 3.1f, 1f) < 0.1f ? 1f : 0f;
        lid.transform.localPosition = new Vector3(jarPos.x, (47f + rattle) / PPU, 0f);

        // Lichthof: atmet, wird mit dem Fortschritt heller.
        float breathe = 0.5f + 0.5f * Mathf.Sin(now * 2.2f);
        glow.color = new Color(accent.r, accent.g, accent.b, 0.35f + 0.35f * progress + 0.15f * breathe);
        front.color = Color.Lerp(Color.white, Color.Lerp(accent, Color.white, 0.6f), progress * 0.5f);

        UpdateFigure(now, 0f, 1f);
        UpdateBubbles(now, dt);
    }

    private void UpdateFigure(float now, float lift, float alpha)
    {
        Sprite frame = null;
        if (anim != null)
        {
            anim.SetBool("moving", active);
            anim.SetFloat("MoveX", 0f);
            anim.SetFloat("MoveY", -1f);
            anim.SetFloat("LastMoveX", 0f);
            anim.SetFloat("LastMoveY", -1f);
            frame = figure.sprite;
        }
        else if (portrait != null)
        {
            frame = portrait[Mathf.FloorToInt(now * 6f) % portrait.Length];
            figure.sprite = frame;
        }
        if (frame == null) return;

        // Fuesse auf den Glasboden, Koerper mittig - gemessen am Koerper, nicht am Frame.
        Rect body = CharacterLooks.BodyRect(frame);
        float ppu = frame.pixelsPerUnit;
        float scale = figureScale;
        float bodyH = body.height / ppu * scale;
        if (bodyH > MaxBody) scale *= MaxBody / bodyH;
        float feet = (body.y + (anim != null ? footRows : 0) - frame.pivot.y) / ppu * scale;
        float midX = (body.center.x - frame.pivot.x) / ppu * scale;

        float bob = Mathf.Round(Mathf.Sin(now * 1.8f) * 1.5f) / PPU;
        figure.transform.localScale = new Vector3(scale, scale, 1f);
        figure.transform.localPosition = new Vector3(-midX + back.transform.localPosition.x, FeetY - feet + bob + lift, 0f);
        figure.color = new Color(1f, 1f, 1f, alpha);
    }

    private void UpdateBubbles(float now, float dt)
    {
        float speed = active ? 2.2f : 1f;
        for (int i = 0; i < bubbles.Count; i++)
        {
            Vector4 st = bubbleState[i];
            st.y = Mathf.Repeat(st.y + dt * 0.35f * st.z * speed, 1f);
            bubbleState[i] = st;
            float y = Mathf.Lerp(9f, 42f, st.y);
            float x = st.x + Mathf.Round(Mathf.Sin(now * 3f + st.w) * 1f);
            bubbles[i].transform.localPosition = new Vector3(Mathf.Round(x) / PPU, Mathf.Round(y) / PPU, 0f);
            float a = st.y < 0.1f ? st.y / 0.1f : st.y > 0.85f ? (1f - st.y) / 0.15f : 1f;
            bubbles[i].color = new Color(1f, 1f, 1f, 0.75f * a);
        }
    }

    private void UpdateShatter(float t, float dt)
    {
        float k = Mathf.Clamp01(t / ShatterTime);
        field.enabled = false;
        ribbon.enabled = false;

        // Deckel fliegt hoch und dreht sich weg.
        float lt = Mathf.Min(t, 0.7f);
        lid.transform.localPosition = new Vector3(lt * 1.2f, 47f / PPU + lt * 6f - lt * lt * 9f, 0f);
        lid.transform.localRotation = Quaternion.Euler(0f, 0f, -lt * 400f);
        lid.color = new Color(1f, 1f, 1f, Steps(1f - k));

        for (int i = 0; i < shards.Count; i++)
        {
            Vector4 st = shardState[i];
            st.y -= 14f * dt;
            shardState[i] = st;
            Transform s = shards[i].transform;
            s.localPosition += new Vector3(st.x, st.y, 0f) * dt;
            s.localRotation = Quaternion.Euler(0f, 0f, st.z * t);
            shards[i].color = new Color(1f, 1f, 1f, Steps(1f - k));
        }

        // Die Figur huepft heraus und macht dem echten Spieler Platz.
        float hop = Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI) * 0.6f;
        UpdateFigure(Time.unscaledTime, hop, Steps(1f - Mathf.Clamp01((t - 0.3f) / 0.4f)));
        glow.color = new Color(accent.r, accent.g, accent.b, 0.8f * Steps(1f - k));

        if (k >= 1f) Destroy(gameObject);
    }

    private void UpdateFade(float t)
    {
        float k = Mathf.Clamp01(t / FadeTime);
        float a = Steps(1f - k);
        field.enabled = false;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = r.color;
            c.a = Mathf.Min(c.a, a);
            r.color = c;
        }
        if (k >= 1f) Destroy(gameObject);
    }

    /// <summary>Ausblenden in vier harten Stufen - weich passt nicht zu Pixelkunst.</summary>
    private static float Steps(float v) => Mathf.Round(Mathf.Clamp01(v) * 4f) / 4f;

    // ==================================================================
    //  Bilder
    // ==================================================================

    private static Sprite glowSprite;

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int w = 60, h = 16;
        var c = new Canvas32(w, h);
        c.Ellipse(w / 2f, h / 2f, w / 2f, h / 2f, new Color32(255, 255, 255, 40));
        c.Ellipse(w / 2f, h / 2f, w * 0.36f, h * 0.36f, new Color32(255, 255, 255, 80));
        c.Ellipse(w / 2f, h / 2f, w * 0.2f, h * 0.2f, new Color32(255, 255, 255, 130));
        glowSprite = c.ToSprite("ReviveGlow", new Vector2(0.5f, 0.5f));
        return glowSprite;
    }

    /// <summary>Kleine Pixel-Leinwand, y = 0 unten.</summary>
    private sealed class Canvas32
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public Canvas32(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        public Color32 Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? default : Px[y * W + x];

        public void Fill(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
        }

        /// <summary>Abgerundetes Rechteck (Ecken mit Radius r), Grenzen einschliesslich.</summary>
        public bool InRounded(int x, int y, int x0, int y0, int x1, int y1, int r)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return false;
            int cx = x < x0 + r ? x0 + r : x > x1 - r ? x1 - r : x;
            int cy = y < y0 + r ? y0 + r : y > y1 - r ? y1 - r : y;
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= (r + 0.35f) * (r + 0.35f);
        }

        public void Rounded(int x0, int y0, int x1, int y1, int r, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (InRounded(x, y, x0, y0, x1, y1, r)) Set(x, y, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>1-px-Kontur um alles Gemalte (nur in leere Pixel).</summary>
        public void Outline(Color32 c)
        {
            var copy = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a != 0) continue;
                    bool edge = (x > 0 && copy[y * W + x - 1].a != 0) || (x < W - 1 && copy[y * W + x + 1].a != 0)
                             || (y > 0 && copy[(y - 1) * W + x].a != 0) || (y < H - 1 && copy[(y + 1) * W + x].a != 0);
                    if (edge) Px[y * W + x] = c;
                }
        }

        public Sprite ToSprite(string name, Vector2 pivot)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            tex.SetPixels32(Px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), pivot, PPU);
        }
    }

    /// <summary>Die Teile des Keksglases, einmal gemalt und fuer alle Glaeser geteilt.</summary>
    private static class Jar
    {
        // Glas: Koerper x 4..35, y 7..44, Radius 7; Hals x 8..31, y 45..47.
        private const int X0 = 4, X1 = 35, Y0 = 7, Y1 = 44, R = 7;

        private static readonly Color32 Edge = new Color32(0x2c, 0x4a, 0x60, 255);
        private static readonly Color32 EdgeLight = new Color32(0xa6, 0xdc, 0xf2, 200);
        private static readonly Color32 GlassBack = new Color32(0xb4, 0xe1, 0xf7, 40);
        private static readonly Color32 GlassLow = new Color32(0x6f, 0xb7, 0xe0, 70);
        private static readonly Color32 Shine = new Color32(255, 255, 255, 190);
        private static readonly Color32 ShineSoft = new Color32(255, 255, 255, 110);
        private static readonly Color32 Dough = new Color32(0xd9, 0xa0, 0x5b, 255);
        private static readonly Color32 DoughLight = new Color32(0xf0, 0xc9, 0x8a, 255);
        private static readonly Color32 DoughDark = new Color32(0xb0, 0x74, 0x3a, 255);
        private static readonly Color32 Chip = new Color32(0x5a, 0x34, 0x21, 255);
        private static readonly Color32 Ink = GameHudSkin.Ink;

        private static Sprite back, front, lidSprite, ribbonSprite, c1, c2, bigBubble, smallBubble;
        private static Sprite[] shards;

        private static readonly Vector2 Pivot = new Vector2(0.5f, 0f);

        public static Sprite Back
        {
            get
            {
                if (back != null) return back;
                var c = new Canvas32(JarW, JarH);
                // Schatten und Untersetzer (Holzscheibe)
                c.Ellipse(20f, 2.5f, 19f, 2.5f, new Color32(0, 0, 0, 90));
                c.Rounded(1, 2, 38, 7, 2, Ink);
                c.Rounded(2, 3, 37, 6, 1, GameHudSkin.Wood);
                c.Fill(3, 6, 36, 6, GameHudSkin.WoodLight);
                c.Fill(3, 3, 36, 3, GameHudSkin.WoodDark);
                // Glaswand hinten: fast durchsichtig, unten etwas dichter
                for (int y = Y0; y <= Y1; y++)
                    for (int x = X0; x <= X1; x++)
                        if (c.InRounded(x, y, X0, Y0, X1, Y1, R)) c.Set(x, y, y < Y0 + 5 ? GlassLow : GlassBack);
                // hinterer Rand der Oeffnung
                c.Fill(9, 45, 30, 46, new Color32(0x6f, 0xb7, 0xe0, 90));
                back = c.ToSprite("JarBack", Pivot);
                return back;
            }
        }

        public static Sprite Front
        {
            get
            {
                if (front != null) return front;
                var c = new Canvas32(JarW, JarH);
                // Kontur des Koerpers
                for (int y = Y0 - 1; y <= Y1 + 1; y++)
                    for (int x = X0 - 1; x <= X1 + 1; x++)
                    {
                        bool outer = c.InRounded(x, y, X0 - 1, Y0 - 1, X1 + 1, Y1 + 1, R + 1);
                        bool inner = c.InRounded(x, y, X0, Y0, X1, Y1, R);
                        if (outer && !inner) c.Set(x, y, Edge);
                    }
                // Hals: kurzes Stueck Glas zwischen Schulter und Deckel
                c.Fill(7, 45, 32, 47, new Color32(0xb4, 0xe1, 0xf7, 70));
                c.Fill(7, 45, 7, 47, Edge);
                c.Fill(32, 45, 32, 47, Edge);
                // Innenkante links hell
                for (int y = Y0 + R; y <= Y1 - R; y++) c.Set(X0 + 1, y, EdgeLight);
                // Glanz: langer Streifen links, kurzer rechts oben, Bogen unten
                c.Fill(7, 15, 8, 38, Shine);
                c.Set(7, 40, Shine);
                c.Set(7, 41, Shine);
                c.Fill(31, 33, 31, 39, ShineSoft);
                c.Fill(11, 9, 17, 9, ShineSoft);
                // Glanzpunkt am Hals
                c.Set(9, 46, Shine);
                front = c.ToSprite("JarFront", Pivot);
                return front;
            }
        }

        /// <summary>Deckel wie ein Schokokeks, mit Knauf. Pivot unten mittig (sitzt auf dem Hals).</summary>
        public static Sprite Lid
        {
            get
            {
                if (lidSprite != null) return lidSprite;
                var c = new Canvas32(JarW, 12);
                c.Rounded(5, 1, 34, 6, 2, Dough);
                c.Rounded(16, 6, 23, 9, 1, Dough);
                c.Fill(7, 6, 32, 6, DoughLight);
                c.Fill(17, 9, 22, 9, DoughLight);
                c.Fill(6, 1, 33, 1, DoughDark);
                c.Set(10, 4, Chip); c.Set(11, 4, Chip); c.Set(15, 2, Chip);
                c.Set(21, 4, Chip); c.Set(22, 3, Chip); c.Set(28, 4, Chip); c.Set(29, 2, Chip);
                c.Set(19, 7, Chip);
                c.Outline(Ink);
                lidSprite = c.ToSprite("JarLid", new Vector2(0.5f, 1f / 12f));
                return lidSprite;
            }
        }

        /// <summary>Schleife um den Hals, weiss gemalt - getoent in der Farbe des Charakters.</summary>
        public static Sprite Ribbon
        {
            get
            {
                if (ribbonSprite != null) return ribbonSprite;
                var c = new Canvas32(JarW, JarH);
                var white = new Color32(255, 255, 255, 255);
                var shade = new Color32(190, 190, 200, 255);
                c.Fill(7, 44, 32, 46, white);
                c.Fill(7, 44, 32, 44, shade);
                // Schlaufen und Knoten vorn
                c.Rounded(13, 44, 18, 48, 2, white);
                c.Rounded(21, 44, 26, 48, 2, white);
                c.Fill(15, 46, 16, 46, shade);
                c.Fill(23, 46, 24, 46, shade);
                c.Fill(19, 44, 20, 47, shade);
                // Enden haengen herab
                c.Fill(16, 40, 17, 43, white);
                c.Fill(22, 40, 23, 43, white);
                c.Set(16, 39, white);
                c.Set(23, 39, white);
                c.Outline(Ink);
                ribbonSprite = c.ToSprite("JarRibbon", Pivot);
                return ribbonSprite;
            }
        }

        public static Sprite Crack1 => c1 != null ? c1 : c1 = Crack("JarCrack1", new[] { (12, 34), (14, 31), (12, 28), (15, 25), (14, 22) });

        public static Sprite Crack2 => c2 != null ? c2 : c2 = Crack("JarCrack2", new[] { (29, 41), (27, 38), (29, 35), (26, 32) },
                                                                      new[] { (22, 12), (24, 15), (23, 18), (26, 20) });

        private static Sprite Crack(string name, params (int x, int y)[][] lines)
        {
            var c = new Canvas32(JarW, JarH);
            var col = new Color32(240, 252, 255, 230);
            foreach (var line in lines)
            {
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    var a = line[i];
                    var b = line[i + 1];
                    int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                    for (int s = 0; s <= steps; s++)
                    {
                        float t = steps == 0 ? 0f : s / (float)steps;
                        c.Set(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)), col);
                    }
                }
            }
            return c.ToSprite(name, Pivot);
        }

        public static Sprite BubbleBig
        {
            get
            {
                if (bigBubble != null) return bigBubble;
                var c = new Canvas32(4, 4);
                var rim = new Color32(230, 248, 255, 220);
                c.Set(1, 0, rim); c.Set(2, 0, rim); c.Set(0, 1, rim); c.Set(0, 2, rim);
                c.Set(3, 1, rim); c.Set(3, 2, rim); c.Set(1, 3, rim); c.Set(2, 3, rim);
                c.Set(1, 2, new Color32(255, 255, 255, 255));
                bigBubble = c.ToSprite("JarBubble", new Vector2(0.5f, 0.5f));
                return bigBubble;
            }
        }

        public static Sprite BubbleSmall
        {
            get
            {
                if (smallBubble != null) return smallBubble;
                var c = new Canvas32(2, 2);
                c.Fill(0, 0, 1, 1, new Color32(230, 248, 255, 200));
                smallBubble = c.ToSprite("JarBubbleSmall", new Vector2(0.5f, 0.5f));
                return smallBubble;
            }
        }

        public static Sprite[] Shards
        {
            get
            {
                if (shards != null) return shards;
                string[][] shapes =
                {
                    new[] { "#..", "o#.", "oo#", "###" },
                    new[] { ".#.", "#o#", "#o#", ".#." },
                    new[] { "##", "o#" },
                    new[] { "###.", ".oo#", "..##" },
                };
                shards = new Sprite[shapes.Length];
                for (int i = 0; i < shapes.Length; i++)
                {
                    string[] rows = shapes[i];
                    int h = rows.Length, w = rows[0].Length;
                    var c = new Canvas32(w, h);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            char ch = rows[h - 1 - y][x];
                            if (ch == '#') c.Set(x, y, new Color32(0xa6, 0xdc, 0xf2, 255));
                            else if (ch == 'o') c.Set(x, y, new Color32(240, 252, 255, 230));
                        }
                    shards[i] = c.ToSprite("JarShard" + i, new Vector2(0.5f, 0.5f));
                }
                return shards;
            }
        }
    }
}

/// <summary>
/// Pfeil mit Totenkopf ueber dem eigenen Spieler, der zum naechsten
/// Keksglas (<see cref="ReviveAltar"/>) zeigt, solange der nicht im Bild ist - wie
/// <see cref="BossPointer"/>, nur blau mit Totenkopf.
/// </summary>
public class AltarPointer : MonoBehaviour
{
    private const int PPU = 32;
    private const float Radius = 0.75f;
    private const float SkullGap = 0.42f;
    private const float HeadHeight = 1.1f;
    private const float Bob = 0.08f;
    private const float FadeSpeed = 6f;
    private const float ShowOutside = 0.08f;
    private const float HideInside = 0.06f;

    private SpriteRenderer arrow, skull;
    private float alpha;
    private bool showing;

    public static AltarPointer Create(Transform parent)
    {
        var go = new GameObject("AltarPointer");
        go.transform.SetParent(parent, false);
        return go.AddComponent<AltarPointer>();
    }

    private void Awake()
    {
        arrow = NewRenderer("Arrow", BuildArrow());
        skull = NewRenderer("Skull", BuildSkull());
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingLayerName = "Foreground";
        r.sortingOrder = 500;
        r.enabled = false;
        return r;
    }

    /// <summary>Jeden Frame von <see cref="NetRevive"/>: wer schaut, wohin (null = nichts zeigen).</summary>
    public void Track(PlayerController player, Vector2? target)
    {
        Camera cam = Camera.main;
        if (player == null || target == null || cam == null) showing = false;
        else if (showing) showing = !InView(cam, target.Value, HideInside);
        else showing = !InView(cam, target.Value, -ShowOutside);

        alpha = Mathf.MoveTowards(alpha, showing ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
        if (alpha <= 0f || player == null || target == null)
        {
            arrow.enabled = skull.enabled = false;
            return;
        }

        Vector2 head = (Vector2)player.transform.position + Vector2.up * HeadHeight;
        Vector2 dir = target.Value - head;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        float bob = (Mathf.Sin(Time.unscaledTime * 8f) * 0.5f + 0.5f) * Bob;
        Vector2 pos = head + dir * (Radius + bob);
        arrow.transform.position = new Vector3(pos.x, pos.y, 0f);
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        // Der Totenkopf sitzt hinter dem Pfeil (zur Figur hin) und dreht sich nicht mit.
        Vector2 sp = pos - dir * SkullGap;
        skull.transform.position = new Vector3(sp.x, sp.y, 0f);

        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        arrow.color = skull.color = new Color(1f, 1f, 1f, alpha * pulse);
        arrow.enabled = skull.enabled = true;
    }

    private static bool InView(Camera cam, Vector3 world, float margin)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
    }

    // ---------- Bilder ----------

    // Pfeil zeigt nach rechts (+x); gedreht wird per Transform.
    private static readonly string[] ArrowShape =
    {
        "....##.....",
        "....#o#....",
        "....#oo#...",
        "#####ooo#..",
        "#lllllooo#.",
        "#oooooooo#.",
        "#####ooo#..",
        "....#oo#...",
        "....#o#....",
        "....##.....",
    };

    private static readonly string[] SkullShape =
    {
        "..#####..",
        ".#lllll#.",
        "#lllllll#",
        "#l##l##l#",
        "#l##l##l#",
        "#lllolll#",
        ".#lllll#.",
        "..#l#l#..",
        "...###...",
    };

    private static Sprite BuildArrow() => Paint(ArrowShape, "AltarArrow", new Vector2(5f, 5f),
        new Color32(0x6f, 0xb7, 0xe0, 0xFF), new Color32(0xb4, 0xe1, 0xf7, 0xFF));

    private static Sprite BuildSkull() => Paint(SkullShape, "AltarSkull", new Vector2(4f, 4f),
        new Color32(0x3b, 0x2b, 0x33, 0xFF), new Color32(0xff, 0xf4, 0xe0, 0xFF));

    /// <param name="fill">Farbe fuer 'o'.</param>
    /// <param name="light">Farbe fuer 'l'.</param>
    private static Sprite Paint(string[] shape, string name, Vector2 pivotPx, Color32 fill, Color32 light)
    {
        int h = shape.Length, w = shape[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        var px = new Color32[w * h];
        Color32 outline = new Color32(0x2A, 0x10, 0x18, 0xFF);
        for (int y = 0; y < h; y++)
        {
            string row = shape[h - 1 - y];
            for (int x = 0; x < w; x++)
            {
                switch (row[x])
                {
                    case '#': px[y * w + x] = outline; break;
                    case 'o': px[y * w + x] = fill; break;
                    case 'l': px[y * w + x] = light; break;
                    default: px[y * w + x] = new Color32(0, 0, 0, 0); break;
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotPx.x / w, pivotPx.y / h), PPU);
    }
}
