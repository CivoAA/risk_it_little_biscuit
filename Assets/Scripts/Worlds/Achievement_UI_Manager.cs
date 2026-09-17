using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Die Achievement-Liste in der World Map. Füllt das vorhandene Prefab mit den
/// Daten aus dem Katalog <see cref="Ach"/> - Inhalt und Reihenfolge stehen damit
/// in Ach.cs und nicht mehr im Inspector.
///
/// Wer die Anzeige woanders braucht (Hauptmenü, Hub, im Spiel), nimmt besser
/// <see cref="AchievementPanel"/>: das baut sich selbst und braucht weder Szene
/// noch Prefab.
///
/// Die Einträge werden beim ersten Aufbau angelegt und danach nur noch neu
/// beschriftet - früher wurde bei jedem Öffnen die ganze Liste neu instanziiert.
/// </summary>
public class Achievement_UI_Manager : MonoBehaviour
{
    public static Achievement_UI_Manager Instance;

    [Header("🔗 UI-Verknüpfungen")]
    public Transform contentParent;
    public GameObject achievementUIPrefab;
    public Sprite unlockedSprite;

    private readonly List<Entry> entries = new List<Entry>();
    private bool built;

    private class Entry
    {
        public AchievementDef Def;
        public GameObject Root;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Description;
        public Image UnlockedBadge;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Achievements.Unlocked += OnUnlocked;
        Loc.LanguageChanged += Rebuild;
    }

    private void OnDisable()
    {
        Achievements.Unlocked -= OnUnlocked;
        Loc.LanguageChanged -= Rebuild;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        GenerateUI();
    }

    private void OnUnlocked(AchievementDef def) => RefreshProgressUI();

    /// <summary>Baut die Liste auf, falls noch nicht geschehen, und aktualisiert alle Texte.</summary>
    public void GenerateUI()
    {
        if (contentParent == null || achievementUIPrefab == null)
        {
            Debug.LogWarning("[Achievements] Achievement_UI_Manager: contentParent oder Prefab fehlt.");
            return;
        }

        if (!built) BuildEntries();
        RefreshProgressUI();
    }

    /// <summary>Schreibt nur die Werte neu - erzeugt keine Objekte.</summary>
    public void RefreshProgressUI()
    {
        // Wird das Panel zum ersten Mal geöffnet, ist Start() noch nicht gelaufen.
        if (!built)
        {
            GenerateUI();
            return;
        }

        foreach (Entry entry in entries)
        {
            AchievementDef def = entry.Def;
            bool unlocked = def.IsUnlocked;

            if (entry.Name != null) entry.Name.text = def.Name;

            if (entry.Description != null)
            {
                string desc = def.Description;
                if (def.HasProgress && !unlocked)
                {
                    desc += $"  [{Mathf.FloorToInt(def.Value)} / {Mathf.FloorToInt(def.Goal)}]";
                }
                entry.Description.text = desc;
            }

            if (entry.Icon != null)
            {
                Sprite icon = def.Icon;
                entry.Icon.enabled = icon != null;
                entry.Icon.sprite = icon;
                entry.Icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            if (entry.UnlockedBadge != null && unlockedSprite != null && unlocked)
            {
                entry.UnlockedBadge.sprite = unlockedSprite;
            }
        }
    }

    /// <summary>Wirft die Liste weg und baut sie neu - nach einem Sprachwechsel.</summary>
    public void Rebuild()
    {
        foreach (Entry entry in entries)
        {
            if (entry.Root != null) Destroy(entry.Root);
        }

        entries.Clear();
        built = false;
        GenerateUI();
    }

    private void BuildEntries()
    {
        // Reste aus dem Editor oder einem früheren Aufbau entfernen.
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        entries.Clear();

        foreach (AchievementDef def in Ach.All)
        {
            GameObject go = Instantiate(achievementUIPrefab, contentParent);
            go.name = "Achievement_" + def.Id;

            entries.Add(new Entry
            {
                Def            = def,
                Root           = go,
                Icon           = go.transform.Find("achievementIcon")?.GetComponent<Image>(),
                Name           = go.transform.Find("achievementName")?.GetComponent<TMP_Text>(),
                Description    = go.transform.Find("achievementDescription")?.GetComponent<TMP_Text>(),
                UnlockedBadge  = go.transform.Find("achievementUnlockedImage")?.GetComponent<Image>(),
            });
        }

        built = true;
    }
}
