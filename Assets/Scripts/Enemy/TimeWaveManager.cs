using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class TimeWaveManager : MonoBehaviour
{
    [System.Serializable]
    public class WaveEvent
    {
        public float triggerTime;
        public GameObject enemyPrefab;
        public int spawnCount = 1;
        public float spawnInterval = 0.5f;
    }

    [Header("Spawnbereich")]
    public Transform minSpawnPos;
    public Transform maxSpawnPos;

    [Header("Wave 1 Gegner")]
    public GameObject marshmello;
    public GameObject eliteMarshmello;
    public GameObject mausMitMesser;
    public GameObject evilSlime;

    [Header("Wave 2 Gegner")]
    public GameObject saureMilch;
    public GameObject miniMilch;
    public GameObject muffin;

    [Header("Wave 3 Gegner")]
    public GameObject suppe;
    public GameObject pancake;
    public GameObject fetti;

    [Header("Wave 4 Slime Varianten (10 Stück)")]
    public GameObject[] slimeVariants;

    [Header("MiniBosse")]
    public GameObject messerMaus1;
    public GameObject messerMaus2;
    public GameObject miniBoss_marshmello;
    public GameObject blocker;

    [Header("Endboss")]
    public GameObject keksKoenig;

    private float timer = 0f;
    private int currentWaveIndex = 0;
    private WaveEvent[] waveEvents;
    private bool timeLaserUnlocked = false;
    private int lastTimerSecond = -1;

    void Start()
    {
        // Events per Code generieren
        waveEvents = WaveEventData.GetWaveEvents(
            marshmello,
            eliteMarshmello,
            evilSlime,
            mausMitMesser,
            saureMilch,
            miniMilch,
            muffin,
            suppe,
            pancake,
            fetti,
            slimeVariants,
            messerMaus1,
            messerMaus2,
            keksKoenig,
            miniBoss_marshmello,
            blocker
        );
    }

    void Update()
    {
        if (!PlayerController.Instance.gameObject.activeSelf)
            return;

        timer += Time.deltaTime;

        // Timer-Text nur aktualisieren, wenn sich die angezeigte Sekunde ändert
        int currentSecond = Mathf.FloorToInt(timer);
        if (currentSecond != lastTimerSecond)
        {
            lastTimerSecond = currentSecond;
            UIController.Instance.UpdateTimer(timer);
        }

        while (currentWaveIndex < waveEvents.Length && timer >= waveEvents[currentWaveIndex].triggerTime)
        {
            StartCoroutine(SpawnWave(waveEvents[currentWaveIndex]));
            currentWaveIndex++;
        }
        // ⚠️ Bugfix: vorher "timer == 666" – ein Float trifft den Wert nie exakt,
        // dadurch wurde das Unlock nie ausgelöst.
        if (!timeLaserUnlocked && timer >= 666f)
        {
            timeLaserUnlocked = true;
            Unlocks.Grant(Unlocks.TimeLaser);
        }
    }


    private IEnumerator SpawnWave(WaveEvent wave)
    {
        for (int i = 0; i < wave.spawnCount; i++)
        {
            GameObject enemy = Instantiate(wave.enemyPrefab, GetRandomSpawnPosition(), Quaternion.identity);

            // Verschiebe Gegner in "Game"-Szene (wie im alten Spawner)
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(enemy, gameScene);
            }

            if (wave.enemyPrefab == messerMaus1 || wave.enemyPrefab == messerMaus2) 
            {
                SpawnEnemyRing(blocker, 120, 15f); // z. B. 8 Marshmellos um den Spieler
            }

            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }


    private Vector2 GetRandomSpawnPosition()
    {
        Vector2 spawnPoint;

        if (Random.Range(0f, 1f) > 0.5f)
        {
            // Horizontaler Rand
            spawnPoint.x = Random.Range(minSpawnPos.position.x, maxSpawnPos.position.x);
            spawnPoint.y = Random.value > 0.5f ? minSpawnPos.position.y : maxSpawnPos.position.y;
        }
        else
        {
            // Vertikaler Rand
            spawnPoint.y = Random.Range(minSpawnPos.position.y, maxSpawnPos.position.y);
            spawnPoint.x = Random.value > 0.5f ? minSpawnPos.position.x : maxSpawnPos.position.x;
        }

        return spawnPoint;
    }

    private void SpawnEnemyRing(GameObject enemyPrefab, int count, float radius)
    {
        Vector2 playerPos = PlayerController.Instance.transform.position;

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector2 spawnPos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            // In Game-Szene verschieben (wie normal)
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(enemy, gameScene);
            }
        }
    }

}
