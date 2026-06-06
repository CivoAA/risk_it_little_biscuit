using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance;

    [Header("🔓 Unlocks (Inspector)")]
    public List<Unlock> unlocks = new List<Unlock>();

    private string savePath;

    // =============================
    // Unity Lifecycle
    // =============================
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "unlocks.json");
        LoadUnlocks();
    }

    // =============================
    // Public API
    // =============================

    public void Unlock(string id)
    {
        Unlock unlock = unlocks.Find(u => u.id == id);
        if (unlock == null)
        {
            Debug.LogWarning($"⚠️ Unlock ID '{id}' nicht gefunden!");
            return;
        }

        if (unlock.isUnlocked)
            return;

        unlock.isUnlocked = true;
        SaveUnlocks();

        Debug.Log($"🔓 Unlock freigeschaltet: {id}");
    }

    public bool IsUnlocked(string id)
    {
        Unlock unlock = unlocks.Find(u => u.id == id);
        return unlock != null && unlock.isUnlocked;
    }

    public Unlock GetUnlock(string id)
    {
        return unlocks.Find(u => u.id == id);
    }

    // =============================
    // Save / Load + Migration
    // =============================

    private void LoadUnlocks()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("🆕 Keine Unlock-Datei gefunden – neue wird erstellt.");
            SaveUnlocks(); // speichert Inspector-Zustand
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            UnlockDataList loadedData = JsonUtility.FromJson<UnlockDataList>(json);

            if (loadedData == null || loadedData.unlocks == null)
            {
                Debug.LogWarning("⚠️ Unlock-Datei leer oder ungültig.");
                SaveUnlocks();
                return;
            }

            // 🔄 MIGRATION:
            // - gleiche ID → Status übernehmen
            // - neue Unlocks → bleiben locked
            foreach (var saved in loadedData.unlocks)
            {
                var inspectorUnlock = unlocks.Find(u => u.id == saved.id);
                if (inspectorUnlock != null)
                {
                    inspectorUnlock.isUnlocked = saved.isUnlocked;
                }
            }

            Debug.Log($"✅ Unlocks geladen & migriert ({loadedData.unlocks.Count}).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠️ Fehler beim Laden der Unlocks: " + e.Message);
        }

        // Speichern, damit neue Unlocks mit in der Datei landen
        SaveUnlocks();
    }

    public void SaveUnlocks()
    {
        UnlockDataList data = new UnlockDataList
        {
            unlocks = unlocks.Select(u => new UnlockData
            {
                id = u.id,
                isUnlocked = u.isUnlocked
            }).ToList()
        };

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
            Debug.Log("💾 Unlocks gespeichert: " + savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Unlock-Speichern fehlgeschlagen: " + e.Message);
        }
    }

    // =============================
    // DEV / DEBUG
    // =============================

    [ContextMenu("🔄 Reset ALL Unlocks (DEV ONLY)")]
    public void ResetAllUnlocks()
    {
        foreach (var unlock in unlocks)
            unlock.isUnlocked = false;

        SaveUnlocks();
        UnlockUIManager.Instance?.RefreshUI();
        Debug.Log("🔄 Alle Unlocks zurückgesetzt!");
    }
}
