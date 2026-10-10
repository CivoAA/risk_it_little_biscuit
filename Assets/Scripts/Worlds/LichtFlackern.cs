using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Laesst ein Leucht-Prop im Geisterwald flackern: Kerzen, Kuerbisse,
/// Laternen, Pilze. Moduliert die Staerke des Light2D und - wenn das Glow-Bild
/// nicht selbst animiert ist - ein wenig dessen Deckkraft. Jedes Exemplar hat
/// sein eigenes Rauschen, damit nicht alle Flammen im Gleichtakt zucken.
///
/// Rechnet nur, solange das Prop im Bild ist (OnBecameVisible/Invisible am
/// SpriteRenderer desselben Objekts). Setzt der GeistBuilder.
/// </summary>
public class LichtFlackern : MonoBehaviour
{
    [SerializeField] private Light2D licht;
    [Tooltip("Glow-Bild (unbeleuchtet). Leer oder animiert = Deckkraft bleibt.")]
    [SerializeField] private SpriteRenderer glow;
    [Tooltip("Wie stark das Licht schwankt (0 = ruhig, 0.3 = Kerze im Wind).")]
    [SerializeField, Range(0f, 0.6f)] private float amount = 0.15f;
    [SerializeField] private float speed = 2.4f;

    private float baseIntensity;
    private float seed;
    private bool visible = true;

    private void Awake()
    {
        if (licht != null) baseIntensity = licht.intensity;
        seed = Random.value * 100f;
        if (glow != null && glow.GetComponent<SpriteFlipbook>() != null) glow = null;
    }

    private void OnBecameVisible() => visible = true;
    private void OnBecameInvisible() => visible = false;

    private void Update()
    {
        if (!visible) return;
        float t = Time.time * speed;
        // langsames Atmen + schnelles Zucken
        float slow = Mathf.PerlinNoise(seed, t * 0.5f) * 2f - 1f;
        float fast = Mathf.PerlinNoise(seed + 31f, t * 3f) * 2f - 1f;
        float f = 1f + amount * (0.7f * slow + 0.3f * fast);

        if (licht != null) licht.intensity = baseIntensity * f;
        if (glow != null)
        {
            Color c = glow.color;
            c.a = Mathf.Clamp01(0.88f + 0.5f * amount * (0.7f * slow + 0.3f * fast));
            glow.color = c;
        }
    }
}
