using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

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

    // Sterne, die gerade fliegen. Wird die Waffe durch die Evo ersetzt,
    // verschwinden sie sofort, statt neben den Explosivsternen weiterzufliegen.
    private readonly List<GameObject> liveStars = new List<GameObject>();

    void Update()
    {
        if (!IsActive)
        {
            ClearStars();
            return;
        }

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
            WeaponSounds.Play(WeaponSounds.StarLaunch);
            star.transform.localScale *= PlayerController.Instance.AOERange;

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(star, gameScene);
            }

            BouncingStar.Launch(star, moveSpeed, CurrentDuration);
            liveStars.RemoveAll(s => s == null);
            liveStars.Add(star);

            // 1 Sekunde warten, bevor nächster Star gespawnt wird
            if (i < count - 1)
                yield return new WaitForSeconds(1f);
            if (!IsActive) yield break;
        }
    }

    private void ClearStars()
    {
        if (liveStars.Count == 0) return;
        StopAllCoroutines();
        foreach (GameObject star in liveStars)
            if (star != null) Destroy(star);
        liveStars.Clear();
    }
}
