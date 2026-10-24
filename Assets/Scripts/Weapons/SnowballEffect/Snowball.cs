using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Schneeball des <see cref="SnowballEffect"/>: rollt geradeaus, trifft
/// jeden Gegner einmal und waechst mit jedem Treffer eine Groessenstufe. Nach
/// <see cref="Roll.distance"/> Tiles, nach <see cref="Roll.maxHits"/> Treffern
/// oder an einem Boss zerstiebt er: Platzer im Kreis, kurze Bremse, je nach
/// Stufe Frostfleck und Schneesplitter. Die Evo rollt stattdessen einen
/// Riesenball (<see cref="YetiAvalanche"/>), streut beim Explodieren aber
/// diese kleinen hier.
/// </summary>
public class Snowball : MonoBehaviour
{
    public struct Roll
    {
        public float damage;
        public float distance;
        public int startSize;
        /// <summary>Nach so vielen getroffenen Gegnern zerstiebt der Ball (0 = unbegrenzt).</summary>
        public int maxHits;
        public float aoe;
        public float frostTime;
        public bool avalanche;
    }

    private const float TrailEvery = 0.08f;
    private const float FramesPerTurn = 8f;

    private Roll r;
    private int size;
    private bool mini;
    private Vector2 pos;
    private Vector2 dir;
    private float travelled;
    private float turn;
    private float trailTimer;
    private bool done;
    private SpriteRenderer sr;
    private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

    private static readonly List<Enemy> near = new List<Enemy>();

    public static void Launch(Vector2 at, Vector2 dir, Roll roll, bool mini = false)
    {
        GameObject go = SweetFx.NewObject(mini ? "SnowballMini" : "Snowball", at);
        Snowball b = go.AddComponent<Snowball>();
        b.r = roll;
        b.mini = mini;
        if (mini)
        {
            b.r.damage *= SnowballEffect.AvalancheDamage;
            b.r.distance *= SnowballEffect.AvalancheDistance;
            b.r.startSize = 0;
            b.r.avalanche = false;
            b.r.frostTime = 0f;
            b.r.maxHits = 2;
        }
        b.size = Mathf.Clamp(b.r.startSize, 0, SnowballEffect.MaxSize);
        b.pos = at;
        b.dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        b.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(b.sr, 1);
        b.sr.flipX = b.dir.x < 0f;
        b.sr.sprite = SnowballEffect.BallFor(b.size)[0];
    }

    /// <summary>Trefferradius in Tiles: halber Ball plus etwas Koerper des Gegners.</summary>
    private float HitRadius => SnowballEffect.BallPixels(size) * 0.5f / SweetFx.PixelsPerUnit + 0.25f;

    private float Speed => SnowballEffect.RollSpeed * (1f - 0.06f * size) * (mini ? 1.1f : 1f);

    void Update()
    {
        if (done) return;

        float dt = Time.deltaTime;
        float v = Speed;
        pos += dir * v * dt;
        travelled += v * dt;
        transform.position = SweetFx.Snap(pos);

        // Rollen: eine Umdrehung je Umfang, 8 Bilder je Umdrehung
        Sprite[] frames = SnowballEffect.BallFor(size);
        turn += v * dt * SweetFx.PixelsPerUnit / (Mathf.PI * SnowballEffect.BallPixels(size)) * FramesPerTurn;
        sr.sprite = frames[(int)turn % frames.Length];

        trailTimer -= dt;
        if (trailTimer <= 0f)
        {
            trailTimer = TrailEvery * (mini ? 1.6f : 1f);
            float half = SnowballEffect.BallPixels(size) * 0.5f / SweetFx.PixelsPerUnit;
            SweetFx.Play(SnowballEffect.Trail, pos + new Vector2(Random.Range(-0.1f, 0.1f), -half), 10f, 0);
        }

        HitTest();
        if (!done && travelled >= r.distance) Burst();
    }

    private void HitTest()
    {
        float rad = HitRadius;
        float rSqr = rad * rad;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (i >= alive.Count) continue;
            Enemy e = alive[i];
            if (e == null || e.Untouchable || hit.Contains(e)) continue;
            if ((SweetFx.Body(e) - pos).sqrMagnitude > rSqr) continue;

            hit.Add(e);
            bool boss = e.IsBoss;
            e.TakeDamage(r.damage * (1f + SnowballEffect.GrowDamage * size), null, 0.6f);
            WeaponSounds.Play(WeaponSounds.SnowHit, 1f, 1.1f - 0.06f * size);

            // Ein Boss haelt den Ball auf - er zerstiebt an ihm. Ebenso nach dem letzten Treffer.
            if (boss || (r.maxHits > 0 && hit.Count >= r.maxHits))
            {
                Burst();
                return;
            }
            if (size < SnowballEffect.MaxSize)
            {
                size++;
                SweetFx.Play(SnowballEffect.Trail, pos, 14f, 2);
            }
        }
    }

    private void Burst()
    {
        if (done) return;
        done = true;

        Vector2 at = pos;
        float radius = (SnowballEffect.BurstRadius + SnowballEffect.BurstRadiusPerSize * size) * r.aoe * (mini ? 0.7f : 1f);
        float damage = r.damage * (SnowballEffect.BurstBase + SnowballEffect.BurstPerSize * size);

        SweetFx.Play(SnowballEffect.BurstFor(radius), at, 18f, 3);
        WeaponSounds.Play(WeaponSounds.SnowBurst, mini ? 0.6f : 1f, mini ? 1.25f : 1f - 0.04f * size);

        SweetFx.EnemiesWithin(at, radius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;
            e.TakeDamage(damage, null, 0.8f);
            if (SweetFx.Valid(e)) e.ApplySlow(SnowballEffect.BurstSlow, 0.8f);
        }
        near.Clear();

        if (r.frostTime > 0f) SnowFrost.Create(at, radius * 1.1f, r.frostTime);

        if (r.avalanche)
        {
            float baseAngle = Random.value * 360f;
            for (int k = 0; k < SnowballEffect.AvalancheBalls; k++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, baseAngle + k * 120f) * Vector2.right;
                Launch(at + d * 0.3f, d, r, true);
            }
        }

        Destroy(gameObject);
    }
}
