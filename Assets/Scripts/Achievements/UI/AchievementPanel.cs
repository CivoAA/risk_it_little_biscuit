using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Achievement-Anzeige im Stil von "UI 2.0" - dieselbe dunkle Karte, das
/// Titelband und die Holzknoepfe wie <see cref="OptionsPanel"/> und
/// <see cref="PauseMenuPanel"/>.
///
///   AchievementPanel.Toggle();
///
/// Aufbau (Seitenpixel, 480x270):
///   Titelband   ERFOLGE
///   Reiter      eine Kategorie je Knopf (aktiv = Marmelade)
///   links       Liste der Kategorie: Symbol, Name, Haken bzw. "3/10"
///   rechts      das gewaehlte Achievement: Bild, Name, Beschreibung,
///               Fortschrittsbalken, Belohnung
///   Fussleiste  ZURUECK  ·  "12 / 43" mit Gesamtbalken
///
/// Bedienung: Maus, Pfeiltasten/WASD (hoch/runter = Eintrag, links/rechts =
/// Reiter), Mausrad, ESC.
///
/// Baut sich komplett per Code auf und laesst sich aus jeder Szene oeffnen.
/// Inhalt und Reihenfolge kommen aus <see cref="Ach"/> - dieses Skript kennt
/// kein einziges Achievement beim Namen.
/// </summary>
public class AchievementPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int CardX = 40, CardY = 40, CardW = 400, CardH = 208;
    private const int RibbonY = 28;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int InX = CardX + 14, InW = CardW - 28;
    private const int TabY = 55, TabH = 16, TabGap = 3;

    private const int ListX = InX, ListY = 77, ListW = 184, ListH = 136;
    private const int RowH = 19, RowStep = 20, RowIcon = 16;
    private const int ViewPad = 4;

    private const int DetX = ListX + ListW + 6, DetY = ListY, DetW = InX + InW - (ListX + ListW + 6), DetH = ListH;
    private const int PlateS = 40, IconS = 32;

    private const int FootY = 223, FootH = 18, BackW = 76;

    private static readonly Color LockedTint = new Color(0.42f, 0.38f, 0.42f, 1f);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static AchievementPanel instance;
    public static bool IsOpen => instance != null;

    /// <summary>Reiter, der beim naechsten Oeffnen vorne liegt.</summary>
    private static int startTab;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private int openedFrame;

    private readonly List<AchievementCategory> categories = new List<AchievementCategory>();
    private SkinButton[] tabs;
    private int tab;

    private RectTransform viewport, listContent;
    private Image scrollHandle;
    private float scrollTop;
    private readonly List<RowView> rows = new List<RowView>();
    private int selected;
    private int hovered = -1;

    private Image detailPlate, detailIcon, detailCheck;
    private TextMeshProUGUI detailName, detailCategory, detailDesc, detailProgressText, detailReward;
    private Image detailBarFrame, detailBarFill;

    private TextMeshProUGUI counter;
    private Image totalFill;

    private class RowView
    {
        public AchievementDef Def;
        public Image Back;
        public Image Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Progress;
        public Image Check;
        public int Index;
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

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
        openedFrame = Time.frameCount;
        // Katalogtexte haben Umlaute - ThaleahFat nur fuer den Titel.
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        Build();
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Rebuild;
        Achievements.Unlocked += OnUnlocked;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Rebuild;
        Achievements.Unlocked -= OnUnlocked;
    }

    private void OnDestroy()
    {
        startTab = tab;
        if (instance == this) instance = null;
    }

    private void OnUnlocked(AchievementDef def) => Refresh();

    // ==================================================================
    //  Eingabe
    // ==================================================================

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (page != null && size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab))
            StepTab(1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) StepRow(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) StepRow(1);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Scroll(-wheel * RowStep);
    }

    private void StepTab(int delta)
    {
        if (categories.Count == 0) return;
        OptionsKit.PlayClick();
        SetTab((tab + delta + categories.Count) % categories.Count);
    }

    private void StepRow(int delta)
    {
        if (rows.Count == 0) return;
        OptionsKit.PlayClick();
        Select(Mathf.Clamp(selected + delta, 0, rows.Count - 1), true);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Rebuild()
    {
        if (page == null) return;
        OptionsKit.Clear(transform);
        textFont = PixelUI.FindTextFont();
        Build();
    }

    private void Build()
    {
        // Ueber dem Hauptmenue, unter Pause (200) und Optionen (210).
        page = OptionsKit.CreatePage(gameObject, 150, 0.88f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.achievements.title", "ERFOLGE"),
                          pixelFont, textFont);

        BuildTabs();
        BuildList();
        BuildDetail();
        BuildFooter();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        SetTab(Mathf.Clamp(startTab, 0, Mathf.Max(0, categories.Count - 1)));
    }

    /// <summary>Ein Reiter je Kategorie, die mindestens ein Achievement hat. Alle gleich breit.</summary>
    private void BuildTabs()
    {
        categories.Clear();
        foreach (AchievementCategory c in System.Enum.GetValues(typeof(AchievementCategory)))
            foreach (AchievementDef def in Ach.All)
                if (def.Category == c) { categories.Add(c); break; }

        int n = Mathf.Max(1, categories.Count);
        int w = (InW - (n - 1) * TabGap) / n;
        int x0 = InX + (InW - (n * w + (n - 1) * TabGap)) / 2;

        tabs = new SkinButton[categories.Count];
        for (int i = 0; i < categories.Count; i++)
        {
            int index = i;
            AchievementCategory c = categories[i];
            tabs[i] = SkinButton.Create(page, x0 + i * (w + TabGap), TabY, w, TabH,
                                        Loc.Get($"ach.cat.{c}", c.ToString().ToUpperInvariant()),
                                        textFont, SkinButton.Kind.Wood, () => SetTab(index));
        }
    }

    private void BuildList()
    {
        OptionsKit.Img("ListWell", page, ListX, ListY, ListW, ListH, GameHudSkin.Well, true);

        viewport = OptionsKit.Rect("Viewport", page, ListX + ViewPad, ListY + ViewPad,
                                   ListW - 2 * ViewPad - 4, ListH - 2 * ViewPad);
        viewport.gameObject.AddComponent<RectMask2D>();
        listContent = OptionsKit.Rect("Content", viewport, 0, 0, ListW - 2 * ViewPad - 4, ListH - 2 * ViewPad);

        scrollHandle = OptionsKit.Img("ScrollHandle", page, ListX + ListW - 5, ListY + ViewPad, 2, 10,
                                      GameHudSkin.White, GameHudSkin.Stone);
    }

    private void BuildDetail()
    {
        OptionsKit.Img("DetailWell", page, DetX, DetY, DetW, DetH, GameHudSkin.Well, true);

        int cx = DetX + DetW / 2;
        int plateX = cx - PlateS / 2, plateY = DetY + 8;
        detailPlate = OptionsKit.Img("Plate", page, plateX, plateY, PlateS, PlateS, GameHudSkin.IconPlate(false));
        detailIcon = OptionsKit.Img("Icon", page, plateX + (PlateS - IconS) / 2, plateY + (PlateS - IconS) / 2,
                                    IconS, IconS, null);
        detailIcon.preserveAspect = true;

        Sprite check = GameHudSkin.Check;
        detailCheck = OptionsKit.Img("Check", page, plateX + PlateS - check.rect.width + 2, plateY - 2,
                                     check.rect.width, check.rect.height, check);

        int tx = DetX + 8, tw = DetW - 16;
        detailName = OptionsKit.Label("Name", page, tx, DetY + 52, tw, 13, "", textFont, OptionsKit.SizeText,
                                      GameHudSkin.Gold, TextAlignmentOptions.Center);
        detailCategory = OptionsKit.Label("Category", page, tx, DetY + 64, tw, 13, "", textFont,
                                          OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);
        detailDesc = OptionsKit.Label("Desc", page, tx, DetY + 79, tw, 28, "", textFont, OptionsKit.SizeText,
                                      GameHudSkin.Parchment, TextAlignmentOptions.Top);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;

        int barW = tw - 30;
        detailBarFrame = OptionsKit.Img("BarFrame", page, tx, DetY + 111, barW, 8, GameHudSkin.BarFrame, true);
        detailBarFill = OptionsKit.Img("BarFill", page, tx + 1, DetY + 112, 0, 6, GameHudSkin.White,
                                       GameHudSkin.Gold);
        detailProgressText = OptionsKit.Label("Progress", page, tx + barW + 2, DetY + 108, tw - barW - 2, 13,
                                              "", textFont, OptionsKit.SizeText, GameHudSkin.Cream,
                                              TextAlignmentOptions.Right);

        detailReward = OptionsKit.Label("Reward", page, tx, DetY + 120, tw, 13, "", textFont, OptionsKit.SizeText,
                                        GameHudSkin.Mint, TextAlignmentOptions.Center);
    }

    private void BuildFooter()
    {
        SkinButton.Create(page, InX, FootY, BackW, FootH, Loc.Get("ui.achievements.close", "ZURÜCK"),
                          textFont, SkinButton.Kind.Wood, Close);

        // Gesamtstand rechts: Zahl und Balken.
        const int barW = 150;
        int barX = InX + InW - barW;
        OptionsKit.Img("TotalFrame", page, barX, FootY + 5, barW, 8, GameHudSkin.BarFrame, true);
        totalFill = OptionsKit.Img("TotalFill", page, barX + 1, FootY + 6, 0, 6, GameHudSkin.White, GameHudSkin.Gold);

        Sprite star = GameHudSkin.Star;
        OptionsKit.Img("Star", page, barX - 60, FootY + Mathf.Round((FootH - star.rect.height) / 2f),
                       star.rect.width, star.rect.height, star);
        counter = OptionsKit.Label("Counter", page, barX - 48, FootY + 2, 44, 13, "", textFont,
                                   OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Right);
    }

    // ==================================================================
    //  Liste
    // ==================================================================

    private void SetTab(int index)
    {
        if (categories.Count == 0) return;
        tab = Mathf.Clamp(index, 0, categories.Count - 1);
        for (int i = 0; i < tabs.Length; i++) tabs[i].Active = i == tab;

        OptionsKit.Clear(listContent);
        rows.Clear();
        hovered = -1;

        AchievementCategory c = categories[tab];
        foreach (AchievementDef def in Ach.All)
            if (def.Category == c) AddRow(def);

        float h = rows.Count * RowStep - (rows.Count > 0 ? RowStep - RowH : 0);
        listContent.sizeDelta = new Vector2(listContent.sizeDelta.x, Mathf.Max(ViewH, h));
        scrollTop = 0f;
        listContent.anchoredPosition = Vector2.zero;

        Refresh();
        Select(0, true);
    }

    private float ViewH => ListH - 2 * ViewPad;
    private float ContentHeight => rows.Count * RowStep - (rows.Count > 0 ? RowStep - RowH : 0);

    private void AddRow(AchievementDef def)
    {
        int index = rows.Count;
        float w = listContent.sizeDelta.x;
        RectTransform row = OptionsKit.Rect("Row_" + def.Id, listContent, 0, index * RowStep, w, RowH);

        Image back = row.gameObject.AddComponent<Image>();
        back.sprite = GameHudSkin.White;
        back.raycastTarget = true;

        Image icon = OptionsKit.Img("Icon", row, 3, (RowH - RowIcon) / 2, RowIcon, RowIcon, null);
        icon.preserveAspect = true;

        TextMeshProUGUI label = OptionsKit.Label("Label", row, RowIcon + 7, 2, w - RowIcon - 40, RowH - 3, "",
                                                 textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                                 TextAlignmentOptions.Left);
        label.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI progress = OptionsKit.Label("Progress", row, w - 38, 2, 34, RowH - 3, "", textFont,
                                                    OptionsKit.SizeText, GameHudSkin.Stone,
                                                    TextAlignmentOptions.Right);

        Sprite c = GameHudSkin.Check;
        Image check = OptionsKit.Img("Check", row, w - 4 - c.rect.width, Mathf.Round((RowH - c.rect.height) / 2f),
                                     c.rect.width, c.rect.height, c);

        var view = new RowView
        {
            Def = def, Back = back, Icon = icon, Label = label, Progress = progress, Check = check, Index = index,
        };
        rows.Add(view);

        PointerRelay relay = row.gameObject.AddComponent<PointerRelay>();
        relay.Enter = () => { hovered = view.Index; PaintRows(); };
        relay.Exit = () => { if (hovered == view.Index) hovered = -1; PaintRows(); };
        relay.Down = () =>
        {
            OptionsKit.PlayClick();
            Select(view.Index, false);
        };
    }

    private void Select(int index, bool scrollIntoView)
    {
        if (rows.Count == 0)
        {
            ShowDetail(null);
            return;
        }

        selected = Mathf.Clamp(index, 0, rows.Count - 1);
        PaintRows();
        ShowDetail(rows[selected].Def);

        if (scrollIntoView)
        {
            float top = selected * RowStep, bottom = top + RowH;
            if (top < scrollTop) SetScroll(top);
            else if (bottom > scrollTop + ViewH) SetScroll(bottom - ViewH);
        }
    }

    private void Scroll(float delta) => SetScroll(scrollTop + delta);

    private void SetScroll(float value)
    {
        float hidden = Mathf.Max(0f, ContentHeight - ViewH);
        scrollTop = Mathf.Clamp(value, 0f, hidden);
        // Ganze Pixel - sonst verschmiert die Liste beim Hochskalieren.
        listContent.anchoredPosition = new Vector2(0f, Mathf.Round(scrollTop));
        UpdateScrollBar();
    }

    private void UpdateScrollBar()
    {
        float hidden = Mathf.Max(0f, ContentHeight - ViewH);
        scrollHandle.enabled = hidden > 0f;
        if (hidden <= 0f) return;

        float size = Mathf.Max(8f, Mathf.Round(ViewH * (ViewH / ContentHeight)));
        float y = ListY + ViewPad + Mathf.Round((ViewH - size) * (scrollTop / hidden));
        OptionsKit.Move(scrollHandle.rectTransform, ListX + ListW - 5, y, 2, size);
    }

    /// <summary>Zebra, Maus und Auswahl: gewaehlt = Holz mit Goldkante, Maus = eine Stufe heller.</summary>
    private void PaintRows()
    {
        foreach (RowView r in rows)
        {
            Color c;
            if (r.Index == selected) c = GameHudSkin.WoodMid;
            else if (r.Index == hovered) c = GameHudSkin.StoneDark;
            else if (r.Index % 2 == 0) c = GameHudSkin.CardFill;
            else c = new Color(0f, 0f, 0f, 0f);
            r.Back.color = c;
        }
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    public void Refresh()
    {
        int unlocked = Achievements.UnlockedCount, total = Achievements.TotalCount;
        counter.text = string.Format(Loc.Get("ui.achievements.counter", "{0} / {1}"), unlocked, total);
        SetFill(totalFill, 148, total > 0 ? unlocked / (float)total : 0f);

        foreach (RowView r in rows)
        {
            AchievementDef def = r.Def;
            bool done = def.IsUnlocked;

            r.Icon.sprite = def.Icon;
            r.Icon.color = done ? Color.white : LockedTint;
            r.Label.text = def.Name;
            r.Label.color = done ? GameHudSkin.Cream : GameHudSkin.StoneLight;
            r.Check.enabled = done;
            r.Progress.text = !done && def.HasProgress
                ? $"{Mathf.FloorToInt(Mathf.Min(def.Value, def.Goal))}/{Mathf.FloorToInt(def.Goal)}"
                : "";
        }

        PaintRows();
        if (rows.Count > 0) ShowDetail(rows[Mathf.Clamp(selected, 0, rows.Count - 1)].Def);
        UpdateScrollBar();
    }

    private void ShowDetail(AchievementDef def)
    {
        bool has = def != null;
        detailPlate.enabled = has;
        detailIcon.enabled = has;
        detailBarFrame.enabled = detailBarFill.enabled = false;
        detailProgressText.text = "";

        if (!has)
        {
            detailName.text = Loc.Get("ui.achievements.empty", "Hier ist noch nichts.");
            detailName.color = GameHudSkin.Stone;
            detailCategory.text = detailDesc.text = detailReward.text = "";
            detailCheck.enabled = false;
            return;
        }

        bool done = def.IsUnlocked;

        detailIcon.sprite = def.Icon;
        detailIcon.color = done ? Color.white : LockedTint;
        detailCheck.enabled = done;

        detailName.text = def.Name.ToUpperInvariant();
        detailName.color = done ? GameHudSkin.Gold : GameHudSkin.Cream;
        detailCategory.text = Loc.Get($"ach.cat.{def.Category}", def.Category.ToString().ToUpperInvariant());
        detailDesc.text = def.Description;

        if (!done && def.HasProgress)
        {
            detailBarFrame.enabled = detailBarFill.enabled = true;
            float w = detailBarFrame.rectTransform.sizeDelta.x - 2f;
            SetFill(detailBarFill, w, Mathf.Clamp01(def.Value / def.Goal));
            detailProgressText.text = $"{Mathf.FloorToInt(Mathf.Min(def.Value, def.Goal))}/{Mathf.FloorToInt(def.Goal)}";
        }

        if (done)
        {
            detailReward.text = Loc.Get("ui.achievements.unlocked", "Freigeschaltet");
            detailReward.color = GameHudSkin.Mint;
        }
        else
        {
            detailReward.text = "";
        }
    }

    /// <summary>Fuellbalken auf ganze Pixel: Breite = Anteil der Innenbreite.</summary>
    private static void SetFill(Image fill, float innerWidth, float t)
    {
        RectTransform rt = fill.rectTransform;
        float w = Mathf.Round(innerWidth * Mathf.Clamp01(t));
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        fill.enabled = w > 0f;
    }
}
