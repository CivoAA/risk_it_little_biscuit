using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using Unity.VisualScripting;
using TMPro;
using UnityEngine.SceneManagement;

public class EnemySpawner : MonoBehaviour
{
    /*[System.Serializable]
    public class Wave
    {
        public GameObject enemyPrefab;
        public float spawnTimer;
        public float spawnInterval;
        public int enemiesPerWave;
        public int spawnedEnemyCount;
    }*/

    //public List<Wave> waves;
    //public List<GameObject> Listwaves;
    public int waveNumber;
    public Transform minPos;
    public Transform maxPos;
    public int i = 2;
    public int maxwave = 12;
    [SerializeField] private TMP_Text WaveText;
    private int currentWaveRepeatCount = 0;
    private int lastDisplayedWave = int.MinValue;

    void Update()
    {
        // Text nur aktualisieren, wenn sich die Wave ändert (keine String-Allokation pro Frame)
        if (WaveText != null && lastDisplayedWave != i - 1)
        {
            lastDisplayedWave = i - 1;
            WaveText.text = "Wave: " + lastDisplayedWave;
        }

        if (PlayerController.Instance != null && PlayerController.Instance.gameObject.activeSelf)
        {
            if (i >= maxwave)
            {
                i = maxwave;
            }

            // Absicherung: Index darf die Anzahl der Kind-Objekte nicht überschreiten
            if (i < 0 || i >= transform.childCount)
            {
                return;
            }

            GameObject waves = transform.GetChild(i).gameObject;
            WaveConfig waveConfig = waves.GetComponent<WaveConfig>();
            if (waveConfig == null) return;
            if (waveConfig.stats == null || waveConfig.stats.Count == 0) return;

            if (waveNumber < 0 || waveNumber >= waveConfig.stats.Count)
            {
                waveNumber = 0; 
            }

            // Timer hochzählen
            waveConfig.stats[waveNumber].spawnTimer += Time.deltaTime;
            if (waveConfig.stats[waveNumber].spawnTimer >= waveConfig.stats[waveNumber].spawnInterval)
            {
                waveConfig.stats[waveNumber].spawnTimer = 0;
                SpawnEnemy(waveConfig);
            }

            if (waveConfig.stats[waveNumber].spawnedEnemyCount >= waveConfig.stats[waveNumber].enemiesPerWave)
            {
                // aktuelle Stat-Einheit abgeschlossen
                waveConfig.stats[waveNumber].spawnedEnemyCount = 0;

                // Intervall jedes Mal verkürzen
                waveConfig.stats[waveNumber].spawnInterval *= 0.85f;

                // zum nächsten Stat-Eintrag
                waveNumber++; // <<< NEU

                // Wenn wir das Ende der stats-Liste erreicht haben → eine Runde fertig
                if (waveNumber >= waveConfig.stats.Count) // <<< NEU
                {
                    waveNumber = 0; // <<< NEU
                    currentWaveRepeatCount++; // <<< NEU

                    // erst jetzt prüfen ob wir WaveAmount erreicht haben
                    if (currentWaveRepeatCount >= waveConfig.WaveAmount) // <<< NEU
                    {
                        i++;                          // nächste Welle // <<< NEU
                        currentWaveRepeatCount = 0;   // Zähler zurücksetzen // <<< NEU
                    }
                }
            }
        }
    }

    private void SpawnEnemy(WaveConfig waveConfig)
    {
        GameObject enemy = Instantiate(waveConfig.stats[waveNumber].enemyPrefab, RandomSpawnPoint(), transform.rotation);
        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(enemy, gameScene);
        }
        waveConfig.stats[waveNumber].spawnedEnemyCount++;
    }

    private Vector2 RandomSpawnPoint()
    {
        Vector2 spawnPoint;
        if (Random.Range(0f, 1f) > 0.5)
        {
            spawnPoint.x = Random.Range(minPos.position.x, maxPos.position.x);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.y = minPos.position.y;
            }
            else
            {
                spawnPoint.y = maxPos.position.y;
            }
        }
        else
        {
            spawnPoint.y = Random.Range(minPos.position.y, maxPos.position.y);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.x = minPos.position.x;
            }
            else
            {
                spawnPoint.x = maxPos.position.x;
            }
        }

        return spawnPoint;
    }
}
