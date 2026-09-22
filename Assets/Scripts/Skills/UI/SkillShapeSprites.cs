using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zeichnet die Knotenformen (<see cref="SkillShape"/>) zur Laufzeit als
/// Pixeltextur - Kreis, Rechteck, Raute, Stern, Sechseck, Dreieck, Kreuz.
///
/// Es sind immer ZWEI Sprites je Form: die gefuellte Flaeche und der Rand darum.
/// Uebereinandergelegt und getrennt eingefaerbt ergibt das den Look aus dem
/// Konzeptbild - heller Kern, dunkle Kante. Alles ist weiss, gefaerbt wird ueber
/// Image.color.
///
/// Solange es keine gemalten Symbole gibt, haengt der Skilltree damit an keiner
/// einzigen Bilddatei. Wer spaeter echte Assets hat, traegt sie im Knoten unter
/// "Icon" ein - das gewinnt dann ueber die Form.
///
/// Wie <see cref="HubPixelSprites"/>: jede UI legt sich eine eigene Instanz an
/// und gibt sie in OnDestroy wieder frei, sonst bleiben die Texturen liegen.
/// </summary>
public sealed class SkillShapeSprites : System.IDisposable
{
    /// <summary>Muss dem referencePixelsPerUnit des Canvas entsprechen (Standard 100).</summary>
    const float UnitsPerPixel = 100f;

    /// <summary>Kantenlaenge der erzeugten Texturen. Ungerade Zahl - dann gibt es
    /// eine echte Mitte und die Formen sitzen symmetrisch.</summary>
    public const int Size = 15;

    readonly List<Object> created = new List<Object>();
    readonly Dictionary<SkillShape, Sprite> fills = new Dictionary<SkillShape, Sprite>();
    readonly Dictionary<SkillShape, Sprite> outlines = new Dictionary<SkillShape, Sprite>();
    readonly Dictionary<SkillShape, Sprite> glosses = new Dictionary<SkillShape, Sprite>();

    Sprite padlock, check, line, dot;

    /// <summary>Die gefuellte Flaeche einer Form.</summary>
    public Sprite Fill(SkillShape shape)
    {
        if (fills.TryGetValue(shape, out Sprite s) && s != null) return s;

        s = Build("SkillFill_" + shape, Mask(shape));
        fills[shape] = s;
        return s;
    }

    /// <summary>
    /// Der Rand derselben Form: jeder Pixel, der in der Flaeche liegt und
    /// mindestens einen Nachbarn ausserhalb hat. So passt die Kante immer exakt
    /// zur Fuellung, egal wie die Form aussieht.
    /// </summary>
    public Sprite Outline(SkillShape shape)
    {
        if (outlines.TryGetValue(shape, out Sprite s) && s != null) return s;

        bool[,] solid = Mask(shape);
        var edge = new bool[Size, Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (!solid[x, y]) continue;

                bool border = x == 0 || y == 0 || x == Size - 1 || y == Size - 1 ||
                              !solid[x - 1, y] || !solid[x + 1, y] ||
                              !solid[x, y - 1] || !solid[x, y + 1];

                edge[x, y] = border;
            }
        }

