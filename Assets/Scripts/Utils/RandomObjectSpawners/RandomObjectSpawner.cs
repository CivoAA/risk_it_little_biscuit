using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Objekte, die gespawnt werden können")]
    public GameObject[] spawnablePrefabs;

    [Header("Einstellungen")]
    public Transform player;
    public float blockSize = 20f;
    public float spawnAheadDistance = 100f;
    public float minY = -8f;
    public float maxY = 8f;
    public int maxObjectsPerBlock = 1;

    [Header("No-Spawn-Zone um Kamera")]
    public Transform leftNoSpawnPoint;
    public Transform rightNoSpawnPoint;

    private HashSet<int> generatedBlocks = new HashSet<int>();

    private void Update()
    {
        // Die Inspector-Referenz gilt, solange Spieler und Spawner in derselben
        // Szene liegen. Sobald die Maps in eigene Szenen wandern, faellt sie weg
        // (szenenuebergreifende Referenzen speichert Unity nicht) - dann kommt
        // der Spieler hier ueber sein Singleton.
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        if (player == null)
            return;

        int leftBlock = Mathf.FloorToInt((player.position.x - spawnAheadDistance) / blockSize);
        int rightBlock = Mathf.FloorToInt((player.position.x + spawnAheadDistance) / blockSize);

        for (int block = leftBlock; block <= rightBlock; block++)
        {
            if (!generatedBlocks.Contains(block))
            {
                GenerateBlock(block);
                generatedBlocks.Add(block);
            }
        }
    }

    private void GenerateBlock(int blockIndex)
    {
        if (spawnablePrefabs.Length == 0)
            return;

        float blockStartX = blockIndex * blockSize;
        float blockEndX = blockStartX + blockSize;

        int objectCount = Random.Range(0, maxObjectsPerBlock + 1);

        for (int i = 0; i < objectCount; i++)
        {
            float randomX = Random.Range(blockStartX, blockEndX);
            float randomY = Random.Range(minY, maxY);

            // 🔹 Überspringen, falls in der No-Spawn-Zone
            if (IsInNoSpawnZone(randomX))
                continue;

            GameObject prefab = spawnablePrefabs[Random.Range(0, spawnablePrefabs.Length)];
            GameObject spawnedObject = Instantiate(prefab, new Vector3(randomX, randomY, 0f), Quaternion.identity);

            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(spawnedObject, gameScene);
            }
            else
            {
                Debug.LogWarning("⚠️ Game Scene ist nicht geladen! Objekt bleibt in aktueller Scene.");
            }
        }
    }

    private bool IsInNoSpawnZone(float x)
    {
        if (leftNoSpawnPoint == null || rightNoSpawnPoint == null)
            return false;

        float left = Mathf.Min(leftNoSpawnPoint.position.x, rightNoSpawnPoint.position.x);
        float right = Mathf.Max(leftNoSpawnPoint.position.x, rightNoSpawnPoint.position.x);

        return x >= left && x <= right;
    }
}
