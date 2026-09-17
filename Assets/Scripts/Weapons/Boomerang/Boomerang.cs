using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class Boomerang : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    public Vector2 target;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxBoomerang);
        }
        if (weaponLevel >= 0)
        {
            UpdateTarget();
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && !shooting && target != Vector2.zero)
            {
                shooting = true;
                StartCoroutine(SpawnBoomerang());
            }
        }
    }

    IEnumerator SpawnBoomerang()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            AudioController.Instance.PalySound(AudioController.Instance.Werfen);

            GameObject boomerang = Instantiate(prefab, transform.position, transform.rotation);
            boomerang.transform.localScale *= (PlayerController.Instance.AOERange * 0.7f);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(boomerang, gameScene);
            }

            StartCoroutine(MoveBoomerang(boomerang, target));

            yield return new WaitForSeconds(0.2f);
        }

        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator MoveBoomerang(GameObject boomerang, Vector2 targetPos)
    {
        float moveSpeed = 12f;
        float extraForwardDistance = 5f; // vorher 10f → jetzt halbe Strecke
        float extraBehindPlayerDistance = 8f; // deutlich weiter hinter Spieler

        Vector2 startPos = transform.position;
        Vector2 dirToEnemy = (targetPos - startPos).normalized;

        // Punkt etwas hinter dem Gegner (nur halbe Distanz)
        Vector2 forwardPoint = targetPos + dirToEnemy * extraForwardDistance;

        // HINFLUG
        while (boomerang != null && Vector2.Distance(boomerang.transform.position, forwardPoint) > 0.05f)
        {
            boomerang.transform.position = Vector2.MoveTowards(boomerang.transform.position, forwardPoint, moveSpeed * Time.deltaTime);
            yield return null;
        }

        // RÜCKFLUG: fliegt deutlich weiter hinter den Spieler
        Vector2 dirToPlayer = (startPos - (Vector2)boomerang.transform.position).normalized;
        Vector2 beyondPlayer = startPos + dirToPlayer * extraBehindPlayerDistance;

        while (boomerang != null && Vector2.Distance(boomerang.transform.position, beyondPlayer) > 0.05f)
        {
            boomerang.transform.position = Vector2.MoveTowards(boomerang.transform.position, beyondPlayer, moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (boomerang != null)
            Destroy(boomerang);

        target = Vector2.zero;
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
            Enemy enemy = collider.GetComponent<Enemy>();
            if (!enemiesInRange.Contains(enemy))
                enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            enemiesInRange.Remove(enemy);
        }
    }
}
