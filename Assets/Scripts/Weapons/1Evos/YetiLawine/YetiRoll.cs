using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Die Yeti-Walze der <see cref="SnowballEffectEvo"/> (Yeti-Lawine): der Yeti
/// rollt sich selbst zu einem Riesenschneeball zusammen und wird
/// <see cref="Duration"/> Sekunden lang gesteuert wie sonst auch.
///
/// Rollen  - <see cref="SpeedBonus"/> schneller und <see cref="DamageTaken"/>
///           Schaden (bewusst NICHT unverwundbar - wer in eine Elite-Gruppe
///           rollt, merkt es). Gewoehnliche Gegner, die er beruehrt, rollt er
///           ein (<see cref="Enemy.EnterBubble"/> wie die P. Bubble Gun): sie
///           kleben in Schneeklumpen am Ball, drehen sich mit, koennen
///           solange weder sterben noch zuschlagen. Je zwei Gefangene wird der
///           Ball eine Groesse groesser. Elites, Bosse und alles bei voller
///           Ladung werden ueberrollt (Treffer + Stoss, je Gegner alle
///           <see cref="RollHitInterval"/> s). Die anderen Waffen feuern weiter.
/// Sprung  - zum Schluss hopst der Ball hoch ...
/// Lawine  - ... und schlaegt ein: Gefangene zerspringen
///           (<see cref="ShatterFactor"/>), die Schneewalze trifft und bremst
///           alles im Kreis, Frost bleibt liegen, <see cref="SprayBalls"/>
///           Schneesplitter rollen davon, die Kamera wackelt.
///
/// <see cref="PlayerController"/> fragt <see cref="Active"/> fuer Tempo und Schaden.
/// </summary>
public class YetiRoll : MonoBehaviour
{
    public const float Duration = 3f;
    public const float SpeedBonus = 0.6f;
    public const float DamageTaken = 0.5f;

    public const int MaxCaptives = 8;
    public const float RollHitInterval = 0.5f;

    /// <summary>Ueberrollen: damage * RollFactor * (1 + RollPerCaptive * Gefangene).</summary>
    public const float RollFactor = 1.5f;
    public const float RollPerCaptive = 0.1f;
    /// <summary>Schaden beim Einrollen (der Gefangene bleibt bei mindestens 1 Leben).</summary>
    public const float CaptureFactor = 0.5f;
    /// <summary>Gefangene zerspringen bei der Lawine mit so viel Schaden.</summary>
    public const float ShatterFactor = 3f;
    /// <summary>Lawine: damage * (BlastFactor + BlastPerCaptive * Gefangene).</summary>
    public const float BlastFactor = 2f;
    public const float BlastPerCaptive = 0.25f;
    public const float BlastRadius = 1.8f;
    public const float BlastRadiusPerCaptive = 0.2f;
    public const float MaxBlastRadius = 3.4f;
    public const float BlastSlow = 0.5f;
    public const int SprayBalls = 6;

    private const float HopTime = 0.35f;
    private const float HopHeight = 0.9f;
    private const float FormTime = 0.15f;
    private const float TrailEvery = 0.05f;
    private const float FramesPerTurn = 8f;
    private const float AfterImmunity = 0.6f;

    private enum Phase { Roll, Hop }

    private struct Captive
    {
        public Enemy enemy;
        public float angle;
        public SpriteRenderer clump;
    }

    private static YetiRoll current;

    /// <summary>Rollt der Yeti gerade (inklusive Sprung)?</summary>
    public static bool Active => current != null;

    private PlayerController player;
    private SpriteRenderer playerBody;
    private SpriteRenderer sr;
    private Snowball.Roll spray;
    private float damage, aoe, frostTime, life;
    private Phase phase;
    private float age, phaseAge, turn, spin, trailTimer;
    private Vector2 lastPos;
    private bool exploded;

    private readonly List<Captive> captives = new List<Captive>();
    private readonly HashSet<Enemy> taken = new HashSet<Enemy>();
    private readonly Dictionary<Enemy, float> lastHit = new Dictionary<Enemy, float>();
    private static readonly List<Enemy> near = new List<Enemy>();

