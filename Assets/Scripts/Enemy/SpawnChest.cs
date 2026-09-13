using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnChest : MonoBehaviour
{

    public static SpawnChest Instance;
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

    public void Spawn(Vector2 Pos)
    {
        GameObject LootChest = Instantiate(prefab, Pos, transform.rotation);
        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(LootChest, gameScene);
        }
    }
    
}
