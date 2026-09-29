using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafik fuer die Kapsel im Hub (<see cref="AchievementsBookPanel"/>) und den
/// Skilltree (<see cref="HubSkilltreeUI"/>). Gleiche Palette und gleiche Regeln
/// wie der Rest von <see cref="GameHudSkin"/>: zur Laufzeit gemalt,
/// pixelsPerUnit 100, Point-Filter, nach Groesse Gemaltes auch genau so gross
/// zeichnen.
/// </summary>
public static partial class GameHudSkin
{
    /// <summary>Kapselfluessigkeit - das Gelb, in dem die Blasen aufsteigen.</summary>
    public static readonly Color32 Capsule = Hex(0xf2c14e);

    // ---------- Symbole ----------

    /// <summary>Schluessel fuer den Reiter UNLOCKS.</summary>
    public static Sprite Key => Get("key", () => Outlined(new[]
    {
        ".lyy.....",
        "ly.oy....",
        "yo.oyyyyy",
        ".yoo..o.o",
    }, UiColors));

    /// <summary>Blase fuer die Kapsel: Ring mit Lichtpunkt, weiss - gefaerbt per Image.color.</summary>
    public static Sprite Bubble(int size) => Get("bubble" + size, () =>
    {
        var c = new Px(size, size);
        var ring = new Color32(255, 255, 255, 200);
        var body = new Color32(255, 255, 255, 60);
        FillEllipse(c, size, size, 1f, ring);
        if (size >= 5) FillEllipse(c, size, size, (size - 2f) / size, body);
        c.Set(size / 3, size / 3, new Color32(255, 255, 255, 255));
        return c;
    });

    /// <summary>
    /// Knopf ohne eigene Farbe: derselbe Aufbau wie <see cref="Button"/>, aber in
    /// Grautoenen - per Image.color bekommt der Knopf die Farbe einer
    /// Skilltree-Kategorie und behaelt Licht- und Schattenkante. 9-Slice, Rand 4.
    /// </summary>
    public static Sprite TintButton(bool lit) => Get("tintbutton" + lit, () =>
    {
        byte top = 255, fill = (byte)(lit ? 235 : 205), band = (byte)(lit ? 225 : 190), low = 140;
        var c = new Px(10, 10);
        c.FillRounded(0, 0, 10, 10, 2, Ink);
        c.FillRounded(1, 1, 8, 8, 1, Grey(fill));
        c.HLine(2, 1, 6, Grey(top));
        c.HLine(1, 2, 8, Grey(band));
        c.HLine(1, 7, 8, Grey(low));
        c.HLine(2, 8, 6, Grey(low));
        return c;
    }, new Vector4(4, 4, 4, 4));

    /// <summary>
    /// Dunkles Kaestchen in einem Knopf (hinter dem Kategoriesymbol): Kontur,
    /// eingelassen, oben ein Schattenstrich. 9-Slice, Rand 2.
    /// </summary>
    public static Sprite Socket => Get("socket", () =>
    {
        var c = new Px(6, 6);
        c.FillRounded(0, 0, 6, 6, 1, Ink);
        c.Fill(1, 1, 4, 4, CardDeep);
        c.HLine(1, 1, 4, Night);
        return c;
    }, new Vector4(2, 2, 2, 2));

    /// <summary>Waagerechte Trennlinie: 1 px dunkel, darunter 1 px Licht. 9-Slice, Rand 1.</summary>
    public static Sprite Rule => Get("rule", () =>
    {
        var c = new Px(3, 2);
        c.HLine(0, 0, 3, Night);
        Color32 hi = Cream; hi.a = 26;
        c.HLine(0, 1, 3, hi);
        return c;
    }, new Vector4(1, 0, 1, 0));

    /// <summary>
    /// Eckwinkel 5x5 fuer die Markierung eines Skilltree-Knotens, im
    /// Uhrzeigersinn ab links oben (0..3). Weiss mit Ink-Kontur innen -
    /// gefaerbt per Image.color, damit er auf jedem Grund steht.
    /// </summary>
    public static Sprite Corner(int i) => Get("corner" + i, () =>
    {
        var c = new Px(5, 5);
        var w = new Color32(255, 255, 255, 255);
        c.HLine(0, 0, 5, w);
        c.VLine(0, 0, 5, w);
        c.HLine(1, 1, 3, Ink);
        c.VLine(1, 1, 3, Ink);

        // Gemalt ist links oben - fuer die anderen Ecken spiegeln.
        bool flipX = i == 1 || i == 2, flipY = i == 2 || i == 3;
        var o = new Px(5, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                o.Set(flipX ? 4 - x : x, flipY ? 4 - y : y, c.Get(x, y));
        return o;
    });

    private static Color32 Grey(byte v) => new Color32(v, v, v, 255);
}
