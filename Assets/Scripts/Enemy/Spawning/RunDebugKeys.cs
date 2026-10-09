using UnityEngine;

/// <summary>
/// Testtasten im laufenden Run - nur im Editor und in Development-Builds,
/// im Release (Steam) gibt es das Objekt gar nicht.
///
///   F9          Keks-Koenig sofort spawnen (wie der Boss-Beat im Wellenplan:
///               Ansage, Boss-Leiste, gedaempfter Nachschub)
///   Shift + F9  dasselbe, aber gleich auf halbem Leben - er bekommt nach dem
///               Ankommen sofort seinen Wutanfall und kaempft in Phase 2
///   F10         Der Verkohlte mit 10 Leben: er laeuft an und saugt den
///               Spieler sofort ein (Phase 3, Herzkammer). Geht auch in der
///               Test-Szene, dort ueber den TestSceneBossSpawner.
///   F7          Schoko-Salve geben bzw. eine Stufe hoeher
///   F8          Milch-Tunker geben bzw. eine Stufe hoeher
///   F6          Mochi-Faden geben bzw. eine Stufe hoeher
///   F5          Fuchsfeuer geben bzw. eine Stufe hoeher
///   F4          Koenigsplumps geben bzw. eine Stufe hoeher
///   Shift + F4-F8  nur noch diese Waffe: alle anderen Waffen und Evos weg,
///               sie selbst auf Stufe 1 - zum Vergleichen als Startwaffe.
///               F4-F8 gehen ueberall, wo ein Spieler steht (auch Test-Szene).
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
    private const KeyCode SpawnCharredKey = KeyCode.F10;
    private const KeyCode ChocoChipsKey = KeyCode.F7;
    private const KeyCode MilkDunkKey = KeyCode.F8;
    private const KeyCode MochiStrandKey = KeyCode.F6;
    private const KeyCode FoxFireKey = KeyCode.F5;
    private const KeyCode RoyalSplatKey = KeyCode.F4;
    private const float CharredHealth = 10f;

    private GameObject king;
    private GameObject charred;

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
        if (Input.GetKeyDown(ChocoChipsKey))
        {
            GiveWeapon<ChocoChips>("Schoko-Salve");
            return;
        }

        if (Input.GetKeyDown(MilkDunkKey))
        {
            GiveWeapon<MilkDunk>("Milch-Tunker");
            return;
        }

        if (Input.GetKeyDown(MochiStrandKey))
        {
            GiveWeapon<MochiStrand>("Mochi-Faden");
            return;
        }

        if (Input.GetKeyDown(FoxFireKey))
        {
            GiveWeapon<FoxFire>("Fuchsfeuer");
            return;
        }

        if (Input.GetKeyDown(RoyalSplatKey))
        {
            GiveWeapon<RoyalSplat>("Koenigsplumps");
            return;
        }

        if (Input.GetKeyDown(SpawnCharredKey))
        {
            SpawnCharred();
            return;
        }

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

    /// <summary>
    /// Testwaffe geben bzw. aufstufen. Mit Shift alle anderen Waffen und Evos
    /// ablegen und mit dieser allein auf Stufe 1 weiterspielen. Laeuft ueber
    /// TestSceneLoadout, damit Evo-Partner wie im Spiel markiert werden.
    /// </summary>
    private void GiveWeapon<T>(string label) where T : Weapon
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        T weapon = player.GetComponentInChildren<T>(true);
        if (weapon == null)
        {
            Debug.LogWarning("[RunDebugKeys] " + label + " haengt nicht am Player-Prefab.");
            return;
        }

        bool solo = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (solo)
        {
            foreach (Weapon other in TestSceneLoadout.Group(player, TestSceneLoadout.Category.Weapons))
                if (other != null && other != weapon) TestSceneLoadout.Remove(other);
            foreach (Weapon other in TestSceneLoadout.Group(player, TestSceneLoadout.Category.Evos))
                if (other != null) TestSceneLoadout.Remove(other);

            TestSceneLoadout.Remove(weapon);
        }

        TestSceneLoadout.StepUp(player, weapon);

        string text = label + " Stufe " + (weapon.weaponLevel + 1) + (solo ? " (allein)" : "");
        Debug.Log("[RunDebugKeys] " + text);
        if (DamageNumberController.Instance != null)
            DamageNumberController.Instance.CreateText(text, player.transform.position + Vector3.up * 1.2f);
    }

    /// <summary>
    /// Verkohlter auf 10 Leben. Im Lauf wie ein Boss-Beat (Ansage, Boss-Leiste),
    /// in der Test-Szene ueber deren Boss-Spawner. Er stirbt trotzdem erst im
    /// Herzen - bis dahin haelt ihn <see cref="Enemy.MinHealthFraction"/>.
    /// </summary>
    private void SpawnCharred()
    {
        if (VerkohlterHerzkammer.Active != null)
        {
            Debug.LogWarning("[RunDebugKeys] Die Herzkammer ist noch offen - erst das Herz erledigen.");
            return;
        }

        if (charred != null) Destroy(charred);
        charred = null;

        SpawnDirector director = SpawnDirector.Active;
        if (director != null)
        {
            charred = director.DebugSpawnBoss(EnemyId.Verkohlter);
        }
        else
        {
            TestSceneBossSpawner spawner = FindAnyObjectByType<TestSceneBossSpawner>();
            if (spawner != null)
            {
                spawner.Spawn(EnemyId.Verkohlter);
                if (spawner.Boss != null) charred = spawner.Boss.gameObject;
            }
        }

        Enemy enemy = charred != null ? charred.GetComponent<Enemy>() : null;
        if (enemy == null)
        {
            Debug.LogWarning("[RunDebugKeys] Verkohlter konnte nicht gespawnt werden (kein Lauf / kein Prefab im SpawnCatalog?).");
            return;
        }

        enemy.DebugSetHealth(CharredHealth);
        Debug.Log("[RunDebugKeys] Verkohlter mit " + CharredHealth + " Leben gespawnt - gleich wird eingesaugt.");
    }
}
