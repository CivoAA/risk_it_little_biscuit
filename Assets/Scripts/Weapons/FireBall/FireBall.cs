using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class FireBall : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    public Vector2 target;

    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_FireBall");
            UnlockManager.Instance.Unlock("unlock_celestial_star");
        }
        if (weaponLevel < 0) return;

        UpdateTarget();

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0)
        {
            spawnCounter = stats[weaponLevel].cooldown;
            StartCoroutine(SpawnFireBalls());
        }
    }

    private IEnumerator SpawnFireBalls()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            if (target == Vector2.zero) yield break;

            AudioController.Instance.PalySound(AudioController.Instance.BOBA);

            GameObject fireBall = Instantiate(prefab, transform.position, transform.rotation);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(fireBall, gameScene);
            }

            StartCoroutine(MoveAndDestroy(fireBall, target));

            // Abstand zwischen Schüssen
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator MoveAndDestroy(GameObject fireBall, Vector2 Target)
    {
        float moveSpeed = 10f;

        Vector2 shooterPos = transform.position;
        Vector2 enemyPos = Target;
        Vector2 direction = (enemyPos - shooterPos).normalized;
        Vector2 targetPos = enemyPos + direction * 10f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        fireBall.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

        while (fireBall != null && Vector3.Distance(fireBall.transform.position, targetPos) > 0.001f)
        {
            fireBall.transform.position = Vector3.MoveTowards(
                fireBall.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (fireBall != null)
        {
            Destroy(fireBall);
        }
    }

    private void UpdateTarget()
    {
        enemiesInRange.RemoveAll(e => e == null);

        if (enemiesInRange.Count == 0)
        {
            target = Vector2.zero;
            return;
        }

        Enemy closestEnemy = null;
        float closestDistance = Mathf.Infinity;
        Vector2 myPos = transform.position;

        foreach (Enemy enemy in enemiesInRange)
        {
            float dist = Vector2.Distance(myPos, enemy.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
            target = closestEnemy.transform.position;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy e = collider.GetComponent<Enemy>();
            if (e != null && !enemiesInRange.Contains(e))
                enemiesInRange.Add(e);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy e = collider.GetComponent<Enemy>();
            if (e != null)
                enemiesInRange.Remove(e);
        }
    }
}