    /// <summary>Startet die Walze. False, wenn schon eine rollt.</summary>
    public static bool Begin(PlayerController who, float damage, float duration, float aoe, float frostTime, Snowball.Roll spray)
    {
        if (current != null || who == null) return false;

        GameObject go = new GameObject("YetiRoll");
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        YetiRoll r = go.AddComponent<YetiRoll>();
        current = r;
        r.player = who;
        r.damage = damage;
        r.life = Mathf.Max(1f, duration);
        r.aoe = aoe;
        r.frostTime = frostTime;
        r.spray = spray;
        r.lastPos = who.transform.position;
        r.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(r.sr, 2);
        r.sr.sprite = SnowballEffect.BigFor(0)[0];

        r.playerBody = who.BodyRenderer();
        if (r.playerBody != null) r.playerBody.enabled = false;

        SweetFx.Play(SnowballEffect.BurstFor(0.9f), r.BallCenter, 18f, 3);
        WeaponSounds.Play(WeaponSounds.SnowThrow, 1.4f, 0.7f);
        ScreenShake.Kick(1f, 0.15f);
        r.Place();
        return true;
    }

    /// <summary>Sofort beenden, ohne Lawine (neuer Lauf, Tod).</summary>
    public static void Cancel()
    {
        if (current != null) Destroy(current.gameObject);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
        ReleaseAll(false);
        if (playerBody != null) playerBody.enabled = true;
        playerBody = null;
    }

    private int SizeIndex => Mathf.Min(3, captives.Count / 2);

    private float RadiusTiles => SnowballEffect.BigPixels(SizeIndex) * 0.5f / SweetFx.PixelsPerUnit;

    /// <summary>Ballmitte: der Ball steht auf den Fuessen des Yetis, beim Sprung hoeher.</summary>
    private Vector2 BallCenter
    {
        get
        {
            float hop = phase == Phase.Hop ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(phaseAge / HopTime)) * HopHeight : 0f;
            return (Vector2)player.transform.position + new Vector2(0f, RadiusTiles + hop);
        }
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy || player.playerHealth <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;
        phaseAge += dt;

        Vector2 p = player.transform.position;
        Vector2 delta = p - lastPos;
        lastPos = p;
        float moved = delta.magnitude;

        // Rollen: Bildwechsel nach Umfang, Gefangene drehen mit
        int px = SnowballEffect.BigPixels(SizeIndex);
        turn += moved * SweetFx.PixelsPerUnit / (Mathf.PI * px) * FramesPerTurn;
        if (Mathf.Abs(delta.x) > 1e-4f) sr.flipX = delta.x < 0f;
        spin += moved / Mathf.Max(0.3f, RadiusTiles) * (sr.flipX ? 1f : -1f);
        Sprite[] frames = SnowballEffect.BigFor(SizeIndex);
        sr.sprite = frames[(int)turn % frames.Length];

        if (phase == Phase.Roll)
        {
            if (moved > 0.001f)
            {
                trailTimer -= dt;
                if (trailTimer <= 0f)
                {
                    trailTimer = TrailEvery;
                    SweetFx.Play(SnowballEffect.Trail, p + new Vector2(Random.Range(-0.3f, 0.3f), 0.05f), 10f, 0);
                }
            }
            if (age > FormTime) Contact();
            if (age >= life)
            {
                phase = Phase.Hop;
                phaseAge = 0f;
                WeaponSounds.Play(WeaponSounds.SnowThrow, 1.2f, 1.2f);
            }
        }
        else if (phaseAge >= HopTime)
        {
            Explode();
            return;
        }

