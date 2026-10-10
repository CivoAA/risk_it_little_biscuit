using UnityEngine;

/// <summary>
/// Ein Baby-Squiddy aus der Quallen-Brut: ein echter Gegner
/// (<see cref="EnemyId.QuallenBaby"/>) mit kleinem Heiligenschein. Schwimmt
/// wie die Grosse im Rueckstoss - zusammenziehen, Schub, gleiten - und lenkt
/// dabei auf den Spieler zu. Beruehrt es ihn, entlaedt es sich mit einem
/// kleinen Blitz und platzt. Waffen schiessen darauf; zerschossen platzt es
/// harmlos (mit Beute). Nach <see cref="Lifetime"/> zerplatzt es von selbst.
///
/// Bei der Hochspannung der Grossen springt der Strom auf jedes Baby ueber
/// (<see cref="Overcharge"/>): dann entlaedt es sich in einem Kreis um sich.
///
/// Bilder: squiddy_baby (0-7 schwimmen, 8-13 zerplatzen).
/// </summary>
[RequireComponent(typeof(Enemy), typeof(Rigidbody2D))]
public class SquiddyBaby : MonoBehaviour
{
    private const float Fps = 12f;
    private const float Lifetime = 14f;
    private const float Grip = 2.2f;
    private const float TouchRadius = 0.5f;

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private Sprite[] frames;

    private Enemy enemy;
    private Rigidbody2D rb;
    private Vector2 vel;
    private float t, age;
    private bool done;
    private float doneT;
    private float spawnBoost;

    public bool Alive => !done && this != null;

    /// <summary>Hochspannung der Grossen: steht still und zittert, bis sie sich entlaedt.</summary>
    [System.NonSerialized] public bool Charged;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;
        enemy.HoldDeath = true;
        enemy.SkipDeathEffect = true;
        enemy.DeathHeld += OnShotDown;
        t = Random.value;
    }

    private void OnDestroy()
    {
        if (enemy != null) enemy.DeathHeld -= OnShotDown;
    }

    /// <summary>Beim Ausploppen: ein Stoss in diese Richtung.</summary>
    public void Push(Vector2 velocity)
    {
        vel = velocity;
        spawnBoost = 0.5f;
    }

    private void FixedUpdate()
    {
        if (done || Charged)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        float dt = Time.fixedDeltaTime;
        PlayerController player = PlayerController.Instance;
        // Rueckstoss im Takt der Bilder: Schub kurz nach dem Zusammenziehen
        float phase = (t * Fps / 8f) % 1f;
        float thrust = phase < 0.35f ? Mathf.Sin(phase / 0.35f * Mathf.PI) : 0f;
        float speed = enemy.CurrentSpeed * (0.35f + 1.6f * thrust);
        if (player != null && player.gameObject.activeSelf && spawnBoost <= 0f)
        {
            Vector2 to = (Vector2)player.transform.position - rb.position;
            if (to.sqrMagnitude > 0.01f)
                vel = Vector2.Lerp(vel, to.normalized * speed, 1f - Mathf.Exp(-Grip * dt));
        }
        spawnBoost -= dt;
        vel *= spawnBoost > 0f ? Mathf.Exp(-2.5f * dt) : 1f;
        rb.linearVelocity = vel;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (done)
        {
            doneT += dt;
            int k = 8 + Mathf.FloorToInt(doneT * 16f);
            if (frames != null && k < frames.Length) body.sprite = frames[k];
            else Finish();
            return;
        }
        t += dt;
        age += dt;
        if (frames != null && frames.Length >= 8) body.sprite = frames[Mathf.FloorToInt(t * Fps * (Charged ? 2f : 1f)) % 8];
        if (Mathf.Abs(vel.x) > 0.1f) body.flipX = vel.x < 0f;
        // unter Strom: zittert um ganze Pixel
        body.transform.localPosition = Charged ? new Vector3(((int)(t * 30f) % 2) / SquiddyFx.Ppu, 0f, 0f) : Vector3.zero;

        if (age >= Lifetime && !Charged)
        {
            Pop(false);
            return;
        }
        PlayerController player = PlayerController.Instance;
        if (player != null && player.gameObject.activeSelf &&
            ((Vector2)player.transform.position - rb.position).sqrMagnitude <= TouchRadius * TouchRadius)
        {
            player.TakeDamage(enemy.ContactDamage);
            Pop(false);
        }
    }

    /// <summary>Hochspannung: entlaedt sich in einem Kreis um sich (Treffer wertet die Grosse aus).</summary>
    public void Overcharge()
    {
        Pop(false);
    }

    private bool shotDown;

    private void OnShotDown()
    {
        shotDown = true;
        Pop(true);
    }

    private void Pop(bool killed)
    {
        if (done) return;
        done = true;
        doneT = 0f;
        enemy.Untouchable = true;
        if (hitbox != null) hitbox.enabled = false;
        SquiddySounds.Pop();
        _ = killed;
    }

    private void Finish()
    {
        if (shotDown) enemy.FinishHeldDeath();
        else Destroy(gameObject);
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer SquiddyBuilder.</summary>
    public void EditorBind(SpriteRenderer renderer, Collider2D collider, Sprite[] strip)
    {
        body = renderer;
        hitbox = collider;
        frames = strip;
    }
#endif
}
