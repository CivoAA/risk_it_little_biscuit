using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Eine Note der <see cref="MochiMelody"/>: steigt ueber Mochis Kopf auf,
/// schwenkt in einer Kurve auf ihren Gegner ein und schlenkert dabei wie auf
/// einer Notenlinie, hinter sich eine Glitzerspur. Beim Treffer ploppt sie,
/// meldet das fuer das Crescendo und springt ab der Notenkette zum
/// naechsten Gegner weiter.
///
/// Lebt in der Lauf-Szene, nicht am Spieler; skaliert wird nie (pixelgenaue
/// Kamera), Ausblenden nur ueber die Deckkraft.
/// </summary>
public class MelodyNote : MonoBehaviour
{
    private const float Speed = 7.5f;
    private const float Life = 2.4f;
    private const float FadeTime = 0.25f;
    private const float HitRadius = 0.4f;
    private const float Fps = 8f;
    private const float SparkEvery = 0.07f;
    private const float RetargetRange = 4f;

    /// <summary>Schlenker quer zur Flugrichtung, in Tiles.</summary>
    private const float Wiggle = 0.14f;

    /// <summary>Der Gegner sitzt mit dem Pivot unten - getroffen wird etwas hoeher.</summary>
    private static readonly Vector2 AimOffset = new Vector2(0f, 0.35f);

    private static readonly Dictionary<Enemy, int> targeted = new Dictionary<Enemy, int>();

    /// <summary>Fliegt gerade schon eine Note auf diesen Gegner?</summary>
    public static bool IsTargeted(Enemy enemy) { return enemy != null && targeted.ContainsKey(enemy); }

    private MochiMelody owner;
    private Enemy target;
    private readonly HashSet<Enemy> struck = new HashSet<Enemy>();
    private SpriteRenderer sr;
    private Sprite[] frames;
    private Vector2 pos;
    private Vector2 vel;
    private float damage;
    private int bounces;
    private float age;
    private float lifeLeft;
    private float phase;
    private float sparkTimer;
    private bool fading;

    public static void Launch(MochiMelody owner, Vector2 at, Vector2 dir, Enemy target,
                              float damage, int bounces, int color)
    {
        GameObject go = new GameObject("MelodyNote");
        go.transform.position = at;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        MelodyNote note = go.AddComponent<MelodyNote>();
        note.owner = owner;
        note.pos = at;
        note.vel = dir.normalized * Speed;
        note.damage = damage;
        note.bounces = bounces;
        note.lifeLeft = Life;
        note.phase = Random.value * 10f;
        note.frames = MochiMelody.NoteFrames(color);
        note.sr.sprite = note.frames[0];
        note.SetTarget(target);
    }

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, 2);
    }

    void OnDestroy()
    {
        SetTarget(null);
    }

    private void SetTarget(Enemy enemy)
    {
        if (target != null && targeted.TryGetValue(target, out int n))
        {
            if (n <= 1) targeted.Remove(target);
            else targeted[target] = n - 1;
        }
        target = enemy;
        if (target != null)
        {
            targeted.TryGetValue(target, out int m);
            targeted[target] = m + 1;
        }
    }

    private static bool Valid(Enemy enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && !enemy.Untouchable;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;
        lifeLeft -= dt;

        if (fading || lifeLeft <= 0f)
        {
            Fade(dt);
            return;
        }

        if (!Valid(target))
        {
            SetTarget(Nearest(pos, RetargetRange));
        }

        if (target != null)
        {
            Vector2 aim = (Vector2)target.transform.position + AimOffset;
            Vector2 to = aim - pos;
            if (to.sqrMagnitude <= HitRadius * HitRadius)
            {
                Strike(aim);
                if (fading) return;
            }
            else
            {
                // erst steigt sie, dann zieht die Kurve immer enger
                float steer = Mathf.Lerp(3f, 16f, Mathf.Clamp01(age / 0.6f));
                Vector2 want = to.normalized * Speed;
                vel = Vector2.Lerp(vel, want, 1f - Mathf.Exp(-steer * dt));
                if (vel.sqrMagnitude < 0.01f) vel = want;
                vel = vel.normalized * Speed;
            }
        }

        pos += vel * dt;

        Vector2 side = new Vector2(-vel.y, vel.x).normalized;
        Vector2 shown = pos + side * Mathf.Sin(age * 15f + phase) * Wiggle;
        const float ppu = 32f;
        transform.position = new Vector3(Mathf.Round(shown.x * ppu) / ppu, Mathf.Round(shown.y * ppu) / ppu, 0f);
        sr.sprite = frames[(int)((age + phase) * Fps) % frames.Length];

        sparkTimer -= dt;
        if (sparkTimer <= 0f)
        {
            sparkTimer = SparkEvery;
            FoxFx.Play(MochiMelody.SparkFrames, shown - vel.normalized * 0.2f, 16f, 1);
        }
    }

    private void Strike(Vector2 aim)
    {
        Enemy hit = target;
        struck.Add(hit);
        FoxFx.Play(MochiMelody.HitFrames, aim, 22f, 3);
        hit.TakeDamage(damage, null, 0.3f);
        if (owner != null) owner.NoteLanded();

        if (bounces > 0)
        {
            Enemy next = Nearest(pos, MochiMelody.BounceRange);
            if (next != null)
            {
                bounces--;
                SetTarget(next);
                lifeLeft = Mathf.Max(lifeLeft, 1.2f);
                age = 0.3f;   // kurz wieder weit ausschwingen
                vel = (Vector2)(Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 70f : -70f) * vel);
                return;
            }
        }

        SetTarget(null);
        fading = true;
        Destroy(gameObject);
    }

    private Enemy Nearest(Vector2 from, float radius)
    {
        Enemy best = null;
        float bestScore = float.MaxValue;
        float maxSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (!Valid(enemy) || struck.Contains(enemy)) continue;
            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr > maxSqr) continue;
            float score = IsTargeted(enemy) ? sqr * 3f : sqr;
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        return best;
    }

    private void Fade(float dt)
    {
        if (!fading)
        {
            fading = true;
            SetTarget(null);
            lifeLeft = FadeTime;
        }
        pos += vel * dt * 0.5f;
        transform.position = pos;
        Color c = sr.color;
        c.a = Mathf.Clamp01(lifeLeft / FadeTime);
        sr.color = c;
        if (lifeLeft <= 0f) Destroy(gameObject);
    }
}
