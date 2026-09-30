using UnityEngine;

/// <summary>
/// Grafik der Levelauswahl im Hub (<see cref="HubLevelSelectUI"/>): Punkte der
/// Reiseroute, Absperrband fuer Level ohne Szene, Vignette ueber dem Panorama
/// und die kleinen Teilchen fuer die Stimmung (Mehlstaub, Blaetter,
/// Gluehwuermchen, Sonnenstrahl). Gleiche Regeln wie der Rest der Datei:
/// zur Laufzeit gemalt, pixelsPerUnit 100, Point-Filter.
/// </summary>
public static partial class GameHudSkin
{
    /// <summary>
    /// Ein Punkt der Reiseroute, 2x2: gegangene Strecke golden, der Weg zu
    /// gesperrten Karten steingrau. Unten eine Stufe dunkler.
    /// </summary>
    public static Sprite TrailDot(bool lit) => Get("traildot" + lit, () =>
    {
        var c = new Px(2, 2);
        Color32 top = lit ? GoldLight : StoneLight, low = lit ? GoldDark : StoneDark;
        c.HLine(0, 0, 2, top);
        c.HLine(0, 1, 2, low);
        return c;
    });

    /// <summary>
    /// Absperrband: schraege Streifen Gold/Ink, oben und unten eine Kontur.
    /// 12x10, wird mit Image.Type.Tiled waagerecht gekachelt.
    /// </summary>
    public static Sprite CautionTape => Get("cautiontape", () =>
    {
        const int w = 12, h = 10;
        var c = new Px(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 col;
                if (y == 0 || y == h - 1) col = Ink;
                else col = ((x + y) / 6) % 2 == 0 ? Gold : Ink;
                if (y == 1 && col.Equals(Gold)) col = GoldLight;
                if (y == h - 2 && col.Equals(Gold)) col = GoldDark;
                c.Set(x, y, col);
            }
        return c;
    });

    /// <summary>
    /// Dunkler Rand ueber einem Bild: zwei Stufen Deckkraft je 3 px, Ecken
    /// kraeftiger. Genau in dieser Groesse zeichnen.
    /// </summary>
    public static Sprite Vignette(int w, int h) => Get("vignette" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                int dc = Mathf.Min(x, w - 1 - x) + Mathf.Min(y, h - 1 - y);
                byte a = d < 2 ? (byte)90 : d < 5 ? (byte)45 : dc < 14 ? (byte)30 : (byte)0;
                if (a > 0) c.Set(x, y, new Color32(0x1c, 0x14, 0x19, a));
            }
        return c;
    });

    /// <summary>
    /// Schraeger Sonnenstrahl, weiss mit gestufter Deckkraft - Farbe und
    /// Staerke per Image.color. Faellt von oben rechts nach unten links.
    /// </summary>
    public static Sprite Sunbeam(int w, int h) => Get("sunbeam" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        float slope = (w * 0.55f) / h;
        for (int y = 0; y < h; y++)
        {
            float centre = w - 1 - w * 0.22f - y * slope;
            float half = 7f + y * 0.12f;
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - centre) / half;
                byte a = d < 0.35f ? (byte)70 : d < 0.7f ? (byte)42 : d < 1f ? (byte)20 : (byte)0;
                // unten auslaufen lassen
                if (y > h * 0.7f) a = (byte)(a * Mathf.Clamp01((h - y) / (h * 0.3f)));
                if (a > 0) c.Set(x, y, new Color32(255, 255, 255, a));
            }
        }
        return c;
    });

    /// <summary>Blatt 3x2 (gruen oder herbstlich) - faellt im Wald-Panorama.</summary>
    public static Sprite Leaf(int variant) => Get("leaf" + variant, () =>
    {
        var c = new Px(3, 2);
        Color32 a, b;
        switch (variant % 3)
        {
            case 1:  a = GoldLight; b = GoldDark; break;
            case 2:  a = Hex(0xf0a060); b = Hex(0xb8603a); break;
            default: a = MintLight; b = MintDark; break;
        }
        c.Set(0, 0, a);
        c.Set(1, 0, a);
        c.Set(1, 1, b);
        c.Set(2, 1, b);
        return c;
    });

    /// <summary>Gluehwuermchen 3x3: heller Kern, weicher Schein als Plus.</summary>
    public static Sprite Firefly => Get("firefly", () =>
    {
        var c = new Px(3, 3);
        Color32 glow = GoldLight; glow.a = 110;
        c.Set(1, 0, glow);
        c.Set(0, 1, glow);
        c.Set(2, 1, glow);
        c.Set(1, 2, glow);
        c.Set(1, 1, Cream);
        return c;
    });

    /// <summary>Schatten unter der Figur auf der Reiseroute: gestufte Ellipse in Ink.</summary>
    public static Sprite FootShadow(int w, int h) => Get("footshadow" + w + "x" + h, () =>
    {
        var c = new Px(w, h);
        FillEllipse(c, w, h, 1f, Hex(0x1c1419, 70));
        FillEllipse(c, w, h, 0.6f, Hex(0x1c1419, 120));
        return c;
    });
}
