using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// ---------------------------------------------------------------------------
///  DER SKILLTREE IM HUB - Stand: nur die Oberflaeche.
///
///  Was hier steht, ist der Rahmen: Titelschild, die vier Kategorien links, das
///  grosse Feld in der Mitte und die Beschreibungskarte rechts. Die Knoten zum
///  Skillen kommen spaeter in das Feld in der Mitte - es heisst in der Hierarchie
///  darum schon "Body" und ist absichtlich leer.
///
///  Eine Kategorie eintragen oder aendern:
///    1. Im Inspector unter "Kategorien" eine Zeile ergaenzen oder anpassen.
///    2. Farbe setzen - alles andere (Rahmen, Banner, Schriftfarbe) rechnet sich
///       daraus aus. Bei hellen Farben wie Gelb springt die Schrift von selbst
///       auf Dunkel um, siehe InkOn().
///    3. Symbol ist optional. Leer = eine Scheibe in der Kategoriefarbe.
///
///  Der Hintergrund um das Papier herum ist bewusst nur eine ruhige Flaeche -
///  die Pixelart dafuer kommt nach und wird dann in "Hintergrundbild"
///  eingetragen; sie legt sich ueber die vollen 320x180.
///
///  Bedienung: Klick, W/S bzw. Hoch/Runter, Mausrad, ESC. Hover und Klick
///  rechnet dieses Skript wie die Levelauswahl selbst aus der Mausposition aus
///  (UpdateHover/ClickAt) - wer einen Knopf ergaenzt, muss ihn in BEIDEN
///  Methoden eintragen, sonst reagiert er nicht.
/// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public class HubSkilltreeUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Ein Pfad im Skilltree.</summary>
    [System.Serializable]
    public class Category
    {
        [Tooltip("Steht auf dem Knopf links und ueber der Beschreibung rechts.")]
        public string displayName = "";

        [Tooltip("Ueberschrift im grossen Feld. Leer = Name plus 'PFAD'.")]
        public string pathLabel = "";

        [Tooltip("Text in der Beschreibungskarte rechts.")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Der Spruch unter der Beschreibung. Leer = die Zeile faellt weg.")]
        [TextArea(1, 3)]
        public string quote = "";

        [Tooltip("Faerbt Knopf, Banner und Kugel. Alles andere leitet sich daraus ab.")]
        public Color color = Color.white;

        [Tooltip("Optional. Leer = eine Scheibe in der Kategoriefarbe.")]
        public Sprite icon;
    }

    [Header("Kategorien")]
    [Tooltip("Reihenfolge von oben nach unten.")]
    [SerializeField]
    private List<Category> categories = new List<Category>
    {
        new Category
        {
            displayName = "ENTWICKLUNG",
            pathLabel   = "ENTWICKLUNGSPFAD",
            description = "Wachse ueber dich hinaus: mehr Erfahrung, mehr Leben, mehr Moeglichkeiten.",
            quote       = "\"Jeder Schritt zaehlt.\"",
            color       = new Color32(0x3C, 0x6F, 0xC0, 0xFF),
        },
        new Category
        {
            displayName = "KAMPF",
            pathLabel   = "KAMPFPFAD",
            description = "Schaerfe deine Waffen und triff haerter, wo es weh tut.",
            quote       = "\"Angriff ist die beste Verteidigung.\"",
            color       = new Color32(0xB2, 0x41, 0x41, 0xFF),
        },
        new Category
        {
            displayName = "GLÜCK",
            pathLabel   = "GLÜCKSPFAD",
            description = "Bessere Funde, seltenere Beute und der eine Wurf, der alles dreht.",
            quote       = "\"Glueck ist kein Zufall.\"",
            color       = new Color32(0x3E, 0x8F, 0x4F, 0xFF),
        },
        new Category
        {
            displayName = "SPIRIT",
            pathLabel   = "SPIRITPFAD",
            description = "Sammle Seelen schneller und hole aus jedem Lauf mehr heraus.",
            quote       = "\"Der Geist ueberdauert.\"",
            color       = new Color32(0xC8, 0x9A, 0x2C, 0xFF),
        },
    };

    [Header("Beschriftungen")]
    [SerializeField] private string titleLabel = "SKILLTREE";
    [SerializeField] private string backLabel  = "ZURÜCK";
    [Tooltip("Nur wenn eine Kategorie keinen eigenen Pfadnamen hat. {0} = Name.")]
    [SerializeField] private string pathLabelFormat = "{0}PFAD";

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
    [SerializeField] private Rect titleArea = new Rect(96f, 2f, 128f, 19f);
    [Tooltip("Der oberste Kategorieknopf. Die anderen ruecken um Hoehe plus Abstand nach.")]
    [SerializeField] private Rect categoryArea = new Rect(14f, 32f, 80f, 24f);
    [SerializeField] private float categoryGap = 6f;
    [Tooltip("Das grosse Feld in der Mitte - hier kommen spaeter die Knoten hinein.")]
    [SerializeField] private Rect contentArea = new Rect(102f, 28f, 130f, 118f);
    [Tooltip("Das farbige Banner oben im grossen Feld.")]
    [SerializeField] private Rect contentHeaderArea = new Rect(108f, 33f, 118f, 15f);
    [Tooltip("Die freie Flaeche darunter. Bleibt leer, bis es Inhalte gibt.")]
    [SerializeField] private Rect contentBodyArea = new Rect(106f, 54f, 122f, 88f);
    [SerializeField] private Rect detailArea = new Rect(240f, 28f, 64f, 118f);
    [SerializeField] private Rect backArea = new Rect(8f, 157f, 62f, 15f);

    [Header("Beschreibungskarte (Pixel im 320x180-Bild)")]
    [SerializeField] private Rect detailOrbArea   = new Rect(256f, 34f, 32f, 32f);
    [SerializeField] private Rect detailNameArea  = new Rect(242f, 70f, 60f, 12f);
    [SerializeField] private Rect detailDivider1  = new Rect(248f, 86f, 48f, 1f);
    [SerializeField] private Rect detailTextArea  = new Rect(244f, 92f, 56f, 28f);
    [SerializeField] private Rect detailDivider2  = new Rect(248f, 124f, 48f, 1f);
    [SerializeField] private Rect detailQuoteArea = new Rect(244f, 129f, 56f, 15f);

    [Header("Schriftgroessen")]
    [SerializeField] private float titleFontSize      = 12f;
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
    enum Hit { None, Category, Back }

    readonly List<Tab> tabs = new List<Tab>();
    readonly HubPixelSprites pixels = new HubPixelSprites();

    GameObject root;
    RectTransform screen;
    Image headerFill, headerFrame, headerPlusLeft, headerPlusRight;
    Image orbRing, orbFill;
    Image divider2a, divider2b, dividerPlus2;
    Image backFillImage;
    TextMeshProUGUI headerText, detailName, detailText, detailQuote;

    Hit hover = Hit.None;
    int hoverTab = -1;

    int selected;
    int openedOnFrame = -1;
    bool built;

    Category Current => (selected >= 0 && selected < categories.Count) ? categories[selected] : null;

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
        BuildTabs();
        BuildContent();
        BuildDetail();
        BuildBackButton();

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

    void BuildTabs()
    {
        tabs.Clear();

        for (int i = 0; i < categories.Count; i++)
        {
            Category c = categories[i];
            var tab = new Tab();
            Rect area = TabArea(i);

            tab.Go = HubUiKit.NewRect("Kategorie " + (i + 1), screen);
            HubUiKit.Place((RectTransform)tab.Go.transform, area);

            // Alles darin rechnet ab der linken oberen Ecke des Knopfes.
            var local = new Rect(0f, 0f, area.width, area.height);

            tab.Glow  = Frame("Glow", tab.Go.transform, Grow(local, 1f), panelFill);
            tab.Fill  = Fill("Fill", tab.Go.transform, local, panelInset);
            tab.Outer = Frame("Outer", tab.Go.transform, local, c.color);
            tab.Inner = Frame("Inner", tab.Go.transform, Inset(local, 2f), c.color);

            var iconBox = new Rect(4f, (area.height - 14f) * 0.5f, 14f, 14f);
            tab.IconFill = Fill("IconBox", tab.Go.transform, iconBox, panelFill);
            tab.Icon = HubUiKit.NewImage("Icon", tab.Go.transform,
                                         c.icon != null ? c.icon : pixels.Disc, c.color);
            HubUiKit.Place((RectTransform)tab.Icon.transform, Inset(iconBox, 1f));

            tab.Label = HubUiKit.NewText("Label", tab.Go.transform, font, categoryFontSize,
                                         panelInk, TextAlignmentOptions.Left);
            HubUiKit.Place((RectTransform)tab.Label.transform,
                           new Rect(22f, 0f, area.width - 26f, area.height));
            tab.Label.text = c.displayName;

            // Der Zeiger steht rechts heraus und zeigt auf das grosse Feld.
            tab.Pointer = HubUiKit.NewImage("Pointer", tab.Go.transform, pixels.ArrowRight, c.color);
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

        // Absichtlich leer: hier kommen die Knoten zum Skillen hinein. Das Objekt
        // steht schon da, damit spaeter klar ist, wo sie hingehoeren.
        GameObject body = HubUiKit.NewRect("Body", screen);
        HubUiKit.Place((RectTransform)body.transform, contentBodyArea);
    }

    void BuildDetail()
    {
        Fill("Detail", screen, detailArea, panelInset);
        Frame("DetailFrame", screen, detailArea, panelBorder);

        // Zwei Scheiben uebereinander: die untere schaut als Rand heraus.
        orbRing = HubUiKit.NewImage("OrbRing", screen, pixels.Disc, Color.white);
        HubUiKit.Place((RectTransform)orbRing.transform, detailOrbArea);
        orbFill = HubUiKit.NewImage("Orb", screen, pixels.Disc, Color.white);
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

    // -------------------------------------------------------------- Laufzeit

    void Update()
    {
        if (!IsOpen) return;

        // Das [E], mit dem das Fenster aufgeht, darf nicht gleich durchklicken
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0)) ClickAt(hover, hoverTab);

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Move(wheel > 0f ? -1 : +1);
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

        if (MousePixel(out Vector2 m))
        {
            if (backArea.Contains(m)) hover = Hit.Back;
            else
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    if (!TabArea(i).Contains(m)) continue;
                    hover = Hit.Category;
                    hoverTab = i;
                    break;
                }
            }
        }

        if (hover != was || hoverTab != wasTab) Refresh();
    }

    void ClickAt(Hit what, int tab)
    {
        switch (what)
        {
            case Hit.Category:
                if (tab != selected) Select(tab);
                break;
            case Hit.Back:
                Close();
                break;
        }
    }

    void Move(int delta)
    {
        if (categories.Count == 0) return;

        int next = Mathf.Clamp(selected + delta, 0, categories.Count - 1);
        if (next == selected) return;

        Select(next);
    }

    void Select(int index)
    {
        selected = index;
        PlaySfx(moveClip);
        Refresh();
    }

    // -------------------------------------------------------------- Anzeige

    void Refresh()
    {
        RefreshTabs();
        RefreshContent();
        RefreshDetail();

        backFillImage.color = hover == Hit.Back ? Lift(backFill, hoverLift) : backFill;
    }

    void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count && i < categories.Count; i++)
        {
            Tab t = tabs[i];
            Category c = categories[i];

            bool isSelected = i == selected;
            bool isHover = hover == Hit.Category && hoverTab == i;

            Color border = Shade(c.color, borderShade);

            t.Glow.gameObject.SetActive(isSelected);
            t.Pointer.gameObject.SetActive(isSelected);
            t.Pointer.color = c.color;

            if (isSelected)
            {
                // Gewaehlt: der Knopf traegt seine Farbe, die Schrift wird hell -
                // oder dunkel, wenn die Farbe dafuer zu hell ist (Gelb).
                t.Fill.color     = c.color;
                t.Outer.color    = border;
                t.Inner.color    = Lift(c.color, 0.35f);
                t.Label.color    = InkOn(c.color);
                t.IconFill.color = Lift(c.color, 0.55f);
            }
            else
            {
                t.Fill.color     = isHover ? Lift(panelInset, hoverLift * 0.5f) : panelInset;
                t.Outer.color    = isHover ? c.color : Shade(c.color, borderShade * 0.5f);
                t.Inner.color    = Lift(c.color, 0.5f);
                t.Label.color    = border;
                t.IconFill.color = panelFill;
            }

            // Die Ersatzscheibe bleibt in der Kategoriefarbe - auf dem gewaehlten
            // Knopf in der dunklen, sonst verschwindet sie im hellen Kaestchen.
            t.Icon.color = c.icon != null ? Color.white
                                          : (isSelected ? border : c.color);
        }
    }

    void RefreshContent()
    {
        Category c = Current;
        if (c == null)
        {
            headerText.text = "";
            return;
        }

        Color border = Shade(c.color, borderShade);
        Color ink = InkOn(c.color);

        headerFill.color  = c.color;
        headerFrame.color = border;
        headerText.color  = ink;
        headerPlusLeft.color = headerPlusRight.color = ink;

        headerText.text = string.IsNullOrWhiteSpace(c.pathLabel)
            ? string.Format(pathLabelFormat, c.displayName)
            : c.pathLabel;
    }

    void RefreshDetail()
    {
        Category c = Current;
        if (c == null)
        {
            detailName.text = detailText.text = detailQuote.text = "";
            return;
        }

        orbRing.color = Shade(c.color, borderShade);
        orbFill.color = c.color;

        detailName.text  = c.displayName;
        detailName.color = Shade(c.color, borderShade);
        detailText.text  = c.description;

        // Ohne Spruch braucht es auch die zweite Trennlinie nicht.
        bool hasQuote = !string.IsNullOrWhiteSpace(c.quote);
        detailQuote.text = hasQuote ? c.quote : "";
        divider2a.gameObject.SetActive(hasQuote);
        divider2b.gameObject.SetActive(hasQuote);
        dividerPlus2.gameObject.SetActive(hasQuote);
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
