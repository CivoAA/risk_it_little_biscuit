using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holt den in der Overworld gekauften Begleiter in den Run.
///
/// Die Shop-Stufe kommt aus <see cref="Shop.Companion"/>. Stufe 0 bedeutet
/// "nicht gekauft", dann passiert hier nichts.
/// </summary>
public class CompanionSpawner : MonoBehaviour
{
    [SerializeField] private GameObject companionPrefab;

    [Tooltip("Versatz zum Spieler beim Spawn.")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(-1.5f, 0f);

    [Header("Test")]
    [Tooltip("Ignoriert den Shop und spawnt den Begleiter immer - fuer die Test-Szene.")]
    [SerializeField] private bool forceSpawn = false;

    [Tooltip("Stufe, die bei forceSpawn benutzt wird.")]
    [SerializeField] private int forcedTier = 1;

    void Start()
    {
        int tier = forceSpawn ? forcedTier : ShopTier();
        if (tier <= 0) return;

        if (companionPrefab == null)
        {
            Debug.LogWarning("CompanionSpawner: kein companionPrefab gesetzt - Begleiter wird nicht gespawnt.");
            return;
        }

        Vector3 spawnPos = transform.position + (Vector3)spawnOffset;
        GameObject companion = Instantiate(companionPrefab, spawnPos, Quaternion.identity);

        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(companion, gameScene);
        }

        Companion script = companion.GetComponent<Companion>();
        if (script != null) script.SetTier(tier);
    }

    private int ShopTier() => Shop.GetInt(Shop.Companion);
}
