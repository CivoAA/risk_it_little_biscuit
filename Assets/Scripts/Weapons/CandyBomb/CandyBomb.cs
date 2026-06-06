using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CandyBomb : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_CandyBomb");
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = 999f;
                StartCoroutine(SpawnBomb());
            }
        }
    }

    private IEnumerator SpawnBomb()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            // Bombe erzeugen
            GameObject candyBomb = Instantiate(prefab, transform.position, transform.rotation);

            // 🔹 Größe skalieren basierend auf Waffen-Range + Spieler-AOE
            float totalScale = stats[weaponLevel].range + PlayerController.Instance.AOERange;
            candyBomb.transform.localScale = Vector3.one * totalScale;

            // 🔹 In "Game"-Szene verschieben (falls nötig)
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(candyBomb, gameScene);
            }

            // 🔹 0.25 Sekunden warten, bevor die nächste Bombe gespawnt wird
            yield return new WaitForSeconds(0.8f);
        }

        spawnCounter = stats[weaponLevel].cooldown;
    }
}
