using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnDeath : MonoBehaviour
{
    public static SpawnDeath Instance;
    [SerializeField] private GameObject prefab;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Spawn(Vector2 pos)
    {
        // Neue Position berechnen
        Vector2 spawnPos = pos + new Vector2(100f, 100f);

        // Prefab instantiieren
        GameObject death = Instantiate(prefab, spawnPos, Quaternion.identity);

        // In die Lauf-Szene verschieben, falls sie geladen ist
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(death, gameScene);
        }
    }
}
