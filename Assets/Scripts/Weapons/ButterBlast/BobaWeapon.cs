using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class BobaWeapon : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange;
    public Vector2 target;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxButterblast);
        }
        if (weaponLevel >= 0)
        {
            UpdateTarget();
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && shooting == false && target != Vector2.zero)
            {
                shooting = true;
                StartCoroutine(SpawnShuriken());
            }
        }
    }

    IEnumerator SpawnShuriken()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.ExtraShots);

        for (int i = 0; i < shots; i++)
        {
            AudioController.Instance.PalySound(AudioController.Instance.BOBA);

            GameObject BOBA = Instantiate(prefab, transform.position, transform.rotation);
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(BOBA, gameScene);
            }

            StartCoroutine(MoveAndDestroy(BOBA, BestLineTarget()));

            yield return new WaitForSeconds(0.2f);
        }

        // Cooldown korrekt setzen NACHDEM alle Schüsse abgefeuert wurden
        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator MoveAndDestroy(GameObject BOBA, Vector2 Target)
    {
        float moveSpeed = 10f;

        Vector2 shooterPos = transform.position;
        Vector2 enemyPos = Target;
        Vector2 direction = (enemyPos - shooterPos).normalized;
        Vector2 targetPos = enemyPos + direction * Overshoot;

        // Der Butterblock zeigt im Sprite nach rechts (Licht von oben). Beim
        // Flug nach links wird er gespiegelt, damit die Oberseite oben bleibt.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        BOBA.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        SpriteRenderer sr = BOBA.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipY = direction.x < 0f;

        while (BOBA != null && Vector3.Distance(BOBA.transform.position, targetPos) > 0.001f)
        {
            BOBA.transform.position = Vector3.MoveTowards(
                BOBA.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (BOBA != null)
        {
            Destroy(BOBA);
            target = Vector2.zero;
        }
    }

    /// <summary>Flugweite hinter dem Ziel - muss zu MoveAndDestroy passen.</summary>
    private const float Overshoot = 10f;

    /// <summary>
    /// Halbe Breite des Korridors, in dem ein Gegner als "auf der Linie" zaehlt:
    /// halbe Hoehe des Butter-Colliders (0,7 x Scale 2 / 2) plus etwas Koerper.
    /// </summary>
    private const float LineHalfWidth = 0.85f;

    /// <summary>
    /// Der Schuss durchdringt alles - also nicht stur auf den naechsten Gegner,
    /// sondern auf den, hinter dem die meisten anderen in einer Linie stehen.
    /// Jeder Gegner in Reichweite ist ein Kandidat; gezaehlt wird, wie viele
    /// lebende Gegner im Korridor bis zum Flugende liegen. Gleichstand: der
    /// naehere gewinnt, damit ein einzelner Gegner wie bisher anvisiert wird.
    /// </summary>
    private Vector2 BestLineTarget()
    {
        if (enemiesInRange == null) return target;

        Vector2 origin = transform.position;
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        Vector2 best = target;
        int bestCount = 0;
        float bestDistance = Mathf.Infinity;

        foreach (Enemy candidate in enemiesInRange)
        {
            if (candidate == null) continue;

            Vector2 toCandidate = (Vector2)candidate.transform.position - origin;
            float distance = toCandidate.magnitude;
            if (distance < 0.0001f) continue;

            Vector2 dir = toCandidate / distance;
            float length = distance + Overshoot;

            int count = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                Enemy e = alive[i];
                if (e == null) continue;

                Vector2 toEnemy = (Vector2)e.transform.position - origin;
                float along = Vector2.Dot(toEnemy, dir);
                if (along < 0f || along > length) continue;

                float side = Mathf.Abs(toEnemy.x * dir.y - toEnemy.y * dir.x);
                if (side <= LineHalfWidth) count++;
            }

            if (count > bestCount || (count == bestCount && distance < bestDistance))
            {
                bestCount = count;
                bestDistance = distance;
                best = candidate.transform.position;
            }
        }

        return best;
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
