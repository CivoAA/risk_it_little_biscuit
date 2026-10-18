using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Schiesst kurze Laserstrahlen auf zufaellige Gegner im Bild. Jeder Strahl
/// steht fest in der Welt (laeuft nicht mit dem Spieler mit), trifft einmal und
/// blendet dann aus - siehe <see cref="TimeLaserPrefab"/>.
/// </summary>
public class TimeLaser : Weapon
{
    /// <summary>Wie die Schuesse der Stufe nur halb: zwei Extra-Schuesse fuer einen Strahl mehr.</summary>
    protected override int ExtraShotsPerShot => 2;

    [SerializeField] private GameObject prefab; // Das Laserobjekt
    public List<Enemy> enemiesInRange = new List<Enemy>();
    private readonly List<Enemy> targets = new List<Enemy>();
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel < 0) return;

        spawnCounter -= Time.deltaTime;

        // 🔁 Warten bis Cooldown abgelaufen ist
        if (spawnCounter <= 0 && !shooting && CollectTargets())
        {
            StartCoroutine(SpawnLaser());
        }
    }

    // Wird die Waffe mitten in der Salve deaktiviert, endet die Coroutine
    // ohne shooting zurueckzusetzen - dann wuerde nie wieder geschossen.
    void OnDisable()
    {
        shooting = false;
    }

    IEnumerator SpawnLaser()
    {
        shooting = true;
        // Halbe aufrunden: Mathf.RoundToInt macht aus 2,5 eine 2 (zur geraden Zahl)
        int shots = Mathf.FloorToInt(stats[weaponLevel].shots * 0.5f + 0.5f) + RollExtraShots();

        // Sofort Cooldown setzen, damit neuer Schuss nach Ablauf wieder möglich ist
        spawnCounter = CurrentCooldown;

        for (int i = 0; i < shots; i++)
        {
            if (CollectTargets())
            {
                // 🎯 Zufälligen Gegner auswählen
                Enemy targetEnemy = targets[Random.Range(0, targets.Count)];
                Fire(targetEnemy.transform.position);
            }

            // kleine Verzögerung zwischen mehreren Schüssen
            yield return new WaitForSeconds(0.1f);
        }

        shooting = false;
    }

    private void Fire(Vector3 targetPosition)
    {
        WeaponSounds.Play(WeaponSounds.LaserShot);

        // Das Sprite zeigt vom Pivot aus nach links, darum +180
        Vector2 direction = (targetPosition - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;

        // Bewusst ohne Parent: der Strahl bleibt stehen, wo er abgefeuert wurde
        GameObject laser = Instantiate(prefab, transform.position, Quaternion.Euler(0f, 0f, angle));

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(laser, gameScene);
        }

        TimeLaserPrefab beam = laser.GetComponent<TimeLaserPrefab>();
        if (beam != null) beam.weapon = this;
    }

    /// <summary>
    /// Fuellt <see cref="targets"/> mit lebenden Gegnern, die im Bild stehen.
    /// Der Reichweiten-Collider ist groesser als das Bild der Pixel-Perfect-
    /// Kamera - ohne den Bildfilter schoss der Laser auf unsichtbare Gegner.
    /// </summary>
    private bool CollectTargets()
    {
        enemiesInRange.RemoveAll(e => e == null || !e.isActiveAndEnabled);

        targets.Clear();
        bool hasView = ViewBounds.TryGetWorldRect(out Rect view);
        foreach (Enemy enemy in enemiesInRange)
        {
            if (!hasView || view.Contains(enemy.transform.position))
                targets.Add(enemy);
        }
        return targets.Count > 0;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            if (enemy != null && !enemiesInRange.Contains(enemy))
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
