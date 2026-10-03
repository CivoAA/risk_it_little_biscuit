using UnityEngine;
using System.Collections.Generic;

public class ExpPickup : MonoBehaviour
{
    [Header("Settings")]
    public int xpValue = 1;
    public float flySpeed = 3f;
    public float collectDistance = 0.5f;
    public float maxChaseTime = 5f;   // nach so vielen Sekunden auto-einsammeln

    [Tooltip("Bilder pro Sekunde der Idle-Schleife (Tools/bonbons.py).")]
    public float idleFps = 8f;

    /// <summary>Ab so viel XP wird das Bonbon mittel bzw. gross (gleich wie SpawnExp).</summary>
    public const int MediumFrom = 50, BigFrom = 300;

    private bool movingToPlayer = false;
    private Transform playerTransform;
    private float chaseTimer = 0f;

    private string look;
    private SpriteRenderer spriteRenderer;
    private Sprite[] idleFrames;
    private float idleTime;

    // Globale Liste aller Candies im Spiel
    private static readonly HashSet<ExpPickup> allCandies = new HashSet<ExpPickup>();
    private static bool attractTriggered = false;

    // Bonbons kommen aus dem RunPool und werden wiederverwendet: alles, was
    // ein Bonbon waehrend seines Lebens aendert, wird beim Aktivieren
    // zurueckgesetzt, und eingerichtet wird erst im ersten Update - dann hat
    // SpawnExp xpValue und Aussehen schon gesetzt.
    private bool initialized;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
    }

    private void OnEnable()
    {
        allCandies.Add(this);
        movingToPlayer = false;
        playerTransform = null;
        chaseTimer = 0f;
        initialized = false;
    }

    private void OnDisable()
    {
        allCandies.Remove(this);
        look = null;

        // Der Glueckstreffer faerbt golden ein - das gilt nur fuer dieses Leben.
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = baseColors[i];
    }

    /// <summary>
    /// Bildstreifen aus Resources/PickUps waehlen (candy_small/medium/big/lucky).
    /// Ohne Aufruf entscheidet der XP-Wert. Gibt false zurueck, wenn es den
    /// Streifen nicht gibt - dann bleibt das Prefab-Sprite.
    /// </summary>
    public bool SetLook(string key)
    {
        look = key;
        return PickUps.LoadFrames(key) != null;
    }

    public static string LookFor(int xp) =>
        xp >= BigFrom ? "candy_big" : xp >= MediumFrom ? "candy_medium" : "candy_small";

    private void Initialize()
    {
        initialized = true;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        idleFrames = PickUps.LoadFrames(look ?? LookFor(xpValue));
        if (idleFrames != null)
            idleTime = Random.Range(0, idleFrames.Length) / Mathf.Max(0.01f, idleFps);
        AnimateIdle();
    }

    private void AnimateIdle()
    {
        if (idleFrames == null || spriteRenderer == null) return;
        spriteRenderer.sprite = idleFrames[Mathf.FloorToInt(idleTime * idleFps) % idleFrames.Length];
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
        if (!initialized) Initialize();

        idleTime += Time.deltaTime;
        AnimateIdle();

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

        // Zurueck in den Pool (siehe SpawnExp) - aus dem Pool stammt fast jedes Bonbon.
        RunPool.Release(gameObject);
    }
}
