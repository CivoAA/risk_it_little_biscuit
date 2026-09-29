using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Skilltree "Schockwelle" (<see cref="SkillGrants.Schockwelle"/>): wird der
/// Spieler getroffen, stoesst eine Welle alle Gegner in der Naehe weg. Macht
/// keinen Schaden - sie schafft Platz. Bosse und Minibosse bleiben stehen
/// (<see cref="Enemy.ApplyPull"/> ignoriert sie).
///
/// Ausgeloest wird sie von <see cref="PlayerController.TakeDamage"/>, der auch
/// die Abklingzeit haelt. Hier stehen nur die Werte, der Stoss und der Ring.
/// </summary>
public class Shockwave : MonoBehaviour
{
    public const float Cooldown = 10f;   // Sekunden bis zur naechsten Welle
    public const float Radius = 4f;      // Welteinheiten
    public const float PushSpeed = 14f;  // wie schnell die Gegner wegfliegen
    public const float PushTime = 0.25f; // wie lange der Stoss anhaelt

    private const float RingTime = 0.3f;
    private const int PixelsPerUnit = 32; // wie Gegner und Map

    private static Sprite ringSprite;

    private SpriteRenderer sr;
    private float age;

    /// <summary>Stoesst alle Gegner im Radius weg und zeigt den Ring.</summary>
    public static void Fire(Vector3 center)
    {
        var enemies = Enemy.Alive;
        float r2 = Radius * Radius;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null) continue;

            Vector2 away = e.transform.position - center;
            if (away.sqrMagnitude > r2) continue;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;

            e.ApplyPull(away.normalized * PushSpeed, PushTime);
        }

        SpawnRing(center);
    }

    private static void SpawnRing(Vector3 center)
    {
        GameObject go = new GameObject("Shockwave");
        go.transform.position = center;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        go.AddComponent<Shockwave>();
    }

    private void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = RingSprite();
        sr.sortingOrder = 50;
        sr.color = GameHudSkin.IcingLight;
        transform.localScale = Vector3.one * 0.2f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / RingTime);

        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, 1f - (1f - t) * (1f - t));
        Color c = sr.color;
        c.a = 1f - t;
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }

    /// <summary>Ein 2px-Ring, dessen Durchmesser bei Massstab 1 genau 2 x Radius ist.</summary>
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;

        int size = Mathf.RoundToInt(Radius * 2f * PixelsPerUnit);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        float outer = size / 2f;
        float inner = outer - 2f;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - outer, dy = y + 0.5f - outer;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = d <= outer && d >= inner
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        return ringSprite;
    }
}
