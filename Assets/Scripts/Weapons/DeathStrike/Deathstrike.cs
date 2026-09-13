using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Deathstrike : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_Deathstrike");
        }
        if (weaponLevel < 0) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f)
        {
            spawnCounter = CurrentCooldown;
            StartCoroutine(SpawnDeathstrike());
        }
    }

    IEnumerator SpawnDeathstrike()
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots), 1, 20);

        Scene gameScene = SceneManager.GetSceneByName("Game");

        enemiesInRange.RemoveAll(e => e == null);
        if (enemiesInRange.Count == 0) yield break;

        List<Enemy> shuffledEnemies = new List<Enemy>(enemiesInRange);
        ShuffleList(shuffledEnemies);

        int hits = Mathf.Min(count, shuffledEnemies.Count);
        for (int i = 0; i < hits; i++)
        {
            Enemy target = shuffledEnemies[i];
            if (target == null) continue;

            Vector2 spawnPos = target.transform.position;

            GameObject ds = Instantiate(prefab, spawnPos, Quaternion.identity);

            if (gameScene.IsValid() && gameScene.isLoaded)
                SceneManager.MoveGameObjectToScene(ds, gameScene);

            var prefabScript = ds.GetComponent<DeathstrikePrefab>();
            if (prefabScript != null)
                prefabScript.weapon = this;

            yield return new WaitForSeconds(0.05f); // slight delay to spread spawns
        }
    }




    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Enemy")) return;

        Enemy e = col.GetComponent<Enemy>();
        if (e != null && !enemiesInRange.Contains(e))
            enemiesInRange.Add(e);
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (!col.CompareTag("Enemy")) return;

        Enemy e = col.GetComponent<Enemy>();
        if (e != null)
            enemiesInRange.Remove(e);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }

}
