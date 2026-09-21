using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Das Erfolge-/Unlocks-Buch: ein aufgeschlagenes Kochbuch, das in der gelben
/// Kapselfluessigkeit schwebt. Zwei Reiter, links die Liste, rechts das Detail.
///
///   AchievementsBookPanel.Toggle();
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, keine
/// Verdrahtung im Inspector, genau wie <see cref="AchievementPanel"/> und
/// <see cref="UnlockPanel"/>. Damit laesst es sich aus jeder Szene oeffnen.
///
/// Die Grafik kommt aus Assets/Resources/AchievementsBook/ui/ (siehe
/// Assets/Art/UI_Objects/AchievementsBook/ACHIEVEMENTS_BOOK_UI.md - dort stehen
/// alle Masse, Textzonen und 9-Slice-Raender, nach denen hier gerechnet wird).
/// Jede Zahl in diesem Skript ist ein Pixel im 320x180-Raster, Ursprung oben
/// links. Landet etwas auf einem halben Pixel, verschmiert es beim Hochskalieren.
///
/// Inhalt und Reihenfolge kommen aus den vorhandenen Katalogen <see cref="Ach"/>
/// und <see cref="Unlocks"/> - dieses Skript kennt weder ein Achievement noch
/// einen Unlock beim Namen.
/// </summary>
public class AchievementsBookPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (alle aus ACHIEVEMENTS_BOOK_UI.md)
    // ==================================================================

    private const int RefW = 320;
    private const int RefH = 180;

    private const string UiPath = "AchievementsBook/ui/";
    private const string IconPath = "AchievementsBook/";

    // Buch
    private static readonly Vector4 Book  = new Vector4(16f, 23f, 290f, 144f);
    private static readonly Vector4 Spine = new Vector4(156f, 25f, 8f, 138f);

    // Reiter
    private const float TabX = 16f, TabY = 9f, TabW = 60f, TabH = 14f, TabStep = 62f;

    // Der aktive Reiter reicht so viel tiefer, dass er die Oberkante der
    // Buchseite ueberdeckt. tab_active.png ist genau darum 60x16 gross.
    private const float TabOverlap = 2f;

    // Zurueck-Knopf rechts in derselben Zeile, rechte Kante buendig mit dem Buch
    private static readonly Vector4 Back = new Vector4(251f, 9f, 52f, 14f);

    // Fortschrittsleiste: 10 Zellen, rechts daneben der Zaehler
    private const float BarX = 20f, BarY = 29f, CellW = 9f, CellH = 4f, CellStep = 10f;
    private const int CellCount = 10;
    private static readonly Vector4 Counter = new Vector4(121f, 26f, 34f, 10f);

    // Liste: 6 Zeilen a 21px = 126 = volle Hoehe des Sichtfensters
    private const float ListX = 20f, ListY = 36f, ListW = 132f, ListH = 126f;
    private const float RowH = 21f;
    private const float SlotDX = 1f, SlotDY = 0f, SlotS = 21f;
    private const float RowTextDX = 25f, RowTextW = 94f;

    // Unlocks bekommen statt der Liste ein Kachelgitter: dort zaehlt nur das
    // Symbol, Name und Beschreibung stehen ohnehin rechts. 4 Spalten a 32px
    // passen mit 1px Luecke in die 132 des Sichtfensters, 3 Zeilen zeigen alle
    // zehn Unlocks ohne Scrollen.
    private const float TileS = 32f, TileStepX = 33f, TileStepY = 36f;
    private const int TileCols = 4;

    // Detailseite. Alles haengt an DetailLeft/DetailW, damit die Spalte in
    // einem Stueck bleibt: Titel, Text, Trennlinie und Belohnungsbox stehen auf
    // derselben Kante, der Icon-Rahmen sitzt genau in ihrer Mitte.
    //
    // Die Spalte beginnt bei 182 und nicht mehr bei 168: die rechte Seite
    // laeuft zum Bund hin in den Falzschatten (bis x=179, siehe GUTTER in
    // Tools/erfolgsbuch_ui.py), und der Text fing vorher sichtbar in diesem
    // dunklen Streifen an.
    private const float DetailLeft = 182f, DetailW = 117f;

    private static readonly Vector4 SlotXL  = new Vector4(224f, 32f, 32f, 32f);
    private static readonly Vector4 Title   = new Vector4(DetailLeft, 67f, DetailW, 14f);
    private static readonly Vector4 Desc    = new Vector4(DetailLeft, 84f, DetailW, 34f);
    private static readonly Vector4 Prog    = new Vector4(DetailLeft, 120f, DetailW, 9f);
    private static readonly Vector4 Divider = new Vector4(DetailLeft, 132f, DetailW, 1f);
    private static readonly Vector4 Reward  = new Vector4(DetailLeft, 138f, DetailW, 20f);
    private static readonly Vector4 RewIco  = new Vector4(DetailLeft + 4f, 142f, 12f, 12f);
    private static readonly Vector4 RewTxt  = new Vector4(DetailLeft + 20f, 141f, DetailW - 24f, 14f);

    private const float SizeTitle = 12f, SizeText = 8f, SizeSmall = 6f;

    // Palette
    private static readonly Color TextDark  = Hex(0x3b2b33);
    private static readonly Color TextMid   = Hex(0x6f4630);
    private static readonly Color TextDim   = Hex(0xb99772);
    private static readonly Color TextOnTab = Hex(0xf2dcbc);
    private static readonly Color TextOnGold = Hex(0x4d2e1e);
    private static readonly Color LineColor = Hex(0xd9b189);

    /// <summary>
    /// Gesperrte Icons werden nicht schwarz gefaerbt, sondern nur gedaempft -
    /// das Motiv soll erkennbar bleiben.
    /// </summary>
    private static readonly Color IconLockedTint = Hex(0xb99772);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static AchievementsBookPanel instance;
    public static bool IsOpen => instance != null;

    /// <summary>Reiter, der beim naechsten Oeffnen vorne liegt.</summary>
    private static int startTab;

    private TMP_FontAsset font;
    private int tab;
    private int selected;

    // Geoeffnet wird mit [E], geschlossen auch - im Frame des Oeffnens darf die
    // Taste darum nicht noch einmal zaehlen.
    private int openedFrame;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Row> rows = new List<Row>();
    private readonly List<Tile> tiles = new List<Tile>();
    private readonly List<Image> cells = new List<Image>();
    private readonly List<Bubble> bubbles = new List<Bubble>();

    private RectTransform page;
    private RectTransform listContent;
    private RectTransform gridContent;
    private ScrollRect scroll;
    private Image[] tabImages;
    private TMP_Text[] tabLabels;

    private TMP_Text counterText, detailTitle, detailDesc, detailProgress, rewardText;
    private Image detailSlot, detailIcon, rewardBox, rewardIcon;

    private HubUI hub;

    /// <summary>Ein Listeneintrag, egal ob Achievement oder Unlock.</summary>
    private class Entry
    {
        public string Title;
        public string Desc;
        public string RowProgress;
        public string DetailProgress;
        public string Reward;
        public bool Done;
        public Sprite IconRow;
        public Sprite IconDetail;
        public Sprite RewardIcon;
    }

    private class Row
    {
        public Image Frame;       // Hintergrund: normal oder erledigt
        public Image Hover;       // duenner Rahmen, solange die Maus draufliegt
        public Image Selection;   // dicker Rahmen, liegt ueber allem
        public Image Slot;
        public Image Icon;
        public TMP_Text Title;
        public TMP_Text Progress;
    }

    /// <summary>Eine Kachel im Unlocks-Gitter. Traegt nur das Symbol.</summary>
    private class Tile
    {
        public Image Frame;       // slot_large_unlocked / _locked
        public Image Icon;
        public Image Hover;
        public Image Selection;
    }

    /// <summary>
    /// Schaltet den Hover-Rahmen einer Zeile oder Kachel. Eigene Komponente
    /// statt eines EventTriggers - das ist billiger und liest sich besser.
    /// </summary>
    private class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Target;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Target != null) Target.enabled = true;
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (Target != null) Target.enabled = false;
        }

        private void OnDisable()
        {
            if (Target != null) Target.enabled = false;
        }
    }

    private class Bubble
    {
        public RectTransform Rt;
        public float Size;
        public float Speed;
        public float Drift;
        public float Phase;
        public bool RightSide;
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("AchievementsBookPanel");
        instance = go.AddComponent<AchievementsBookPanel>();
    }

    /// <summary>Oeffnet direkt auf einem bestimmten Reiter: 0 = Erfolge, 1 = Unlocks.</summary>
    public static void Open(int tabIndex)
    {
        startTab = Mathf.Clamp(tabIndex, 0, 1);
        Open();
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

    /// <summary>
    /// Liest die Kataloge neu, falls das Fenster gerade offen ist. Fuer Aenderungen,
    /// die kein Ereignis feuern - etwa <see cref="Achievements.ResetAll"/> aus der
    /// Cheat-Konsole.
    /// </summary>
    public static void RefreshIfOpen()
    {
        if (instance != null) instance.Rebuild();
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        font = FindFont();
        tab = startTab;
        Build();
        BlockHub(true);
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Rebuild;
        Achievements.Unlocked += OnAchievementUnlocked;
        Unlocks.Granted += OnUnlockGranted;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Rebuild;
        Achievements.Unlocked -= OnAchievementUnlocked;
        Unlocks.Granted -= OnUnlockGranted;
    }

    private void OnDestroy()
    {
        BlockHub(false);
        if (instance == this) instance = null;
    }

    private void OnAchievementUnlocked(AchievementDef def) => Rebuild();
    private void OnUnlockGranted(UnlockDef def) => Rebuild();

    /// <summary>
    /// Sperrt den Hub, solange das Fenster offen ist - so wie Shop, Skilltree und
    /// UnlockPanel es tun. Laeuft das Fenster woanders (Hauptmenue, World Map,
    /// im Spiel), passiert nichts und es wird auch kein HubUI angelegt.
    /// </summary>
    private void BlockHub(bool blocked)
    {
        if (blocked)
        {
            hub = FindAnyObjectByType<HubUI>();
            if (hub == null) return;

            HubUI.PushModal();
            hub.SetPlayerFrozen(true);
            return;
        }

        if (hub == null) return;

        HubUI.PopModal();
        hub.SetPlayerFrozen(false);
        hub = null;
    }

    // ==================================================================
    //  Eingabe
    // ==================================================================

    private void Update()
    {
        AnimateBubbles();

        // Der Tastendruck, der das Fenster aufgemacht hat, darf es nicht gleich
        // wieder zumachen.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SetTab(tab - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SetTab(tab + 1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);
    }

    private void Move(int delta)
    {
        if (entries.Count == 0) return;
        Select(Mathf.Clamp(selected + delta, 0, entries.Count - 1));
        ScrollTo(selected);
    }

    /// <summary>
    /// Zieht den Inhalt nur nach, wenn die Auswahl aus dem Sichtfenster laeuft -
    /// und dann immer um ganze Zeilen, damit nichts auf halben Pixeln landet.
    /// </summary>
    private void ScrollTo(int index)
    {
        RectTransform content = GridMode ? gridContent : listContent;
        if (scroll == null || content == null) return;

        // In der Liste ist eine Reihe ein Eintrag, im Gitter sind es vier.
        float step = GridMode ? TileStepY : RowH;
        int perLine = GridMode ? TileCols : 1;

        int lines = Mathf.CeilToInt(entries.Count / (float)perLine);
        float hidden = Mathf.Max(0f, lines * step - ListH);
        if (hidden <= 0f) return;

        float top = content.anchoredPosition.y;
        float lineTop = (index / perLine) * step;
        float lineBottom = lineTop + step;

        if (lineTop < top) top = lineTop;
        else if (lineBottom > top + ListH) top = lineBottom - ListH;

        top = Mathf.Round(Mathf.Clamp(top, 0f, hidden) / step) * step;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, top);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;          // ueber Shop 130, Levelauswahl 135, Skilltree 140

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Kapselfluessigkeit fuellt den ganzen Bildschirm. Als 9-Slice, damit der
        // 4px-Rahmen auch bei anderen Seitenverhaeltnissen am Rand klebt und
        // seine Staerke behaelt - nur die Fluessigkeit dazwischen wird gedehnt.
        Image bg = Stretch("Backdrop", transform, Gfx("bg_capsule"));
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;

        BuildBubbles(StretchRect("Bubbles", transform));

        // Die Seite selbst bleibt immer exakt 320x180 und mittig, egal wie breit
        // der Bildschirm ist.
        page = Rect("Page", transform, 0f, 0f, RefW, RefH);
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        page.anchoredPosition = Vector2.zero;

        Img("BookPanel", page, Book.x, Book.y, Book.z, Book.w, Gfx("book_panel"));
        Img("BookSpine", page, Spine.x, Spine.y, Spine.z, Spine.w, Gfx("book_spine"));

        BuildTabs();
        BuildProgressBar();
        BuildBody();
        BuildDetail();

        Rebuild();
    }

    // ---------- Blasen ----------

    private void BuildBubbles(RectTransform parent)
    {
        // Nur in den Streifen links und rechts neben dem Buch: in der Mitte
        // waeren sie ohnehin dahinter versteckt und wuerden bloss am Buchrand
        // auftauchen und wieder verschwinden - genau das sah unlogisch aus.
        //
        // Und nur die vier kleinen: bei 320px Breite ist so ein Streifen 11px
        // schmal, eine 16er Blase wuerde dort ueber den Kapselrahmen laufen.
        for (int i = 0; i < 4; i++)
        {
            Sprite sprite = Gfx($"bubble_{i + 1:00}");
            if (sprite == null) continue;

            // Groesse kommt aus dem Sprite, nicht aus einer zweiten Liste -
            // sonst wird die Blase beim kleinsten Versatz gedehnt.
            float s = sprite.rect.width;

            RectTransform rt = Rect($"Bubble_{i + 1:00}", parent, 0f, 0f, s, s);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;

            Bubble b = new Bubble
            {
                Rt = rt,
                Size = s,
                Speed = Mathf.Lerp(11f, 5f, Mathf.InverseLerp(6f, 16f, s)),
                Drift = Random.Range(1f, 2f),
                Phase = Random.Range(0f, Mathf.PI * 2f),
                RightSide = i % 2 == 1,
            };
            bubbles.Add(b);

            rt.anchoredPosition = new Vector2(SideX(b), Random.Range(0f, RefH));
        }
    }

    /// <summary>
    /// x irgendwo im sichtbaren Streifen neben dem Buch - so, dass die Blase
    /// ganz hineinpasst und weder den Kapselrahmen noch die Buchkante ueberlauft.
    /// </summary>
    private float SideX(Bubble b)
    {
        // Beim ersten Aufruf steht das Canvas-Rect noch nicht, dann mit der
        // Referenzbreite rechnen.
        float w = ((RectTransform)transform).rect.width;
        if (w <= 1f) w = RefW;

        float margin = Mathf.Max(12f, (w - Book.z) * 0.5f);     // Buch ist mittig
        float half = b.Size * 0.5f;

        float min = CapsuleFrame + half;                        // hinter dem Rahmen
        float max = Mathf.Max(min, margin - half);               // vor der Buchkante
        float inset = Random.Range(min, max);

        return b.RightSide ? w - inset : inset;
    }

    /// <summary>Breite des Kapselrahmens: 4px Holz plus 1px Kontur.</summary>
    private const float CapsuleFrame = 5f;

    private void AnimateBubbles()
    {
        float h = ((RectTransform)transform).rect.height;

        foreach (Bubble b in bubbles)
        {
            Vector2 p = b.Rt.anchoredPosition;
            p.y += b.Speed * Time.unscaledDeltaTime;

            if (p.y > h + 10f)
            {
                p.y = -10f;
                p.x = SideX(b);
            }

            b.Phase += Time.unscaledDeltaTime * 0.8f;
            b.Rt.anchoredPosition = new Vector2(p.x + Mathf.Sin(b.Phase) * b.Drift, p.y);
        }
    }

    // ---------- Reiter, Leiste, Liste, Detail ----------

    private void BuildTabs()
    {
        tabImages = new Image[2];
        tabLabels = new TMP_Text[2];

        string[] names =
        {
            Loc.Get("ui.book.tab.achievements", "ERFOLGE"),
            Loc.Get("ui.book.tab.unlocks", "UNLOCKS"),
        };

        for (int i = 0; i < 2; i++)
        {
            Image img = Img($"Tab_{i}", page, TabX + i * TabStep, TabY, TabW, TabH,
                            Gfx("tab_inactive"));
            img.raycastTarget = true;
            tabImages[i] = img;

            tabLabels[i] = Label($"Label_{i}", img.rectTransform, 2f, 2f, 56f, 10f,
                                 names[i], SizeText, TextDark, TextAlignmentOptions.Center);

            Button button = img.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;

            int captured = i;
            button.onClick.AddListener(() =>
            {
                PlayClick();
                SetTab(captured);
            });
        }

        BuildBackButton();
    }

    /// <summary>
    /// Sieht aus wie ein inaktiver Reiter, ist aber ein Knopf. Beschriftung ueber
    /// den vorhandenen Schluessel, der steht schon in beiden Sprachdateien -
    /// deutsch bewusst "ZURUECK" ohne Umlaut, weil ThaleahFat keinen kann.
    /// </summary>
    private void BuildBackButton()
    {
        Image plate = Img("Back", page, Back.x, Back.y, Back.z, Back.w, Gfx("tab_inactive"));
        plate.type = Image.Type.Sliced;
        plate.raycastTarget = true;

        Label("Label", plate.rectTransform, 2f, 2f, Back.z - 4f, 10f,
              Loc.Get("ui.achievements.close", "ZURUECK"), SizeText, TextOnTab,
              TextAlignmentOptions.Center);

        // Gleiche Hover-Sprache wie Liste und Gitter: ein duenner Rahmen.
        Image hover = Img("Hover", plate.rectTransform, 0f, 0f, Back.z, Back.w, Gfx("row_hover"));
        hover.type = Image.Type.Sliced;
        hover.enabled = false;

        Button button = plate.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = plate;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            Close();
        });

        plate.gameObject.AddComponent<RowHover>().Target = hover;
    }

    private void BuildProgressBar()
    {
        for (int i = 0; i < CellCount; i++)
        {
            cells.Add(Img($"Cell_{i:00}", page, BarX + i * CellStep, BarY, CellW, CellH,
                          Gfx("progress_cell_empty")));
        }

        counterText = Label("Counter", page, Counter.x, Counter.y, Counter.z, Counter.w,
                            "", SizeSmall, TextMid, TextAlignmentOptions.Right);
    }

    /// <summary>
    /// Beide Ansichten haengen im selben Sichtfenster: die Liste fuer die
    /// Erfolge, das Kachelgitter fuer die Unlocks. Umgeschaltet wird nur, welche
    /// gerade aktiv ist.
    /// </summary>
    private void BuildBody()
    {
        RectTransform viewport = Rect("Viewport", page, ListX, ListY, ListW, ListH);
        viewport.gameObject.AddComponent<RectMask2D>();

        listContent = Rect("ListContent", viewport, 0f, 0f, ListW, 0f);

        VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        gridContent = Rect("GridContent", viewport, 0f, 0f, ListW, 0f);

        GridLayoutGroup grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(TileS, TileS);
        grid.spacing = new Vector2(TileStepX - TileS, TileStepY - TileS);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = TileCols;
        grid.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter gridFitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
    }

    /// <summary>Kachelgitter im Unlocks-Reiter, Liste im Erfolge-Reiter.</summary>
    private bool GridMode => tab == 1;

    private void ApplyMode()
    {
        listContent.gameObject.SetActive(!GridMode);
        gridContent.gameObject.SetActive(GridMode);

        scroll.content = GridMode ? gridContent : listContent;
        scroll.scrollSensitivity = GridMode ? TileStepY : RowH;   // eine Reihe pro Rastung
    }

    private void BuildDetail()
    {
        detailSlot = Img("SlotXL", page, SlotXL.x, SlotXL.y, SlotXL.z, SlotXL.w,
                         Gfx("slot_large_unlocked"));
        detailIcon = Img("IconXL", page, SlotXL.x, SlotXL.y, SlotXL.z, SlotXL.w, null);

        detailTitle = Label("Title", page, Title.x, Title.y, Title.z, Title.w,
                            "", SizeTitle, TextDark, TextAlignmentOptions.Center);

        detailDesc = Label("Desc", page, Desc.x, Desc.y, Desc.z, Desc.w,
                           "", SizeText, TextMid, TextAlignmentOptions.TopLeft);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        detailDesc.overflowMode = TextOverflowModes.Truncate;

        detailProgress = Label("Progress", page, Prog.x, Prog.y, Prog.z, Prog.w,
                               "", SizeSmall, TextDim, TextAlignmentOptions.Center);

        Img("Divider", page, Divider.x, Divider.y, Divider.z, Divider.w, null).color = LineColor;

        // Die Box ist nativ 128 breit, die Detailspalte ist schmaler. Als
        // 9-Slice bleiben die Goldfassung links und die Kante rechts scharf,
        // nur die leere Mitte dazwischen wird gestaucht.
        rewardBox = Img("RewardBox", page, Reward.x, Reward.y, Reward.z, Reward.w, Gfx("reward_box"));
        rewardBox.type = Image.Type.Sliced;
        rewardIcon = Img("RewardIcon", page, RewIco.x, RewIco.y, RewIco.z, RewIco.w, null);
        rewardText = Label("RewardText", page, RewTxt.x, RewTxt.y, RewTxt.z, RewTxt.w,
                           "", SizeSmall, TextMid, TextAlignmentOptions.TopLeft);
        rewardText.textWrappingMode = TextWrappingModes.Normal;
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    private void SetTab(int index)
    {
        index = Mathf.Clamp(index, 0, 1);
        if (index == tab) return;

        tab = index;
        startTab = index;
        selected = 0;
        Rebuild();

        RectTransform content = GridMode ? gridContent : listContent;
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
    }

    /// <summary>Liest die Kataloge neu ein und beschriftet die Zeilen danach.</summary>
    public void Rebuild()
    {
        CollectEntries();

        for (int i = 0; i < 2; i++)
        {
            bool active = i == tab;
            tabImages[i].sprite = Gfx(active ? "tab_active" : "tab_inactive");
            tabLabels[i].color = active ? TextDark : TextOnTab;

            // Der aktive Reiter ist 2px hoeher und deckt damit Kontur (y=23)
            // und Lichtkante (y=24) der Buchseite zu. Erst dadurch haengt er an
            // der Seite, statt als eigene Karte darueber zu schweben - die
            // Beschriftung bleibt oben verankert und wandert nicht mit.
            tabImages[i].rectTransform.sizeDelta =
                new Vector2(TabW, active ? TabH + TabOverlap : TabH);
        }

        ApplyMode();

        if (GridMode) BuildTiles();
        else BuildRows();

        selected = entries.Count == 0 ? 0 : Mathf.Clamp(selected, 0, entries.Count - 1);
        Select(selected);
        RefreshBar();
    }

    private void CollectEntries()
    {
        entries.Clear();

        if (tab == 0)
        {
            foreach (AchievementDef def in SortedAchievements())
            {
                bool done = def.IsUnlocked;
                bool counted = def.HasProgress;
                string count = $"{Mathf.FloorToInt(def.Value)} / {Mathf.FloorToInt(def.Goal)}";

                entries.Add(new Entry
                {
                    Title = def.Name,
                    Desc = def.Description,
                    RowProgress = done ? "" : counted ? count : "",
                    DetailProgress = done ? "" : counted ? count : "",
                    // Ohne "Belohnung:" davor - die Goldfassung mit dem Abzeichen
                    // sagt das schon, und die Detailspalte ist schmal.
                    Reward = def.Souls > 0
                        ? $"+{def.Souls} {Loc.Get("ui.achievements.souls", "Cookie Souls")}"
                        : "",
                    Done = done,
                    IconRow = BookIcon(def.IconKey, 21),
                    IconDetail = BookIcon(def.IconKey, 32) ?? def.Icon,
                    RewardIcon = BookIcon("_badge_unlocked", 12),
                });
            }
            return;
        }

        foreach (UnlockDef def in Unlocks.All)
        {
            bool done = def.IsUnlocked;

            // Woher kommt der Unlock? Vergibt ein Achievement ihn mit, steht das
            // in der Detailspalte - genau wie im UnlockPanel.
            AchievementDef source = Ach.FindByUnlock(def.Id);
            string desc = done
                ? def.Description
                : source != null
                    ? string.Format(Loc.Get("ui.unlocks.from", "From: {0}"), source.Name)
                    : Loc.Get("ui.unlocks.hint", "Keep playing to find this one.");

            entries.Add(new Entry
            {
                Title = done ? def.Name : "???",
                Desc = desc,
                RowProgress = "",
                DetailProgress = "",

                // Ein Unlock vergibt nichts, die Box blieb hier bisher leer.
                // Freigeschaltet bekommt sie jetzt trotzdem etwas zu sagen -
                // die untere Haelfte der Seite stand sonst ohne Grund leer,
                // und "hab ich" ist genau die Auskunft, die man hier sucht.
                Reward = done ? Loc.Get("ui.unlocks.owned", "Unlocked") : "",
                Done = done,
                IconRow = BookIcon(def.IconKey, 21),
                IconDetail = BookIcon(def.IconKey, 32) ?? def.Icon,
                RewardIcon = done ? BookIcon("_badge_unlocked", 12) : null,
            });
        }
    }

    /// <summary>
    /// Nach Kategorie gruppiert, innerhalb der Gruppe in Katalogreihenfolge -
    /// gleiche Sortierung wie im AchievementPanel, damit beide dasselbe zeigen.
    /// </summary>
    private static IEnumerable<AchievementDef> SortedAchievements()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            foreach (AchievementDef def in Ach.All)
            {
                if (def.Category == category) yield return def;
            }
        }
    }

    private void BuildRows()
    {
        while (rows.Count < entries.Count) rows.Add(CreateRow(rows.Count));

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            bool used = i < entries.Count;
            row.Frame.gameObject.SetActive(used);
            if (!used)
            {
                row.Hover.enabled = false;
                continue;
            }

            Entry e = entries[i];

            // Erledigt bekommt eine eigene Flaeche, nicht nur ein Haekchen -
            // man soll auf einen Blick sehen, was schon steht.
            row.Frame.sprite = Gfx(e.Done ? "row_done" : "row_normal");
            row.Slot.sprite = Gfx(e.Done ? "slot_small_unlocked" : "slot_small_locked");

            row.Icon.sprite = e.IconRow;
            row.Icon.enabled = e.IconRow != null;
            row.Icon.color = e.Done ? Color.white : IconLockedTint;

            row.Title.text = e.Title;
            row.Title.color = e.Done ? TextOnGold : TextMid;
            row.Progress.text = e.RowProgress;
            row.Progress.color = e.Done ? TextMid : TextDim;
        }
    }

    private Row CreateRow(int index)
    {
        Image frame = Img($"Row_{index:00}", listContent, 0f, 0f, ListW, RowH, Gfx("row_normal"));
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = true;

        LayoutElement le = frame.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RowH;
        le.minHeight = RowH;

        // Ganz unten in der Zeile, damit der duenne Strich nie ueber Icon oder
        // Text liegt.
        Image hover = Img("Hover", frame.rectTransform, 0f, 0f, ListW, RowH, Gfx("row_hover"));
        hover.type = Image.Type.Sliced;
        hover.enabled = false;

        Image slot = Img("Slot", frame.rectTransform, SlotDX, SlotDY, SlotS, SlotS,
                         Gfx("slot_small_unlocked"));
        Image icon = Img("Icon", frame.rectTransform, SlotDX, SlotDY, SlotS, SlotS, null);

        // Zeilenlokal y=2, nicht y=1: der Auswahlrahmen liegt als letztes Kind
        // ueber dem Text und hat bei y=1 seine Innenkante. Die Punkte ueber
        // einem A/O/U mit Umlaut sitzen ganz oben in der Zeile und kaemen ihr
        // sonst ins Gehege. 10 ist immer noch groesser als SizeText - die
        // Zone darf nie flacher werden als die Schrift, sonst wirft TMP die
        // Zeile still weg.
        TMP_Text title = Label("Title", frame.rectTransform, RowTextDX, 2f, RowTextW, 10f,
                               "", SizeText, TextMid, TextAlignmentOptions.Left);
        title.overflowMode = TextOverflowModes.Ellipsis;

        TMP_Text prog = Label("Progress", frame.rectTransform, RowTextDX, 11f, RowTextW, 9f,
                              "", SizeSmall, TextDim, TextAlignmentOptions.Left);

        // Auswahl liegt als reiner Rahmen obenauf, damit "ausgewaehlt" und
        // "erledigt" gleichzeitig sichtbar sind.
        Image sel = Img("Selection", frame.rectTransform, 0f, 0f, ListW, RowH, Gfx("row_selected"));
        sel.type = Image.Type.Sliced;
        sel.enabled = false;

        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;

        frame.gameObject.AddComponent<RowHover>().Target = hover;

        int captured = index;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            Select(captured);
        });

        return new Row
        {
            Frame = frame, Hover = hover, Selection = sel, Slot = slot, Icon = icon,
            Title = title, Progress = prog,
        };
    }

    // ---------- Kachelgitter (Unlocks) ----------

    private void BuildTiles()
    {
        while (tiles.Count < entries.Count) tiles.Add(CreateTile(tiles.Count));

        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            bool used = i < entries.Count;
            tile.Frame.gameObject.SetActive(used);
            if (!used)
            {
                tile.Hover.enabled = false;
                continue;
            }

            Entry e = entries[i];

            // Das Gitter gibt es nur fuer Unlocks, und dort ist "hab ich" der
            // eigentliche Inhalt der Kachel. Ein vergoldeter Rahmen sagt das
            // von selbst - vorher war der Unterschied nur, dass die Kachel
            // *nicht* die blasse Vertiefung war, und eine Abwesenheit sieht man
            // schlecht.
            tile.Frame.sprite = Gfx(e.Done ? "slot_large_owned" : "slot_large_locked");
            tile.Icon.sprite = e.IconDetail;              // die 32er-Variante
            tile.Icon.enabled = e.IconDetail != null;
            tile.Icon.color = e.Done ? Color.white : IconLockedTint;
        }
    }

    private Tile CreateTile(int index)
    {
        Image frame = Img($"Tile_{index:00}", gridContent, 0f, 0f, TileS, TileS,
                          Gfx("slot_large_unlocked"));
        frame.raycastTarget = true;

        Image icon = Img("Icon", frame.rectTransform, 0f, 0f, TileS, TileS, null);

        // Hover und Auswahl sind dieselben Sprites wie in der Liste. Sie sind
        // 9-Slices, also laesst sich derselbe Rahmen auf 32x32 ziehen, ohne dass
        // die Kante dicker wird.
        Image hover = Img("Hover", frame.rectTransform, 0f, 0f, TileS, TileS, Gfx("row_hover"));
        hover.type = Image.Type.Sliced;
        hover.enabled = false;

        Image sel = Img("Selection", frame.rectTransform, 0f, 0f, TileS, TileS, Gfx("row_selected"));
        sel.type = Image.Type.Sliced;
        sel.enabled = false;

        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;

        frame.gameObject.AddComponent<RowHover>().Target = hover;

        int captured = index;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            Select(captured);
        });

        return new Tile { Frame = frame, Icon = icon, Hover = hover, Selection = sel };
    }

    // ---------- Auswahl ----------

    private void Select(int index)
    {
        selected = index;

        if (GridMode)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (i >= entries.Count) continue;
                tiles[i].Selection.enabled = i == selected;
            }
        }
        else
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= entries.Count) continue;
                rows[i].Selection.enabled = i == selected;
            }
        }

        ShowDetail(entries.Count > 0 ? entries[selected] : null);
    }

    private void ShowDetail(Entry e)
    {
        if (e == null)
        {
            detailTitle.text = Loc.Get("ui.achievements.empty", "Nothing here yet.");
            detailDesc.text = "";
            detailProgress.text = "";
            rewardText.text = "";
            detailIcon.enabled = false;
            rewardBox.enabled = false;
            rewardIcon.enabled = false;
            return;
        }

        detailSlot.sprite = Gfx(e.Done
            ? (GridMode ? "slot_large_owned" : "slot_large_unlocked")
            : "slot_large_locked");

        detailIcon.sprite = e.IconDetail;
        detailIcon.enabled = e.IconDetail != null;
        detailIcon.color = e.Done ? Color.white : IconLockedTint;

        detailTitle.text = e.Title;
        detailTitle.color = e.Done ? TextDark : TextMid;
        detailDesc.text = e.Desc;
        detailProgress.text = e.DetailProgress;

        // Offene Erfolge haben eine Belohnung, freigeschaltete Unlocks eine
        // Statuszeile - beides steht in derselben Box. Ist nichts davon da,
        // verschwindet sie ganz, statt leer herumzustehen. Der Text kommt
        // fertig aus CollectEntries, weil "Belohnung:" nur vor einer Belohnung
        // stehen darf und nicht vor "Freigeschaltet".
        bool hasReward = !string.IsNullOrEmpty(e.Reward);
        rewardBox.enabled = hasReward;
        rewardText.text = e.Reward;
        rewardIcon.sprite = e.RewardIcon;
        rewardIcon.enabled = hasReward && e.RewardIcon != null;
    }

    private void RefreshBar()
    {
        int done = tab == 0 ? Achievements.UnlockedCount : Unlocks.UnlockedCount;
        int total = tab == 0 ? Achievements.TotalCount : Unlocks.TotalCount;

        counterText.text = string.Format(Loc.Get("ui.achievements.counter", "{0} / {1}"), done, total);

        // Angefangen zaehlt als angefangen: sobald etwas offen ist, leuchtet
        // mindestens eine Zelle, und voll ist die Leiste nur bei wirklich allem.
        int filled = 0;
        if (total > 0 && done > 0)
        {
            filled = Mathf.Clamp(Mathf.CeilToInt(done / (float)total * CellCount), 1, CellCount);
            if (done < total) filled = Mathf.Min(filled, CellCount - 1);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].sprite = Gfx(i < filled ? "progress_cell_full" : "progress_cell_empty");
        }
    }

    // ==================================================================
    //  Kleinkram
    // ==================================================================

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private static Sprite Gfx(string name)
    {
        // != null statt TryGetValue allein: raeumt Unity die Resources zwischen
        // zwei Szenen auf, steht im Cache eine zerstoerte Referenz.
        if (spriteCache.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        Sprite s = Resources.Load<Sprite>(UiPath + name);
        if (s == null)
        {
            Debug.LogWarning($"[Buch] Sprite '{name}' fehlt - erwartet wird " +
                             $"Assets/Resources/{UiPath}{name}.png");
        }
        spriteCache[name] = s;
        return s;
    }

    /// <summary>
    /// Das auf das Slot-Raster gebrachte Icon (12, 21 oder 32 Pixel). Fehlt es,
    /// gibt es null zurueck und der Aufrufer nimmt das alte 64x64-Bild.
    /// </summary>
    private static Sprite BookIcon(string iconKey, int size)
    {
        if (string.IsNullOrEmpty(iconKey)) return null;
        return Resources.Load<Sprite>($"{IconPath}{iconKey}_{size}");
    }

    /// <summary>
    /// Jersey10 zuerst: die Pixelfont des Projekts (ThaleahFat) kann keine
    /// Umlaute, und in diesem Fenster steht deutscher Text. Die Auswahl steht
    /// in <see cref="PixelUI.FindTextFont"/>, damit die anderen Fenster mit
    /// denselben Katalogtexten nicht wieder auf ThaleahFat zurueckfallen.
    /// </summary>
    private static TMP_FontAsset FindFont()
    {
        return PixelUI.FindTextFont() ?? PixelUI.FindPixelFont();
    }

    private static Color Hex(int rgb) => new Color32(
        (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

    /// <summary>Rechteck im 320x180-Raster: x/y zaehlen von oben links.</summary>
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    private static Image Img(string name, Transform parent, float x, float y, float w, float h,
                             Sprite sprite)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform StretchRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private static Image Stretch(string name, Transform parent, Sprite sprite)
    {
        RectTransform rt = StretchRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Die Hoehe muss groesser sein als die Schriftgroesse. Ist das Rechteck zu
    /// flach, wirft TextMeshPro die Zeile still weg und es steht gar nichts da.
    /// </summary>
    private TMP_Text Label(string name, Transform parent, float x, float y, float w, float h,
                           string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);

        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
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
