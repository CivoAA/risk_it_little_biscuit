using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zwiebelschalen der Schalenklinge (<see cref="ShellBlade"/>): alle
/// <see cref="RechargeTime"/> Sekunden waechst dem Ritter eine Schale, die
/// den naechsten Treffer komplett abfaengt. Normal hoechstens eine, mit dem
/// Skilltree-Knoten "Zwiebelhaut" (<see cref="SkillGrants.Zwiebelhaut"/>) drei.
///
/// Haengt am Spieler, damit <see cref="PlayerController.TakeDamage"/> sie
/// findet; angelegt wird sie von der Evo, sobald die aktiv ist. Laedt nur,
/// solange die Evo <see cref="Charge"/> ruft - vorhandene Schalen bleiben.
///
/// Aussehen: jede Schale ein duenner Ring aus Zwiebelhaut um den Ritter
/// (Pixel aus dem Code, 32 PPU), die aeusseren etwas groesser. Faengt eine
/// einen Treffer ab, platzt sie in ein paar Hautfetzen.
/// </summary>
public class OnionShell : MonoBehaviour
{
    /// <summary>So lange braucht eine neue Schale.</summary>
    public const float RechargeTime = 2f;

    /// <summary>Hoechstzahl ohne / mit Zwiebelhaut.</summary>
    public const int BaseShells = 1;
    public const int SkinShells = 3;

    /// <summary>Mitte der Ringe relativ zum Spieler-Pivot (der liegt unter den Fuessen).</summary>
    private static readonly Vector2 Center = new Vector2(0f, 0.45f);

    /// <summary>Radius der innersten Schale in Pixeln; jede weitere liegt <see cref="RingStep"/> weiter aussen.</summary>
    private const int InnerRadius = 17;
    private const int RingStep = 3;

    private static readonly Color32 Skin = new Color32(238, 226, 244, 255);
    private static readonly Color32 SkinDark = new Color32(178, 136, 200, 255);
    private static readonly Color32 Shine = new Color32(255, 255, 255, 255);

    public int Count { get; private set; }

    private float timer;
    private int chargedFrame = -1;
    private SpriteRenderer playerRenderer;
    private readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();

    public static OnionShell Ensure(PlayerController player)
    {
        if (player == null) return null;
        OnionShell shell = player.GetComponent<OnionShell>();
        return shell != null ? shell : player.gameObject.AddComponent<OnionShell>();
    }

    private void Awake()
    {
        playerRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>Von der Evo jeden Frame gerufen, solange sie angreift.</summary>
    public void Charge(int maxShells)
    {
        // Laeuft die Evo doppelt (Tofu-Kopie), zaehlt der Frame trotzdem nur einmal.
        if (chargedFrame == Time.frameCount) return;
        chargedFrame = Time.frameCount;

        if (Count >= maxShells)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < RechargeTime) return;

        timer = 0f;
        Count++;
        StartCoroutine(Grow(RingFor(Count - 1)));
    }

    /// <summary>Faengt einen Treffer ab, wenn eine Schale da ist - die aeusserste platzt.</summary>
    public bool TryBlock()
    {
        if (Count <= 0) return false;

        Count--;
        SpriteRenderer ring = rings[Count];
        ring.enabled = false;
        Burst(ring.transform.position, InnerRadius + RingStep * Count);
        return true;
    }

    private void LateUpdate()
    {
        // Leichtes Schimmern, die Schalen atmen versetzt.
        for (int i = 0; i < rings.Count; i++)
        {
            SpriteRenderer sr = rings[i];
            if (sr == null || !sr.enabled) continue;

            Color c = sr.color;
            c.a = 0.7f + 0.2f * Mathf.Sin(Time.time * 4f + i * 1.3f);
            sr.color = c;
        }
    }

    // ------------------------------------------------------------------
    //  Anzeige
    // ------------------------------------------------------------------

