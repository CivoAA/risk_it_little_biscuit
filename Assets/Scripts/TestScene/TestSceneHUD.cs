using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Das komplette Test-Interface: Schadensanzeige oben links, Buttons für
/// Level/Reroll/Banish/Neustart und ein Menü, in dem sich jede Waffe, jeder
/// Buff und jede Evo direkt auf eine beliebige Stufe setzen lässt.
///
/// Die Oberfläche wird zur Laufzeit erzeugt. So hängt die Test-Szene an keiner
/// einzigen Prefab-Referenz und geht auch dann nicht kaputt, wenn im echten
/// Spiel am UI geschraubt wird.
/// </summary>
public class TestSceneHUD : MonoBehaviour
{
    [Header("Bedienung")]
    [Tooltip("Taste, die das Waffen-/Evo-Menü ein- und ausblendet.")]
    public KeyCode toggleLoadoutKey = KeyCode.F1;

    [Tooltip("Menü beim Start geöffnet anzeigen.")]
    public bool startWithLoadoutOpen = true;

    [Header("Aussehen")]
    public int fontSize = 22;
    public Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.86f);
    public Color accentColor = new Color(1f, 0.82f, 0.25f, 1f);

    private static readonly NumberFormatInfo NumberFormat = new NumberFormatInfo
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = new[] { 3 }
    };

    private DamageMeter meter;
    private DummyArena arena;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI dummyButtonLabel;

    private GameObject loadoutPanel;
    private RectTransform listContent;
    private TextMeshProUGUI playerLevelLabel;
    private TMP_InputField playerLevelInput;
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<WeaponRow> rows = new List<WeaponRow>();

    private TestSceneLoadout.Category activeTab = TestSceneLoadout.Category.Weapons;
    private bool rowsDirty = true;

    private sealed class WeaponRow
    {
        public Weapon weapon;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI levelLabel;
    }

    void Start()
    {
        meter = DamageMeter.Instance;
        if (meter == null)
        {
            meter = gameObject.AddComponent<DamageMeter>();
        }

        // Nachrüsten, falls die Szene noch vor der Dummy-Arena gebaut wurde.
        arena = FindFirstObjectByType<DummyArena>();
        if (arena == null)
        {
            arena = gameObject.AddComponent<DummyArena>();
        }

        BuildUi();
        SetLoadoutVisible(startWithLoadoutOpen);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleLoadoutKey))
        {
            SetLoadoutVisible(loadoutPanel != null && !loadoutPanel.activeSelf);
        }

        RefreshStats();

        if (loadoutPanel != null && loadoutPanel.activeSelf)
        {
            if (rowsDirty)
            {
                RebuildRows();
            }
            RefreshRows();
        }
    }

    // ==================================================================
    // Anzeige
    // ==================================================================

    private void RefreshStats()
    {
        if (statsText == null || meter == null)
        {
            return;
        }

        string critTag = meter.lastHitWasCrit ? "  <color=#FFD24A>CRIT</color>" : string.Empty;

        PlayerController player = PlayerController.Instance;
        float critChance = player != null ? player.critChance : 0f;
        float critDamage = player != null ? player.critDamage : 0f;
        float damageMultiplier = player != null ? player.damageMultiplier : 1f;

        statsText.text =
            $"Crit Chance  <color=#FFD24A>{critChance * 100f:0.#} %</color>\n" +
            $"Crit Damage  <color=#FFD24A>{critDamage:0.##} x</color>\n" +
            $"Damage Mult. <color=#FFD24A>{damageMultiplier:0.##} x</color>\n" +
            "<color=#666666>――――――――――</color>\n" +
            $"<b>Last Hit</b>   {Num(meter.lastHit)}{critTag}\n" +
            $"<b>DPS</b> ({meter.dpsWindow:0}s)  {Num(meter.Dps)}\n" +
            $"<b>DPM</b> ({meter.dpmWindow:0}s) {Num(meter.Dpm)}\n" +
            "<color=#666666>――――――――――</color>\n" +
            $"Total   {Num(meter.totalDamage)}  in {Clock(meter.Elapsed)}\n" +
            $"Ø DPS   {Num(meter.AverageDps)}\n" +
            $"Hits    {meter.hitCount}  ({meter.CritRate * 100f:0}% Crit)";

        if (dummyButtonLabel != null && arena != null)
        {
            dummyButtonLabel.text = "Dummies: " + arena.CurrentCount;
        }
    }

    private static string Num(float value)
    {
        if (Mathf.Abs(value) >= 100f)
        {
            return value.ToString("#,##0", NumberFormat);
        }
        return value.ToString("#,##0.0", NumberFormat);
    }

    private static string Clock(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }

    // ==================================================================
    // Waffenliste
    // ==================================================================

    private void RebuildRows()
    {
        rowsDirty = false;
        rows.Clear();

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        PlayerController player = PlayerController.Instance;
        foreach (Weapon weapon in TestSceneLoadout.Group(player, activeTab))
        {
            if (weapon != null)
            {
                rows.Add(BuildRow(weapon, player));
            }
        }

        for (int i = 0; i < tabButtons.Count; i++)
        {
            ColorBlock colors = tabButtons[i].colors;
            colors.normalColor = i == (int)activeTab
                ? new Color(0.25f, 0.28f, 0.34f, 1f)
                : new Color(0.13f, 0.14f, 0.17f, 1f);
            tabButtons[i].colors = colors;
        }
    }

    private WeaponRow BuildRow(Weapon weapon, PlayerController player)
    {
        GameObject row = UiObject("Row " + weapon.name, listContent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        row.AddComponent<LayoutElement>().minHeight = 34f;
        Image background = row.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.04f);

        TextMeshProUGUI nameLabel = Label(weapon.name, row.transform, fontSize - 4, TextAlignmentOptions.MidlineLeft);
        LayoutElement nameLayout = nameLabel.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = 210f;
        nameLayout.flexibleWidth = 1f;

        TextMeshProUGUI levelLabel = Label("--", row.transform, fontSize - 4, TextAlignmentOptions.Midline);
        levelLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 92f;

        Weapon captured = weapon;
        MakeButton("–", row.transform, 34f, () => TestSceneLoadout.StepDown(captured));
        MakeButton("+", row.transform, 34f, () => TestSceneLoadout.StepUp(player, captured));
        MakeButton("MAX", row.transform, 56f, () => StartCoroutine(TestSceneLoadout.MaxOut(player, captured)));
        MakeButton("X", row.transform, 34f, () => TestSceneLoadout.Remove(captured));

        return new WeaponRow { weapon = weapon, nameLabel = nameLabel, levelLabel = levelLabel };
    }

    private void RefreshRows()
    {
        foreach (WeaponRow row in rows)
        {
            if (row.weapon == null)
            {
                continue;
            }

            int max = TestSceneLoadout.MaxLevelOf(row.weapon);
            bool owned = TestSceneLoadout.IsOwned(row.weapon);

            // Im Spiel wird Stufe 0 als "Level 1" angezeigt – hier genauso.
            row.levelLabel.text = owned
                ? $"<color=#FFD24A>Lv {row.weapon.weaponLevel + 1}</color> / {max + 1}"
                : $"<color=#777777>– / {max + 1}</color>";

            row.nameLabel.color = owned ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        PlayerController player = PlayerController.Instance;
        if (playerLevelLabel != null && player != null)
        {
            playerLevelLabel.text = $"Player Level  <color=#FFD24A>{player.currentLevel}</color>" +
                                    $"     Reroll <color=#FFD24A>{player.rerollAmount}</color>" +
                                    $"     Banish <color=#FFD24A>{player.banishAmount}</color>";
        }
    }

    // ==================================================================
    // Aktionen
    // ==================================================================

    private void GiveLevel()
    {
        TestSceneLoadout.GrantLevelUp(PlayerController.Instance);
    }

    private void GiveReroll()
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.rerollAmount++;
            UIController.Instance?.RefreshRerollandBanish();
        }
    }

    private void GiveBanish()
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.banishAmount++;
            UIController.Instance?.RefreshRerollandBanish();
        }
    }

    private void CycleDummies()
    {
        if (arena != null)
        {
            arena.Cycle();
            // Messung zurücksetzen: alte und neue Gruppengröße zusammenzurechnen
            // würde den DPS-Vergleich wertlos machen.
            meter.ResetMeter();
        }
    }

    private void RecenterDummies()
    {
        if (arena != null)
        {
            arena.Rearrange();
        }
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ApplyPlayerLevelInput()
    {
        if (playerLevelInput == null)
        {
            return;
        }

        if (int.TryParse(playerLevelInput.text, out int level))
        {
            TestSceneLoadout.SetPlayerLevel(PlayerController.Instance, level);
        }
    }

    private IEnumerator MaxOutAll()
    {
        PlayerController player = PlayerController.Instance;
        foreach (Weapon weapon in TestSceneLoadout.Group(player, activeTab))
        {
            yield return TestSceneLoadout.MaxOut(player, weapon);
        }
    }

    private void SetLoadoutVisible(bool visible)
    {
        if (loadoutPanel == null)
        {
            return;
        }

        loadoutPanel.SetActive(visible);
        if (visible)
        {
            rowsDirty = true;
        }
    }

    private void SwitchTab(TestSceneLoadout.Category category)
    {
        activeTab = category;
        rowsDirty = true;
    }

    // ==================================================================
    // UI-Aufbau
    // ==================================================================

    private void BuildUi()
    {
        GameObject canvasObject = UiObject("Test Scene Canvas", null);
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // immer über dem Spiel-UI
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        BuildStatsPanel(canvasObject.transform);
        BuildLoadoutPanel(canvasObject.transform);
    }

    private void BuildStatsPanel(Transform parent)
    {
        RectTransform panel = Panel("Stats", parent, panelColor);

        // Unten links: oben links liegen im Spiel-UI schon Lebensbalken und
        // Waffen-Slots. Der Y-Abstand hält Abstand zur XP-Leiste, die unten
        // über die volle Breite läuft (74 px hoch bei 1920x1080).
        Anchor(panel, new Vector2(0f, 0f), new Vector2(16f, 90f));
        panel.sizeDelta = new Vector2(360f, 0f);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI title = Label("TEST SCENE", panel, fontSize - 6, TextAlignmentOptions.MidlineLeft);
        title.color = accentColor;
        title.characterSpacing = 8f;

        statsText = Label(string.Empty, panel, fontSize - 3, TextAlignmentOptions.TopLeft);
        statsText.lineSpacing = 6f;

        // Buttonreihen
        GameObject row1 = Row(panel);
        MakeButton("+1 Level", row1.transform, 100f, GiveLevel);
        MakeButton("+1 Reroll", row1.transform, 104f, GiveReroll);
        MakeButton("+1 Banish", row1.transform, 108f, GiveBanish);

        GameObject row2 = Row(panel);
        MakeButton("Restart Scene", row2.transform, 166f, RestartScene);
        MakeButton("Reset Meter", row2.transform, 154f, () => meter.ResetMeter());

        GameObject row3 = Row(panel);
        Button dummyButton = MakeButton("Dummies: 1", row3.transform, 172f, CycleDummies);
        dummyButtonLabel = dummyButton.GetComponentInChildren<TextMeshProUGUI>();
        MakeButton("Re-Center", row3.transform, 148f, RecenterDummies);

        hintText = Label($"[{toggleLoadoutKey}] Waffen-Menü   [ESC] Pause", panel, fontSize - 8,
            TextAlignmentOptions.MidlineLeft);
        hintText.color = new Color(0.6f, 0.62f, 0.66f, 1f);
    }

    private void BuildLoadoutPanel(Transform parent)
    {
        RectTransform panel = Panel("Loadout", parent, panelColor);
        loadoutPanel = panel.gameObject;
        Anchor(panel, new Vector2(1f, 1f), new Vector2(-16f, -16f));
        panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(520f, 640f);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        TextMeshProUGUI title = Label("LOADOUT", panel, fontSize - 6, TextAlignmentOptions.MidlineLeft);
        title.color = accentColor;
        title.characterSpacing = 8f;

        // Reiter
        GameObject tabs = Row(panel);
        tabButtons.Clear();
        tabButtons.Add(MakeButton("Weapons", tabs.transform, 150f,
            () => SwitchTab(TestSceneLoadout.Category.Weapons)));
        tabButtons.Add(MakeButton("Buffs", tabs.transform, 150f,
            () => SwitchTab(TestSceneLoadout.Category.Buffs)));
        tabButtons.Add(MakeButton("Evos", tabs.transform, 150f,
            () => SwitchTab(TestSceneLoadout.Category.Evos)));

        // Scrollbare Liste
        GameObject scrollObject = UiObject("Scroll View", panel);
        scrollObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 26f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = UiObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect);
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = viewportRect;

        GameObject content = UiObject("Content", viewport.transform);
        listContent = (RectTransform)content.transform;
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = Vector2.zero;
        listContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 3f;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = listContent;

        // Sammelaktionen
        GameObject bulk = Row(panel);
        MakeButton("Alle auf MAX", bulk.transform, 180f, () => StartCoroutine(MaxOutAll()));
        MakeButton("Alle entfernen", bulk.transform, 180f,
            () => TestSceneLoadout.ClearAll(PlayerController.Instance));

        // Spieler-Level
        playerLevelLabel = Label(string.Empty, panel, fontSize - 5, TextAlignmentOptions.MidlineLeft);

        GameObject levelRow = Row(panel);
        MakeButton("–", levelRow.transform, 44f, () => ShiftPlayerLevel(-1));
        MakeButton("+", levelRow.transform, 44f, () => ShiftPlayerLevel(1));
        playerLevelInput = MakeInput(levelRow.transform, 96f);
        MakeButton("Set", levelRow.transform, 74f, ApplyPlayerLevelInput);

        TextMeshProUGUI note = Label(
            "Hinweis: Buff-Boni werden beim Herunterstufen nicht zurückgenommen –\n" +
            "dafür die Szene neu starten.",
            panel, fontSize - 9, TextAlignmentOptions.TopLeft);
        note.color = new Color(0.6f, 0.62f, 0.66f, 1f);
    }

    private void ShiftPlayerLevel(int delta)
    {
        PlayerController player = PlayerController.Instance;
        if (player != null)
        {
            TestSceneLoadout.SetPlayerLevel(player, player.currentLevel + delta);
        }
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            return;
        }

        UnityEngine.EventSystems.EventSystem existing =
            FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        GameObject go = new GameObject("EventSystem (Test)");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ------------------------------------------------------------------
    // kleine UI-Bausteine
    // ------------------------------------------------------------------

    private static GameObject UiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private RectTransform Panel(string name, Transform parent, Color color)
    {
        GameObject go = UiObject(name, parent);
        go.AddComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    private GameObject Row(Transform parent)
    {
        GameObject go = UiObject("Row", parent);
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        go.AddComponent<LayoutElement>().minHeight = 36f;
        return go;
    }

    private TextMeshProUGUI Label(string text, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject go = UiObject("Label", parent);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.richText = true;
        label.raycastTarget = false;
        return label;
    }

    private Button MakeButton(string text, Transform parent, float width, UnityAction onClick)
    {
        GameObject go = UiObject("Button " + text, parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.21f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.17f, 0.21f, 1f);
        colors.highlightedColor = new Color(0.26f, 0.28f, 0.34f, 1f);
        colors.pressedColor = new Color(0.38f, 0.40f, 0.48f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minHeight = 32f;

        TextMeshProUGUI label = Label(text, go.transform, fontSize - 6, TextAlignmentOptions.Midline);
        Stretch((RectTransform)label.transform);

        return button;
    }

    private TMP_InputField MakeInput(Transform parent, float width)
    {
        GameObject go = UiObject("Input", parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.10f, 0.11f, 0.14f, 1f);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minHeight = 32f;

        GameObject textArea = UiObject("Text Area", go.transform);
        RectTransform textAreaRect = (RectTransform)textArea.transform;
        Stretch(textAreaRect);
        textAreaRect.offsetMin = new Vector2(8f, 4f);
        textAreaRect.offsetMax = new Vector2(-8f, -4f);
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI text = Label(string.Empty, textArea.transform, fontSize - 6, TextAlignmentOptions.MidlineLeft);
        Stretch((RectTransform)text.transform);

        TMP_InputField input = go.AddComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.text = "1";

        return input;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = offset;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
