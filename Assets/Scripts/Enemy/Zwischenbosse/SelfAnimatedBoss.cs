using System.Collections;
using UnityEngine;

/// <summary>
/// Gemeinsamer Unterbau der Zwischenbosse Stracciatella, Knusperhaus und
/// Ofenkartoffel: laufen stur auf den Spieler zu, spielen ihre Bildstreifen
/// selbst ab (kein Animator), spiegeln sich selbst (Bilder schauen nach
/// rechts, Katalog-Facing Neutral) und machen zwischendurch ihre Attacke.
///
/// Eine Unterklasse schreibt nur noch <see cref="Run"/> (Ablauf aus
/// <see cref="Walk"/> und der eigenen Attacke) und gibt den Laufstreifen an.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public abstract class SelfAnimatedBoss : MonoBehaviour
{
    protected const float Fps = 12f;
    private const float PhaseTwoAt = 0.5f;

    /// <summary>Auf ganze Pixel (PPU 32), damit Effekte nicht flimmern.</summary>
    protected const float Ppu = 32f;

    [SerializeField] protected SpriteRenderer body;

    protected Enemy enemy;
    protected Rigidbody2D rb;
    protected bool walking;
    protected bool facingLeft;
    private float walkClock;

    public bool IsPhaseTwo => enemy != null && enemy.HealthFraction <= PhaseTwoAt;

    protected abstract Sprite[] WalkStrip { get; }

    /// <summary>Haelt ihn waehrend des Laufens kurz an (z. B. erschrocken). Das Bild setzt dann die Unterklasse.</summary>
    protected virtual bool Paused => false;

    /// <summary>Faktor aufs Lauftempo, 1 = normal (z. B. Ofenkartoffel auf der Jagd).</summary>
    protected virtual float SpeedFactor => 1f;

    private bool Moving => walking && !Paused;

    /// <summary>Der ganze Kampf: Laufen und Attacken im Wechsel. Startet bei OnEnable.</summary>
    protected abstract IEnumerator Run();

    /// <summary>Raeumt Effekte weg, wenn der Boss stirbt oder abgeschaltet wird.</summary>
    protected virtual void Cleanup() { }

    // ---------------------------------------------------------------- Ablauf

    protected virtual void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;

        // Haengt die Werkstatt einen Animator dran: der wuerde die Bilder ueberschreiben.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        if (body != null)
        {
            Animator bodyAnimator = body.GetComponent<Animator>();
            if (bodyAnimator != null) bodyAnimator.enabled = false;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        walking = false;
        Cleanup();
    }

    protected virtual void FixedUpdate()
    {
        if (rb == null) return;

        // Im Stand laesst er sich nicht schieben - sonst rutscht er aus seiner Attacke.
        rb.constraints = Moving ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.FreezeAll;

        Transform player = Player;
        if (Moving && player != null)
        {
            Vector2 to = (Vector2)player.position - rb.position;
            rb.linearVelocity = to.sqrMagnitude > 0.04f ? to.normalized * enemy.CurrentSpeed * SpeedFactor :Vector2.zero;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    protected virtual void Update()
    {
        if (!Moving) return;
        walkClock += Time.deltaTime;
        Show(WalkStrip, Mathf.FloorToInt(walkClock * Fps));
        FacePlayer();
    }

    protected IEnumerator Walk(float seconds)
    {
        walking = true;
        yield return new WaitForSeconds(seconds);
        walking = false;
    }

    protected IEnumerator PlayOnce(Sprite[] strip, int from = 0, int to = -1)
    {
        if (strip == null) yield break;
        if (to < 0) to = strip.Length - 1;
        for (int i = from; i <= to; i++)
        {
            Show(strip, i);
            yield return new WaitForSeconds(1f / Fps);
        }
    }

    /// <summary>Spielt strip[from..to] im Kreis, so lange wie angegeben.</summary>
    protected IEnumerator Loop(Sprite[] strip, int from, int to, float seconds)
    {
        float t = 0f;
        int n = to - from + 1;
        while (t < seconds)
        {
            t += Time.deltaTime;
            Show(strip, from + Mathf.FloorToInt(t * Fps) % n);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- Kleinkram

    protected void Show(Sprite[] strip, int i)
    {
        if (body == null || strip == null || strip.Length == 0) return;
        body.sprite = strip[((i % strip.Length) + strip.Length) % strip.Length];
    }

    /// <summary>Bild schaut nach rechts - steht der Spieler links, spiegeln. 0.3 Totzone gegen Flackern.</summary>
    protected void FacePlayer()
    {
        Transform player = Player;
        if (player == null || body == null) return;

        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.3f) return;
        facingLeft = dx < 0f;
        body.flipX = facingLeft;
    }

    /// <summary>Punkt im Bild (Bild schaut nach rechts) in Weltkoordinaten, mit Spiegelung.</summary>
    protected Vector2 Local(Vector2 p)
    {
        if (facingLeft) p.x = -p.x;
        return (Vector2)transform.position + p;
    }

    /// <summary>
    /// Der Spieler, auf den der Boss es gerade abgesehen hat - sonst null.
    /// Allein der eigene; im Koop der naechste, der noch steht (<see cref="NetPlayers"/>).
    /// </summary>
    protected Transform Player =>
        NetPlayers.TryNearest(transform.position, out NetPlayers.Target t) ? t.Transform : null;

    protected static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
    }

    protected SpriteRenderer NewEffectRenderer(string name, int order, Material material = null)
    {
        var go = new GameObject(name);
        RunScene.Place(go, "Effekte");
        var r = go.AddComponent<SpriteRenderer>();
        r.sharedMaterial = material != null ? material : body != null ? body.sharedMaterial : null;
        r.sortingLayerName = "Objects";
        r.sortingOrder = order;
        r.enabled = false;
        return r;
    }

    protected static void Kill(Component c)
    {
        if (c != null) Destroy(c.gameObject);
    }

#if UNITY_EDITOR
    public void EditorBindBody(SpriteRenderer bodyRenderer) => body = bodyRenderer;
#endif
}
