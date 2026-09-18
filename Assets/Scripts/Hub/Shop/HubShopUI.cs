using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Der Shop im Hub, formatfuellend auf 320x180.
///
/// Der Rahmen ist das gepixelte shopUI.png - Trennlinien, Panels und Knopfflaechen
/// stecken schon im Bild. Dieses Skript legt nur Text, Symbole und die Auswahl
/// darueber. Alle Kaesten stehen als <see cref="Rect"/> im Inspector, gemessen in
/// Pixeln des Bildes mit Nullpunkt links oben - wer das Bild nachschaerft, zieht
/// die Rechtecke einfach nach.
///
/// Was im Shop steht, kommt aus dem Katalog <see cref="Shop"/> - eine Zeile C#
/// pro Eintrag, in der Reihenfolge, in der sie in der Liste erscheinen. Preise,
/// Stufen und Muenzen laufen ueber dieselbe Stelle wie im Shop der World Map,
/// beide zeigen also immer denselben Stand.
///
/// Dieses Skript kennt keinen einzigen Eintrag beim Namen: neue Items tauchen
/// hier von allein auf, sobald sie im Katalog stehen.
/// </summary>
[DisallowMultipleComponent]
public class HubShopUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    // Inhalt und Reihenfolge stehen im Katalog Shop.cs - hier gibt es dazu
    // bewusst kein Inspector-Feld mehr.

    [Header("Beschriftungen")]
    [SerializeField] private string titleLabel = "SHOP";
    [SerializeField] private string goldLabel  = "GOLD:";
    [SerializeField] private string priceLabel = "PRICE:";
    [SerializeField] private string buyLabel   = "BUY";
    [SerializeField] private string backLabel  = "BACK";
    [Tooltip("Statt eines Preises, wenn die letzte Stufe gekauft ist.")]
    [SerializeField] private string maxLabel = "MAX";
    [Tooltip("{0} = aktuelle Stufe, {1} = hoechste Stufe.")]
    [SerializeField] private string levelFormat = "Level {0}/{1}";
    [Tooltip("{0} = jetziger Wert, {1} = Wert nach dem Kauf.")]
    [SerializeField] private string upgradeFormat = "{0}  >  {1}";
    [Tooltip("Zeichen links neben dem gewaehlten Eintrag.")]
    [SerializeField] private string selectionMarker = ">";
    [Tooltip("Zeichen links neben einem fertig gekauften Eintrag. Leer lassen, wenn " +
             "die Pixelschrift das Zeichen nicht hat - die Zeile ist auch ohne markiert.")]
    [SerializeField] private string boughtMarker = "";

    [Header("Grafik")]
    [Tooltip("Der gepixelte Rahmen. 320x180, fuellt den Bildschirm.")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("Muenze neben Gold- und Preisanzeige.")]
    [SerializeField] private Sprite coinSprite;
    [Tooltip("PixelArtFont. Leer = TMP-Standardfont.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Kaesten (Pixel im 320x180-Bild, Nullpunkt links oben)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [SerializeField] private Rect titleArea   = new Rect(18f, 12f, 92f, 15f);
    [SerializeField] private Rect goldTextArea = new Rect(205f, 15f, 40f, 11f);
    [SerializeField] private Rect goldCoinArea = new Rect(245f, 16f, 9f, 9f);
    [SerializeField] private Rect goldValueArea = new Rect(257f, 15f, 37f, 11f);
    [SerializeField] private Rect listArea    = new Rect(13f, 34f, 143f, 129f);
    [SerializeField] private Rect detailIconArea = new Rect(171f, 43f, 38f, 37f);
    [SerializeField] private Rect detailNameArea = new Rect(217f, 45f, 78f, 13f);
    [SerializeField] private Rect detailTextArea = new Rect(217f, 65f, 78f, 44f);
    [SerializeField] private Rect priceTextArea  = new Rect(171f, 116f, 42f, 12f);
    [SerializeField] private Rect priceCoinArea  = new Rect(212f, 117f, 10f, 10f);
    [SerializeField] private Rect priceValueArea = new Rect(226f, 116f, 70f, 12f);
    [SerializeField] private Rect buyArea  = new Rect(168f, 143f, 64f, 16f);
    [SerializeField] private Rect backArea = new Rect(242f, 143f, 60f, 16f);

    [Header("Liste")]
    [SerializeField] private int visibleRows = 6;
    [Tooltip("Fertig gekaufte Eintraege wandern ans Ende der Liste. Umsortiert wird " +
             "erst beim naechsten Oeffnen - sonst rutscht die Liste beim Kaufen weg.")]
    [SerializeField] private bool sortBoughtToBottom = true;
    [Tooltip("Ganze Pixel halten die Schrift scharf - das Listenfeld hat keine " +
             "eingebackenen Trennlinien, an die sich die Zeilen halten muessten.")]
    [SerializeField] private float rowHeight = 20f;
    [Tooltip("Abstand der Zeile zum linken und oberen Rand des Listenfeldes.")]
    [SerializeField] private Vector2 rowInset = new Vector2(5f, 4f);
    [SerializeField] private float rowWidth = 133f;

    [Header("Scrollbalken")]
    [SerializeField] private bool showScrollbar = true;
    [Tooltip("Rechts im Listenfeld. Die Zeilen enden bei x=151, die Flaeche bei x=155.")]
    [SerializeField] private Rect scrollbarArea = new Rect(152f, 38f, 3f, 120f);
    [SerializeField] private Color scrollbarTrackColor = new Color32(0xC4, 0xA5, 0x7B, 0xFF);
    [SerializeField] private Color scrollbarThumbColor = new Color32(0x63, 0x41, 0x45, 0xFF);
    [Tooltip("Verschwindet, solange alles auf eine Seite passt.")]
    [SerializeField] private bool hideScrollbarWhenUnneeded = true;
    [Tooltip("Damit der Griff bei vielen Eintraegen nicht zum Strich wird.")]
    [SerializeField] private float minThumbHeight = 10f;

    [Header("Zeilenrahmen")]
    [Tooltip("Umrandet jeden Eintrag wie einen Knopf.")]
    [SerializeField] private bool showRowFrames = true;
    [Tooltip("Leer = ein 1px-Rahmen wird zur Laufzeit erzeugt. Eigenes 9-Slice-Sprite " +
             "gewinnt, falls eingetragen.")]
    [SerializeField] private Sprite rowFrameSprite;
    [SerializeField] private Color rowFrameColor = new Color32(0x97, 0x71, 0x5E, 0xFF);
    [SerializeField] private Color rowFrameSelectedColor = new Color32(0x3B, 0x24, 0x33, 0xFF);
    [Tooltip("Luft zwischen zwei Rahmen, damit sie sich nicht zu einer doppelten Linie addieren.")]
    [SerializeField] private float rowFrameGap = 1f;

    [Header("Zeilen-Aufteilung (relativ zur Zeile)")]
    [SerializeField] private Rect rowMarkerArea = new Rect(2f, 4f, 7f, 11f);
    [SerializeField] private Rect rowIconArea   = new Rect(10f, 3f, 14f, 14f);
    [SerializeField] private Rect rowNameArea   = new Rect(28f, 4f, 62f, 11f);
    [SerializeField] private Rect rowCoinArea   = new Rect(93f, 5f, 9f, 9f);
    [SerializeField] private Rect rowPriceArea  = new Rect(104f, 4f, 28f, 11f);

    [Header("Schriftgroessen")]
    [SerializeField] private float titleFontSize  = 11f;
    [SerializeField] private float goldFontSize   = 8f;
    [SerializeField] private float rowFontSize    = 8f;
    [SerializeField] private float detailNameFontSize = 9f;
    [SerializeField] private float detailFontSize = 7f;
    [SerializeField] private float buttonFontSize = 9f;
    [SerializeField] private float detailLineSpacing = 6f;
    [Tooltip("So klein darf ein zu langer Name hoechstens werden, bevor er ueberlaeuft.")]
    [SerializeField, Range(3f, 8f)] private float minAutoFontSize = 5f;

    [Header("Farben")]
    [SerializeField] private Color titleColor     = new Color32(0x3B, 0x24, 0x33, 0xFF);
    [SerializeField] private Color goldTextColor  = new Color32(0xE6, 0xC4, 0x93, 0xFF);
    [SerializeField] private Color rowTextColor   = new Color32(0x3B, 0x24, 0x33, 0xFF);
    [SerializeField] private Color rowSelectedColor = new Color32(0x2A, 0x18, 0x24, 0xFF);
    [SerializeField] private Color rowLockedColor = new Color32(0x97, 0x71, 0x5E, 0xFF);
    [SerializeField] private Color highlightColor = new Color32(0xF2, 0xE2, 0xBE, 0xFF);
    [SerializeField] private Color detailTextColor = new Color32(0x63, 0x41, 0x45, 0xFF);
    [SerializeField] private Color buyTextColor   = new Color32(0xE6, 0xC4, 0x93, 0xFF);
    [SerializeField] private Color backTextColor  = new Color32(0x3B, 0x24, 0x33, 0xFF);
    [Tooltip("Preis in der Liste, wenn das Geld nicht reicht.")]
    [SerializeField] private Color tooExpensiveColor = new Color32(0xA8, 0x3C, 0x3C, 0xFF);
    [Tooltip("Name eines fertig gekauften Eintrags - blasser als der Rest.")]
    [SerializeField] private Color rowBoughtColor = new Color32(0x97, 0x71, 0x5E, 0xFF);
    [Tooltip("Rahmen eines fertig gekauften Eintrags.")]
    [SerializeField] private Color rowBoughtFrameColor = new Color32(0xC4, 0xA5, 0x7B, 0xFF);
    [Tooltip("Leichter Schleier ueber der ganzen Zeile, solange sie nicht gewaehlt ist. " +
             "Das ist die eigentliche Markierung - sie haengt an keinem Schriftzeichen.")]
    [SerializeField] private Color rowBoughtTint = new Color32(0x97, 0x71, 0x5E, 0x33);
    [Tooltip("Symbol eines gekauften Eintrags. Weiss = unveraendert.")]
    [SerializeField] private Color boughtIconTint = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Der Kaufknopf, wenn gerade nichts zu kaufen ist.")]
    [SerializeField] private Color buyDisabledColor = new Color32(0x6E, 0x6E, 0x6E, 0x80);
    [Tooltip("Der Rand neben den 320x180, wenn der Bildschirm nicht 16:9 ist. " +
             "Standard ist der dunkelste Ton des Rahmens.")]
    [SerializeField] private Color backdropColor = new Color32(0x3B, 0x24, 0x33, 0xFF);
    [Tooltip("Liegt beim Ueberfahren ueber der Knopfflaeche. Die Knoepfe stecken im " +
             "Hintergrundbild, also wird aufgehellt statt umgefaerbt - so bleibt die " +
             "gepixelte Flaeche erkennbar.")]
    [SerializeField] private Color buttonHoverTint = new Color(1f, 1f, 1f, 0.16f);

    [Header("Steuerung")]
    [Tooltip("Aus: gekauft wird nur ueber den BUY-Knopf. An: die beiden Tasten " +
             "darunter kaufen den gewaehlten Eintrag zusaetzlich.")]
    [SerializeField] private bool allowKeyboardBuy = false;
    [SerializeField] private KeyCode buyKey   = KeyCode.Return;
    [SerializeField] private KeyCode buyKeyAlt = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Sound")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Leer = es wird MenuClick aus dem AudioController gespielt, genau wie " +
             "im Shop der World Map.")]
    [SerializeField] private AudioClip buyClip;
    [Tooltip("Wenn das Geld nicht reicht oder die Stufe schon MAX ist. " +
             "Leer = PlayerHit aus dem AudioController, wie im alten Shop.")]
    [SerializeField] private AudioClip denyClip;
    [Tooltip("Beim Ueberfahren von BUY und BACK. Optional.")]
    [SerializeField] private AudioClip hoverClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ------------------------------------------------------------ Laufzeit

    class Row
    {
        public GameObject Go;
        public Image Highlight;
        public Image Frame;
        public TextMeshProUGUI Marker;
        public Image Icon;
        public TextMeshProUGUI Name;
        public Image Coin;
        public TextMeshProUGUI Price;
    }

    GameObject root;          // ganzer Canvasbereich, wird an- und ausgeschaltet
    RectTransform screen;     // exakt 320x180 darin - daran haengt alles Ausgemessene
    TextMeshProUGUI goldValueText, detailNameText, detailText, priceValueText, buyText;
    Image detailIcon, buyImage, backImage;
    GameObject scrollbarRoot;
    Scrollbar scrollbar;
    bool suppressScrollbarCallback;
    Color buyEnabledColor;
    bool buyEnabled, buyHovered, backHovered;
    readonly List<Row> rows = new List<Row>();

    // Nur die Eintraege, die gerade freigeschaltet sind - der Rest taucht gar nicht auf
    readonly List<ShopItemDef> shown = new List<ShopItemDef>();
    int selected;
    int scrollTop;
    int openedOnFrame = -1;
    int lastCurrency = int.MinValue;
    bool built;

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
        // Das Rahmensprite wurde zur Laufzeit erzeugt, also raeumen wir es auch
        // selbst wieder weg - Unity sammelt so etwas nicht ein.
        if (generatedFrame != null)
        {
            if (generatedFrame.texture != null) Destroy(generatedFrame.texture);
            Destroy(generatedFrame);
            generatedFrame = null;
        }

        // Szenenwechsel mit offenem Shop: die Sperre wieder abmelden,
        // sonst reagiert der Hub beim naechsten Mal auf gar nichts mehr.
        if (!IsOpen) return;
        IsOpen = false;
        HubUI.PopModal();
    }

    void Build()
    {
        if (built) return;
        built = true;

        var canvasGO = new GameObject("ShopCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) canvasGO.layer = uiLayer;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ueber Textbox (100) und Konsole (120)
        canvas.sortingOrder = 130;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        // Der Rahmen ist genau 320x180 - er soll formatfuellend bleiben und
        // nicht an einer Seite abgeschnitten werden.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root = HubUiKit.NewRect("Shop", canvasGO.transform);
        HubUiKit.Stretch((RectTransform)root.transform);

        // Bei einem Bildschirm, der nicht genau 16:9 ist, bleibt neben den
        // 320x180 ein Rand. Der bekommt eine eigene Flaeche, damit dort nicht
        // der Hub durchscheint - und faengt gleich die Klicks ab.
        var backdrop = HubUiKit.NewImage("Backdrop", root.transform, null, backdropColor);
        HubUiKit.Stretch((RectTransform)backdrop.transform);
        backdrop.raycastTarget = true;

        // Feste 320x180, mittig. Nur so sitzt jeder ausgemessene Pixel da, wo
        // er auch im shopUI.png sitzt - ein mitgedehnter Canvas wuerde die
        // Kaesten gegen die Grafik verschieben, zum Rand hin immer weiter.
        screen = (RectTransform)HubUiKit.NewRect("Screen", root.transform).transform;
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(0.5f, 0.5f);
        screen.sizeDelta = referenceResolution;
        screen.anchoredPosition = Vector2.zero;

        // ---- Hintergrund --------------------------------------------------
        var bg = HubUiKit.NewImage("Background", screen, backgroundSprite, Color.white);
        HubUiKit.Stretch((RectTransform)bg.transform);
        bg.preserveAspect = false;   // fuellt genau die 320x180

        // ---- Kopfzeile ----------------------------------------------------
        var title = HubUiKit.NewText("Title", screen, font, titleFontSize,
                                     titleColor, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)title.transform, titleArea);
        title.text = titleLabel;

        var gold = HubUiKit.NewText("GoldLabel", screen, font, goldFontSize,
                                    goldTextColor, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)gold.transform, goldTextArea);
        gold.text = goldLabel;

        var goldCoin = HubUiKit.NewImage("GoldCoin", screen, coinSprite, Color.white);
        HubUiKit.Place((RectTransform)goldCoin.transform, goldCoinArea);

        goldValueText = HubUiKit.NewText("GoldValue", screen, font, goldFontSize,
                                         goldTextColor, TextAlignmentOptions.Right);
        HubUiKit.Place((RectTransform)goldValueText.transform, goldValueArea);

        // ---- Liste --------------------------------------------------------
        for (int i = 0; i < visibleRows; i++) rows.Add(BuildRow(i));
        if (showScrollbar) BuildScrollbar();

        // ---- Detailfeld ---------------------------------------------------
        detailIcon = HubUiKit.NewImage("DetailIcon", screen, null, Color.white);
        HubUiKit.Place((RectTransform)detailIcon.transform, detailIconArea);
        // Das Sprite kommt erst beim Auswaehlen - das Seitenverhaeltnis muss
        // trotzdem jetzt schon feststehen, sonst wird Pixelart verzerrt.
        detailIcon.preserveAspect = true;

        detailNameText = HubUiKit.NewText("DetailName", screen, font, detailNameFontSize,
                                          rowTextColor, TextAlignmentOptions.TopLeft);
        HubUiKit.Place((RectTransform)detailNameText.transform, detailNameArea);
        FitOneLine(detailNameText, detailNameFontSize);

        detailText = HubUiKit.NewText("DetailText", screen, font, detailFontSize,
                                      detailTextColor, TextAlignmentOptions.TopLeft);
        HubUiKit.Place((RectTransform)detailText.transform, detailTextArea);
        detailText.lineSpacing = detailLineSpacing;

        var price = HubUiKit.NewText("PriceLabel", screen, font, detailNameFontSize,
                                     rowTextColor, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)price.transform, priceTextArea);
        price.text = priceLabel;

        var priceCoin = HubUiKit.NewImage("PriceCoin", screen, coinSprite, Color.white);
        HubUiKit.Place((RectTransform)priceCoin.transform, priceCoinArea);

        priceValueText = HubUiKit.NewText("PriceValue", screen, font, detailNameFontSize,
                                          rowTextColor, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)priceValueText.transform, priceValueArea);

        // ---- Knoepfe ------------------------------------------------------
        // Die Knopfflaechen sind schon im Hintergrundbild. Hier liegt nur eine
        // unsichtbare Klickflaeche drauf, plus die Beschriftung.
        buyImage = HubUiKit.NewImage("BuyHit", screen, null, Color.clear);
        HubUiKit.Place((RectTransform)buyImage.transform, buyArea);
        buyImage.raycastTarget = true;
        AddClick(buyImage.gameObject, TryBuy);
        AddHover(buyImage.gameObject, SetBuyHover);

        buyText = HubUiKit.NewText("BuyLabel", screen, font, buttonFontSize,
                                   buyTextColor, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)buyText.transform, buyArea);
        buyText.text = buyLabel;
        buyEnabledColor = buyTextColor;

        backImage = HubUiKit.NewImage("BackHit", screen, null, Color.clear);
        HubUiKit.Place((RectTransform)backImage.transform, backArea);
        backImage.raycastTarget = true;
        AddClick(backImage.gameObject, Close);
        AddHover(backImage.gameObject, SetBackHover);

        var back = HubUiKit.NewText("BackLabel", screen, font, buttonFontSize,
                                    backTextColor, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)back.transform, backArea);
        back.text = backLabel;

        root.SetActive(false);
    }

    /// <summary>
    /// Unitys Scrollbar statt Eigenbau: Ziehen am Griff, Klick auf die Spur und
    /// das Rechnen der Griffposition kommen damit fertig. Wir uebersetzen nur
    /// zwischen seinem 0..1-Wert und der obersten sichtbaren Zeile.
    /// </summary>
    void BuildScrollbar()
    {
        scrollbarRoot = HubUiKit.NewRect("Scrollbar", screen);
        HubUiKit.Place((RectTransform)scrollbarRoot.transform, scrollbarArea);

        var track = scrollbarRoot.AddComponent<Image>();
        track.color = scrollbarTrackColor;
        track.raycastTarget = true;   // Klick auf die Spur springt seitenweise

        var slidingArea = HubUiKit.NewRect("Sliding Area", scrollbarRoot.transform);
        HubUiKit.Stretch((RectTransform)slidingArea.transform);

        var handle = HubUiKit.NewRect("Handle", slidingArea.transform);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = scrollbarThumbColor;
        HubUiKit.Stretch((RectTransform)handle.transform);

        scrollbar = scrollbarRoot.AddComponent<Scrollbar>();
        scrollbar.handleRect = (RectTransform)handle.transform;
        scrollbar.targetGraphic = handleImage;
        // Wert 0 = oben. Sonst laeuft der Griff andersherum als die Liste.
        scrollbar.direction = Scrollbar.Direction.TopToBottom;
        // Kein Farbwechsel beim Ueberfahren - das flackert in Pixelart nur
        scrollbar.transition = Selectable.Transition.None;
        // Haelt den Balken aus der Tastaturnavigation raus, sonst wuerde er
        // die Pfeiltasten mitlesen, die schon die Auswahl bewegen.
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
    }

    void OnScrollbarChanged(float value)
    {
        // Wir haben den Wert gerade selbst gesetzt - nicht zurueckreagieren
        if (suppressScrollbarCallback || !IsOpen) return;

        int maxTop = MaxScrollTop;
        if (maxTop <= 0) return;

        int top = Mathf.RoundToInt(value * maxTop);
        if (top == scrollTop) return;

        scrollTop = top;
        // Auswahl ins neue Fenster ziehen, sonst schiebt Refresh() gleich wieder zurueck
        selected = Mathf.Clamp(selected, scrollTop, scrollTop + visibleRows - 1);
        Refresh();
    }

    int MaxScrollTop => Mathf.Max(0, shown.Count - visibleRows);

    void UpdateScrollbar()
    {
        if (scrollbar == null) return;

        int maxTop = MaxScrollTop;
        bool needed = maxTop > 0;

        if (hideScrollbarWhenUnneeded && scrollbarRoot.activeSelf != needed)
            scrollbarRoot.SetActive(needed);

        suppressScrollbarCallback = true;

        if (needed)
        {
            float fraction = (float)visibleRows / shown.Count;
            float minFraction = scrollbarArea.height > 0f
                ? minThumbHeight / scrollbarArea.height : 0.1f;
            scrollbar.size = Mathf.Clamp(fraction, Mathf.Min(minFraction, 1f), 1f);
            // Ein Schritt je moeglicher Fensterposition - der Griff rastet auf Zeilen ein
            scrollbar.numberOfSteps = maxTop + 1;
            scrollbar.value = (float)scrollTop / maxTop;
        }
        else
        {
            scrollbar.size = 1f;
            scrollbar.numberOfSteps = 0;
            scrollbar.value = 0f;
        }

        suppressScrollbarCallback = false;
    }

    Row BuildRow(int slot)
    {
        var row = new Row();
        row.Go = HubUiKit.NewRect("Row" + slot, screen);
        HubUiKit.Place((RectTransform)row.Go.transform, new Rect(
            listArea.x + rowInset.x,
            listArea.y + rowInset.y + slot * rowHeight,
            rowWidth,
            rowHeight));

        // Auswahlbalken liegt hinter allem anderen
        row.Highlight = HubUiKit.NewImage("Highlight", row.Go.transform, null, highlightColor);
        FillRow((RectTransform)row.Highlight.transform);
        // Klickflaeche: die ganze Zeile waehlt aus
        row.Highlight.raycastTarget = true;
        int captured = slot;
        AddClick(row.Highlight.gameObject, () => ClickRow(captured));

        // Rahmen ueber der Fuellung, aber unter dem Inhalt
        if (showRowFrames)
        {
            row.Frame = HubUiKit.NewImage("Frame", row.Go.transform, RowFrameSprite(), Color.white);
            FillRow((RectTransform)row.Frame.transform);
            row.Frame.type = Image.Type.Sliced;
            row.Frame.preserveAspect = false;
            row.Frame.color = rowFrameColor;
        }

        row.Marker = HubUiKit.NewText("Marker", row.Go.transform, font, rowFontSize,
                                      rowSelectedColor, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)row.Marker.transform, rowMarkerArea);

        row.Icon = HubUiKit.NewImage("Icon", row.Go.transform, null, Color.white);
        HubUiKit.Place((RectTransform)row.Icon.transform, rowIconArea);
        row.Icon.preserveAspect = true;

        row.Name = HubUiKit.NewText("Name", row.Go.transform, font, rowFontSize,
                                    rowTextColor, TextAlignmentOptions.Left);
        HubUiKit.Place((RectTransform)row.Name.transform, rowNameArea);
        FitOneLine(row.Name, rowFontSize);

        row.Coin = HubUiKit.NewImage("Coin", row.Go.transform, coinSprite, Color.white);
        HubUiKit.Place((RectTransform)row.Coin.transform, rowCoinArea);

        row.Price = HubUiKit.NewText("Price", row.Go.transform, font, rowFontSize,
                                     rowTextColor, TextAlignmentOptions.Right);
        HubUiKit.Place((RectTransform)row.Price.transform, rowPriceArea);

        return row;
    }

    /// <summary>
    /// Namen sind verschieden lang - "Currency Gain" passt, "Celestial Star" nicht.
    /// Statt abzuschneiden darf die Zeile kleiner werden, umbrechen aber nicht:
    /// eine zweite Zeile wuerde die Zeilenhoehe sprengen.
    /// </summary>
    void FitOneLine(TextMeshProUGUI text, float maxSize)
    {
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(minAutoFontSize, maxSize);
        text.fontSizeMax = maxSize;
    }

    /// <summary>
    /// Fuellt die Zeile, laesst unten aber <see cref="rowFrameGap"/> frei -
    /// sonst stossen die Rahmen zweier Zeilen aneinander und sehen doppelt dick aus.
    /// </summary>
    void FillRow(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(0f, rowFrameGap);
        r.offsetMax = Vector2.zero;
    }

    Sprite generatedFrame;

    /// <summary>
    /// Ein 1px-Rahmen als 3x3-Sprite mit 9-Slice-Raendern. Bei einem Pixel pro
    /// Einheit bleibt die Linie genau einen Design-Pixel breit, egal wie gross
    /// die Zeile ist - anders als ein skaliertes Bild.
    /// </summary>
    Sprite RowFrameSprite()
    {
        if (rowFrameSprite != null) return rowFrameSprite;
        if (generatedFrame != null) return generatedFrame;

        var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "HubShopRowFrame",
        };

        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                tex.SetPixel(x, y, (x == 1 && y == 1) ? Color.clear : Color.white);
        tex.Apply();

        generatedFrame = Sprite.Create(tex, new Rect(0f, 0f, 3f, 3f), new Vector2(0.5f, 0.5f),
                                       1f, 0, SpriteMeshType.FullRect, new Vector4(1f, 1f, 1f, 1f));
        generatedFrame.name = "HubShopRowFrame";
        return generatedFrame;
    }

    /// <summary>
    /// Ein Objekt braucht nur einen EventTrigger - Klick und Hover teilen ihn sich,
    /// sonst haengen zwei Komponenten mit je einer Haelfte am selben Knopf.
    /// </summary>
    static EventTrigger TriggerOn(GameObject go)
    {
        EventTrigger t = go.GetComponent<EventTrigger>();
        return t != null ? t : go.AddComponent<EventTrigger>();
    }

    static void AddEvent(GameObject go, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        TriggerOn(go).triggers.Add(entry);
    }

    static void AddClick(GameObject go, System.Action action) =>
        AddEvent(go, EventTriggerType.PointerClick, action);

    static void AddHover(GameObject go, System.Action<bool> onHover)
    {
        AddEvent(go, EventTriggerType.PointerEnter, () => onHover(true));
        AddEvent(go, EventTriggerType.PointerExit,  () => onHover(false));
    }

    // --------------------------------------------------------------- Oeffnen

    public void Open()
    {
        if (IsOpen) return;

        Build();
        HubUiKit.EnsureEventSystem();

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        root.SetActive(true);

        RebuildShownList();
        selected = 0;
        scrollTop = 0;
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

    /// <summary>
    /// Sammelt die Eintraege ein, die gerade sichtbar sein duerfen. Die Regel
    /// steht am Katalogeintrag (requiredUnlock), nicht hier.
    ///
    /// Fertig gekauftes rutscht ans Ende: erst alles, was man noch kaufen kann,
    /// dann die abgehakten - beide Gruppen in Katalogreihenfolge. Zwei Durchgaenge
    /// statt Sort(), weil Sort() nicht stabil ist und die Katalogreihenfolge
    /// innerhalb der Gruppen sonst durcheinandergeraet.
    ///
    /// Gerufen wird das nur beim Oeffnen. Wer gerade etwas kauft, dem soll die
    /// Zeile nicht unter dem Finger wegspringen - der neue Platz gilt ab dem
    /// naechsten Besuch.
    /// </summary>
    void RebuildShownList()
    {
        shown.Clear();

        foreach (ShopItemDef def in Shop.All)
        {
            if (!Shop.IsVisible(def)) continue;
            if (sortBoughtToBottom && IsFullyBought(def)) continue;
            shown.Add(def);
        }

        if (!sortBoughtToBottom) return;

        foreach (ShopItemDef def in Shop.All)
        {
            if (Shop.IsVisible(def) && IsFullyBought(def)) shown.Add(def);
        }
    }

    /// <summary>
    /// Alle Stufen gekauft - da ist nichts mehr zu holen. Eintraege ohne
    /// Preisliste zaehlen nicht dazu: bei denen war nie etwas zu kaufen.
    /// </summary>
    static bool IsFullyBought(ShopItemDef def) => def.MaxLevel > 0 && Shop.IsMaxed(def);

    // -------------------------------------------------------------- Laufzeit

    void Update()
    {
        if (!IsOpen) return;

        // Das [E], mit dem der Shop aufgeht, darf nicht gleich etwas kaufen
        if (Time.frameCount == openedOnFrame) return;

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Move(wheel > 0f ? -1 : +1);

        if (allowKeyboardBuy && (Input.GetKeyDown(buyKey) || Input.GetKeyDown(buyKeyAlt))) TryBuy();

        // Das Gold kann sich auch ausserhalb aendern (Konsole, anderer Shop).
        // Dann stimmt nicht nur die Zahl oben nicht mehr, sondern auch, welche
        // Preise rot sind und ob BUY noch geht - also einmal komplett nachziehen.
        if (Currency != lastCurrency) Refresh();
    }

    void Move(int delta)
    {
        if (shown.Count == 0) return;

        int next = Mathf.Clamp(selected + delta, 0, shown.Count - 1);
        if (next == selected) return;

        selected = next;
        PlaySfx(moveClip);
        Refresh();
    }

    /// <summary>
    /// Ein Klick auf eine Zeile waehlt sie aus - mehr nicht. Gekauft wird
    /// ausschliesslich ueber BUY, damit niemand im Vorbeiklicken sein Geld los ist.
    /// </summary>
    void ClickRow(int slot)
    {
        int index = scrollTop + slot;
        if (index < 0 || index >= shown.Count) return;
        if (index == selected) return;

        selected = index;
        PlaySfx(moveClip);
        Refresh();
    }

    // ------------------------------------------------------------- Anzeige

    void Refresh()
    {
        // Auswahl ins sichtbare Fenster schieben
        if (selected < scrollTop) scrollTop = selected;
        if (selected > scrollTop + visibleRows - 1) scrollTop = selected - visibleRows + 1;
        scrollTop = Mathf.Clamp(scrollTop, 0, Mathf.Max(0, shown.Count - visibleRows));

        for (int slot = 0; slot < rows.Count; slot++)
        {
            Row row = rows[slot];
            int index = scrollTop + slot;

            if (index >= shown.Count) { row.Go.SetActive(false); continue; }

            row.Go.SetActive(true);
            ShopItemDef entry = shown[index];
            bool isSelected = index == selected;
            bool bought = IsFullyBought(entry);

            row.Highlight.color = isSelected ? highlightColor
                                : bought ? rowBoughtTint : Color.clear;

            row.Marker.text = isSelected ? selectionMarker : bought ? boughtMarker : "";
            row.Marker.color = isSelected ? rowSelectedColor : rowBoughtColor;

            if (row.Frame != null)
                row.Frame.color = isSelected ? rowFrameSelectedColor
                                : bought ? rowBoughtFrameColor : rowFrameColor;

            Sprite icon = entry.Icon;
            row.Icon.sprite = icon;
            row.Icon.enabled = icon != null;
            row.Icon.color = bought ? boughtIconTint : Color.white;

            row.Name.text = entry.Name;
            row.Name.color = isSelected ? rowSelectedColor
                           : bought ? rowBoughtColor : rowTextColor;

            bool maxed = Shop.IsMaxed(entry);
            int price = Shop.NextPrice(entry);

            if (entry.MaxLevel == 0)
            {
                // Eintrag ohne Preisliste - hier laesst sich nichts sagen
                row.Price.text = "-";
                row.Price.color = rowLockedColor;
                row.Coin.enabled = false;
            }
            else if (maxed)
            {
                row.Price.text = maxLabel;
                row.Price.color = rowLockedColor;
                row.Coin.enabled = false;
            }
            else
            {
                row.Price.text = price.ToString();
                row.Price.color = price > Currency ? tooExpensiveColor
                                : isSelected ? rowSelectedColor : rowTextColor;
                row.Coin.enabled = coinSprite != null;
            }
        }

        RefreshDetail();
        UpdateGold();
        UpdateScrollbar();
        lastCurrency = Currency;
    }

    void RefreshDetail()
    {
        bool any = selected >= 0 && selected < shown.Count;

        detailIcon.enabled = false;
        if (!any)
        {
            detailNameText.text = "";
            detailText.text = "";
            priceValueText.text = "";
            SetBuyEnabled(false);
            return;
        }

        ShopItemDef entry = shown[selected];

        Sprite icon = entry.Icon;
        detailIcon.sprite = icon;
        detailIcon.enabled = icon != null;
        detailNameText.text = entry.Name;

        bool hasCosts = entry.MaxLevel > 0;
        bool maxed = Shop.IsMaxed(entry);
        int price = Shop.NextPrice(entry);

        detailText.text = BuildDescription(entry, maxed);

        priceValueText.text = !hasCosts ? "-" : maxed ? maxLabel : price.ToString();
        priceValueText.color = (hasCosts && !maxed && price > Currency)
            ? tooExpensiveColor : rowTextColor;

        SetBuyEnabled(Shop.CanBuy(entry));
    }

    /// <summary>
    /// Beschreibung, Stufe und der Sprung, den der naechste Kauf bringt -
    /// derselbe Inhalt wie im alten Shop, nur auf drei Zeilen verteilt.
    /// </summary>
    string BuildDescription(ShopItemDef entry, bool maxed)
    {
        var lines = new List<string>();

        string text = entry.Description;
        if (!string.IsNullOrEmpty(text)) lines.Add(text.Trim());

        if (entry.MaxLevel > 0)
            lines.Add(string.Format(levelFormat, Shop.LevelOf(entry), entry.MaxLevel));

        if (entry.Values.Count > 0)
        {
            string current = Nice(Shop.CurrentValue(entry));
            lines.Add(maxed
                ? string.Format(upgradeFormat, current, maxLabel)
                : string.Format(upgradeFormat, current, Nice(Shop.NextValue(entry))));
        }

        return string.Join("\n", lines);
    }

    /// <summary>0,35 statt 0,3500001 - und ganze Zahlen ohne Komma.</summary>
    static string Nice(float v) =>
        Mathf.Approximately(v, Mathf.Round(v))
            ? Mathf.RoundToInt(v).ToString()
            : v.ToString("0.##");

    void SetBuyEnabled(bool enabled)
    {
        buyEnabled = enabled;
        buyText.color = enabled ? buyEnabledColor : buyDisabledColor;
        buyImage.raycastTarget = enabled;

        // Wird der Knopf unter dem Mauszeiger ausgegraut - etwa weil man die
        // letzte Stufe gerade gekauft hat - kommt kein PointerExit mehr. Die
        // Aufhellung muss hier also selbst weg, sonst bleibt sie haengen.
        if (!enabled) buyHovered = false;
        ApplyHoverTints();
    }

    /// <summary>Hover nur, solange der Knopf auch etwas tut.</summary>
    void SetBuyHover(bool on)
    {
        buyHovered = on && buyEnabled;
        if (buyHovered) PlaySfx(hoverClip);
        ApplyHoverTints();
    }

    void SetBackHover(bool on)
    {
        backHovered = on;
        if (backHovered) PlaySfx(hoverClip);
        ApplyHoverTints();
    }

    void ApplyHoverTints()
    {
        if (buyImage != null)  buyImage.color  = buyHovered  ? buttonHoverTint : Color.clear;
        if (backImage != null) backImage.color = backHovered ? buttonHoverTint : Color.clear;
    }

    void UpdateGold()
    {
        // Ohne Spielstand gibt es auch kein Geld - das ist eine 0, kein Fragezeichen.
        goldValueText.text = Currency.ToString();
    }

    // ----------------------------------------------------------------- Kauf

    void TryBuy()
    {
        if (selected < 0 || selected >= shown.Count) { PlayDenySound(); return; }

        // Der Katalog entscheidet, ob der Kauf durchgeht - Preis abziehen, Stufe
        // erhoehen und Speichern passiert dort an einer Stelle, fuer beide Shops.
        if (!Shop.TryBuy(shown[selected])) { PlayDenySound(); return; }

        PlayBuySound();
        Refresh();
    }

    // ----------------------------------------------------------- Spielstand

    int Currency => Shop.Currency;

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Eigener Clip, wenn einer eingetragen ist - sonst der Ton, den der Shop in
    /// der World Map an dieser Stelle spielt. So klingt der Hub-Shop ohne Zutun
    /// genauso, auch wenn die Clip-Felder leer bleiben.
    /// </summary>
    void PlayBuySound()
    {
        if (buyClip != null) { PlaySfx(buyClip); return; }
        AudioController ac = AudioController.Instance;
        if (ac != null) ac.PalySound(ac.MenuClick);
    }

    void PlayDenySound()
    {
        if (denyClip != null) { PlaySfx(denyClip); return; }
        AudioController ac = AudioController.Instance;
        if (ac != null) ac.PalySound(ac.PlayerHit);
    }
}
