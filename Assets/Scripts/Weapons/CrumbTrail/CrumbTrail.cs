using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kruemel-Spur: legt beim Laufen Kruemel ab, die liegen bleiben und Gegner
/// darin regelmaessig verletzen. Die einzige Waffe, bei der Bewegung selbst
/// der Ausloeser ist - Stehenbleiben erzeugt nichts.
///
/// cooldown    = Mindestzeit zwischen zwei Kruemeln
/// duration    = Lebensdauer eines Kruemels
/// damage      = Schaden pro Tick
/// range       = Groesse des Kruemels (wird zusaetzlich mit AOERange skaliert)
/// AttackSpeed = Abstand zwischen zwei Ticks
/// shots       = maximale Anzahl gleichzeitig liegender Kruemel (+ playerShots)
/// </summary>
public class CrumbTrail : Weapon
{
    [SerializeField] private GameObject prefab;

    [Tooltip("Strecke, die der Spieler seit dem letzten Kruemel zurueckgelegt haben muss.")]
    [SerializeField] private float minDropDistance = 0.7f;

    private readonly List<GameObject> activeCrumbs = new List<GameObject>();
    private float spawnCounter;
    private Vector2 lastDropPos;
    private bool hasDropped;

    void Update()
    {
        activeCrumbs.RemoveAll(c => c == null);

        // Bereits liegende Kruemel laufen bewusst normal aus, wenn die Waffe
        // durch eine Evo ersetzt wird - sie pruefen selbst auf IsActive.
        if (!IsActive) return;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxCrumbTrail);
        }

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        Vector2 pos = transform.position;
        if (hasDropped && Vector2.Distance(pos, lastDropPos) < minDropDistance) return;

        DropCrumb(pos);

        lastDropPos = pos;
        hasDropped = true;
        spawnCounter = CurrentCooldown;
    }

    private void DropCrumb(Vector2 pos)
    {
        int maxCrumbs = Mathf.Max(1, Mathf.RoundToInt(
            CurrentStats.shots + PlayerController.Instance.playerShots));

        // Aeltesten Kruemel abraeumen, damit die Spur eine feste Laenge hat und
        // nicht bei hoher Bewegungsgeschwindigkeit die Szene volllaeuft.
        while (activeCrumbs.Count >= maxCrumbs)
        {
            GameObject oldest = activeCrumbs[0];
            activeCrumbs.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }

        GameObject crumb = Instantiate(prefab, pos, Quaternion.identity);

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(crumb, gameScene);
        }

        CrumbTrailPrefab script = crumb.GetComponent<CrumbTrailPrefab>();
        if (script != null) script.weapon = this;

        activeCrumbs.Add(crumb);
    }
}
