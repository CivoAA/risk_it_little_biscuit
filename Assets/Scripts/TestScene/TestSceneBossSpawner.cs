using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setzt einen Boss in die Test-Szene, damit man seine Attacken ausprobieren
/// kann, ohne auf ihn zu warten: im Wellenplan steht er erst nach rund 890
/// Sekunden, siehe <see cref="WavePlans"/>.
///
/// Welche Bosse es gibt, steht im <see cref="EnemyCatalog"/> (Rolle Boss,
/// nicht archiviert) - ein neuer Boss taucht in der Auswahl der Test-Szene
/// also von selbst auf, sobald er dort eingetragen und gebaut ist.
///
/// Das Prefab kommt ueber den Asset-Pfad aus dem Katalog statt ueber ein
/// Inspector-Feld. Die Test-Szene haengt bewusst an keiner einzigen
/// Prefab-Referenz (siehe <see cref="TestSceneHUD"/>), sonst muesste sie nach
/// jedem Neubau wieder von Hand verdrahtet werden. Ausserhalb des Editors gibt
/// es die AssetDatabase nicht - dann hilft der SpawnCatalog aus GameCore, und
/// sonst wird sauber gemeldet, dass es nicht geht.
/// </summary>
public class TestSceneBossSpawner : MonoBehaviour
{
    [Tooltip("Abstand vom Spieler, in dem der Boss auftaucht.")]
    public float spawnDistance = 10f;

    private GameObject current;
    private Enemy currentEnemy;
    private EnemyKeckKönig currentKing;
    private EnemyGlutwurz currentGlutwurz;
    private EnemySchleimkoenig currentSchleimkoenig;
    private EnemyVerkohlter currentVerkohlter;

    /// <summary>Steht gerade ein Boss?</summary>
    public bool Alive
    {
        get { return current != null; }
    }

    public Enemy Boss
    {
        get { return current != null ? currentEnemy : null; }
    }

    /// <summary>Laeuft beim aktuellen Boss schon die zweite Phase?</summary>
    public bool IsPhaseTwo
    {
        get
        {
            if (current == null) return false;
            if (currentKing != null) return currentKing.IsPhaseTwo;
            if (currentGlutwurz != null) return currentGlutwurz.IsPhaseTwo;
            if (currentSchleimkoenig != null) return currentSchleimkoenig.IsPhaseTwo;
            if (currentVerkohlter != null) return currentVerkohlter.IsPhaseTwo;
            return false;
        }
    }

    /// <summary>Alle Bosse aus dem Katalog, zu denen ein Prefab liegt.</summary>
    public static List<EnemyId> Available()
    {
        return Available(EnemyRole.Boss);
    }

    /// <summary>
    /// Alle Gegner einer Rolle (Boss oder Zwischenboss = MiniBoss) aus dem
    /// Katalog, zu denen ein Prefab liegt.
    /// </summary>
    public static List<EnemyId> Available(EnemyRole role)
    {
        var list = new List<EnemyId>();
        foreach (EnemyDef def in EnemyCatalog.All)
        {
            if (def.Role != role || def.Archived) continue;
            if (LoadPrefab(def.Id) == null) continue;
            list.Add(def.Id);
        }
        return list;
    }

    /// <summary>
    /// Stellt einen frischen Boss hin. Ein bereits stehender wird vorher
    /// entfernt - zwei Bosse gleichzeitig sagen ueber die Attacken nichts aus,
    /// weil man nicht mehr sieht, welche Warnung zu wem gehoert.
    /// </summary>
    public string Spawn(EnemyId id)
    {
        GameObject prefab = LoadPrefab(id);
        string name = Bestiary.NameOf(id);
        if (prefab == null)
        {
            return "<color=#FF6A4A>Prefab fuer " + name + " nicht gefunden.</color>";
        }

        Clear();

        Vector3 center = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 at = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnDistance;

        current = Instantiate(prefab, at, Quaternion.identity);
        current.name = prefab.name + " (Test)";
        currentEnemy = current.GetComponent<Enemy>();
        if (currentEnemy != null) currentEnemy.SpawnedAs = id;
        currentKing = current.GetComponent<EnemyKeckKönig>();
        currentGlutwurz = current.GetComponent<EnemyGlutwurz>();
        currentSchleimkoenig = current.GetComponent<EnemySchleimkoenig>();
        currentVerkohlter = current.GetComponent<EnemyVerkohlter>();

        // Gleiche Vorsicht wie ueberall sonst: erzeugt wird in der aktiven
        // Szene, und das muss nicht die sein, in der der Spieler steht.
        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded && current.scene != run)
        {
            SceneManager.MoveGameObjectToScene(current, run);
        }

        return name + " steht. Viel Glueck.";
    }

    /// <summary>
    /// Feuerkreis der Glutwurz ausloesen, ohne erst wegzulaufen. Steht keine
    /// Glutwurz, wird eine hingestellt - sie springt dann gleich los.
    /// </summary>
    public string GlutwurzLeap()
    {
        if (currentGlutwurz == null)
        {
            string spawned = Spawn(EnemyId.Glutwurz);
            if (currentGlutwurz == null) return spawned;
        }

        bool rebuild = currentGlutwurz.HasArena;
        currentGlutwurz.RequestLeap();
        return rebuild
            ? "Glutwurz springt erneut - der alte Feuerkreis geht aus."
            : "Glutwurz springt nach der laufenden Attacke.";
    }

    /// <summary>Raeumt den Boss weg. True, wenn wirklich einer dastand.</summary>
    public bool Clear()
    {
        if (current == null) return false;

        // Destroy statt Leben auf 0: ueber Enemy.TakeDamage wuerde der ganze
        // Todesfall mitlaufen (Boss-Level, SpawnDeath), und das
        // gehoert nicht zum Aufraeumen dazu.
        Destroy(current);
        current = null;
        currentEnemy = null;
        currentKing = null;
        currentGlutwurz = null;
        currentSchleimkoenig = null;
        currentVerkohlter = null;
        return true;
    }

    private static GameObject LoadPrefab(EnemyId id)
    {
        EnemyDef def = EnemyCatalog.Get(id);
        GameObject prefab = null;

#if UNITY_EDITOR
        if (def != null && !string.IsNullOrEmpty(def.Prefab))
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(def.Prefab);
        }
#endif
        if (prefab != null) return prefab;

        SpawnCatalog catalog = FindAnyObjectByType<SpawnCatalog>();
        return catalog != null ? catalog.Prefab(id) : null;
    }
}
