using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafik des Hauptmenues (<see cref="JuicyMainMenu"/>, Stil Sign): kleine
/// Holzschilder passend zum Logo - Brett mit Maserung, Messingnieten, dicker
/// Unterkante und Schlagschatten auf der Wand. Dazu der Goldrahmen fuer den
/// gewaehlten Eintrag, Symbole je Eintrag, der Keks als Zeiger und das
/// Funkeln aus dem Logo.
///
/// Maße eines Schilds (von oben, Breite w):
///   1 Rand fuer den Goldrahmen, 1 Kontur, 12 Brett, 3 Kante, 1 Kontur,
///   1 Rand, 3 Schatten = <see cref="SignH"/>. Gedrueckt rutscht das Brett
///   2 Pixel tiefer in die Kante.
/// </summary>
public static partial class GameHudSkin
{
    public enum SignLook { Wood, WoodLit, Gold, GoldLit }

    /// <summary>Hoehe eines Schilds samt Rahmenplatz und Schatten. Gerade, damit die Mitte auf ganzen Pixeln liegt.</summary>
    public const int SignH = 22;
    /// <summary>Oberkante des Bretts im Bild (ohne Druck) - Rahmenrand plus Kontur.</summary>
    public const int SignFaceTop = 2;
    public const int SignFaceH = 12;

    private struct SignColors
    {
        public Color32 top, face, grain, bevel, lip, lipLow, rivet, rivetLight, rivetDark;
    }

    private static SignColors ColorsFor(SignLook look)
    {
        switch (look)
        {
            case SignLook.WoodLit:
                return new SignColors
                {
                    top = Hex(0xd2a874), face = WoodLight, grain = Hex(0x9f7049), bevel = Hex(0x956641),
                    lip = Wood, lipLow = WoodMid, rivet = Gold, rivetLight = Cream, rivetDark = GoldDark
                };
            case SignLook.Gold:
                return new SignColors
                {
                    top = GoldLight, face = Gold, grain = Hex(0xe2a93f), bevel = Hex(0xd99a35),
                    lip = GoldDark, lipLow = Hex(0x9c6420), rivet = Wood, rivetLight = WoodLight, rivetDark = WoodDark
                };
            case SignLook.GoldLit:
                return new SignColors
                {
                    top = Cream, face = GoldLight, grain = Hex(0xf3cf6f), bevel = Hex(0xedc35f),
                    lip = Gold, lipLow = GoldDark, rivet = Wood, rivetLight = WoodLight, rivetDark = WoodDark
                };
            default:
                return new SignColors
                {
                    top = Hex(0xa87a52), face = Wood, grain = Hex(0x80533a), bevel = WoodMid,
                    lip = WoodDark, lipLow = Hex(0x47281a), rivet = GoldDark, rivetLight = GoldLight, rivetDark = Hex(0x8a5a22)
                };
        }
    }

