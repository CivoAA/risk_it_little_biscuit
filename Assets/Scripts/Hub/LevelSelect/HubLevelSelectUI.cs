using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Die Levelauswahl im Hub, formatfuellend auf 320x180.
///
/// Links die Levelkarten - jede ist der gepixelte Rahmen frameLvL mit der Nummer
/// im Kopf und dem freien Fenster fuer das spaetere Vorschaubild -, darunter die
/// Fortschrittslinie; rechts das Beschreibungsfeld mit dem Spielen-Knopf. Passen
/// nicht alle Karten nebeneinander, wird geblaettert: mit A/D, den Pfeiltasten,
/// dem Mausrad oder den beiden Pfeilen an der Linie.
///
/// Alle Kaesten stehen als <see cref="Rect"/> im Inspector, gemessen in Pixeln
/// der 320x180-Vorlage mit Nullpunkt links oben - wie beim Shop. Was der Rahmen
/// nicht mitbringt (Auswahlzeiger, Schloss, Haken), entsteht als Pixel-Textur in
/// <see cref="HubPixelSprites"/>.
///
/// Maus: Die Treffer rechnet dieses Skript selbst aus der Mausposition aus,
/// statt sich auf EventSystem-Raycasts zu verlassen. Das ist ein Codeweg fuer
/// Hover und Klick, er haengt an keinem Input-Modul und laesst sich hier
/// nachvollziehen - dieselbe Rechnung wie fuer das Zeichnen.
///
/// ---------------------------------------------------------------------------
///  EIN LEVEL ANBINDEN - Kurzfassung
/// ---------------------------------------------------------------------------
///  1. In der hub-Szene das Objekt "LevelSelectUI" waehlen. Unter "Inhalt"
///     steht die Liste "Levels" - ein Eintrag ist eine Karte, die Reihenfolge
///     ist die Reihenfolge auf dem Bildschirm, die Nummer auf der Karte ist die
///     Position in der Liste (Eintrag 1 = Karte "1").
///
///  2. Am Eintrag ausfuellen:
///       Scene To Load  Name der Szene, die das Level enthaelt, z.B. "Game".
///                      Die Szene MUSS in File > Build Settings stehen, sonst
///                      bleibt der Knopf grau und zeigt "BALD" (siehe IsLinked).
///       Map Id         Welche Welt in dieser Szene hochkommt. Das ist derselbe
///                      Index wie MapName.mapID in der World Map; WorldSelector
///                      in Game.unity schaltet danach seine Welten frei
///                      (worlds[mapId]). Neue Welt = neuer Eintrag dort.
///       Display Name / Description   Text im rechten Feld.
///       Preview        Bild fuer Kartenfenster und Beschreibungsfeld. Leer =
///                      das Fenster bleibt frei.
///
///  3. Sperren ist optional: Story Unlock Id / Endless Unlock Id leer lassen =
///     immer offen. Steht dort eine Unlock-ID, muss der Katalog in Unlocks.cs
///     sie kennen und sie freigeschaltet sein - sonst zeigt die Karte ein
///     Schloss.
///
///  4. Das war's. Play() unten macht daraus denselben Ablauf wie die World Map:
///     Fortschritt sichern, Skilltree setzen, MapsManager.selectedMap setzen,
///     Shop-Stand einfrieren, Szene laden. Wer dort etwas ergaenzen will,
///     findet jeden Schritt einzeln kommentiert.
/// ---------------------------------------------------------------------------
///
/// Gestartet wird ein Level ueber dieselben Aufrufe wie in der World Map
/// (PlayerWorldInteraction, Fall "Map"). Unlocks, Achievements, Skills und Shop
/// sind statische Kataloge und immer da; MapsManager und MenuManager sind
/// Szenen-Manager aus der World Map und fehlen, wenn die hub-Szene allein
/// laeuft - darum sind die beiden geprueft und es faellt notfalls auf ein
/// schlichtes LoadScene zurueck.
/// </summary>
[DisallowMultipleComponent]
public class HubLevelSelectUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Ein Level in der Auswahl.</summary>
    [System.Serializable]
    public class LevelEntry
    {
        [Tooltip("Name im Beschreibungsfeld.")]
        public string displayName = "";

        [Tooltip("Text im rechten Feld.")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Bild im Fenster der Karte und im Beschreibungsfeld. Leer = das " +
                 "Fenster bleibt frei.")]
        public Sprite preview;

        [Tooltip("Welche Welt geladen wird - derselbe Index wie MapName.mapID in der World Map.")]
        public int mapId;

        [Tooltip("Szene, die das Level enthaelt. Muss in den Build Settings stehen. " +
                 "Leer oder unbekannt = das Level zeigt sich als 'noch nicht da'.")]
        public string sceneToLoad = "Game";

        [Tooltip("Optional: Story ist erst offen, wenn diese Unlock-ID freigeschaltet ist. Leer = immer offen.")]
        public string storyUnlockId = "";

        [Tooltip("Hat dieses Level ueberhaupt einen Endless-Modus?")]
        public bool endlessAvailable = true;

        [Tooltip("Optional: Endless ist erst offen, wenn diese Unlock-ID freigeschaltet ist. Leer = immer offen.")]
        public string endlessUnlockId = "";
    }

    [Header("Inhalt")]
    [Tooltip("Reihenfolge der Karten von links nach rechts. Die Nummer auf der Karte " +
             "ist die Position in dieser Liste.")]
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

    [Header("Beschriftungen")]
    [SerializeField] private string titleLabel   = "LEVELAUSWAHL";
    [SerializeField] private string endlessLabel = "ENDLESS MODE";
    [SerializeField] private string playLabel    = "SPIELEN";
    [SerializeField] private string backLabel    = "ZURÜCK";
    [Tooltip("Auf dem Knopf, solange das Level noch keine Szene hat.")]
    [SerializeField] private string comingSoonLabel = "BALD";
    [Tooltip("Steht im Beschreibungsfeld, wenn das Level noch zu ist.")]
    [SerializeField] private string lockedDescription = "Noch verschlossen.";
    [Tooltip("{0} = Nummer, {1} = Name.")]
    [SerializeField] private string detailTitleFormat = "{0} - {1}";

    [Header("Grafik")]
    [Tooltip("Der gepixelte Kartenrahmen (frameLvL). Leer = schlichte Flaeche mit 1px-Rahmen.")]
    [SerializeField] private Sprite cardFrameSprite;
    [Tooltip("Die Umrandung der gewaehlten Karte (highlight_level_selec). Sie ist " +
             "groesser als die Karte und legt sich aussen herum - den Zeiger oben " +
             "bringt sie schon mit. Leer = heller 1px-Rahmen plus eigener Zeiger.")]
    [SerializeField] private Sprite selectionSprite;
    [Tooltip("Optionaler gepixelter Rahmen ueber die vollen 320x180. Leer = schlichte Flaeche.")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("Pixelschrift des Panels. Hier steht Jersey10, weil ThaleahFat/PixelArtFont " +
             "keine Umlaute kennt (106 Zeichen, kein äöü) - ZURÜCK bliebe darin lückenhaft. " +
             "Leer = die zuerst gefundene Pixelschrift, sonst TMP-Standard.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Kaesten (Pixel im 320x180-Bild, Nullpunkt links oben)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [SerializeField] private Rect bannerArea = new Rect(8f, 8f, 152f, 18f);
    [Tooltip("Die Flaeche, in der die Karten liegen. Was nicht hineinpasst, wird geblaettert.")]
    [SerializeField] private Rect cardsArea  = new Rect(8f, 38f, 204f, 79f);
    [Tooltip("Groesse einer Karte - beim frameLvL-Rahmen seine echten 64x79 Pixel.")]
    [SerializeField] private Vector2 cardSize = new Vector2(64f, 79f);
    [Tooltip("Luft zwischen zwei Karten.")]
    [SerializeField] private float cardGap   = 6f;
    [Tooltip("Hoehe der Fortschrittslinie unter den Karten.")]
    [SerializeField] private float trackY    = 128f;
    [SerializeField] private float trackNodeSize = 5f;
    [Tooltip("Blaetterpfeile links und rechts der Linie. Nur sichtbar, wenn es " +
             "mehr Karten gibt als Platz.")]
    [SerializeField] private Rect scrollLeftArea  = new Rect(24f, 124f, 5f, 9f);
    [SerializeField] private Rect scrollRightArea = new Rect(191f, 124f, 5f, 9f);
    [SerializeField] private Rect backArea   = new Rect(8f, 151f, 62f, 15f);
    [SerializeField] private Rect detailArea = new Rect(216f, 20f, 96f, 148f);

    [Header("Karte (Pixel, relativ zur Karte)")]
    [Tooltip("Wo das Rahmenbild in der Karte sitzt. 0,0 mit voller Kartengroesse " +
             "passt zum zugeschnittenen frameLvL.")]
    [SerializeField] private Rect cardFrameArea   = new Rect(0f, 0f, 64f, 79f);
    [Tooltip("Der Kopf des Rahmens, in dem die Levelnummer steht.")]
    [SerializeField] private Rect cardNumberArea  = new Rect(2f, 3f, 60f, 15f);
    [Tooltip("Das freie Fenster im Rahmen - hier kommt spaeter das Levelbild hin.")]
    [SerializeField] private Rect cardPreviewArea = new Rect(6f, 20f, 52f, 50f);
    [Tooltip("Wo die Auswahl-Umrandung sitzt, relativ zur Karte. Die Umrandung ist " +
             "69x85 gross, die Karte 64x79 - sie ragt also rundherum heraus, " +
             "darum die negativen Werte.")]
    [SerializeField] private Rect selectionArea = new Rect(-3f, -3f, 69f, 85f);
    [Tooltip("Nur ohne Umrandungsbild: Abstand des Ersatzzeigers zur Oberkante der Karte.")]
    [SerializeField] private float markerGap = 1f;

    [Header("Beschreibungsfeld (Pixel im 320x180-Bild)")]
    [SerializeField] private Rect detailTitleArea   = new Rect(222f, 27f, 84f, 12f);
    [SerializeField] private Rect detailPreviewArea = new Rect(222f, 43f, 84f, 48f);
    [SerializeField] private Rect detailTextArea    = new Rect(222f, 96f, 84f, 34f);
    [SerializeField] private Rect dividerArea       = new Rect(222f, 133f, 84f, 1f);
    [SerializeField] private Rect endlessBoxArea    = new Rect(222f, 138f, 9f, 9f);
    [SerializeField] private Rect endlessLabelArea  = new Rect(235f, 138f, 71f, 9f);
    [SerializeField] private Rect playArea          = new Rect(222f, 151f, 84f, 15f);

    [Header("Endless")]
    [Tooltip("Zeigt den Haken unter der Beschreibung. Aus: die Auswahl startet " +
             "immer die Story, die Logik bleibt aber im Skript.")]
    [SerializeField] private bool showEndlessToggle = true;

    [Header("Schriftgroessen")]
    [SerializeField] private float titleFontSize       = 11f;
    [SerializeField] private float cardNumberFontSize  = 12f;
    [SerializeField] private float detailTitleFontSize = 9f;
    [SerializeField] private float detailFontSize      = 7f;
    [SerializeField] private float buttonFontSize      = 9f;
    [SerializeField] private float detailLineSpacing   = 4f;

    [Header("Farben")]
    [Tooltip("Der Rand neben den 320x180, wenn der Bildschirm nicht 16:9 ist.")]
    [SerializeField] private Color backdropColor   = new Color32(0x21, 0x1A, 0x1C, 0xFF);
    [Tooltip("Das leere Fenster in der Karte, solange kein Levelbild da ist.")]
    [SerializeField] private Color previewEmpty    = new Color32(0x1E, 0x18, 0x22, 0xFF);
    [Tooltip("Der Kartenrahmen, wenn die Karte weder gewaehlt noch unter der Maus ist.")]
    [SerializeField] private Color cardTint         = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color cardTintHover    = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private Color cardTintSelected = Color.white;
    [SerializeField] private Color cardTintLocked   = new Color(0.42f, 0.40f, 0.45f, 1f);
    [Tooltip("Nur fuer die Rueckfallebene ohne Rahmenbild.")]
    [SerializeField] private Color cardFill        = new Color32(0x3A, 0x2C, 0x2C, 0xFF);
    [SerializeField] private Color cardBorder      = new Color32(0x6B, 0x51, 0x47, 0xFF);
    [SerializeField] private Color selectionBorder = new Color32(0xF0, 0xE6, 0xCC, 0xFF);
    [Tooltip("Liegt ueber dem Fenster eines gesperrten Levels.")]
    [SerializeField] private Color lockedVeil      = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color panelFill       = new Color32(0xE8, 0xDE, 0xC2, 0xFF);
    [SerializeField] private Color panelBorder     = new Color32(0xC0, 0xAE, 0x8A, 0xFF);
    [SerializeField] private Color panelInk        = new Color32(0x33, 0x26, 0x2B, 0xFF);
    [SerializeField] private Color panelInkDim     = new Color32(0x6B, 0x51, 0x47, 0xFF);
    [SerializeField] private Color textOnDark      = new Color32(0xE8, 0xDE, 0xC2, 0xFF);
    [SerializeField] private Color lockedText      = new Color32(0x8C, 0x80, 0x78, 0xFF);
    [Tooltip("Der Spielen-Knopf, wenn das Level wirklich startbar ist.")]
    [SerializeField] private Color playFillReady   = new Color32(0x86, 0xA5, 0x7A, 0xFF);
    [Tooltip("Der Spielen-Knopf, wenn das Level zu ist oder noch keine Szene hat.")]
    [SerializeField] private Color playFillLocked  = new Color32(0x9A, 0x90, 0x86, 0xFF);
    [Tooltip("Die Box des gesetzten Endless-Hakens. Den Knopf faerbt Endless bewusst nicht.")]
    [SerializeField] private Color endlessFill     = new Color32(0x86, 0xA5, 0x7A, 0xFF);
    [Tooltip("So viel heller wird ein Knopf unter der Maus. 0 = kein Hover.")]
    [SerializeField, Range(0f, 1f)] private float hoverLift = 0.3f;

    [Header("Steuerung")]
    [SerializeField] private KeyCode playKey    = KeyCode.Return;
    [SerializeField] private KeyCode playKeyAlt = KeyCode.E;
    [SerializeField] private KeyCode endlessKey = KeyCode.Tab;
    [SerializeField] private KeyCode closeKey   = KeyCode.Escape;

    [Header("Sound")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Beim Setzen und Loesen des Endless-Hakens. Leer = der Ton vom Umschalten.")]
    [SerializeField] private AudioClip toggleClip;
    [Tooltip("Leer = MenuClick aus dem AudioController.")]
    [SerializeField] private AudioClip startClip;
    [Tooltip("Wenn das Level zu ist oder noch keine Szene hat. Leer = PlayerHit aus dem AudioController.")]
    [SerializeField] private AudioClip denyClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ------------------------------------------------------------ Laufzeit

    class Card
    {
        public GameObject Go;
        public Image Frame;          // der gepixelte Rahmen (oder die Ersatzflaeche)
        public Image Fallback;       // nur ohne Rahmenbild: Flaeche hinter dem Rahmen
        public Image Selection;      // heller 1px-Rahmen um die gewaehlte Karte
        public TextMeshProUGUI Number;
        public Image PreviewFill, Preview, Veil, Lock;
        public Image Node;           // Punkt auf der Fortschrittslinie
    }

    /// <summary>Was gerade unter der Maus liegt.</summary>
    enum Hit { None, Card, Play, Back, Endless, ScrollLeft, ScrollRight }

    readonly List<Card> cards = new List<Card>();
    readonly HubPixelSprites pixels = new HubPixelSprites();

    GameObject root;
    RectTransform screen;
    Image markerImage, scrollLeftImage, scrollRightImage;
    TextMeshProUGUI detailTitle, detailText, endlessText, playText;
    Image detailPreviewFill, detailPreview, endlessBoxFill, endlessCheck, playFill;
    GameObject endlessGroup;

    Hit hover = Hit.None;
    int hoverCard = -1;

    int selected;
    int scrollTop;
    bool endlessChosen;
    int openedOnFrame = -1;
    bool built;

    LevelEntry Current => (selected >= 0 && selected < levels.Count) ? levels[selected] : null;

    /// <summary>So viele Karten liegen nebeneinander, bevor geblaettert wird.</summary>
    int VisibleCards => Mathf.Max(1, Mathf.FloorToInt((cardsArea.width + cardGap) / (cardSize.x + cardGap)));

    bool NeedsScrolling => levels.Count > VisibleCards;

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

        // Szenenwechsel mit offener Auswahl: die Sperre wieder abmelden, sonst
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

        var canvasGO = new GameObject("LevelSelectCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) canvasGO.layer = uiLayer;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ueber Textbox (100), Konsole (120) und Shop (130)
        canvas.sortingOrder = 135;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root = HubUiKit.NewRect("LevelSelect", canvasGO.transform);
        HubUiKit.Stretch((RectTransform)root.transform);

        // Deckt den Hub zu. Klicks faengt nicht diese Flaeche ab, sondern die
        // Trefferpruefung in Update - solange die Auswahl offen ist, kommt im
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

        BuildBanner();
        BuildCards();
        BuildTrack();
        BuildDetail();
        BuildBackButton();

        root.SetActive(false);
    }

    void BuildBanner()
    {
        Image fill = Fill("Banner", screen, bannerArea, panelFill);
        Frame("BannerFrame", screen, bannerArea, panelBorder);

        TextMeshProUGUI t = HubUiKit.NewText("Title", fill.transform, font, titleFontSize,
                                             panelInk, TextAlignmentOptions.Center);
        HubUiKit.Stretch((RectTransform)t.transform);
        t.text = titleLabel;
    }

    void BuildCards()
    {
        cards.Clear();

        for (int i = 0; i < levels.Count; i++)
        {
            var card = new Card();
            card.Go = HubUiKit.NewRect("Card " + (i + 1), screen);
            HubUiKit.Place((RectTransform)card.Go.transform, CardArea(i));

            // Das Fenster liegt unter dem Rahmen - der laesst es frei
            card.PreviewFill = HubUiKit.NewImage("Window", card.Go.transform, null, previewEmpty);
            HubUiKit.Place((RectTransform)card.PreviewFill.transform, cardPreviewArea);

            card.Preview = HubUiKit.NewImage("Preview", card.Go.transform, null, Color.white);
            HubUiKit.Place((RectTransform)card.Preview.transform, cardPreviewArea);
            card.Preview.gameObject.SetActive(false);

            if (cardFrameSprite != null)
            {
                card.Frame = HubUiKit.NewImage("Frame", card.Go.transform, cardFrameSprite, cardTint);
                card.Frame.preserveAspect = false;
                HubUiKit.Place((RectTransform)card.Frame.transform, cardFrameArea);
            }
            else
            {
                // Ohne Rahmenbild: Flaeche hinter das Fenster und ein 1px-Rahmen
                card.Fallback = HubUiKit.NewImage("Fill", card.Go.transform, null, cardFill);
                HubUiKit.Stretch((RectTransform)card.Fallback.transform);
                card.Fallback.transform.SetAsFirstSibling();
                card.Frame = Frame("Frame", card.Go.transform,
                                   new Rect(0f, 0f, cardSize.x, cardSize.y), cardBorder);
            }

            card.Number = HubUiKit.NewText("Number", card.Go.transform, font, cardNumberFontSize,
                                           textOnDark, TextAlignmentOptions.Center);
            HubUiKit.Place((RectTransform)card.Number.transform, cardNumberArea);
            card.Number.text = (i + 1).ToString();

            card.Veil = HubUiKit.NewImage("Veil", card.Go.transform, null, lockedVeil);
            HubUiKit.Place((RectTransform)card.Veil.transform, cardPreviewArea);

            card.Lock = HubUiKit.NewImage("Lock", card.Go.transform, pixels.Padlock, lockedText);
            card.Lock.preserveAspect = false;
            HubUiKit.Place((RectTransform)card.Lock.transform,
                           new Rect(cardPreviewArea.x + (cardPreviewArea.width - 7f) * 0.5f,
                                    cardPreviewArea.y + (cardPreviewArea.height - 10f) * 0.5f, 7f, 10f));

            // Liegt ueber allem und markiert die gewaehlte Karte. Mit Bild ist es
            // die gemalte Umrandung (sie bringt den Zeiger oben mit), ohne Bild
            // ein heller 1px-Rahmen - dann setzt BuildCards unten zusaetzlich
            // einen Zeiger.
            if (selectionSprite != null)
            {
                card.Selection = HubUiKit.NewImage("Selection", card.Go.transform,
                                                   selectionSprite, Color.white);
                card.Selection.preserveAspect = false;
                HubUiKit.Place((RectTransform)card.Selection.transform, selectionArea);
            }
            else
            {
                card.Selection = Frame("Selection", card.Go.transform,
                                       new Rect(0f, 0f, cardSize.x, cardSize.y), selectionBorder);
            }

            cards.Add(card);
        }

        // Der Zeiger ist nur die Rueckfallebene: die gemalte Umrandung hat schon einen.
        if (selectionSprite == null)
        {
            markerImage = HubUiKit.NewImage("Marker", screen, pixels.ArrowDown, selectionBorder);
            markerImage.preserveAspect = false;
        }
    }

    void BuildTrack()
    {
        if (levels.Count == 0) return;

        float x0 = TrackX(0);
        float x1 = TrackX(levels.Count - 1);
        Fill("Track", screen, new Rect(x0, trackY, Mathf.Max(1f, x1 - x0), 1f), panelInkDim);

        for (int i = 0; i < levels.Count; i++)
        {
            var node = new Rect(Mathf.Round(TrackX(i) - trackNodeSize * 0.5f),
                                trackY - (trackNodeSize - 1f) * 0.5f, trackNodeSize, trackNodeSize);
            Fill("NodeFill " + (i + 1), screen, node, backdropColor);
            cards[i].Node = Frame("Node " + (i + 1), screen, node, cardBorder);
        }

        scrollLeftImage  = HubUiKit.NewImage("ScrollLeft", screen, pixels.ArrowLeft, textOnDark);
        scrollLeftImage.preserveAspect = false;
        HubUiKit.Place((RectTransform)scrollLeftImage.transform, scrollLeftArea);

        scrollRightImage = HubUiKit.NewImage("ScrollRight", screen, pixels.ArrowRight, textOnDark);
        scrollRightImage.preserveAspect = false;
        HubUiKit.Place((RectTransform)scrollRightImage.transform, scrollRightArea);
    }

    /// <summary>
    /// Mitte des Punktes fuer Level i. Die Punkte verteilen sich gleichmaessig
    /// ueber die Kartenflaeche: passt alles nebeneinander, sitzt jeder genau
    /// unter seiner Karte; sind es mehr, wird die Linie zur Uebersicht.
    /// </summary>
    float TrackX(int index)
    {
        float half = cardSize.x * 0.5f;
        float left = cardsArea.x + half;
        float right = cardsArea.xMax - half;
        if (levels.Count <= 1) return Mathf.Round(left);
        return Mathf.Round(left + (right - left) * index / (levels.Count - 1));
    }

    void BuildDetail()
    {
        Fill("Detail", screen, detailArea, panelFill);
        Frame("DetailFrame", screen, detailArea, panelBorder);

        detailTitle = HubUiKit.NewText("DetailTitle", screen, font, detailTitleFontSize,
                                       panelInk, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)detailTitle.transform, detailTitleArea);

        detailPreviewFill = Fill("DetailWindow", screen, detailPreviewArea, previewEmpty);
        detailPreview = HubUiKit.NewImage("DetailPreview", screen, null, Color.white);
        HubUiKit.Place((RectTransform)detailPreview.transform, detailPreviewArea);
        detailPreview.gameObject.SetActive(false);
        Frame("DetailPreviewFrame", screen, detailPreviewArea, panelBorder);

        detailText = HubUiKit.NewText("DetailText", screen, font, detailFontSize,
                                      panelInkDim, TextAlignmentOptions.TopLeft);
        HubUiKit.Place((RectTransform)detailText.transform, detailTextArea);
        detailText.lineSpacing = detailLineSpacing;

        Fill("Divider", screen, dividerArea, panelBorder);

        // ---- Endless-Haken ------------------------------------------------
        // Bleibt vollstaendig gebaut, aber abgeschaltet: so ist er eine Zeile
        // Arbeit entfernt, sobald Endless wieder dazusoll.
        endlessGroup = HubUiKit.NewRect("Endless", screen);
        HubUiKit.Stretch((RectTransform)endlessGroup.transform);

        endlessBoxFill = Fill("EndlessBox", endlessGroup.transform, endlessBoxArea, panelFill);
        Frame("EndlessBoxFrame", endlessGroup.transform, endlessBoxArea, panelInk);

        endlessCheck = HubUiKit.NewImage("EndlessCheck", endlessGroup.transform, pixels.Check, panelInk);
        endlessCheck.preserveAspect = false;
        HubUiKit.Place((RectTransform)endlessCheck.transform,
                       new Rect(endlessBoxArea.x + 1f, endlessBoxArea.y + 1f, 7f, 7f));

        endlessText = HubUiKit.NewText("EndlessLabel", endlessGroup.transform, font, detailFontSize,
                                       panelInk, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)endlessText.transform, endlessLabelArea);
        endlessText.text = endlessLabel;

        endlessGroup.SetActive(showEndlessToggle);

        // ---- Spielen ------------------------------------------------------
        playFill = Fill("Play", screen, playArea, playFillReady);
        Frame("PlayFrame", screen, playArea, panelInk);

        playText = HubUiKit.NewText("PlayLabel", screen, font, buttonFontSize,
                                    panelInk, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)playText.transform, playArea);
        playText.text = playLabel;
    }

    Image backFill;
    TextMeshProUGUI backText;

    void BuildBackButton()
    {
        backFill = Fill("Back", screen, backArea, cardFill);
        Frame("BackFrame", screen, backArea, cardBorder);

        backText = HubUiKit.NewText("BackLabel", screen, font, detailFontSize,
                                    textOnDark, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)backText.transform, backArea);
        backText.text = "< " + backLabel;
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

    /// <summary>Wo Karte i gerade liegt - abhaengig davon, wie weit geblaettert ist.</summary>
    Rect CardArea(int index)
    {
        float x = cardsArea.x + (index - scrollTop) * (cardSize.x + cardGap);
        return new Rect(x, cardsArea.y, cardSize.x, cardSize.y);
    }

    bool IsCardVisible(int index) => index >= scrollTop && index < scrollTop + VisibleCards;

    // --------------------------------------------------------------- Oeffnen

    public void Open()
    {
        if (IsOpen) return;

        Build();

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        root.SetActive(true);

        // Auf dem ersten offenen Level starten, damit man nie vor einer
        // gesperrten Karte steht und sich fragt, warum nichts geht.
        selected = FirstUnlocked();

        // Der Haken steht vorn, wenn im Hauptmenue "Endless" gedrueckt wurde oder
        // wenn die Story dieses Levels noch zu ist und nur Endless offen steht.
        endlessChosen = showEndlessToggle && EndlessUnlocked(Current)
                        && (GameSession.IsEndless || !StoryUnlocked(Current));
        ScrollToSelected();
        hover = Hit.None;
        hoverCard = -1;
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

    int FirstUnlocked()
    {
        for (int i = 0; i < levels.Count; i++)
            if (StoryUnlocked(levels[i]) || EndlessUnlocked(levels[i])) return i;
        return 0;
    }

    void ScrollToSelected()
    {
        int maxTop = Mathf.Max(0, levels.Count - VisibleCards);
        scrollTop = Mathf.Clamp(scrollTop, selected - VisibleCards + 1, selected);
        scrollTop = Mathf.Clamp(scrollTop, 0, maxTop);
    }

    // -------------------------------------------------------------- Laufzeit

    void Update()
    {
        if (!IsOpen) return;

        // Das [E], mit dem die Auswahl aufgeht, darf nicht gleich ein Level starten
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0)) ClickAt(hover, hoverCard);

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(+1);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Move(wheel > 0f ? -1 : +1);

        if (showEndlessToggle && Input.GetKeyDown(endlessKey)) ToggleEndless();
        if (Input.GetKeyDown(playKey) || Input.GetKeyDown(playKeyAlt)) Play();
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
        int wasCard = hoverCard;

        hover = Hit.None;
        hoverCard = -1;

        if (MousePixel(out Vector2 m))
        {
            if (playArea.Contains(m)) hover = Hit.Play;
            else if (backArea.Contains(m)) hover = Hit.Back;
            else if (showEndlessToggle && (endlessBoxArea.Contains(m) || endlessLabelArea.Contains(m)))
                hover = Hit.Endless;
            else if (NeedsScrolling && scrollLeftArea.Contains(m)) hover = Hit.ScrollLeft;
            else if (NeedsScrolling && scrollRightArea.Contains(m)) hover = Hit.ScrollRight;
            else
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    if (!IsCardVisible(i) || !CardArea(i).Contains(m)) continue;
                    hover = Hit.Card;
                    hoverCard = i;
                    break;
                }
            }
        }

        if (hover != was || hoverCard != wasCard) Refresh();
    }

    void ClickAt(Hit what, int card)
    {
        switch (what)
        {
            case Hit.Card:
                // Karten waehlen nur aus. Gestartet wird ausschliesslich ueber
                // den Spielen-Knopf (oder dessen Tastenkuerzel), damit niemand
                // aus Versehen in ein Level rutscht.
                if (card != selected) Select(card);
                break;
            case Hit.Play:        Play(); break;
            case Hit.Back:        Close(); break;
            case Hit.Endless:     ToggleEndless(); break;
            case Hit.ScrollLeft:  Move(-1); break;
            case Hit.ScrollRight: Move(+1); break;
        }
    }

    void Move(int delta)
    {
        if (levels.Count == 0) return;

        int next = Mathf.Clamp(selected + delta, 0, levels.Count - 1);
        if (next == selected) return;

        Select(next);
    }

    void Select(int index)
    {
        selected = index;
        ScrollToSelected();

        // Ein Level, das nur noch Endless hat, setzt den Haken selbst - und
        // eines ohne Endless nimmt ihn wieder weg.
        if (showEndlessToggle && !StoryUnlocked(Current) && EndlessUnlocked(Current)) endlessChosen = true;
        if (!showEndlessToggle || !EndlessUnlocked(Current)) endlessChosen = false;

        PlaySfx(moveClip);
        Refresh();
    }

    void ToggleEndless()
    {
        LevelEntry e = Current;
        if (e == null || !showEndlessToggle) return;

        if (!EndlessUnlocked(e)) { PlayDenySound(); return; }

        // Ohne offene Story bleibt Endless die einzige Wahl - der Haken laesst
        // sich dann nicht ausschalten, sonst zeigt der Knopf ins Leere.
        if (endlessChosen && !StoryUnlocked(e)) { PlayDenySound(); return; }

        endlessChosen = !endlessChosen;
        PlaySfx(toggleClip != null ? toggleClip : moveClip);
        RefreshDetail();
    }

    // -------------------------------------------------------------- Anzeige

    void Refresh()
    {
        RefreshCards();
        RefreshDetail();
    }

    void RefreshCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            LevelEntry e = levels[i];

            bool visible = IsCardVisible(i);
            if (c.Go.activeSelf != visible) c.Go.SetActive(visible);
            if (!visible) continue;

            HubUiKit.Place((RectTransform)c.Go.transform, CardArea(i));

            bool open = StoryUnlocked(e) || EndlessUnlocked(e);
            bool isSelected = i == selected;
            bool isHovered = hover == Hit.Card && hoverCard == i;

            Color tint = !open ? cardTintLocked
                       : isSelected ? cardTintSelected
                       : isHovered ? cardTintHover
                       : cardTint;

            if (cardFrameSprite != null) c.Frame.color = tint;
            else
            {
                c.Frame.color = isSelected ? selectionBorder : cardBorder;
                if (c.Fallback != null)
                    c.Fallback.color = isHovered ? Lift(cardFill, hoverLift * 0.5f) : cardFill;
            }

            c.Selection.gameObject.SetActive(isSelected);
            c.Number.color = !open ? lockedText : (isSelected ? selectionBorder : textOnDark);

            ApplyPreview(e, open, c.PreviewFill, c.Preview);
            c.Veil.gameObject.SetActive(!open);
            c.Lock.gameObject.SetActive(!open);
        }

        // Punkte gelten auch fuer Karten, die gerade weggeblaettert sind
        for (int i = 0; i < cards.Count; i++)
            if (cards[i].Node != null)
                cards[i].Node.color = i == selected ? selectionBorder : cardBorder;

        if (markerImage != null)
        {
            bool show = cards.Count > 0 && IsCardVisible(selected);
            markerImage.gameObject.SetActive(show);
            if (show)
            {
                Rect card = CardArea(selected);
                HubUiKit.Place((RectTransform)markerImage.transform,
                               new Rect(card.x + (card.width - 9f) * 0.5f,
                                        card.y - 5f - markerGap, 9f, 5f));
            }
        }

        if (scrollLeftImage != null)
        {
            bool canLeft = NeedsScrolling && scrollTop > 0;
            scrollLeftImage.gameObject.SetActive(canLeft);
            if (canLeft)
                scrollLeftImage.color = hover == Hit.ScrollLeft ? selectionBorder : textOnDark;
        }
        if (scrollRightImage != null)
        {
            bool canRight = NeedsScrolling && scrollTop + VisibleCards < levels.Count;
            scrollRightImage.gameObject.SetActive(canRight);
            if (canRight)
                scrollRightImage.color = hover == Hit.ScrollRight ? selectionBorder : textOnDark;
        }

        // ZURUECK haengt an keiner Karte, wird aber im selben Durchgang gesetzt
        if (backFill != null)
        {
            bool on = hover == Hit.Back;
            backFill.color = on ? Lift(cardFill, hoverLift * 0.6f) : cardFill;
            backText.color = on ? selectionBorder : textOnDark;
        }
    }

    void RefreshDetail()
    {
        LevelEntry e = Current;

        if (e == null)
        {
            detailTitle.text = "";
            detailText.text = "";
            return;
        }

        bool storyOk   = StoryUnlocked(e);
        bool endlessOk = EndlessUnlocked(e);
        bool open = storyOk || endlessOk;
        bool linked = IsLinked(e);

        string name = open ? (e.displayName ?? "") : "???";
        detailTitle.text = string.Format(detailTitleFormat, selected + 1, name.ToUpperInvariant());
        detailTitle.color = open ? panelInk : panelInkDim;

        detailText.text = open ? e.description : lockedDescription;
        ApplyPreview(e, open, detailPreviewFill, detailPreview);

        // ---- Haken --------------------------------------------------------
        if (showEndlessToggle)
        {
            endlessBoxFill.color = (endlessChosen && endlessOk) ? endlessFill : panelFill;
            if (endlessOk && hover == Hit.Endless)
                endlessBoxFill.color = Lift(endlessBoxFill.color, hoverLift * 0.5f);

            endlessCheck.gameObject.SetActive(endlessChosen && endlessOk);
            endlessText.color = endlessOk ? panelInk : panelInkDim;
        }

        // ---- Spielen ------------------------------------------------------
        // Gruen heisst: das Level laesst sich jetzt starten. Ob Endless gewaehlt
        // ist, aendert die Farbe bewusst nicht - das sagt schon der Haken.
        bool canPlay = open && linked;
        Color fill = canPlay ? playFillReady : playFillLocked;
        playFill.color = (canPlay && hover == Hit.Play) ? Lift(fill, hoverLift) : fill;
        playText.color = canPlay ? panelInk : panelInkDim;
        playText.text = (open && !linked) ? comingSoonLabel : playLabel;
    }

    void ApplyPreview(LevelEntry e, bool open, Image window, Image image)
    {
        bool hasSprite = e != null && e.preview != null;

        image.gameObject.SetActive(hasSprite);
        if (hasSprite)
        {
            image.sprite = e.preview;
            // Gesperrt wird abgedunkelt - der Schleier darueber macht den Rest.
            image.color = open ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        window.color = previewEmpty;
    }

    /// <summary>Hellt eine Farbe auf, ohne ihre Deckkraft anzutasten.</summary>
    static Color Lift(Color c, float amount)
    {
        return new Color(Mathf.Lerp(c.r, 1f, amount),
                         Mathf.Lerp(c.g, 1f, amount),
                         Mathf.Lerp(c.b, 1f, amount), c.a);
    }

    // ------------------------------------------------------------- Unlocks

    bool StoryUnlocked(LevelEntry e)
    {
        if (e == null) return false;
        if (string.IsNullOrWhiteSpace(e.storyUnlockId)) return true;
        return Unlocks.IsUnlocked(e.storyUnlockId);
    }

    bool EndlessUnlocked(LevelEntry e)
    {
        if (e == null || !e.endlessAvailable) return false;
        if (string.IsNullOrWhiteSpace(e.endlessUnlockId)) return true;
        return Unlocks.IsUnlocked(e.endlessUnlockId);
    }

    /// <summary>
    /// Gibt es die Szene zu diesem Level schon? Ist sie nicht eingetragen oder
    /// fehlt sie in den Build Settings, bleibt die Karte sichtbar und der Knopf
    /// sagt es - so kann man Level vorbereiten, bevor sie gebaut sind.
    /// </summary>
    bool IsLinked(LevelEntry e)
    {
        if (e == null || string.IsNullOrWhiteSpace(e.sceneToLoad)) return false;
        return Application.CanStreamedLevelBeLoaded(e.sceneToLoad);
    }

    // -------------------------------------------------------------- Starten

    void Play()
    {
        LevelEntry e = Current;
        if (e == null) return;

        bool mayPlay = endlessChosen ? EndlessUnlocked(e) : StoryUnlocked(e);
        if (!mayPlay) { PlayDenySound(); return; }

        if (!IsLinked(e))
        {
            Debug.Log($"[Levelauswahl] Level {selected + 1} ist noch nicht verknuepft " +
                      $"(Szene \"{e.sceneToLoad}\"). Szenenname im Inspector eintragen und " +
                      "in die Build Settings aufnehmen.");
            PlayDenySound();
            return;
        }

        // Ab hier laeuft genau das ab, was in der World Map beim Betreten eines
        // Map-Punktes passiert (PlayerWorldInteraction, Fall "Map"). Wer beim
        // Anbinden eines Levels etwas vermisst, vergleicht am besten dort.

        // 1. Story oder Endless? Das liest der Rest des Spiels aus GameSession.
        GameSession.SelectedMode = endlessChosen ? GameMode.Endless : GameMode.Story;

        // 2. Stand sichern, bevor es losgeht: der Tracker merkt sich, welche
        //    Achievements und Unlocks vor dem Lauf schon offen waren, damit der
        //    Abschlussbildschirm nur die neuen zeigt. Er legt sich selbst an
        //    (RuntimeInitializeOnLoadMethod), das ?. ist nur Vorsicht.
        //    Ach.FirstGame wird hier bewusst NICHT freigeschaltet - das macht
        //    PlayerController.StartStats() in der Zielszene. Stuende es hier,
        //    waeren die Souls dafuer schon vor dem Merken gezaehlt und wuerden
        //    in der "Cookie Souls: +X"-Anzeige des ersten Laufs fehlen.
        SessionProgressTracker.Instance?.SnapshotBeforeGame();

        // 3. Skilltree auf den gewaehlten Charakter stellen - Skills und Shop
        //    sind statische Kataloge, die brauchen keinen Manager in der Szene.
        Skills.SetActiveTreeForCharacter(Shop.SkinIndex);

        // 4. Welche Welt gespielt wird, steht im MapsManager - er ueberlebt den
        //    Szenenwechsel, und WorldSelector in der Zielszene schaltet daraufhin
        //    worlds[selectedMap] frei. Als Szenen-Objekt steht er nur in der
        //    World Map; kommt der Spieler aus dem Hub, legt Ensure() ihn an.
        MapsManager.Ensure().selectedMap = e.mapId;

        // 5. Shop-Stand fuer diesen Lauf einfrieren. Die gekauften Upgrades
        //    liefen frueher als float[30] extraData ueber den MapsManager; die
        //    Game-Szene fragt sie jetzt ueber Shop.Get / Shop.IsBought ab.
        Shop.CaptureRun();

        PlayStartSound();

        // 6. Umschalten. Den Szenennamen erst merken, dann schliessen - nach dem
        //    Wechsel steht die Auswahl sonst noch offen im Hub herum.
        //    Der Name ist zugleich das Rueckreiseziel: GameManager.Restart()
        //    bringt den Spieler nach dem Lauf genau hierher zurueck.
        string hubScene = gameObject.scene.name;
        GameSession.ReturnScene = hubScene;
        Close();

        // Der uebliche Weg: additiv laden und den Hub stilllegen, genau wie die
        // World Map es mit sich selbst macht. Der MenuManager ueberlebt das.
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ActivateScene(e.sceneToLoad);
            MenuManager.Instance.DeactivateScene(hubScene);
            return;
        }

        // Rueckfallebene ohne MenuManager (hub allein gestartet): hart umschalten.
        SceneManager.LoadScene(e.sceneToLoad, LoadSceneMode.Single);
    }

    // ----------------------------------------------------------------- Ton

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    void PlayStartSound()
    {
        if (startClip != null) { PlaySfx(startClip); return; }
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();
    }

    void PlayDenySound()
    {
        if (denyClip != null) { PlaySfx(denyClip); return; }
        if (AudioController.Instance != null && AudioController.Instance.PlayerHit != null)
            AudioController.Instance.PlayerHit.Play();
    }
}
