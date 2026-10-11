using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ein einzelner Laserstrahl: blitzt auf, trifft alles unter sich genau einmal,
/// wird dann duenner und blendet aus. Er steht fest in der Welt.
/// </summary>
public class TimeLaserPrefab : MonoBehaviour
{
    public TimeLaser weapon;

    [Tooltip("So lange steht der Strahl voll da, bevor er ausblendet.")]
    [SerializeField] private float flashTime = 0.06f;
    [Tooltip("Dauer des Ausblendens.")]
    [SerializeField] private float fadeTime = 0.25f;
    [Tooltip("Dicke beim Aufblitzen, relativ zur normalen Dicke.")]
    [SerializeField] private float flashThickness = 1.4f;

    private static readonly List<Collider2D> hits = new List<Collider2D>();

    void Start()
    {
        if (weapon == null)
            weapon = WeaponFinder.Find<TimeLaser>("Time Laser");

        HitOnce();
        StartCoroutine(FlashAndFade());
    }

    // Schaden einmal per Abfrage statt ueber OnTriggerEnter: der Strahl ist
    // nur Sekundenbruchteile da, und so trifft er sicher jeden Gegner darunter.
    private void HitOnce()
    {
        Collider2D beam = GetComponent<Collider2D>();
        if (beam == null) return;

        if (weapon != null && weapon.IsActive)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.NoFilter();
            hits.Clear();
            beam.Overlap(filter, hits);

            HashSet<Enemy> damaged = new HashSet<Enemy>();
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Enemy")) continue;
                Enemy enemy = hit.GetComponent<Enemy>();
                if (enemy != null && damaged.Add(enemy))
                    enemy.TakeDamage(weapon.CurrentStats.damage, 0.1f);
            }
        }

        beam.enabled = false;
    }

    private IEnumerator FlashAndFade()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 baseScale = transform.localScale;
        Color baseColor = sr != null ? sr.color : Color.white;

        // Aufblitzen: kurz dicker, dann auf normale Dicke
        for (float t = 0f; t < flashTime; t += Time.deltaTime)
        {
            float k = Mathf.Lerp(flashThickness, 1f, t / flashTime);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z);
            yield return null;
        }

        // Ausblenden: duenner und durchsichtiger werden
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            float k = 1f - t / fadeTime;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z);
            if (sr != null) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
            yield return null;
        }

        Destroy(gameObject);
    }
}
