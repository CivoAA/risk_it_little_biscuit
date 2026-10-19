using UnityEngine;

/// <summary>Ein fliegender Saucentropfen, der als Fleck am Boden liegen bleiben kann.</summary>
public class SauceDrop : MonoBehaviour
{
    private Vector2 from;
    private Vector2 to;
    private float time;
    private float t;
    private bool mustard;
    private float splatLife;
    private float splatAge = -1f;
    private SpriteRenderer sr;

    public static void Fly(Vector2 from, Vector2 to, float time, bool mustard, float splatLife)
    {
        GameObject go = SweetFx.NewObject("SauceDrop", from);
        SauceDrop d = go.AddComponent<SauceDrop>();
        d.from = from;
        d.to = to;
        d.time = Mathf.Max(0.05f, time);
        d.mustard = mustard;
        d.splatLife = splatLife;
        d.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(d.sr, 2);
        d.sr.sprite = SauceBottle.Drop(mustard)[0];
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (splatAge >= 0f)
        {
            splatAge += dt;
            Sprite[] frames = SauceBottle.Splat(mustard);
            sr.sprite = frames[Mathf.Min(frames.Length - 1, (int)(splatAge * 14f))];
            float fade = Mathf.Clamp01((splatAge - splatLife * 0.6f) / (splatLife * 0.4f));
            sr.color = new Color(1f, 1f, 1f, 1f - fade);
            if (splatAge >= splatLife) Destroy(gameObject);
            return;
        }

        t += dt / time;
        float u = Mathf.Clamp01(t);
        // kleiner Bogen, damit es nach Spritzen und nicht nach Laser aussieht
        Vector2 p = Vector2.Lerp(from, to, u) + new Vector2(0f, 0.35f * 4f * u * (1f - u));
        transform.position = SweetFx.Snap(p);
        Sprite[] drops = SauceBottle.Drop(mustard);
        sr.sprite = drops[u > 0.6f && drops.Length > 1 ? 1 : 0];
        if (u < 1f) return;

        if (splatLife <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        splatAge = 0f;
        // Flecken liegen unter allem
        sr.sortingOrder -= 3;
    }
}
