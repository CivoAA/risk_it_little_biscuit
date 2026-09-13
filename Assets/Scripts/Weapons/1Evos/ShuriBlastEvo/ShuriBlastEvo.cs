using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ShuriBlastEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    public Vector2 target;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel >= 0)
        {
            UpdateTarget();
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && !shooting)
            {
                // Achievment Unlocken
                AchievementManager.Instance.UnlockAchievement("Shuri_Blast_Evo");
                StartCoroutine(SpawnShuriken());
            }
        }
    }

    IEnumerator SpawnShuriken()
    {
        shooting = true;

        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            if (target == Vector2.zero) break;

            AudioController.Instance.PalySound(AudioController.Instance.BOBA);

            GameObject BOBA = Instantiate(prefab, transform.position, transform.rotation);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(BOBA, gameScene);
            }
            StartCoroutine(MoveAndDestroy(BOBA, target));

            yield return new WaitForSeconds(0.3f);
        }

        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator MoveAndDestroy(GameObject BOBA, Vector2 Target)
    {
        float moveSpeed = 10f;

        Vector2 shooterPos = transform.position;
        Vector2 enemyPos = Target;
        Vector2 direction = (enemyPos - shooterPos).normalized;
        Vector2 targetPos = enemyPos + direction * 10f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        BOBA.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

        while (BOBA != null && Vector3.Distance(BOBA.transform.position, targetPos) > 0.001f)
        {
            BOBA.transform.position = Vector3.MoveTowards(
                BOBA.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (BOBA != null)
        {
            Destroy(BOBA);
            target = Vector2.zero;
        }
    }

    private void UpdateTarget()
    {
        if (enemiesInRange == null || enemiesInRange.Count == 0)
            return;

        Enemy closestEnemy = null;
        float closestDistance = Mathf.Infinity;
        Vector2 myPos = transform.position;

        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy == null) continue;

            float distance = Vector2.Distance(myPos, enemy.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
        {
            target = closestEnemy.transform.position;
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Add(collider.GetComponent<Enemy>());
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Remove(collider.GetComponent<Enemy>());
        }
    }
}
