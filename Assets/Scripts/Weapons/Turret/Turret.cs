using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turret: setzt im Cooldown-Takt einen Geschuetzturm an der Spielerposition
/// ab. Der Turm bleibt stehen, schiesst selbstaendig auf Gegner in seiner
/// Reichweite und verschwindet nach Ablauf der Wirkdauer.
///
/// Dadurch deckt die Waffe Stellen ab, an denen der Spieler gerade NICHT mehr
/// ist - das Gegenstueck zu allen Waffen, die am Spieler kleben.
///
/// cooldown    = Pause zwischen zwei Aufbauten
/// duration    = Standzeit eines Turms
/// damage      = Schaden pro Schuss
/// range       = Reichweite der Zielerfassung
/// AttackSpeed = Abstand zwischen zwei Schuessen des Turms
/// shots       = Anzahl Tuerme pro Aufbau (+ playerShots)
/// </summary>
public class Turret : Weapon
{
    [SerializeField] private GameObject prefab;

    [Tooltip("Streuung, wenn mehrere Tuerme gleichzeitig aufgebaut werden.")]
    [SerializeField] private float spreadRadius = 1.2f;

    private float spawnCounter;
    private bool building;

    void Update()
    {
        if (!IsActive) return;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxTurret);
        }

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f && !building)
        {
            StartCoroutine(BuildTurrets());
        }
    }

    private IEnumerator BuildTurrets()
    {
        building = true;

        int count = Mathf.Max(1, Mathf.RoundToInt(
            CurrentStats.shots + PlayerController.Instance.playerShots));

        Scene gameScene = SceneManager.GetSceneByName("Game");

        for (int i = 0; i < count; i++)
        {
            if (!IsActive) break;

            // Erster Turm genau auf den Spieler, weitere leicht versetzt,
            // damit sie sich nicht gegenseitig verdecken.
            Vector2 pos = transform.position;
            if (i > 0) pos += Random.insideUnitCircle * spreadRadius;

            // Bewusst ohne Parent: der Turm soll stehen bleiben.
            GameObject turret = Instantiate(prefab, pos, Quaternion.identity);

            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(turret, gameScene);
            }

            TurretPrefab script = turret.GetComponent<TurretPrefab>();
            if (script != null) script.weapon = this;

            yield return new WaitForSeconds(0.2f);
        }

        spawnCounter = CurrentCooldown;
        building = false;
    }
}
