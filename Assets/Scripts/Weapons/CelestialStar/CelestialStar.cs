using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Schickt Sterne los, die wie das DVD-Logo ueber den Bildschirm prallen.
/// Die Bewegung selbst steckt in <see cref="BouncingStar"/> (auch fuer die Evo).
/// </summary>
public class CelestialStar : Weapon
{
    [SerializeField] private GameObject prefab;
    [Tooltip("Welteinheiten pro Sekunde. Das Bild ist ca. 10 x 5.6 Einheiten gross.")]
    [SerializeField] private float moveSpeed = 5.5f;
    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxCelestialStar);
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = CurrentCooldown;

                // Anzahl der Sterne aus AttackSpeed holen
                int spawnCount = Mathf.Max(1, Mathf.RoundToInt(stats[weaponLevel].AttackSpeed));
                StartCoroutine(SpawnMultipleStars(spawnCount));
            }
        }
    }

    private IEnumerator SpawnMultipleStars(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject star = Instantiate(prefab, transform.position, Quaternion.identity);
            star.transform.localScale *= PlayerController.Instance.AOERange;

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(star, gameScene);
            }

            BouncingStar.Launch(star, moveSpeed, CurrentDuration);

            // 1 Sekunde warten, bevor nächster Star gespawnt wird
            if (i < count - 1)
                yield return new WaitForSeconds(1f);
        }
    }
}
