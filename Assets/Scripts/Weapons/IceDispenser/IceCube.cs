using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Eiswuerfel des <see cref="IceDispenser"/>: rutscht geradeaus, wird
/// gleichmaessig langsamer und bleibt nach <see cref="Slide.distance"/> Tiles
/// liegen, wackelt noch kurz und schmilzt. Durchschlaegt alles, trifft aber
/// jeden Gegner nur einmal; jeder Treffer bremst ihn um ein Fuenftel.
///
/// Gefrorene Gegner zerschmettert er (Splitter in die Nachbarschaft, auf der
/// letzten Stufe drei Splitter-Wuerfel), danach prallt er mit Karambolage zum
/// naechsten Gegner. Alle Werte kommen beim Abschuss als Kopie mit.
/// </summary>
public class IceCube : MonoBehaviour
{
    public struct Slide
    {
        public float damage;
        public float distance;
        public float hitRadius;
        public float shardRadius;
        public float freezeTime;
        public int ricochets;
        public bool avalanche;
    }

    private const float RestTime = 0.5f;
    private const float MeltTime = 0.35f;
    private const float TrailEvery = 0.07f;
    private const float Fps = 12f;
    private const float HitSlow = 0.8f;

    /// <summary>Splitter-Wuerfel: weniger Schaden, kuerzere Strecke.</summary>
    private const float MiniDamage = 0.5f;
    private const float MiniDistance = 0.55f;

    private Slide s;
    private bool mini;
    private Vector2 pos;
    private Vector2 dir;
    private float speed;
    private float decel;
    private float restAge = -1f;
    private float age;
    private float trailTimer;
    private SpriteRenderer sr;
    private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

    private static readonly List<Enemy> near = new List<Enemy>();

    public static void Launch(Vector2 at, Vector2 dir, Slide slide, bool mini)
    {
        GameObject go = SweetFx.NewObject(mini ? "IceMini" : "IceCube", at);
        IceCube c = go.AddComponent<IceCube>();
        c.s = slide;
        c.mini = mini;
        if (mini)
        {
            c.s.damage *= MiniDamage;
            c.s.distance *= MiniDistance;
            c.s.ricochets = 0;
            c.s.avalanche = false;
            c.s.hitRadius *= 0.8f;
        }
        c.pos = at;
        c.dir = dir.normalized;
        c.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(c.sr, 1);
        c.Kick(IceDispenser.SlideSpeed * (mini ? 0.85f : 1f), c.s.distance);
    }

    /// <summary>Neues Tempo; abgebremst so, dass er nach <paramref name="distance"/> liegt.</summary>
    private void Kick(float v, float distance)
    {
        speed = v;
        decel = v * v / (2f * Mathf.Max(0.5f, distance));
        restAge = -1f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        Sprite[] frames = mini ? IceDispenser.Mini : IceDispenser.Cube;

        if (restAge >= 0f)
        {
            restAge += dt;
            float melt = Mathf.Clamp01((restAge - RestTime) / MeltTime);
            sr.color = new Color(1f, 1f, 1f, 1f - melt);
            sr.sprite = frames[(int)(restAge * Fps * 0.5f) % frames.Length];
            if (melt >= 1f) Destroy(gameObject);
            return;
        }

        pos += dir * speed * dt;
        speed -= decel * dt;
        transform.position = SweetFx.Snap(pos);
        // schneller rutschen = schneller wackeln
        sr.sprite = frames[(int)(age * Fps * Mathf.Lerp(0.6f, 1.4f, speed / IceDispenser.SlideSpeed)) % frames.Length];

        trailTimer -= dt;
        if (trailTimer <= 0f)
        {
            trailTimer = TrailEvery;
            SweetFx.Play(IceDispenser.Trail, pos + new Vector2(Random.Range(-0.1f, 0.1f), -0.15f), 10f, 0);
        }

        HitTest();

        if (speed <= 0f)
        {
            speed = 0f;
            restAge = 0f;
        }
    }

    private void HitTest()
    {
        float r = s.hitRadius + 0.25f;
        float rSqr = r * r;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (i >= alive.Count) continue;
            Enemy e = alive[i];
            if (e == null || e.Untouchable || hit.Contains(e)) continue;
            if ((SweetFx.Body(e) - pos).sqrMagnitude > rSqr) continue;

            hit.Add(e);
            if (e.IsFrozen) Shatter(e);
            else Chill(e);
            if (restAge >= 0f || speed <= 0f) return;
        }
    }

    private void Chill(Enemy e)
    {
        WeaponSounds.Play(WeaponSounds.IceHit);
        bool boss = e.IsBoss;
        e.TakeDamage(s.damage, null, 0.3f);
        if (!SweetFx.Valid(e))
        {
            speed *= HitSlow;
            return;
        }

        if (IceChill.Add(e, s.freezeTime) && boss)
        {
            // Bosse frieren nicht ein - der dritte Stapel beisst stattdessen.
            e.TakeDamage(s.damage * IceDispenser.BossBiteFactor, null, 0f);
            SweetFx.Play(IceDispenser.ShatterFor(0.7f), SweetFx.Body(e), 16f, 3);
            WeaponSounds.Play(WeaponSounds.IceShatter, 0.7f, 1.15f);
        }
        speed *= HitSlow;
    }

    private void Shatter(Enemy e)
    {
        Vector2 at = SweetFx.Body(e);
        e.EndFreeze();
        IceChill.Clear(e);
        e.TakeDamage(s.damage * IceDispenser.ShatterFactor, null, 1f);

        SweetFx.Play(IceDispenser.ShatterFor(s.shardRadius), at, 16f, 3);
        WeaponSounds.Play(WeaponSounds.IceShatter);

        // Splitter in die Nachbarschaft
        SweetFx.EnemiesWithin(at, s.shardRadius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy o = near[i];
            if (o == null || o == e) continue;
            o.TakeDamage(s.damage * IceDispenser.ShardFactor, null, 0.5f);
            if (SweetFx.Valid(o) && !o.IsFrozen) IceChill.Add(o, s.freezeTime);
        }
        near.Clear();

        if (s.avalanche)
        {
            float baseAngle = Random.value * 360f;
            for (int k = 0; k < IceDispenser.AvalancheCubes; k++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, baseAngle + k * 120f) * Vector2.right;
                Launch(at + d * 0.3f, d, s, true);
            }
        }

        if (s.ricochets > 0 && Ricochet(at)) return;
        speed *= HitSlow;
    }

    private bool Ricochet(Vector2 from)
    {
        Enemy best = null;
        float bestSqr = 5f * 5f;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || hit.Contains(e)) continue;
            float d = (SweetFx.Body(e) - from).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = e;
            }
        }
        if (best == null) return false;

        s.ricochets--;
        dir = (SweetFx.Body(best) - pos).normalized;
        Kick(IceDispenser.SlideSpeed * 0.9f, Mathf.Sqrt(bestSqr) + 2f);
        return true;
    }
}
