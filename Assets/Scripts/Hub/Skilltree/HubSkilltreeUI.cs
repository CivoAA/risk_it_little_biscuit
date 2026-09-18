using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// ---------------------------------------------------------------------------
///  DER SKILLTREE IM HUB.
///
///  Das Fenster ist der Rahmen: Titelschild, die vier Kategorien links, das
///  grosse Feld in der Mitte und die Beschreibungskarte rechts. Die Knoten im
///  Feld baut <see cref="HubSkilltreeGraph"/> - der Aufbau steht also nicht hier,
///  sondern dort.
///
///  WO DER INHALT HERKOMMT:
///    Nicht mehr aus dem Inspector. Kategorien, Farben, Texte und Knoten stehen
///    im Baum-Asset des Charakters unter Assets/Resources/SkillTrees/. Gebaut
///    wird das unter Tools -> Skilltree -> Editor. Beim Oeffnen holt sich das
///    Fenster ueber Skills.Sync() den Baum des gerade gewaehlten Charakters -
///    jeder Charakter hat also seinen eigenen.
///
///  Der Hintergrund um das Papier herum ist bewusst nur eine ruhige Flaeche -
///  die Pixelart dafuer kommt nach und wird dann in "Hintergrundbild"
///  eingetragen; sie legt sich ueber die vollen 320x180.
///
///  Bedienung: Klick, W/S bzw. Hoch/Runter fuer die Kategorie, A/D bzw.
///  Links/Rechts und Mausrad zum Scrollen im Baum, ESC zum Schliessen. Hover und
///  Klick rechnet dieses Skript wie die Levelauswahl selbst aus der Mausposition
///  aus (UpdateHover/ClickAt) - wer einen Knopf ergaenzt, muss ihn in BEIDEN
///  Methoden eintragen, sonst reagiert er nicht.
/// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public class HubSkilltreeUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Beschriftungen")]
    [SerializeField] private string titleLabel = "SKILLTREE";
    [SerializeField] private string backLabel  = "ZURÜCK";
    [Tooltip("Nur wenn eine Kategorie im Baum-Asset keinen eigenen Pfadnamen hat. {0} = Name.")]
    [SerializeField] private string pathLabelFormat = "{0}PFAD";
    [Tooltip("Die Punkteanzeige unten rechts. {0} = Anzahl.")]
    [SerializeField] private string pointsFormat = "SKILLPUNKTE: {0}";
    [Tooltip("Unter der Beschreibung eines Knotens. {0} = Preis.")]
    [SerializeField] private string priceFormat = "KOSTET {0} SP";
    [SerializeField] private string boughtLabel = "GEKAUFT";
    [SerializeField] private string lockedLabel = "GESPERRT";

    [Header("Grafik")]
    [Tooltip("Die Pixelart hinter dem Papier - legt sich ueber die vollen 320x180. " +
             "Leer = ruhige Flaeche in Hintergrundfarbe.")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("Optionaler gepixelter Rahmen fuer die grosse Papierflaeche. " +
             "Leer = Flaeche mit 1px-Rahmen.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("Pixelschrift. Hier gehoert Jersey10 hin - ThaleahFat/PixelArtFont kennen " +
             "keine Umlaute, und GLÜCK wie ZURÜCK blieben darin lueckenhaft. " +
             "Leer = die zuerst gefundene Pixelschrift, sonst TMP-Standard.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Kaesten (Pixel im 320x180-Bild, Nullpunkt links oben)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [Tooltip("Die grosse Papierflaeche.")]
    [SerializeField] private Rect panelArea = new Rect(8f, 10f, 304f, 142f);
    [Tooltip("Das Titelschild. Es liegt bewusst ueber der Oberkante des Papiers.")]
    [SerializeField] private Rect titleArea = new Rect(94f, 3f, 132f, 22f);
    [Tooltip("Der oberste Kategorieknopf. Die anderen ruecken um Hoehe plus Abstand nach.")]
    [SerializeField] private Rect categoryArea = new Rect(14f, 38f, 76f, 22f);
    [SerializeField] private float categoryGap = 6f;
    [Tooltip("Das grosse Feld in der Mitte.")]
    [SerializeField] private Rect contentArea = new Rect(96f, 30f, 142f, 118f);
    [Tooltip("Das farbige Banner oben im grossen Feld.")]
    [SerializeField] private Rect contentHeaderArea = new Rect(104f, 35f, 126f, 14f);
    [Tooltip("Die Flaeche darunter - hier baut sich der Baum auf und wird gescrollt.")]
    [SerializeField] private Rect contentBodyArea = new Rect(100f, 54f, 134f, 90f);
    [SerializeField] private Rect detailArea = new Rect(244f, 30f, 62f, 118f);
    [SerializeField] private Rect backArea = new Rect(8f, 156f, 62f, 16f);
    [Tooltip("Die Skillpunkte, unten rechts neben dem Zurueck-Knopf.")]
    [SerializeField] private Rect pointsArea = new Rect(198f, 156f, 106f, 16f);

    [Header("Beschreibungskarte (Pixel im 320x180-Bild)")]
    [SerializeField] private Rect detailOrbArea   = new Rect(259f, 38f, 32f, 32f);
    [SerializeField] private Rect detailNameArea  = new Rect(246f, 74f, 58f, 12f);
    [SerializeField] private Rect detailDivider1  = new Rect(251f, 90f, 48f, 1f);
    [SerializeField] private Rect detailTextArea  = new Rect(248f, 96f, 54f, 28f);
    [SerializeField] private Rect detailDivider2  = new Rect(251f, 128f, 48f, 1f);
    [SerializeField] private Rect detailQuoteArea = new Rect(248f, 133f, 54f, 14f);

    [Header("Schriftgroessen")]
    [SerializeField] private float titleFontSize      = 13f;
    [SerializeField] private float categoryFontSize   = 8f;
    [SerializeField] private float headerFontSize     = 8f;
    [SerializeField] private float detailNameFontSize = 9f;
    [SerializeField] private float detailFontSize     = 6f;
    [SerializeField] private float buttonFontSize     = 7f;
    [SerializeField] private float detailLineSpacing  = 4f;

    [Header("Farben")]
    [Tooltip("Der Rand neben den 320x180, wenn der Bildschirm nicht 16:9 ist - und " +
             "die Flaeche hinter dem Papier, solange es dafuer keine Pixelart gibt.")]
    [SerializeField] private Color backdropColor = new Color32(0x21, 0x1A, 0x1C, 0xFF);
    [SerializeField] private Color panelFill     = new Color32(0xE8, 0xDE, 0xC2, 0xFF);
    [Tooltip("Das Feld in der Mitte und die Karte rechts liegen etwas heller im Papier.")]
    [SerializeField] private Color panelInset    = new Color32(0xF0, 0xE6, 0xCC, 0xFF);
    [SerializeField] private Color panelBorder   = new Color32(0xC0, 0xAE, 0x8A, 0xFF);
    [SerializeField] private Color panelInk      = new Color32(0x33, 0x26, 0x2B, 0xFF);
    [SerializeField] private Color panelInkDim   = new Color32(0x6B, 0x51, 0x47, 0xFF);
    [Tooltip("Schrift auf gesaettigten Flaechen - dunkle Farben bekommen sie, helle " +
             "wie Gelb stattdessen panelInk. Siehe InkOn().")]
    [SerializeField] private Color textOnColor   = new Color32(0xF4, 0xEC, 0xD8, 0xFF);
    [SerializeField] private Color backFill      = new Color32(0x4A, 0x36, 0x2E, 0xFF);
    [SerializeField] private Color backBorder    = new Color32(0x2A, 0x1E, 0x1A, 0xFF);
    [SerializeField] private Color backInk       = new Color32(0xE8, 0xDE, 0xC2, 0xFF);
    [Tooltip("So viel dunkler wird aus der Kategoriefarbe ihr Rahmen. 0 = gleiche Farbe.")]
    [SerializeField, Range(0f, 1f)] private float borderShade = 0.42f;
    [Tooltip("So viel heller wird ein Knopf unter der Maus. 0 = kein Hover.")]
    [SerializeField, Range(0f, 1f)] private float hoverLift = 0.25f;

    [Header("Steuerung")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Sound")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ------------------------------------------------------------ Laufzeit

    /// <summary>Die Teile eines Kategorieknopfes, die beim Umschalten die Farbe wechseln.</summary>
    class Tab
    {
        public GameObject Go;
        public Image Glow;       // heller Saum, nur um den gewaehlten Knopf
        public Image Fill;       // Papier oder Kategoriefarbe
        public Image Outer;      // Rahmen in Kategoriefarbe
        public Image Inner;      // duenne Linie innen
        public Image IconFill;   // Kaestchen hinter dem Symbol
        public Image Icon;
        public TextMeshProUGUI Label;
        public Image Pointer;    // der Zeiger, der rechts heraussteht
    }

    /// <summary>Was gerade unter der Maus liegt.</summary>
    enum Hit { None, Category, Back, ScrollLeft, ScrollRight, Node }

    readonly List<Tab> tabs = new List<Tab>();
    readonly HubPixelSprites pixels = new HubPixelSprites();

    GameObject root;
    RectTransform screen;
    Transform tabHolder;
    Image headerFill, headerFrame, headerPlusLeft, headerPlusRight;
    Image orbRing, orbFill;
    Image divider2a, divider2b, dividerPlus2;
    Image backFillImage;
    TextMeshProUGUI headerText, detailName, detailText, detailQuote, pointsText;

    HubSkilltreeGraph graph;
    SkillShapeSprites orbShapes;

    /// <summary>Die Kategorien des gerade gezeigten Baums - Reihenfolge wie SkillCategory.</summary>
    readonly List<SkillBranchDef> branches = new List<SkillBranchDef>();
    SkillTreeDef shownTree;

    Hit hover = Hit.None;
    int hoverTab = -1;

    int selected;
    int openedOnFrame = -1;
    bool built;

    SkillBranchDef Current =>
        (selected >= 0 && selected < branches.Count) ? branches[selected] : null;

    // ---------------------------------------------------------------- Aufbau

    void Awake()
    {
        EnsureSfxSource();
        Build();
    }

    void EnsureSfxSource()
    {
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    void OnDestroy()
    {
        pixels.Dispose();
        orbShapes?.Dispose();
        graph?.Dispose();

        Skills.Changed -= OnSkillsChanged;

        // Szenenwechsel mit offenem Fenster: die Sperre wieder abmelden, sonst
        // reagiert der Hub beim naechsten Mal auf gar nichts mehr.
        if (!IsOpen) return;
        IsOpen = false;
        HubUI.PopModal();
    }

    void Build()
    {
        if (built) return;
        built = true;

        if (font == null) font = PixelUI.FindPixelFont();
        orbShapes = new SkillShapeSprites();

        var canvasGO = new GameObject("SkilltreeCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) canvasGO.layer = uiLayer;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ueber Textbox (100), Konsole (120), Shop (130) und Levelauswahl (135)
        canvas.sortingOrder = 140;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root = HubUiKit.NewRect("Skilltree", canvasGO.transform);
        HubUiKit.Stretch((RectTransform)root.transform);

        // Deckt den Hub zu. Klicks faengt nicht diese Flaeche ab, sondern die
        // Trefferpruefung in Update - solange das Fenster offen ist, kommt im
        // Hub ohnehin nichts an (HubUI.PushModal).
        var backdrop = HubUiKit.NewImage("Backdrop", root.transform, null, backdropColor);
        HubUiKit.Stretch((RectTransform)backdrop.transform);

        // Feste 320x180, mittig - nur so sitzt jeder ausgemessene Pixel da, wo
        // er hingehoert, egal wie gross das Fenster ist.
        screen = (RectTransform)HubUiKit.NewRect("Screen", root.transform).transform;
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0.5f, 0.5f);
        screen.sizeDelta = referenceResolution;
        screen.anchoredPosition = Vector2.zero;

        if (backgroundSprite != null)
        {
            var bg = HubUiKit.NewImage("Background", screen, backgroundSprite, Color.white);
            HubUiKit.Stretch((RectTransform)bg.transform);
            bg.preserveAspect = false;
        }

        BuildPanel();
        BuildTitle();

        // Die Kategorieknoepfe haengen am Baum und werden darum erst in Open()
        // gefuellt. Der Behaelter steht schon, damit die Reihenfolge der Ebenen
        // stimmt: Knoepfe vor dem Feld, Feld vor der Karte.
        GameObject tabRoot = HubUiKit.NewRect("Kategorien", screen);
        // Muss ueber die vollen 320x180 gehen: die Knoepfe werden mit
        // HubUiKit.Place gesetzt, und das rechnet ab der linken OBEREN Ecke des
        // Elters. Ohne das sitzt der Behaelter als Punkt in der Bildmitte - die
        // Knoepfe landen dann mitten im Baum, und die Trefferpruefung in
        // UpdateHover sucht sie trotzdem links (TabArea).
        HubUiKit.Stretch((RectTransform)tabRoot.transform);
        tabHolder = tabRoot.transform;

        BuildContent();
        BuildDetail();
        BuildBackButton();

        // Das Feld mit den Knoten kommt zuletzt, damit die Blaetterpfeile ueber
        // dem Rahmen liegen.
        graph = new HubSkilltreeGraph(screen, contentBodyArea, new HubSkilltreeGraph.Palette
        {
            PanelFill   = panelFill,
            PanelInset  = panelInset,
            PanelBorder = panelBorder,
            PanelInk    = panelInk,
            PanelInkDim = panelInkDim,
            TextOnColor = textOnColor,
            BorderShade = borderShade,
            HoverLift   = hoverLift,
        });

        Skills.Changed += OnSkillsChanged;

        root.SetActive(false);
    }

    void BuildPanel()
    {
        if (panelSprite != null)
        {
            Image img = HubUiKit.NewImage("Panel", screen, panelSprite, Color.white);
            img.preserveAspect = false;
            HubUiKit.Place((RectTransform)img.transform, panelArea);
            return;
        }

        Fill("Panel", screen, panelArea, panelFill);
        Frame("PanelFrame", screen, panelArea, panelBorder);
    }

    void BuildTitle()
    {
        Fill("Title", screen, titleArea, panelFill);
        Frame("TitleFrame", screen, titleArea, panelInk);
        // Zweite Linie innen - das gibt dem Schild die geschnitzte Kante.
        Frame("TitleFrameInner", screen, Inset(titleArea, 2f), panelBorder);

        TextMeshProUGUI t = HubUiKit.NewText("TitleLabel", screen, font, titleFontSize,
                                             panelInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)t.transform, titleArea);
        t.text = titleLabel;

        // Die beiden Kreuze links und rechts der Schrift.
        float plusY = titleArea.y + (titleArea.height - 5f) * 0.5f;
        Plus("TitlePlusLeft", screen, new Rect(titleArea.x + 7f, plusY, 5f, 5f), panelInkDim);
        Plus("TitlePlusRight", screen, new Rect(titleArea.xMax - 12f, plusY, 5f, 5f), panelInkDim);
    }

    /// <summary>
    /// Legt die Kategorieknoepfe neu an. Laeuft bei jedem Oeffnen, weil Farbe und
    /// Beschriftung aus dem Baum des Charakters kommen - und der kann zwischen
    /// zwei Besuchen ein anderer sein.
    /// </summary>
    void BuildTabs()
    {
        foreach (Tab t in tabs)
        {
            if (t.Go != null) Destroy(t.Go);
        }
        tabs.Clear();

        for (int i = 0; i < branches.Count; i++)
        {
            SkillBranchDef c = branches[i];
            var tab = new Tab();
            Rect area = TabArea(i);

            tab.Go = HubUiKit.NewRect("Kategorie " + (i + 1), tabHolder);
            HubUiKit.Place((RectTransform)tab.Go.transform, area);

            // Alles darin rechnet ab der linken oberen Ecke des Knopfes.
            var local = new Rect(0f, 0f, area.width, area.height);

            tab.Glow  = Frame("Glow", tab.Go.transform, Grow(local, 1f), panelFill);
            tab.Fill  = Fill("Fill", tab.Go.transform, local, panelInset);
            tab.Outer = Frame("Outer", tab.Go.transform, local, c.Color);
            tab.Inner = Frame("Inner", tab.Go.transform, Inset(local, 2f), c.Color);

            var iconBox = new Rect(4f, (area.height - 14f) * 0.5f, 14f, 14f);
            tab.IconFill = Fill("IconBox", tab.Go.transform, iconBox, panelFill);
            tab.Icon = HubUiKit.NewImage("Icon", tab.Go.transform,
                                         c.Icon != null ? c.Icon : pixels.Disc, c.Color);
            HubUiKit.Place((RectTransform)tab.Icon.transform, Inset(iconBox, 1f));

            tab.Label = HubUiKit.NewText("Label", tab.Go.transform, font, categoryFontSize,
                                         panelInk, TextAlignmentOptions.Left);
            HubUiKit.Place((RectTransform)tab.Label.transform,
                           new Rect(22f, 0f, area.width - 26f, area.height));
            tab.Label.text = c.Name;

            // Der Zeiger steht rechts heraus und zeigt auf das grosse Feld.
            tab.Pointer = HubUiKit.NewImage("Pointer", tab.Go.transform, pixels.ArrowRight, c.Color);
            tab.Pointer.preserveAspect = false;
            HubUiKit.Place((RectTransform)tab.Pointer.transform,
                           new Rect(area.width, (area.height - 9f) * 0.5f, 5f, 9f));

            tabs.Add(tab);
        }
    }

    void BuildContent()
    {
        Fill("Content", screen, contentArea, panelInset);
        Frame("ContentFrame", screen, contentArea, panelBorder);

        headerFill  = Fill("Header", screen, contentHeaderArea, Color.white);
        headerFrame = Frame("HeaderFrame", screen, contentHeaderArea, Color.white);

        float plusY = contentHeaderArea.y + (contentHeaderArea.height - 5f) * 0.5f;
        headerPlusLeft  = Plus("HeaderPlusLeft", screen,
                               new Rect(contentHeaderArea.x + 5f, plusY, 5f, 5f), Color.white);
        headerPlusRight = Plus("HeaderPlusRight", screen,
                               new Rect(contentHeaderArea.xMax - 10f, plusY, 5f, 5f), Color.white);

        headerText = HubUiKit.NewText("HeaderLabel", screen, font, headerFontSize,
                                      Color.white, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)headerText.transform, contentHeaderArea);
    }

    void BuildDetail()
    {
        Fill("Detail", screen, detailArea, panelInset);
        Frame("DetailFrame", screen, detailArea, panelBorder);

        // Zwei Scheiben uebereinander: die untere schaut als Rand heraus. Zeigt
        // die Maus auf einen Knoten, tauschen beide auf dessen Form.
        orbRing = HubUiKit.NewImage("OrbRing", screen, pixels.Disc, Color.white);
        orbRing.preserveAspect = false;
        HubUiKit.Place((RectTransform)orbRing.transform, detailOrbArea);
        orbFill = HubUiKit.NewImage("Orb", screen, pixels.Disc, Color.white);
        orbFill.preserveAspect = false;
        HubUiKit.Place((RectTransform)orbFill.transform, Inset(detailOrbArea, 2f));

        detailName = HubUiKit.NewText("DetailName", screen, font, detailNameFontSize,
                                      panelInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)detailName.transform, detailNameArea);

        Divider("Divider1", detailDivider1, out _, out _, out _);

        detailText = HubUiKit.NewText("DetailText", screen, font, detailFontSize,
                                      panelInkDim, TextAlignmentOptions.Top);
        HubUiKit.Place((RectTransform)detailText.transform, detailTextArea);
        detailText.lineSpacing = detailLineSpacing;

        Divider("Divider2", detailDivider2, out divider2a, out divider2b, out dividerPlus2);

        detailQuote = HubUiKit.NewText("DetailQuote", screen, font, detailFontSize,
                                       panelInkDim, TextAlignmentOptions.Top);
        HubUiKit.Place((RectTransform)detailQuote.transform, detailQuoteArea);
        detailQuote.lineSpacing = detailLineSpacing;
    }

    void BuildBackButton()
    {
        backFillImage = Fill("Back", screen, backArea, backFill);
        Frame("BackFrame", screen, backArea, backBorder);

        TextMeshProUGUI label = HubUiKit.NewText("BackLabel", screen, font, buttonFontSize,
                                                 backInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)label.transform, backArea);
        label.text = "< " + backLabel;

        pointsText = HubUiKit.NewText("Points", screen, font, buttonFontSize,
                                      backInk, TextAlignmentOptions.Right);
        HubUiKit.Place((RectTransform)pointsText.transform, pointsArea);
    }

    // ----------------------------------------------------------- Bausteine

    Image Fill(string name, Transform parent, Rect area, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, null, color);
        HubUiKit.Place((RectTransform)img.transform, area);
        return img;
    }

    /// <summary>1px-Rahmen ueber der Flaeche, innen offen.</summary>
    Image Frame(string name, Transform parent, Rect area, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, pixels.Frame, color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.preserveAspect = false;
        HubUiKit.Place((RectTransform)img.transform, area);
        return img;
    }

    Image Plus(string name, Transform parent, Rect area, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, pixels.Plus, color);
        img.preserveAspect = false;
        HubUiKit.Place((RectTransform)img.transform, area);
        return img;
    }

    /// <summary>Trennlinie mit einem Kreuz in der Mitte - die Linie bricht dafuer auf.</summary>
    void Divider(string name, Rect area, out Image left, out Image right, out Image plus)
    {
        float half = Mathf.Max(0f, (area.width - 9f) * 0.5f);
        left  = Fill(name + "Left", screen, new Rect(area.x, area.y, half, area.height), panelBorder);
        right = Fill(name + "Right", screen,
                     new Rect(area.xMax - half, area.y, half, area.height), panelBorder);
        plus  = Plus(name + "Plus", screen,
                     new Rect(area.center.x - 2.5f, area.y - 2f, 5f, 5f), panelBorder);
    }

    static Rect Inset(Rect r, float by) =>
        new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f);

    static Rect Grow(Rect r, float by) => Inset(r, -by);

    Rect TabArea(int index) =>
        new Rect(categoryArea.x,
                 categoryArea.y + index * (categoryArea.height + categoryGap),
                 categoryArea.width, categoryArea.height);

    // --------------------------------------------------------------- Oeffnen

    public void Open()
    {
        if (IsOpen) return;

        Build();

        // Der Baum haengt am gewaehlten Charakter - erst holen, dann anzeigen.
        Skills.Sync();
        LoadTree();

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        root.SetActive(true);

        hover = Hit.None;
        hoverTab = -1;
        Refresh();

        HubUI.PushModal();
        HubUI.Instance.SetPlayerFrozen(true);

        PlaySfx(openClip);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root.SetActive(false);

        HubUI.PopModal();
        HubUI.Instance.SetPlayerFrozen(false);

        PlaySfx(closeClip);
    }

    /// <summary>Uebernimmt den aktiven Baum in die Knoepfe und ins Feld.</summary>
    void LoadTree()
    {
        SkillTreeDef tree = Skills.ActiveTree;

        // Derselbe Baum wie beim letzten Mal? Dann reicht der Zustand.
        if (tree == shownTree && tabs.Count == branches.Count && tabs.Count > 0) return;

        shownTree = tree;

        branches.Clear();
        if (tree != null) branches.AddRange(tree.Branches);

        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, branches.Count - 1));

        BuildTabs();
        graph.Show(Current);
    }

    void OnSkillsChanged()
    {
        if (!IsOpen) return;
        graph.Refresh();
        Refresh();
    }

    // -------------------------------------------------------------- Laufzeit

    void Update()
    {
        if (!IsOpen) return;

        // Das [E], mit dem das Fenster aufgeht, darf nicht gleich durchklicken
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0)) ClickAt(hover, hoverTab);

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        // Hoch/Runter wechselt die Kategorie, Links/Rechts scrollt im Baum -
        // genau so, wie der Baum aufgebaut ist.
        if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);

        if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) ScrollTree(-1f);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ScrollTree(+1f);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) ScrollTree(wheel > 0f ? -1f : +1f);
    }

    /// <summary>
    /// Die Mausposition in Pixeln der 320x180-Vorlage, Nullpunkt links oben -
    /// also in genau denselben Koordinaten, in denen die Kaesten oben stehen.
    /// </summary>
    bool MousePixel(out Vector2 pixel)
    {
        pixel = default;
        if (screen == null) return false;

        // Overlay-Canvas: die Kamera ist hier bewusst null.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screen, Input.mousePosition, null, out Vector2 local))
            return false;

        pixel = new Vector2(local.x + referenceResolution.x * 0.5f,
                            referenceResolution.y * 0.5f - local.y);
        return true;
    }

    void UpdateHover()
    {
        Hit was = hover;
        int wasTab = hoverTab;

        hover = Hit.None;
        hoverTab = -1;

        bool valid = MousePixel(out Vector2 m);

        // Der Graph prueft selbst, ob ein Knoten getroffen ist - er weiss als
        // Einziger, wie weit gerade gescrollt wurde.
        bool graphChanged = graph.UpdateHover(valid, m);

        if (valid)
        {
            if (backArea.Contains(m)) hover = Hit.Back;
            else if (graph.Hovered != null) hover = Hit.Node;
            else if (graph.CanScrollLeft && graph.ScrollLeftArea.Contains(m)) hover = Hit.ScrollLeft;
            else if (graph.CanScrollRight && graph.ScrollRightArea.Contains(m)) hover = Hit.ScrollRight;
            else
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    if (!TabArea(i).Contains(m)) continue;
                    hover = Hit.Category;
                    hoverTab = i;
                    break;
                }
            }
        }

        if (hover != was || hoverTab != wasTab || graphChanged) Refresh();
    }

    void ClickAt(Hit what, int tab)
    {
        switch (what)
        {
            case Hit.Category:
                if (tab != selected) Select(tab);
                break;

            case Hit.Node:
                BuyHovered();
                break;

            case Hit.ScrollLeft:
                ScrollTree(-1f);
                break;

            case Hit.ScrollRight:
                ScrollTree(+1f);
                break;

            case Hit.Back:
                Close();
                break;
        }
    }

    void BuyHovered()
    {
        bool bought = graph.ClickHovered(out bool hitNode);
        if (!hitNode) return;

        AudioController ac = AudioController.Instance;

        // Gekauft klingt wie ein Menueklick, abgelehnt wie ein Treffer - denselben
        // Unterschied macht der Shop auch.
        if (ac != null) ac.PalySound(bought ? ac.MenuClick : ac.PlayerHit);

        Refresh();
    }

    void ScrollTree(float steps)
    {
        if (!graph.Scroll(steps)) return;

        // Nach dem Scrollen liegt unter der Maus etwas anderes.
        UpdateHover();
        Refresh();
    }

    void Move(int delta)
    {
        if (branches.Count == 0) return;

        int next = Mathf.Clamp(selected + delta, 0, branches.Count - 1);
        if (next == selected) return;

        Select(next);
    }

    void Select(int index)
    {
        selected = index;
        PlaySfx(moveClip);

        graph.Show(Current);
        Refresh();
    }

    // -------------------------------------------------------------- Anzeige

    void Refresh()
    {
        RefreshTabs();
        RefreshContent();
        RefreshDetail();

        backFillImage.color = hover == Hit.Back ? Lift(backFill, hoverLift) : backFill;

        if (pointsText != null) pointsText.text = string.Format(pointsFormat, Skills.Currency);
    }

    void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count && i < branches.Count; i++)
        {
            Tab t = tabs[i];
            SkillBranchDef c = branches[i];

            bool isSelected = i == selected;
            bool isHover = hover == Hit.Category && hoverTab == i;

            Color border = Shade(c.Color, borderShade);

            t.Glow.gameObject.SetActive(isSelected);
            t.Pointer.gameObject.SetActive(isSelected);
            t.Pointer.color = c.Color;

            if (isSelected)
            {
                // Gewaehlt: der Knopf traegt seine Farbe, die Schrift wird hell -
                // oder dunkel, wenn die Farbe dafuer zu hell ist (Gelb).
                t.Fill.color     = c.Color;
                t.Outer.color    = border;
                t.Inner.color    = Lift(c.Color, 0.35f);
                t.Label.color    = InkOn(c.Color);
                t.IconFill.color = Lift(c.Color, 0.55f);
            }
            else
            {
                t.Fill.color     = isHover ? Lift(panelInset, hoverLift * 0.5f) : panelInset;
                t.Outer.color    = isHover ? c.Color : Shade(c.Color, borderShade * 0.5f);
                t.Inner.color    = Lift(c.Color, 0.5f);
                t.Label.color    = border;
                t.IconFill.color = panelFill;
            }

            // Die Ersatzscheibe bleibt in der Kategoriefarbe - auf dem gewaehlten
            // Knopf in der dunklen, sonst verschwindet sie im hellen Kaestchen.
            t.Icon.color = c.Icon != null ? Color.white
                                          : (isSelected ? border : c.Color);
        }
    }

    void RefreshContent()
    {
        SkillBranchDef c = Current;
        if (c == null)
        {
            headerText.text = "";
            return;
        }

        Color border = Shade(c.Color, borderShade);
        Color ink = InkOn(c.Color);

        headerFill.color  = c.Color;
        headerFrame.color = border;
        headerText.color  = ink;
        headerPlusLeft.color = headerPlusRight.color = ink;

        headerText.text = string.IsNullOrWhiteSpace(c.Path)
            ? string.Format(pathLabelFormat, c.Name)
            : c.Path;
    }

    /// <summary>
    /// Die Karte rechts zeigt, worum es geht. Der Reihe nach:
    ///   - Maus auf einem Knoten: dieser Knoten - Form, Name, Wirkung, Preis.
    ///   - Maus auf einem Kategorieknopf links: DIESE Kategorie, auch wenn sie
    ///     noch gar nicht gewaehlt ist. So sieht man vor dem Klick, was einen
    ///     dort erwartet.
    ///   - Sonst: die gewaehlte Kategorie.
    /// </summary>
    void RefreshDetail()
    {
        // Die Karte folgt der Maus ueber die Knoepfe links, das grosse Feld
        // daneben bleibt aber auf der gewaehlten Kategorie.
        SkillBranchDef c = (hover == Hit.Category && hoverTab >= 0 && hoverTab < branches.Count)
            ? branches[hoverTab]
            : Current;

        if (c == null)
        {
            detailName.text = detailText.text = detailQuote.text = "";
            return;
        }

        Color border = Shade(c.Color, borderShade);

        // Ein Knoten unter der Maus gewinnt - dann steht die Maus ohnehin nicht
        // gleichzeitig auf einem Knopf links.
        SkillNodeDef node = graph.Hovered;

        if (node != null)
        {
            bool unlocked = Skills.IsUnlocked(node);
            bool open = !unlocked && Skills.RequirementsMet(node);

            orbRing.sprite = orbShapes.Outline(node.Shape);
            orbFill.sprite = orbShapes.Fill(node.Shape);

            orbRing.color = unlocked || open ? border : Shade(panelBorder, 0.35f);
            orbFill.color = unlocked ? c.Color : open ? panelFill : panelBorder;

            detailName.text  = node.Name;
            detailName.color = border;
            detailText.text  = node.Description;

            // Der Startknoten wird nicht gekauft - "GEKAUFT" waere dort Unsinn.
            detailQuote.text = node.IsStart ? ""
                             : unlocked     ? boughtLabel
                             : open         ? string.Format(priceFormat, node.Price)
                                            : MissingText(node);
        }
        else
        {
            orbRing.sprite = pixels.Disc;
            orbFill.sprite = pixels.Disc;

            orbRing.color = border;
            orbFill.color = c.Color;

            detailName.text  = c.Name;
            detailName.color = border;
            detailText.text  = c.Description;
            detailQuote.text = c.Quote;
        }

        // Ohne Text unten braucht es auch die zweite Trennlinie nicht.
        bool hasFooter = !string.IsNullOrWhiteSpace(detailQuote.text);
        divider2a.gameObject.SetActive(hasFooter);
        divider2b.gameObject.SetActive(hasFooter);
        dividerPlus2.gameObject.SetActive(hasFooter);
    }

    /// <summary>Welche Knoten noch fehlen. Mehr als zwei werden nicht aufgezaehlt.</summary>
    string MissingText(SkillNodeDef node)
    {
        var missing = new List<string>();

        foreach (SkillNodeDef parent in node.Requires)
        {
            if (!Skills.IsUnlocked(parent)) missing.Add(parent.Name);
        }

        if (missing.Count == 0) return lockedLabel;
        if (missing.Count > 2) return lockedLabel;

        return lockedLabel + ": " + string.Join(" + ", missing);
    }

    // ---------------------------------------------------------------- Farben

    /// <summary>Hellt eine Farbe auf, ohne ihre Deckkraft anzutasten.</summary>
    static Color Lift(Color c, float amount)
    {
        return new Color(Mathf.Lerp(c.r, 1f, amount),
                         Mathf.Lerp(c.g, 1f, amount),
                         Mathf.Lerp(c.b, 1f, amount), c.a);
    }

    /// <summary>Dunkelt eine Farbe ab, ohne ihre Deckkraft anzutasten.</summary>
    static Color Shade(Color c, float amount)
    {
        return new Color(Mathf.Lerp(c.r, 0f, amount),
                         Mathf.Lerp(c.g, 0f, amount),
                         Mathf.Lerp(c.b, 0f, amount), c.a);
    }

    /// <summary>
    /// Welche Schriftfarbe auf dieser Flaeche lesbar ist. Blau, Rot und Gruen
    /// vertragen helle Schrift - Gelb nicht, darauf wird sie dunkel.
    /// </summary>
    Color InkOn(Color background)
    {
        float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
        return luma > 0.6f ? panelInk : textOnColor;
    }

    // ----------------------------------------------------------------- Ton

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
