using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sammelt, was in einem Lauf neu dazugekommen ist - für die Anzeige im
/// Game-Over- und Sieg-Bildschirm.
///
/// Wie das Achievement-System legt sich dieses Objekt vor der ersten Szene
/// selbst an und überlebt jeden Szenenwechsel. Es steht damit in keiner Szene
/// und ist trotzdem überall da: World Map, Game-Szene und Test-Szene
/// gleichermassen. Früher lag es nur in "World Map.unity" - wer die Game-Szene
/// oder die Test-Szene direkt startete, bekam beim Tod eine
/// NullReferenceException mitten in <see cref="GameManager.GameOver"/>.
///
/// Achievements und Unlocks melden beide per Ereignis, sobald etwas aufgeht.
/// Der Vorher-Nachher-Vergleich für Unlocks bleibt trotzdem stehen: er fängt
/// den Fall ab, dass ein Unlock ausserhalb eines Laufs vergeben wurde.
/// </summary>
[DisallowMultipleComponent]
public class SessionProgressTracker : MonoBehaviour
{
    public static SessionProgressTracker Instance;

    private HashSet<string> unlocksBefore;

    public readonly List<AchievementDef> newAchievements = new List<AchievementDef>();
    public readonly List<string> newUnlocks = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("~SessionProgressTracker")
        {
            hideFlags = HideFlags.HideInHierarchy
        };

        Instance = go.AddComponent<SessionProgressTracker>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        Achievements.Unlocked += OnAchievementUnlocked;
        Unlocks.Granted += OnUnlockGranted;
    }

    private void OnDisable()
    {
        Achievements.Unlocked -= OnAchievementUnlocked;
        Unlocks.Granted -= OnUnlockGranted;
    }

    private void OnAchievementUnlocked(AchievementDef def)
    {
        if (def != null && !newAchievements.Contains(def)) newAchievements.Add(def);
    }

    private void OnUnlockGranted(UnlockDef def)
    {
        if (def != null && !newUnlocks.Contains(def.Id)) newUnlocks.Add(def.Id);
    }

    public void SnapshotBeforeGame()
    {
        newAchievements.Clear();
        newUnlocks.Clear();

        unlocksBefore = new HashSet<string>();

        foreach (UnlockDef unlock in Unlocks.All)
        {
            if (unlock.IsUnlocked) unlocksBefore.Add(unlock.Id);
        }
    }

    public void EvaluateAfterGame()
    {
        // Ohne Snapshot (z.B. direkter Szenenstart im Editor) gibt es nichts zu
        // vergleichen - die Listen stimmen trotzdem, sie kommen per Ereignis.
        if (unlocksBefore == null) return;

        foreach (UnlockDef unlock in Unlocks.All)
        {
            if (unlock.IsUnlocked && !unlocksBefore.Contains(unlock.Id) && !newUnlocks.Contains(unlock.Id))
            {
                newUnlocks.Add(unlock.Id);
            }
        }
    }
}
