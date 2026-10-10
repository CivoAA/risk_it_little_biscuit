using UnityEngine;

/// <summary>
/// Ein Nessel-Tentakel, der beim Nessel-Stern aus dem Boden bricht: erst
/// glimmt ein Riss (Bilder 0-2, die letzte Warnung), dann schiesst der
/// Tentakel hoch und peitscht unter Strom (3-6) - nur dann tut er weh -,
/// zieht sich zurueck (7-9) und der Riss erlischt (10-11).
///
/// Bilder: squiddy_nessel_tentakel (40x88, Pivot am Boden).
/// </summary>
public class SquiddyNessel : MonoBehaviour
{
    private const float Fps = 15f;
    private const int HurtFirst = 3, HurtLast = 6;
    private const float HitRadius = 0.6f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float t, delay, damage;
    private bool started, hit;

    public static SquiddyNessel Spawn(Sprite[] strip, Vector2 at, float afterSeconds, float hitDamage)
    {
        SpriteRenderer sr = SquiddyFx.NewSprite("Nessel", SquiddyFx.Snap(at), 4);
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        sr.enabled = false;
        var n = sr.gameObject.AddComponent<SquiddyNessel>();
        n.sr = sr;
        n.frames = strip;
        n.delay = afterSeconds;
        n.damage = hitDamage;
        return n;
    }

    private void Update()
    {
        if (!started)
        {
            delay -= Time.deltaTime;
            if (delay > 0f) return;
            started = true;
            sr.enabled = true;
        }
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t * Fps);
        if (frames == null || i >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        sr.sprite = frames[i];
        if (i == HurtFirst && !hit) SquiddySounds.Erupt();

        if (!hit && i >= HurtFirst && i <= HurtLast)
        {
            PlayerController player = PlayerController.Instance;
            if (player != null && player.gameObject.activeSelf &&
                ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude <= HitRadius * HitRadius)
            {
                player.TakeDamage(damage);
                hit = true;
            }
        }
    }
}
