using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class TimeLaser : Weapon
{
    [SerializeField] private GameObject prefab; // Das Laserobjekt
    public List<Enemy> enemiesInRange = new List<Enemy>();
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel < 0) return;

        spawnCounter -= Time.deltaTime;

        // 🔁 Warten bis Cooldown abgelaufen ist
        if (spawnCounter <= 0 && !shooting)
        {
            if (enemiesInRange.Count > 0)
            {
                StartCoroutine(SpawnLaser());
            }
        }
    }

    IEnumerator SpawnLaser()
    {
        shooting = true;
        int shots = Mathf.RoundToInt((stats[weaponLevel].shots + PlayerController.Instance.playerShots) * 0.5f);

        // Sofort Cooldown setzen, damit neuer Schuss nach Ablauf wieder möglich ist
        spawnCounter = CurrentCooldown;

        for (int i = 0; i < shots; i++)
        {
            if (enemiesInRange.Count > 0)
            {
                // 🎯 Zufälligen Gegner auswählen
                Enemy targetEnemy = enemiesInRange[Random.Range(0, enemiesInRange.Count)];

                if (targetEnemy != null)
                {
                    AudioController.Instance.PalySound(AudioController.Instance.Laser);

                    // Richtung & Rotation berechnen
                    Vector2 direction = (targetEnemy.transform.position - transform.position).normalized;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;

                    // Laser am Spieler spawnen und korrekt drehen
                    GameObject laser = Instantiate(prefab, transform.position, Quaternion.Euler(0f, 0f, angle));

                    // Erst in die Szene verschieben
                    Scene gameScene = RunScene.Current;
                    if (gameScene.IsValid() && gameScene.isLoaded)
                    {
                        SceneManager.MoveGameObjectToScene(laser, gameScene);
                    }

                    laser.transform.SetParent(transform, worldPositionStays: true);
                }
            }

            // kleine Verzögerung zwischen mehreren Schüssen
            yield return new WaitForSeconds(0.1f);
        }

        shooting = false;
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
