using UnityEngine;

[System.Serializable]
public class SkillData
{
    public SkillNode.SkillType type;

    [Header("Icons")]
    public Sprite lockedIcon;
    public Sprite unlockedIcon;

    [Header("Description")]
    [TextArea(2, 4)]
    public string description;
}

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    [Header("🎯 Skill Data (Icon + Beschreibung)")]
    public SkillData[] skills;

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

    // ------------------------------------------------------------
    // Zugriffsfunktionen
    // ------------------------------------------------------------
    public Sprite GetLockedIcon(SkillNode.SkillType type)
    {
        foreach (var s in skills)
            if (s.type == type)
                return s.lockedIcon;
        return null;
    }

    public Sprite GetUnlockedIcon(SkillNode.SkillType type)
    {
        foreach (var s in skills)
            if (s.type == type)
                return s.unlockedIcon;
        return null;
    }

    public string GetSkillDescription(SkillNode.SkillType type, float value)
    {
        foreach (var s in skills)
        {
            if (s.type != type)
                continue;

            string formattedValue;

            switch (type)
            {
                // Prozent-Werte (0.03 → 3%)
                case SkillNode.SkillType.IncreaseCritChance:
                case SkillNode.SkillType.IncreaseCritDamage:
                case SkillNode.SkillType.IncreaseDamage:
                case SkillNode.SkillType.IncreaseDodgeChance:
                case SkillNode.SkillType.xpMultiplier:
                case SkillNode.SkillType.IncreasePickupRange:
                case SkillNode.SkillType.IncreaseAOERange:
                    formattedValue = (value * 100f).ToString("0.#");
                    break;
                
                case SkillNode.SkillType.IncreaseSpeed:
                case SkillNode.SkillType.IncreaseLifeSteal:
                    formattedValue = value.ToString("0.##");
                    break;

                // Ganze Zahlen
                case SkillNode.SkillType.IncreaseLuck:
                case SkillNode.SkillType.IncreaseExtraShot:
                    formattedValue = Mathf.RoundToInt(value).ToString();
                    break;

                // Default (HP, Damage etc.)
                default:
                    formattedValue = value.ToString("0.##");
                    break;
            }

            return s.description.Replace("X", formattedValue);
        }

        return $"No description available for {type}.";
    }


    public void OnResetSkillsButton()
    {
        Debug.Log("🟡 Reset-Button wurde gedrückt!");

        if (SkillSaveManager.Instance == null)
        {
            Debug.LogError("❌ SkillSaveManager.Instance ist NULL!");
            return;
        }

        SkillSaveManager.Instance.ResetAllSkills();
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);
    }

}
