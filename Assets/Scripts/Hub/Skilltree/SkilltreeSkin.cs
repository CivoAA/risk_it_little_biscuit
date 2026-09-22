using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DER ZIERRAT DES SKILLTREE-FENSTERS.
///
/// Alles, was das Fenster ueber nackte Rechtecke hinaushebt, entsteht hier zur
/// Laufzeit als Pixeltextur: abgerundete Flaechen und Rahmen, die hellen und
/// dunklen Kanten darin, Eckwinkel, Nieten, Funken, die Spitzen am Banner, das
/// Papierkorn und das warme Licht hinter dem Papier.
///
/// Warum erzeugt statt gemalt: die Masse stehen im Inspector und koennen sich
/// aendern. Ein gemalter Rahmen muesste dann neu geschnitten werden - ein
/// 9-Slice, der hier aus drei Zahlen faellt, passt sich von selbst an. Und
/// solange es keine gemalten Assets gibt, haengt das Fenster an keiner Bilddatei.
///
/// Alles ist WEISS. Gefaerbt wird ueber Image.color - so traegt ein Rahmen
/// einmal die Kategoriefarbe und einmal die Papierkante, ohne zweite Textur.
///
/// PIXELGENAU: jedes Sprite bekommt pixelsPerUnit 100 - denselben Wert wie
/// canvas.referencePixelsPerUnit. Sonst rechnet Unity die 9-Slice-Raender um
/// und der 1px-Rahmen deckt die ganze Flaeche zu (siehe HubPixelSprites).
///
/// Wie HubPixelSprites: eine Instanz je Fenster, in OnDestroy wieder freigeben.
/// </summary>
public sealed class SkilltreeSkin : System.IDisposable
{
    /// <summary>Muss dem referencePixelsPerUnit des Canvas entsprechen (Standard 100).</summary>
    const float UnitsPerPixel = 100f;

    readonly List<Object> created = new List<Object>();

    // Nach Eckenradius abgelegt - dieselbe Rundung wird im Fenster oft gebraucht.
    readonly Dictionary<int, Sprite> fills   = new Dictionary<int, Sprite>();
    readonly Dictionary<int, Sprite> edges   = new Dictionary<int, Sprite>();
    readonly Dictionary<int, Sprite> capsTop = new Dictionary<int, Sprite>();
    readonly Dictionary<int, Sprite> capsBot = new Dictionary<int, Sprite>();

    readonly Dictionary<string, Sprite> odds = new Dictionary<string, Sprite>();

    Sprite sparkle, spark, stud, gem, arrowLeft, glow, vignette;
    Sprite[] brackets;

    // ==================================================================
    //  Abgerundete Flaechen - der Grundbaustein
    // ==================================================================

    /// <summary>
    /// Eine gefuellte Flaeche mit abgeschraegten Ecken, als 9-Slice. Der Radius
    /// ist die Zahl der Pixel, die an jeder Ecke diagonal wegfallen: 1 knipst
    /// nur den Eckpixel weg, 3 gibt die weiche Kante des grossen Papiers.
    /// </summary>
    public Sprite Fill(int radius)
    {
        if (fills.TryGetValue(radius, out Sprite s) && s != null) return s;

        int size = Size(radius);
        s = Build("SkinFill" + radius, size, size,
                  (x, y) => Solid(x, y, size, radius), Slice(radius));
        fills[radius] = s;
        return s;
    }

    /// <summary>
    /// Der 1px-Rand derselben Form, innen offen. Nicht ueber Nachbarpixel
    /// gesucht, sondern direkt gezeichnet: ein Sprite von 5x5 hat zu wenig
    /// Flaeche, als dass eine Kantensuche die spaeter gestreckten Seiten noch
    /// durchgehend traefe.
    /// </summary>
    public Sprite Edge(int radius)
    {
        if (edges.TryGetValue(radius, out Sprite s) && s != null) return s;

        int size = Size(radius);
        s = Build("SkinEdge" + radius, size, size,
                  (x, y) => Rim(x, y, size, radius), Slice(radius));
        edges[radius] = s;
        return s;
    }

    /// <summary>
    /// Nur die oberste Linie der Form - der helle Saum, der einen Knopf
    /// erhaben aussehen laesst. Die Rundung bleibt ausgespart, die Linie
    /// endet also nicht hart in der Ecke.
    /// </summary>
    public Sprite CapTop(int radius)
    {
        if (capsTop.TryGetValue(radius, out Sprite s) && s != null) return s;

        int size = Size(radius);
        // y zaehlt von unten - die oberste Zeile ist size-1.
        s = Build("SkinCapTop" + radius, size, size,
                  (x, y) => y == size - 1 && Solid(x, y, size, radius), Slice(radius));
        capsTop[radius] = s;
        return s;
    }

