using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Die Achievement-Anzeige. Baut sich komplett per Code auf - kein Szenenobjekt,
/// kein Prefab, keine Verdrahtung im Inspector. Dadurch lässt sie sich aus jeder
/// Szene heraus öffnen: Hauptmenü, Hub, World Map, im Spiel.
///
///   AchievementPanel.Toggle();
///
/// Aufbau wie im Shop: links die Liste zum Durchscrollen, rechts das ausgewählte
/// Achievement mit Bild, Name, Beschreibung, Fortschritt und Belohnung. Gemessen
/// wird im 320x180-Raster, damit die Kanten auf ganzen Pixeln sitzen.
///
/// Inhalt und Reihenfolge kommen aus <see cref="Ach"/> - dieses Skript kennt kein
/// einziges Achievement beim Namen.
/// </summary>
public class AchievementPanel : MonoBehaviour
{
    private const int RefWidth = 320;
    private const int RefHeight = 180;

    private const float SizeTitle = 12f;
    private const float SizeText = 8f;
    private const float SizeSmall = 6f;

    private const float ListX = 6f;
    private const float ListWidth = 136f;
    private const float DetailX = 148f;
    private const float DetailWidth = 166f;
    private const float TopY = -20f;
    private const float BodyHeight = 138f;

    private const float RowHeight = 16f;
    private const float RowIcon = 12f;

    private static AchievementPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset font;
    private RectTransform listContent;

    private readonly List<Row> rows = new List<Row>();
    private AchievementDef selected;

    private Image detailIcon;
    private TMP_Text detailName;
    private TMP_Text detailDesc;
    private TMP_Text detailProgress;
    private TMP_Text detailReward;
    private TMP_Text counter;

    private class Row
    {
        public AchievementDef Def;
        public Image Frame;
        public Image Icon;
        public TMP_Text Label;
    }

    // ---------- Öffnen / Schliessen ----------

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("AchievementPanel");
        instance = go.AddComponent<AchievementPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void Awake()
    {
        instance = this;
        font = PixelUI.FindPixelFont();
        Build();
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Refresh;
        Achievements.Unlocked += OnUnlocked;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Refresh;
        Achievements.Unlocked -= OnUnlocked;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnUnlocked(AchievementDef def) => Refresh();

    // ---------- Aufbau ----------

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Image backdrop = PixelUI.Panel("Backdrop", transform, Vector2.zero, Vector2.zero, PixelUI.Backdrop);
        RectTransform br = backdrop.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = Vector2.zero;
        br.anchoredPosition = Vector2.zero;
        backdrop.raycastTarget = true;

        BuildHeader();
        BuildList();
        BuildDetail();

        Refresh();
        Select(FirstDef());
    }

    private void BuildHeader()
    {
        PixelUI.Label("Title", transform, new Vector2(RefWidth, SizeTitle), new Vector2(0f, -4f),
                      Loc.Get("ui.achievements.title", "ACHIEVEMENTS"), SizeTitle, PixelUI.TextAccent,
                      TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));

        counter = PixelUI.Label("Counter", transform, new Vector2(60f, 10f), new Vector2(-6f, -5f),
                                "", SizeText, PixelUI.TextDim, TextAlignmentOptions.Right, font,
                                new Vector2(1f, 1f));