    private SpriteRenderer RingFor(int index)
    {
        while (rings.Count <= index)
        {
            int n = rings.Count;
            GameObject go = new GameObject("OnionShell_" + n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Center;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite(InnerRadius + RingStep * n);
            if (playerRenderer != null)
            {
                sr.sortingLayerID = playerRenderer.sortingLayerID;
                sr.sortingOrder = playerRenderer.sortingOrder + 2;
            }
            sr.enabled = false;
            rings.Add(sr);
        }
        return rings[index];
    }

    /// <summary>Neue Schale: kommt etwas zu gross und setzt sich dann.</summary>
    private IEnumerator Grow(SpriteRenderer ring)
    {
        ring.enabled = true;
        const float time = 0.15f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (ring == null || !ring.enabled) yield break;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, t / time);
            yield return null;
        }
        if (ring != null) ring.transform.localScale = Vector3.one;
    }

    /// <summary>Hautfetzen fliegen vom Ring weg und verblassen.</summary>
    private void Burst(Vector3 center, int radiusPx)
    {
        const int pieces = 10;
        float radius = radiusPx / 32f;
        for (int i = 0; i < pieces; i++)
        {
            float a = (i + Random.Range(-0.3f, 0.3f)) / pieces * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            StartCoroutine(Flake(center + (Vector3)(dir * radius), dir, i % 3 == 0));
        }
    }

    private IEnumerator Flake(Vector3 start, Vector2 dir, bool dark)
    {
        GameObject go = new GameObject("OnionShellFlake");
        go.transform.position = start;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dark ? FlakeDark : FlakeLight;
        if (playerRenderer != null)
        {
            sr.sortingLayerID = playerRenderer.sortingLayerID;
            sr.sortingOrder = playerRenderer.sortingOrder + 2;
        }

        const float time = 0.35f;
        float speed = Random.Range(2.5f, 4f);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            go.transform.position = start + (Vector3)(dir * speed * t * (1f - k * 0.5f));
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }

        Destroy(go);
    }

    // ------------------------------------------------------------------
    //  Pixel aus dem Code
    // ------------------------------------------------------------------

    private static readonly Dictionary<int, Sprite> ringSprites = new Dictionary<int, Sprite>();
    private static Sprite flakeLight, flakeDark;

    private static Sprite FlakeLight => flakeLight ??= Flake(Skin);
    private static Sprite FlakeDark => flakeDark ??= Flake(SkinDark);

    private static Sprite Flake(Color32 color)
    {
        // Kleiner Haken aus drei Pixeln.
        Texture2D tex = NewTexture(2, 2);
        Color32 clear = new Color32(0, 0, 0, 0);
        tex.SetPixels32(new[] { color, color, color, clear });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>
    /// Ring aus Zwiebelhaut: aussen ein dunkler, innen ein heller Pixel, oben
    /// eine kleine Zwiebelspitze, links oben ein Glanzlicht.
    /// </summary>
    private static Sprite RingSprite(int radius)
    {
        if (ringSprites.TryGetValue(radius, out Sprite cached)) return cached;

        const int tip = 3;
        int size = radius * 2 + 3;
        int h = size + tip;
        Texture2D tex = NewTexture(size, h);
        Color32[] px = new Color32[size * h];
        float c = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > radius + 0.5f || d < radius - 1.5f) continue;

                bool outer = d >= radius - 0.5f;
                Color32 col = outer ? SkinDark : Skin;

                // Glanz links oben auf dem inneren Pixel.
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (!outer && ang > 115f && ang < 150f) col = Shine;

                px[y * size + x] = col;
            }
        }

        // Zwiebelspitze oben in der Mitte.
        int mid = Mathf.RoundToInt(c);
        int top = Mathf.RoundToInt(c + radius);
        for (int i = 1; i <= tip; i++)
        {
            int y = top + i;
            if (y >= h) break;
            px[y * size + mid] = i == tip ? SkinDark : Skin;
            if (i == 1)
            {
                px[y * size + mid - 1] = SkinDark;
                px[y * size + mid + 1] = SkinDark;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        // Pivot = Ringmitte, nicht Bildmitte (die Spitze steht oben ueber).
        Vector2 pivot = new Vector2((c + 0.5f) / size, (c + 0.5f) / h);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, h), pivot, 32f);
        ringSprites[radius] = sprite;
        return sprite;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }
}
