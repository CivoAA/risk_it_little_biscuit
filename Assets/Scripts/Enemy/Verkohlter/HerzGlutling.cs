using UnityEngine;

/// <summary>
/// Ein Glutling (Endkampf, <see cref="VerkohlterHerzFinale"/>): kleiner
/// verkohlter Kruemel mit Flammenschopf, den das Herz aus dem Lavabecken
/// spuckt. Er flattert dem Spieler hinterher und faengt dabei Schuesse ab,
/// die dem Herzen galten - die Waffen zielen naemlich gern auf ihn.
/// Beruehrung tut weh (Beruehrungsschaden des Gegners).
///
/// Echter Gegner ohne Prefab (<see cref="Enemy.ConfigureRuntime"/>).
/// Bild: herz_glutling (8 Bilder 24x24).
/// </summary>
public class HerzGlutling : MonoBehaviour
{
    private const float Fps = 12f;
    private const float LaunchTime = 0.75f;
    private const float LaunchArc = 2.6f;
    private const float Grip = 2.2f;

    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Sprite[] frames;
    private Vector2 from, landing, vel;
    private float age, speed, phase;
    private Enemy enemy;
    private HerzGeschosse sparks;
    private bool removed;

    public static HerzGlutling Spawn(Vector2 from, Vector2 landing, float hp, float contactDamage, float speed,
                                     HerzGeschosse sparks)
    {
        var go = new GameObject("Glutling");
        go.SetActive(false);
        RunScene.Place(go, "Effekte");
        go.transform.position = from;
        int layer = LayerMask.NameToLayer("Enemy");
        if (layer >= 0) go.layer = layer;
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.28f;

        var art = new GameObject("Bild");
        art.transform.SetParent(go.transform, false);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Objects";
        sr.sortingOrder = 3;

        Enemy e = go.AddComponent<Enemy>();
        e.ConfigureRuntime(hp, contactDamage, speed, 8);

        var g = go.AddComponent<HerzGlutling>();
        g.rb = rb;
        g.body = sr;
        g.enemy = e;
        g.from = from;
        g.landing = landing;
        g.speed = speed;
        g.sparks = sparks;
        g.phase = Random.value * 10f;
        g.frames = VerkohlterArt.Strip("herz_glutling");
        sr.sprite = VerkohlterArt.Frame(g.frames, 0);

        go.SetActive(true);
        e.SelfSteered = true;
        e.SkipDeathEffect = true;
        return g;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (frames.Length > 0) body.sprite = frames[Mathf.FloorToInt((age + phase) * Fps) % frames.Length];
        PlayerController pc = PlayerController.Instance;
        if (pc != null) body.flipX = pc.transform.position.x < transform.position.x;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (age < LaunchTime)
        {
            // Bogen aus dem Becken
            float q = Mathf.Clamp01(age / LaunchTime);
            Vector2 p = Vector2.Lerp(from, landing, q) + Vector2.up * (4f * q * (1f - q) * LaunchArc);
            rb.MovePosition(VerkohlterArt.Snap(p));
            return;
        }
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeSelf) return;
        Vector2 to = (Vector2)pc.transform.position - rb.position;
        float sp = enemy != null ? enemy.CurrentSpeed : speed;
        Vector2 want = to.sqrMagnitude > 0.01f ? to.normalized * sp : Vector2.zero;
        // flattert: leichtes Schlingern quer zur Flugrichtung
        want += Vector2.Perpendicular(want).normalized * Mathf.Sin((age + phase) * 5f) * 0.9f;
        vel = Vector2.Lerp(vel, want, 1f - Mathf.Exp(-Grip * dt));
        rb.MovePosition(rb.position + vel * dt);
    }

    private void OnDestroy()
    {
        if (removed || sparks == null) return;
        // Zerplatzt (getroffen): ein paar Glutpixel
        for (int k = 0; k < 7; k++)
            sparks.AddSpark(transform.position, Random.insideUnitCircle * 2.8f, 0.4f);
    }

    /// <summary>Ohne Beute weg (Herz tot, Kampf vorbei).</summary>
    public void Remove()
    {
        if (this == null) return;
        removed = true;
        if (sparks != null)
            for (int k = 0; k < 5; k++) sparks.AddSpark(transform.position, Random.insideUnitCircle * 2f, 0.35f);
        Destroy(gameObject);
    }
}
