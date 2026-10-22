using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zerlaufenes Schokostueck der <see cref="ChocoChipsEvo"/>: liegt unter den
/// Gegnern, bremst alle darin auf <see cref="Slow"/> und macht alle
/// <see cref="TickTime"/> Sekunden etwas Schaden (<see cref="DamageFactor"/>
/// vom Stueck). Erst platscht sie auf (Bilder 0-3), dann glaenzt und blubbert
/// sie in einer Schleife (4-9), am Ende blasst sie aus.
///
/// Es liegen hoechstens <see cref="MaxPuddles"/> gleichzeitig - die aelteste
/// macht Platz, sonst waere der Boden bei voller Salve nur noch braun.
/// </summary>
public class ChocoPuddle : MonoBehaviour
{
    public const float Life = 2.5f;
    public const float Slow = 0.6f;
    public const float DamageFactor = 0.4f;
    public const float TickTime = 0.5f;
    public const int MaxPuddles = 12;

    /// <summary>Halbe Breite der Pfuetze in Tiles (40 px Bild, etwas Rand).</summary>
    private const float Radius = 0.55f;

    /// <summary>Die Pfuetze ist flach gemalt: Hoehe = Breite / 2.</summary>
    private const float Flatten = 2f;

    private const int SpawnFrames = 4;
    private const float SpawnFps = 20f;
    private const float LoopFps = 8f;
    private const float FadeOut = 0.5f;
    private const float FastFade = 0.2f;

    private static readonly List<ChocoPuddle> active = new List<ChocoPuddle>();

    private SpriteRenderer sr;
    private float damage;
    private float life;
    private float age;
    private float nextTick;
    private readonly List<Enemy> hitBuffer = new List<Enemy>();

    public static void Create(Vector2 at, float chipDamage)
    {
        Sprite[] frames = Frames;
        if (frames.Length == 0) return;

        // Platz machen: die aelteste blasst schnell aus.
        active.RemoveAll(p => p == null);
        if (active.Count >= MaxPuddles) active[0].FadeNow();

        GameObject go = new GameObject("ChocoPuddle");
        go.transform.position = new Vector3(Mathf.Round(at.x * 32f) / 32f, Mathf.Round(at.y * 32f) / 32f, 0f);
        ChocoChipsEvo.MoveToRunScene(go);

        ChocoPuddle puddle = go.AddComponent<ChocoPuddle>();
        puddle.damage = chipDamage * DamageFactor;
        puddle.life = Life;
        puddle.nextTick = TickTime;
        puddle.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(puddle.sr, -2);
        puddle.sr.sprite = frames[0];
        active.Add(puddle);
    }

    private void FadeNow()
    {
        active.Remove(this);
        life = Mathf.Min(life, age + FastFade);
    }

    void OnDestroy()
    {
        active.Remove(this);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        Sprite[] frames = Frames;
        int spawnFrame = (int)(age * SpawnFps);
        if (spawnFrame < SpawnFrames || frames.Length <= SpawnFrames)
        {
            sr.sprite = frames[Mathf.Min(spawnFrame, frames.Length - 1)];
        }
        else
        {
            float loopAge = age - SpawnFrames / SpawnFps;
            int loop = frames.Length - SpawnFrames;
            sr.sprite = frames[SpawnFrames + (int)(loopAge * LoopFps) % loop];
        }

        float fade = Mathf.Clamp01((life - age) / FadeOut);
        sr.color = new Color(1f, 1f, 1f, fade);

        if (age < nextTick) return;
        nextTick = age + TickTime;

        Vector2 at = transform.position;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        hitBuffer.Clear();
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            Vector2 d = (Vector2)enemy.transform.position - at;
            d.y *= Flatten;
            if (d.sqrMagnitude <= Radius * Radius) hitBuffer.Add(enemy);
        }

        // Erst sammeln, dann treffen: TakeDamage kann Gegner aus Enemy.Alive nehmen.
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null) continue;
            enemy.ApplySlow(Slow, TickTime * 1.5f);
            enemy.TakeDamage(damage, null, 0f);
        }
    }

    private static Sprite[] frames;

    private static Sprite[] Frames
    {
        get
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                frames = SpriteStrip.Load("Weapons/choco_puddle");
            return frames;
        }
    }
}
