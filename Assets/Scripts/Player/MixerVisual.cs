using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Neues Aussehen fuer den Mixer und sein Feld - Pixel fuer Pixel per Code
/// gemalt, in der Palette des HUD (<see cref="GameHudSkin"/>).
///
/// Die Mechanik bleibt komplett in <see cref="MixerObject"/>: dort schrumpft
/// chargeSprite, solange der Spieler im Feld steht, und bei null geht das
/// Power-up-Fenster auf. Dieses Bauteil liest nur ab, wie weit das ist, und
/// zeigt es:
///
///  - Feld: Ring am Boden mit 36 Segmenten, der sich im Uhrzeigersinn fuellt
///    (mint, kurz vor Schluss gold), vier Pfeile zeigen zur Mitte, Funken
///    werden in den Mixer gesogen.
///  - Mixer: roter Retro-Standmixer. Im Krug liegen Zutaten; beim Laden steigt
///    ein Erdbeer-Smoothie mit Wirbel und Blasen, das Messer dreht, das
///    Geraet ruettelt, die Lampe blinkt. Kurz vor voll wird alles gold.
///  - Fertig: sobald das Spiel nach der Auswahl weiterlaeuft, springt der
///    Deckel ab, Funken spruehen, das Feld blitzt und verschwindet. Danach
///    steht der Mixer leer und aus da - er laedt nie wieder.
///
/// Die alten Grafiken (Mixer-Sprite, Zone, ZoneOutside) werden nur
/// unsichtbar geschaltet; MixerObject arbeitet weiter mit ihnen.
/// Eine Welt-Einheit sind 32 Texel.
/// </summary>
[DisallowMultipleComponent]
public class MixerVisual : MonoBehaviour
{
    private const float PPU = 32f;
    private const int BodyW = 48, BodyH = 80;
    private const int LidTop = 8, LidH = 12;          // Deckel: Zeilen 8..19 im Koerper-Raster
    private const int JugTop = 18, JugBottom = 54;
    private const int IdleRows = 4, MaxRows = 31, UsedRows = 2;
    private const int Segments = 36;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    // ---- Verbindung zur Mechanik ----
    private MixerObject mixer;
    private Collider2D trigger;
    private Transform charge;

    // ---- Darstellung ----
    private Transform root;
    private SpriteRenderer body, lid, shadow, field;
    private readonly SpriteRenderer[] arrows = new SpriteRenderer[4];
    private Texture2D bodyTex, fieldTex;
    private Color32[] bodyPx, fieldPx;
    private int fieldSize, fieldR;
    private Vector2 fieldCenter;
    private int[] ringIdx;
    private float[] ringAngle;
    private byte[] ringRow;       // 0 = aussen .. 3 = innen
    private bool[] ringTick;

    private readonly List<Spark> sparks = new List<Spark>();
    private static Sprite sparkSprite;

    // ---- Zustand ----
    private enum State { Ready, Waiting, Burst, Used }
    private State state = State.Ready;
    private float progress, lastProgress;
    private bool charging;
    private float chargingUntil;
    private int paintedKey = int.MinValue, paintedFieldStep = -1;
    private float burstStart, nextSpark;
    private Vector3 lidHome;

