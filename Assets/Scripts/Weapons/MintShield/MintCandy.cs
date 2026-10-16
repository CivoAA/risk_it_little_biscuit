using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pfefferminzbonbon aus dem Minzsturm (<see cref="MintShield"/>, letzte
/// Stufe): fliegt drehend geradeaus, durchschlaegt bis zu <see cref="Pierce"/>
/// Gegner, wird zum Ende langsamer und verblasst.
/// </summary>
public class MintCandy : MonoBehaviour
{
    private const float Speed = 9f;
    private const float HitRadius = 0.35f;
    private const int Pierce = 3;
    private const float Fps = 16f;

    private Vector2 pos;
    private Vector2 dir;
    private float damage;
    private float travel;
    private float flown;
    private int hits;
    private float age;
    private SpriteRenderer sr;
    private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

    public static void Launch(Vector2 at, Vector2 dir, float damage, float distance)
    {
        GameObject go = SweetFx.NewObject("MintCandy", at);
        MintCandy c = go.AddComponent<MintCandy>();
        c.pos = at;
        c.dir = dir.normalized;
        c.damage = damage;
        c.travel = Mathf.Max(1f, distance);
        c.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(c.sr, 2);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float left = 1f - flown / travel;
        float step = Speed * Mathf.Lerp(0.35f, 1f, left) * dt;
        pos += dir * step;
        flown += step;
        transform.position = SweetFx.Snap(pos);

        Sprite[] frames = MintShield.CandyFrames;
        sr.sprite = frames[(int)(age * Fps) % frames.Length];
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(left * 4f));

        float rSqr = (HitRadius + 0.2f) * (HitRadius + 0.2f);
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = alive.Count - 1; i >= 0 && hits < Pierce; i--)
        {
            if (i >= alive.Count) continue;
            Enemy e = alive[i];
            if (e == null || e.Untouchable || hit.Contains(e)) continue;
            if ((SweetFx.Body(e) - pos).sqrMagnitude > rSqr) continue;
            hit.Add(e);
            hits++;
            e.TakeDamage(damage, MintShield.WaveSlow, 0.6f);
        }

        if (hits >= Pierce || flown >= travel) Destroy(gameObject);
    }
}
