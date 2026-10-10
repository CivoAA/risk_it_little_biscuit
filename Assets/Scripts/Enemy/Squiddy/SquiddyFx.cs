using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Kleine Bausteine fuer die Effekte des Gespensts / Squiddys. Alles entsteht
/// zur Laufzeit (wie <see cref="BossTelegraph"/>) - das Prefab traegt nur die
/// Bildstreifen.
/// </summary>
public static class SquiddyFx
{
    public const float Ppu = 32f;

    private static Material unlit;

    /// <summary>Unbeleuchtet: im dunklen Geisterwald leuchten Geist und Qualle selbst.</summary>
    public static Material Unlit
    {
        get
        {
            if (unlit != null) return unlit;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            unlit = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return unlit;
        }
    }

    /// <summary>Ein Sprite-Objekt in der Laufszene (Container "Effekte").</summary>
    public static SpriteRenderer NewSprite(string name, Vector3 position, int order, string layer = "Objects")
    {
        var go = new GameObject(name);
        RunScene.Place(go, "Effekte");
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = Unlit;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>Spielt einen Streifen einmal ab und loescht sich (oder haelt das letzte Bild).</summary>
    public static SquiddyFlipbook OneShot(string name, Sprite[] frames, Vector3 position, float fps, int order,
                                          Color? tint = null, string layer = "Objects")
    {
        if (frames == null || frames.Length == 0) return null;
        SpriteRenderer sr = NewSprite(name, position, order, layer);
        if (tint.HasValue) sr.color = tint.Value;
        SquiddyFlipbook fb = sr.gameObject.AddComponent<SquiddyFlipbook>();
        fb.Play(frames, fps, 0, frames.Length - 1, false, true);
        return fb;
    }

    /// <summary>Rastet eine Weltposition auf ganze Pixel (wie die Pixel-Perfect-Kamera).</summary>
    public static Vector3 Snap(Vector3 p)
    {
        return new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
    }

    public static Light2D NewLight(Transform parent, Vector3 local, Color color, float intensity, float radius)
    {
        var go = new GameObject("Schein");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = 0f;
        l.falloffIntensity = 0.6f;
        l.shadowsEnabled = false;
        var layers = new int[SortingLayer.layers.Length];
        for (int i = 0; i < layers.Length; i++) layers[i] = SortingLayer.layers[i].id;
        l.targetSortingLayers = layers;
        return l;
    }
}

/// <summary>Spielt Bilder eines Streifens ab (einmal oder als Schleife), mit Time.deltaTime.</summary>
public class SquiddyFlipbook : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] strip;
    private float fps, t;
    private int first, last;
    private bool loop, destroyAtEnd;

    public bool Done { get; private set; }

    public void Play(Sprite[] frames, float framesPerSecond, int from, int to, bool looping, bool destroy)
    {
        sr = GetComponent<SpriteRenderer>();
        strip = frames;
        fps = framesPerSecond;
        first = Mathf.Clamp(from, 0, frames.Length - 1);
        last = Mathf.Clamp(to, first, frames.Length - 1);
        loop = looping;
        destroyAtEnd = destroy;
        t = 0f;
        Done = false;
        if (sr != null) sr.sprite = strip[first];
    }

    private void Update()
    {
        if (strip == null || Done) return;
        t += Time.deltaTime;
        int n = last - first + 1;
        int i = Mathf.FloorToInt(t * fps);
        if (i >= n && !loop)
        {
            Done = true;
            if (destroyAtEnd) Destroy(gameObject);
            return;
        }
        if (sr != null) sr.sprite = strip[first + (loop ? i % n : i)];
    }
}

/// <summary>
/// Ein flackernder Blitz zwischen zwei Punkten, pixelgenau in eine kleine
/// Textur gezeichnet (keine gedrehten Sprites, die Pixel bleiben ganz).
/// Wird alle paar Bilder neu gewuerfelt.
/// </summary>
public class SquiddyBlitz : MonoBehaviour
{
    private static readonly Color32 Core = new Color32(255, 255, 255, 255);
    private static readonly Color32 Glow = new Color32(127, 214, 255, 255);
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    private SpriteRenderer sr;
    private Texture2D tex;
    private Color32[] px;
    private int w, h;
    private Transform from, to;
    private Vector3 fromOffset, toOffset;
    private float next;
    private readonly List<Vector2Int> cells = new List<Vector2Int>();
    private readonly HashSet<int> cellSet = new HashSet<int>();

    public static SquiddyBlitz Between(Transform a, Vector3 aOffset, Transform b, Vector3 bOffset, int order = 40)
    {
        SpriteRenderer sr = SquiddyFx.NewSprite("Kettenblitz", a.position, order);
        var z = sr.gameObject.AddComponent<SquiddyBlitz>();
        z.sr = sr;
        z.from = a;
        z.to = b;
        z.fromOffset = aOffset;
        z.toOffset = bOffset;
        return z;
    }

