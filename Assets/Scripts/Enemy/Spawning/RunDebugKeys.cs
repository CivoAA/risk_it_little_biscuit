using UnityEngine;

/// <summary>
/// Testtasten im laufenden Run - nur im Editor und in Development-Builds,
/// im Release (Steam) gibt es das Objekt gar nicht.
///
///   F9          Keks-Koenig sofort spawnen (wie der Boss-Beat im Wellenplan:
///               Ansage, Boss-Leiste, gedaempfter Nachschub)
///   Shift + F9  dasselbe, aber gleich auf halbem Leben - er bekommt nach dem
///               Ankommen sofort seinen Wutanfall und kaempft in Phase 2
///
/// Ein frueher per Taste gesetzter Koenig wird vorher entfernt: zwei auf
/// einmal sagen ueber die Attacken nichts aus, weil man nicht mehr sieht,
/// welche Warnung zu wem gehoert.
///
/// Haengt sich selbst beim Spielstart an (kein Szenen-Objekt noetig) und tut
/// nur etwas, solange ein <see cref="SpawnDirector"/> laeuft, also in Runs.
/// </summary>
public class RunDebugKeys : MonoBehaviour
{
    private const KeyCode SpawnKingKey = KeyCode.F9;

    private GameObject king;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        var go = new GameObject("RunDebugKeys");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<RunDebugKeys>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(SpawnKingKey)) return;

        SpawnDirector director = SpawnDirector.Active;
        if (director == null) return;

        bool phaseTwo = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (king != null) Destroy(king);
        king = director.DebugSpawnBoss(EnemyId.KeksKoenig);
        if (king == null)
        {
            Debug.LogWarning("[RunDebugKeys] Keks-Koenig konnte nicht gespawnt werden (kein Prefab im SpawnCatalog?).");
            return;
        }

        if (phaseTwo)
        {
            Enemy enemy = king.GetComponent<Enemy>();
            if (enemy != null) enemy.DebugSetHealthFraction(0.5f);
        }

        Debug.Log("[RunDebugKeys] Keks-Koenig gespawnt" + (phaseTwo ? " (Phase 2)." : "."));
    }
}
