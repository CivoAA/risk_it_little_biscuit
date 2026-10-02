using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MixerObject : MonoBehaviour
{
    public SpriteRenderer chargeSprite;
    public float shrinkSpeed = 1f;          // Wie schnell schrumpft der Kreis
    public float rechargeTime = 2f;         // Wie lange er braucht, um sich wieder aufzubauen
    public float normalSize = 5.1f;         // Originalgröße
    public float chargeTime = 5f;           // Zeit, bis er leer ist (optional, falls du Zeitbasierung willst)
    public PowerUpDatabase powerUpDatabase; // deine PowerUp-Liste
    public PowerUpButton[] powerUpButtons;  // deine 3 UI Buttons
    private bool playerInside = false;
    private bool actionTriggered = false;
    private Coroutine currentCoroutine;
    public Sprite idleSprite;
    private SpriteRenderer spriteRenderer;
    private Animator animator;


    [Tooltip("Aus = alter Mixer-Sprite mit gruenem Kreis statt MixerVisual.")]
    public bool useNewVisual = true;

    // Alle Mixer, die noch nicht benutzt wurden - fuer den Kartografen.
    private static readonly List<MixerObject> unused = new List<MixerObject>();
    public static IReadOnlyList<MixerObject> Unused => unused;

    // Alle Mixer, auch benutzte - die Spawner halten zu ihnen Abstand.
    private static readonly List<MixerObject> all = new List<MixerObject>();
    public static IReadOnlyList<MixerObject> All => all;

    private void Awake()
    {
        all.Add(this);
    }

    private void OnDestroy()
    {
        all.Remove(this);
    }

    private void OnEnable()
    {
        if (!actionTriggered && !unused.Contains(this)) unused.Add(this);
    }

    private void OnDisable()
    {
        unused.Remove(this);
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Neues Aussehen (Standmixer + Ring am Boden). Liest nur chargeSprite
        // und den Trigger ab - die Mechanik hier bleibt, wie sie ist.
        if (useNewVisual && GetComponent<MixerVisual>() == null)
            gameObject.AddComponent<MixerVisual>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox") && enabled)
        {
            playerInside = true;
            actionTriggered = false;

            // 🔹 Animation aktivieren
            if (animator != null)
            {
                animator.enabled = true;
            }

            if (currentCoroutine != null)
                StopCoroutine(currentCoroutine);

            currentCoroutine = StartCoroutine(ShrinkSprite());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (actionTriggered) return; // ✅ Mixer wurde schon ausgelöst – nichts mehr machen

        if (other.CompareTag("PlayerHitbox"))
        {
            playerInside = false;

            // 🔹 Animation deaktivieren → Sprite zurücksetzen
            if (animator != null)
            {
                animator.enabled = false;
                spriteRenderer.sprite = idleSprite;
            }

            if (currentCoroutine != null)
                StopCoroutine(currentCoroutine);

            currentCoroutine = StartCoroutine(RechargeSprite());
        }
    }

    private IEnumerator ShrinkSprite()
    {
        Vector3 startScale = chargeSprite.transform.localScale;

        while (playerInside && chargeSprite.transform.localScale.x > 0f)
        {
            shrinkSpeed = PlayerController.Instance.powerUpShrinkSpeed;
            float shrink = shrinkSpeed * Time.deltaTime;
            chargeSprite.transform.localScale -= new Vector3(shrink, shrink, 0f);

            // Wenn Sprite leer (0) → Aktion auslösen
            if (chargeSprite.transform.localScale.x <= 0f && !actionTriggered)
            {
                chargeSprite.transform.localScale = Vector3.zero;
                actionTriggered = true;
                mixerActive = true;
                unused.Remove(this);

                // Vor dem Oeffnen wuerfeln - die Mixer-Ansicht liest die
                // Ergebnisse fuer Titel und Hinweis.
                RollSpecials();

                UIController.Instance.PowerUpPanelOpen();
                AssignRandomPowerUps();

                // 🔹 Animation aktiv lassen
                if (animator != null)
                {
                    animator.enabled = true; // sicherstellen, dass sie weiterläuft
                }

                // 🔹 Alle "Beine-Zone"-Sprites deaktivieren (außer dem Mixer selbst)
                foreach (Transform child in transform)
                {
                    if (child != chargeSprite.transform)
                    {
                        var sr = child.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.enabled = false;
                    }
                }

                // 🔹 Collider deaktivieren, aber NICHT das Script selbst!
                GetComponent<Collider2D>().enabled = false;
                // Script aktiv lassen, damit Animation weiterläuft
                playerInside = false; // Sicherheitshalber

                break;
            }

            yield return null;
        }
    }


    private IEnumerator RechargeSprite()
    {
        // 🔹 Wenn der Mixer schon aktiv war → nie wieder aufladen
        if (actionTriggered)
            yield break;

        yield return new WaitForSeconds(0.75f); // ⏳ Warte 0,75 Sekunden bevor es wächst

        Vector3 startScale = chargeSprite.transform.localScale;
        Vector3 endScale = Vector3.one * normalSize;
        float elapsed = 0f;

        while (!playerInside && elapsed < rechargeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rechargeTime);

            chargeSprite.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        // Sicherstellen, dass es genau auf der Zielgröße endet
        chargeSprite.transform.localScale = endScale;
    }


    private bool mixerActive = false;
    
    private void AssignRandomPowerUps()
    {
        if (powerUpDatabase == null)
        powerUpDatabase = UIController.Instance.PowerUpDatabase;
        // 🔹 Falls Buttons nicht im Inspector gesetzt sind → aus UIController holen
        if (powerUpButtons == null || powerUpButtons.Length == 0)
            powerUpButtons = UIController.Instance.PowerUpButtons;

        if (powerUpDatabase == null || powerUpDatabase.powerUps.Count < powerUpButtons.Length)
        {
            Debug.LogWarning("Nicht genug PowerUps in der Datenbank oder Buttons fehlen!");
            return;
        }

        // Liste kopieren, um Manipulation zu vermeiden
        List<PowerUpEntry> available = new List<PowerUpEntry>(powerUpDatabase.powerUps);

        for (int i = 0; i < powerUpButtons.Length; i++)
        {
            if (available.Count == 0)
            {
                Debug.LogWarning("Nicht genug verschiedene PowerUps verfügbar!");
                break;
            }

            // Zufällig auswählen
            int randomIndex = Random.Range(0, available.Count);
            PowerUpEntry selected = available[randomIndex];

            // PowerUp zuweisen
            powerUpButtons[i].assignedPowerUp = selected;
            powerUpButtons[i].ActivateButton(AllLegendary ? PowerUpRarity.Legendary : (PowerUpRarity?)null);

            // Damit keine Duplikate vorkommen
            available.RemoveAt(randomIndex);
        }
    }

    // ------------------------------------------------------------------
    // Skilltree: Mixer-Knoten (Werte am Spieler in Prozent, 1 = 1 %)
    // ------------------------------------------------------------------

    /// <summary>Der gerade offene Mixer gibt beim Waehlen alle drei Stats.</summary>
    public static bool Jackpot { get; set; }

    /// <summary>Der gerade offene Mixer hat alle drei Stats legendaer gewuerfelt.</summary>
    public static bool AllLegendary { get; private set; }

    private static void RollSpecials()
    {
        PlayerController p = PlayerController.Instance;
        Jackpot      = p != null && Random.value * 100f < p.mixerAllThreeChance;
        AllLegendary = p != null && Random.value * 100f < p.mixerAllLegendaryChance;
    }

    /// <summary>True, wenn das Prefab ein Mixer ist - nur dann zaehlt "MoreMixers".</summary>
    public static bool IsMixer(GameObject prefab)
    {
        return prefab != null && prefab.GetComponentInChildren<MixerObject>(true) != null;
    }

    /// <summary>
    /// Skaliert die Anzahl, die ein Zufalls-Spawner fuer einen Block gewuerfelt
    /// hat, mit "MoreMixers". Der Nachkomma-Rest wird gewuerfelt - so stimmt der
    /// Durchschnitt genau (+25 % heisst im Schnitt ein Viertel mehr Mixer).
    /// </summary>
    public static int ScaleSpawnCount(int count)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || p.moreMixersPercent <= 0f || count <= 0) return count;

        float scaled = count * (1f + p.moreMixersPercent / 100f);
        int whole = Mathf.FloorToInt(scaled);
        return whole + (Random.value < scaled - whole ? 1 : 0);
    }


}
