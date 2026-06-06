using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class EnemyKeckKönig : MonoBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Attacke")]
    [SerializeField] private float attackInterval = 5f;   // alle X Sekunden
    [SerializeField] private GameObject attackPrefab;     // wird direkt auf dem Boss gespawnt

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform player;

    private bool isAttacking = false;
    private float attackTimer;
    private GameObject activeAttackInstance;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        player = PlayerController.Instance.transform;
        attackTimer = attackInterval;
    }

    void FixedUpdate()
    {
        if (player == null) return;

        if (!isAttacking)
        {
            // immer Richtung Spieler laufen
            Vector2 dir = (player.position - transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;
            spriteRenderer.flipX = dir.x > 0;
        }
        else
        {
            // während Attacke stehen bleiben
            rb.linearVelocity = Vector2.zero;
        }
    }

    void Update()
    {
        if (player == null) return;

        // wenn gerade keine Attacke läuft, zähle Timer runter
        if (!isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                StartAttack();
                attackTimer = attackInterval;
            }
        }
        else
        {
            // sobald das Attack-Prefab zerstört wurde, Attacke beenden
            if (activeAttackInstance == null)
            {
                isAttacking = false;
            }
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        // Attack-Prefab direkt auf Boss-Position als Child spawnen
        activeAttackInstance = Instantiate(attackPrefab, transform.position, Quaternion.identity, transform);
    }
}
