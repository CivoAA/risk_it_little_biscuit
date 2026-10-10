using UnityEngine;

/// <summary>
/// Kleine Eissplitter hinter dem rutschenden Keks. Ein Pool aus winzigen
/// Sprites, die kurz aufblitzen und verblassen.
/// </summary>
public class GlatteisSplitter : MonoBehaviour
{
    private const int PoolSize = 24;
    private const float Interval = 0.05f;
    private const float Life = 0.45f;

    private static GlatteisSplitter instance;
    private static Sprite sprite;
    private static float timer;

    private readonly SpriteRenderer[] flakes = new SpriteRenderer[PoolSize];
    private readonly float[] age = new float[PoolSize];
    private readonly Vector2[] drift = new Vector2[PoolSize];
    private int next;

    public static void Emit(Vector2 feet, Vector2 velocity, float dt)
    {
        if (velocity.sqrMagnitude < 1.5f) return;
        timer -= dt;
        if (timer > 0f) return;
        timer = Interval;

        if (instance == null)
        {
            var go = new GameObject("Glatteis-Splitter");
            instance = go.AddComponent<GlatteisSplitter>();
        }
        instance.Spawn(feet, velocity);
    }

    private static Sprite FlakeSprite()
    {
        if (sprite != null) return sprite;
        // 3x3-Kreuz, Mitte weiss, Arme eisblau
        var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color c = Color.clear, w = Color.white, b = new Color(0.69f, 0.85f, 0.95f, 1f);
        tex.SetPixels(new[] { c, b, c, b, w, b, c, b, c });
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 32f);
        return sprite;
    }

    private void Awake()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Splitter");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FlakeSprite();
            sr.sortingLayerName = "Background";
            sr.sortingOrder = 20;
            sr.enabled = false;
            flakes[i] = sr;
            age[i] = Life;
        }
    }

    private void Spawn(Vector2 feet, Vector2 velocity)
    {
        int i = next;
        next = (next + 1) % PoolSize;
        Vector2 back = -velocity.normalized;
        Vector2 side = new Vector2(-back.y, back.x) * Random.Range(-0.25f, 0.25f);
        flakes[i].transform.position = feet + back * 0.15f + side + Vector2.up * 0.06f;
        drift[i] = back * Random.Range(0.3f, 0.8f) + side;
        age[i] = 0f;
        flakes[i].enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < PoolSize; i++)
        {
            if (age[i] >= Life) continue;
            age[i] += dt;
            float t = age[i] / Life;
            SpriteRenderer sr = flakes[i];
            if (t >= 1f) { sr.enabled = false; continue; }
            sr.transform.position += (Vector3)(drift[i] * dt);
            sr.color = new Color(1f, 1f, 1f, 1f - t * t);
        }
    }
}
