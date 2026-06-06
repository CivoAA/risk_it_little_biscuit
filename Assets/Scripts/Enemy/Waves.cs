using System.Collections.Generic;
using UnityEngine;

public class Waves : MonoBehaviour
{
    public List<WaveStats> stats;

    [System.Serializable]
    public class WaveStats
    {
        public GameObject enemyPrefab;
        public float spawnTimer;
        public float spawnInterval;
        public int enemiesPerWave;
        public int spawnedEnemyCount;
    }

}
