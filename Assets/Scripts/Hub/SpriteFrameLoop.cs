using UnityEngine;

/// <summary>
/// Spielt auf einem SpriteRenderer eine kurze Bilderfolge in Dauerschleife -
/// gedacht fuer Deko, die nur ein bisschen leben soll (die Blasen in der
/// Erfolgs-Kapsel).
///
/// Bewusst ohne Animator und Controller: drei Sprites und ein Intervall reichen,
/// und SpriteOutline zieht den Bildwechsel von selbst nach.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteFrameLoop : MonoBehaviour
{
    [Tooltip("Reihenfolge der Bilder. Leer = das Skript schaltet sich ab.")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("Sekunden pro Bild.")]
    [SerializeField, Min(0.02f)] private float frameTime = 0.3f;

    [Tooltip("Zufaelliger Startpunkt, damit mehrere Kopien nicht im Gleichschritt laufen.")]
    [SerializeField] private bool randomStart = true;

    SpriteRenderer sr;
    float elapsed;
    int index;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (frames == null || frames.Length == 0)
        {
            enabled = false;
            return;
        }

        if (randomStart)
        {
            index = Random.Range(0, frames.Length);
            elapsed = Random.Range(0f, frameTime);
        }
        Show();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < frameTime) return;

        // Bei kurzen Intervallen koennen nach einem Hakler mehrere Bilder faellig sein.
        while (elapsed >= frameTime)
        {
            elapsed -= frameTime;
            index = (index + 1) % frames.Length;
        }
        Show();
    }

    void Show()
    {
        // Luecken im Array einfach ueberspringen, statt das Sprite zu loeschen.
        if (frames[index] != null) sr.sprite = frames[index];
    }
}
