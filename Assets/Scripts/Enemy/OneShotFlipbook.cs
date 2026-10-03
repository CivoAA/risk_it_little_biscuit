using UnityEngine;

/// <summary>
/// Spielt einen Bildstreifen genau einmal ab und gibt das Objekt dann an den
/// <see cref="RunPool"/> zurueck - fuer Todeseffekte, die laenger sind als
/// ein Puff (der Verkohlte zerplatzt in 24 Bildern). Laeuft mit
/// Time.deltaTime, steht also bei Level-Up und Pause mit still.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OneShotFlipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 12f;

    [Tooltip("So lange bleibt das letzte Bild noch stehen, bevor es verschwindet.")]
    [SerializeField] private float holdLast = 1.5f;

    private SpriteRenderer sr;
    private float time;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        time = 0f;
        Apply();
    }

    private void Update()
    {
        time += Time.deltaTime;
        Apply();

        float length = frames != null ? frames.Length / Mathf.Max(0.01f, fps) : 0f;
        if (time >= length + holdLast) RunPool.Release(gameObject);
    }

    private void Apply()
    {
        if (sr == null || frames == null || frames.Length == 0) return;
        sr.sprite = frames[Mathf.Clamp(Mathf.FloorToInt(time * fps), 0, frames.Length - 1)];

        // Die letzte halbe Sekunde blendet es aus, statt wegzuspringen.
        float left = frames.Length / Mathf.Max(0.01f, fps) + holdLast - time;
        Color c = sr.color;
        c.a = Mathf.Clamp01(left / 0.5f);
        sr.color = c;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer die Bauskripte.</summary>
    public void EditorBind(Sprite[] strip, float framesPerSecond, float hold)
    {
        frames = strip;
        fps = framesPerSecond;
        holdLast = hold;
    }
#endif
}
