using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wirbel: setzt einen Strudel ab, der Gegner zur Mitte zieht, bremst und sie
/// dabei regelmaessig verletzt. Der Schaden ist bewusst niedrig - der Wert der Waffe
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

    [Tooltip("Mindestabstand zwischen Spieler und Wirbelrand (Tiles). Steht der Spieler im Sog, zieht der Wirbel die Gegner auf ihn zu.")]
    [SerializeField] private float playerGap = 2f;

    [Tooltip("Mindestabstand zwischen den Raendern zweier Wirbel (Tiles). Ueberlappen sie, ziehen sie die Gegner hin und her.")]
    [SerializeField] private float vortexGap = 0.3f;

    public List<Enemy> enemiesInRange = new List<Enemy>();

    /// <summary>Wirbel, die gerade stehen - neue duerfen sie nicht ueberlappen.</summary>
    private readonly List<GameObject> liveVortices = new List<GameObject>();

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
            CurrentStats.shots + PlayerController.Instance.ExtraShots));

        Scene gameScene = RunScene.Current;

        for (int i = 0; i < count; i++)
        {
            // Waffe kann waehrend der Salve durch eine Evo ersetzt werden
            if (!IsActive) break;

            Vector2 pos = PickSpawnPoint();

            GameObject vortex = Instantiate(prefab, pos, Quaternion.identity);
            liveVortices.Add(vortex);

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

    /// <summary>Radius eines Wirbels in Tiles: Collider 0,5 x Scale (range x AOERange).</summary>
    private float VortexRadius
    {
        get { return 0.5f * CurrentStats.range * PlayerController.Instance.AOERange; }
    }

    /// <summary>
    /// Auf den naechsten Gegner, der weit genug vom Spieler weg steht und an
    /// dem der Wirbel keinen anderen ueberlappt. Gibt es keinen solchen
    /// Gegner, wird ein freier Platz auf einem Ring um den Spieler gesucht -
    /// beginnend in Richtung des naechsten Gegners, damit der Wirbel trotzdem
    /// dort landet, wo die Welle herkommt.
    /// </summary>
    private Vector2 PickSpawnPoint()
    {
        liveVortices.RemoveAll(v => v == null);

        Vector2 myPos = transform.position;
        float radius = VortexRadius;
        float minFromPlayer = radius + playerGap;

        Enemy best = null;
        float bestDistance = Mathf.Infinity;
        Enemy closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy == null) continue;

            Vector2 pos = enemy.transform.position;
            float distance = Vector2.Distance(myPos, pos);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }

            if (distance < minFromPlayer || distance >= bestDistance) continue;
            if (OverlapsLiveVortex(pos, radius)) continue;

            bestDistance = distance;
            best = enemy;
        }

        if (best != null) return best.transform.position;

        Vector2 baseDir = closest != null
            ? ((Vector2)closest.transform.position - myPos).normalized
            : Random.insideUnitCircle.normalized;
        if (baseDir == Vector2.zero) baseDir = Vector2.right;

        // Ringe nach aussen, auf jedem Ring abwechselnd links/rechts der Grundrichtung.
        for (int ring = 0; ring < 3; ring++)
        {
            float distance = minFromPlayer + ring * radius;
            for (int step = 0; step <= 6; step++)
            {
                float angle = 30f * ((step + 1) / 2) * (step % 2 == 1 ? 1f : -1f);
                Vector2 pos = myPos + (Vector2)(Quaternion.Euler(0f, 0f, angle) * baseDir) * distance;
                if (!OverlapsLiveVortex(pos, radius)) return pos;
            }
        }

        return myPos + baseDir * minFromPlayer;
    }

    private bool OverlapsLiveVortex(Vector2 pos, float radius)
    {
        float minDistance = 2f * radius + vortexGap;
        foreach (GameObject vortex in liveVortices)
        {
            if (vortex == null) continue;
            if (Vector2.Distance(pos, vortex.transform.position) < minDistance) return true;
        }
        return false;
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
