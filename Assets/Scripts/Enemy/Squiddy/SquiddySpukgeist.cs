using UnityEngine;

/// <summary>
/// Ein kleiner, wuetender Zuckergeist aus dem Geisterreigen des Gespensts.
/// Fliegt geradeaus (mit leichtem Drall, so entstehen die Spiralarme), tut
/// bei Beruehrung weh und verpufft. Kein Gegner - Waffen gehen durch (es ist
/// ein Geist).
///
/// Bilder: squiddy_spukgeist (0-7 fliegen, Bild schaut nach rechts, 8-12 verpuffen).
/// </summary>
public class SquiddySpukgeist : MonoBehaviour
{
    private const float HitRadius = 0.36f;
    private const float Fps = 12f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private Vector2 pos;
    private Vector2 vel;
    private float spin;          // Grad pro Sekunde, um die Flugrichtung
    private float range, travelled;
    private float damage;
    private float t;
    private bool poof;
    private float poofT;

    public static SquiddySpukgeist Launch(Sprite[] strip, Vector2 from, Vector2 dir, float speed, float spinDeg,
                                          float maxRange, float hit)
    {
        SpriteRenderer sr = SquiddyFx.NewSprite("Spukgeist", from, 35);
        var g = sr.gameObject.AddComponent<SquiddySpukgeist>();
        g.sr = sr;
        g.frames = strip;
        g.pos = from;
        g.vel = dir.normalized * speed;
        g.spin = spinDeg;
        g.range = maxRange;
        g.damage = hit;
        g.t = Random.value;
        if (strip != null && strip.Length > 0) sr.sprite = strip[0];
        return g;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        if (poof)
        {
            poofT += dt;
            int i = 8 + Mathf.FloorToInt(poofT * 16f);
            if (frames == null || i >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[i];
            return;
        }

        vel = (Vector2)(Quaternion.Euler(0f, 0f, spin * dt) * vel);
        Vector2 step = vel * dt;
        pos += step;
        travelled += step.magnitude;
        transform.position = SquiddyFx.Snap(pos);
        if (frames != null && frames.Length >= 8) sr.sprite = frames[Mathf.FloorToInt(t * Fps) % 8];
        if (Mathf.Abs(vel.x) > 0.05f) sr.flipX = vel.x < 0f;

        // die letzten Meter blasser werden
        float fade = Mathf.Clamp01((range - travelled) / 1.5f);
        sr.color = new Color(1f, 1f, 1f, fade);
        if (travelled >= range)
        {
            Destroy(gameObject);
            return;
        }

        PlayerController player = PlayerController.Instance;
        if (player != null && player.gameObject.activeSelf &&
            ((Vector2)player.transform.position + Vector2.up * 0.35f - pos).sqrMagnitude <= HitRadius * HitRadius)
        {
            player.TakeDamage(damage);
            Poof();
        }
    }

    public void Poof()
    {
        if (poof) return;
        poof = true;
        sr.color = Color.white;
    }
}