    private class Spark
    {
        public SpriteRenderer R;
        public Vector2 From, To;
        public float Start, Life;
        public bool Inward;
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Start()
    {
        mixer = GetComponent<MixerObject>();
        trigger = GetComponent<Collider2D>();
        if (mixer == null || trigger == null)
        {
            enabled = false;
            return;
        }
        charge = mixer.chargeSprite != null ? mixer.chargeSprite.transform : null;

        SpriteRenderer own = GetComponent<SpriteRenderer>();
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = false;

        // Halter ohne eigenen SpriteRenderer: MixerObject schaltet beim
        // Ausloesen die Renderer seiner direkten Kinder ab - diesen nicht.
        root = new GameObject("MixerVisual").transform;
        root.SetParent(transform, false);
        float s = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? 1f / transform.lossyScale.x : 1f;
        root.localScale = new Vector3(s, s, 1f);

        int layer = own != null ? own.sortingLayerID : 0;
        int order = own != null ? own.sortingOrder : 0;

        BuildField(layer, order - 2);
        shadow = NewRenderer("Shadow", ShadowSprite(), layer, order - 1, Vector2.zero);
        shadow.transform.localPosition = new Vector3(0f, 2f / PPU, 0f);

        bodyTex = NewTexture(BodyW, BodyH);
        bodyPx = new Color32[BodyW * BodyH];
        body = NewRenderer("Body", Sprite.Create(bodyTex, new Rect(0, 0, BodyW, BodyH), new Vector2(0.5f, 0f), PPU),
                           layer, order, Vector2.zero);

        Texture2D lidTex = NewTexture(BodyW, LidH);
        lidTex.SetPixels32(PaintLid());
        lidTex.Apply(false);
        lid = NewRenderer("Lid", Sprite.Create(lidTex, new Rect(0, 0, BodyW, LidH), new Vector2(0.5f, 0f), PPU),
                          layer, order + 1, Vector2.zero);
        lidHome = new Vector3(0f, (BodyH - LidTop - LidH) / PPU, 0f);
        lid.transform.localPosition = lidHome;

        for (int i = 0; i < 4; i++)
            arrows[i] = NewRenderer("Arrow" + i, ArrowSprite(i), layer, order - 1, Vector2.zero);

        for (int i = 0; i < 20; i++)
        {
            SpriteRenderer r = NewRenderer("Spark" + i, SparkSprite(), layer, order + 2, Vector2.zero);
            r.enabled = false;
            sparks.Add(new Spark { R = r });
        }

        PaintBody(0, 0, true);
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite, int layer, int order, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingLayerID = layer;
        r.sortingOrder = order;
        return r;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
    }

