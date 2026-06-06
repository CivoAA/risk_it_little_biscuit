using UnityEngine;
using System.Collections;

public class SprintAttack : MonoBehaviour
{
    [Header("Sprint Settings")]
    public float chargeTime = 1.5f;          // wie lange das Ziel sich auflädt
    public float sprintSpeed = 25f;          // Sprintgeschwindigkeit
    public float stopDistance = 0.2f;        // wann stoppen
    public float randomOffsetRange = 2f;     // wie weit um den Spieler herum das Ziel leicht versetzt ist
    public GameObject chargeIndicatorPrefab; // visuelles Ziel- bzw. Aufladeprefab

    private Transform player;
    private Transform boss;
    private Vector3 targetPosition;
    private bool hasSprinted = false;

    private GameObject indicatorInstance;

    private void Start()
    {
        player = PlayerController.Instance.transform;
        boss = transform.parent; // da das Prefab als Child unter dem Boss gespawnt wird
        StartCoroutine(PrepareAndSprint());
    }

    private IEnumerator PrepareAndSprint()
    {
        // 1️⃣ Zielposition (leicht zufällig um den Spieler herum)
        Vector2 randomOffset = Random.insideUnitCircle * randomOffsetRange;
        targetPosition = player.position + new Vector3(randomOffset.x, randomOffset.y, 0);

        // 2️⃣ Ladeeffekt / Indikator anzeigen
        if (chargeIndicatorPrefab != null)
        {
            indicatorInstance = Instantiate(chargeIndicatorPrefab, targetPosition, Quaternion.identity);
        }

        // Warte, während sich die Zone „auflädt“
        yield return new WaitForSeconds(chargeTime);

        // Indikator entfernen
        if (indicatorInstance != null)
            Destroy(indicatorInstance);

        // 3️⃣ Boss sprintet mit hoher Geschwindigkeit zur Zielposition
        hasSprinted = true;
        Vector2 direction = (targetPosition - boss.position).normalized;
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = direction * sprintSpeed;
        }

        // 4️⃣ Warte, bis der Boss nahe am Ziel ist
        while (Vector2.Distance(boss.position, targetPosition) > stopDistance)
        {
            yield return null;
        }

        // 5️⃣ Stoppen und ggf. Schaden machen
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Option: Explosion oder AoE beim Aufprall
        Collider2D[] hits = Physics2D.OverlapCircleAll(boss.position, 2f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController.Instance.TakeDamage(50f);
            }
        }

        // kurze Pause fürs "Cooldown-Feeling"
        yield return new WaitForSeconds(0.3f);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPosition, 0.5f);
    }
}
