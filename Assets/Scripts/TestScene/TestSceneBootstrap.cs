using UnityEngine;

/// <summary>
/// Macht die Test-Szene alleine lauffähig.
///
/// Im echten Spiel liegt die Game-Szene additiv über der World Map und benutzt
/// deren Manager (MapsManager, AchievementManager, UnlockManager, ...). In der
/// Test-Szene gibt es die nicht, deshalb legt dieses Skript vor allen anderen
/// Awake-Aufrufen Ersatz-Manager an:
///
/// * <see cref="MapsManager"/> mit vollständigen extraData (alle Waffen/Buffs
///   freigeschaltet), damit im Level-Up-Panel wirklich alles auftauchen kann.
/// * <see cref="AchievementManager"/> und <see cref="UnlockManager"/> im
///   Sandbox-Modus: sie schalten nichts frei und schreiben nichts in die
///   Speicherdateien. Dein echter Fortschritt bleibt also unberührt.
///
/// Wird nur in der Test-Szene verwendet. Ist einer der Manager schon vorhanden
/// (z. B. weil die Szene später doch aus der World Map geladen wird), wird
/// nichts angelegt.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class TestSceneBootstrap : MonoBehaviour
{
    [Header("Spielstart")]
    [Tooltip("extraData[0] – Skin des Spielers (0 = normal).")]
    public int skinIndex = 0;

    [Tooltip("extraData[1] – Index der Startwaffe in PlayerController.activeWeapon.")]
    public int startWeaponIndex = 0;

    [Tooltip("Alle Waffen/Buffs/Evos beim Start auf 'nicht besessen' setzen, " +
             "damit du mit einem sauberen Blatt testen kannst.")]
    public bool clearWeaponsOnStart = true;

    [Header("Sandbox")]
    [Tooltip("Ersatz-Manager anlegen, falls sie fehlen (Achievements/Unlocks ohne Speichern).")]
    public bool createSandboxManagers = true;

    private bool cleared;

    void Awake()
    {
        EnsureMapsManager();

        if (createSandboxManagers)
        {
            EnsureAchievementManager();
            EnsureUnlockManager();
        }
    }

    void Update()
    {
        // Läuft dank DefaultExecutionOrder vor allen anderen Updates, also bevor
        // die Waffen im ersten Frame das erste Mal feuern können.
        if (!cleared)
        {
            cleared = true;
            if (clearWeaponsOnStart)
            {
                ClearAllWeapons();
            }
        }
    }

    // ------------------------------------------------------------------
    // Ersatz-Manager
    // ------------------------------------------------------------------

    private void EnsureMapsManager()
    {
        if (MapsManager.Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("MapsManager (Test)");
        go.SetActive(false);
        MapsManager maps = go.AddComponent<MapsManager>();
        maps.selectedMap = 0;
        maps.extraData = BuildExtraData();
        go.SetActive(true);
    }

    /// <summary>
    /// extraData-Layout siehe LevelPoint / PlayerController.StartStats:
    /// 0 Skin, 1 Startwaffe, 2..9 Shop-Extras, 10..17 Waffen-Unlocks,
    /// 18..26 Buff-Unlocks. In der Test-Szene ist alles freigeschaltet.
    /// </summary>
    private float[] BuildExtraData()
    {
        float[] data = new float[32];
        data[0] = skinIndex;
        data[1] = startWeaponIndex;

        // 2..9 bleiben 0: keine Shop-Boni, damit die Werte vergleichbar sind.
        for (int i = 10; i <= 26; i++)
        {
            data[i] = 1f;
        }

        return data;
    }

    private void EnsureAchievementManager()
    {
        if (AchievementManager.Instance != null)
        {
            return;
        }

        // Inaktiv anlegen, damit sandboxMode vor dem Awake gesetzt werden kann.
        GameObject go = new GameObject("AchievementManager (Sandbox)");
        go.SetActive(false);
        go.AddComponent<AchievementManager>().sandboxMode = true;
        go.SetActive(true);
    }

    private void EnsureUnlockManager()
    {
        if (UnlockManager.Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("UnlockManager (Sandbox)");
        go.SetActive(false);
        go.AddComponent<UnlockManager>().sandboxMode = true;
        go.SetActive(true);
    }

    // ------------------------------------------------------------------

    private void ClearAllWeapons()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            return;
        }

        TestSceneLoadout.ClearAll(player);
    }
}