    private void OnDestroy()
    {
        if (bodyTex != null) Destroy(bodyTex);
        if (fieldTex != null) Destroy(fieldTex);
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void Update()
    {
        if (root == null) return;
        float now = Time.time;

        // ---- Mechanik ablesen ----
        float scale = charge != null ? Mathf.Abs(charge.localScale.x) : mixer.normalSize;
        progress = mixer.normalSize > 0f ? 1f - Mathf.Clamp01(scale / mixer.normalSize) : 0f;
        if (progress > lastProgress + 0.0001f) chargingUntil = now + 0.15f;
        charging = now < chargingUntil;
        lastProgress = progress;

        if (state == State.Ready && !trigger.enabled)
        {
            // Ausgeloest. Waehrend das Power-up-Fenster offen ist, steht die
            // Zeit - der Knall kommt, sobald es weitergeht.
            state = State.Waiting;
            progress = 1f;
        }
        if (state == State.Waiting && Time.timeScale > 0f)
        {
            state = State.Burst;
            burstStart = now;
            Burst();
        }
        if (state == State.Burst && now - burstStart > 0.9f) state = State.Used;

        UpdateField(now);
        UpdateBody(now);
        UpdateLid(now);
        UpdateArrows(now);
        UpdateSparks(now);
    }

    // ---------- Mixer ----------

    private void UpdateBody(float now)
    {
        // Solange das Power-up-Fenster offen ist, bleibt der Krug voll und gold.
        bool used = state == State.Burst || state == State.Used;
        int rows = used ? UsedRows
                 : state == State.Waiting ? MaxRows
                 : Mathf.RoundToInt(Mathf.Lerp(IdleRows, MaxRows, progress));
        int frame = charging ? Mathf.FloorToInt(now * 10f) % 4 : Mathf.FloorToInt(now * 2f) % 4;
        int key = rows * 64 + frame * 8 + (charging ? 4 : 0) + (int)state;
        if (key != paintedKey)
        {
            paintedKey = key;
            PaintBody(rows, frame, !used);
        }

        // Ruetteln: einen Texel hin und her, je voller, desto schneller.
        float x = 0f;
        if (state == State.Ready && charging && progress > 0.15f)
        {
            float hz = Mathf.Lerp(12f, 30f, progress);
            x = (Mathf.FloorToInt(now * hz) % 2 == 0 ? 1f : -1f) / PPU;
        }
        else if (state == State.Burst && now - burstStart < 0.25f)
        {
            x = (Mathf.FloorToInt(now * 40f) % 2 == 0 ? 1f : -1f) / PPU;
        }
        body.transform.localPosition = new Vector3(x, 0f, 0f);
    }

    private void UpdateLid(float now)
    {
        if (state == State.Ready || state == State.Waiting)
        {
            float x = body.transform.localPosition.x;
            // Kurz vor voll hebt sich der Deckel im Takt - gleich fliegt er.
            float hop = state == State.Ready && charging && progress > 0.8f
                        && Mathf.FloorToInt(now * 8f) % 2 == 0 ? 1f / PPU : 0f;
            lid.transform.localPosition = lidHome + new Vector3(x, hop, 0f);
            lid.enabled = true;
            return;
        }

        // Deckel springt ab: steigt, bleibt kurz, blinkt und ist weg.
        float t = now - burstStart;
        if (t > 0.9f)
        {
            lid.enabled = false;
            return;
        }
        float rise = 1.6f * (1f - (1f - Mathf.Clamp01(t / 0.35f)) * (1f - Mathf.Clamp01(t / 0.35f)));
        float drift = Mathf.Clamp01(t / 0.9f) * 0.5f;
        lid.transform.localPosition = lidHome + new Vector3(Snap(-drift), Snap(rise), 0f);
        lid.enabled = t < 0.5f || Mathf.FloorToInt(t * 16f) % 2 == 0;
    }

    private static float Snap(float v) => Mathf.Round(v * PPU) / PPU;

    // ---------- Feld ----------

    private void UpdateField(float now)
    {
        if (state == State.Used)
        {
            field.enabled = false;
            return;
        }

        if (state == State.Burst)
        {
            // Aufblitzen, dann in drei Stufen weg.
            float t = now - burstStart;
            float a = t < 0.1f ? 1f : t < 0.3f ? 0.66f : t < 0.5f ? 0.33f : 0f;
            field.color = new Color(1f, 1f, 1f, a);
            field.enabled = a > 0f;
            PaintFieldProgress(1f, true);
            return;
        }

        PaintFieldProgress(progress, false);

        // Ohne Spieler atmet der Ring leise, damit man ihn bemerkt.
        float alpha = 1f;
        if (!charging && progress < 0.001f)
            alpha = Mathf.FloorToInt(now * 1.5f) % 2 == 0 ? 1f : 0.8f;
        field.color = new Color(1f, 1f, 1f, alpha);
        field.enabled = true;
    }

    private void UpdateArrows(float now)
    {
        bool on = state == State.Ready;
        for (int i = 0; i < 4; i++)
        {
            SpriteRenderer a = arrows[i];
            a.enabled = on;
            if (!on) continue;

            // In Ruhe wippen die Pfeile einen Texel, beim Laden laufen sie zur Mitte.
            int step = charging ? Mathf.FloorToInt(now * 12f) % 6 : (Mathf.FloorToInt(now * 2f) % 2);
            float r = (fieldR - 14 - step) / PPU;
            Vector2 dir = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
            a.transform.localPosition = (Vector3)(fieldCenter + dir * r);
            a.color = charging ? (Color)GameHudSkin.GoldLight : Color.white;
        }
    }

    // ---------- Funken ----------

    private void UpdateSparks(float now)
    {
        // Beim Laden werden Funken vom Ring in den Krug gesogen.
        if (state == State.Ready && charging && now >= nextSpark)
        {
            nextSpark = now + Mathf.Lerp(0.12f, 0.04f, progress);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 from = fieldCenter + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * ((fieldR - 3) / PPU);
            Vector2 to = new Vector2(0f, (BodyH - 40) / PPU);
            Emit(from, to, now, 0.45f, true);
        }

        foreach (Spark sp in sparks)
        {
            if (!sp.R.enabled) continue;
            float t = (now - sp.Start) / sp.Life;
            if (t >= 1f)
            {
                sp.R.enabled = false;
                continue;
            }
            float k = sp.Inward ? t * t : 1f - (1f - t) * (1f - t);
            Vector2 p = Vector2.Lerp(sp.From, sp.To, k);
            sp.R.transform.localPosition = new Vector3(Snap(p.x), Snap(p.y), 0f);
            // Letztes Drittel: flackern statt weich ausblenden.
            sp.R.color = t > 0.66f && Mathf.FloorToInt(now * 20f) % 2 == 0 ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }
    }

    private void Emit(Vector2 from, Vector2 to, float now, float life, bool inward)
    {
        foreach (Spark sp in sparks)
        {
            if (sp.R.enabled) continue;
            sp.From = from;
            sp.To = to;
            sp.Start = now;
            sp.Life = life;
            sp.Inward = inward;
            sp.R.enabled = true;
            sp.R.transform.localPosition = from;
            return;
        }
    }

    private void Burst()
    {
        Vector2 top = new Vector2(0f, (BodyH - JugTop) / PPU);
        for (int i = 0; i < 16; i++)
        {
            float ang = i / 16f * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.35f);
            Emit(top, top + dir * Random.Range(1.2f, 2.2f), burstStart, Random.Range(0.45f, 0.75f), false);
        }
    }