    /// <summary>Die unterste Linie - der Schatten unter dem hellen Saum.</summary>
    public Sprite CapBottom(int radius)
    {
        if (capsBot.TryGetValue(radius, out Sprite s) && s != null) return s;

        int size = Size(radius);
        s = Build("SkinCapBottom" + radius, size, size,
                  (x, y) => y == 0 && Solid(x, y, size, radius), Slice(radius));
        capsBot[radius] = s;
        return s;
    }

    /// <summary>Kantenlaenge: links und rechts je Radius plus Rand, dazu 1px Mitte.</summary>
    static int Size(int radius) => 2 * radius + 3;

    static Vector4 Slice(int radius)
    {
        float m = radius + 1f;
        return new Vector4(m, m, m, m);
    }

    /// <summary>Wie weit ein Pixel von der naechsten Ecke weg ist.</summary>
    static void CornerDistance(int x, int y, int size, out int dx, out int dy)
    {
        dx = Mathf.Min(x, size - 1 - x);
        dy = Mathf.Min(y, size - 1 - y);
    }

    static bool Solid(int x, int y, int size, int radius)
    {
        CornerDistance(x, y, size, out int dx, out int dy);
        return dx + dy >= radius;
    }

    /// <summary>Die Aussenkante: die vier Seiten und die Schraege in den Ecken.</summary>
    static bool Rim(int x, int y, int size, int radius)
    {
        if (!Solid(x, y, size, radius)) return false;

        CornerDistance(x, y, size, out int dx, out int dy);
        return dx == 0 || dy == 0 || dx + dy == radius;
    }

    // ==================================================================
    //  Zierrat
    // ==================================================================

    /// <summary>Vierzackiger Funke mit Raute in der Mitte - wie im Konzeptbild.</summary>
    public Sprite Sparkle => sparkle != null ? sparkle : (sparkle = Build("SkinSparkle", 7, 7, (x, y) =>
    {
        int dx = Mathf.Abs(x - 3), dy = Mathf.Abs(y - 3);
        return dx == 0 || dy == 0 || dx + dy <= 2;
    }));

    /// <summary>Der kleine Bruder davon: ein Kreuz aus fuenf Pixeln.</summary>
    public Sprite Spark => spark != null ? spark : (spark = Build("SkinSpark", 3, 3, (x, y) =>
        x == 1 || y == 1));

    /// <summary>Niete in den Ecken des Titelschilds.</summary>
    public Sprite Stud => stud != null ? stud : (stud = Build("SkinStud", 3, 3, (x, y) =>
        Mathf.Abs(x - 1) + Mathf.Abs(y - 1) <= 1));

    /// <summary>Raute vor der Punkteanzeige - der Skillpunkt als Symbol.</summary>
    public Sprite Gem => gem != null ? gem : (gem = Build("SkinGem", 7, 7, (x, y) =>
        Mathf.Abs(x - 3) + Mathf.Abs(y - 3) <= 3));

    /// <summary>Der Pfeil im Zurueck-Knopf: Spitze links, Schaft nach rechts.</summary>
    public Sprite ArrowLeft => arrowLeft != null ? arrowLeft : (arrowLeft = Build("SkinArrowLeft", 7, 7, (x, y) =>
    {
        int dy = Mathf.Abs(y - 3);
        if (x <= 2) return dy <= x;          // Spitze: bei x=0 ein Pixel, bei x=2 fuenf
        return dy <= 1;                      // Schaft
    }));

    /// <summary>
    /// Eckwinkel fuer das Papier und die beiden Felder darin. 0 = links oben,
    /// dann im Uhrzeigersinn. Gezeichnet wird einmal links oben, die drei
    /// anderen sind gespiegelt - so sitzt jeder Pixel garantiert symmetrisch.
    /// </summary>
    public Sprite Bracket(int corner)
    {
        brackets = brackets ?? new Sprite[4];
        corner = Mathf.Clamp(corner, 0, 3);
        if (brackets[corner] != null) return brackets[corner];

        const int n = 5;
        brackets[corner] = Build("SkinBracket" + corner, n, n, (x, y) =>
        {
            // Spiegeln, bis die Ecke links oben liegt. y zaehlt von unten, links
            // oben ist also x klein und y gross.
            int px = (corner == 1 || corner == 2) ? n - 1 - x : x;
            int py = (corner == 2 || corner == 3) ? n - 1 - y : y;

            bool arm = (py == n - 1 && px <= 2) || (px == 0 && py >= n - 3);
            return arm;
        });

        return brackets[corner];
    }

