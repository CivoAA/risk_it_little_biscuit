using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerHitFeedback : MonoBehaviour
{
    [Header("Screen Flash")]
    public Image damageVignette;     // Dein UI-Image
    public float vignetteMaxAlpha = 0.6f;
    public float vignetteFadeTime = 0.4f;

    private Coroutine vignetteCoroutine;

    [Header("References")]
    public ParticleSystem hitParticles;           // Dein ParticleSystem im Inspector zuweisen
    public SpriteRenderer playerSpriteRenderer;   // Dein Player-SpriteRenderer

    [Header("Flash Settings")]
    public Color flashColor = Color.red;          // Farbe für Treffer-Flash
    public float flashDuration = 0.1f;            // Wie lange der Flash sichtbar bleibt

    [Header("Particles")]
    public bool usePlayerColorForParticles = true; // Sollen Partikel die gleiche Farbe wie Player haben?

    private Color originalColor;

    private void Awake()
    {
        if (playerSpriteRenderer != null)
        {
            originalColor = playerSpriteRenderer.color;
        }
    }
    public void OnPlayerHit()
    {
        // Farbflash starten
        if (playerSpriteRenderer != null)
        {
            StartCoroutine(FlashRed());
        }

        // Partikel abspielen
        if (hitParticles != null)
        {
            var main = hitParticles.main;
            hitParticles.Stop();
            main.startColor = originalColor;
            hitParticles.Play();
        }
        // Screen Vignette Flash
        if (damageVignette != null)
        {
            if (vignetteCoroutine != null)
                StopCoroutine(vignetteCoroutine);

            vignetteCoroutine = StartCoroutine(VignetteFlash());
        }
    }

    private IEnumerator FlashRed()
    {
        playerSpriteRenderer.color = flashColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        playerSpriteRenderer.color = originalColor;
    }

    private IEnumerator VignetteFlash()
    {
        Color c = damageVignette.color;

        // Sofort auf max Alpha setzen
        c.a = vignetteMaxAlpha;
        damageVignette.color = c;

        float t = 0f;

        while (t < vignetteFadeTime)
        {
            t += Time.unscaledDeltaTime;

            float normalized = t / vignetteFadeTime;

            // Alpha langsam runterfahren
            c.a = Mathf.Lerp(vignetteMaxAlpha, 0f, normalized);
            damageVignette.color = c;

            yield return null;
        }

        // Sicherheit: Alpha = 0
        c.a = 0f;
        damageVignette.color = c;
    }
}
