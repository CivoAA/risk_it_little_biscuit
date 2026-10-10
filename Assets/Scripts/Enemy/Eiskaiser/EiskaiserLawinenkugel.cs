using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Lawinenkugel aus dem Kaiser-Kick des <see cref="EnemyEiskaiser"/>.
///
/// Ein Schneeball, der dem Spieler hinterherrollt und dabei traege nachlenkt.
/// Unterwegs FRISST er die Horde: jeder normale Gegner, den er beruehrt,
/// verschwindet in ihm, die Kugel waechst und bekommt mehr Leben (bunte
/// Brocken stecken dann sichtbar im Schnee).
///
/// Die Kugel ist ein echter Gegner (<see cref="EnemyId.Lawinenkugel"/>): die
/// Waffen schiessen auf sie, und mit dem Leben schrumpft sie. Wer sie
/// zerschiesst, loest eine Schneelawine aus, die die Gegner drumherum
/// mitreisst. Trifft sie dagegen den Spieler, platzt sie dort - je groesser,
/// desto mehr Schaden. Gegen Hindernisse oder nach <see cref="Lifetime"/>
/// zerfaellt sie ohne Wirkung.
///
/// Bilder aus Tools/eiskaiser.py: eiskaiser_kugel (6 Groessen x 4 Rollbilder),
/// eiskaiser_platzt (8 Bilder).
/// </summary>
[RequireComponent(typeof(Enemy), typeof(Rigidbody2D))]
public class EiskaiserLawinenkugel : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float Lifetime = 10f;
    private const float BaseSpeed = 4.4f;
    private const float SpeedPerSize = 0.2f;
    /// <summary>
    /// Lenkung als Schwung: die Geschwindigkeit zieht mit dieser Rate zum
    /// Spieler. So schiesst sie vorbei und kommt in einem Bogen zurueck - mit
    /// fester Drehrate hat sie ihn nur umkreist.
    /// </summary>
    private const float Grip = 1.5f, GripP2 = 1.9f;
    private const float GrowthPerMeal = 0.35f;                 // Wachstum je geschlucktem Gegner
    private const float HealthPerMeal = 35f;
    private const float StartGrowth = 2.6f;
    private const float MaxGrowth = 6.4f;
    private const float EatMargin = 0.45f;
    private const float PlayerHitPerSize = 0.3f;               // Schaden = Kontakt * (1 + 0.3 * Groesse)
    private const float AvalancheBase = 1.6f, AvalanchePerSize = 0.45f;
    private const float AvalancheEliteFraction = 0.25f;        // Elites verlieren so viel ihres Lebens

    /// <summary>Radius je Groesse in Pixeln (muss zu Tools/eiskaiser.py passen).</summary>
    private static readonly int[] RadiusPx = { 8, 11, 14, 18, 22, 27, 32 };
    private const int RollFrames = 4;
    private const float PixelsPerUnit = 32f;

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private CircleCollider2D hitbox;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] burst;

    // ---------------------------------------------------------------- Zustand

    private Enemy enemy;
    private Rigidbody2D rb;
    private bool rolling;
    private bool phaseTwo;
    private bool done;
    private Vector2 dir;
    private Vector2 vel;
    private float growth = StartGrowth;
    private int size;
    private float age;
    private float rollPhase;
    private float nextMeal;

    private static readonly List<Enemy> scratch = new List<Enemy>();

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;
        enemy.HoldDeath = true;
        enemy.SkipDeathEffect = true;
        enemy.DeathHeld += OnShotDown;

        // Solange der Kaiser sie noch formt: nicht treffbar, kein Koerper
        enemy.Untouchable = true;
        if (hitbox != null) hitbox.enabled = false;
        size = 0;
        Show();
    }

    private void OnDestroy()
    {
        if (enemy != null) enemy.DeathHeld -= OnShotDown;
    }

    /// <summary>Losschiessen (der Tritt).</summary>
    public void Launch(Vector2 direction, bool secondPhase)
    {
        dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        phaseTwo = secondPhase;
        rolling = true;
        vel = dir * BaseSpeed * 1.3f;                      // der Tritt gibt Extra-Schwung
        enemy.Untouchable = false;
        if (hitbox != null) hitbox.enabled = true;
        UpdateSize();
    }

    private void FixedUpdate()
    {
        if (!rolling || done)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        PlayerController player = PlayerController.Instance;
        if (player != null && player.gameObject.activeSelf)
        {
            Vector2 to = (Vector2)player.transform.position - Center;
            if (to.sqrMagnitude > 0.01f)
            {
                float grip = phaseTwo ? GripP2 : Grip;
                vel = Vector2.Lerp(vel, to.normalized * Speed, 1f - Mathf.Exp(-grip * Time.fixedDeltaTime));
            }
        }
        if (vel.sqrMagnitude > 0.01f) dir = vel.normalized;
        rb.linearVelocity = vel;
    }

    private void Update()
    {
        if (done) return;
        if (!rolling)
        {
            Show();
            return;
        }

        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Burst(false);
            return;
        }

        // Rollen: je schneller, desto schneller die Bilder
        rollPhase += Time.deltaTime * vel.magnitude * 2.4f;
        if (Time.time >= nextMeal)
        {
            nextMeal = Time.time + 0.08f;
            Eat();
        }
        UpdateSize();
        Show();
        CheckPlayer();
    }

    private float Speed => BaseSpeed + SpeedPerSize * size;
    private float RadiusUnits => RadiusPx[size] / PixelsPerUnit;
    private Vector2 Center => (Vector2)transform.position + Vector2.up * RadiusUnits;

    // ---------------------------------------------------------------- Fressen

    private void Eat()
    {
        float reach = RadiusUnits + EatMargin;
        Vector2 c = Center;
        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.Role != EnemyRole.Normal || e.Identity == EnemyId.Lawinenkugel) continue;
            if (((Vector2)e.transform.position - c).sqrMagnitude > reach * reach) continue;

            Destroy(e.gameObject);
            growth = Mathf.Min(MaxGrowth, growth + GrowthPerMeal);
            float hp = enemy.HealthFraction * enemy.MaxHealth + HealthPerMeal;
            float max = enemy.MaxHealth + HealthPerMeal;
            enemy.ResetHealthPool(max, hp / max);
            EiskaiserSounds.Gulp();
        }
    }

    private void UpdateSize()
    {
        float hf = Mathf.Clamp01(enemy.HealthFraction);
        int s = Mathf.Clamp(Mathf.RoundToInt(growth * (0.35f + 0.65f * hf)), 0, RadiusPx.Length - 1);
        if (s == size) return;
        size = s;
        if (hitbox != null)
        {
            hitbox.radius = RadiusUnits * 0.9f;
            hitbox.offset = new Vector2(0f, RadiusUnits);
        }
    }

    private void Show()
    {
        if (body == null || frames == null || frames.Length < RadiusPx.Length * RollFrames) return;
        int f = Mathf.FloorToInt(rollPhase) % RollFrames;
        // Rollt die Kugel nach links, laufen die Bilder rueckwaerts
        if (dir.x < 0f) f = (RollFrames - f) % RollFrames;
        body.sprite = frames[size * RollFrames + f];
    }

    // ---------------------------------------------------------------- Treffer

    private void CheckPlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf) return;
        float reach = RadiusUnits + 0.3f;
        if (((Vector2)player.transform.position - Center).sqrMagnitude > reach * reach) return;

        player.TakeDamage(enemy.ContactDamage * (1f + PlayerHitPerSize * size));
        Burst(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!rolling || done || age < 0.3f) return;
        GameObject other = collision.gameObject;
        if (other.CompareTag("Player") || other.GetComponentInParent<Enemy>() != null) return;
        Burst(false);                                      // gegen ein Hindernis gerollt
    }

    /// <summary>Zerschossen: Lawine reisst die Gegner drumherum mit, dann normal sterben (Beute).</summary>
    private void OnShotDown()
    {
        if (done) return;
        float reach = AvalancheBase + AvalanchePerSize * size;
        Vector2 c = Center;
        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.IsBoss) continue;
            if (((Vector2)e.transform.position - c).sqrMagnitude > reach * reach) continue;
            if (e.Role == EnemyRole.Elite) e.TakeDamage(e.MaxHealth * AvalancheEliteFraction);
            else if (e.Role == EnemyRole.Normal) e.TakeDamage(e.MaxHealth * 10f + 999f);
        }
        Burst(true);
    }

    private void Burst(bool shotDown)
    {
        if (done) return;
        done = true;
        rolling = false;
        rb.linearVelocity = Vector2.zero;

        if (burst != null && burst.Length > 0)
        {
            var go = new GameObject("Lawine");
            RunScene.Place(go, "Effekte");
            go.transform.position = Center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Objects";
            sr.sortingOrder = 30;
            go.AddComponent<EiskaiserBurst>().Play(sr, burst, 16f);
        }
        ScreenShake.Kick(2f + size, 0.2f + 0.05f * size);
        EiskaiserSounds.Shatter();

        if (shotDown) enemy.FinishHeldDeath();
        else Destroy(gameObject);
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer EiskaiserBuilder.</summary>
    public void EditorBind(SpriteRenderer renderer, CircleCollider2D collider, Sprite[] rollFrames, Sprite[] burstFrames)
    {
        body = renderer;
        hitbox = collider;
        frames = rollFrames;
        burst = burstFrames;
    }
#endif
}
