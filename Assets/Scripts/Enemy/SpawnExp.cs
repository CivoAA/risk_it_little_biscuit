using UnityEngine;
using UnityEngine.SceneManagement;

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

        if (expAmount >= 300)
            prefabToSpawn = bigPrefab;
        else if (expAmount >= 50)
            prefabToSpawn = mediumPrefab;

        bool ownLuckyPrefab = lucky && luckyPrefab != null;
        if (ownLuckyPrefab) prefabToSpawn = luckyPrefab;

        // Prefab instanziieren
        GameObject exp = Instantiate(prefabToSpawn, pos, transform.rotation);

        if (lucky)
        {
            if (!ownLuckyPrefab)
            {
                foreach (SpriteRenderer sr in exp.GetComponentsInChildren<SpriteRenderer>())
                    sr.color = luckyTint;
            }
            DamageNumberController.Instance?.CreateText("x2", pos);
        }

        // XP-Wert setzen
        ExpPickup xp = exp.GetComponent<ExpPickup>();
        if (xp != null)
        {
            xp.xpValue = expAmount;
        }

        // optional in die Lauf-Szene verschieben
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(exp, gameScene);
        }
    }
}