    // ==================================================================
    //  Malen: Mixer
    // ==================================================================

    private static int JugHalf(int y)
    {
        float t = Mathf.Clamp01((y - JugTop) / (float)(JugBottom - JugTop));
        return Mathf.RoundToInt(Mathf.Lerp(14f, 10f, t));
    }

    private static int JugLeft(int y) => 24 - JugHalf(y);
    private static int JugRight(int y) => 23 + JugHalf(y);

    /// <summary>
    /// Malt den ganzen Mixer ohne Deckel neu. rows = Fuellhoehe in Zeilen,
    /// frame = Animationsschritt (Wirbel, Messer, Lampe).
    /// </summary>
    private void PaintBody(int rows, int frame, bool live)
    {
        System.Array.Clear(bodyPx, 0, bodyPx.Length);
        Color32 ink = GameHudSkin.Ink;
        bool gold = live && progress >= 0.85f;
        bool used = !live;

        // ---- Fuesse ----
        Fill(11, 77, 5, 2, ink);
        Fill(32, 77, 5, 2, ink);

        // ---- Motorblock ----
        for (int y = 58; y <= 76; y++)
        {
            int grow = (y - 58) * 2 / 18;
            int l = 11 - grow, r = 36 + grow;
            for (int x = l; x <= r; x++)
            {
                bool corner = (y == 58 || y == 76) && (x == l || x == r);
                if (corner) continue;
                Color32 c;
                if (x == l || x == r || y == 58 || y == 76) c = ink;
                else if (y == 59 || x == l + 1) c = GameHudSkin.JamLight;
                else if (x == r - 1 || y >= 74) c = GameHudSkin.JamDark;
                else c = GameHudSkin.Jam;
                Set(x, y, c);
            }
        }
        // Zierstreifen
        for (int x = 13; x <= 34; x++) Set(x, 61, GameHudSkin.JamDark);

        // Bedienfeld
        FillRound(16, 63, 16, 10, ink);
        Fill(17, 64, 14, 8, GameHudSkin.Cream);
        Fill(17, 71, 14, 1, GameHudSkin.ParchDark);

        // Drehknopf mit Zeiger
        int cx = 21, cy = 67;
        FillRound(cx - 2, cy - 2, 5, 5, ink);
        Fill(cx - 1, cy - 1, 3, 3, GameHudSkin.Gold);
        Set(cx - 1, cy - 1, GameHudSkin.GoldLight);
        int dial = live && charging ? frame : used ? 0 : 1;
        Vector2Int[] tip = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        Set(cx + tip[dial].x, cy + tip[dial].y, ink);

        // Lampe
        Color32 led = used ? GameHudSkin.Plum
                    : gold ? (frame % 2 == 0 ? GameHudSkin.GoldLight : GameHudSkin.Gold)
                    : charging ? (frame % 2 == 0 ? GameHudSkin.MintLight : GameHudSkin.Mint)
                    : GameHudSkin.MintDark;
        Fill(26, 65, 3, 3, ink);
        Set(27, 66, led);
        Fill(26, 69, 3, 1, GameHudSkin.ParchDark);

        // ---- Chromring ----
        for (int x = 12; x <= 35; x++)
        {
            Set(x, 55, ink);
            Set(x, 56, GameHudSkin.StoneLight);
            Set(x, 57, GameHudSkin.Stone);
        }
        Set(12, 56, ink); Set(12, 57, ink); Set(35, 56, ink); Set(35, 57, ink);
        Set(14, 56, GameHudSkin.Cream);

        // ---- Henkel (rechts, am Krug) ----
        int hr = JugRight(JugTop + 16);
        for (int y = JugTop + 6; y <= JugTop + 27; y++)
        {
            for (int x = hr + 3; x <= hr + 6; x++)
            {
                Color32 c = x == hr + 3 || x == hr + 6 ? ink : x == hr + 4 ? GameHudSkin.JamLight : GameHudSkin.JamDark;
                Set(x, y, c);
            }
        }
        for (int arm = 0; arm < 2; arm++)
        {
            int y0 = arm == 0 ? JugTop + 6 : JugTop + 24;
            for (int y = y0; y <= y0 + 3; y++)
            {
                int from = JugRight(y) + 1;
                for (int x = from; x <= hr + 6; x++)
                {
                    Color32 c = y == y0 || y == y0 + 3 ? ink : y == y0 + 1 ? GameHudSkin.JamLight : GameHudSkin.JamDark;
                    if (x == hr + 6) c = ink;
                    Set(x, y, c);
                }
            }
        }

        // ---- Krug: Glas ----
        Color32 glass = used ? new Color32(0xc9, 0xd8, 0xe0, 120) : new Color32(0xdf, 0xf2, 0xfb, 150);
        for (int y = JugTop + 1; y < JugBottom; y++)
            for (int x = JugLeft(y) + 1; x < JugRight(y); x++)
                Set(x, y, glass);

        // ---- Fuellung ----
        int surface = JugBottom - rows;   // oberste Zeile der Fuellung
        if (live && !charging && progress < 0.02f) PaintIngredients();
        else PaintLiquid(surface, frame, gold, used);

        // ---- Messer ----
        int by = JugBottom - 3;
        Color32 blade = GameHudSkin.StoneLight;
        if (!live || !charging || frame % 2 == 0)
        {
            for (int x = 19; x <= 28; x++) Over(x, by, blade);
        }
        else
        {
            Over(21, by - 2, blade); Over(22, by - 1, blade);
            Over(25, by + 1, blade); Over(26, by + 2, blade);
            Over(26, by - 2, blade); Over(25, by - 1, blade);
            Over(22, by + 1, blade); Over(21, by + 2, blade);
        }
        Set(23, by, ink); Set(24, by, ink);
        Set(23, by + 1, GameHudSkin.Stone); Set(24, by + 1, GameHudSkin.Stone);

        // ---- Glanz und Messstriche ----
        for (int y = JugTop + 4; y <= JugBottom - 9; y++)
        {
            int l = JugLeft(y);
            Over(l + 2, y, new Color32(255, 255, 255, 170));
            if (y < JugTop + 14) Over(l + 3, y, new Color32(255, 255, 255, 110));
        }
        for (int y = JugTop + 9; y <= JugBottom - 6; y += 6)
        {
            int r = JugRight(y);
            Over(r - 2, y, new Color32(0x3b, 0x2b, 0x33, 140));
            Over(r - 3, y, new Color32(0x3b, 0x2b, 0x33, 140));
        }

        // ---- Krug: Kontur und Tuelle ----
        for (int y = JugTop; y <= JugBottom; y++)
        {
            Set(JugLeft(y), y, ink);
            Set(JugRight(y), y, ink);
        }
        for (int x = JugLeft(JugBottom); x <= JugRight(JugBottom); x++) Set(x, JugBottom, ink);
        for (int x = JugLeft(JugTop); x <= JugRight(JugTop); x++) Set(x, JugTop, ink);
        int sl = JugLeft(JugTop);
        Set(sl - 1, JugTop, ink); Set(sl - 2, JugTop, ink); Set(sl - 3, JugTop - 1, ink);
        Set(sl - 1, JugTop + 1, ink); Set(sl - 2, JugTop + 1, ink);

        bodyTex.SetPixels32(bodyPx);
        bodyTex.Apply(false);
    }

