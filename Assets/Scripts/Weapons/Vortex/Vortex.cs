using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wirbel: setzt einen Strudel ab, der Gegner zur Mitte zieht und sie dabei
/// regelmaessig verletzt. Der Schaden ist bewusst niedrig - der Wert der Waffe
/// liegt darin, den Pulk fuer alle anderen Flaechenwaffen zusammenzuschieben.
///
/// cooldown    = Pause zwischen zwei Wirbeln
/// duration    = Wirkdauer eines Wirbels
/// damage      = Schaden pro Tick
/// range       = Radius (wird zusaetzlich mit AOERange skaliert)
/// AttackSpeed = Abstand zwischen zwei Ticks
/// shots       = Anzahl Wirbel pro Ausloesung (+ playerShots)
/// </summary>
public class Vortex : Weapon
{
    [SerializeField] private GameObject prefab;

    [Tooltip("Streuung um das Ziel, wenn mehrere Wirbel gleichzeitig gesetzt werden.")]
    [SerializeField] private float spreadRadius = 2f;

    public List<Enemy> enemiesInRange = new List<Enemy>();

    private float spawnCounter;
    private bool spawning;

    void Update()
    {
        enemiesInRange.RemoveAll(e => e == null);

        if (!IsActive) return;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxVortex);
        }

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f && !spawning)
        {
            StartCoroutine(SpawnVortices());
        }
    }

    private IEnumerator SpawnVortices()
    {
        spawning = true;

        int count = Mathf.Max(1, Mathf.RoundToInt(
            CurrentStats.shots + PlayerController.Instance.playerShots));

        Scene gameScene = SceneManager.GetSceneByName("Game");

        for (int i = 0; i < count; i++)
        {
            // Waffe kann waehrend der Salve durch eine Evo ersetzt werden
            if (!IsActive) break;

            Vector2 pos = PickSpawnPoint(i);

            GameObject vortex = Instantiate(prefab, pos, Quaternion.identity);

            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(vortex, gameScene);
            }

            VortexPrefab script = vortex.GetComponent<VortexPrefab>();
            if (script != null) script.weapon = this;

            yield return new WaitForSeconds(0.25f);
        }

        spawnCounter = CurrentCooldown;
        spawning = false;
    }

    /// <summary>Erster Wirbel mitten in den Pulk, weitere leicht versetzt.</summary>
    private Vector2 PickSpawnPoint(int index)
    {
        Vector2 center = ClosestEnemyPosition();
        if (index == 0) return center;

        return center + Random.insideUnitCircle * spreadRadius;
    }

    /// <summary>
    /// Position des naechsten Gegners. Ist keiner in Reichweite, wird blind in
    /// die Naehe gesetzt - so laeuft der Cooldown nicht ins Leere, wenn gerade
    /// eine Welle nachrueckt.
    /// </summary>
    private Vector2 ClosestEnemyPosition()
    {
        Enemy closest = null;
        float closestDistance = Mathf.Infinity;
        Vector2 myPos = transform.position;

        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy == null) continue;

            float distance = Vector2.Distance(myPos, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }
        }

        if (closest == null)
        {
            return myPos + Random.insideUnitCircle.normalized * spreadRadius;
        }

        return closest.transform.position;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null && !enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null) enemiesInRange.Remove(enemy);
    }
}
