using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnExp : MonoBehaviour
{
    public static SpawnExp Instance;

    [Header("Prefabs je nach XP-Wert")]
    [SerializeField] private GameObject smallPrefab;  // Standard XP
    [SerializeField] private GameObject mediumPrefab; // ab 50+
    [SerializeField] private GameObject bigPrefab;    // ab 400+

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnEP(Vector2 pos, int expAmount)
    {
        // Prefab je nach expAmount auswählen
        GameObject prefabToSpawn = smallPrefab;

        if (expAmount >= 300)
            prefabToSpawn = bigPrefab;
        else if (expAmount >= 50)
            prefabToSpawn = mediumPrefab;

        // Prefab instanziieren
        GameObject exp = Instantiate(prefabToSpawn, pos, transform.rotation);

        // XP-Wert setzen
        ExpPickup xp = exp.GetComponent<ExpPickup>();
        if (xp != null)
        {
            xp.xpValue = expAmount;
        }

        // optional ins "Game"-Scene verschieben
        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(exp, gameScene);
        }
    }
}