    /// <summary>
    /// Die Spitze am Banner ueber dem Baum. Wird in der Hoehe des Banners
    /// gebaut, damit sie fugenlos anschliesst - <paramref name="outline"/>
    /// liefert statt der Flaeche nur ihren Rand.
    /// </summary>
    public Sprite Tip(int width, int height, bool right, bool outline)
    {
        string key = "tip" + width + "x" + height + (right ? "R" : "L") + (outline ? "O" : "F");
        if (odds.TryGetValue(key, out Sprite s) && s != null) return s;

        width  = Mathf.Max(2, width);
        height = Mathf.Max(3, height);

        float cy = (height - 1) * 0.5f;

        // Bei x=0 volle Hoehe, zur Spitze hin laeuft sie auf einen Pixel zu.
        System.Func<int, int, bool> mask = (x, y) =>
        {
            int fromBody = right ? x : width - 1 - x;
            float allowed = (cy + 0.5f) * (1f - fromBody / (float)width);
            return Mathf.Abs(y - cy) <= allowed;
        };

        s = Build("SkinTip" + key, width, height,
                  outline ? EdgeOf(mask, width, height) : mask);

        odds[key] = s;
        return s;
    }

    /// <summary>
    /// Papierkorn: einzelne dunklere Punkte, gleichmaessig gestreut. Liegt als
    /// eine Textur in der Groesse der Flaeche vor statt gekachelt - so
    /// wiederholt sich kein Muster, und ein paar Kilobyte kosten nichts.
    /// </summary>
    public Sprite Grain(int width, int height, int seed)
    {
        string key = "grain" + width + "x" + height + "#" + seed;
        if (odds.TryGetValue(key, out Sprite s) && s != null) return s;

        width  = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        var rng = new System.Random(seed);
        var dots = new bool[width, height];

        int count = Mathf.RoundToInt(width * height * 0.014f);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(width);
            int y = rng.Next(height);
            dots[x, y] = true;

            // Jeder sechste Punkt bekommt einen Nachbarn - das gibt der Flaeche
            // die kurzen Fasern, die handgeschoepftes Papier hat.
            if (rng.Next(6) != 0) continue;
            int nx = Mathf.Clamp(x + 1, 0, width - 1);
            dots[nx, y] = true;
        }

        s = Build("SkinGrain", width, height, (x, y) => dots[x, y]);
        odds[key] = s;
        return s;
    }

    // ==================================================================
    //  Licht - die beiden einzigen weichen Flaechen im Fenster
    // ==================================================================

    /// <summary>
    /// Warmes Licht hinter dem Papier. Bewusst weich gefiltert: ein harter
    /// Verlauf wuerde in Ringen treppen, und Licht ist das eine, was auch in
    /// Pixelart weich sein darf.
    /// </summary>
    public Sprite Glow => glow != null ? glow : (glow = BuildSmooth("SkinGlow", 64, 64, (u, v) =>
    {
        float dx = u * 2f - 1f, dy = v * 2f - 1f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
    }));

    /// <summary>Dunklere Ecken - schiebt den Blick auf die Mitte.</summary>
    public Sprite Vignette => vignette != null ? vignette : (vignette = BuildSmooth("SkinVignette", 64, 36, (u, v) =>
    {
        float dx = (u * 2f - 1f) * 0.92f, dy = v * 2f - 1f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.3f, d));
    }));

    // ==================================================================
    //  Texturen bauen
    // ==================================================================

    /// <summary>Macht aus einer Maske ihre Aussenkante.</summary>
    static System.Func<int, int, bool> EdgeOf(System.Func<int, int, bool> mask, int w, int h)
    {
        return (x, y) =>
        {
            if (!mask(x, y)) return false;
            return x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                   !mask(x - 1, y) || !mask(x + 1, y) ||
                   !mask(x, y - 1) || !mask(x, y + 1);
        };
    }

    Sprite Build(string name, int w, int h, System.Func<int, int, bool> solid, Vector4 border = default)
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

        return Finish(tex, name, w, h, border);
    }

    /// <summary>
    /// Wie Build, aber mit Deckkraft je Pixel und weichem Filter - fuer die
    /// beiden Lichtflaechen. u/v laufen von 0 bis 1.
    /// </summary>
    Sprite BuildSmooth(string name, int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = name,
        };

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
            }
        }

        return Finish(tex, name, w, h, default);
    }

    Sprite Finish(Texture2D tex, string name, int w, int h, Vector4 border)
    {
        tex.Apply();

        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f),
                                 UnitsPerPixel, 0, SpriteMeshType.FullRect, border);
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
        edges.Clear();
        capsTop.Clear();
        capsBot.Clear();
        odds.Clear();

        sparkle = spark = stud = gem = arrowLeft = glow = vignette = null;
        brackets = null;
    }
}
