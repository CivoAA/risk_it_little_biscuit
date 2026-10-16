using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kleine Helfer fuer die Waffen Eiswuerfel-Spender und
/// Pfefferminz-Schild: Bildstreifen aus Resources/Weapons laden (mit
/// Zwischenspeicher), aus mehreren fertigen Groessen die passende waehlen
/// (Pixelart wird nie skaliert) und Objekte in der Lauf-Szene anlegen.
/// </summary>
public static class SweetFx
{
    public const float PixelsPerUnit = 32f;

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    /// <summary>Streifen Resources/Weapons/&lt;name&gt;; fehlt er, ein Klecks in <paramref name="fallback"/>.</summary>
    public static Sprite[] Strip(string name, Color32 fallback, int fallbackSize = 8)
    {
        if (cache.TryGetValue(name, out Sprite[] frames) && frames.Length > 0 && frames[0] != null) return frames;

        frames = SpriteStrip.Load("Weapons/" + name);
        if (frames.Length == 0) frames = new[] { SpriteStrip.Blob(fallbackSize, fallback) };
        cache[name] = frames;
        return frames;
    }

    /// <summary>
    /// Aus &lt;prefix&gt;_&lt;size&gt; den Streifen, dessen Groesse (in Pixeln)
    /// am naechsten an <paramref name="wantedPixels"/> liegt.
    /// </summary>
    public static Sprite[] Sized(string prefix, int[] sizes, float wantedPixels, Color32 fallback)
    {
        int best = sizes[0];
        for (int i = 1; i < sizes.Length; i++)
            if (Mathf.Abs(sizes[i] - wantedPixels) < Mathf.Abs(best - wantedPixels)) best = sizes[i];
        return Strip(prefix + "_" + best, fallback, Mathf.Max(8, best));
    }

    /// <summary>Auf ganze Pixel gerundet.</summary>
    public static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, 0f);
    }

    /// <summary>Leeres Objekt in der Lauf-Szene (sonst bliebe es beim Szenenwechsel liegen).</summary>
    public static GameObject NewObject(string name, Vector2 at)
    {
        GameObject go = new GameObject(name);
        go.transform.position = Snap(at);

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded) SceneManager.MoveGameObjectToScene(go, gameScene);
        return go;
    }

    /// <summary>Objekt mit SpriteRenderer, sortiert relativ zum Spieler.</summary>
    public static SpriteRenderer NewRenderer(string name, Vector2 at, int sortingOffset, Transform parent = null)
    {
        GameObject go = parent != null ? new GameObject(name) : NewObject(name, at);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = Snap(at);
        }
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, sortingOffset);
        return sr;
    }

    /// <summary>Einmal-Effekt in Originalgroesse (wie FoxFx).</summary>
    public static void Play(Sprite[] frames, Vector2 at, float fps, int sortingOffset = 2)
    {
        FoxFx.Play(frames, at, fps, sortingOffset);
    }

    /// <summary>Lebende Gegner im Kreis (ohne Untouchable), in <paramref name="into"/>.</summary>
    public static void EnemiesWithin(Vector2 center, float radius, List<Enemy> into)
    {
        into.Clear();
        float rSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || !e.gameObject.activeInHierarchy) continue;
            if ((Body(e) - center).sqrMagnitude <= rSqr) into.Add(e);
        }
    }

    /// <summary>Koerpermitte eines Gegners (Pivot liegt meist unter den Fuessen).</summary>
    public static Vector2 Body(Enemy e)
    {
        return (Vector2)e.transform.position + RoyalSplat.BodyOffset;
    }

    /// <summary>Gilt der Gegner noch (lebt, aktiv)?</summary>
    public static bool Valid(Enemy e)
    {
        return e != null && e.gameObject.activeInHierarchy && e.HealthFraction > 0f;
    }

    /// <summary>Kleinster Abstand von p zur Strecke a-b.</summary>
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 1e-6f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Spieler-Werte mit Schutz gegen fehlenden Spieler.</summary>
    public static float Aoe => PlayerController.Instance != null ? Mathf.Clamp(PlayerController.Instance.AOERange, 0.5f, 2.4f) : 1f;
    public static float DurationFactor => PlayerController.Instance != null ? PlayerController.Instance.DurationMultiplier : 1f;
}
