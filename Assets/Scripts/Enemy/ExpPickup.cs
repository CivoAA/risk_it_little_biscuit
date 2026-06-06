using UnityEngine;
using System.Collections.Generic;

public class ExpPickup : MonoBehaviour
{
    [Header("Settings")]
    public int xpValue = 1;
    public float flySpeed = 3f;
    public float collectDistance = 0.5f;
    public float maxChaseTime = 5f;   // nach so vielen Sekunden auto-einsammeln

    private bool movingToPlayer = false;
    private Transform playerTransform;
    private float chaseTimer = 0f;

    // Globale Liste aller Candies im Spiel
    private static readonly List<ExpPickup> allCandies = new List<ExpPickup>();
    private static bool attractTriggered = false;

    private void Awake()
    {
        allCandies.Add(this);
    }

    private void OnDestroy()
    {
        allCandies.Remove(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Spieler ist in Sammelreichweite → Candy fängt an zu folgen
        if (other.CompareTag("Player"))
        {
            StartChasingPlayer();
        }
    }

    private void Update()
    {
        var pc = PlayerController.Instance;

        // Magnet (attractAllXP) aktiviert → alle Candies fliegen los
        if (pc != null && pc.attractAllXP && !attractTriggered)
        {
            attractTriggered = true;
            AttractAllCandies();
        }
        else if (pc != null && !pc.attractAllXP && attractTriggered)
        {
            attractTriggered = false;
        }

        // Wenn Candy gerade folgt
        if (movingToPlayer && playerTransform != null)
        {
            chaseTimer += Time.deltaTime;

            // Richtung Spieler bewegen
            transform.position = Vector3.MoveTowards(
                transform.position,
                playerTransform.position,
                flySpeed * Time.deltaTime
            );

            // Wenn Spieler erreicht → einsammeln
            if (Vector3.Distance(transform.position, playerTransform.position) <= collectDistance)
            {
                CollectAndDestroy();
                return;
            }

            // Wenn zu lange unterwegs → automatisch einsammeln
            if (chaseTimer >= maxChaseTime)
            {
                CollectAndDestroy();
                return;
            }
        }
    }

    private void StartChasingPlayer()
    {
        if (movingToPlayer) return;

        movingToPlayer = true;
        chaseTimer = 0f;

        var pc = PlayerController.Instance;
        if (pc != null)
            playerTransform = pc.transform;
    }

    private static void AttractAllCandies()
    {
        foreach (var candy in allCandies)
        {
            if (candy != null && !candy.movingToPlayer)
            {
                candy.StartChasingPlayer();
            }
        }
    }

    private void CollectAndDestroy()
    {
        var pc = PlayerController.Instance;
        if (pc != null)
            pc.GetExperience(xpValue);

        Destroy(gameObject);
    }
}
