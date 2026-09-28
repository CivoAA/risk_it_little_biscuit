using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafik der Charakterauswahl im Hub (<see cref="HubCharacterSelectUI"/>):
/// die Fenster der Keksdose, die Buehne mit Spot und Tortenplatte, dazu ein
/// paar Symbole. Gleiche Palette und gleiche Regeln wie der Rest dieser Datei -
/// alles zur Laufzeit gemalt, pixelsPerUnit 100, Point-Filter.
///
/// Was nach Groesse gemalt wird (<see cref="SlotBack"/>, <see cref="Stage"/>,
/// <see cref="Spotlight"/> ...), gehoert auch in genau dieser Groesse
/// gezeichnet - Baender und Bretter wuerden sonst ungleich dick.
/// </summary>
public static partial class GameHudSkin
{
    public enum SlotLook { Wood, Hover, Gold, Stone }

    /// <summary>
    /// Rahmen um ein Fenster der Keksdose: Kontur mit Radius 3, zwei Pixel
    /// Holz (Gold fuer die Auswahl, Stein fuer leere Plaetze), oben links hell,
    /// unten rechts dunkel. Innen frei bis auf die Eckpixel und eine
    /// Schattenzeile oben - das Fenster wirkt dadurch eingelassen. 9-Slice, Rand 4;
    /// das Fenster beginnt 3 px innen.
    /// </summary>
    public static Sprite SlotFrame(SlotLook look) => Get("slotframe" + look, () =>
    {
        Color32 ring, light, dark;
        switch (look)
        {
            case SlotLook.Hover: ring = WoodLight; light = Hex(0xc9a06e); dark = WoodMid; break;
            case SlotLook.Gold:  ring = Gold;      light = GoldLight;     dark = GoldDark; break;
            case SlotLook.Stone: ring = StoneDark; light = Stone;         dark = Hex(0x3a2e34); break;
            default:             ring = Wood;      light = WoodLight;     dark = WoodDark; break;
        }

        const int s = 12;
        var c = new Px(s, s);
        c.FillRounded(0, 0, s, s, 3, Ink);
        c.FillRounded(1, 1, s - 2, s - 2, 2, ring);
        c.HLine(3, 1, s - 6, light);
        c.VLine(1, 3, s - 6, light);
        c.HLine(3, s - 2, s - 6, dark);
        c.VLine(s - 2, 3, s - 6, dark);

        c.Fill(3, 3, s - 6, s - 6, Clear);
        c.HLine(4, 3, s - 8, new Color32(0, 0, 0, 70));
        c.Set(3, 3, dark);
        c.Set(s - 4, 3, dark);
        c.Set(3, s - 4, dark);
        c.Set(s - 4, s - 4, dark);
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>
    /// Hintergrund in einem Fenster der Keksdose: Pergament in drei harten
    /// Baendern mit Lichthof - wie das Portraet im HUD. Getoent wird per
    /// Image.color mit der Farbe des Charakters.
    /// </summary>
    public static Sprite SlotBack(int w, int h) => Get("slotback" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        int b1 = Mathf.RoundToInt(h * 0.38f), b2 = Mathf.RoundToInt(h * 0.71f);
        c.Fill(0, 0, w, b1, Parchment);
        c.Fill(0, b1, w, b2 - b1, ParchMid);
        c.Fill(0, b2, w, h - b2, ParchDark);
        int hw = Mathf.RoundToInt(w * 0.56f), hh = Mathf.RoundToInt(h * 0.46f);
        c.FillRounded((w - hw) / 2, Mathf.RoundToInt(h * 0.12f), hw, hh, 4, Cream);
        return c;
    });

    /// <summary>Leerer Platz in der Dose: dunkle Rinne mit einem Punktraster.</summary>
    public static Sprite SlotEmpty(int w, int h) => Get("slotempty" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        c.Fill(0, 0, w, h, TroughLow);
        for (int y = 2; y < h; y += 4)
            for (int x = (y / 4) % 2 == 0 ? 2 : 4; x < w; x += 4)
                c.Set(x, y, Trough);
        return c;
    });

