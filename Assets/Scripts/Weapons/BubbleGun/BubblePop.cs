using UnityEngine;

/// <summary>Platzen der Blase: ein Ring, der auf den Explosionsradius aufgeht und verblasst.</summary>
public class BubblePop : MonoBehaviour
{
    private const float Lifetime = 0.25f;

    private SpriteRenderer sr;
    private float radius;
    private float age;

    public static void Spawn(Vector2 position, float radius, SpriteRenderer sortingFrom)
    {
        GameObject go = BubbleGun.CreateInRunScene("BubblePop", position);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = RingSprite;
        if (sortingFrom != null)
        {
            renderer.sortingLayerID = sortingFrom.sortingLayerID;
            renderer.sortingOrder = sortingFrom.sortingOrder;
        }

        BubblePop pop = go.AddComponent<BubblePop>();
        pop.sr = renderer;
        pop.radius = radius;
        pop.Apply(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / Lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        float d = 2f * radius * Mathf.Lerp(0.35f, 1f, 1f - (1f - t) * (1f - t));
        transform.localScale = new Vector3(d, d, 1f);
        Color col = sr.color;
        col.a = 1f - t * t;
        sr.color = col;
    }

    private static Sprite ringSprite;

    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null) ringSprite = BuildRing(32);
            return ringSprite;
        }
    }

    private static Sprite BuildRing(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ring = new Color32(200, 238, 255, 230);
        Color32 drop = new Color32(255, 255, 255, 230);

        float c = n * 0.5f;
        float outer = c - 0.5f;
        Color32[] px = new Color32[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f - c;
                float fy = y + 0.5f - c;
                float r = Mathf.Sqrt(fx * fx + fy * fy);

                Color32 col = clear;
                if (r <= outer && outer - r < 1.5f)
                {
                    // Gestrichelt: die Haut reisst in Stuecke.
                    float angle = Mathf.Atan2(fy, fx) * Mathf.Rad2Deg + 180f;
                    if ((int)(angle / 30f) % 2 == 0) col = ring;
                }
                else if (r <= outer * 0.7f && r > outer * 0.55f)
                {
                    float angle = Mathf.Atan2(fy, fx) * Mathf.Rad2Deg + 195f;
                    if ((int)(angle / 15f) % 4 == 0) col = drop;
                }

                px[y * n + x] = col;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