    /// <summary>Ruhezustand: bunte Zutaten liegen unten im Krug.</summary>
    private void PaintIngredients()
    {
        string[] rows =
        {
            "..j..c..g....jj..c...",
            ".jjj.cc.....cjjg.cc..",
            "cjjjccc.gg.ccjjccccj.",
            "ccjcccgcggcccjcccgcjj",
        };
        for (int r = 0; r < rows.Length; r++)
        {
            int y = JugBottom - rows.Length + r;
            int l = JugLeft(y) + 1, right = JugRight(y) - 1;
            for (int i = 0; i < rows[r].Length; i++)
            {
                int x = l + i;
                if (x > right) break;
                char ch = rows[r][i];
                Color32 c = ch == 'j' ? GameHudSkin.Jam
                          : ch == 'c' ? GameHudSkin.ParchMid
                          : ch == 'g' ? GameHudSkin.Gold
                          : Clear;
                if (c.a > 0) Set(x, y, c);
            }
        }
    }

    /// <summary>Smoothie mit schraegem Wirbel, Schaumkrone und Blasen.</summary>
    private void PaintLiquid(int surface, int frame, bool gold, bool used)
    {
        Color32 main = gold ? GameHudSkin.Gold : GameHudSkin.Jam;
        Color32 light = gold ? GameHudSkin.GoldLight : GameHudSkin.JamLight;
        Color32 dark = gold ? GameHudSkin.GoldDark : GameHudSkin.JamDark;
        Color32 foam = gold ? GameHudSkin.Cream : new Color32(0xfb, 0xd6, 0xdc, 255);
        if (used)
        {
            main = new Color32(0xb0, 0x6a, 0x78, 255);
            light = main;
            dark = GameHudSkin.JamDeep;
            foam = main;
        }

        for (int y = surface; y < JugBottom; y++)
        {
            int l = JugLeft(y) + 1, r = JugRight(y) - 1;
            for (int x = l; x <= r; x++)
            {
                Color32 c;
                if (y == surface)
                {
                    // Schaum wellt sich: jede zweite Spalte eine Zeile tiefer.
                    bool dip = ((x + frame) & 3) == 0;
                    c = dip ? light : foam;
                }
                else if (y >= JugBottom - 2) c = dark;
                else
                {
                    int band = ((x + (y - surface) * 2 + frame * 2) / 3) & 3;
                    c = band == 0 ? light : main;
                }
                Set(x, y, c);
            }
        }

        if (used) return;

        // Blasen steigen auf - fester Satz, der mit dem Frame weiterrueckt.
        int depth = JugBottom - 3 - surface;
        if (depth < 4) return;
        for (int i = 0; i < 4; i++)
        {
            int bx = 17 + (i * 7) % 14;
            int by = JugBottom - 4 - ((frame * 3 + i * 5) % depth);
            if (by > surface + 1) Set(bx, by, foam);
        }
    }

