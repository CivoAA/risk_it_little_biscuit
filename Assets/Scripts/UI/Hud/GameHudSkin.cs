using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Grafik des Lauf-HUDs (<see cref="GameHud"/>), Pixel fuer Pixel zur
/// Laufzeit gemalt - wie <see cref="SkilltreeSkin"/> im Hub. Keine Bilddatei,
/// kein Import, nichts im Inspector.
///
/// Farben kommen aus der Palette des Pausenmenues
/// (Assets/Art/UI_Objects/PauseMenu/pause_menu.gpl), damit HUD und Pause wie
/// aus einem Guss wirken: dunkle Kontur #3b2b33, Holz, Pergament, dazu je eine
/// Farbe fuer Leben (Marmelade), Erfahrung (Zuckerguss) und Gold.
///
/// Alle Sprites haben pixelsPerUnit 100 und Point-Filter. Mit 100 ist ein
/// Texel eines 9-Slice-Randes genau eine Canvas-Einheit - siehe
/// [[unity-ui-sliced-ppu]]. Symbole werden ohne Kontur beschrieben, die
/// 1-px-Kontur legt <see cref="Outlined"/> automatisch drumherum.
/// </summary>
public static class GameHudSkin
{
    // ==================================================================
    //  Palette
    // ==================================================================

    public static readonly Color32 Ink        = Hex(0x3b2b33);
    public static readonly Color32 Night      = Hex(0x1c1419);
    public static readonly Color32 Trough     = Hex(0x2b2028);
    public static readonly Color32 TroughLow  = Hex(0x221a20);

    public static readonly Color32 WoodDark   = Hex(0x5a3421);
    public static readonly Color32 WoodMid    = Hex(0x7a4f37);
    public static readonly Color32 Wood       = Hex(0x8a5a3d);
    public static readonly Color32 WoodLight  = Hex(0xa87a52);

    public static readonly Color32 Parchment  = Hex(0xf2dcbc);
    public static readonly Color32 ParchMid   = Hex(0xe9cfa8);
    public static readonly Color32 ParchDark  = Hex(0xd9b189);
    public static readonly Color32 Cream      = Hex(0xfff4e0);

    public static readonly Color32 Jam        = Hex(0xd4566c);
    public static readonly Color32 JamLight   = Hex(0xf08a9a);
    public static readonly Color32 JamDark    = Hex(0xb83a52);
    public static readonly Color32 JamDeep    = Hex(0x7d2338);

    public static readonly Color32 Icing      = Hex(0x6fb7e0);
    public static readonly Color32 IcingLight = Hex(0xb4e1f7);
    public static readonly Color32 IcingDark  = Hex(0x4a86b8);

    public static readonly Color32 Gold       = Hex(0xf2c14e);
    public static readonly Color32 GoldLight  = Hex(0xfbe39a);
    public static readonly Color32 GoldDark   = Hex(0xc98a2b);

    public static readonly Color32 Mint       = Hex(0x7ccf8a);
    public static readonly Color32 Rose       = Hex(0xf3c4bc);   // Schadensrest: hell, aber nicht weiss
    public static readonly Color32 Plum       = Hex(0x3a2030);

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    // ==================================================================
    //  Sprites (einmal gemalt, dann gecacht)
    // ==================================================================

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Ein weisser Pixel - fuer Flaechen, die per Image.color gefaerbt werden.</summary>
    public static Sprite White => Get("white", () =>
    {
        var c = new Px(1, 1);
        c.Set(0, 0, Color.white);
        return c;
    });

    /// <summary>
    /// Halbdurchsichtige Unterlage hinter Leisten und Chips: dunkel, Ecken
    /// mit Radius 2, oben eine hauchduenne Lichtkante. 9-Slice, Rand 3.
    /// </summary>
    public static Sprite Plate => Get("plate", () =>
    {
        var c = new Px(8, 8);
        Color32 fill = Night; fill.a = 170;
        c.FillRounded(0, 0, 8, 8, 2, fill);
        Color32 hi = Parchment; hi.a = 28;
        c.HLine(2, 0, 4, hi);
        return c;
    }, new Vector4(3, 3, 3, 3));

