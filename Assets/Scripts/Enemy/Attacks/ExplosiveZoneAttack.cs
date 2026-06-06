using System.Collections;
using UnityEngine;

public class ExplosiveZoneAttack : MonoBehaviour
{
    [Header("Damage Settings")]
    public float growSpeed = 1.5f;
    public float maxScale = 3f;
    public float damage = 80f;
    public float explosionDelay = 1.2f;

    private bool hasExploded = false;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;

    private void Start()
    {
        player = PlayerController.Instance.transform;
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.enabled = false; // erst am Ende aktiv

        StartCoroutine(GrowAndExplode());
    }

    private IEnumerator GrowAndExplode()
    {
        transform.localScale = Vector3.zero;

        // Wachsen bis Zielgröße erreicht
        while (transform.localScale.x < maxScale)
        {
            transform.localScale += Vector3.one * (Time.deltaTime * growSpeed);
            yield return null;
        }

        // kurze Wartezeit für visuelle "Explosion"
        yield return new WaitForSeconds(explosionDelay);

        // Explosion & Schaden
        if (!hasExploded)
        {
            hasExploded = true;
            circleCollider.enabled = true; // optional, falls du Partikel oder Sound triggert willst

            float distance = Vector2.Distance(player.position, transform.position);
            float radius = maxScale * 0.5f; // einfacher Radius-Check
            if (distance <= radius)
            {
                PlayerController.Instance.TakeDamage(damage);
            }

            // Optional: kurzes Aufblitzen für Feedback
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            yield return new WaitForSeconds(0.15f);

            // Destroy Zone
            Destroy(gameObject);
        }
    }
}
