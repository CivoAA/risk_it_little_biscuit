using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafik fuer die Tutorials (<see cref="TutorialPanel"/>): Mauszeiger fuer die
/// Bildtafeln und die Symbole der Themenliste. Gleiche Regeln wie der Rest von
/// <see cref="GameHudSkin"/> - zur Laufzeit gemalt, Kontur kommt automatisch,
/// nur in ganzen Vielfachen zeichnen.
/// </summary>
public static partial class GameHudSkin
{
    /// <summary>Mauszeiger der Bildtafeln. Die Spitze sitzt bei (1, 1).</summary>
    public static Sprite Pointer => Get("pointer", () => Outlined(new[]
    {
        "w......",
        "ww.....",
        "www....",
        "wwww...",
        "wwwww..",
        "wwwwww.",
        "wwwwwww",
        "wwwg...",
        "ww.wg..",
        "w..wg..",
        "....wg.",
        "....g..",
    }, TutorialColors));

    /// <summary>Gedrueckter Zeiger: eine Stufe dunkler.</summary>
    public static Sprite PointerDown => Get("pointerdown", () => Outlined(new[]
    {
        "g......",
        "gg.....",
        "ggg....",
        "gggg...",
        "ggggg..",
        "gggggg.",
        "ggggggg",
        "gggo...",
        "gg.go..",
        "g..go..",
        "....go.",
        "....o..",
    }, TutorialColors));

    /// <summary>Goldpfeil nach unten - wie <see cref="Arrow"/>, nur gedreht.</summary>
    public static Sprite ArrowDown => Get("arrowdown", () => Outlined(new[]
    {
        "..y..",
        "..y..",
        "..y..",
        "lyyyo",
        ".lyo.",
        "..y..",
    }, TutorialColors));

    /// <summary>Stecknadel fuer die Reiseroute in der Levelauswahl.</summary>
    public static Sprite Pin => Get("pin", () => Outlined(new[]
    {
        "..rrr..",
        ".rhhrr.",
        "rrhwhrr",
        "rrrhrrr",
        "rrrrrrd",
        ".rrrrd.",
        "..rdd..",
        "...d...",
    }, TutorialColors));

    /// <summary>Hammer - Thema Werkbank.</summary>
    public static Sprite Hammer => Get("hammer", () => Outlined(new[]
    {
        "SsssssS",
        "SsIIIsS",
        "SSSbSSS",
        "...b...",
        "...b...",
        "...c...",
        "...B...",
    }, TutorialColors));

    /// <summary>Schokokeks mit Gesicht - Thema Charaktere.</summary>
    public static Sprite CookieFace => Get("cookieface", () => Outlined(new[]
    {
        "..ooo..",
        ".oyyyo.",
        "oykyyko",
        "oyyyyyo",
        "oyykyyo",
        ".oyyyo.",
        "..ooo..",
    }, TutorialColors));

    /// <summary>Offenes Buch - Thema Kapsel und der Tutorial-Knopf.</summary>
    public static Sprite Book => Get("book", () => Outlined(new[]
    {
        "hh.....hh",
        "rwwh.hwwr",
        "rwwwhwwwr",
        "rgwwhwwgr",
        "rwwwhwwwr",
        "rrrrdrrrr",
    }, TutorialColors));

    /// <summary>Gluehbirne fuer den Hinweiskasten.</summary>
    public static Sprite Bulb => Get("bulb", () => Outlined(new[]
    {
        ".lll.",
        "lyyyl",
        "lyyyl",
        ".yoy.",
        ".sss.",
        "..S..",
    }, TutorialColors));

    /// <summary>Chili - Thema Schwierigkeit (wie das Chili-Regal in Ricos Laden).</summary>
    public static Sprite Chili => Get("chili", () => Outlined(new[]
    {
        "....mm",
        "...mn.",
        "..rrr.",
        ".rhrr.",
        ".rhrd.",
        "rrrd..",
        "rrd...",
        "rd....",
    }, TutorialColors));

    /// <summary>Kleiner Stern der Levelauswahl: geschafft.</summary>
    public static Sprite MiniStar => Get("ministar", () => Outlined(new[]
    {
        "..l..",
        ".lyy.",
        "yyyyo",
        ".yyo.",
        ".y.o.",
    }, TutorialColors));

    /// <summary>Kleiner Stern der Levelauswahl: noch offen.</summary>
    public static Sprite MiniStarEmpty => Get("ministarempty", () => Outlined(new[]
    {
        "..s..",
        ".sSS.",
        "SSSSS",
        ".SSS.",
        ".S.S.",
    }, TutorialColors));

    private static Dictionary<char, Color32> TutorialColors => tutorialColors ??= new Dictionary<char, Color32>
    {
        { 'm', Mint }, { 'n', MintDark },
        { 'w', Cream }, { 'g', ParchDark }, { 'k', Ink },
        { 'r', Jam }, { 'h', JamLight }, { 'd', JamDark },
        { 'y', Gold }, { 'l', GoldLight }, { 'o', GoldDark },
        { 'b', WoodMid }, { 'B', WoodDark }, { 'c', WoodLight },
        { 's', StoneLight }, { 'S', Stone },
        { 'I', IcingLight },
    };
    private static Dictionary<char, Color32> tutorialColors;
}