    private void LateUpdate()
    {
        if (from == null || to == null)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time < next) return;
        next = Time.time + 0.07f;
        Redraw();
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);
    }

    private void Redraw()
    {
        Vector2 a = SquiddyFx.Snap(from.position + fromOffset);
        Vector2 b = SquiddyFx.Snap(to.position + toOffset);
        Vector2 min = Vector2.Min(a, b) - Vector2.one * 0.5f;
        Vector2 max = Vector2.Max(a, b) + Vector2.one * 0.5f;
        int nw = Mathf.Clamp(Mathf.CeilToInt((max.x - min.x) * SquiddyFx.Ppu), 4, 1024);
        int nh = Mathf.Clamp(Mathf.CeilToInt((max.y - min.y) * SquiddyFx.Ppu), 4, 1024);
        if (tex == null || nw != w || nh != h)
        {
            if (tex != null) Destroy(tex);
            if (sr.sprite != null) Destroy(sr.sprite);
            w = nw;
            h = nh;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[w * h];
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, SquiddyFx.Ppu);
        }
        for (int i = 0; i < px.Length; i++) px[i] = Clear;

        Vector2Int pa = new Vector2Int(Mathf.RoundToInt((a.x - min.x) * SquiddyFx.Ppu), Mathf.RoundToInt((a.y - min.y) * SquiddyFx.Ppu));
        Vector2Int pb = new Vector2Int(Mathf.RoundToInt((b.x - min.x) * SquiddyFx.Ppu), Mathf.RoundToInt((b.y - min.y) * SquiddyFx.Ppu));

        // Mittelpunkt-Verschiebung: 3 Stufen Zickzack
        var pts = new List<Vector2> { pa, pb };
        for (int d = 0; d < 4; d++)
        {
            var nxt = new List<Vector2> { pts[0] };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 p0 = pts[i], p1 = pts[i + 1];
                Vector2 dir = p1 - p0;
                float len = dir.magnitude;
                Vector2 n = len > 0.01f ? new Vector2(-dir.y, dir.x) / len : Vector2.zero;
                nxt.Add((p0 + p1) * 0.5f + n * Random.Range(-0.22f, 0.22f) * len);
                nxt.Add(p1);
            }
            pts = nxt;
        }
        cells.Clear();
        cellSet.Clear();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 p0 = pts[i], p1 = pts[i + 1];
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y))) + 1;
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(p0, p1, s / (float)steps);
                int x = Mathf.Clamp(Mathf.RoundToInt(p.x), 1, w - 2), y = Mathf.Clamp(Mathf.RoundToInt(p.y), 1, h - 2);
                if (cellSet.Add(y * w + x)) cells.Add(new Vector2Int(x, y));
            }
        }
        foreach (Vector2Int c in cells)
        {
            Set(c.x + 1, c.y, Glow);
            Set(c.x - 1, c.y, Glow);
            Set(c.x, c.y + 1, Glow);
            Set(c.x, c.y - 1, Glow);
        }
        foreach (Vector2Int c in cells) px[c.y * w + c.x] = Core;
        tex.SetPixels32(px);
        tex.Apply(false, false);
        transform.position = new Vector3(min.x, min.y, 0f);
    }

    private void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h || cellSet.Contains(y * w + x)) return;
        px[y * w + x] = c;
    }
}

/// <summary>Senkrechter Lichtstrahl von oben (Squiddy faehrt auf). Weiche Pixelkanten, unbeleuchtet.</summary>
public class SquiddyLichtstrahl : MonoBehaviour
{
    private SpriteRenderer sr;
    private float t, life;

    public static SquiddyLichtstrahl Spawn(Vector3 groundPos, float seconds)
    {
        const int w = 72, h = 360;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs(x + 0.5f - w / 2f) / (w / 2f);
                float a = u < 0.35f ? 0.55f : u < 0.62f ? 0.32f : u < 0.85f ? 0.14f : 0f;
                float fadeTop = Mathf.Clamp01(y / (float)h * 1.3f);
                bool core = u < 0.18f;
                byte g = core ? (byte)255 : (byte)236;
                px[y * w + x] = new Color32(255, g, (byte)(core ? 230 : 170), (byte)(255 * a * (1f - 0.4f * fadeTop)));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        // hinter dem Boss (dessen SortingGroup liegt auf Objects/1), sonst waescht der Strahl ihn aus
        SpriteRenderer sr = SquiddyFx.NewSprite("Lichtstrahl", groundPos, 0);
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), SquiddyFx.Ppu);
        var beam = sr.gameObject.AddComponent<SquiddyLichtstrahl>();
        beam.sr = sr;
        beam.life = seconds;
        sr.color = new Color(1f, 1f, 1f, 0f);
        return beam;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float a = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((life - t) / 0.8f);
        // sanftes Pulsieren
        a *= 0.85f + 0.15f * Mathf.Sin(t * 7f);
        sr.color = new Color(1f, 1f, 1f, a);
        if (t >= life)
        {
            if (sr.sprite != null)
            {
                Destroy(sr.sprite.texture);
                Destroy(sr.sprite);
            }
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Buehnenlicht fuer die Enthuellung: dunkelt die Welt ab, waehrend der Boss
/// selbst leuchtet. Im Geisterwald ueber <see cref="Geisterwald.BossDim"/>
/// (der setzt das globale Licht jedes Bild selbst), anderswo direkt am
/// globalen Light2D.
/// </summary>
public static class SquiddyBuehne
{
    private static Light2D global;
    private static float baseIntensity = -1f;

    public static void SetDim(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Geisterwald.Active != null)
        {
            Geisterwald.BossDim = amount;
            return;
        }
        if (global == null)
        {
            foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType != Light2D.LightType.Global) continue;
                global = l;
                baseIntensity = l.intensity;
                break;
            }
        }
        if (global != null && baseIntensity >= 0f) global.intensity = baseIntensity * (1f - amount);
    }

    public static void Reset()
    {
        Geisterwald.BossDim = 0f;
        if (global != null && baseIntensity >= 0f) global.intensity = baseIntensity;
        global = null;
        baseIntensity = -1f;
    }
}
