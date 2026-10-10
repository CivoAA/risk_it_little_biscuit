using UnityEngine;

/// <summary>
/// Leiser Schneefall ueber der Eiswelt (Map_World5). Haengt am Map-Objekt
/// (setzt der EisBuilder). Ein fester Satz Flocken liegt in der Welt, sinkt,
/// schaukelt und springt auf die andere Seite des Bildes, sobald er aus dem
/// Kamerabild faellt - so schneit es ueberall, ohne je etwas zu erzeugen.
///
/// Laeuft mit Time.deltaTime, steht also bei Level-Up und Pause still.
/// Positionen rasten auf ganze Pixel (32 px je Einheit), wie die Pixel-Perfect-Kamera.
/// </summary>
public class Schneefall : MonoBehaviour
{
    [Tooltip("Anzahl Flocken im Bild.")]
    [SerializeField] private int count = 70;

    [Tooltip("Fallgeschwindigkeit in Einheiten pro Sekunde (min, max).")]
    [SerializeField] private Vector2 fallSpeed = new Vector2(0.5f, 1.3f);

    [Tooltip("Seitlicher Wind in Einheiten pro Sekunde.")]
    [SerializeField] private float wind = -0.25f;

    [Tooltip("Deckkraft der Flocken (min, max).")]
    [SerializeField] private Vector2 alpha = new Vector2(0.45f, 0.9f);

    private const float Ppu = 32f;
    private const float Margin = 1f;

    private Transform[] flakes;
    private SpriteRenderer[] renderers;
    private Vector2[] pos;
    private float[] speed;
    private float[] phase;
    private Camera cam;

    private void Start()
    {
        Sprite small = MakeSprite(false);
        Sprite big = MakeSprite(true);

        flakes = new Transform[count];
        renderers = new SpriteRenderer[count];
        pos = new Vector2[count];
        speed = new float[count];
        phase = new float[count];

        cam = Camera.main;
        Rect view = View();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Flocke");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            bool isBig = i % 5 == 0;
            sr.sprite = isBig ? big : small;
            sr.sortingLayerName = "Foreground";
            sr.sortingOrder = 40;
            float a = Random.Range(alpha.x, alpha.y) * (isBig ? 1f : 0.85f);
            sr.color = new Color(1f, 1f, 1f, a);

            flakes[i] = go.transform;
            renderers[i] = sr;
            pos[i] = new Vector2(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax));
            // grosse Flocken sind "naeher" und fallen schneller
            speed[i] = Random.Range(fallSpeed.x, fallSpeed.y) * (isBig ? 1.35f : 1f);
            phase[i] = Random.value * Mathf.PI * 2f;
        }
    }

    private Rect View()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return new Rect(-9f, -6f, 18f, 12f);
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w / 2f - Margin, c.y - h / 2f - Margin, w + Margin * 2f, h + Margin * 2f);
    }

    private void LateUpdate()
    {
        if (flakes == null) return;
        Rect view = View();
        float dt = Time.deltaTime;
        float t = Time.time;

        for (int i = 0; i < flakes.Length; i++)
        {
            Vector2 p = pos[i];
            p.y -= speed[i] * dt;
            p.x += (wind + Mathf.Sin(t * 1.3f + phase[i]) * 0.35f) * dt;

            // aus dem Bild gefallen oder zurueckgelassen: auf die Gegenseite
            if (p.y < view.yMin) { p.y += view.height; p.x = Random.Range(view.xMin, view.xMax); }
            else if (p.y > view.yMax) p.y -= view.height;
            if (p.x < view.xMin) p.x += view.width;
            else if (p.x > view.xMax) p.x -= view.width;

            pos[i] = p;
            flakes[i].position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
        }
    }

    private static Sprite MakeSprite(bool big)
    {
        int n = big ? 3 : 1;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        if (big)
        {
            Color c = Color.clear, w = Color.white, s = new Color(0.86f, 0.88f, 1f, 1f);
            tex.SetPixels(new[] { c, s, c, s, w, s, c, s, c });
        }
        else
        {
            tex.SetPixel(0, 0, Color.white);
        }
        tex.Apply();
        // Pivot unten links: so liegt die Flocke nach dem Einrasten genau auf ganzen Pixeln.
        return Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.zero, Ppu);
    }
}
