using UnityEngine;
using System.Collections;

public class BloodyFork : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        // Waffe aktiv
        if (weaponLevel >= 0)
        {
            // Achievment Unlocken
            AchievementManager.Instance.UnlockAchievement("Bloody_Fork_Evo");
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && !shooting)
            {
                shooting = true;
                StartCoroutine(SpawnForks());
            }
        }
    }

    IEnumerator SpawnForks()
    {
        // Anzahl der "Wellen" aus shots berechnen
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            AudioController.Instance.PalySound(AudioController.Instance.ForkHit, 0.1f);

            // alle 8 Gabeln gleichzeitig spawnen
            SpawnAllForks();

            yield return new WaitForSeconds(0.5f);
        }

        // Cooldown erst NACH den Schüssen starten
        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    private void SpawnAllForks()
    {
        // Offsets und Winkel für gerade Richtungen
        Vector3[] straightOffsets = {
            new Vector3( 0.4f, 0f, 0f),   // rechts
            new Vector3(-0.4f, 0f, 0f),   // links
            new Vector3( 0f, -0.4f, 0f),  // unten
            new Vector3( 0f,  0.4f, 0f),  // oben
        };
        float[] straightAngles = { -90f, 90f, 180f, 0f };

        // Offsets und Winkel für diagonale Richtungen
        Vector3[] diagOffsets = {
            new Vector3( 0.3f,  0.3f, 0f),  // oben rechts
            new Vector3( 0.3f, -0.3f, 0f),  // unten rechts
            new Vector3(-0.3f, -0.3f, 0f),  // unten links
            new Vector3(-0.3f,  0.3f, 0f),  // oben links
        };
        float[] diagAngles = { -45f, -135f, 135f, 45f };

        // gerade + diagonale in einer Welle spawnen
        for (int i = 0; i < straightOffsets.Length; i++)
        {
            SpawnFork(prefab, straightOffsets[i], straightAngles[i]);
        }

        for (int i = 0; i < diagOffsets.Length; i++)
        {
            SpawnFork(prefab, diagOffsets[i], diagAngles[i]);
        }
    }

    private void SpawnFork(GameObject prefab, Vector3 offset, float angle)
    {
        Vector3 pos = transform.position + offset;
        GameObject fork = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, angle), transform);

        // 🔹 Fork-Größe dynamisch skalieren mit AOERange
        float scaleFactor = PlayerController.Instance.AOERange;
        fork.transform.localScale *= scaleFactor;

        StartCoroutine(DestroyAfterDelay(fork, 0.5f));
    }

    private IEnumerator DestroyAfterDelay(GameObject fork, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fork != null)
        {
            Destroy(fork);
        }
    }
}
