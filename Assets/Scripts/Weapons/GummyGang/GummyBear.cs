using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Baerchen der <see cref="GummyGang"/>: hopst los, rennt zu einem Gegner
/// (moeglichst einem, an dem noch keine zwei haengen), haelt sich fest und
/// beisst im Takt. Nach seinen Bissen zerplatzt es - ab Stufe 4 mit
/// Zuckerschock im Kreis. Findet es keinen Gegner, trottet es zurueck zum
/// Spieler und zerplatzt nach einer Weile von selbst.
/// </summary>
public class GummyBear : MonoBehaviour
{
    public struct Bite
    {
        public float damage;
        public float interval;
        public int bites;
        public float seekRange;
        public float shockDamage;
        public float shockRadius;
    }

    private enum State { Hop, Run, Hold }

    private const float HopTime = 0.3f;
    private const float HopHeight = 0.6f;
    private const float RunFps = 10f;
    private const float BiteOpen = 0.12f;
    private const float GrabDistance = 0.25f;
    private const float IdleLife = 6f;
    private const float MaxLife = 25f;
    private const float RetargetEvery = 0.4f;

    private static readonly List<GummyBear> all = new List<GummyBear>();
    private static readonly List<Enemy> near = new List<Enemy>();

    private Bite b;
    private string flavor;
    private State state;
    private Enemy target;
    private Vector2 grip;
    private Vector2 pos;
    private Vector2 hopFrom;
    private Vector2 hopTo;
    private float age;
    private float stateAge;
    private float idle;
    private float biteTimer;
    private float retarget;
    private int bitesLeft;
    private SpriteRenderer sr;

    public static GummyBear Spawn(Vector2 at, string flavor, Bite bite)
    {
        GameObject go = SweetFx.NewObject("GummyBear", at);
        GummyBear g = go.AddComponent<GummyBear>();
        g.b = bite;
        g.flavor = flavor;
        g.bitesLeft = bite.bites;
        g.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(g.sr, 2);
        g.sr.sprite = GummyGang.Bear(flavor)[0];
        g.pos = at;
        g.hopFrom = at;
        g.hopTo = at + Random.insideUnitCircle.normalized * 0.7f;
        g.state = State.Hop;
        return g;
    }

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        stateAge += dt;
        if (age > MaxLife)
        {
            Burst();
            return;
        }

        switch (state)
        {
            case State.Hop: UpdateHop(); break;
            case State.Run: UpdateRun(dt); break;
            case State.Hold: UpdateHold(dt); break;
        }
    }

    private void UpdateHop()
    {
        float u = Mathf.Clamp01(stateAge / HopTime);
        pos = Vector2.Lerp(hopFrom, hopTo, u);
        transform.position = SweetFx.Snap(pos + new Vector2(0f, 4f * HopHeight * u * (1f - u)));
        sr.sprite = GummyGang.Bear(flavor)[1];
        if (u >= 1f) Enter(State.Run);
    }

    private void UpdateRun(float dt)
    {
        retarget -= dt;
        if (!SweetFx.Valid(target) || target.Untouchable || retarget <= 0f)
        {
            retarget = RetargetEvery;
            if (!SweetFx.Valid(target) || target.Untouchable) target = FindTarget();
        }

        Vector2 goal;
        if (target != null)
        {
            idle = 0f;
            goal = TargetGrip();
        }
        else
        {
            // nichts zu beissen: beim Spieler bleiben, irgendwann platzen
            idle += dt;
            if (idle > IdleLife)
            {
                Burst();
                return;
            }
            goal = PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position + grip : pos;
        }

        Vector2 to = goal - pos;
        float step = GummyGang.RunSpeed * dt;
        if (target != null && to.magnitude <= Mathf.Max(GrabDistance, step))
        {
            pos = goal;
            Enter(State.Hold);
            biteTimer = b.interval * 0.5f;
            return;
        }
        if (target == null && to.magnitude < 0.6f)
        {
            sr.sprite = GummyGang.Bear(flavor)[0];
            return;
        }
        pos += to.normalized * Mathf.Min(step, to.magnitude);
        transform.position = SweetFx.Snap(pos);
        Sprite[] frames = GummyGang.Bear(flavor);
        sr.sprite = frames[(int)(age * RunFps) % Mathf.Min(4, frames.Length)];
    }

    private void UpdateHold(float dt)
    {
        if (!SweetFx.Valid(target) || target.Untouchable)
        {
            // Gegner weg: abspringen, weitersuchen
            target = null;
            SetSorting(null);
            Enter(State.Run);
            return;
        }

        pos = TargetGrip();
        transform.position = SweetFx.Snap(pos);

        biteTimer -= dt;
        Sprite[] frames = GummyGang.Bear(flavor);
        int open = Mathf.Min(4, frames.Length - 1);
        int shut = Mathf.Min(5, frames.Length - 1);
        sr.sprite = frames[biteTimer < BiteOpen ? open : shut];
        if (biteTimer > 0f) return;

        biteTimer = b.interval;
        target.TakeDamage(b.damage, null, 0f);
        WeaponSounds.Play(WeaponSounds.GummyBite);
        bitesLeft--;
        if (bitesLeft <= 0) Burst();
    }

    private void Enter(State s)
    {
        state = s;
        stateAge = 0f;
        if (s == State.Hold) SetSorting(target);
        if (s == State.Run) grip = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.1f));
    }

    /// <summary>Wo am Gegner es sich festhaelt (seitlich am Bauch).</summary>
    private Vector2 TargetGrip()
    {
        return SweetFx.Body(target) + new Vector2(grip.x * 0.6f, grip.y * 0.5f - 0.15f);
    }

    /// <summary>Am Gegner vor ihm sortieren, sonst wie die anderen Waffen.</summary>
    private void SetSorting(Enemy e)
    {
        SpriteRenderer es = e != null ? e.GetComponentInChildren<SpriteRenderer>() : null;
        if (es != null)
        {
            sr.sortingLayerID = es.sortingLayerID;
            sr.sortingOrder = es.sortingOrder + 2;
        }
        else
        {
            SaladFan.CopySorting(sr, 2);
        }
    }

    private Enemy FindTarget()
    {
        Enemy best = null;
        float bestScore = float.MaxValue;
        Vector2 home = PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position : pos;
        float maxSqr = b.seekRange * b.seekRange;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || !e.gameObject.activeInHierarchy) continue;
            if (((Vector2)e.transform.position - home).sqrMagnitude > maxSqr) continue;
            float d = ((Vector2)e.transform.position - pos).magnitude;
            // schon belegte Gegner nur, wenn sonst nichts da ist
            int crowd = CountOn(e);
            float score = d + crowd * (crowd >= GummyGang.BearsPerEnemy ? 100f : 2f);
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    private static int CountOn(Enemy e)
    {
        int n = 0;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].target == e) n++;
        return n;
    }

    private void Burst()
    {
        Vector2 at = pos + new Vector2(0f, 0.25f);
        SweetFx.Play(GummyGang.Pop(flavor), at, 16f, 3);
        WeaponSounds.Play(WeaponSounds.GummyPop);

        if (b.shockDamage > 0f && state == State.Hold)
        {
            SweetFx.Play(GummyGang.ShockFor(b.shockRadius), at, 18f, 3);
            WeaponSounds.Play(WeaponSounds.GummyShock);
            SweetFx.EnemiesWithin(at, b.shockRadius, near);
            for (int i = 0; i < near.Count; i++)
            {
                Enemy e = near[i];
                if (e != null) e.TakeDamage(b.shockDamage, null, 0.5f);
            }
            near.Clear();
        }
        Destroy(gameObject);
    }
}
