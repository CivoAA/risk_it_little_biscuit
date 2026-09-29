using UnityEngine;

/// <summary>
/// Spielt eine Reihe Sprites am SpriteRenderer ab - fuer Waffenobjekte, die
/// nur eine kleine Schleife brauchen (Strudel, Kruemel, Lache ...) und dafuer
/// keinen eigenen Animator-Controller. Die Bilder zeichnet
/// Tools/waffen_modelle.py.
///
/// Laeuft mit Time.deltaTime, steht also mit dem Spiel still (Level-Up,
/// Pause). Blendet am Anfang kurz ein; wer seine Lebenszeit kennt, meldet sie
/// ueber <see cref="SetLifeLeft"/> und wird zum Ende hin ausgeblendet.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 10f;

    [Tooltip("Jedes Exemplar startet bei einem anderen Bild - mehrere nebeneinander " +
             "sehen dann nicht wie geklont aus.")]
    [SerializeField] private bool randomStart = true;

    [Tooltip("Sekunden zum Einblenden nach dem Erscheinen. 0 = sofort da.")]
    [SerializeField] private float fadeIn = 0.12f;

    [Tooltip("Sekunden zum Ausblenden vor dem Ende (siehe SetLifeLeft).")]
    [SerializeField] private float fadeOut = 0.3f;

    private SpriteRenderer sr;
    private Color baseColor;
    private float time;
    private float age;
    private float lifeLeft = float.PositiveInfinity;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        if (randomStart && frames != null && frames.Length > 0)
            time = Random.Range(0, frames.Length) / Mathf.Max(0.01f, fps);
        Apply();
    }

    void Update()
    {
        time += Time.deltaTime;
        age += Time.deltaTime;
        Apply();
    }

    /// <summary>Restlebenszeit in Sekunden - ab <see cref="fadeOut"/> wird ausgeblendet.</summary>
    public void SetLifeLeft(float seconds) => lifeLeft = seconds;

    private void Apply()
    {
        if (frames != null && frames.Length > 0)
            sr.sprite = frames[Mathf.FloorToInt(time * fps) % frames.Length];

        float a = 1f;
        if (fadeIn > 0f) a = Mathf.Min(a, age / fadeIn);
        if (fadeOut > 0f) a = Mathf.Min(a, lifeLeft / fadeOut);

        Color c = baseColor;
        c.a *= Mathf.Clamp01(a);
        sr.color = c;
    }
}
