using UnityEngine;

public class SpawnExp : MonoBehaviour
{
    public static SpawnExp Instance;

    [Header("Prefabs je nach XP-Wert")]
    [SerializeField] private GameObject smallPrefab;  // Standard XP
    [SerializeField] private GameObject mediumPrefab; // ab 50+
    [SerializeField] private GameObject bigPrefab;    // ab 400+

    [Header("Doppelte XP (Skill LuckyXpChance)")]
    [Tooltip("Eigenes Prefab fuer den Glueckstreffer. Leer = normales Prefab, golden eingefaerbt.")]
    [SerializeField] private GameObject luckyPrefab;
    [SerializeField] private Color luckyTint = new Color(1f, 0.85f, 0.25f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <param name="lucky">Glueckstreffer: expAmount ist schon verdoppelt, hier geht es nur ums Aussehen.</param>
    public void SpawnEP(Vector2 pos, int expAmount, bool lucky = false)
    {
        // Prefab je nach expAmount auswählen
        GameObject prefabToSpawn = smallPrefab;

        if (expAmount >= ExpPickup.BigFrom)
            prefabToSpawn = bigPrefab;
        else if (expAmount >= ExpPickup.MediumFrom)
            prefabToSpawn = mediumPrefab;

        bool ownLuckyPrefab = lucky && luckyPrefab != null;
        if (ownLuckyPrefab) prefabToSpawn = luckyPrefab;

        // Aus dem Pool holen (landet dabei in der Lauf-Szene) - bei grossen
        // Haufen fallen Hunderte Bonbons kurz nacheinander.
        GameObject exp = RunPool.Spawn(prefabToSpawn, pos, transform.rotation, "Exp");
        if (exp == null) return;

        ExpPickup xp = exp.GetComponent<ExpPickup>();

        if (lucky)
        {
            // Eigenes goldenes Bonbon (Resources/PickUps) geht vor dem Einfaerben.
            bool ownLuckyLook = !ownLuckyPrefab && xp != null && xp.SetLook("candy_lucky");
            if (!ownLuckyPrefab && !ownLuckyLook)
            {
                foreach (SpriteRenderer sr in exp.GetComponentsInChildren<SpriteRenderer>())
                    sr.color = luckyTint;
            }
            DamageNumberController.Instance?.CreateText("x2", pos);
        }

        // XP-Wert setzen
        if (xp != null)
        {
            xp.xpValue = expAmount;
        }
    }

    /// <summary>
    /// Bonbon-Bilder laden und ein paar Bonbons anlegen, solange der Lauf noch
    /// laedt - sonst passiert beides beim ersten Kill mitten im Gefecht.
    /// </summary>
    void Start()
    {
        if (Instance != this) return;

        foreach (string look in new[] { "candy_small", "candy_medium", "candy_big", "candy_lucky" })
            PickUps.LoadFrames(look);

        RunPool.Prewarm(smallPrefab, 60, "Exp");
    }
}