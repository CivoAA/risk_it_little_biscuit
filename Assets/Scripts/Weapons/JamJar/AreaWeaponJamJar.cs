using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AreaWeaponJamJar : Weapon
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private GameObject JamJarprefab;
    private GameObject JamJar;
    private float spawnCounter;

    // Update is called once per frame
    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_ThrowingJamJar");
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = stats[weaponLevel].cooldown;

                Vector2 randomPoint = RandomSpawnPoint();
                JamJar = Instantiate(JamJarprefab, transform.position, transform.rotation, transform);
                // Coroutine starten
                StartCoroutine(MoveAndDestroy(JamJar, randomPoint));
            }
        }
    }

    IEnumerator MoveAndDestroy(GameObject jar, Vector2 targetPos)
    {
        float moveSpeed = 10f;
        float rotationZ = Mathf.Atan2(targetPos.y, targetPos.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, rotationZ);

        // Solange das Jar sich nicht am Ziel befindet
            while (Vector3.Distance(jar.transform.position, targetPos) > 0.001f)
            {
                jar.transform.position = Vector3.MoveTowards(jar.transform.position,targetPos,moveSpeed * Time.deltaTime);
                yield return null; // 1 Frame warten
            }

        // Am Ziel: Effekt spawnen + Jar zerstören
        GameObject JamJam = Instantiate(prefab, targetPos, rot);
        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(JamJam, gameScene);
        }
        Destroy(jar);
    }

    private Vector2 RandomSpawnPoint()
    {
        Vector2 spawnPoint;
        if (Random.Range(0f, 1f) > 0.5)
        {
            spawnPoint.x = Random.Range(transform.position.x - 5f, transform.position.x + 5f);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.y = transform.position.y;
            }
            else
            {
                spawnPoint.y = transform.position.y;
            }
        }
        else
        {
            spawnPoint.y = Random.Range(transform.position.y - 5f, transform.position.y + 5f);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.x = transform.position.x;
            }
            else
            {
                spawnPoint.x = transform.position.x;
            }
        }

        return spawnPoint;
    }
}
