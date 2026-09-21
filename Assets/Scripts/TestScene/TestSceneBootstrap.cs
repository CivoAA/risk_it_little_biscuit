using UnityEngine;

/// <summary>
/// Macht die Test-Szene alleine lauffähig.
///
/// Im echten Spiel liegt die Game-Szene additiv über der World Map und benutzt
/// deren Manager (MapsManager, ...). In der
/// Test-Szene gibt es die nicht, deshalb legt dieses Skript vor allen anderen
/// Awake-Aufrufen Ersatz-Manager an:
///
/// * <see cref="MapsManager"/> plus einen Shop-Stand, in dem alle Waffen und
///   Buffs gekauft sind, damit im Level-Up-Panel wirklich alles auftauchen kann.
/// * Achievements und Unlocks im Sandbox-Modus: sie schalten
///   nichts frei und schreiben nichts in die Speicherdateien. Dein echter
///   Fortschritt bleibt also unberührt.
///
/// Wird nur in der Test-Szene verwendet. Ist einer der Manager schon vorhanden
/// (z. B. weil die Szene später doch aus der World Map geladen wird), wird
/// nichts angelegt.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class TestSceneBootstrap : MonoBehaviour
{
    [Header("Spielstart")]
    [Tooltip("Skin des Spielers (0 = normal).")]
    public int skinIndex = 0;

    [Tooltip("Index der Startwaffe in PlayerController.activeWeapon.")]
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

        Shop.SandboxMode = true;

        // Die Testszene stellt ihren Charakter frei ein (CaptureRunUnlockAll).
        // Der Verteiler haengt am Charakter - ohne das hier schriebe das
        // Umschalten in die echte loadout.json.
        Loadout.SandboxMode = true;

        if (createSandboxManagers)
        {
            EnableAchievementSandbox();
            EnableUnlockSandbox();
            Skills.SandboxMode = true;
        }

        // Im echten Spiel macht das die World Map beim Betreten der Karte. Ohne
        // diesen Schnitt wuerde die Liste "neu freigeschaltet" am Game-Over-Schirm
        // ueber jedes Restart hinweg weiterwachsen - der Tracker ueberlebt den
        // Szenenwechsel.
        SessionProgressTracker.Instance.SnapshotBeforeGame();
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
        go.SetActive(true);

        // Alle Waffen und Buffs gelten als gekauft, damit im Level-Up-Panel
        // wirklich alles auftauchen kann. Die mehrstufigen Upgrades bleiben auf
        // 0, damit die Werte mit einem frischen Spielstand vergleichbar sind.
        Shop.CaptureRunUnlockAll(skinIndex, startWeaponIndex);
    }

    /// <summary>
    /// Achievements laufen weiter mit, werden aber nicht mehr gespeichert und
    /// nicht an Steam gemeldet. Kein Ersatzobjekt nötig - das System ist statisch
    /// und läuft ohnehin schon.
    /// </summary>
    private void EnableAchievementSandbox()
    {
        Achievements.SandboxMode = true;
    }

    /// <summary>
    /// Unlocks laufen weiter mit, werden aber nicht gespeichert. Kein Ersatzobjekt
    /// noetig - das System ist statisch und laeuft ohnehin schon.
    /// </summary>
    private void EnableUnlockSandbox()
    {
        Unlocks.SandboxMode = true;
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
