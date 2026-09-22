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

    [Header("Titelschild")]
    [Tooltip("Das Schild liegt etwas waermer als das Papier - sonst verschwindet es darin.")]
    [SerializeField] private Color plaqueFill  = new Color32(0xE2, 0xD2, 0xA8, 0xFF);
    [SerializeField] private Color plaqueShade = new Color32(0xB0, 0x93, 0x63, 0xFF);
    [Tooltip("Nieten, Kreuze und die Raute vor den Skillpunkten.")]
    [SerializeField] private Color accentGold  = new Color32(0xC8, 0xA2, 0x4A, 0xFF);

    [Header("Zierrat")]
    [Tooltip("Wie dunkel die Schatten unter Papier, Schild und Knoepfen liegen. 0 = flach.")]
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.34f;
    [Tooltip("Wie deckend der Hub hinter dem Fenster abgedeckt wird. 1 = gar nicht mehr zu sehen.")]
    [SerializeField, Range(0f, 1f)] private float backdropOpacity = 0.93f;
    [Tooltip("Das warme Licht hinter dem Papier - Fackelschein, solange es keine Pixelart gibt.")]
    [SerializeField] private Color glowColor = new Color32(0xFF, 0xC2, 0x7A, 0xFF);
    [SerializeField, Range(0f, 0.6f)] private float glowStrength = 0.17f;
    [Tooltip("Wie dunkel die Ecken auslaufen.")]
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.55f;
    [Tooltip("Wie deutlich das Papierkorn zu sehen ist. 0 = glatte Flaeche.")]
    [SerializeField, Range(0f, 1f)] private float grainStrength = 0.30f;
    [Tooltip("Licht pulsiert, Funken blinken, der gewaehlte Knopf atmet. Aus = alles steht still.")]
    [SerializeField] private bool animateDecor = true;

    [Header("Farbe der Fusszeile in der Karte")]
    [SerializeField] private Color priceInk  = new Color32(0x8A, 0x64, 0x15, 0xFF);
    [SerializeField] private Color ownedInk  = new Color32(0x4C, 0x7A, 0x3A, 0xFF);
    [SerializeField] private Color lockedInk = new Color32(0x9A, 0x4A, 0x3C, 0xFF);

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
        public Image Sheen;      // helle Linie unter der Oberkante
        public Image Shade;      // dunkle Linie ueber der Unterkante
        public Image Outer;      // Rahmen in Kategoriefarbe
        public Image Inner;      // duenne Linie innen
        public Image IconFill;   // Kaestchen hinter dem Symbol
        public Image IconEdge;
        public Image Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI LabelShadow;
        public Image Pointer;    // der Zeiger, der rechts heraussteht
        public Image PointerEdge;
    }

    /// <summary>Ein blinkender Funke. Die Farbe kommt aus der Kategorie, die Deckkraft aus der Zeit.</summary>
    class Twinkle
    {
        public Image Image;
        public float Phase;
        public float Base;       // Grunddeckkraft, bevor das Blinken darauf geht
    }

    /// <summary>Was gerade unter der Maus liegt.</summary>
    enum Hit { None, Category, Back, ScrollLeft, ScrollRight, Node }

    readonly List<Tab> tabs = new List<Tab>();
    readonly List<Twinkle> twinkles = new List<Twinkle>();
    readonly HubPixelSprites pixels = new HubPixelSprites();
    readonly SkilltreeSkin skin = new SkilltreeSkin();

    GameObject root;
    RectTransform screen;
    Transform tabHolder;
    Image glowImage, vignetteImage;
    Image headerFill, headerSheen, headerShade, headerLineTop, headerLineBottom;
    Image headerTipLeft, headerTipRight, headerTipLeftEdge, headerTipRightEdge;
    Image headerPlusLeft, headerPlusRight;
    Image orbShadow, orbRing, orbFill, orbGloss;
    Image divider1a, divider1b, dividerPlus1;
    Image divider2a, divider2b, dividerPlus2;
    Image backFillImage, backSheen, backShade, backArrow, pointsGem;
    RectTransform backArrowRect;
    TextMeshProUGUI headerText, headerTextShadow;
    TextMeshProUGUI detailName, detailNameShadow, detailText, detailQuote, pointsText;

    /// <summary>Rundungen - hier stehen sie einmal, damit das Fenster eine Handschrift hat.</summary>
    const int PanelRadius  = 3;   // das grosse Papier
    const int FieldRadius  = 2;   // Felder, Schild, Knoepfe
    const int SmallRadius  = 1;   // Kaestchen und duenne Innenlinien
    const int BannerTip    = 6;   // Breite der Spitzen am Banner

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
        skin.Dispose();
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

        // FindTextFont, nicht FindPixelFont: ZURÜCK und GLÜCK brauchen Umlaute,
        // und die kennt ThaleahFat nicht.
        if (font == null) font = PixelUI.FindTextFont();
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
        //
        // Nicht ganz deckend: ein Hauch vom Hub bleibt stehen, damit das Fenster
        // ueber dem Raum schwebt und nicht auf einer leeren Platte liegt.
        var backdrop = HubUiKit.NewImage("Backdrop", root.transform, null,
                                         Alpha(backdropColor, backdropOpacity));
        HubUiKit.Stretch((RectTransform)backdrop.transform);

        // Die Vignette geht ueber das ganze Fenster, nicht nur ueber die 320x180 -
        // sonst bliebe bei breiten Bildschirmen aussen eine harte Kante stehen.
        // Sie ist die eine Flaeche, die weich sein darf.
        vignetteImage = HubUiKit.NewImage("Vignette", root.transform, skin.Vignette,
                                          new Color(0f, 0f, 0f, vignetteStrength));
        vignetteImage.preserveAspect = false;
        HubUiKit.Stretch((RectTransform)vignetteImage.transform);

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
        else
        {
            // Warmes Licht hinter dem Papier - der Fackelschein aus dem
            // Konzeptbild, bis es die Pixelart dafuer gibt.
            glowImage = HubUiKit.NewImage("Glow", screen, skin.Glow,
                                          Alpha(glowColor, glowStrength));
            glowImage.preserveAspect = false;
            var gr = (RectTransform)glowImage.transform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(380f, 300f);
            gr.anchoredPosition = new Vector2(0f, 10f);
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
            Shadow      = shadowStrength,
        }, skin, font);

        Skills.Changed += OnSkillsChanged;

        root.SetActive(false);
    }

    /// <summary>
    /// Das grosse Papier. Von unten nach oben: Schatten, Flaeche, Korn, die
    /// helle Oberkante und die dunkle Unterkante darin, dann die doppelte
    /// Aussenlinie und zuletzt die vier Eckwinkel.
    /// </summary>
    void BuildPanel()
    {
        if (panelSprite != null)
        {
            Image img = HubUiKit.NewImage("Panel", screen, panelSprite, Color.white);
            img.preserveAspect = false;
            HubUiKit.Place((RectTransform)img.transform, panelArea);
            return;
        }

        Round("PanelShadow", screen, Offset(panelArea, 2f, 3f),
              Alpha(Color.black, shadowStrength), PanelRadius);

        Round("Panel", screen, panelArea, panelFill, PanelRadius);

        if (grainStrength > 0f)
        {
            Rect grainArea = Inset(panelArea, 3f);
            Image grain = HubUiKit.NewImage("PanelGrain", screen,
                skin.Grain(Mathf.RoundToInt(grainArea.width),
                           Mathf.RoundToInt(grainArea.height), 7331),
                Alpha(panelBorder, grainStrength));
            grain.preserveAspect = false;
            HubUiKit.Place((RectTransform)grain.transform, grainArea);
        }

        // Erhaben: hell an der Oberkante, dunkel an der Unterkante.
        CapTop("PanelLight", screen, Inset(panelArea, 1f), Lift(panelFill, 0.6f), FieldRadius);
        CapBottom("PanelShade", screen, Inset(panelArea, 1f), Shade(panelFill, 0.14f), FieldRadius);

        Round("PanelEdge", screen, panelArea, Shade(panelBorder, 0.52f), PanelRadius, edge: true);
        Round("PanelEdgeInner", screen, Inset(panelArea, 3f), panelBorder, FieldRadius, edge: true);

        Brackets("PanelCorner", screen, Inset(panelArea, 6f), panelBorder);
    }

    /// <summary>
    /// Das Titelschild. Es liegt bewusst ueber der Oberkante des Papiers und ist
    /// darum das einzige Stueck, das einen eigenen, waermeren Ton bekommt -
    /// sonst wuerde es im Papier verschwinden.
    /// </summary>
    void BuildTitle()
    {
        Round("TitleShadow", screen, Offset(titleArea, 2f, 3f),
              Alpha(Color.black, Mathf.Min(1f, shadowStrength + 0.1f)), FieldRadius);

        Round("Title", screen, titleArea, plaqueFill, FieldRadius);
        CapTop("TitleLight", screen, Inset(titleArea, 1f), Lift(plaqueFill, 0.55f), FieldRadius);

        // Zwei Pixel dunkler Sockel unten - das gibt dem Schild Dicke.
        Fill("TitleBase", screen,
             new Rect(titleArea.x + 2f, titleArea.yMax - 4f, titleArea.width - 4f, 2f),
             plaqueShade);

        Round("TitleEdge", screen, titleArea, panelInk, FieldRadius, edge: true);
        Round("TitleEdgeInner", screen, Inset(titleArea, 2f), plaqueShade, SmallRadius, edge: true);

        // Vier Nieten in den Ecken.
        foreach (Vector2 p in CornerPoints(Inset(titleArea, 4f), 3f))
            Raw("TitleStud", screen, new Rect(p.x, p.y, 3f, 3f), skin.Stud, accentGold);

        // Geschnitten statt gedruckt: eine helle Kopie einen Pixel tiefer,
        // darueber die dunkle Schrift.
        TextMeshProUGUI shadow = HubUiKit.NewText("TitleLabelLight", screen, font, titleFontSize,
                                                  Lift(plaqueFill, 0.75f), TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)shadow.transform, Offset(titleArea, 0f, 1f));
        shadow.text = titleLabel;

        TextMeshProUGUI t = HubUiKit.NewText("TitleLabel", screen, font, titleFontSize,
                                             panelInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)t.transform, titleArea);
        t.text = titleLabel;

        // Die beiden Kreuze links und rechts der Schrift.
        float plusY = titleArea.y + (titleArea.height - 5f) * 0.5f;
        Plus("TitlePlusLeft", screen, new Rect(titleArea.x + 9f, plusY, 5f, 5f), accentGold);
        Plus("TitlePlusRight", screen, new Rect(titleArea.xMax - 14f, plusY, 5f, 5f), accentGold);
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
            HubUiKit.Place((RectTransform)tab.Go.transform, Snap(area));

            // Alles darin rechnet ab der linken oberen Ecke des Knopfes.
            var local = new Rect(0f, 0f, area.width, area.height);

            // Der Schatten liegt ganz unten und bewegt sich nie - er gehoert zum
            // Knopf, nicht zu seinem Zustand.
            Round("Shadow", tab.Go.transform, Offset(local, 2f, 2f),
                  Alpha(Color.black, shadowStrength * 0.8f), FieldRadius);

            tab.Glow  = Round("Glow", tab.Go.transform, Grow(local, 1f), panelFill,
                              PanelRadius, edge: true);
            tab.Fill  = Round("Fill", tab.Go.transform, local, panelInset, FieldRadius);
            tab.Sheen = CapTop("Sheen", tab.Go.transform, Inset(local, 1f), panelFill, FieldRadius);
            tab.Shade = CapBottom("Shade", tab.Go.transform, Inset(local, 1f), panelBorder, FieldRadius);
            tab.Outer = Round("Outer", tab.Go.transform, local, c.Color, FieldRadius, edge: true);
            tab.Inner = Round("Inner", tab.Go.transform, Inset(local, 2f), c.Color,
                              SmallRadius, edge: true);

            var iconBox = new Rect(4f, (area.height - 14f) * 0.5f, 14f, 14f);
            tab.IconFill = Round("IconBox", tab.Go.transform, iconBox, panelFill, SmallRadius);
            tab.IconEdge = Round("IconEdge", tab.Go.transform, iconBox, c.Color,
                                 SmallRadius, edge: true);
            tab.Icon = HubUiKit.NewImage("Icon", tab.Go.transform,
                                         c.Icon != null ? c.Icon : pixels.Disc, c.Color);
            // Genau 12x12 - so gross sind die gemalten Kategoriesymbole, und nur
            // in ihrer echten Groesse bleiben sie scharf.
            Place(tab.Icon, Inset(iconBox, 1f));

            // Erst der Schatten der Schrift, dann die Schrift - sonst steht sie
            // auf gesaettigtem Blau oder Rot wie aufgeklebt da.
            tab.LabelShadow = HubUiKit.NewText("LabelShadow", tab.Go.transform, font,
                                               categoryFontSize, Color.clear,
                                               TextAlignmentOptions.Left);
            HubUiKit.Place((RectTransform)tab.LabelShadow.transform,
                           Snap(new Rect(23f, 1f, area.width - 26f, area.height)));
            tab.LabelShadow.text = c.Name;

            tab.Label = HubUiKit.NewText("Label", tab.Go.transform, font, categoryFontSize,
                                         panelInk, TextAlignmentOptions.Left);
            HubUiKit.Place((RectTransform)tab.Label.transform,
                           Snap(new Rect(22f, 0f, area.width - 26f, area.height)));
            tab.Label.text = c.Name;

            // Der Zeiger steht rechts heraus und zeigt auf das grosse Feld. Die
            // dunkle Kopie dahinter setzt ihn vom Papier ab.
            // Dieselbe Groesse wie der Zeiger, nur einen Pixel versetzt - in
            // einen breiteren Kasten gezogen wuerde aus dem Pfeil ein Kamm.
            tab.PointerEdge = HubUiKit.NewImage("PointerEdge", tab.Go.transform,
                                                pixels.ArrowRight, Alpha(Color.black, 0.3f));
            tab.PointerEdge.preserveAspect = false;
            Place(tab.PointerEdge, new Rect(area.width + 1f, (area.height - 9f) * 0.5f + 1f, 5f, 9f));

            tab.Pointer = HubUiKit.NewImage("Pointer", tab.Go.transform, pixels.ArrowRight, c.Color);
            tab.Pointer.preserveAspect = false;
            Place(tab.Pointer, new Rect(area.width, (area.height - 9f) * 0.5f, 5f, 9f));

            tabs.Add(tab);
        }
    }

    /// <summary>
    /// Das grosse Feld und das Banner darin. Das Feld liegt VERTIEFT im Papier -
    /// darum die dunkle Linie oben und die helle unten, also genau andersherum
    /// als bei den Knoepfen.
    /// </summary>
    void BuildContent()
    {
        Round("Content", screen, contentArea, panelInset, FieldRadius);
        CapTop("ContentDepthTop", screen, Inset(contentArea, 1f),
               Shade(panelBorder, 0.18f), FieldRadius);
        CapBottom("ContentDepthBottom", screen, Inset(contentArea, 1f),
                  Lift(panelFill, 0.7f), FieldRadius);
        Round("ContentFrame", screen, contentArea, panelBorder, FieldRadius, edge: true);
        Brackets("ContentCorner", screen, Inset(contentArea, 3f), Shade(panelBorder, 0.2f));

        BuildHeaderBanner();
    }

    /// <summary>
    /// Das Banner ueber dem Baum: ein Band mit zwei Spitzen, wie im Konzeptbild.
    /// Der Koerper bleibt eckig, damit die Spitzen fugenlos anschliessen - die
    /// waagerechten Linien oben und unten laufen einfach durch.
    /// </summary>
    void BuildHeaderBanner()
    {
        Rect a = contentHeaderArea;
        int h = Mathf.Max(3, Mathf.RoundToInt(a.height));

        var leftTip  = new Rect(a.x - BannerTip, a.y, BannerTip, h);
        var rightTip = new Rect(a.xMax, a.y, BannerTip, h);

        // Schatten unter Band und Spitzen.
        Fill("HeaderShadow", screen, Offset(a, 1f, 2f), Alpha(Color.black, shadowStrength * 0.7f));
        Raw("HeaderShadowL", screen, Offset(leftTip, 1f, 2f),
            skin.Tip(BannerTip, h, false, false), Alpha(Color.black, shadowStrength * 0.7f));
        Raw("HeaderShadowR", screen, Offset(rightTip, 1f, 2f),
            skin.Tip(BannerTip, h, true, false), Alpha(Color.black, shadowStrength * 0.7f));

        headerTipLeft  = Raw("HeaderTipL", screen, leftTip,
                             skin.Tip(BannerTip, h, false, false), Color.white);
        headerTipRight = Raw("HeaderTipR", screen, rightTip,
                             skin.Tip(BannerTip, h, true, false), Color.white);

        headerFill = Fill("Header", screen, a, Color.white);

        // Der Verlauf im Band: hell unter der Oberkante, dunkel ueber der Unterkante.
        headerSheen = Fill("HeaderSheen", screen, new Rect(a.x, a.y + 1f, a.width, 1f), Color.white);
        headerShade = Fill("HeaderShade", screen, new Rect(a.x, a.yMax - 2f, a.width, 1f), Color.white);

        headerLineTop    = Fill("HeaderLineTop", screen, new Rect(a.x, a.y, a.width, 1f), Color.white);
        headerLineBottom = Fill("HeaderLineBottom", screen,
                                new Rect(a.x, a.yMax - 1f, a.width, 1f), Color.white);

        headerTipLeftEdge  = Raw("HeaderTipLEdge", screen, leftTip,
                                 skin.Tip(BannerTip, h, false, true), Color.white);
        headerTipRightEdge = Raw("HeaderTipREdge", screen, rightTip,
                                 skin.Tip(BannerTip, h, true, true), Color.white);

        float plusY = a.y + (a.height - 5f) * 0.5f;
        headerPlusLeft  = Plus("HeaderPlusLeft", screen,
                               new Rect(a.x + 5f, plusY, 5f, 5f), Color.white);
        headerPlusRight = Plus("HeaderPlusRight", screen,
                               new Rect(a.xMax - 10f, plusY, 5f, 5f), Color.white);

        headerTextShadow = HubUiKit.NewText("HeaderLabelShadow", screen, font, headerFontSize,
                                            Color.white, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)headerTextShadow.transform, Offset(a, 0f, 1f));

        headerText = HubUiKit.NewText("HeaderLabel", screen, font, headerFontSize,
                                      Color.white, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)headerText.transform, a);
    }

    void BuildDetail()
    {
        Round("Detail", screen, detailArea, panelInset, FieldRadius);
        CapTop("DetailDepthTop", screen, Inset(detailArea, 1f),
               Shade(panelBorder, 0.18f), FieldRadius);
        CapBottom("DetailDepthBottom", screen, Inset(detailArea, 1f),
                  Lift(panelFill, 0.7f), FieldRadius);
        Round("DetailFrame", screen, detailArea, panelBorder, FieldRadius, edge: true);
        Brackets("DetailCorner", screen, Inset(detailArea, 3f), Shade(panelBorder, 0.2f));

        // Die Formen sind 15x15 Pixel gross. Auf 32 gezogen wuerde jeder
        // Texturpixel 2,13 Bildpunkte breit - die Kante der Kugel liefe dann
        // ungleich dick. Also auf das naechste ganze Vielfache heruntergehen
        // und mittig in den ausgemessenen Kasten setzen: der Kasten bleibt, wo
        // er ist, die Kugel wird nur sauber.
        Rect orb = SnapToShape(detailOrbArea);

        // Alle vier Lagen in derselben Groesse, wie bei den Knoten im Baum:
        // Schatten, Fuellung, Glanz, Kante. Die Kante liegt obenauf und schaut
        // nur am Rand hervor - so passt sie zu jeder Form.
        orbShadow = Raw("OrbShadow", screen, Offset(orb, 1f, 2f),
                        orbShapes.Fill(SkillShape.Kreis),
                        Alpha(Color.black, shadowStrength * 0.8f));
        orbFill  = Raw("Orb",      screen, orb, orbShapes.Fill(SkillShape.Kreis), Color.white);
        orbGloss = Raw("OrbGloss", screen, orb, orbShapes.Gloss(SkillShape.Kreis), Color.white);
        orbRing  = Raw("OrbRing",  screen, orb, orbShapes.Outline(SkillShape.Kreis), Color.white);

        // Funken um die Kugel - sie blinken in AnimateDecor. Jeder bekommt genau
        // die Groesse seines Sprites, sonst franst das Kreuz aus.
        AddTwinkle(new Rect(orb.x - 5f, orb.y + 1f, 7f, 7f), skin.Sparkle, 0.9f);
        AddTwinkle(new Rect(orb.xMax + 1f, orb.y + 3f, 3f, 3f), skin.Spark, 0.7f);
        AddTwinkle(new Rect(orb.x - 2f, orb.yMax - 6f, 3f, 3f), skin.Spark, 0.6f);

        TextMeshProUGUI nameShadow = HubUiKit.NewText("DetailNameShadow", screen, font,
                                                      detailNameFontSize, Alpha(panelFill, 0.9f),
                                                      TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)nameShadow.transform, Offset(detailNameArea, 0f, 1f));

        detailName = HubUiKit.NewText("DetailName", screen, font, detailNameFontSize,
                                      panelInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)detailName.transform, detailNameArea);
        detailNameShadow = nameShadow;

        Divider("Divider1", detailDivider1, out divider1a, out divider1b, out dividerPlus1);

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

    /// <summary>
    /// Zurueck-Knopf und Punkteanzeige. Beide sitzen unter dem Papier auf dem
    /// Hintergrund und bekommen darum dasselbe dunkle Holz - so lesen sie sich
    /// als ein Paar und nicht als zwei lose Teile.
    /// </summary>
    void BuildBackButton()
    {
        Round("BackShadow", screen, Offset(backArea, 2f, 2f),
              Alpha(Color.black, shadowStrength), FieldRadius);

        backFillImage = Round("Back", screen, backArea, backFill, FieldRadius);
        backSheen = CapTop("BackSheen", screen, Inset(backArea, 1f),
                           Lift(backFill, 0.3f), FieldRadius);
        backShade = CapBottom("BackShade", screen, Inset(backArea, 1f),
                              Shade(backFill, 0.35f), FieldRadius);
        Round("BackFrame", screen, backArea, backBorder, FieldRadius, edge: true);

        backArrow = Raw("BackArrow", screen,
                        new Rect(backArea.x + 6f, backArea.y + (backArea.height - 7f) * 0.5f, 7f, 7f),
                        skin.ArrowLeft, backInk);
        backArrowRect = (RectTransform)backArrow.transform;

        TextMeshProUGUI label = HubUiKit.NewText("BackLabel", screen, font, buttonFontSize,
                                                 backInk, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)label.transform,
                       new Rect(backArea.x + 17f, backArea.y, backArea.width - 22f, backArea.height));
        label.text = backLabel;

        // Die Punkteanzeige bekommt dieselbe Platte - rechtsbuendig als
        // Gegengewicht zum Knopf links.
        Round("PointsShadow", screen, Offset(pointsArea, 2f, 2f),
              Alpha(Color.black, shadowStrength), FieldRadius);
        Round("PointsPlate", screen, pointsArea, backFill, FieldRadius);
        CapTop("PointsSheen", screen, Inset(pointsArea, 1f), Lift(backFill, 0.3f), FieldRadius);
        CapBottom("PointsShade", screen, Inset(pointsArea, 1f), Shade(backFill, 0.35f), FieldRadius);
        Round("PointsFrame", screen, pointsArea, backBorder, FieldRadius, edge: true);

        pointsGem = Raw("PointsGem", screen,
                        new Rect(pointsArea.x + 6f, pointsArea.y + (pointsArea.height - 7f) * 0.5f, 7f, 7f),
                        skin.Gem, accentGold);

        pointsText = HubUiKit.NewText("Points", screen, font, buttonFontSize,
                                      backInk, TextAlignmentOptions.Right);
        HubUiKit.Place((RectTransform)pointsText.transform, Inset(pointsArea, 6f));
    }

    /// <summary>Legt einen blinkenden Funken an. Die Farbe setzt RefreshDetail.</summary>
    void AddTwinkle(Rect area, Sprite sprite, float strength)
    {
        Image img = Raw("Sparkle", screen, area, sprite, Color.clear);
        twinkles.Add(new Twinkle { Image = img, Phase = twinkles.Count * 1.9f, Base = strength });
    }

    // ----------------------------------------------------------- Bausteine

    /// <summary>
    /// Setzt ein Stueck auf ganze Pixel. Die Kaesten aus dem Inspector sind
    /// ganzzahlig - aber sobald etwas MITTIG in einen davon soll, faellt eine
    /// halbe heraus: (22-9)/2 ist 6,5. Ein Pfeil auf einem halben Pixel wird
    /// beim Zeichnen ungleich abgetastet, und aus der Spitze wird ein Kamm.
    /// Darum geht in diesem Fenster jedes Bild durch diese Zeile.
    /// </summary>
    static Rect Snap(Rect r) =>
        new Rect(Mathf.Round(r.x), Mathf.Round(r.y),
                 Mathf.Max(1f, Mathf.Round(r.width)), Mathf.Max(1f, Mathf.Round(r.height)));

    static void Place(Image img, Rect area) =>
        HubUiKit.Place((RectTransform)img.transform, Snap(area));

    Image Fill(string name, Transform parent, Rect area, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, null, color);
        Place(img, area);
        return img;
    }

    Image Plus(string name, Transform parent, Rect area, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, pixels.Plus, color);
        img.preserveAspect = false;
        Place(img, area);
        return img;
    }

    /// <summary>Ein Sprite, das genau in seinen Kasten gezogen wird - ohne 9-Slice.</summary>
    Image Raw(string name, Transform parent, Rect area, Sprite sprite, Color color)
    {
        Image img = HubUiKit.NewImage(name, parent, sprite, color);
        img.preserveAspect = false;
        Place(img, area);
        return img;
    }

    /// <summary>
    /// Eine Flaeche oder ein Rahmen mit abgeschraegten Ecken. Der Kern des
    /// ganzen Fensters: alles, was nicht strichduenn ist, kommt hier durch.
    /// </summary>
    Image Round(string name, Transform parent, Rect area, Color color, int radius, bool edge = false)
    {
        Image img = HubUiKit.NewImage(name, parent,
                                      edge ? skin.Edge(radius) : skin.Fill(radius), color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.preserveAspect = false;
        Place(img, area);
        return img;
    }

    /// <summary>Die helle Linie an der Oberkante - macht aus einer Flaeche einen Knopf.</summary>
    Image CapTop(string name, Transform parent, Rect area, Color color, int radius)
    {
        Image img = HubUiKit.NewImage(name, parent, skin.CapTop(radius), color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.preserveAspect = false;
        Place(img, area);
        return img;
    }

    /// <summary>Die dunkle Linie an der Unterkante.</summary>
    Image CapBottom(string name, Transform parent, Rect area, Color color, int radius)
    {
        Image img = HubUiKit.NewImage(name, parent, skin.CapBottom(radius), color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.preserveAspect = false;
        Place(img, area);
        return img;
    }

    /// <summary>Vier Eckwinkel an den Ecken eines Kastens - der Zierrat auf dem Papier.</summary>
    void Brackets(string name, Transform parent, Rect area, Color color)
    {
        Vector2[] p = CornerPoints(area, 5f);
        for (int i = 0; i < 4; i++)
            Raw(name + i, parent, new Rect(p[i].x, p[i].y, 5f, 5f), skin.Bracket(i), color);
    }

    /// <summary>
    /// Die vier Ecken eines Kastens fuer ein Stueck der Groesse <paramref name="size"/>,
    /// im Uhrzeigersinn ab links oben - genau so zaehlt SkilltreeSkin.Bracket.
    /// </summary>
    static Vector2[] CornerPoints(Rect r, float size)
    {
        return new[]
        {
            new Vector2(r.x, r.y),
            new Vector2(r.xMax - size, r.y),
            new Vector2(r.xMax - size, r.yMax - size),
            new Vector2(r.x, r.yMax - size),
        };
    }

    /// <summary>Trennlinie mit einem Kreuz in der Mitte - die Linie bricht dafuer auf.</summary>
    void Divider(string name, Rect area, out Image left, out Image right, out Image plus)
    {
        float half = Mathf.Max(0f, (area.width - 9f) * 0.5f);
        left  = Fill(name + "Left", screen, new Rect(area.x, area.y, half, area.height), panelBorder);
        right = Fill(name + "Right", screen,
                     new Rect(area.xMax - half, area.y, half, area.height), panelBorder);
        plus  = Plus(name + "Plus", screen,
                     new Rect(area.center.x - 2.5f, area.y - 2f, 5f, 5f), accentGold);
    }

    static Rect Inset(Rect r, float by) =>
        new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f);

    static Rect Grow(Rect r, float by) => Inset(r, -by);

    static Rect Offset(Rect r, float dx, float dy) =>
        new Rect(r.x + dx, r.y + dy, r.width, r.height);

    /// <summary>
    /// Zieht einen Kasten auf das naechste ganze Vielfache der Formgroesse
    /// zusammen und setzt ihn mittig zurueck. Nur so bleibt eine 15x15-Form
    /// pixelgenau; sonst wird jeder Texturpixel ein krummes Stueck breit und
    /// die Kante laeuft ungleich dick.
    /// </summary>
    static Rect SnapToShape(Rect r)
    {
        float s = SkillShapeSprites.Size;
        float k = Mathf.Max(1f, Mathf.Floor(Mathf.Min(r.width, r.height) / s));
        float side = s * k;

        return new Rect(Mathf.Round(r.center.x - side * 0.5f),
                        Mathf.Round(r.center.y - side * 0.5f), side, side);
    }

    /// <summary>Dieselbe Farbe mit anderer Deckkraft.</summary>
    static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

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

        AnimateDecor();

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
        RefreshBack();

        if (pointsText != null) pointsText.text = string.Format(pointsFormat, Skills.Currency);
    }

    void RefreshBack()
    {
        bool isHover = hover == Hit.Back;

        backFillImage.color = isHover ? Lift(backFill, hoverLift) : backFill;
        backSheen.color     = Lift(backFill, isHover ? 0.5f : 0.3f);
        backShade.color     = Shade(backFill, 0.35f);
        backArrow.color     = isHover ? Color.white : backInk;

        // Unter der Maus zieht der Pfeil einen Pixel nach links - der Knopf
        // zeigt damit selbst, wohin er fuehrt.
        if (backArrowRect != null)
        {
            backArrowRect.anchoredPosition =
                new Vector2(Mathf.Round(backArea.x + (isHover ? 4f : 6f)),
                            -Mathf.Round(backArea.y + (backArea.height - 7f) * 0.5f));
        }
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
            t.PointerEdge.gameObject.SetActive(isSelected);
            t.Pointer.color = c.Color;

            if (isSelected)
            {
                // Gewaehlt: der Knopf traegt seine Farbe, die Schrift wird hell -
                // oder dunkel, wenn die Farbe dafuer zu hell ist (Gelb).
                Color ink = InkOn(c.Color);

                t.Fill.color       = c.Color;
                t.Sheen.color      = Lift(c.Color, 0.45f);
                t.Shade.color      = Shade(c.Color, 0.3f);
                t.Outer.color      = border;
                t.Inner.color      = Lift(c.Color, 0.35f);
                t.Label.color      = ink;
                t.LabelShadow.color = ink.Equals(panelInk) ? Alpha(Lift(c.Color, 0.6f), 0.7f)
                                                           : Alpha(border, 0.8f);
                t.IconFill.color   = Lift(c.Color, 0.6f);
                t.IconEdge.color   = border;
            }
            else
            {
                t.Fill.color       = isHover ? Lift(panelInset, hoverLift * 0.5f) : panelInset;
                t.Sheen.color      = isHover ? Lift(panelFill, 0.5f) : panelFill;
                t.Shade.color      = Alpha(panelBorder, isHover ? 0.5f : 0.8f);
                t.Outer.color      = isHover ? c.Color : Shade(c.Color, borderShade * 0.5f);
                t.Inner.color      = Lift(c.Color, isHover ? 0.35f : 0.55f);
                t.Label.color      = border;
                t.LabelShadow.color = Alpha(panelFill, 0.9f);
                t.IconFill.color   = panelFill;
                t.IconEdge.color   = Lift(c.Color, 0.35f);
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
            headerText.text = headerTextShadow.text = "";
            return;
        }

        Color border = Shade(c.Color, borderShade);
        Color ink = InkOn(c.Color);

        headerFill.color  = c.Color;
        headerSheen.color = Lift(c.Color, 0.35f);
        headerShade.color = Shade(c.Color, 0.28f);
        headerLineTop.color = headerLineBottom.color = border;

        headerTipLeft.color = headerTipRight.color = Shade(c.Color, 0.15f);
        headerTipLeftEdge.color = headerTipRightEdge.color = border;

        headerText.color  = ink;
        headerTextShadow.color = ink.Equals(panelInk) ? Alpha(Lift(c.Color, 0.55f), 0.8f)
                                                      : Alpha(border, 0.85f);
        headerPlusLeft.color = headerPlusRight.color = Alpha(ink, 0.8f);

        headerText.text = headerTextShadow.text = string.IsNullOrWhiteSpace(c.Path)
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
            detailName.text = detailNameShadow.text = "";
            detailText.text = detailQuote.text = "";
            return;
        }

        Color border = Shade(c.Color, borderShade);
        Color glossOn = c.Color;
        Color footer = panelInkDim;

        // Ein Knoten unter der Maus gewinnt - dann steht die Maus ohnehin nicht
        // gleichzeitig auf einem Knopf links.
        SkillNodeDef node = graph.Hovered;

        if (node != null)
        {
            bool unlocked = Skills.IsUnlocked(node);
            bool open = !unlocked && Skills.RequirementsMet(node);

            orbShadow.sprite = orbShapes.Fill(node.Shape);
            orbRing.sprite   = orbShapes.Outline(node.Shape);
            orbFill.sprite   = orbShapes.Fill(node.Shape);
            orbGloss.sprite  = orbShapes.Gloss(node.Shape);

            orbRing.color = unlocked || open ? border : Shade(panelBorder, 0.35f);
            orbFill.color = unlocked ? c.Color : open ? panelFill : panelBorder;
            glossOn = orbFill.color;

            detailName.text  = node.Name;
            detailName.color = border;
            detailText.text  = node.Description;

            // Der Startknoten wird nicht gekauft - "GEKAUFT" waere dort Unsinn.
            detailQuote.text = node.IsStart ? ""
                             : unlocked     ? boughtLabel
                             : open         ? string.Format(priceFormat, node.Price)
                                            : MissingText(node);

            // Die Fusszeile sagt ihren Zustand auch ueber die Farbe: Gold fuer
            // einen Preis, Gruen fuer erledigt, Rot fuer verschlossen.
            footer = node.IsStart ? panelInkDim
                   : unlocked     ? ownedInk
                   : open         ? priceInk
                                  : lockedInk;
        }
        else
        {
            orbShadow.sprite = orbShapes.Fill(SkillShape.Kreis);
            orbRing.sprite   = orbShapes.Outline(SkillShape.Kreis);
            orbFill.sprite   = orbShapes.Fill(SkillShape.Kreis);
            orbGloss.sprite  = orbShapes.Gloss(SkillShape.Kreis);

            orbRing.color = border;
            orbFill.color = c.Color;
            glossOn = c.Color;

            detailName.text  = c.Name;
            detailName.color = border;
            detailText.text  = c.Description;
            detailQuote.text = c.Quote;
        }

        detailNameShadow.text = detailName.text;
        detailQuote.color = footer;

        // Das Glanzlicht ist nur da, wo die Kugel auch Farbe hat - auf einer
        // grauen, gesperrten Form waere ein Glanz reine Behauptung.
        bool lit = Luma(glossOn) > 0.28f;
        orbGloss.gameObject.SetActive(lit);
        orbGloss.color = Alpha(Color.white, Luma(glossOn) > 0.72f ? 0.28f : 0.42f);

        // Die Funken tragen die Kategoriefarbe - AnimateDecor macht daraus das
        // Blinken, hier steht nur, WELCHE Farbe blinkt.
        Color sparkColor = Lift(c.Color, 0.55f);
        foreach (Twinkle t in twinkles) t.Image.color = Alpha(sparkColor, t.Base);

        // Ohne Text unten braucht es auch die zweite Trennlinie nicht.
        bool hasFooter = !string.IsNullOrWhiteSpace(detailQuote.text);
        divider2a.gameObject.SetActive(hasFooter);
        divider2b.gameObject.SetActive(hasFooter);
        dividerPlus2.gameObject.SetActive(hasFooter);

        dividerPlus1.color = dividerPlus2.color = accentGold;
        divider1a.color = divider1b.color = panelBorder;
        divider2a.color = divider2b.color = panelBorder;
    }

    // ------------------------------------------------------------- Bewegung

    /// <summary>
    /// Das bisschen Leben im Fenster: das Licht hinter dem Papier atmet, die
    /// Funken an der Kugel blinken, und der Saum um den gewaehlten Knopf geht
    /// mit. Nichts davon bewegt einen Pixel - es aendert nur Deckkraft und
    /// Farbe, damit das Bild pixelgenau bleibt.
    ///
    /// Laeuft auf unscaledTime: das Fenster friert den Spieler ein, und wer
    /// spaeter die Zeit anhaelt, soll hier nicht alles stehen sehen.
    /// </summary>
    void AnimateDecor()
    {
        if (!animateDecor) return;

        float t = Time.unscaledTime;

        if (glowImage != null)
            glowImage.color = Alpha(glowColor, glowStrength * (0.82f + 0.18f * Mathf.Sin(t * 0.9f)));

        foreach (Twinkle tw in twinkles)
        {
            float wave = Mathf.Sin(t * 2.3f + tw.Phase);
            // Laenger dunkel als hell - ein Funke blitzt, er leuchtet nicht.
            float a = Mathf.InverseLerp(-0.1f, 1f, wave);
            tw.Image.color = Alpha(tw.Image.color, tw.Base * (0.15f + 0.85f * a));
        }

        SkillBranchDef c = Current;
        if (c != null && selected >= 0 && selected < tabs.Count)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
            tabs[selected].Glow.color = Color.Lerp(panelFill, Lift(c.Color, 0.75f), wave);
        }

        graph?.Animate(t);
    }

    static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

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
