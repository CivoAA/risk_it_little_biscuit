using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine geworfene Scheibe des <see cref="CandyFrisbee"/>:
///
///   Anflug   segelt auf ihr Ziel zu (lenkt nach, wenn es sich bewegt)
///   Sprung   trifft es, sucht den naechsten Gegner, den sie noch nicht
///            getroffen hat, und fliegt weiter - bis <see cref="Flight.targets"/>
///            Gegner getroffen sind oder keiner mehr in Sprungweite steht
///   Rueckweg segelt zum Spieler zurueck und verschwindet in seiner Hand
///
/// Stirbt das Ziel unterwegs (andere Waffe), sucht sie sich ohne Treffer ein
/// neues. Alle Werte kommen beim Wurf als Kopie mit.
/// </summary>
public class FrisbeeDisc : MonoBehaviour
{
    public struct Flight
    {
        public float damage;
        public int targets;
        public float hitRadius;
        public float jumpRange;
    }

    private const float Speed = 11f;
    private const float ReturnSpeed = 13f;
    private const float Fps = 20f;
    private const float MaxLife = 6f;

    private CandyFrisbee owner;
    private Flight f;
    private Enemy target;
    private bool returning;
    private int hits;
    private float age;
    private Vector2 pos;
    private SpriteRenderer sr;
    private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

    public static void Launch(CandyFrisbee owner, Vector2 at, Enemy first, Flight flight)
    {
        GameObject go = SweetFx.NewObject("FrisbeeDisc", at);
        FrisbeeDisc d = go.AddComponent<FrisbeeDisc>();
        d.owner = owner;
        d.f = flight;
        d.pos = at;
        d.target = first;
        d.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(d.sr, 2);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        Sprite[] frames = CandyFrisbee.DiscFrames;
        sr.sprite = frames[(int)(age * Fps) % frames.Length];

        if (PlayerController.Instance == null || !PlayerController.Instance.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        if (!returning && age > MaxLife) returning = true;

        if (returning) UpdateReturn(dt);
        else UpdateSeek(dt);

        transform.position = SweetFx.Snap(pos);
    }

    private void UpdateSeek(float dt)
    {
        if (!SweetFx.Valid(target) || target.Untouchable)
        {
            target = NextTarget();
            if (target == null)
            {
                returning = true;
                return;
            }
        }

        Vector2 to = SweetFx.Body(target);
        pos = Vector2.MoveTowards(pos, to, Speed * dt);
        if ((to - pos).sqrMagnitude > f.hitRadius * f.hitRadius) return;

        hit.Add(target);
        hits++;
        target.TakeDamage(f.damage, null, 0.8f);
        WeaponSounds.Play(WeaponSounds.FrisbeeHit);

        target = hits < f.targets ? NextTarget() : null;
        if (target == null) returning = true;
    }

    /// <summary>Naechster lebender Gegner in Sprungweite, den die Scheibe noch nicht getroffen hat.</summary>
    private Enemy NextTarget()
    {
        Enemy best = null;
        float bestSqr = f.jumpRange * f.jumpRange;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || hit.Contains(e) || !SweetFx.Valid(e)) continue;
            float d = (SweetFx.Body(e) - pos).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = e;
            }
        }
        return best;
    }

    private void UpdateReturn(float dt)
    {
        Vector2 home = owner != null
            ? owner.CatchPoint
            : (Vector2)PlayerController.Instance.transform.position + CandyFrisbee.Origin;
        pos = Vector2.MoveTowards(pos, home, ReturnSpeed * dt);
        if ((home - pos).sqrMagnitude < 0.04f || age > MaxLife + 3f) Destroy(gameObject);
    }
}
