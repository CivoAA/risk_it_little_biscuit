using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frostfleck des <see cref="SnowballEffect"/> (ab Stufe 4): liegt flach unter
/// den Gegnern, funkelt und bremst alle, die darin stehen, auf
/// <see cref="SnowballEffect.FrostSlow"/>. Macht keinen Schaden - wie die
/// Gelee-Pfuetze des Koenigsplumps (<see cref="SplatPuddle"/>).
/// </summary>
public class SnowFrost : MonoBehaviour
{
    private const float Fps = 5f;
    private const float FadeIn = 0.12f;
    private const float FadeOut = 0.4f;
    private const float TickTime = 0.15f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float radius, life, age, nextTick;

    public static void Create(Vector2 at, float radius, float seconds)
    {
        Sprite[] frames = SnowballEffect.FrostFor(radius);
        if (frames == null || frames.Length == 0) return;

        GameObject go = SweetFx.NewObject("SnowFrost", at);
        SnowFrost frost = go.AddComponent<SnowFrost>();
        frost.frames = frames;
        frost.radius = radius;
        frost.life = Mathf.Max(0.5f, seconds);
        frost.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(frost.sr, -2);
        frost.sr.sprite = frames[0];
        frost.sr.color = new Color(1f, 1f, 1f, 0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[Mathf.FloorToInt(age * Fps) % frames.Length];
        float a = Mathf.Min(Mathf.Clamp01(age / FadeIn), Mathf.Clamp01((life - age) / FadeOut));
        sr.color = new Color(1f, 1f, 1f, a);

        if (age < nextTick) return;
        nextTick = age + TickTime;

        // Flach gemalt (Hoehe = Breite / 1.25) - gebremst wird in dieser Ellipse, an den Fuessen gemessen.
        Vector2 at = transform.position;
        float reach = radius + 0.1f;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            Vector2 d = (Vector2)enemy.transform.position - at;
            d.y *= 1.25f;
            if (d.sqrMagnitude <= reach * reach)
                enemy.ApplySlow(SnowballEffect.FrostSlow, TickTime * 2f);
        }
    }
}
