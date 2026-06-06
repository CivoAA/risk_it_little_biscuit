using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Achievement_UI_Manager : MonoBehaviour
{
    public static Achievement_UI_Manager Instance;

    [Header("🔗 UI-Verknüpfungen")]
    public Transform contentParent;            
    public GameObject achievementUIPrefab;     
    public Sprite unlockedSprite;             

    private List<Achievement> achievements = new List<Achievement>();
    private List<GameObject> spawnedAchievements = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
    }

    void Start()
    {
        achievements = AchievementManager.Instance.achievements;
        GenerateUI();
    }

    public void GenerateUI()
    {
        if (AchievementManager.Instance == null)
        {
            Debug.LogWarning("❌ AchievementManager.Instance ist NULL! UI wird nicht generiert.");
            return;
        }

        achievements = AchievementManager.Instance.achievements;
        // Alles alte löschen
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        spawnedAchievements.Clear();

        foreach (Achievement ach in achievements)
        {
            GameObject entry = Instantiate(achievementUIPrefab, contentParent);
            spawnedAchievements.Add(entry);

            // 📜 Referenzen suchen
            Image iconImage = entry.transform.Find("achievementIcon")?.GetComponent<Image>();
            TMP_Text nameText = entry.transform.Find("achievementName")?.GetComponent<TMP_Text>();
            TMP_Text descText = entry.transform.Find("achievementDescription")?.GetComponent<TMP_Text>();
            Image unlockedImage = entry.transform.Find("achievementUnlockedImage")?.GetComponent<Image>();

            // ✅ Werte setzen
            if (nameText != null)
                nameText.text = ach.AchievmentName;

            if (descText != null)
            {
                string desc = ach.AchievmentNameDescription;
                if (ach.maxvalue > 1)
                    desc += $"  [{Mathf.FloorToInt(ach.value)} / {Mathf.FloorToInt(ach.maxvalue)}]";
                descText.text = desc;
            }

            if (iconImage != null && ach.icon != null)
                iconImage.sprite = ach.icon;

            if (unlockedImage != null && ach.unlocked && unlockedSprite != null)
            {
                unlockedImage.sprite = unlockedSprite;
            }
        }
    }

    public void RefreshProgressUI()
    {
        GenerateUI();
    }
}
