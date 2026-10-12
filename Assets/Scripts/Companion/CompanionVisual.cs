using UnityEngine;

/// <summary>
/// Aussehen des <see cref="Companion"/>: Makro, ein kleines Macaron-UFO.
///
/// Der Root-SpriteRenderer des Prefabs wird zum Bodenschatten, der Koerper
/// sitzt auf einem Kind und schwebt darueber. Das Wippen laeuft in ganzen
/// Pixeln (32 PPU), sonst zittert das Bild unter der Pixel-Perfect-Kamera.
///
/// Bilder liegen in Resources/Companion, je Shop-Stufe eine Sorte:
/// makro_t&lt;N&gt; (0-11 Schweben, 12-23 Schweben mit Blinzeln, 24-27 Schuss),
/// makro_shot_t&lt;N&gt;, makro_hit_t&lt;N&gt;, makro_shadow (0 = nah, 1 = hoch).
/// </summary>
public class CompanionVisual : MonoBehaviour
{
    private const string Folder = "Companion/";
    private const float PPU = 32f;
    private const int IdleFrames = 12;
    private const int ShootStart = 24;
    private const int ShootFrames = 4;

    [Tooltip("Abstand Schattenmitte -> Koerpermitte in Pixeln.")]
    [SerializeField] private int hoverHeight = 16;

    [Tooltip("Ausschlag des Wippens in Pixeln (nach oben und unten).")]
    [SerializeField] private float bobAmplitude = 1.6f;

    [SerializeField] private float bobPeriod = 1.7f;
    [SerializeField] private float idleFps = 10f;
    [SerializeField] private float shootFps = 16f;

    [Tooltip("Sekunden zwischen zwei Blinzlern (zufaellig in diesem Bereich).")]
    [SerializeField] private Vector2 blinkPause = new Vector2(2.5f, 5f);

    private SpriteRenderer shadow;
    private SpriteRenderer body;
    private Sprite[] bodyFrames;
    private Sprite[] shadowFrames;

    private float idleTime;
    private float bobTime;
    private float shootTime = -1f;
    private float nextBlink;
    private bool blinkWanted;
    private int blinkLoop = -1;
    private int lastLoop;
    private int tier = 1;

    public Sprite[] ShotFrames { get; private set; }
    public Sprite[] HitFrames { get; private set; }

    /// <summary>Wo die Zuckerperle herauskommt: Mitte der Cremefuellung.</summary>
    public Vector3 Muzzle
    {
        get { return body != null ? body.transform.position + new Vector3(0f, -2f / PPU, 0f) : transform.position; }
    }

    public void Init(int shopTier, SpriteRenderer rootRenderer)
    {
        tier = Mathf.Clamp(shopTier, 1, 3);
        shadow = rootRenderer;

        bodyFrames = SpriteStrip.Load(Folder + "makro_t" + tier);
        shadowFrames = SpriteStrip.Load(Folder + "makro_shadow");
        ShotFrames = SpriteStrip.Load(Folder + "makro_shot_t" + tier);
        HitFrames = SpriteStrip.Load(Folder + "makro_hit_t" + tier);

        if (body == null)
        {
            GameObject go = new GameObject("Body");
            go.transform.SetParent(transform, false);
            body = go.AddComponent<SpriteRenderer>();
        }

        if (shadow != null)
        {
            shadow.color = Color.white;
            body.sharedMaterial = shadow.sharedMaterial;
            body.sortingLayerID = shadow.sortingLayerID;
            body.sortingOrder = shadow.sortingOrder + 1;
        }

        if (bodyFrames.Length < ShootStart + ShootFrames)
        {
            // Bilder fehlen: wenigstens ein Klecks statt eines weissen Quadrats.
            bodyFrames = new[] { SpriteStrip.Blob(16, new Color32(243, 154, 172, 255)) };
        }

        bobTime = Random.value * bobPeriod;
        nextBlink = Random.Range(blinkPause.x, blinkPause.y);
        Apply();
    }

    /// <summary>Blick in Schussrichtung; die Bilder schauen nach rechts.</summary>
    public void Face(Vector2 direction)
    {
        if (body != null && Mathf.Abs(direction.x) > 0.05f) body.flipX = direction.x < 0f;
    }

    public void PlayShoot()
    {
        shootTime = 0f;
    }

    void Update()
    {
        if (body == null) return;

        float dt = Time.deltaTime;
        idleTime += dt;
        bobTime += dt;

        if (shootTime >= 0f)
        {
            shootTime += dt;
            if (shootTime * shootFps >= ShootFrames) shootTime = -1f;
        }

        nextBlink -= dt;
        if (nextBlink <= 0f) blinkWanted = true;

        Apply();
    }

    private void Apply()
    {
        if (body == null || bodyFrames == null || bodyFrames.Length == 0) return;

        if (bodyFrames.Length < ShootStart + ShootFrames)
        {
            body.sprite = bodyFrames[0];
        }
        else if (shootTime >= 0f)
        {
            body.sprite = bodyFrames[ShootStart + Mathf.Min(ShootFrames - 1, Mathf.FloorToInt(shootTime * shootFps))];
        }
        else
        {
            // Das Blinzeln steckt in der zweiten Schleife. Sie faengt erst am
            // naechsten Schleifenanfang an, damit das Lauflicht nicht springt.
            int frame = Mathf.FloorToInt(idleTime * idleFps);
            int loop = frame / IdleFrames;
            if (blinkWanted && loop != lastLoop)
            {
                blinkLoop = loop;
                blinkWanted = false;
                nextBlink = Random.Range(blinkPause.x, blinkPause.y);
            }

            lastLoop = loop;
            frame %= IdleFrames;
            body.sprite = bodyFrames[loop == blinkLoop ? IdleFrames + frame : frame];
        }

        float wave = Mathf.Sin(bobTime / bobPeriod * Mathf.PI * 2f);
        int bob = Mathf.RoundToInt(wave * bobAmplitude);
        body.transform.localPosition = new Vector3(0f, (hoverHeight + bob) / PPU, 0f);

        if (shadow != null && shadowFrames != null && shadowFrames.Length >= 2)
        {
            shadow.sprite = shadowFrames[bob > 0 ? 1 : 0];
        }
    }
}
