using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Die Unlock-Liste in der World Map. Fuellt das vorhandene Prefab mit den Daten
/// aus dem Katalog <see cref="Unlocks"/> - Inhalt und Reihenfolge stehen damit in
/// Unlocks.cs und nicht mehr im Inspector.
///
/// Die Eintraege werden einmal angelegt und danach nur noch neu beschriftet.
/// </summary>
public class UnlockUIManager : MonoBehaviour
{
    public static UnlockUIManager Instance;

    [Header("UI-Verknuepfungen")]
    public GameObject unlockFramePrefab;
    public Transform contentParent;

    [Header("Detail-Anzeige")]
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public Image unlockIcon;

    private readonly List<Entry> entries = new List<Entry>();
    private bool built;
    private UnlockDef selected;

    private class Entry
    {
        public UnlockDef Def;
        public GameObject Root;
        public Image Icon;
        public Button Button;
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
        Unlocks.Granted += OnGranted;
        Loc.LanguageChanged += Rebuild;
    }

    private void OnDisable()
    {
        Unlocks.Granted -= OnGranted;
        Loc.LanguageChanged -= Rebuild;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start() => RefreshUI();

    private void OnGranted(UnlockDef def) => RefreshUI();

    /// <summary>Baut die Liste auf, falls noetig, und aktualisiert alle Eintraege.</summary>
    public void RefreshUI()
    {
        if (contentParent == null || unlockFramePrefab == null)
        {
            Debug.LogWarning("[Unlocks] UnlockUIManager: contentParent oder Prefab fehlt.");
            return;
        }

        if (!built) Build();

        foreach (Entry e in entries)
        {
            bool open = e.Def.IsUnlocked;

            if (e.Icon != null)
            {
                Sprite icon = e.Def.Icon;
                e.Icon.enabled = icon != null;
                e.Icon.sprite = icon;
                e.Icon.color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        // Die Auswahl des Spielers stehen lassen. Frueher sprang hier jedes
        // Aktualisieren - auch das durch ein frisch vergebenes Unlock ausgeloeste -
        // zurueck auf den ersten Eintrag.
        if (selected == null && entries.Count > 0) selected = entries[0].Def;
        if (selected != null) Select(selected);
    }

    private void Build()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        entries.Clear();

        foreach (UnlockDef def in Unlocks.All)
        {
            GameObject go = Instantiate(unlockFramePrefab, contentParent);
            go.name = "Unlock_" + def.Id;

            Transform iconTransform = go.transform.Find("unlock_icon");
            Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();

            UnlockDef captured = def;
            button.onClick.AddListener(() => Select(captured));

            entries.Add(new Entry { Def = def, Root = go, Icon = icon, Button = button });
        }

        built = true;
    }

    /// <summary>Wirft die Liste weg und baut sie neu - nach einem Sprachwechsel.</summary>
    public void Rebuild()
    {
        foreach (Entry e in entries)
        {
            if (e.Root != null) Destroy(e.Root);
        }

        entries.Clear();
        built = false;
        RefreshUI();
    }

    private void Select(UnlockDef def)
    {
        if (def == null) return;

        selected = def;
        bool open = def.IsUnlocked;

        if (nameText != null) nameText.text = open ? def.Name : "???";
        if (descriptionText != null) descriptionText.text = open ? def.Description : "";

        if (unlockIcon != null)
        {
            Sprite icon = def.Icon;
            unlockIcon.enabled = icon != null;
            unlockIcon.sprite = icon;
            unlockIcon.color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }
    }
}
