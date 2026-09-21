using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Die roten Warnflaechen der Boss-Attacken: wo es gleich weh tut und wann.
///
/// Gelesen werden sie immer gleich, egal welche Attacke gerade laeuft:
///   - der blasse Umriss steht still und zeigt den BEREICH
///   - die kraeftige Fuellung waechst und zeigt die ZEIT
///   - ist die Fuellung voll, schlaegt es ein
/// Wer das einmal verstanden hat, versteht jede neue Attacke ohne Erklaerung.
/// Deshalb sollte eine neue Attacke hier eine Form dazubekommen, statt sich
/// eine eigene Sprache auszudenken.
///
/// Alles entsteht zur Laufzeit: Textur, Sprite, Material, GameObject. Es gibt
/// bewusst kein Prefab dafuer - eine Attacke soll sich in einer Zeile Code
/// aendern lassen, ohne dass parallel ein Prefab nachgezogen werden muss, und
/// ohne dass ein Prefab-Neubau das Aussehen wieder mitnimmt.
/// </summary>
public static class BossTelegraph
{
    // ------------------------------------------------------------- Aussehen

    /// <summary>Blass: der Bereich, der gleich getroffen wird.</summary>
    private static readonly Color AreaColor = new Color(1f, 0.16f, 0.10f, 0.26f);

    /// <summary>Kraeftig: die ablaufende Zeit.</summary>
    private static readonly Color FillColor = new Color(1f, 0.32f, 0.12f, 0.45f);

    /// <summary>Der harte Rand einer Zone - ohne ihn zerlaeuft sie im Boden.</summary>
    private static readonly Color EdgeColor = new Color(1f, 0.22f, 0.12f, 0.85f);

    /// <summary>Der Blitz im Moment des Einschlags.</summary>
    internal static readonly Color ImpactColor = new Color(1f, 0.93f, 0.82f, 0.90f);

    // Unter die Gegner (der Boss liegt auf 1), aber ueber den Boden. Dieselbe
    // Ecke, in der auch ZoneOutside schon liegt.
    private const string Layer = "Objects";
    private const int AreaOrder = -3;
    private const int FillOrder = -2;

    private const int TextureSize = 128;

    private static Sprite quadSprite;
    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Material sharedMaterial;

    // ------------------------------------------------------------- Bauteile

    /// <summary>
    /// Eine Bahn von <paramref name="from"/> nach <paramref name="to"/>: der
    /// Weg, den der Boss gleich durchsprintet. Die Fuellung waechst der Laenge
    /// nach, laeuft also auf den Spieler zu - das liest sich als "von da
    /// kommt es gleich".
    /// </summary>
    public static BossTelegraphMarker Path(Vector2 from, Vector2 to, float width)
    {
        GameObject root = NewRoot("BossTelegraph_Bahn");
        SpriteRenderer area = NewRenderer(root.transform, Quad, AreaOrder, AreaColor);
        SpriteRenderer fill = NewRenderer(root.transform, Quad, FillOrder, FillColor);

        BossTelegraphMarker marker = root.AddComponent<BossTelegraphMarker>();
        marker.Bind(area, fill, AreaColor, FillColor);
        marker.Aim(from, to, width);
        return marker;
    }

    /// <summary>
    /// Ein Kreis, in dem gleich etwas einschlaegt. Der Ring bleibt stehen und
    /// zeigt den Radius, die Scheibe waechst von innen nach aussen.
    /// </summary>
    public static BossTelegraphMarker Zone(Vector2 center, float radius)
    {
        GameObject root = NewRoot("BossTelegraph_Zone");
        SpriteRenderer area = NewRenderer(root.transform, Ring, AreaOrder, EdgeColor);
        SpriteRenderer fill = NewRenderer(root.transform, Disc, FillOrder, FillColor);

        BossTelegraphMarker marker = root.AddComponent<BossTelegraphMarker>();
        marker.Bind(area, fill, EdgeColor, FillColor);
        marker.Spread(center, radius);
        return marker;
    }

    // ---------------------------------------------------------------- Aufbau

    private static GameObject NewRoot(string name)
    {
        var root = new GameObject(name);

        // Ohne das landet die Markierung in der aktiven Szene - waehrend eines
        // Laufs ist das der Hub, und sie ueberlebt das Entladen der Karte.
        // Dieselbe Falle wie bei den Gegnern, siehe RunScene.
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(root, run);
        }

        return root;
    }

    private static SpriteRenderer NewRenderer(Transform parent, Sprite sprite, int order, Color color)
    {
        var child = new GameObject(order == AreaOrder ? "Bereich" : "Fuellung");
        child.transform.SetParent(parent, false);

        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = SharedMaterial;
        renderer.sortingLayerName = Layer;
        renderer.sortingOrder = order;
        renderer.color = color;
        return renderer;
    }

    /// <summary>
    /// Unbeleuchtet - das Projekt rendert ueber den 2D-Renderer der URP, und
    /// ein beleuchtetes Sprite waere ohne 2D-Licht in der Naehe einfach
    /// schwarz.
    /// </summary>
    private static Material SharedMaterial
    {
        get
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return sharedMaterial;
        }
    }

    // --------------------------------------------------------------- Sprites

    /// <summary>
    /// Ein Rechteck mit Pivot links-mitte: bei localScale (Laenge, Breite)
    /// reicht es vom Pivot nach rechts. Damit ist "Bahn zeichnen" nur noch
    /// Position + Drehung + Skalierung.
    /// </summary>
    private static Sprite Quad
    {
        get
        {
            if (quadSprite == null)
            {
                Texture2D texture = NewTexture(4);
                var pixels = new Color[4 * 4];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply(false, false);

                quadSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0f, 0.5f), 4f);
                quadSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            return quadSprite;
        }
    }

    private static Sprite Disc
    {
        get
        {
            if (discSprite == null) discSprite = BuildRadial(false);
            return discSprite;
        }
    }

    private static Sprite Ring
    {
        get
        {
            if (ringSprite == null) ringSprite = BuildRadial(true);
            return ringSprite;
        }
    }

    /// <summary>
    /// Scheibe oder Ring. Die Pixel pro Einheit sind halb so gross wie die
    /// Textur - dadurch hat der Sprite bei Scale 1 genau Radius 1, und ein
    /// Aufrufer kann localScale direkt als Weltradius setzen.
    /// </summary>
    private static Sprite BuildRadial(bool ring)
    {
        const int size = TextureSize;
        const float mid = size * 0.5f;
        const float outer = mid - 1f;
        const float inner = outer * 0.84f;

        Texture2D texture = NewTexture(size);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - mid;
                float dy = y + 0.5f - mid;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Clamp01 auf die Pixel-Differenz ergibt eine ein Pixel breite
                // weiche Kante - reicht voellig und kostet kein Blur.
                float alpha = Mathf.Clamp01(outer - distance);
                if (ring) alpha *= Mathf.Clamp01(distance - inner);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