    /// <summary>Holzschild in Breite w (Brett samt Kontur), gedrueckt = Brett 2 px tiefer.</summary>
    public static Sprite MenuSign(int w, SignLook look, bool pressed) => Get("menusign" + w + look + pressed, () =>
    {
        SignColors k = ColorsFor(look);
        int W = w + 2;
        var c = new Px(W, SignH);

        // Schatten auf der Wand: unter der Kante, nach unten auslaufend.
        c.FillRounded(2, 17, W - 4, 4, 2, Hex(0x1c1419, 64));
        c.FillRounded(3, 19, W - 6, 3, 1, Hex(0x1c1419, 34));

        int drop = pressed ? 2 : 0;
        int top = 1 + drop;
        int h = 17 - drop;               // Kontur bis Kontur
        c.FillRounded(1, top, w, h, 2, Ink);
        c.FillRounded(2, top + 1, w - 2, h - 2, 1, k.face);

        int faceY = top + 1;
        int lipY = faceY + SignFaceH;     // Kante: 3 Zeilen, gedrueckt nur noch 1
        int lipH = 3 - drop;
        c.Fill(2, lipY, w - 2, lipH, k.lip);
        c.HLine(3, lipY + lipH - 1, w - 4, k.lipLow);
        if (lipH > 1) c.HLine(2, lipY, w - 2, k.lip);

        // Brett: Lichtkante oben, Fase links/rechts/unten.
        c.HLine(3, faceY, w - 4, k.top);
        c.VLine(2, faceY + 1, SignFaceH - 1, k.bevel);
        c.VLine(w - 1, faceY + 1, SignFaceH - 1, k.bevel);
        c.HLine(3, faceY + SignFaceH - 1, w - 4, k.bevel);

        // Maserung: zwei unterbrochene Linien, aus der Breite gewuerfelt - jedes Schild sieht anders aus.
        var rng = new System.Random(w * 7919 + (int)look * 31);
        foreach (int gy in new[] { 3, 8 })
        {
            int x = 5 + rng.Next(0, 6);
            while (x < w - 6)
            {
                int len = rng.Next(4, 11);
                c.HLine(x, faceY + gy, Mathf.Min(len, w - 6 - x), k.grain);
                x += len + rng.Next(8, 20);
            }
        }
        // Astloch
        int kx = 12 + rng.Next(0, Mathf.Max(1, w - 30));
        c.HLine(kx, faceY + 5, 2, k.grain);
        c.Set(kx - 1, faceY + 6, k.grain);
        c.Set(kx + 2, faceY + 6, k.grain);
        c.HLine(kx, faceY + 7, 2, k.grain);

        // Messingnieten links und rechts.
        foreach (int nx in new[] { 4, w - 5 })
        {
            int ny = faceY + 5;
            c.Set(nx, ny, k.rivetLight);
            c.Set(nx + 1, ny, k.rivet);
            c.Set(nx, ny + 1, k.rivet);
            c.Set(nx + 1, ny + 1, k.rivetDark);
            c.HLine(nx, ny + 2, 2, k.bevel);
        }
        return c;
    });

    /// <summary>Goldrahmen um das gewaehlte Schild - gleiche Bildgroesse wie <see cref="MenuSign"/>.</summary>
    public static Sprite MenuSignRing(int w) => Get("menusignring" + w, () =>
    {
        int W = w + 2;
        var c = new Px(W, SignH);
        c.FillRounded(0, 0, W, 19, 3, Gold);
        c.FillRounded(1, 1, w, 17, 2, Color.clear);
        // Unten eine Stufe dunkler - der Rahmen hat auch eine Kante.
        c.HLine(3, 0, W - 6, GoldLight);
        c.HLine(3, 18, W - 6, GoldDark);
        c.Set(2, 17, GoldDark);
        c.Set(W - 3, 17, GoldDark);
        return c;
    });

    // ==================================================================
    //  Symbole (9x9, Kontur legt Outlined drumherum -> 11x11)
    // ==================================================================

    public static Sprite MenuIcon(string key)
    {
        switch (key)
        {
            case "start": return Get("menuicon_start", () => Outlined(new[]
            {
                "ll.......",
                "lccc.....",
                "lccccc...",
                "lccccccc.",
                "lcccccccc",
                "lcccccss.",
                "lcccss...",
                "lcss.....",
                "ss.......",
            }, MenuIconColors));
            case "achievements": return Get("menuicon_trophy", () => Outlined(new[]
            {
                "lllllllll",
                "lyyyyyyyd",
                "y.lyyyd.d",
                "yllyyydd.",
                "..lyyyd..",
                "...yyd...",
                "....d....",
                "..lyyyd..",
                "..ddddd..",
            }, MenuIconColors));
            case "options": return Get("menuicon_gear", () => Outlined(new[]
            {
                "...lll...",
                ".l.lcc.c.",
                "..lcccc..",
                "llc...ccs",
                "lcc...css",
                "lcc...css",
                "..ccccs..",
                ".c.css.s.",
                "...sss...",
            }, MenuIconColors));
            case "quit": return Get("menuicon_door", () => Outlined(new[]
            {
                "lllllll..",
                "lwwwwwl..",
                "lwbbbwl..",
                "lwbbbwl..",
                "lwbbyws..",
                "lwbbbwl..",
                "lwbbbwl..",
                "lwbbbwl..",
                "sssssss..",
            }, MenuIconColors));
        }
        return null;
    }

