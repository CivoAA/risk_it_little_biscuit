using UnityEngine;
using System.Collections;

public class Spikefork : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;
    private Vector2 inputDir;
    private Vector2 moveDir;

    private int lastHorizontalDir = 1; // 1 = rechts, -1 = links
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_SpikeFork");
            UnlockManager.Instance.Unlock("unlock_spike_fork");
        }
        inputDir = PlayerController.Instance.playerMoveDirection;
        // Richtung merken
        if (inputDir != Vector2.zero)
        {
            moveDir = inputDir;

            if (inputDir.x > 0f)
                lastHorizontalDir = 1;
            else if (inputDir.x < 0f)
                lastHorizontalDir = -1;
        }

        // Waffe aktiv
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && !shooting)
            {
                shooting = true;
                StartCoroutine(SpawnFork());
            }
        }
    }

    IEnumerator SpawnFork()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            AudioController.Instance.PalySound(AudioController.Instance.ForkHit, 0.1f);

            // erster Fork
            GameObject fork = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            StartCoroutine(SpawnAndDestroy(fork, true));

            // zweiter Fork nur wenn AttackSpeed > 0
            if (stats[weaponLevel].AttackSpeed > 0)
            {
                GameObject fork2 = Instantiate(prefab, transform.position, Quaternion.identity, transform);
                StartCoroutine(SpawnAndDestroy(fork2, false));
            }

            yield return new WaitForSeconds(0.5f);
        }

        // Cooldown setzen
        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator SpawnAndDestroy(GameObject fork, bool rightSide)
    {
        float yOffset = Random.Range(0.35f, 0.55f);

        Vector3 pos = transform.position;
        pos.x += rightSide ? lastHorizontalDir * 0.4f : lastHorizontalDir * -0.4f; 
        pos.y += yOffset;
        fork.transform.position = pos;

        // Rotation abhängig von Seite + Richtung
        if (rightSide)
        {
            fork.transform.rotation = (lastHorizontalDir == 1)
                ? Quaternion.Euler(0f, 0f, -90f) // rechts
                : Quaternion.Euler(0f, 0f, 90f); // links
        }
        else
        {
            fork.transform.rotation = (lastHorizontalDir == -1)
                ? Quaternion.Euler(0f, 0f, -90f) // rechts gespiegelt
                : Quaternion.Euler(0f, 0f, 90f); // links gespiegelt
        }

        // kurze Zeit warten
        yield return new WaitForSeconds(0.5f);

        if (fork != null)
        {
            Destroy(fork);
        }
    }
}
