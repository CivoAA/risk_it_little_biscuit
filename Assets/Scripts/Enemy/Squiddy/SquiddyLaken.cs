using UnityEngine;

/// <summary>
/// Das Laken, das Squiddy bei der Enthuellung wegschleudert: fliegt in einem
/// Bogen davon, flattert, segelt herab, sackt am Boden zusammen und bleibt
/// mit seinem leeren Gesicht liegen - bis der Kampf vorbei ist (dann blendet
/// es aus). Reine Deko.
///
/// Bilder: squiddy_laken_flug (0-3 entfalten, 4-11 flattern),
///         squiddy_laken_boden (0-6 sacken, 7 liegt).
/// </summary>
public class SquiddyLaken : MonoBehaviour
{
    private const float Gravity = 9f;
    private const float Drag = 1.6f;          // Luftwiderstand: das Tuch segelt
    private const float MaxFall = 1.6f;       // so schnell sinkt es hoechstens
    private const float LieTime = 25f;
    /// <summary>Hoehe der Bildmitte, bei der der Saum den Boden beruehrt (Flugbild ~74 px hoch).</summary>
    private const float LandHeight = 1.15f;

    private SpriteRenderer sr;
    private Sprite[] fly, land;
    private Vector2 ground;                   // Punkt auf dem Boden unter dem Tuch
    private float height;                     // Hoehe der Bildmitte darueber (Einheiten)
    private Vector2 vGround;
    private float vHeight;
    private float t;
    private int state;                        // 0 fliegt, 1 landet, 2 liegt, 3 weg
    private float stateT;

    public static SquiddyLaken Throw(Sprite[] flyFrames, Sprite[] landFrames, Vector2 groundBelow, float startHeight,
                                     Vector2 velocity, float upVelocity)
    {
        SpriteRenderer sr = SquiddyFx.NewSprite("Laken", groundBelow, 50);
        var l = sr.gameObject.AddComponent<SquiddyLaken>();
        l.sr = sr;
        l.fly = flyFrames;
        l.land = landFrames;
        l.ground = groundBelow;
        l.height = startHeight;
        l.vGround = velocity;
        l.vHeight = upVelocity;
        sr.sprite = flyFrames != null && flyFrames.Length > 0 ? flyFrames[0] : null;
        return l;
    }

    /// <summary>Kampf vorbei: sanft ausblenden.</summary>
    public void FadeAway()
    {
        if (state < 3)
        {
            state = 3;
            stateT = 0f;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        stateT += dt;
        switch (state)
        {
            case 0:
                vHeight -= Gravity * dt;
                vHeight = Mathf.Max(vHeight, -MaxFall);
                vGround *= Mathf.Exp(-Drag * dt);
                // Pendeln beim Herabsegeln
                Vector2 sway = new Vector2(Mathf.Sin(t * 3.1f) * 0.9f, 0f) * Mathf.Clamp01(t - 0.5f);
                ground += (vGround + sway) * dt;
                height += vHeight * dt;
                int f = t < 4f / 14f ? Mathf.FloorToInt(t * 14f) : 4 + Mathf.FloorToInt((t - 4f / 14f) * 12f) % 8;
                if (fly != null && fly.Length > 0) sr.sprite = fly[Mathf.Min(f, fly.Length - 1)];
                transform.position = SquiddyFx.Snap(ground + Vector2.up * height);   // Bild-Mitte
                sr.sortingOrder = 50;
                if (height <= LandHeight && vHeight < 0f)
                {
                    state = 1;
                    stateT = 0f;
                    sr.sortingOrder = -2;
                    SquiddySounds.Blub();
                }
                break;
            case 1:
            case 2:
                int k = Mathf.Min(Mathf.FloorToInt(stateT * 12f), land.Length - 1);
                if (land != null && land.Length > 0) sr.sprite = land[k];
                transform.position = SquiddyFx.Snap(ground);
                if (state == 1 && k == land.Length - 1) state = 2;
                if (stateT > LieTime) FadeAway();
                break;
            case 3:
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - stateT / 1.2f));
                if (stateT >= 1.2f) Destroy(gameObject);
                break;
        }
    }
}
