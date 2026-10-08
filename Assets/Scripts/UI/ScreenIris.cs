using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kreisblende ueber dem ganzen Bild (wie am Ende alter Cartoons): ein
/// schwarzer Schirm mit rundem Loch, das sich um einen Punkt zuzieht oder
/// oeffnet. Am Lochrand glimmt ein Glutring. Dazu ein Vollbild-Blitz.
///
/// Gezeichnet wird in eine kleine Textur (480 Pixel breit, Point-Filter), die
/// auf den ganzen Schirm gezogen wird - so hat die Blende dieselben groben
/// Pixel wie das Spiel. Liegt auf einem eigenen Overlay-Canvas ueber dem HUD.
///
/// <code>
/// ScreenIris iris = ScreenIris.Create();
/// iris.Set(viewportPoint, 0.3f);   // Loch mit 30 % der Bildhoehe als Radius
/// iris.Set(viewportPoint, 0f);     // ganz zu
/// iris.Open();                     // weg
/// </code>
/// </summary>
public class ScreenIris : MonoBehaviour
{
    private const int TexWidth = 480;
    private const int SortOrder = 500;

    private RawImage image;
    private Image flash;
    private Texture2D tex;
    private Color32[] pixels;
    private int w, h;

    public static ScreenIris Create()
    {
        var go = new GameObject("ScreenIris");
        RunScene.Place(go, "Effekte");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortOrder;
        return go.AddComponent<ScreenIris>();
    }

    private void Awake()
    {
        w = TexWidth;
        h = Mathf.Max(1, Mathf.RoundToInt(TexWidth * (float)Screen.height / Mathf.Max(1, Screen.width)));
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ScreenIris",
        };
        pixels = new Color32[w * h];

        image = NewChild<RawImage>("Blende");
        image.texture = tex;
        image.raycastTarget = false;
        image.enabled = false;

        flash = NewChild<Image>("Blitz");
        flash.raycastTarget = false;
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.enabled = false;
    }

    private T NewChild<T>(string name) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go.AddComponent<T>();
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    /// <summary>
    /// Blende auf einen Punkt (Viewport 0..1) mit Lochradius als Anteil der
    /// Bildhoehe. Radius 0 = ganz schwarz, gross genug = Blende aus.
    /// </summary>
    public void Set(Vector2 viewport, float radius)
    {
        float r = radius * h;
        float cx = viewport.x * w;
        float cy = viewport.y * h;

        // Deckt das Loch das ganze Bild ab, braucht es keine Blende.
        float far = Mathf.Max(Vector2.Distance(new Vector2(cx, cy), Vector2.zero),
                              Vector2.Distance(new Vector2(cx, cy), new Vector2(w, 0)),
                              Vector2.Distance(new Vector2(cx, cy), new Vector2(0, h)),
                              Vector2.Distance(new Vector2(cx, cy), new Vector2(w, h)));
        if (r > far + 4f)
        {
            image.enabled = false;
            return;
        }

        Color32 black = VerkohlterArt.Void;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ring1 = VerkohlterArt.Fire[3];
        Color32 ring2 = VerkohlterArt.Fire[1];
        float r2 = r * r;
        float ringA = (r + 1.2f) * (r + 1.2f);
        float ringB = (r + 2.6f) * (r + 2.6f);
        bool ring = r > 2f;

        for (int y = 0; y < h; y++)
        {
            float dy = y + 0.5f - cy;
            float dy2 = dy * dy;
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx;
                float d2 = dx * dx + dy2;
                Color32 c;
                if (d2 < r2) c = clear;
                else if (ring && d2 < ringA) c = ring1;
                else if (ring && d2 < ringB) c = ring2;
                else c = black;
                pixels[row + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
        image.enabled = true;
    }

    /// <summary>Ganz schwarz.</summary>
    public void Close()
    {
        Set(new Vector2(0.5f, 0.5f), 0f);
    }

    /// <summary>Blende weg.</summary>
    public void Open()
    {
        image.enabled = false;
    }

    /// <summary>Vollbild-Blitz: Farbe mit Deckkraft, 0 = aus.</summary>
    public void Flash(Color color, float alpha)
    {
        flash.enabled = alpha > 0.001f;
        color.a = Mathf.Clamp01(alpha);
        flash.color = color;
    }
}
