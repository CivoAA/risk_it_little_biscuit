using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class SkillSaveManager : MonoBehaviour
{
    public static SkillSaveManager Instance;
    private string savePath;

    public SkillSaveData currentData = new SkillSaveData();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, "skills.json");
        LoadSkills();

        // 🧩 SkillTree sofort aktivieren, damit SkillNodes ihr Awake() bekommen
        GameObject skillTree = FindInactiveObjectByName("SkillTreeCanvas");
        if (skillTree != null)
        {
            bool wasActive = skillTree.activeSelf;

            // Aktivieren → SkillNodes laufen einmal durch Awake()
            skillTree.SetActive(true);

            // Force Update aller Skills, damit alles sicher initialisiert ist
            Canvas.ForceUpdateCanvases();

            // Wenn es vorher inaktiv war → wieder deaktivieren
            if (!wasActive)
                skillTree.SetActive(false);

            Debug.Log("🧩 SkillTreeCanvas initialisiert und wieder deaktiviert.");
        }
        else
        {
            Debug.LogWarning("⚠️ SkillTreeCanvas nicht gefunden!");
        }
    }

    private void Start()
    {
        // Beim Start einmalig Skills anwenden, nachdem Szene initialisiert ist
        StartCoroutine(DelayedApplyAtSceneStart());
    }

    private IEnumerator DelayedApplyAtSceneStart()
    {
        // Kurz warten, damit UI-Referenzen vollständig da sind
        yield return new WaitForSeconds(0.2f);

        ApplyToScene();
        Debug.Log("✅ Skills automatisch beim Szenenstart angewendet.");
    }

    public void SaveSkills()
    {
        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(savePath, json);
        Debug.Log("💾 Skills gespeichert unter: " + savePath);
    }

    public void ApplyToScene()
    {
        if (SkillNode.allSkills.Count == 0)
        {
            StartCoroutine(RetryApply());
            return;
        }

        foreach (var node in SkillNode.allSkills)
        {
            node.unlocked = currentData.unlockedSkillIDs.Contains(node.skillID);
            node.UpdateVisuals();
        }

        SkillStatsManager.Instance?.UpdateFromSkills();
        Debug.Log($"✅ {currentData.unlockedSkillIDs.Count} Skills angewendet.");
    }

    private IEnumerator RetryApply()
    {
        yield return new WaitForSeconds(0.1f);
        ApplyToScene();
    }

    public void LoadSkills()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentData = JsonUtility.FromJson<SkillSaveData>(json);
            Debug.Log($"✅ Skills geladen (SkillCurrency: {currentData.skillCurrency})");
        }
        else
        {
            currentData = new SkillSaveData();
            SaveSkills();
        }
    }

    public void AddUnlockedSkill(int skillID)
    {
        if (!currentData.unlockedSkillIDs.Contains(skillID))
        {
            currentData.unlockedSkillIDs.Add(skillID);
            SaveSkills();
            Debug.Log($"💾 Skill {skillID} zur Liste hinzugefügt.");
        }
        else
        {
            Debug.Log($"ℹ️ Skill {skillID} war bereits freigeschaltet.");
        }
    }

    public bool IsSkillUnlocked(int skillID)
    {
        return currentData.unlockedSkillIDs.Contains(skillID);
    }

    // 💰 Skill-Währung
    public void AddSkillCurrency(int amount)
    {
        currentData.skillCurrency += amount;
        SaveSkills();
    }

    public bool TrySpendSkillCurrency(int amount)
    {
        if (currentData.skillCurrency < amount)
        {
            Debug.Log("❌ Nicht genug Skill-Punkte!");
            return false;
        }

        currentData.skillCurrency -= amount;
        SaveSkills();
        return true;
    }

    public void ResetAllSkills()
    {
        if (currentData.unlockedSkillIDs == null || currentData.unlockedSkillIDs.Count == 0)
        {
            Debug.Log("Keine Skills freigeschaltet – nichts zurückzusetzen.");
            return;
        }

        int refund = 0;

        foreach (var id in currentData.unlockedSkillIDs)
        {
            SkillNode node = SkillNode.allSkills.Find(n => n.skillID == id);
            if (node != null) refund += node.price;
        }

        currentData.unlockedSkillIDs.Clear();
        currentData.skillCurrency += refund;
        SaveSkills();

        foreach (var skill in SkillNode.allSkills)
        {
            if (skill == null) continue;
            skill.unlocked = false;
            skill.UpdateVisuals();
        }

        SkillStatsManager.Instance?.UpdateFromSkills();
        WM_UIController.Instance?.UpdateSkillCurrencyText();

        StartCoroutine(DelayedVisualRefresh());
        Debug.Log($"🔁 Skills zurückgesetzt. {refund} Skillpunkte erstattet.");
    }

    public IEnumerator DelayedVisualRefresh()
    {
        yield return null;
        foreach (var skill in SkillNode.allSkills)
            if (skill != null) skill.UpdateVisuals();
    }

    // 🔍 Sucht auch deaktivierte GameObjects (z. B. UI-Panels)
    private GameObject FindInactiveObjectByName(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in all)
        {
            if (obj.name == name)
                return obj;
        }
        return null;
    }

    public void ResetSkillCurrency()
    {
        currentData.skillCurrency = 0;
        SaveSkills();
        WM_UIController.Instance?.UpdateSkillCurrencyText();
        Debug.Log("SkillCurrency wurde auf 0 gesetzt.");
    }
    public void GainSkillCurrency()
    {
        currentData.skillCurrency += 50;
        SaveSkills();
        WM_UIController.Instance?.UpdateSkillCurrencyText();
        Debug.Log("SkillCurrency wurde um 50 erhöt.");
    }

    public void ResetAllProgress()
    {
        AchievementManager.Instance.ResetAllProgress();
    }
}
