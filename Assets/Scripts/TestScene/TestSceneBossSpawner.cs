using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setzt den Keks-Koenig in die Test-Szene, damit man seine Attacken
/// ausprobieren kann, ohne auf ihn zu warten: im Wellenplan steht er erst nach
/// rund 890 Sekunden, siehe <see cref="WavePlans"/>.
///
/// Das Prefab kommt ueber den Asset-Pfad statt ueber ein Inspector-Feld. Die
/// Test-Szene haengt bewusst an keiner einzigen Prefab-Referenz (siehe
/// <see cref="TestSceneHUD"/>), sonst muesste sie nach jedem Neubau wieder von
/// Hand verdrahtet werden. Ausserhalb des Editors gibt es die AssetDatabase
/// nicht - dann wird im Resources-Ordner weitergesucht und sonst sauber
/// gemeldet, dass es nicht geht. Die Test-Szene ist ein Editor-Werkzeug, das
/// reicht hier.
/// </summary>
public class TestSceneBossSpawner : MonoBehaviour
{
    private const string AssetPath = "Assets/Prefabs/Enemy/Boss/KecksKoenig.prefab";
    private const string ResourcePath = "Enemy/KecksKoenig";

    [Tooltip("Abstand vom Spieler, in dem der Boss auftaucht.")]
    public float spawnDistance = 10f;

    private GameObject current;
    private Enemy currentEnemy;
    private EnemyKeckKönig currentKing;

    /// <summary>Steht gerade ein Boss?</summary>
    public bool Alive
    {
        get { return current != null; }
    }

    public Enemy Boss
    {
        get { return current != null ? currentEnemy : null; }
    }

    public EnemyKeckKönig King
    {
        get { return current != null ? currentKing : null; }
    }

    /// <summary>
    /// Stellt einen frischen Boss hin. Ein bereits stehender wird vorher
    /// entfernt - zwei Keks-Koenige gleichzeitig sagen ueber die Attacken
    /// nichts aus, weil man nicht mehr sieht, welche Warnung zu wem gehoert.
    /// </summary>
    public string Spawn()
    {
        GameObject prefab = LoadPrefab();
        if (prefab == null)
        {
            return "<color=#FF6A4A>Boss-Prefab nicht gefunden.</color>";
        }

        Clear();

        Vector3 center = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 at = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnDistance;

        current = Instantiate(prefab, at, Quaternion.identity);
        current.name = "KecksKoenig (Test)";
        currentEnemy = current.GetComponent<Enemy>();
        currentKing = current.GetComponent<EnemyKeckKönig>();

        // Gleiche Vorsicht wie ueberall sonst: erzeugt wird in der aktiven
        // Szene, und das muss nicht die sein, in der der Spieler steht.
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded && current.scene != run)
        {
            SceneManager.MoveGameObjectToScene(current, run);
        }

        return "Keks-Koenig steht. Viel Glueck.";
    }

    /// <summary>Raeumt den Boss weg. True, wenn wirklich einer dastand.</summary>
    public bool Clear()
    {
        if (current == null) return false;

        // Destroy statt Leben auf 0: ueber Enemy.TakeDamage wuerde der ganze
        // Todesfall mitlaufen (Truhe, Skill-Waehrung, SpawnDeath), und das
        // gehoert nicht zum Aufraeumen dazu.
        Destroy(current);
        current = null;
        currentEnemy = null;
        currentKing = null;
        return true;
    }

    private static GameObject LoadPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab != null) return prefab;

#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
#endif
        return prefab;
    }
}
