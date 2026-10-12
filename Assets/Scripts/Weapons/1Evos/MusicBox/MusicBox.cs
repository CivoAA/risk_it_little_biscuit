using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Eine abgestellte Musikbox des <see cref="MusicBoxQuartet"/>. Spielt selbst
/// nichts - die Evo spielt von hier aus (Noten steigen aus dem Deckel, der
/// Akkord-Ring platzt um die Box). Die Box zeigt nur, was passiert: sie
/// klappt auf, wenn sie gesetzt wird, huepft bei jedem Ton und klappt zu,
/// wenn sie geht.
///
/// Bilder: Resources/Weapons/music_box (0 zu, 1 offen, 2 offen im Huepfer).
/// Skaliert wird nie (pixelgenaue Kamera), Ausblenden nur ueber die Deckkraft.
/// </summary>
public class MusicBox : MonoBehaviour
{
    private const float OpenDelay = 0.08f;
    private const float PulseTime = 0.12f;
    private const float FadeTime = 0.25f;
    private const float PixelsPerUnit = 32f;

    /// <summary>Mitte der Box (Pivot unten) - hier platzt der Akkord-Ring.</summary>
    private static readonly Vector2 CenterOffset = new Vector2(0f, 0.3f);

    /// <summary>Hier steigen die Noten auf - aus dem offenen Deckel.</summary>
    private static readonly Vector2 MouthOffset = new Vector2(0f, 0.65f);

    private static readonly Color32 FallbackColor = new Color32(196, 106, 79, 255);

    private static Sprite[] frames;

    private SpriteRenderer sr;
    private float openIn = OpenDelay;
    private float pulse;
    private float fade = -1f;

    public Vector2 Center => (Vector2)transform.position + CenterOffset;
    public Vector2 Mouth => (Vector2)transform.position + MouthOffset;

    /// <summary>Klappt gerade zu und verschwindet gleich.</summary>
    public bool Closing => fade >= 0f;

    private static Sprite[] Frames
    {
        get
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                frames = SpriteStrip.Load("Weapons/music_box");
                if (frames.Length == 0) frames = new[] { SpriteStrip.Blob(16, FallbackColor) };
            }
            return frames;
        }
    }

    /// <summary>Stellt eine Box bei <paramref name="at"/> ab (Pivot = Boden).</summary>
    public static MusicBox Spawn(Vector2 at)
    {
        // aufs Pixelraster, sonst flimmert sie neben allem anderen
        at = new Vector2(Mathf.Round(at.x * PixelsPerUnit) / PixelsPerUnit,
                         Mathf.Round(at.y * PixelsPerUnit) / PixelsPerUnit);

        GameObject go = new GameObject("MusicBox");
        go.transform.position = at;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        MusicBox box = go.AddComponent<MusicBox>();
        FoxFx.Play(MochiMelody.HitFrames, box.Center, 22f, 3);
        return box;
    }

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, -1);
        sr.sprite = Frames[0];
    }

    /// <summary>Ein Ton von dieser Box - sie huepft kurz.</summary>
    public void Pulse()
    {
        if (!Closing) pulse = PulseTime;
    }

    /// <summary>Klappt zu, blendet aus und verschwindet.</summary>
    public void Close()
    {
        if (Closing) return;
        fade = FadeTime;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Sprite[] f = Frames;

        if (Closing)
        {
            fade -= dt;
            sr.sprite = f[0];
            Color c = sr.color;
            c.a = Mathf.Clamp01(fade / FadeTime);
            sr.color = c;
            if (fade <= 0f) Destroy(gameObject);
            return;
        }

        if (openIn > 0f)
        {
            openIn -= dt;
            sr.sprite = f[0];
            return;
        }

        pulse -= dt;
        int frame = pulse > 0f ? 2 : 1;
        sr.sprite = f[Mathf.Min(frame, f.Length - 1)];
    }
}