        s = Build("SkillOutline_" + shape, edge);
        outlines[shape] = s;
        return s;
    }

    /// <summary>
    /// Das Glanzlicht auf derselben Form: ein zwei Pixel breiter Bogen links
    /// oben, wie ihn eine Kugel im Licht bekommt. Darueber gelegt und weiss
    /// eingefaerbt wird aus einer flachen Scheibe eine Kugel.
    ///
    /// Gerechnet wird es aus der Form selbst - zweimal nach innen geschrumpft
    /// ergibt den aeusseren Rand des Bogens, viermal den inneren. So sitzt der
    /// Glanz auf jeder Form dort, wo er hingehoert, auch auf Stern und Raute.
    /// </summary>
    public Sprite Gloss(SkillShape shape)
    {
        if (glosses.TryGetValue(shape, out Sprite s) && s != null) return s;

        bool[,] outer = Erode(Mask(shape), 2);
        bool[,] inner = Erode(outer, 2);

        var arc = new bool[Size, Size];
        const float c = (Size - 1) * 0.5f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (!outer[x, y] || inner[x, y]) continue;

                // y zaehlt von unten: oben links liegt also zwischen 105 und 170 Grad.
                float angle = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg;
                arc[x, y] = angle >= 103f && angle <= 172f;
            }
        }

        s = Build("SkillGloss_" + shape, arc);
        glosses[shape] = s;
        return s;
    }

    /// <summary>Schrumpft eine Maske um <paramref name="steps"/> Pixel nach innen.</summary>
    static bool[,] Erode(bool[,] src, int steps)
    {
        bool[,] cur = src;

        for (int i = 0; i < steps; i++)
        {
            var next = new bool[Size, Size];

            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    next[x, y] = cur[x, y] && cur[x - 1, y] && cur[x + 1, y] &&
                                 cur[x, y - 1] && cur[x, y + 1];
                }
            }

            cur = next;
        }

        return cur;
    }

    /// <summary>Schloss auf gesperrten Knoten - wie im Konzeptbild.</summary>
    public Sprite Padlock => padlock != null ? padlock : (padlock = BuildFn("SkillPadlock", 7, 9, (x, y) =>
    {
        if (y <= 4) return !(x == 3 && y >= 1 && y <= 2);  // Koerper mit Schluesselloch
        if (y == 8) return x >= 2 && x <= 4;               // Buegel oben
        return x == 2 || x == 4;                           // Buegel seitlich
    }));

    /// <summary>Haken auf gekauften Knoten, wenn kein eigenes Symbol da ist.</summary>
    public Sprite Check => check != null ? check : (check = BuildFn("SkillCheck", 7, 7, (x, y) =>
        (x <= 2 && (y == 4 - x || y == 5 - x)) ||
        (x >= 2 && (y == x || y == x + 1))));

    /// <summary>Eine weisse Flaeche - daraus werden die Verbindungslinien.</summary>
    public Sprite Line => line != null ? line : (line = BuildFn("SkillLine", 1, 1, (x, y) => true));

    /// <summary>Ein 3x3-Punkt fuer den Knick einer Linie zwischen zwei Bahnen.</summary>
    public Sprite Dot => dot != null ? dot : (dot = BuildFn("SkillDot", 3, 3, (x, y) => true));

    // ==================================================================
    //  Die Formen selbst
    // ==================================================================

    static bool[,] Mask(SkillShape shape)
    {
        var m = new bool[Size, Size];
        const float c = (Size - 1) * 0.5f;   // Mitte, bei 15 also 7

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                m[x, y] = Inside(shape, dx, dy, c);
            }
        }

        return m;
    }

    static bool Inside(SkillShape shape, float dx, float dy, float r)
    {
        switch (shape)
        {
            case SkillShape.Kreis:
                return dx * dx + dy * dy <= (r + 0.4f) * (r + 0.4f);

            case SkillShape.Rechteck:
                // Ein Quadrat mit abgeschnittenen Ecken - ein voellig gerades
                // Quadrat sieht neben den runden Formen zu klobig aus.
                return Mathf.Abs(dx) <= r && Mathf.Abs(dy) <= r &&
                       Mathf.Abs(dx) + Mathf.Abs(dy) <= r * 1.75f;

            case SkillShape.Raute:
                return Mathf.Abs(dx) + Mathf.Abs(dy) <= r + 0.5f;

            case SkillShape.Sechseck:
                // Zwei schraege Kanten links und rechts, oben und unten gerade.
                return Mathf.Abs(dy) <= r && Mathf.Abs(dx) <= r &&
                       Mathf.Abs(dx) * 0.58f + Mathf.Abs(dy) <= r + 0.5f;

            case SkillShape.Dreieck:
                // Spitze oben. y zaehlt von unten, deshalb die Umrechnung.
                {
                    float top = r - dy;                       // 0 an der Spitze
                    return dy <= r && top >= 0f && Mathf.Abs(dx) <= top * 0.52f;
                }

            case SkillShape.Kreuz:
                {
                    float arm = r * 0.36f;
                    return (Mathf.Abs(dx) <= arm && Mathf.Abs(dy) <= r) ||
                           (Mathf.Abs(dy) <= arm && Mathf.Abs(dx) <= r);
                }

            case SkillShape.Stern:
                return InsideStar(dx, dy, r);

            default:
                return dx * dx + dy * dy <= r * r;
        }
    }

    /// <summary>
    /// Fuenfzackiger Stern: der erlaubte Abstand vom Mittelpunkt pendelt mit dem
    /// Winkel zwischen Zacken- und Kerbenradius. Einfacher als Polygone zu
    /// schneiden und bei 15x15 Pixeln nicht zu unterscheiden.
    /// </summary>
    static bool InsideStar(float dx, float dy, float r)
    {
        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f)) return true;

        float outer = r + 0.5f;
        float inner = outer * 0.45f;

        // Winkel so drehen, dass eine Zacke nach oben zeigt.
        float angle = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;

        // Auf einen Zackenabschnitt falten (360/5 = 72 Grad, halbiert 36).
        float step = Mathf.PI * 2f / 5f;
        float t = Mathf.Repeat(angle, step) / step;          // 0..1 im Abschnitt
        float k = Mathf.Abs(t - 0.5f) * 2f;                  // 1 an der Zacke, 0 in der Kerbe

        float allowed = Mathf.Lerp(inner, outer, k);
        return dx * dx + dy * dy <= allowed * allowed;
    }

    // ==================================================================
    //  Textur bauen
    // ==================================================================

    Sprite Build(string name, bool[,] solid) =>
        BuildFn(name, Size, Size, (x, y) => solid[x, y]);

    Sprite BuildFn(string name, int w, int h, System.Func<int, int, bool> solid)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
            name       = name,
        };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, solid(x, y) ? Color.white : Color.clear);
        }

        tex.Apply();

        // Wie bei HubPixelSprites: pixelsPerUnit MUSS zur Canvas-Referenz passen,
        // sonst stimmt die Groesse nicht mehr mit den ausgemessenen Kaesten ueberein.
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f),
                                 UnitsPerPixel, 0, SpriteMeshType.FullRect);
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
        fills.Clear();
        outlines.Clear();
        glosses.Clear();
        padlock = check = line = dot = null;
    }
}
