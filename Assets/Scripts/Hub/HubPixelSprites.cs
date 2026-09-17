using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kleine Symbole, die zur Laufzeit als Pixel-Textur entstehen: Rahmen, Pfeil,
/// Schloss, Haken. Solange es dafuer keine gemalten Assets gibt, haengt die
/// Hub-UI an keiner einzigen Bilddatei - und was hier rauskommt, ist garantiert
/// pixelgenau, weil jede Textur in ihrer echten Groesse gezeichnet wird.
///
/// Die Sprites sind weiss, eingefaerbt wird ueber Image.color. Wer sie ersetzen
/// will, traegt im jeweiligen UI-Skript einfach ein eigenes Sprite ein.
///
/// Jede UI legt sich eine eigene Instanz an und gibt sie in OnDestroy wieder
/// frei - Unity raeumt zur Laufzeit erzeugte Texturen nicht von selbst weg.
/// </summary>
public sealed class HubPixelSprites : System.IDisposable
{
    /// <summary>
    /// Muss dem referencePixelsPerUnit des Canvas entsprechen (Unity-Standard 100).
    /// </summary>
    const float UnitsPerPixel = 100f;

    readonly List<Object> created = new List<Object>();

    Sprite frame, arrowDown, arrowLeft, arrowRight, padlock, check, disc, plus;

    /// <summary>1px-Rahmen als 3x3-Sprite mit 9-Slice-Raendern: die Linie bleibt
    /// genau einen Design-Pixel breit, egal wie gross die Flaeche wird.</summary>
    public Sprite Frame => frame != null ? frame : (frame = Build("HubFrame", 3, 3,
        (x, y) => !(x == 1 && y == 1), new Vector4(1f, 1f, 1f, 1f)));

    /// <summary>Zeiger ueber der gewaehlten Karte. Spitze nach unten.</summary>
    public Sprite ArrowDown => arrowDown != null ? arrowDown : (arrowDown = Build("HubArrowDown", 9, 5,
        // y zaehlt von unten: die Spitze sitzt in Zeile 0, die breite Kante oben.
        (x, y) => Mathf.Abs(x - 4) <= y));

    /// <summary>Blaetterpfeil, wenn mehr Level da sind als Platz.</summary>
    public Sprite ArrowLeft => arrowLeft != null ? arrowLeft : (arrowLeft = Build("HubArrowLeft", 5, 9,
        (x, y) => Mathf.Abs(y - 4) <= x));

    public Sprite ArrowRight => arrowRight != null ? arrowRight : (arrowRight = Build("HubArrowRight", 5, 9,
        (x, y) => Mathf.Abs(y - 4) <= 4 - x));

    /// <summary>Schloss fuer gesperrte Level: Koerper mit Buegel und Schluesselloch.</summary>
    public Sprite Padlock => padlock != null ? padlock : (padlock = Build("HubPadlock", 7, 10, (x, y) =>
    {
        if (y <= 5)                                   // Koerper
            return !(x == 3 && y >= 2 && y <= 3);     // Schluesselloch bleibt frei
        if (y == 9) return x >= 1 && x <= 5;          // Buegel oben
        return x == 1 || x == 5;                      // Buegel seitlich
    }));

    /// <summary>Gefuellte Scheibe - Platzhalter fuer Kategorie-Symbole und die
    /// grosse Kugel im Beschreibungsfeld, solange es dafuer keine Icons gibt.</summary>
    public Sprite Disc => disc != null ? disc : (disc = Build("HubDisc", 16, 16, (x, y) =>
    {
        float dx = x - 7.5f, dy = y - 7.5f;
        return dx * dx + dy * dy <= 7.5f * 7.5f;
    }));

    /// <summary>Kleines Kreuz, wie es auf den Bannern und Trennlinien sitzt.</summary>
    public Sprite Plus => plus != null ? plus : (plus = Build("HubPlus", 5, 5,
        (x, y) => x == 2 || y == 2));

    /// <summary>Haken in der Endless-Box.</summary>
    public Sprite Check => check != null ? check : (check = Build("HubCheck", 7, 7, (x, y) =>
        // Zwei Schenkel, je zwei Pixel dick - y zaehlt von unten, der Knick
        // sitzt darum links unten bei x=2.
        (x <= 2 && (y == 4 - x || y == 5 - x)) ||
        (x >= 2 && (y == x || y == x + 1))));

    Sprite Build(string name, int w, int h, System.Func<int, int, bool> solid, Vector4? border = null)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, solid(x, y) ? Color.white : Color.clear);
        tex.Apply();

        // Pixel-per-Unit MUSS zur Referenz des Canvas passen (Standard 100).
        // Ein Image rechnet die 9-Slice-Raender mit
        // sprite.pixelsPerUnit / canvas.referencePixelsPerUnit um - bei einer 1
        // hier wuerde aus einem 1px-Rand ein 100 Einheiten breiter, und Unity
        // zieht ihn dann auf die ganze Flaeche zusammen: der "Rahmen" deckt
        // alles zu. Mit 100 bleibt ein Texturpixel genau eine Canvas-Einheit,
        // also ein Pixel der 320x180-Vorlage.
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f),
                                 UnitsPerPixel, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
        s.name = name;

        created.Add(tex);
        created.Add(s);
        return s;
    }

    public void Dispose()
    {
        foreach (Object o in created)
        {
            if (o == null) continue;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
        created.Clear();
        frame = arrowDown = arrowLeft = arrowRight = padlock = check = disc = plus = null;
    }
}
