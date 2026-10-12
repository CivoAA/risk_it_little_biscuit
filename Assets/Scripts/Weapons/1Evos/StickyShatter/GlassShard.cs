using UnityEngine;

/// <summary>
/// Ein Glassplitter der <see cref="StickyShatterEvo"/>. Fliegt geradeaus,
/// trifft den ersten Gegner und ist dann weg. Wird komplett im Code gebaut -
/// kein Prefab, das Bild ist ein paar Pixel aus <see cref="Look"/>.
/// </summary>
public class GlassShard : MonoBehaviour
{
    private const float HitRadius = 0.15f;

    private static Sprite look;

    private Vector2 velocity;
    private float lifeLeft;
    private float damage;
    private bool spent;

    /// <summary>
    /// Schiesst einen Splitter von <paramref name="position"/> in
    /// <paramref name="direction"/>. Sortierung und Material kommen von
    /// <paramref name="sortLike"/> (das Glas), damit er ueber den Lachen liegt.
    /// </summary>
    public static GameObject Fire(Vector2 position, Vector2 direction, float speed, float range,
                                  float damage, SpriteRenderer sortLike)
    {
        var go = new GameObject("Glass Shard");
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Look;
        sr.flipX = direction.x < 0f;
        if (sortLike != null)
        {
            sr.sharedMaterial = sortLike.sharedMaterial;
            sr.sortingLayerID = sortLike.sortingLayerID;
            sr.sortingOrder = sortLike.sortingOrder + 1;
        }

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = HitRadius;

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;

        var shard = go.AddComponent<GlassShard>();
        speed = Mathf.Max(0.01f, speed);
        shard.velocity = direction.normalized * speed;
        shard.lifeLeft = range / speed;
        shard.damage = damage;
        return go;
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);

        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (spent || !collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy == null) return;

        spent = true;
        enemy.TakeDamage(damage);
        Destroy(gameObject);
    }

    /// <summary>Kleine helle Scherbe, 4x4 Pixel.</summary>
    private static Sprite Look
    {
        get
        {
            if (look != null) return look;

            // y zaehlt von unten. . = leer, H = Glanz, G = Glas, D = Kante
            string[] rows =
            {
                ".H..",
                "HGG.",
                ".GGD",
                "..D.",
            };

            Color glanz = new Color(1f, 1f, 1f, 1f);
            Color glas = new Color(0.72f, 0.88f, 0.95f, 0.9f);
            Color kante = new Color(0.42f, 0.62f, 0.75f, 1f);

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "GlassShard",
            };

            for (int y = 0; y < 4; y++)
            {
                string row = rows[3 - y];
                for (int x = 0; x < 4; x++)
                {
                    char c = row[x];
                    tex.SetPixel(x, y, c == 'H' ? glanz : c == 'G' ? glas : c == 'D' ? kante : Color.clear);
                }
            }
            tex.Apply();

            look = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), PixelPool.Ppu);
            look.name = "GlassShard";
            return look;
        }
    }
}
