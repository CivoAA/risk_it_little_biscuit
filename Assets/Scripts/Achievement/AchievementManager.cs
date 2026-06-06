using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Steamworks;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("🔧 Einstellungen")]
    [Tooltip("Wenn aktiv, werden NUR die Achievements aus dem Inspector gespeichert (alte Save-Dateien werden ignoriert).")]
    public bool overwriteWithInspectorData = false;

    [Header("🎯 Achievements")]
    public List<Achievement> achievements = new List<Achievement>();

    private string savePath;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "achievements.json");
        LoadAchievements();
    }

    // 🔓 Achievement sofort freischalten
    public void UnlockAchievement(string id)
    {
        var ach = achievements.Find(a => a.id == id);
        if (ach == null)
        {
            Debug.LogWarning($"⚠️ Achievement ID '{id}' not found in list!");
            return;
        }

        if (ach.unlocked)
        {
            Debug.Log($"🏆 Achievement '{id}' already unlocked.");
            return;
        }

        ach.unlocked = true;
        ach.value = ach.maxvalue; // Fortschritt vollenden

        // 💰 Belohnung vergeben
        if (SkillSaveManager.Instance != null)
        {
            SkillSaveManager.Instance.AddSkillCurrency(5);
            WM_UIController.Instance?.UpdateSkillCurrencyText();
            Debug.Log($"💰 +5 SkillCurrency for unlocking '{id}'!");
        }
        else
        {
            Debug.LogWarning("⚠️ SkillSaveManager.Instance ist NULL – keine Belohnung vergeben!");
        }

        // 🔹 Steamworks Sync
        if (SteamManager.Initialized)
        {
            try
            {
                SteamUserStats.SetAchievement(id);
                SteamUserStats.StoreStats();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("⚠️ Failed to set Steam achievement: " + e.Message);
            }
        }

        SaveAchievements();
    }

    // 🔁 Fortschritt aktualisieren (z. B. bei Kills, Collects usw.)
    public void UpdateAchievementValue(string id, float amountToAdd)
    {
        var ach = achievements.Find(a => a.id == id);
        if (ach == null)
        {
            //Debug.LogWarning($"⚠️ Achievement ID '{id}' not found in list!");
            return;
        }

        // Wenn schon freigeschaltet → nichts mehr tun
        if (ach.unlocked)
            return;

        // Fortschritt erhöhen
        ach.value += amountToAdd;
        ach.value = Mathf.Min(ach.value, ach.maxvalue); // Clamp auf max

        // Fortschritt loggen (nur bei Achievements mit maxvalue > 1)
        if (ach.maxvalue > 1)
            //Debug.Log($"🏁 Achievement '{id}' progress: {ach.value}/{ach.maxvalue}");

        // Ziel erreicht → automatisch freischalten
        if (ach.value >= ach.maxvalue)
        {
            UnlockAchievement(id);
        }

        SaveAchievements();
    }

   public void SaveAchievements()
    {
        if (string.IsNullOrEmpty(savePath))
        {
            Debug.LogError("❌ [Test] savePath is NULL or empty before writing achievements!");
        }
        else
        {
            Debug.Log($"✅ [Test] savePath is set: {savePath}");
        }

        try
        {
            AchievementList list = new AchievementList { achievements = achievements };
            string json = JsonUtility.ToJson(list, true);
            File.WriteAllText(savePath, json);
            Debug.Log("💾 Achievements saved to " + savePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save achievements: " + e.Message);
        }
    }

    // 📂 Laden
    public void LoadAchievements()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("No previous achievement file found — starting fresh.");
            SaveAchievements();
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            AchievementList loadedList = JsonUtility.FromJson<AchievementList>(json);

            foreach (var loaded in loadedList.achievements)
            {
                var existing = achievements.Find(a => a.id == loaded.id);
                if (existing != null)
                {
                    existing.unlocked = loaded.unlocked;
                    existing.value = loaded.value;
                }
            }

            Debug.Log($"📂 Loaded {loadedList.achievements.Count} achievements from file.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠️ Failed to read achievements.json: " + e.Message);
        }

        SaveAchievements();
    }
    public void ResetAllProgress()
    {
        // 1. Achievements zurücksetzen
        foreach (var ach in achievements)
        {
            ach.unlocked = false;
            ach.value = 0;
        }

        SaveAchievements();

        // 2. Skills komplett zurücksetzen
        if (SkillSaveManager.Instance != null)
        {
            SkillSaveManager.Instance.ResetAllSkills(); // Das erstattet Punkte!
            
            // 3. Danach Skill-Punkte auf 0 setzen
            SkillSaveManager.Instance.ResetSkillCurrency();
        }
        else
        {
            Debug.LogWarning("SkillSaveManager.Instance ist NULL - kein Skill-Reset!");
        }

        Debug.Log("Alles zurückgesetzt: Achievements, Skills und SkillCurrency.");
    }

}