    private static Color32[] PaintLid()
    {
        var px = new Color32[BodyW * LidH];
        void S(int x, int y, Color32 c)
        {
            int ly = y - LidTop;
            if (x < 0 || x >= BodyW || ly < 0 || ly >= LidH) return;
            px[(LidH - 1 - ly) * BodyW + x] = c;
        }

        Color32 ink = GameHudSkin.Ink;
        // Knauf
        for (int y = 9; y <= 12; y++)
            for (int x = 21; x <= 26; x++)
            {
                bool edge = x == 21 || x == 26 || y == 9;
                bool corner = y == 9 && (x == 21 || x == 26);
                if (corner) continue;
                S(x, y, edge ? ink : y == 10 ? GameHudSkin.GoldLight : GameHudSkin.Gold);
            }
        // Kappe
        for (int y = 13; y <= 18; y++)
            for (int x = 9; x <= 38; x++)
            {
                bool corner = (y == 13 || y == 18) && (x == 9 || x == 38);
                if (corner) continue;
                Color32 c = x == 9 || x == 38 || y == 13 || y == 18 ? ink
                          : y == 14 ? GameHudSkin.StoneLight
                          : y == 17 ? GameHudSkin.StoneDark
                          : GameHudSkin.Stone;
                S(x, y, c);
            }
        S(12, 15, GameHudSkin.Cream);
        return px;
    }

