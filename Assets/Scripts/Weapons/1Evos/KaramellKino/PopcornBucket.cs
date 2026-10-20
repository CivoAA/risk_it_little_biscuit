using UnityEngine;

/// <summary>
/// Der Kino-Eimer des <see cref="PopcornPanEvo"/>: faellt von oben (Schatten
/// waechst am Boden), schlaegt auf, kippt um und verschuettet seine Koerner,
/// liegt dann kurz und blendet aus.
///
/// Bilder popcorn_bucket: 0 stehend, 1-3 Kippen, 4-5 liegend (verschuettet).
/// </summary>
public class PopcornBucket : MonoBehaviour
{
    private const float FallTime = 0.4f;
    private const float FallHeight = 4f;
    private const float TipFps = 14f;
    private const int SpillFrame = 3;
    private const float LieTime = 0.7f;
    private const float FadeTime = 0.3f;

    private PopcornPanEvo pan;
    private int kernels;
    private Vector2 ground;
    private float t;
    private bool landed;
    private bool spilled;
    private float tipAge;
    private SpriteRenderer body;
    private SpriteRenderer shadow;

    public static void Drop(PopcornPanEvo pan, Vector2 at, int kernels)
    {
        GameObject go = SweetFx.NewObject("PopcornBucket", at);
        PopcornBucket b = go.AddComponent<PopcornBucket>();
        b.pan = pan;
        b.kernels = kernels;
        b.ground = at;
        b.shadow = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(b.shadow, 0);
        b.shadow.sprite = PopcornPan.Shadow[0];

        GameObject body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        b.body = body.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(b.body, 3);
        b.body.sprite = PopcornPanEvo.BucketFrames[0];
        b.body.transform.position = SweetFx.Snap(at + new Vector2(0f, FallHeight));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Sprite[] frames = PopcornPanEvo.BucketFrames;

        if (!landed)
        {
            t += dt / FallTime;
            float u = Mathf.Clamp01(t);
            body.transform.position = SweetFx.Snap(ground + new Vector2(0f, FallHeight * (1f - u * u)));
            // Schatten erst klein, dann voll (nur zwei Stufen - Pixelart wird nicht skaliert)
            shadow.enabled = u > 0.35f;
            if (u < 1f) return;

            landed = true;
            shadow.enabled = false;
            body.transform.position = SweetFx.Snap(ground);
            WeaponSounds.Play(WeaponSounds.PopcornPop, 1f, 0.6f);
            return;
        }

        tipAge += dt;
        int f = Mathf.Min(frames.Length - 1, (int)(tipAge * TipFps));
        if (frames.Length > 4 && f >= 4) f = 4 + (int)(tipAge * 6f) % Mathf.Max(1, frames.Length - 4);
        body.sprite = frames[f];

        if (!spilled && (tipAge * TipFps >= SpillFrame || frames.Length <= SpillFrame))
        {
            spilled = true;
            if (pan != null) pan.Spill(ground, kernels);
        }

        float lie = tipAge - (SpillFrame + 1) / TipFps;
        if (lie < LieTime) return;
        float fade = Mathf.Clamp01((lie - LieTime) / FadeTime);
        body.color = new Color(1f, 1f, 1f, 1f - fade);
        if (fade >= 1f) Destroy(gameObject);
    }
}
