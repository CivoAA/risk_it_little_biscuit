using System.Collections.Generic;
using UnityEngine;

public class SessionProgressTracker : MonoBehaviour
{
    public static SessionProgressTracker Instance;

    private HashSet<string> achievementsBefore;
    private HashSet<string> unlocksBefore;

    public List<string> newAchievements = new();
    public List<string> newUnlocks = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SnapshotBeforeGame()
    {
        achievementsBefore = new HashSet<string>();
        unlocksBefore = new HashSet<string>();

        foreach (var ach in AchievementManager.Instance.achievements)
        {
            if (ach.unlocked)
                achievementsBefore.Add(ach.id);
        }

        foreach (var unlock in UnlockManager.Instance.unlocks)
        {
            if (unlock.isUnlocked)
                unlocksBefore.Add(unlock.id);
        }

        newAchievements.Clear();
        newUnlocks.Clear();
    }

    public void EvaluateAfterGame()
    {
        // Absicherung: Wenn das Spiel ohne SnapshotBeforeGame gestartet wurde
        // (z. B. direkter Szenenstart im Editor), wäre achievementsBefore null.
        if (achievementsBefore == null || unlocksBefore == null)
        {
            Debug.LogWarning("SessionProgressTracker: Kein Snapshot vorhanden – EvaluateAfterGame wird übersprungen.");
            newAchievements.Clear();
            newUnlocks.Clear();
            return;
        }

        foreach (var ach in AchievementManager.Instance.achievements)
        {
            if (ach.unlocked && !achievementsBefore.Contains(ach.id))
                newAchievements.Add(ach.id);
        }

        foreach (var unlock in UnlockManager.Instance.unlocks)
        {
            if (unlock.isUnlocked && !unlocksBefore.Contains(unlock.id))
                newUnlocks.Add(unlock.id);
        }
    }
}