        Place();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        Vector2 c = BallCenter;
        float r = RadiusTiles * 0.85f;
        for (int i = captives.Count - 1; i >= 0; i--)
        {
            Captive cap = captives[i];
            if (cap.enemy == null || !cap.enemy.gameObject.activeInHierarchy)
            {
                if (cap.clump != null) Destroy(cap.clump.gameObject);
                captives.RemoveAt(i);
                continue;
            }
            float a = cap.angle + spin;
            Vector2 at = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
            cap.enemy.MoveInBubble(at - RoyalSplat.BodyOffset);
        }
    }

    void LateUpdate()
    {
        if (player == null) return;
        Place();

        Sprite[] clumps = SnowballEffect.Clump;
        for (int i = 0; i < captives.Count; i++)
        {
            Captive c = captives[i];
            if (c.enemy == null || c.clump == null) continue;
            c.clump.transform.position = SweetFx.Snap(SweetFx.Body(c.enemy));
            c.clump.sprite = clumps[(int)(age * 6f + i) % clumps.Length];
        }
    }

    private void Place()
    {
        transform.position = SweetFx.Snap(BallCenter);
    }

    // ------------------------------------------------------------------
    //  Kampf
    // ------------------------------------------------------------------

    private void Contact()
    {
        Vector2 c = BallCenter;
        float rad = RadiusTiles + 0.35f;
        float rSqr = rad * rad;

        near.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || taken.Contains(e)) continue;
            if ((SweetFx.Body(e) - c).sqrMagnitude > rSqr) continue;
            near.Add(e);
        }

        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;

            if (captives.Count < MaxCaptives && e.EnterBubble())
            {
                Grab(e, c);
                continue;
            }

            if (lastHit.TryGetValue(e, out float at) && Time.time - at < RollHitInterval) continue;
            lastHit[e] = Time.time;
            e.TakeDamage(damage * RollFactor * (1f + RollPerCaptive * captives.Count), null, 1.6f);
            WeaponSounds.Play(WeaponSounds.SnowHit, 1.2f, 0.8f);
        }
        near.Clear();
    }

    private void Grab(Enemy e, Vector2 center)
    {
        taken.Add(e);
        Vector2 d = SweetFx.Body(e) - center;
        float angle = Mathf.Atan2(d.y, d.x) - spin;

        SpriteRenderer clump = SweetFx.NewRenderer("SnowClump", SweetFx.Body(e), 3, transform);
        clump.sprite = SnowballEffect.Clump[0];

        captives.Add(new Captive { enemy = e, angle = angle, clump = clump });
        e.TakeDamage(damage * CaptureFactor, null, 0f);

        SweetFx.Play(SnowballEffect.Trail, SweetFx.Body(e), 14f, 3);
        WeaponSounds.Play(WeaponSounds.AvalancheGrab, 1f, 1f + 0.05f * captives.Count);
        if (captives.Count % 2 == 0) ScreenShake.Kick(1f, 0.12f);
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector2 at = (Vector2)player.transform.position + new Vector2(0f, 0.3f);
        int n = captives.Count;
        float radius = Mathf.Min(MaxBlastRadius, BlastRadius + BlastRadiusPerCaptive * n) * aoe;
        float blast = damage * (BlastFactor + BlastPerCaptive * n);

        // Gefangene kommen frei und zerspringen - jetzt koennen sie sterben.
        ReleaseAll(true);

        SweetFx.Play(SnowballEffect.AvalancheFor(radius), at, 18f, 3);
        WeaponSounds.Play(WeaponSounds.AvalancheBoom);
        WeaponSounds.Play(WeaponSounds.SnowBurst, 1f, 0.7f);
        ScreenShake.Kick(2.5f + 0.25f * n, 0.4f);

        SweetFx.EnemiesWithin(at, radius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;
            e.TakeDamage(blast, null, 1.5f);
            if (SweetFx.Valid(e)) e.ApplySlow(BlastSlow, 1.5f);
        }
        near.Clear();

        if (frostTime > 0f) SnowFrost.Create(at, radius * 0.9f, frostTime);

        float baseAngle = Random.value * 360f;
        for (int k = 0; k < SprayBalls; k++)
        {
            Vector2 d = Quaternion.Euler(0f, 0f, baseAngle + k * (360f / SprayBalls)) * Vector2.right;
            Snowball.Launch(at + d * radius * 0.5f, d, spray, true);
        }

        // Kurz durchatmen, damit der Pulk ihn nach dem Aufschlag nicht sofort erwischt.
        player.GrantImmunity(AfterImmunity);
        Destroy(gameObject);
    }

    private void ReleaseAll(bool shatter)
    {
        for (int i = 0; i < captives.Count; i++)
        {
            Captive c = captives[i];
            if (c.clump != null) Destroy(c.clump.gameObject);
            if (c.enemy == null) continue;
            c.enemy.LeaveBubble();
            if (shatter && c.enemy.gameObject.activeInHierarchy)
            {
                SweetFx.Play(SnowballEffect.BurstFor(0.7f), SweetFx.Body(c.enemy), 18f, 3);
                c.enemy.TakeDamage(damage * ShatterFactor, null, 1.5f);
            }
        }
        captives.Clear();
    }
}