    /// <summary>
    /// Die Buehne: Kontur, dunkle Tapete mit Streifen, unten ein Holzboden
    /// aus Brettern (<paramref name="floor"/> px hoch). Exakt in dieser
    /// Groesse zeichnen.
    /// </summary>
    public static Sprite Stage(int w, int h, int floor) => Get("stage" + w + "x" + h + "f" + floor, () =>
    {
        var c = new Px(w, h);
        c.FillRounded(0, 0, w, h, 3, Ink);

        Color32 wall = Hex(0x2a1f27), stripe = Hex(0x31242d), wallLow = Hex(0x231a21);
        c.FillRounded(1, 1, w - 2, h - 2, 2, wall);
        for (int x = 5; x < w - 2; x += 10) c.Fill(x, 2, 4, h - floor - 3, stripe);
        // Oben eine Stufe dunkler - der Spot soll das Hellste sein.
        c.Fill(1, 1, w - 2, 1, Hex(0x1c1419));
        c.Fill(2, 2, w - 4, 3, wallLow);

        // Fussleiste
        int fy = h - floor;
        c.HLine(1, fy - 2, w - 2, WoodDark);
        c.HLine(1, fy - 1, w - 2, Ink);

        // Bretter: helle Oberkante, Fugen versetzt
        c.Fill(1, fy, w - 2, floor - 1, WoodMid);
        c.HLine(1, fy, w - 2, WoodLight);
        int rowH = Mathf.Max(4, (floor - 2) / 3);
        for (int r = 0, y = fy + 1; y < h - 1; r++, y += rowH)
        {
            c.HLine(1, y + rowH - 1, w - 2, WoodDark);
            int offset = r % 2 == 0 ? 7 : 19;
            for (int x = offset; x < w - 2; x += 24) c.VLine(x, y, Mathf.Min(rowH - 1, h - 1 - y), WoodDark);
        }
        c.FillRounded(1, h - 4, w - 2, 3, 1, WoodDark);
        return c;
    });

