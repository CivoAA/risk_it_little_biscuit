using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BladeSwarm : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    private List<GameObject> activeBlades = new List<GameObject>();
    public Vector2 target;
    private Enemy currentTarget;
    private float attackCounter;
    private float durationCounter;
    private bool shooting = false;

    void Update()
    {
        // kaputte Objekte rauswerfen
        activeBlades.RemoveAll(b => b == null);

        // ► Waffe nicht aktiv: entweder noch nicht erhalten (-1) oder durch die
        // Evo ersetzt (Weapon.RemovedLevel). Früher wurde hier auf -10 geprüft –
        // diesen Wert setzt aber niemand, dadurch blieben die Klingen nach dem
        // Evo-Kauf als wirkungslose "Geister" am Spieler hängen und warfen bei
        // jedem Gegnerkontakt eine IndexOutOfRangeException.
        if (!IsActive)
        {
            if (activeBlades.Count > 0)
            {
                foreach (var blade in activeBlades)
                {
                    if (blade != null) Destroy(blade);
                }
                activeBlades.Clear();
            }
            return; // nichts mehr machen
        }

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxBladeSwarm);
            Unlocks.Grant(Unlocks.BladeSwarm);
        }

        attackCounter -= Time.deltaTime;
        durationCounter -= Time.deltaTime;
        if (durationCounter <= 0f && !shooting)
        {
            // Bewusst ohne CurrentDuration: duration ist hier der Abstand bis zur
            // naechsten Klinge - der Duration-Buff wuerde die Waffe verlangsamen.
            durationCounter = stats[weaponLevel].duration;
            SpawnBlade();
        }
        if (attackCounter <= 0f)
        {
            UpdateTarget();
            if (target != Vector2.zero)
            {
                shooting = true;
                attackCounter = CurrentCooldown;
                StartCoroutine(FireAllBladesAtTarget(target));
            }
        }
    }
    private void SpawnBlade()
    {
        if (activeBlades.Count >= stats[weaponLevel].shots + PlayerController.Instance.playerShots)
        {
            return;
        }
        else
        {
            Vector3 spawnPos = transform.position;
            GameObject blade = Instantiate(prefab, spawnPos, transform.rotation);
            blade.transform.SetParent(transform);

            activeBlades.Add(blade);
            PositionBlades();
        }
    }
    private void PositionBlades()
    {
        float baseRadius = 1f;

        for (int i = 0; i < activeBlades.Count; i++)
        {
            if (activeBlades[i] == null) continue;

            Vector2 pos = transform.position;

            switch (i)
            {
                case 0: // links unten, etwas höher
                    pos += (Vector2.left + Vector2.up * 0.4f) * baseRadius;
                    break;
                case 1: // rechts unten, etwas höher
                    pos += (Vector2.right + Vector2.up * 0.4f) * baseRadius;
                    break;
                case 2: // oben mittig
                    pos += Vector2.up * baseRadius * 1.4f;
                    break;
                case 3: // links über 0, näher
                    pos += (Vector2.left + Vector2.up * 0.8f) * baseRadius;
                    break;
                case 4: // rechts über 1, näher
                    pos += (Vector2.right + Vector2.up * 0.8f) * baseRadius;
                    break;
                case 5: // links über 3, fast oben beim Spieler, noch näher
                    pos += (Vector2.left * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f);
                    break;
                case 6: // rechts über 4, fast oben beim Spieler, noch näher
                    pos += (Vector2.right * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f);
                    break;
                case 7: // links über 3, fast oben beim Spieler, noch näher
                    pos += (Vector2.left * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f);
                    break;
                case 8: // rechts über 4, fast oben beim Spieler, noch näher
                    pos += (Vector2.right * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f);
                    break;
                default:
                    pos += Vector2.up * (baseRadius * 0.5f);
                    break;
            }

            activeBlades[i].transform.position = pos;
            activeBlades[i].transform.rotation = Quaternion.Euler(0f, 0f, 135f);
        }
    }

    private void UpdateTarget()
    {
        enemiesInRange.RemoveAll(e => e == null);

        if (enemiesInRange.Count == 0)
        {
            currentTarget = null;
            target = Vector2.zero;
            return;
        }

        int randomIndex = Random.Range(0, enemiesInRange.Count);
        currentTarget = enemiesInRange[randomIndex];
        target = currentTarget.transform.position; // initial setzen
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
    private IEnumerator FireAllBladesAtTarget(Vector2 target)
    {
        activeBlades.RemoveAll(b => b == null);
        var bladesToFire = new List<GameObject>(activeBlades);
        foreach (GameObject blade in bladesToFire)
        {
            if (blade == null) continue;
            blade.transform.SetParent(null);
            StartCoroutine(MoveAndDestroy(blade, target));
            yield return new WaitForSeconds(0.13f); // Abstand zwischen den Blades
        }
        activeBlades.Clear();
        shooting = false;
    }
    IEnumerator MoveAndDestroy(GameObject blade, Vector2 Target)
    {
        float moveSpeed = 20f;
        float lifetime = 2f;             // maximale Lebenszeit in Sekunden
        float elapsed = 0f;

        Vector2 shooterPos = transform.position;
        Vector2 fallbackDirection = (Target - shooterPos).normalized;

        while (blade != null && elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            Vector2 currentEnemyPos;

            if (currentTarget != null) // Gegner noch da?
            {
                currentEnemyPos = currentTarget.transform.position;
            }
            else
            {
                // Gegner weg → flieg weiter in der ursprünglichen Richtung
                currentEnemyPos = (Vector2)blade.transform.position + fallbackDirection * 10f;
            }

            Vector2 direction = (currentEnemyPos - (Vector2)blade.transform.position).normalized;
            Vector2 targetPos = currentEnemyPos;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            blade.transform.rotation = Quaternion.Euler(0f, 0f, angle + -45f);

            blade.transform.position = Vector2.MoveTowards(
                blade.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime);

            // wenn nahe genug am Ziel beenden
            if (Vector2.Distance(blade.transform.position, targetPos) < 0.1f)
            {
                break;
            }

            yield return null;
        }

        if (blade != null)
        {
            Destroy(blade);
        }
    }

}