        Button back = PixelUI.TextButton("Back", transform, new Vector2(44f, 11f), new Vector2(6f, -4f),
                                         Loc.Get("ui.achievements.close", "BACK"), SizeSmall, font,
                                         new Vector2(0f, 1f));
        back.onClick.AddListener(() =>
        {
            PlayClick();
            Close();
        });
    }

    private void BuildList()
    {
        RectTransform frame = PixelUI.Framed("List", transform, new Vector2(ListWidth, BodyHeight),
                                             new Vector2(ListX, TopY), PixelUI.PanelFill, PixelUI.Outline,
                                             new Vector2(0f, 1f));
        frame.GetComponent<Image>().raycastTarget = true;

        RectTransform viewport = PixelUI.Rect("Viewport", frame, new Vector2(ListWidth - 4f, BodyHeight - 4f),
                                              Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();

        listContent = PixelUI.Rect("Content", viewport, new Vector2(ListWidth - 4f, 0f), Vector2.zero,
                                   new Vector2(0.5f, 1f));

        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 1f;
        layout.padding = new RectOffset(1, 1, 1, 1);

        ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 12f;

        BuildRows();
    }

    private void BuildRows()
    {
        AchievementCategory? lastCategory = null;

        foreach (AchievementDef def in SortedDefs())
        {
            if (lastCategory != def.Category)
            {
                lastCategory = def.Category;
                AddHeader(def.Category);
            }

            AddRow(def);
        }
    }

    private void AddHeader(AchievementCategory category)
    {
        RectTransform holder = PixelUI.Rect("Header_" + category, listContent,
                                            new Vector2(ListWidth - 6f, 10f), Vector2.zero);
        AddLayoutHeight(holder, 10f);

        PixelUI.Label("Text", holder, new Vector2(ListWidth - 8f, 10f), new Vector2(2f, 0f),
                      Loc.Get($"ach.cat.{category}", category.ToString().ToUpperInvariant()),
                      SizeSmall, PixelUI.TextDim, TextAlignmentOptions.Left, font, new Vector2(0f, 0.5f));
    }

    private void AddRow(AchievementDef def)
    {
        Image frame = PixelUI.Panel("Row_" + def.Id, listContent, new Vector2(ListWidth - 6f, RowHeight),
                                    Vector2.zero, PixelUI.RowFill);
        frame.raycastTarget = true;
        AddLayoutHeight(frame.rectTransform, RowHeight);

        Image icon = PixelUI.Panel("Icon", frame.transform, new Vector2(RowIcon, RowIcon),
                                   new Vector2(2f, 0f), Color.white, new Vector2(0f, 0.5f));
        icon.preserveAspect = true;

        TMP_Text label = PixelUI.Label("Label", frame.transform,
                                       new Vector2(ListWidth - RowIcon - 12f, RowHeight),
                                       new Vector2(RowIcon + 5f, 0f), "", SizeSmall, PixelUI.TextNormal,
                                       TextAlignmentOptions.Left, font, new Vector2(0f, 0.5f));
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;

        AchievementDef captured = def;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            Select(captured);
        });

        rows.Add(new Row { Def = def, Frame = frame, Icon = icon, Label = label });
    }

    private static void AddLayoutHeight(RectTransform rt, float height)
    {
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    private void BuildDetail()
    {
        RectTransform frame = PixelUI.Framed("Detail", transform, new Vector2(DetailWidth, BodyHeight),
                                             new Vector2(DetailX, TopY), PixelUI.PanelFill, PixelUI.Outline,
                                             new Vector2(0f, 1f));

        detailIcon = PixelUI.Panel("Icon", frame, new Vector2(32f, 32f), new Vector2(0f, -8f),
                                   Color.white, new Vector2(0.5f, 1f));
        detailIcon.preserveAspect = true;

        detailName = PixelUI.Label("Name", frame, new Vector2(DetailWidth - 12f, 12f),
                                   new Vector2(0f, -44f), "", SizeText, PixelUI.TextAccent,
                                   TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));

        detailDesc = PixelUI.Label("Desc", frame, new Vector2(DetailWidth - 16f, 56f),
                                   new Vector2(0f, -58f), "", SizeSmall, PixelUI.TextNormal,
                                   TextAlignmentOptions.Top, font, new Vector2(0.5f, 1f));
        detailDesc.textWrappingMode = TextWrappingModes.Normal;

        detailProgress = PixelUI.Label("Progress", frame, new Vector2(DetailWidth - 12f, 10f),
                                       new Vector2(0f, -118f), "", SizeSmall, PixelUI.TextDim,
                                       TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));

        detailReward = PixelUI.Label("Reward", frame, new Vector2(DetailWidth - 12f, 10f),
                                     new Vector2(0f, -128f), "", SizeSmall, PixelUI.TextDim,
                                     TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));
    }

    // ---------- Inhalt ----------

    /// <summary>
    /// Nach Kategorie gruppiert, innerhalb der Gruppe in Katalogreihenfolge.
    /// Die Reihenfolge in Ach.cs bestimmt also, wo ein Achievement steht.
    /// </summary>
    private static IEnumerable<AchievementDef> SortedDefs()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            foreach (AchievementDef def in Ach.All)
            {
                if (def.Category == category) yield return def;
            }
        }
    }

    private static AchievementDef FirstDef()
    {
        foreach (AchievementDef def in SortedDefs()) return def;
        return null;
    }

    public void Refresh()
    {
        counter.text = string.Format(Loc.Get("ui.achievements.counter", "{0} / {1}"),
                                     Achievements.UnlockedCount, Achievements.TotalCount);

        foreach (Row row in rows)
        {
            bool unlocked = row.Def.IsUnlocked;

            row.Icon.sprite = row.Def.Icon;
            row.Icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            row.Label.text = row.Def.Name;
            row.Label.color = unlocked ? PixelUI.TextNormal : PixelUI.TextDim;
        }

        UpdateSelectionTint();
        ShowDetail(selected);
    }

    private void Select(AchievementDef def)
    {
        selected = def;
        UpdateSelectionTint();
        ShowDetail(def);
    }

    private void UpdateSelectionTint()
    {
        foreach (Row row in rows)
        {
            row.Frame.color = row.Def == selected ? PixelUI.RowHover : PixelUI.RowFill;
        }
    }

    private void ShowDetail(AchievementDef def)
    {
        if (def == null)
        {
            detailName.text = Loc.Get("ui.achievements.empty", "Nothing here yet.");
            detailDesc.text = "";
            detailProgress.text = "";
            detailReward.text = "";
            detailIcon.enabled = false;
            return;
        }

        bool unlocked = def.IsUnlocked;

        detailIcon.enabled = true;
        detailIcon.sprite = def.Icon;
        detailIcon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);

        detailName.text = def.Name;
        detailName.color = unlocked ? PixelUI.TextAccent : PixelUI.TextDim;
        detailDesc.text = def.Description;

        if (unlocked)
        {
            detailProgress.text = "";
        }
        else if (def.HasProgress)
        {
            detailProgress.text = $"{Mathf.FloorToInt(def.Value)} / {Mathf.FloorToInt(def.Goal)}";
        }
        else
        {
            detailProgress.text = "";
        }

        if (def.Souls > 0 && !unlocked)
        {
            detailReward.text = $"{Loc.Get("ui.achievements.reward", "Reward:")} " +
                                $"+{def.Souls} {Loc.Get("ui.achievements.souls", "Cookie Souls")}";
        }
        else
        {
            detailReward.text = "";
        }
    }

    private static void PlayClick()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.MenuClick != null) audio.PalySound(audio.MenuClick);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }
}