    /// <summary>
    /// Rahmen einer Leiste: 1 px Kontur mit gekappten Ecken, innen dunkle
    /// Rinne mit einer noch dunkleren Zeile oben (Innenschatten). 9-Slice, Rand 2.
    /// </summary>
    public static Sprite BarFrame => Get("barframe", () =>
    {
        var c = new Px(6, 6);
        c.FillRounded(0, 0, 6, 6, 1, Ink);
        c.Fill(1, 1, 4, 4, Trough);
        c.HLine(1, 1, 4, TroughLow);
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>
    /// Holzschild fuer die Uhr: Kontur mit Radius 2, zwei helle Zeilen oben,
    /// zwei dunkle unten, dazwischen Maserung. 9-Slice, Rand 4.
    /// </summary>
    public static Sprite Sign => Get("sign", () =>
    {
        var c = new Px(10, 10);
        c.FillRounded(0, 0, 10, 10, 2, Ink);
        c.FillRounded(1, 1, 8, 8, 1, WoodMid);
        c.HLine(2, 1, 6, WoodLight);
        c.HLine(1, 2, 8, Wood);
        c.HLine(1, 7, 8, WoodDark);
        c.HLine(2, 8, 6, WoodDark);
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>Kleines Abzeichen (Level): Gold mit Licht- und Schattenkante. 9-Slice, Rand 2.</summary>
    public static Sprite Badge => Get("badge", () =>
    {
        var c = new Px(6, 7);
        c.FillRounded(0, 0, 6, 7, 1, Ink);
        c.Fill(1, 1, 4, 5, Gold);
        c.HLine(1, 1, 4, GoldLight);
        c.HLine(1, 5, 4, GoldDark);
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>Band fuer Ansagen (Boss): Marmeladenrot, Rand 2.</summary>
    public static Sprite Ribbon => Get("ribbon", () =>
    {
        var c = new Px(6, 7);
        c.FillRounded(0, 0, 6, 7, 1, Ink);
        c.Fill(1, 1, 4, 5, JamDark);
        c.HLine(1, 1, 4, Jam);
        c.HLine(1, 5, 4, JamDeep);
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>Schildchen fuer die Stufenzahl an der Kachel: Kontur, dunkel, Lichtkante oben. 9-Slice, Rand 2.</summary>
    public static Sprite LevelChip => Get("levelchip", () =>
    {
        var c = new Px(6, 6);
        c.FillRounded(0, 0, 6, 6, 1, Ink);
        c.Fill(1, 1, 4, 4, Hex(0x241a21));
        Color32 hi = Cream; hi.a = 40;
        c.HLine(1, 1, 4, hi);
        return c;
    }, new Vector4(2, 2, 2, 2));

    public enum TileKind { Weapon, Buff, Evo, Empty }

    /// <summary>
    /// Eine Item-Kachel in genau dieser Groesse (nicht gedehnt): Kontur mit
    /// Radius 2, dunkle Flaeche, oben eine hauchfeine Lichtkante und unten eine
    /// 2 px hohe Farbkante, die die Art verraet - Waffen gold, Buffs mint, Evos
    /// rosa mit goldener Kontur. Leere Kacheln sind halb durchsichtig und
    /// haben statt der Farbe nur eine dunkle Rinne.
    /// </summary>
    public static Sprite Tile(int size, TileKind kind) => Get("tile" + size + kind, () =>
    {
        var c = new Px(size, size);
        bool empty = kind == TileKind.Empty;

        Color32 outline = kind == TileKind.Evo ? GoldDark : Ink;
        if (empty) outline.a = 170;
        c.FillRounded(0, 0, size, size, 2, outline);

        Color32 fill = Hex(0x241a21, (byte)(empty ? 120 : 235));
        c.FillRounded(1, 1, size - 2, size - 2, 1, fill);

        Color32 hi = Cream; hi.a = (byte)(empty ? 14 : 34);
        c.HLine(3, 1, size - 6, hi);

        Color32 edge, edgeHi;
        switch (kind)
        {
            case TileKind.Weapon: edge = GoldDark;   edgeHi = Gold;      break;
            case TileKind.Buff:   edge = Hex(0x4f9e6a); edgeHi = Mint;   break;
            case TileKind.Evo:    edge = JamDark;    edgeHi = JamLight;  break;
            default:              edge = Hex(0x1a1318, 150); edgeHi = edge; break;
        }
        c.HLine(2, size - 3, size - 4, edgeHi);
        c.HLine(2, size - 2, size - 4, edge);
        c.Set(1, size - 3, edge);
        c.Set(size - 2, size - 3, edge);

        if (kind == TileKind.Evo)
        {
            // Innen noch eine goldene Linie - Evos sind die Kronjuwelen.
            Color32 g = Gold; g.a = 150;
            c.HLine(2, 1, size - 4, g);
            c.VLine(1, 2, size - 5, g);
            c.VLine(size - 2, 2, size - 5, g);
        }
        return c;
    });

    /// <summary>
    /// Ring um das Portraet, 30x30: Kontur mit Radius 3, zwei Pixel Holz
    /// (oben hell, unten dunkel). Innen 24x24 durchsichtig bis auf die
    /// abgerundeten Innenecken - die decken das Portraet dort ab.
    /// </summary>
    public static Sprite Medallion => Get("medallion", () =>
    {
        const int s = 30;
        var c = new Px(s, s);
        c.FillRounded(0, 0, s, s, 3, Ink);
        c.FillRounded(1, 1, s - 2, s - 2, 2, Wood);
        c.HLine(3, 1, s - 6, WoodLight);
        c.VLine(1, 3, s - 6, WoodLight);
        c.HLine(3, s - 2, s - 6, WoodDark);
        c.VLine(s - 2, 3, s - 6, WoodDark);

        // Innen frei - bis auf je einen Eckpixel.
        c.Fill(3, 3, s - 6, s - 6, Clear);
        c.Set(3, 3, WoodMid);
        c.Set(s - 4, 3, WoodMid);
        c.Set(3, s - 4, WoodMid);
        c.Set(s - 4, s - 4, WoodMid);
        return c;
    });

    /// <summary>Hintergrund im Portraet: Pergament in drei harten Baendern, oben hell.</summary>
    public static Sprite MedallionBack => Get("medallionback", () =>
    {
        const int s = 24;
        var c = new Px(s, s);
        c.Fill(0, 0, s, 9, Parchment);
        c.Fill(0, 9, s, 8, ParchMid);
        c.Fill(0, 17, s, 7, ParchDark);
        // Ein heller Lichtkreis hinter dem Kopf, als Stufen statt Verlauf.
        c.FillRounded(6, 3, 12, 10, 3, Cream);
        return c;
    });

    /// <summary>Funkeln auf Evo-Slots: ein Plus, Mitte fast weiss. Ohne Kontur.</summary>
    public static Sprite Sparkle => Get("sparkle", () =>
    {
        var c = new Px(3, 3);
        c.Set(1, 0, GoldLight);
        c.Set(0, 1, GoldLight);
        c.Set(1, 1, Cream);
        c.Set(2, 1, GoldLight);
        c.Set(1, 2, GoldLight);
        return c;
    });

    // ---------- Symbole (Kontur kommt automatisch) ----------

    public static Sprite Heart => Get("heart", () => Outlined(new[]
    {
        ".hr.rr.",
        "hhrrrrd",
        "hrrrrrd",
        ".rrrrd.",
        "..rrd..",
        "...d...",
    }, IconColors));

    public static Sprite Gem => Get("gem", () => Outlined(new[]
    {
        "..w..",
        ".wbb.",
        "wbbbd",
        ".bbd.",
        "..d..",
    }, IconColors));

    public static Sprite Coin => Get("coin", () => Outlined(new[]
    {
        ".hhy.",
        "hyoyd",
        "hyoyd",
        "yyoyd",
        ".ydd.",
    }, GoldColors));

    /// <summary>Stern fuer Items auf hoechster Stufe.</summary>
    public static Sprite Star => Get("star", () => Outlined(new[]
    {
        "...h...",
        "..hyy..",
        "hhyyyyd",
        ".yyyyd.",
        ".yyoyd.",
        "yyo.ody",
        "yo...od",
    }, GoldColors));

    private static Dictionary<char, Color32> IconColors => iconColors ??= new Dictionary<char, Color32>
    {
        { 'r', Jam }, { 'h', JamLight }, { 'd', JamDark },
        { 'b', Icing }, { 'w', IcingLight },
        { 'y', Gold }, { 'o', GoldDark },
    };
    private static Dictionary<char, Color32> iconColors;

    // Muenze und Krone malen mit eigener Tabelle - dort sind 'h' und 'd'
    // die helle und dunkle Goldstufe, nicht die Marmeladentoene des Herzens.
    private static Dictionary<char, Color32> GoldColors => goldColors ??= new Dictionary<char, Color32>
    {
        { 'y', Gold }, { 'h', GoldLight }, { 'd', GoldDark }, { 'o', GoldDark }, { 'r', Jam },
    };
    private static Dictionary<char, Color32> goldColors;

    // ==================================================================
    //  Level-Up und Evo-Buch (LevelUpScreen)
    // ==================================================================

    public static readonly Color32 CardFill  = Hex(0x2b2028);
    public static readonly Color32 CardDeep  = Hex(0x1f171d);
    public static readonly Color32 MintLight = Hex(0xb2ebb9);
    public static readonly Color32 MintDark  = Hex(0x4f9e6a);
    public static readonly Color32 Stone     = Hex(0x6b5a62);
    public static readonly Color32 StoneLight= Hex(0x8c7a82);
    public static readonly Color32 StoneDark = Hex(0x4a3c43);

    /// <summary>Karte: Kontur mit Radius 3, dunkle Flaeche, Licht oben, Schatten unten. 9-Slice, Rand 4.</summary>
    public static Sprite Card => Get("card", () =>
    {
        var c = new Px(12, 12);
        c.FillRounded(0, 0, 12, 12, 3, Ink);
        c.FillRounded(1, 1, 10, 10, 2, CardFill);
        Color32 hi = Cream; hi.a = 30;
        c.HLine(3, 1, 6, hi);
        c.HLine(3, 10, 6, CardDeep);
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>
    /// Leuchtrahmen genau ueber einer <see cref="Card"/>: aussen deckend, eine
    /// halbe Zeile nach innen, Mitte frei. Weiss - gefaerbt wird per Image.color.
    /// </summary>
    public static Sprite Ring => Get("ring", () =>
    {
        var c = new Px(12, 12);
        c.FillRounded(0, 0, 12, 12, 3, new Color32(255, 255, 255, 255));
        c.FillRounded(1, 1, 10, 10, 2, new Color32(255, 255, 255, 110));
        c.FillRounded(2, 2, 8, 8, 1, Clear);
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>Senke in einer Karte (Fusszeile): eine Stufe dunkler, oben eine Kante. 9-Slice, Rand 2.</summary>
    public static Sprite Well => Get("well", () =>
    {
        var c = new Px(6, 6);
        c.FillRounded(0, 0, 6, 6, 1, CardDeep);
        c.HLine(1, 0, 4, Ink);
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>
    /// Rahmen fuer das grosse Symbol auf der Karte, 40x40, innen 32x32 plus
    /// Luft: Holzring (Evos: Goldring) um drei Pergamentbaender mit Lichthof.
    /// </summary>
    public static Sprite IconPlate(bool evo) => Get("iconplate" + evo, () =>
    {
        const int s = 40;
        var c = new Px(s, s);
        Color32 ring = evo ? Gold : Wood, light = evo ? GoldLight : WoodLight, dark = evo ? GoldDark : WoodDark;
        c.FillRounded(0, 0, s, s, 3, Ink);
        c.FillRounded(1, 1, s - 2, s - 2, 2, ring);
        c.HLine(3, 1, s - 6, light);
        c.VLine(1, 3, s - 6, light);
        c.HLine(3, s - 2, s - 6, dark);
        c.VLine(s - 2, 3, s - 6, dark);

        Color32 top = evo ? Hex(0xfbe0e4) : Parchment;
        Color32 mid = evo ? Hex(0xf6c9d0) : ParchMid;
        Color32 low = evo ? Hex(0xeaa9b4) : ParchDark;
        c.Fill(3, 3, s - 6, 12, top);
        c.Fill(3, 15, s - 6, 11, mid);
        c.Fill(3, 26, s - 6, s - 29, low);
        c.FillRounded(9, 6, s - 18, 22, 6, Cream);
        c.Set(3, 3, dark);
        c.Set(s - 4, 3, dark);
        c.Set(3, s - 4, dark);
        c.Set(s - 4, s - 4, dark);
        return c;
    });

    public enum PipKind { Empty, Full, Next }

    /// <summary>Stufenpunkt 5x5: leer (Loch), voll (Gold) oder die naechste Stufe (hell).</summary>
    public static Sprite Pip(PipKind kind) => Get("pip" + kind, () =>
    {
        var c = new Px(5, 5);
        c.FillRounded(0, 0, 5, 5, 1, Ink);
        switch (kind)
        {
            case PipKind.Full:
                c.Fill(1, 1, 3, 3, Gold);
                c.HLine(1, 1, 3, GoldLight);
                c.HLine(1, 3, 3, GoldDark);
                break;
            case PipKind.Next:
                c.Fill(1, 1, 3, 3, Cream);
                c.HLine(1, 3, 3, GoldLight);
                break;
            default:
                c.Fill(1, 1, 3, 3, Night);
                break;
        }
        return c;
    });

    public enum ButtonLook { Wood, Hover, Pressed, Active, Disabled, Gold, GoldHover, DangerHover }

    /// <summary>Holzknopf wie das Uhrenschild, in fuenf Zustaenden. 9-Slice, Rand 4.</summary>
    public static Sprite Button(ButtonLook look) => Get("button" + look, () =>
    {
        Color32 top, fill, band, low;
        switch (look)
        {
            case ButtonLook.Hover:    top = Hex(0xc9a06e); fill = WoodLight; band = Hex(0xb88a5e); low = WoodMid; break;
            case ButtonLook.Pressed:  top = WoodMid;       fill = WoodDark;  band = WoodDark;       low = Hex(0x472818); break;
            case ButtonLook.Active:   top = JamLight;      fill = Jam;       band = Jam;            low = JamDeep; break;
            case ButtonLook.Disabled: top = StoneLight;    fill = StoneDark; band = Stone;          low = Hex(0x3a2e34); break;
            case ButtonLook.Gold:     top = GoldLight;     fill = Gold;      band = Gold;           low = GoldDark; break;
            case ButtonLook.GoldHover:top = Cream;         fill = GoldLight; band = GoldLight;      low = Gold; break;
            case ButtonLook.DangerHover:top = Hex(0xf8c0c8); fill = JamLight; band = JamLight;     low = JamDark; break;
            default:                  top = WoodLight;     fill = WoodMid;   band = Wood;           low = WoodDark; break;
        }
        var c = new Px(10, 10);
        c.FillRounded(0, 0, 10, 10, 2, Ink);
        c.FillRounded(1, 1, 8, 8, 1, fill);
        c.HLine(2, 1, 6, top);
        c.HLine(1, 2, 8, band);
        c.HLine(1, 7, 8, low);
        c.HLine(2, 8, 6, low);
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>
    /// Schwalbenschwanz fuer das Titelband - haengt links bzw. rechts hinter
    /// dem <see cref="Ribbon"/>, eine Stufe dunkler und mit Kerbe.
    /// </summary>
    public static Sprite RibbonTail(bool left) => Get("ribbontail" + left, () =>
    {
        const int w = 12, h = 16;
        var c = new Px(w, h);
        for (int y = 0; y < h; y++)
        {
            int notch = Mathf.Max(0, 5 - Mathf.Abs(2 * y - (h - 1)) / 2);
            for (int x = notch; x < w; x++)
            {
                bool edge = x == notch || y == 0 || y == h - 1;
                Color32 col = edge ? Ink : y <= 1 ? JamDark : JamDeep;
                c.Set(left ? x : w - 1 - x, y, col);
            }
        }
        return c;
    });

    // ---------- Symbole fuer Level-Up (Kontur kommt automatisch) ----------

    public static Sprite Dice => Get("dice", () => Outlined(new[]
    {
        ".wwwww.",
        "wkwwwkw",
        "wwwwwww",
        "wwwkwww",
        "wwwwwww",
        "wkwwwkw",
        ".ggggg.",
    }, UiColors));

    public static Sprite Banish => Get("banish", () => Outlined(new[]
    {
        "hh...hh",
        "hrr.rrd",
        ".rrrrd.",
        "..rrd..",
        ".rrrrd.",
        "hrd.rrd",
        "dd...dd",
    }, UiColors));

    public static Sprite EvoArrow => Get("evoarrow", () => Outlined(new[]
    {
        "...h...",
        "..hrr..",
        ".hrrrd.",
        "hhrrrdd",
        "..rrd..",
        "..rrd..",
        "..ddd..",
    }, UiColors));

    public static Sprite Plus => Get("plus", () => Outlined(new[]
    {
        "..w..",
        "..w..",
        "wwwww",
        "..g..",
        "..g..",
    }, UiColors));

    public static Sprite Arrow => Get("arrow", () => Outlined(new[]
    {
        "....l..",
        "....yl.",
        "yyyyyyy",
        "....yo.",
        "....o..",
    }, UiColors));

    public static Sprite Check => Get("check", () => Outlined(new[]
    {
        "......m",
        ".....mn",
        "m...mn.",
        "mn.mn..",
        ".mmn...",
        "..n....",
    }, UiColors));

    // ---------- Mixer (Power-ups): Seltenheit und Stat-Symbole ----------

    public static readonly Color32 Grape      = Hex(0xa77bd8);
    public static readonly Color32 GrapeLight = Hex(0xcdb0ef);
    public static readonly Color32 GrapeDark  = Hex(0x7a4fb0);

    /// <summary>Titelband in Zuckerguss-Blau (Mixer) - wie <see cref="Ribbon"/>, Rand 2.</summary>
    public static Sprite RibbonBlue => Get("ribbonblue", () =>
    {
        var c = new Px(6, 7);
        c.FillRounded(0, 0, 6, 7, 1, Ink);
        c.Fill(1, 1, 4, 5, IcingDark);
        c.HLine(1, 1, 4, Icing);
        c.HLine(1, 5, 4, Hex(0x35628c));
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>Schwalbenschwanz zum blauen Band.</summary>
    public static Sprite RibbonTailBlue(bool left) => Get("ribbontailblue" + left, () =>
    {
        const int w = 12, h = 16;
        var c = new Px(w, h);
        for (int y = 0; y < h; y++)
        {
            int notch = Mathf.Max(0, 5 - Mathf.Abs(2 * y - (h - 1)) / 2);
            for (int x = notch; x < w; x++)
            {
                bool edge = x == notch || y == 0 || y == h - 1;
                Color32 col = edge ? Ink : y <= 1 ? IcingDark : Hex(0x35628c);
                c.Set(left ? x : w - 1 - x, y, col);
            }
        }
        return c;
    });

    // ==================================================================
    //  Optionen und Feedback (OptionsPanel, FeedbackPanel)
    // ==================================================================

    /// <summary>
    /// Eingabefeld: eingelassene dunkle Flaeche mit Schattenkante oben.
    /// Mit Fokus wird die Kontur golden. 9-Slice, Rand 3.
    /// </summary>
    public static Sprite Field(bool focused) => Get("field" + focused, () =>
    {
        var c = new Px(8, 8);
        c.FillRounded(0, 0, 8, 8, 1, focused ? Gold : Ink);
        c.Fill(1, 1, 6, 6, Hex(0x171116));
        c.HLine(1, 1, 6, Hex(0x0f0b0e));
        if (focused)
        {
            c.Set(0, 1, GoldDark);
            c.Set(7, 1, GoldDark);
        }
        return c;
    }, new Vector4(3, 3, 3, 3));

    public enum CellKind { Empty, Full, Hover }

    /// <summary>Eine Zelle der Lautstaerke-Leiste, 7x9: leer (Rinne), voll (Gold), unter der Maus (hell).</summary>
    public static Sprite VolumeCell(CellKind kind) => Get("volcell" + kind, () =>
    {
        var c = new Px(7, 9);
        c.FillRounded(0, 0, 7, 9, 1, Ink);
        switch (kind)
        {
            case CellKind.Full:
                c.Fill(1, 1, 5, 7, Gold);
                c.HLine(1, 1, 5, GoldLight);
                c.HLine(1, 7, 5, GoldDark);
                break;
            case CellKind.Hover:
                c.Fill(1, 1, 5, 7, GoldLight);
                c.HLine(1, 1, 5, Cream);
                c.HLine(1, 7, 5, Gold);
                break;
            default:
                c.Fill(1, 1, 5, 7, Trough);
                c.HLine(1, 1, 5, TroughLow);
                break;
        }
        return c;
    });

    /// <summary>Marienkaefer fuer den Feedback-Knopf.</summary>
    public static Sprite Bug => Get("bug", () => Outlined(new[]
    {
        ".k...k.",
        "..kkk..",
        ".rrkrr.",
        "rhrkrkr",
        "rrrkrrr",
        "rkrkrkr",
        ".rrkrr.",
    }, UiColors));

    /// <summary>Sprechblase fuer die Feedback-Art "Feedback".</summary>
    public static Sprite Speech => Get("speech", () => Outlined(new[]
    {
        ".wwwwww.",
        "wwwwwwww",
        "wkwkwkww",
        "wwwwwwww",
        ".wwwwwg.",
        "..ww....",
        ".w......",
    }, UiColors));

    public static Sprite Speed => Get("speed", () => Outlined(new[]
    {
        "c..c...",
        "bb.bb..",
        ".bb.bb.",
        "..be.be",
        ".bb.bb.",
        "bb.bb..",
        "e..e...",
    }, StatColors));

    public static Sprite Clover => Get("clover", () => Outlined(new[]
    {
        ".MM.mm.",
        "Mmmmmmn",
        ".mmmmn.",
        "mmm.mmn",
        "mmnmmnn",
        ".nn.nn.",
        "...n...",
    }, StatColors));

    /// <summary>Zwei Patronen mit dunklem Guertel - Extra-Schuesse.</summary>
    public static Sprite Shots => Get("shots", () => Outlined(new[]
    {
        ".l...l.",
        "lyo.lyo",
        "lyo.lyo",
        "yyo.yyo",
        "eee.eee",
        "yyo.yyo",
        "ooo.ooo",
    }, StatColors));

    public static Sprite Shield => Get("shield", () => Outlined(new[]
    {
        "ccccccc",
        "cbbbbbe",
        "cbccbbe",
        "cbbbbbe",
        ".bbbbe.",
        "..bbe..",
        "...e...",
    }, StatColors));

    public static Sprite Crit => Get("crit", () => Outlined(new[]
    {
        "...h...",
        ".h.r.h.",
        "..rrr..",
        "hrrlrrd",
        "..rrr..",
        ".d.r.d.",
        "...d...",
    }, StatColors));

    // ---------- Death-Screen ----------

    public static Sprite Clock => Get("clock", () => Outlined(new[]
    {
        "..www..",
        ".wwkww.",
        "wwwkwww",
        "wwwkkkw",
        "wwwwwww",
        ".wwwwg.",
        "..ggg..",
    }, BoneColors));

    public static Sprite Skull => Get("skull", () => Outlined(new[]
    {
        ".wwwww.",
        "wwwwwww",
        "wkkwkkw",
        "wkkwkkw",
        "wwwkwww",
        ".wgwgw.",
        ".g.g.g.",
    }, BoneColors));

    private static Dictionary<char, Color32> BoneColors => boneColors ??= new Dictionary<char, Color32>
    {
        { 'w', Cream }, { 'g', ParchDark }, { 'k', Ink },
    };
    private static Dictionary<char, Color32> boneColors;

    private static Dictionary<char, Color32> StatColors => statColors ??= new Dictionary<char, Color32>
    {
        { 'r', Jam }, { 'h', JamLight }, { 'd', JamDark },
        { 'b', Icing }, { 'c', IcingLight }, { 'e', IcingDark },
        { 'y', Gold }, { 'l', GoldLight }, { 'o', GoldDark },
        { 'm', Mint }, { 'M', MintLight }, { 'n', MintDark },
    };
    private static Dictionary<char, Color32> statColors;

    private static Dictionary<char, Color32> UiColors => uiColors ??= new Dictionary<char, Color32>
    {
        { 'w', Cream }, { 'g', ParchDark }, { 'k', Ink },
        { 'r', Jam }, { 'h', JamLight }, { 'd', JamDark },
        { 'y', Gold }, { 'l', GoldLight }, { 'o', GoldDark },
        { 'm', Mint }, { 'n', MintDark },
    };
    private static Dictionary<char, Color32> uiColors;

    // ==================================================================
    //  Werkzeug
    // ==================================================================

    public static Color32 Hex(int rgb, byte a = 255)
    {
        return new Color32((byte)((rgb >> 16) & 0xff), (byte)((rgb >> 8) & 0xff), (byte)(rgb & 0xff), a);
    }

    private static Sprite Get(string key, System.Func<Px> paint, Vector4 border = default)
    {
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
        s = Build(key, paint(), border);
        cache[key] = s;
        return s;
    }

    private static Sprite Build(string key, Px px, Vector4 border)
    {
        var tex = new Texture2D(px.W, px.H, TextureFormat.RGBA32, false)
        {
            name = "GameHud_" + key,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(px.Pixels);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, px.W, px.H), new Vector2(0.5f, 0.5f),
                                      100f, 0, SpriteMeshType.FullRect, border);
        sprite.name = "GameHud_" + key;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>
    /// Malt eine Zeichenkette-Grafik und legt eine 1-px-Kontur in Ink um alles,
    /// was nicht leer ist (nur die vier direkten Nachbarn - so bleiben die
    /// Ecken weich wie bei handgesetzter Pixelart).
    /// </summary>
    private static Px Outlined(string[] rows, Dictionary<char, Color32> colors)
    {
        int h = rows.Length;
        int w = rows[0].Length;
        var c = new Px(w + 2, h + 2);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!colors.TryGetValue(rows[y][x], out Color32 col)) continue;
                c.Set(x + 1, y + 1, col);
            }

        var solid = new bool[(w + 2) * (h + 2)];
        for (int y = 0; y < h + 2; y++)
            for (int x = 0; x < w + 2; x++)
                solid[y * (w + 2) + x] = c.Get(x, y).a > 0;

        for (int y = 0; y < h + 2; y++)
            for (int x = 0; x < w + 2; x++)
            {
                if (solid[y * (w + 2) + x]) continue;
                bool edge = (x > 0 && solid[y * (w + 2) + x - 1])
                         || (x < w + 1 && solid[y * (w + 2) + x + 1])
                         || (y > 0 && solid[(y - 1) * (w + 2) + x])
                         || (y < h + 1 && solid[(y + 1) * (w + 2) + x]);
                if (edge) c.Set(x, y, Ink);
            }

        return c;
    }

    /// <summary>
    /// Kleine Leinwand mit Ursprung OBEN links, so wie man Pixelart ausmisst.
    /// Texture2D zaehlt von unten - das dreht <see cref="Set"/> um.
    /// </summary>
    private sealed class Px
    {
        public readonly int W, H;
        public readonly Color32[] Pixels;

        public Px(int w, int h)
        {
            W = w; H = h;
            Pixels = new Color32[w * h];
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Pixels[(H - 1 - y) * W + x] = c;
        }

        public Color32 Get(int x, int y) => Pixels[(H - 1 - y) * W + x];

        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
        }

        public void HLine(int x, int y, int w, Color32 c) => Fill(x, y, w, 1, c);
        public void VLine(int x, int y, int h, Color32 c) => Fill(x, y, 1, h, c);

        /// <summary>
        /// Rechteck mit gekappten Ecken. Radius 1 laesst den Eckpixel weg,
        /// Radius 2 drei Pixel (L-Form), Radius 3 sechs (Treppe).
        /// </summary>
        public void FillRounded(int x, int y, int w, int h, int r, Color32 c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int dx = Mathf.Min(i, w - 1 - i);
                    int dy = Mathf.Min(j, h - 1 - j);
                    if (dx + dy < r) continue;
                    Set(x + i, y + j, c);
                }
        }
    }
}
