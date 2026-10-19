using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Maiskorn der <see cref="PopcornPan"/>: fliegt im Bogen (Schatten am
/// Boden), liegt dann, wird heiss und poppt. Ein Pop zuendet alle liegenden
/// Koerner in Reichweite sofort mit. Mit Nachhuepfen wird aus dem Korn ein
/// Stueck Popcorn, das zum naechsten Gegner springt und dort noch einmal poppt.
/// Alle Werte kommen beim Wurf als Kopie mit.
/// </summary>
public class PopcornKernel : MonoBehaviour
{
    public struct Pop
    {
        public float damage;
        public float radius;
        public int hops;
    }

    private enum State { Flying, Lying, Done }

    private const float FlightTime = 0.5f;
    private const float FlightHeight = 1.2f;
    private const float HopTime = 0.38f;
    private const float HopHeight = 0.9f;
    private const float HopReach = 3f;
    private const float ChainFuseMin = 0.07f;
    private const float ChainFuseMax = 0.16f;
    private const float HotTime = 0.45f;

    /// <summary>Die Koerper der Gegner sitzen ueber ihrem Pivot - der Pop trifft dort.</summary>
    private static readonly Vector2 PopLift = new Vector2(0f, 0.2f);

    private static readonly List<PopcornKernel> lying = new List<PopcornKernel>();
    private static readonly List<Enemy> near = new List<Enemy>();

    private Pop p;
    private State state;
    private bool piece;
    private Vector2 from;
    private Vector2 to;
    private float flight;
    private float height;
    private float t;
    private float delay;
    private float fuse;
    private int spin;
    private SpriteRenderer body;
    private SpriteRenderer shadow;

    public static void Throw(Vector2 from, Vector2 to, Pop pop, float delay)
    {
        GameObject go = SweetFx.NewObject("PopcornKernel", from);
        PopcornKernel k = go.AddComponent<PopcornKernel>();
        k.p = pop;
        k.delay = delay;
        k.spin = Random.Range(0, 4);
        k.shadow = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(k.shadow, 0);
        k.shadow.sprite = PopcornPan.Shadow[0];

        GameObject b = new GameObject("Body");
        b.transform.SetParent(go.transform, false);
        k.body = b.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(k.body, 2);
        k.body.sprite = PopcornPan.Kernel[k.spin];
        k.Launch(from, to, FlightTime, FlightHeight);
        if (delay > 0f)
        {
            k.body.enabled = false;
            k.shadow.enabled = false;
        }
    }

    private void Launch(Vector2 a, Vector2 b, float time, float h)
    {
        from = a;
        to = b;
        flight = time;
        height = h;
        t = 0f;
        state = State.Flying;
        shadow.enabled = true;
    }

    void OnDestroy()
    {
        lying.Remove(this);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f)
        {
            delay -= dt;
            if (delay > 0f) return;
            body.enabled = true;
            shadow.enabled = true;
        }

        if (state == State.Flying) Fly(dt);
        else if (state == State.Lying) Lie(dt);
    }

    private void Fly(float dt)
    {
        t += dt / flight;
        float u = Mathf.Clamp01(t);
        Vector2 ground = Vector2.Lerp(from, to, u);
        transform.position = SweetFx.Snap(ground);
        float lift = 4f * height * u * (1f - u);
        body.transform.position = SweetFx.Snap(ground + new Vector2(0f, lift));

        Sprite[] frames = piece ? PopcornPan.Piece : PopcornPan.Kernel;
        int n = piece ? frames.Length : Mathf.Min(4, frames.Length);
        body.sprite = frames[(spin + (int)(t * 10f)) % n];

        if (u < 1f) return;

        body.transform.position = transform.position;
        shadow.enabled = false;
        if (piece)
        {
            Burst();
            return;
        }

        state = State.Lying;
        fuse = Random.Range(PopcornPan.FuseMin, PopcornPan.FuseMax);
        body.sprite = PopcornPan.Kernel[spin % 4];
        lying.Add(this);
    }

    private void Lie(float dt)
    {
        fuse -= dt;
        if (fuse <= HotTime)
        {
            // heiss: wackelt hin und her
            Sprite[] frames = PopcornPan.Kernel;
            int hot = Mathf.Min(4, frames.Length - 1);
            body.sprite = frames[Mathf.Min(frames.Length - 1, hot + (int)(Time.time * 14f) % 2)];
        }
        if (fuse <= 0f) Burst();
    }

    /// <summary>Von einem Pop in der Naehe angesteckt.</summary>
    private void Ignite()
    {
        if (state != State.Lying) return;
        fuse = Mathf.Min(fuse, Random.Range(ChainFuseMin, ChainFuseMax));
    }

    private void Burst()
    {
        lying.Remove(this);
        Vector2 at = transform.position;
        Vector2 hitAt = at + PopLift;

        SweetFx.Play(PopcornPan.PopFor(p.radius), hitAt, 18f, 3);
        WeaponSounds.Play(WeaponSounds.PopcornPop);

        SweetFx.EnemiesWithin(hitAt, p.radius + 0.2f, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e != null) e.TakeDamage(p.damage, null, 0.6f);
        }
        near.Clear();

        // Kettenreaktion
        float reach = p.radius * PopcornPan.ChainReach;
        float reachSqr = reach * reach;
        for (int i = lying.Count - 1; i >= 0; i--)
        {
            PopcornKernel k = lying[i];
            if (k == null || k == this) continue;
            if (((Vector2)k.transform.position - at).sqrMagnitude <= reachSqr) k.Ignite();
        }

        if (p.hops > 0 && Hop(at)) return;

        state = State.Done;
        Destroy(gameObject);
    }

    /// <summary>Als Popcorn zum naechsten Gegner weiterspringen.</summary>
    private bool Hop(Vector2 at)
    {
        Enemy best = null;
        float bestScore = float.MaxValue;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            float d = ((Vector2)e.transform.position - at).magnitude;
            // nicht auf der Stelle - die hat der Pop gerade getroffen
            if (d < p.radius * 0.6f || d > HopReach) continue;
            float score = d + Random.value;
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        Vector2 land = best != null
            ? (Vector2)best.transform.position + Random.insideUnitCircle * 0.25f
            : at + Random.insideUnitCircle.normalized * 1.5f;

        p.hops--;
        p.damage *= PopcornPan.HopDamage;
        p.radius *= PopcornPan.HopRadius;
        piece = true;
        Launch(at, land, HopTime, HopHeight);
        return true;
    }
}