    // ---------- Pinsel (Ursprung oben links) ----------

    private void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= BodyW || y >= BodyH) return;
        bodyPx[(BodyH - 1 - y) * BodyW + x] = c;
    }

    private void Over(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= BodyW || y >= BodyH) return;
        int i = (BodyH - 1 - y) * BodyW + x;
        Color32 d = bodyPx[i];
        float a = c.a / 255f;
        bodyPx[i] = new Color32((byte)Mathf.Lerp(d.r, c.r, a), (byte)Mathf.Lerp(d.g, c.g, a),
                                (byte)Mathf.Lerp(d.b, c.b, a), (byte)Mathf.Max(d.a, c.a));
    }

    private void Fill(int x, int y, int w, int h, Color32 c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Set(i, j, c);
    }

    private void FillRound(int x, int y, int w, int h, Color32 c)
    {
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                bool corner = (i == 0 || i == w - 1) && (j == 0 || j == h - 1);
                if (!corner) Set(x + i, y + j, c);
            }
    }

    // ==================================================================
    //  Malen: Feld
    // ==================================================================

    /// <summary>
    /// Das Feld wird einmal voll gemalt; danach aendern sich nur noch die
    /// Pixel der Ringspur, wenn der Fortschritt ein Stueck weiter ist.
    /// </summary>
    private void BuildField(int layer, int order)
    {
        CircleCollider2D circle = trigger as CircleCollider2D;
        float worldR = circle != null ? circle.radius * Mathf.Abs(transform.lossyScale.x) : 4f;
        Vector2 offset = circle != null ? circle.offset * (Vector2)transform.lossyScale : Vector2.zero;
        fieldR = Mathf.Max(24, Mathf.RoundToInt(worldR * PPU));
        fieldCenter = new Vector2(Snap(offset.x), Snap(offset.y));

        fieldSize = fieldR * 2 + 3;
        fieldTex = NewTexture(fieldSize, fieldSize);
        fieldPx = new Color32[fieldSize * fieldSize];

        var idx = new List<int>();
        var ang = new List<float>();
        var row = new List<byte>();
        var tick = new List<bool>();

        float c = fieldR + 1;
        Color32 inside = new Color32(0x6f, 0xb7, 0xe0, 30);
        Color32 dash = new Color32(0xff, 0xf4, 0xe0, 60);
        Color32 line = new Color32(0x3b, 0x2b, 0x33, 190);
        float innerRing = fieldR * 0.62f;

        for (int y = 0; y < fieldSize; y++)
            for (int x = 0; x < fieldSize; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > fieldR) continue;

                // Winkel ab 12 Uhr im Uhrzeigersinn, 0..1 (Texturzeilen zaehlen von unten).
                float a = Mathf.Atan2(dx, dy) / (Mathf.PI * 2f);
                if (a < 0f) a += 1f;
                int i = y * fieldSize + x;

                if (d > fieldR - 1f) fieldPx[i] = line;
                else if (d > fieldR - 5f)
                {
                    idx.Add(i);
                    ang.Add(a);
                    row.Add((byte)Mathf.Clamp(Mathf.FloorToInt(fieldR - 1f - d), 0, 3));
                    float seg = a * Segments;
                    float distPx = Mathf.Abs(seg - Mathf.Round(seg)) / Segments * Mathf.PI * 2f * d;
                    tick.Add(distPx < 0.55f);
                }
                else if (d > fieldR - 6f) fieldPx[i] = line;
                else
                {
                    fieldPx[i] = inside;
                    if (Mathf.Abs(d - innerRing) < 1f && (Mathf.FloorToInt(a * 72f) & 1) == 0)
                        fieldPx[i] = dash;
                }
            }

        ringIdx = idx.ToArray();
        ringAngle = ang.ToArray();
        ringRow = row.ToArray();
        ringTick = tick.ToArray();

        field = NewRenderer("Field", Sprite.Create(fieldTex, new Rect(0, 0, fieldSize, fieldSize),
                                                   new Vector2(0.5f, 0.5f), PPU), layer, order, fieldCenter);
        PaintFieldProgress(0f, false);
    }

    private void PaintFieldProgress(float p, bool flash)
    {
        int step = flash ? 10000 : Mathf.FloorToInt(p * 144f);
        if (step == paintedFieldStep) return;
        paintedFieldStep = step;

        bool gold = p >= 0.85f;
        Color32 empty = new Color32(0x1c, 0x14, 0x19, 140);
        Color32 emptyTick = new Color32(0x3b, 0x2b, 0x33, 220);
        Color32 hiC = gold ? GameHudSkin.GoldLight : GameHudSkin.MintLight;
        Color32 midC = gold ? GameHudSkin.Gold : GameHudSkin.Mint;
        Color32 loC = gold ? GameHudSkin.GoldDark : GameHudSkin.MintDark;
        Color32 head = GameHudSkin.Cream;
        float headWidth = 0.012f;

        for (int k = 0; k < ringIdx.Length; k++)
        {
            float a = ringAngle[k];
            Color32 col;
            if (flash) col = GameHudSkin.Cream;
            else if (a <= p)
            {
                if (ringTick[k]) col = loC;
                else if (p < 1f && a > p - headWidth) col = head;
                else col = ringRow[k] == 0 ? hiC : ringRow[k] == 3 ? loC : midC;
            }
            else col = ringTick[k] ? emptyTick : empty;
            fieldPx[ringIdx[k]] = col;
        }

        fieldTex.SetPixels32(fieldPx);
        fieldTex.Apply(false);
    }

    // ==================================================================
    //  Kleine Sprites
    // ==================================================================

    private static Sprite ShadowSprite()
    {
        const int w = 40, h = 9;
        Texture2D t = NewTexture(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                float d = dx * dx + dy * dy;
                if (d <= 1f) px[y * w + x] = new Color32(0x1c, 0x14, 0x19, (byte)(d < 0.55f ? 110 : 70));
            }
        t.SetPixels32(px);
        t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
    }

    /// <summary>Pfeil zur Mitte: 0 = oben (zeigt runter), 1 = rechts, 2 = unten, 3 = links.</summary>
    private static Sprite ArrowSprite(int dir)
    {
        string[] down =
        {
            "kkkkkkkkk",
            "kwwwwwwwk",
            ".kwwwwwk.",
            "..kwwgk..",
            "...kgk...",
            "....k....",
        };
        int n = down.Length, m = down[0].Length;
        bool vertical = dir == 0 || dir == 2;
        int w = vertical ? m : n, h = vertical ? n : m;
        Texture2D t = NewTexture(w, h);
        var px = new Color32[w * h];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < m; c++)
            {
                char ch = down[r][c];
                if (ch == '.') continue;
                Color32 col = ch == 'k' ? GameHudSkin.Ink : ch == 'g' ? GameHudSkin.ParchDark : GameHudSkin.Cream;
                // Ursprung oben links -> je Richtung drehen.
                int x, y;
                switch (dir)
                {
                    case 0: x = c; y = r; break;                 // zeigt nach unten
                    case 2: x = c; y = n - 1 - r; break;         // zeigt nach oben
                    case 1: x = n - 1 - r; y = c; break;         // rechts, zeigt nach links
                    default: x = r; y = c; break;                // links, zeigt nach rechts
                }
                px[(h - 1 - y) * w + x] = col;
            }
        t.SetPixels32(px);
        t.Apply(false);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
    }

    private static Sprite SparkSprite()
    {
        if (sparkSprite != null) return sparkSprite;
        Texture2D t = NewTexture(3, 3);
        var px = new Color32[9];
        Color32 g = GameHudSkin.GoldLight, w = GameHudSkin.Cream;
        px[1] = g; px[3] = g; px[4] = w; px[5] = g; px[7] = g;
        t.SetPixels32(px);
        t.Apply(false);
        sparkSprite = Sprite.Create(t, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), PPU);
        return sparkSprite;
    }
}
