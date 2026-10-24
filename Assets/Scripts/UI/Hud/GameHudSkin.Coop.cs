using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafik fuer das Koop-HUD (<see cref="NetHud"/>) und die Team-Abrechnung:
/// Sprechblasen fuer Emotes, runde Portraet-Rahmen fuer die Randzeiger,
/// Symbole fuer die Spalten der Abrechnung. Gleiche Regeln wie der Rest von
/// <see cref="GameHudSkin"/> - zur Laufzeit gemalt, nur ganze Vielfache.
/// </summary>
public static partial class GameHudSkin
{
    /// <summary>Sprechblase: Creme mit Kontur und Schattenkante unten. 9-Slice, Rand 3.</summary>
    public static Sprite EmoteBubble => Get("emotebubble", () =>
    {
        var c = new Px(8, 8);
        c.FillRounded(0, 0, 8, 8, 2, Ink);
        c.FillRounded(1, 1, 6, 6, 1, Cream);
        c.HLine(2, 6, 4, ParchMid);
        return c;
    }, new Vector4(3, 3, 3, 3));

    /// <summary>Spitze unter der Sprechblase (5x4), oben buendig an die Blase.</summary>
    public static Sprite EmoteTail => Get("emotetail", () =>
    {
        var c = new Px(5, 4);
        c.HLine(0, 0, 5, Ink);
        c.HLine(1, 0, 3, ParchMid);
        c.Set(0, 1, Ink); c.Set(4, 1, Ink);
        c.HLine(1, 1, 3, Cream);
        c.Set(1, 2, Ink); c.Set(3, 2, Ink);
        c.Set(2, 2, Cream);
        c.Set(2, 3, Ink);
        return c;
    });

    /// <summary>Volle weisse Scheibe - Maske fuer runde Portraets und per Farbe getoenter Ring dahinter.</summary>
    public static Sprite Disc(int size) => Get("disc" + size, () =>
    {
        var c = new Px(size, size);
        FillEllipse(c, size, size, 1f, new Color32(255, 255, 255, 255));
        return c;
    });

    /// <summary>Ring in Ink um eine Scheibe (fuer den Rand des Portraet-Knopfs).</summary>
    public static Sprite DiscOutline(int size) => Get("discoutline" + size, () =>
    {
        var c = new Px(size, size);
        FillEllipse(c, size, size, 1f, Ink);
        var hole = new Px(size, size);
        FillEllipse(hole, size, size, (size - 2f) / size, Ink);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (hole.Get(x, y).a > 0) c.Set(x, y, Clear);
        return c;
    });

    /// <summary>Ausrufezeichen fuer "Hilfe!".</summary>
    public static Sprite Exclaim => Get("exclaim", () => Outlined(new[]
    {
        "hr",
        "hr",
        "hr",
        "rd",
        "..",
        "hr",
        "rd",
    }, IconColors));

    /// <summary>Schwert fuer die Schadens-Spalte.</summary>
    public static Sprite Sword => Get("sword", () => Outlined(new[]
    {
        ".....ww",
        "....wwg",
        "...wwg.",
        "o.wwg..",
        ".owg...",
        ".oo....",
        "o..o...",
    }, UiColors));

    /// <summary>Kleiner Geist fuer "am Boden gelegen".</summary>
    public static Sprite Ghost => Get("ghost", () => Outlined(new[]
    {
        "..www..",
        ".wwwww.",
        "wwkwkww",
        "wwkwkww",
        "wwwwwww",
        "wwwwwwb",
        "w.wbw.b",
    }, GhostColors));

    /// <summary>Herz mit Plus fuer Rettungen.</summary>
    public static Sprite ReviveHeart => Get("reviveheart", () => Outlined(new[]
    {
        ".hm.rr.",
        "hmmmrrd",
        "hrmrrrd",
        ".rrrrd.",
        "..rrd..",
        "...d...",
    }, ReviveColors));

    private static Dictionary<char, Color32> GhostColors => ghostColors ??= new Dictionary<char, Color32>
    {
        { 'w', IcingLight }, { 'b', Icing }, { 'k', Ink },
    };
    private static Dictionary<char, Color32> ghostColors;

    private static Dictionary<char, Color32> ReviveColors => reviveColors ??= new Dictionary<char, Color32>
    {
        { 'r', Jam }, { 'h', JamLight }, { 'd', JamDark }, { 'm', MintLight },
    };
    private static Dictionary<char, Color32> reviveColors;
}