    /// <summary>
    /// Lichtkegel von oben, weiss mit gestuften Deckkraeften - Farbe und
    /// Staerke kommen ueber Image.color. Oben schmal, unten breit.
    /// </summary>
    public static Sprite Spotlight(int w, int h) => Get("spot" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        float top = w * 0.14f, bottom = w * 0.5f;
        for (int y = 0; y < h; y++)
        {
            float half = Mathf.Lerp(top, bottom, y / (float)(h - 1));
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x + 0.5f - w / 2f) / half;
                byte a = d < 0.45f ? (byte)110 : d < 0.75f ? (byte)70 : d < 1f ? (byte)36 : (byte)0;
                if (a > 0) c.Set(x, y, new Color32(255, 255, 255, a));
            }
        }
        return c;
    });

    /// <summary>Lichtpfuetze am Boden: gestufte Ellipse, weiss, gefaerbt per Image.color.</summary>
    public static Sprite LightPool(int w, int h) => Get("pool" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        FillEllipse(c, w, h, 1f, new Color32(255, 255, 255, 40));
        FillEllipse(c, w, h, 0.72f, new Color32(255, 255, 255, 80));
        FillEllipse(c, w, h, 0.42f, new Color32(255, 255, 255, 120));
        return c;
    });

    /// <summary>
    /// Tortenplatte, auf der der Charakter steht: Pergamentteller mit
    /// Goldrand, darunter ein kurzer Holzfuss und ein weicher Schatten.
    /// </summary>
    public static Sprite Pedestal => Get("pedestal", () =>
    {
        const int w = 104, h = 22;
        var c = new Px(w, h);

        // Schatten auf dem Boden
        Ellipse(c, 52, 19, 44, 3, new Color32(0, 0, 0, 90));

        // Fuss
        c.FillRounded(38, 13, 28, 7, 1, Ink);
        c.Fill(39, 14, 26, 5, WoodMid);
        c.HLine(39, 14, 26, WoodLight);
        c.HLine(39, 18, 26, WoodDark);

        // Tellerrand (Seite) - unten die untere Ellipse, dazwischen gefuellt
        Ellipse(c, 52, 10, 50, 5, Ink);
        Ellipse(c, 52, 10, 49, 4, GoldDark);
        c.Fill(2, 6, w - 4, 4, GoldDark);
        c.VLine(2, 6, 4, Ink);
        c.VLine(w - 3, 6, 4, Ink);
        c.Fill(3, 7, w - 6, 1, Gold);

        // Oberseite
        Ellipse(c, 52, 6, 50, 5, Ink);
        Ellipse(c, 52, 6, 49, 4, Gold);
        Ellipse(c, 52, 6, 46, 3, Parchment);
        Ellipse(c, 50, 5, 34, 1, Cream);
        return c;
    });

    /// <summary>Vorhang-Rand oben an der Buehne: Marmeladenrot mit Falten und Zacken. Kachelt waagerecht.</summary>
    public static Sprite Valance(int w) => Get("valance" + w, () =>
    {
        const int h = 9;
        var c = new Px(w, h);
        for (int x = 0; x < w; x++)
        {
            int fold = x % 8;
            int depth = 6 + (fold < 4 ? fold / 2 : (7 - fold) / 2);
            for (int y = 0; y < depth; y++)
            {
                Color32 col = fold == 0 ? JamDeep : fold < 3 ? JamLight : fold < 6 ? Jam : JamDark;
                if (y == 0) col = JamDeep;
                c.Set(x, y, col);
            }
            c.Set(x, depth, Ink);
        }
        return c;
    });

    // ---------- Symbole ----------

    public static Sprite Lock => Get("lock", () => Outlined(new[]
    {
        ".sss.",
        "s...s",
        "s...s",
        "yyyyy",
        "yykyy",
        "yykyy",
        "ddddd",
    }, SelectColors));

    public static Sprite Question => Get("question", () => Outlined(new[]
    {
        ".sss.",
        "s...s",
        "....s",
        "..ss.",
        "..s..",
        ".....",
        "..s..",
    }, SelectColors));

    /// <summary>Pfeil nach links - Spiegelbild von <see cref="Arrow"/>.</summary>
    public static Sprite ArrowLeft => Get("arrowleft", () => Outlined(new[]
    {
        "..l....",
        ".ly....",
        "yyyyyyy",
        ".oy....",
        "..o....",
    }, UiColors));

    private static Dictionary<char, Color32> SelectColors => selectColors ??= new Dictionary<char, Color32>
    {
        { 's', StoneLight }, { 'y', Gold }, { 'd', GoldDark }, { 'k', Ink },
    };
    private static Dictionary<char, Color32> selectColors;

    // ---------- Werkzeug ----------

    /// <summary>Ellipse um (cx, cy) mit den Halbachsen rx/ry, Pixelmitten-Test.</summary>
    private static void Ellipse(Px c, int cx, int cy, int rx, int ry, Color32 col)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / (rx + 0.5f), dy = (y - cy) / (ry + 0.5f);
                if (dx * dx + dy * dy <= 1f) c.Set(x, y, col);
            }
    }

    /// <summary>Ellipse, die <paramref name="scale"/> der Leinwand fuellt, mittig.</summary>
    private static void FillEllipse(Px c, int w, int h, float scale, Color32 col)
    {
        float rx = w / 2f * scale, ry = h / 2f * scale;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / rx, dy = (y + 0.5f - h / 2f) / ry;
                if (dx * dx + dy * dy <= 1f) c.Set(x, y, col);
            }
    }
}
