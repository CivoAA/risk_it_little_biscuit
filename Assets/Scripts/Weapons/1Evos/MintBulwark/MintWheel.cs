using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Das Pfefferminz-Rad des Pfefferminz-Bollwerks (<see cref="MintShieldEvo"/>):
///
///   Verwandlung  die Blase schrumpft am Spieler zu einem harten Pfefferminzbonbon
///   Rollen       das Rad rollt zur dichtesten Gruppe (wird dabei schneller),
///                trifft und schubst alles auf dem Weg und zieht eine Frostspur
///   Abprall      am Ziel klirrt es: Frost-Stoss rundherum, kleiner Wackler,
///                dann weiter zur naechsten Gruppe - wie eine Flipperkugel
///   Rueckweg     nach dem letzten Abprall rollt es zum Spieler zurueck,
///                wird wieder zur Blase und die Evo loest die Nova aus
///
/// Die Drehung haengt am zurueckgelegten Weg (kein Rutschen), nach links dreht
/// es andersherum. Alle Werte kommen beim Start als Kopie mit.
/// </summary>
public class MintWheel : MonoBehaviour
{
    public struct Roll
    {
        public float damage;
        public float hitRadius;
        public int bounces;
        public float clinkDamage;
        public float clinkRadius;
        public float slowTime;
        public float search;
    }

    private enum Phase { Morph, Roll, Return }

    private const float MorphTime = 0.3f;
    private const float MorphFps = 20f;
    private const float StartSpeed = 6f;
    private const float MaxSpeed = 12f;
    private const float Accel = 18f;
    private const float ReturnSpeed = 14f;
    private const float MaxLife = 9f;

    /// <summary>Ein Bild der Drehung je so viel Weg (Umfang des Rads / 8 Bilder).</summary>
    private static readonly float TilesPerFrame = 2f * Mathf.PI * (15.5f / 32f) / 8f;

    private MintShieldEvo owner;
    private Roll r;
    private Phase phase = Phase.Morph;
    private float age;
    private float phaseAge;
    private Vector2 pos;
    private Vector2 target;
    private float speed;
    private float spin;
    private float trailTimer;

    private SpriteRenderer sr;
    private SpriteRenderer shadow;
    private readonly HashSet<Enemy> hitThisRun = new HashSet<Enemy>();
    private readonly HashSet<Enemy> visited = new HashSet<Enemy>();
    private readonly List<Vector2> claimed = new List<Vector2>();
    private static readonly List<Enemy> near = new List<Enemy>();

    public static void Launch(MintShieldEvo owner, Roll roll)
    {
        GameObject go = SweetFx.NewObject("MintWheel", owner.BubbleCenter);
        MintWheel w = go.AddComponent<MintWheel>();
        w.owner = owner;
        w.r = roll;
        w.pos = owner.BubbleCenter;
        w.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(w.sr, 2);
        w.shadow = SweetFx.NewRenderer("MintWheelShadow", w.pos, -1, go.transform);
        w.shadow.sprite = SpriteStrip.Blob(22, new Color32(20, 50, 40, 80));
        w.shadow.transform.localScale = new Vector3(1f, 0.35f, 1f);
        w.shadow.enabled = false;
        WeaponSounds.Play(WeaponSounds.MintWheelLaunch);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        phaseAge += dt;

        if (owner == null || !owner.IsActive || PlayerController.Instance == null
            || !PlayerController.Instance.gameObject.activeInHierarchy)
        {
            if (owner != null) owner.WheelReturned();
            Destroy(gameObject);
            return;
        }

        switch (phase)
        {
            case Phase.Morph: UpdateMorph(); break;
            case Phase.Roll: UpdateRoll(dt); break;
            case Phase.Return: UpdateReturn(dt); break;
        }
    }

    // ------------------------------------------------------------------

    private void UpdateMorph()
    {
        pos = owner.BubbleCenter;
        transform.position = SweetFx.Snap(pos);
        Sprite[] f = MintShieldEvo.MorphFrames;
        sr.sprite = f[Mathf.Min(f.Length - 1, (int)(phaseAge * MorphFps))];
        if (phaseAge < MorphTime) return;

        shadow.enabled = true;
        speed = StartSpeed;
        if (!NextTarget()) StartReturn();
        else phase = Phase.Roll;
    }

    private bool NextTarget()
    {
        if (r.bounces <= 0) return false;
        if (!RoyalSplat.FindCluster(pos, r.search, 1.4f, visited, claimed, 1.2f, out Vector2 spot)) return false;
        target = spot;
        claimed.Add(spot);
        if (claimed.Count > 3) claimed.RemoveAt(0);
        return true;
    }

    private void UpdateRoll(float dt)
    {
        speed = Mathf.Min(MaxSpeed, speed + Accel * dt);
        if (MoveTowards(target, speed * dt))
        {
            Clink();
            r.bounces--;
            hitThisRun.Clear();
            speed = StartSpeed + 2f;
            if (!NextTarget()) StartReturn();
        }
        if (age > MaxLife) StartReturn();
    }

    private void StartReturn()
    {
        phase = Phase.Return;
        phaseAge = 0f;
        hitThisRun.Clear();
    }

    private void UpdateReturn(float dt)
    {
        Vector2 home = owner.BubbleCenter;
        if (MoveTowards(home, ReturnSpeed * dt) || age > MaxLife + 2f)
        {
            SweetFx.Play(MintShieldEvo.MorphBackFrames, home, MorphFps, 2);
            owner.WheelReturned();
            Destroy(gameObject);
        }
    }

    /// <summary>Rollt ein Stueck auf <paramref name="to"/> zu. true = angekommen.</summary>
    private bool MoveTowards(Vector2 to, float step)
    {
        Vector2 d = to - pos;
        float len = d.magnitude;
        bool arrived = len <= step + 0.05f;
        Vector2 move = arrived ? d : d / len * step;
        pos += move;

        // Drehung am Weg; nach links andersherum
        spin += move.magnitude / TilesPerFrame * (move.x < -0.0001f ? -1f : 1f);
        Sprite[] f = MintShieldEvo.WheelFrames;
        int frame = ((int)Mathf.Floor(spin) % f.Length + f.Length) % f.Length;
        sr.sprite = f[frame];

        transform.position = SweetFx.Snap(pos);
        shadow.transform.position = SweetFx.Snap(pos + new Vector2(0f, -0.42f));

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f && move.sqrMagnitude > 0f)
        {
            trailTimer = 0.04f;
            SweetFx.Play(MintShieldEvo.TrailFrames, pos + new Vector2(Random.Range(-0.2f, 0.2f), -0.35f), 10f, 0);
        }

        HitAlong();
        return arrived;
    }

    private void HitAlong()
    {
        SweetFx.EnemiesWithin(pos, r.hitRadius + 0.25f, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null || hitThisRun.Contains(e)) continue;
            hitThisRun.Add(e);
            visited.Add(e);
            e.TakeDamage(r.damage, MintShield.WaveSlow, 1.5f);
            if (SweetFx.Valid(e)) e.ApplySlow(MintShield.WaveSlow, r.slowTime);
        }
        near.Clear();
    }

    private void Clink()
    {
        SweetFx.Play(MintShieldEvo.ClinkFor(r.clinkRadius), pos, 18f, 3);
        WeaponSounds.Play(WeaponSounds.MintWheelBounce);
        ScreenShake.Kick(1.2f, 0.1f);

        SweetFx.EnemiesWithin(pos, r.clinkRadius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;
            visited.Add(e);
            e.TakeDamage(r.clinkDamage, MintShield.WaveSlow, 1f);
        }
        near.Clear();
    }
}