    private static Dictionary<char, Color32> MenuIconColors => menuIconColors ??= new Dictionary<char, Color32>
    {
        { 'l', Cream }, { 'c', Parchment }, { 's', ParchDark },
        { 'y', Gold }, { 'd', GoldDark },
        { 'w', Parchment }, { 'b', WoodDark },
    };
    private static Dictionary<char, Color32> menuIconColors;

    // ==================================================================
    //  Keks-Zeiger und Funkeln
    // ==================================================================

    /// <summary>
    /// Der Keks aus dem Logo als Zeiger: rund, Gesicht, Baeckchen. 12x12 plus
    /// Kontur = 14x14. Schaut nach rechts (zum Schild); fuer rechts spiegeln.
    /// </summary>
    public static Sprite MenuCookie(bool blink) => Get("menucookie" + blink, () => Outlined(new[]
    {
        "....hhhh....",
        "..hhhhcckc..",
        ".hkcccccccs.",
        ".hcccckcccs.",
        "hccccccccccs",
        blink ? "hccccccccccs" : "hccccceccecs",
        "hkcccceccecs",
        "hccccbcmmcbs",
        ".cckccccccs.",
        ".ccccccckss.",
        "..sccsssss..",
        "....ssss....",
    }, CookieColors));

    private static Dictionary<char, Color32> CookieColors => cookieColors ??= new Dictionary<char, Color32>
    {
        { 'h', Hex(0xf6d29a) }, { 'c', Hex(0xe2a867) }, { 's', Hex(0xbf7c45) },
        { 'k', Hex(0x8a5230) }, { 'e', Ink }, { 'm', Ink }, { 'b', JamLight },
    };
    private static Dictionary<char, Color32> cookieColors;

    /// <summary>
    /// Lichtschimmer, der ueber ein Schild laeuft: schraeger Streifen, 3 Pixel
    /// breit plus duenne Nachlauflinie, halbdurchsichtiges warmes Creme (reines Creme wirkt auf Holz grau).
    /// </summary>
    public static Sprite MenuShine => Get("menushine", () =>
    {
        const int w = 11, h = SignFaceH;
        var c = new Px(w, h);
        Color32 strong = Hex(0xffe6b0, 110), weak = Hex(0xffe6b0, 55);
        for (int y = 0; y < h; y++)
        {
            int x0 = (h - 1 - y) / 2;
            c.HLine(x0, y, 3, strong);
            c.Set(x0 + 5, y, weak);
        }
        return c;
    });

    /// <summary>Funkeln wie die Sterne im Logo, 5x5 in drei Stufen (0 = Punkt, 2 = grosser Stern).</summary>
    public static Sprite MenuSparkle(int stage) => Get("menusparkle" + stage, () =>
    {
        var c = new Px(5, 5);
        switch (stage)
        {
            case 0:
                c.Set(2, 2, Cream);
                break;
            case 1:
                c.Set(2, 1, Gold); c.Set(1, 2, Gold); c.Set(3, 2, Gold); c.Set(2, 3, Gold);
                c.Set(2, 2, Cream);
                break;
            default:
                c.Set(2, 0, Gold); c.Set(0, 2, Gold); c.Set(4, 2, Gold); c.Set(2, 4, Gold);
                c.Set(2, 1, GoldLight); c.Set(1, 2, GoldLight); c.Set(3, 2, GoldLight); c.Set(2, 3, GoldLight);
                c.Set(2, 2, Cream);
                break;
        }
        return c;
    });
}
