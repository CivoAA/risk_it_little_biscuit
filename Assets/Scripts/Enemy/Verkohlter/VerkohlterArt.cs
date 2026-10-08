using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bilder und Masse fuer Phase 3 des Verkohlten (Einsaugen + Herzkammer).
/// Die Bilder zeichnet Tools/verkohlter_herz.py nach Resources/Verkohlter -
/// deshalb laedt der Code sie selbst und es muss kein Prefab neu gebaut werden.
///
/// Die Masse unten muessen zum Skript passen (dort stehen sie in Pixeln, hier
/// durch 32 geteilt). Bezugspunkt der Kammer ist die Bildmitte von
/// herzkammer_hinten = dort, wo die Kamera hinschaut.
/// </summary>
public static class VerkohlterArt
{
    public const float PixelsPerUnit = 32f;

    /// <summary>Herz und Kammer laufen auf derselben Uhr: 16 Bilder, 12 fps.</summary>
    public const float Fps = 12f;
    public const int BeatFrames = 16;

    /// <summary>Bild 2 = BUM, Bild 5 = bumm (Tools/verkohlter_herz.py, beat_curve).</summary>
    public const int BeatLub = 2;
    public const int BeatDub = 5;

    // --- Einsaugen (verkohlter_einsaugen: 0-7 Luft holen, 8-15 Sog, 16-23 Schlucken)
    public const int InhaleFrames = 8;
    public const int SuckFirst = 8, SuckFrames = 8;
    public const int GulpFirst = 16, GulpFrames = 8;

    /// <summary>Wo der Schlund sitzt, vom Pivot (Fuesse) aus. Skript: "Schlund relativ zum Pivot".</summary>
    public static readonly Vector2 MawOffset = new Vector2(0.5f, 26f) / PixelsPerUnit;

    // --- Kammer (vom Bezugspunkt = Herzmitte aus)
    public static readonly Vector2 HeartOffset = Vector2.zero;
    public static readonly Vector2 FloorCenter = new Vector2(0f, -16f) / PixelsPerUnit;    // FCY 384
    public static readonly Vector2 FloorRadius = new Vector2(472f, 246f) / PixelsPerUnit;  // FRX, FRY
    public static readonly Vector2 PoolCenter = new Vector2(0f, -56f) / PixelsPerUnit;     // PCY 424
    public static readonly Vector2 PoolRadius = new Vector2(64f, 28f) / PixelsPerUnit;     // POOL_RX, POOL_RY

    /// <summary>
    /// So weit darf sich die Kamera vom Herzen entfernen (sie folgt dem
    /// Spieler). Bei 480x270 bleibt die Herzmitte damit immer im Bild - in den
    /// aeussersten Ecken ist das Herz am Rand angeschnitten (Nicks Wahl: lieber
    /// mehr Platz als weiter herauszoomen).
    /// </summary>
    public static readonly Vector2 CameraSlack = new Vector2(230f, 125f) / PixelsPerUnit;

    /// <summary>Zoom in der Kammer: so viele Bildpixel zeigt die Pixel-Perfect-Kamera.</summary>
    public const int ChamberRefWidth = 480, ChamberRefHeight = 270;

    public static readonly Color Void = new Color32(10, 6, 8, 255);

    /// <summary>Glut-Palette (wie FIRE in Tools/verkohlter.py), 0 = erkaltet, 6 = weiss.</summary>
    public static readonly Color32[] Fire =
    {
        new Color32(0x3d, 0x09, 0x07, 255), new Color32(0x7e, 0x16, 0x08, 255),
        new Color32(0xc8, 0x35, 0x0c, 255), new Color32(0xf5, 0x6d, 0x17, 255),
        new Color32(0xff, 0xb4, 0x3a, 255), new Color32(0xff, 0xe6, 0x8a, 255),
        new Color32(0xff, 0xfb, 0xea, 255),
    };

    public static readonly Color32 Ash = new Color32(0x6a, 0x5e, 0x63, 255);

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    private static Sprite pixel;

    /// <summary>Bildstreifen aus Resources/Verkohlter, nach Bildnummer sortiert. Leer, wenn er fehlt.</summary>
    public static Sprite[] Strip(string name)
    {
        if (cache.TryGetValue(name, out Sprite[] cached) && cached != null) return cached;

        Sprite[] frames = Resources.LoadAll<Sprite>("Verkohlter/" + name);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[Verkohlter] Bilder fehlen: Resources/Verkohlter/" + name +
                             " - Tools/verkohlter_herz.py laufen lassen.");
            frames = new Sprite[0];
        }
        else
        {
            System.Array.Sort(frames, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
        }
        cache[name] = frames;
        return frames;
    }

    public static Sprite Frame(Sprite[] strip, int i)
    {
        if (strip == null || strip.Length == 0) return null;
        return strip[Mathf.Clamp(i, 0, strip.Length - 1)];
    }

    private static int FrameIndex(Sprite s)
    {
        int i = s.name.LastIndexOf('_');
        return i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n) ? n : 0;
    }

    /// <summary>Ein weisser Pixel (1/32 Einheit), Pivot unten links - fuer Funken und Flaechen.</summary>
    public static Sprite Pixel
    {
        get
        {
            if (pixel != null) return pixel;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "VerkohlterPixel",
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero, PixelsPerUnit);
            pixel.name = "VerkohlterPixel";
            return pixel;
        }
    }

    /// <summary>Auf ganze Bildpixel runden.</summary>
    public static Vector2 Snap(Vector2 p)
    {
        return new Vector2(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit);
    }
}
