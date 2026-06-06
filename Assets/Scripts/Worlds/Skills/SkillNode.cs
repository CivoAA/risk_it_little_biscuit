using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum SkillType
    {
        None,
        IncreaseMaxHealth,
        IncreaseSpeed,
        xpMultiplier,
        IncreaseHealthReg,
        IncreaseExtraShot,
        IncreaseArmor,
        IncreaseAOERange,
        IncreaseLifeSteal,
        IncreaseDamage,
        IncreaseCritDamage,
        IncreaseCritChance,
        IncreaseDodgeChance,
        IncreasePickupRange,
        IncreaseLuck,
        BanishAmount,
        RerollAmount,
        StartXPAmount,
        IncreaseShrinkSpeed,
        IncreaseWeaponSlots,
        IncreaseBuffSlots,
        IncreaseEvoSlots,
        UnlockEvoOrWeapon
    }

    [Header("Skill Settings")]
    public int skillID;
    public string skillName;
    public int price = 10;
    public SkillNode[] prerequisites;
    public SkillType skillEffect = SkillType.None;
    public float value = 0f;

    [Header("Optional Custom Description")]
    [TextArea(2, 4)] 
    public string customDescription;

    [Header("Visuals")]
    public Image iconImage;

    private Button button;
    public bool unlocked = false;
    public static List<SkillNode> allSkills = new List<SkillNode>();

    private Color clickableColor;
    private Color normalColor = Color.white;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (!allSkills.Contains(this))
            allSkills.Add(this);

        // Grau (#636363) für freischaltbare Skills
        if (ColorUtility.TryParseHtmlString("#636363", out Color parsed))
            clickableColor = parsed;
        else
            clickableColor = new Color(0.39f, 0.39f, 0.39f);
    }

    private void Start()
    {
        if (button != null)
            button.onClick.AddListener(OnClick);

        // Bereits freigeschaltet? (nach Laden)
        if (SkillSaveManager.Instance != null && SkillSaveManager.Instance.IsSkillUnlocked(skillID))
            unlocked = true;

        UpdateVisuals();
    }

    // 🟢 Wird aufgerufen, wenn der Spieler auf den Button klickt
    private void OnClick()
    {
        Debug.Log($"🟡 Skill Clicked: {skillName}, Price: {price}, Current SkillCurrency: {SkillSaveManager.Instance.currentData.skillCurrency}");

        if (unlocked)
        {
            Debug.Log($"{skillName} ist bereits freigeschaltet!");
            return;
        }

        if (!CanUnlock())
        {
            Debug.Log($"{skillName} kann noch nicht freigeschaltet werden!");
            return;
        }

        if (!SkillSaveManager.Instance.TrySpendSkillCurrency(price))
        {
            Debug.Log($"❌ Nicht genug Skillpunkte! (Hast {SkillSaveManager.Instance.currentData.skillCurrency}, brauchst {price})");
            return;
        }

        Debug.Log($"💰 Zahlung erfolgreich! Neuer Kontostand: {SkillSaveManager.Instance.currentData.skillCurrency}");
        Unlock();
    }


    // 🔒 Prüft, ob der Skill freigeschaltet werden darf
    public bool CanUnlock()
    {
        if (unlocked) return false;
        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        foreach (SkillNode prereq in prerequisites)
        {
            if (prereq == null || !prereq.unlocked)
                return false;
        }

        return true;
    }

    public void Unlock()
    {
        unlocked = true;
        SkillSaveManager.Instance.AddUnlockedSkill(skillID);
        SkillStatsManager.Instance.UpdateFromSkills();
        UpdateVisuals();

        foreach (var skill in allSkills)
            skill.UpdateVisuals();

        Debug.Log($"✅ Skill freigeschaltet: {skillName} (ID {skillID})");

        // 🟩 Neue Zeile:
        WM_UIController.Instance?.UpdateSkillCurrencyText();
    }

    // 🎨 Aktualisiert Icons und Farben
    public void UpdateVisuals()
    {
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        var manager = SkillManager.Instance;
        if (manager == null || iconImage == null) return;

        Sprite lockedIcon = manager.GetLockedIcon(skillEffect);
        Sprite unlockedIcon = manager.GetUnlockedIcon(skillEffect);

        if (unlocked)
        {
            iconImage.sprite = unlockedIcon ?? iconImage.sprite;
            iconImage.color = Color.white;
            button.interactable = false;
        }
        else if (CanUnlock())
        {
            iconImage.sprite = unlockedIcon ?? iconImage.sprite;
            iconImage.color = clickableColor;
            button.interactable = true;
        }
        else
        {
            iconImage.sprite = lockedIcon ?? iconImage.sprite;
            iconImage.color = normalColor;
            button.interactable = false;
        }

        iconImage.SetAllDirty();
    }

    // 🧭 Tooltip anzeigen
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SkillTooltip.Instance == null) return;

        string description;

        if (!string.IsNullOrWhiteSpace(customDescription))
        {
            description = customDescription;
        }
        else if (SkillManager.Instance != null)
        {
            description = SkillManager.Instance.GetSkillDescription(skillEffect, value);
        }
        else
        {
            description = "No description available.";
        }

        SkillTooltip.Instance.Show(skillName, price, description, GetPrerequisiteNames());
    }

    // 🧭 Tooltip ausblenden
    public void OnPointerExit(PointerEventData eventData)
    {
        SkillTooltip.Instance?.Hide(); 
    }

    // 📋 Gibt den Namen des ersten Prerequisites zurück
    private string GetPrerequisiteName()
    {
        if (prerequisites != null && prerequisites.Length > 0 && prerequisites[0] != null)
            return prerequisites[0].skillName;
        return string.Empty;
    }

    // 📋 Gibt alle Prerequisite-Namen als Liste zurück
    private System.Collections.Generic.List<string> GetPrerequisiteNames()
    {
        var names = new System.Collections.Generic.List<string>();

        if (prerequisites != null && prerequisites.Length > 0)
        {
            foreach (var prereq in prerequisites)
            {
                if (prereq != null && !string.IsNullOrWhiteSpace(prereq.skillName))
                    names.Add(prereq.skillName);
            }
        }

        return names;
    }

}
