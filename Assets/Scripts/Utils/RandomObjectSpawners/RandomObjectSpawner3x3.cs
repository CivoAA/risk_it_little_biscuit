using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RandomObjectSpawner3x3 : MonoBehaviour
{
    [Header("Objekte, die gespawnt werden können")]
    public GameObject[] spawnablePrefabs;

    [Header("Einstellungen")]
    public Transform player;
    public float blockSize = 20f;
    public int spawnRadiusInBlocks = 5;
    public int maxObjectsPerBlock = 1;
    [Tooltip("Luecke zwischen zwei Mixer-Feldern, in Feld-Durchmessern (1 = ein ganzes Feld dazwischen).")]
    public float mixerGap = 1f;

    [Header("No-Spawn-Zone um Kamera")]
    [Tooltip("Linke untere Ecke der No-Spawn-Zone (z. B. Kamerarand unten links)")]
    public Transform bottomLeftNoSpawnPoint;
    [Tooltip("Rechte obere Ecke der No-Spawn-Zone (z. B. Kamerarand oben rechts)")]
    public Transform topRightNoSpawnPoint;

    private HashSet<Vector2Int> generatedBlocks = new HashSet<Vector2Int>();
    private GameObject[] mixerPrefabs;

    private void Update()
    {
        // Die Inspector-Referenz gilt, solange Spieler und Spawner in derselben
        // Szene liegen. Sobald die Maps in eigene Szenen wandern, faellt sie weg
        // (szenenuebergreifende Referenzen speichert Unity nicht) - dann kommt
        // der Spieler hier ueber sein Singleton.
        if (player == null && PlayerController.Instance != null)
            player = PlayerController.Instance.transform;

        if (player == null) return;

        int playerBlockX = Mathf.FloorToInt(player.position.x / blockSize);
        int playerBlockY = Mathf.FloorToInt(player.position.y / blockSize);

        for (int x = playerBlockX - spawnRadiusInBlocks; x <= playerBlockX + spawnRadiusInBlocks; x++)
        {
            for (int y = playerBlockY - spawnRadiusInBlocks; y <= playerBlockY + spawnRadiusInBlocks; y++)
            {
                Vector2Int block = new Vector2Int(x, y);
                if (!generatedBlocks.Contains(block))
                {
                    GenerateBlock(block);
                    generatedBlocks.Add(block);
                }
            }
        }
    }

    private void GenerateBlock(Vector2Int block)
    {
        if (spawnablePrefabs.Length == 0) return;

        float blockStartX = block.x * blockSize;
        float blockEndX = blockStartX + blockSize;
        float blockStartY = block.y * blockSize;
        float blockEndY = blockStartY + blockSize;

        int baseCount = Random.Range(0, maxObjectsPerBlock + 1);

        // Skilltree "MoreMixers": zusaetzliche Objekte sind immer Mixer.
        if (mixerPrefabs == null) mixerPrefabs = System.Array.FindAll(spawnablePrefabs, MixerObject.IsMixer);
        int objectCount = mixerPrefabs.Length > 0 ? MixerObject.ScaleSpawnCount(baseCount) : baseCount;

        for (int i = 0; i < objectCount; i++)
        {
            GameObject prefab = i < baseCount
                ? spawnablePrefabs[Random.Range(0, spawnablePrefabs.Length)]
                : mixerPrefabs[Random.Range(0, mixerPrefabs.Length)];

            // Mixer wuerfeln neu, bis ihr Feld frei steht - normale Objekte
            // haben wie bisher einen Versuch.
            bool isMixer = System.Array.IndexOf(mixerPrefabs, prefab) >= 0;
            int attempts = isMixer ? MixerPlacement.Attempts : 1;
            bool found = false;
            float randomX = 0f, randomY = 0f;

            for (int a = 0; a < attempts && !found; a++)
            {
                randomX = Random.Range(blockStartX, blockEndX);
                randomY = Random.Range(blockStartY, blockEndY);

                // 🔹 Überspringen, falls in der No-Spawn-Zone
                if (IsInNoSpawnZone(randomX, randomY))
                    continue;

                found = !isMixer || MixerPlacement.IsFree(prefab, new Vector2(randomX, randomY), mixerGap);
            }

            if (!found)
                continue;

            GameObject spawnedObject = Instantiate(prefab, new Vector3(randomX, randomY, 0f), Quaternion.identity);

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                RunScene.Place(spawnedObject, isMixer ? "Mixer" : "Map-Objekte");
            }
            else
            {
                Debug.LogWarning("⚠️ Keine Lauf-Szene gefunden! Objekt bleibt in aktueller Scene.");
            }
        }
    }

    private bool IsInNoSpawnZone(float x, float y)
    {
        if (bottomLeftNoSpawnPoint != null && topRightNoSpawnPoint != null)
        {
            float left = bottomLeftNoSpawnPoint.position.x;
            float right = topRightNoSpawnPoint.position.x;
            float bottom = bottomLeftNoSpawnPoint.position.y;
            float top = topRightNoSpawnPoint.position.y;

            return (x >= left && x <= right && y >= bottom && y <= top);
        }

        // Die beiden Ecken hingen am Kamerarand und stehen seit dem
        // Szenen-Split in GameCore - die Referenz von hier aus ist damit leer.
        // Ohne Ersatz waere die Antwort "nein, spawn ruhig", und dann poppen
        // Props mitten im Bild neben dem Spieler auf.
        if (ViewBounds.TryGetWorldRect(out Rect view))
        {
            return view.Contains(new Vector2(x, y));
        }

        return false;
    }
}
