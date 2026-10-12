using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas fuer den Hub: [E]-Hinweis und die Textbox am unteren Rand.
/// Baut sich zur Laufzeit selbst auf, damit kein UI-Prefab gepflegt werden muss.
/// Layout rechnet in der Projekt-Referenz 320x180, wie das Hauptmenue.
/// </summary>
[DisallowMultipleComponent]
public class HubUI : MonoBehaviour
{
    static HubUI _instance;
    public static HubUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HubUI>();
                if (_instance == null)
                    _instance = new GameObject("HubUI (auto)").AddComponent<HubUI>();
            }
            return _instance;
        }
    }

    public static bool DialogueOpen { get; private set; }

    // Andere Fenster (Konsole, spaeter Shop) melden sich hier an. Gezaehlt statt
    // gebooled, damit sich zwei Fenster nicht gegenseitig wieder aufsperren.
    static int openModals;

    /// <summary>Solange das stimmt, reagiert nichts im Hub auf [E].</summary>
    public static bool InputBlocked => DialogueOpen || openModals > 0;

    /// <summary>Ein eigenes Fenster hat aufgemacht.</summary>
    public static void PushModal() => openModals++;

    /// <summary>Ein eigenes Fenster hat zugemacht.</summary>
    public static void PopModal()
    {
        openModals = Mathf.Max(0, openModals - 1);
        lastModalClosedFrame = Time.frameCount;
    }

    // Fenster schliessen sich selbst mit Escape. Laeuft deren Update vor
    // unserem, ist der Zaehler schon wieder 0, wenn wir denselben Tastendruck
    // sehen - ohne diese Sperre ginge sofort das Pausenmenue auf.
    static int lastModalClosedFrame = -1;

    [Header("Font")]
    [Tooltip("PixelArtFont. Leer = TMP-Standardfont.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Layout (Referenz 320 x 180)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [SerializeField] private float boxHeight    = 72f;
    [SerializeField] private float boxMarginX   = 10f;
    [SerializeField] private float boxMarginY   = 8f;
    [SerializeField] private float borderWidth  = 2f;
    [SerializeField] private float padding      = 7f;
    [SerializeField] private float bodyFontSize = 8f;
    [SerializeField] private float hintFontSize = 6f;
    [SerializeField] private float lineSpacing  = 12f;

    [Header("Farben")]
    [SerializeField] private Color borderColor = new Color32(0xF0, 0xD4, 0x9B, 0xFF);
    [SerializeField] private Color panelColor  = new Color32(0x2A, 0x1C, 0x14, 0xFA);
    [SerializeField] private Color textColor   = new Color32(0xF7, 0xEC, 0xD6, 0xFF);
    [SerializeField] private Color hintColor   = new Color32(0xD9, 0xA4, 0x41, 0xFF);

    [Header("[E]-Hinweis")]
    [SerializeField] private float promptFontSize    = 9f;
    [SerializeField] private float promptKeyFontSize = 8f;
    [Tooltip("Abstand der Hinweis-Tafel zum unteren Bildschirmrand.")]
    [SerializeField] private float promptY = 14f;
    [SerializeField] private Color promptOutlineColor = new Color32(0x14, 0x0C, 0x08, 0xFF);
    [SerializeField] private Color keyShadowColor     = new Color32(0x8A, 0x5A, 0x1E, 0xFF);

    [Header("Dialog-Extras")]
    [Tooltip("Kantenlaenge des Portraits links in der Box (nur wenn ein Portrait mitkommt).")]
    [SerializeField] private float portraitSize = 32f;
    [Tooltip("So oft pro Sekunde wechselt beim Tippen der Mund (Portrait <-> Sprech-Portrait).")]
    [SerializeField] private float mouthFlapRate = 10f;
    [Tooltip("So weit wippt das Portrait beim Tippen.")]
    [SerializeField] private float portraitBob = 1f;
    [Tooltip("So weit wippt der Weiter-Pfeil, wenn die Seite fertig ist.")]
    [SerializeField] private float arrowBob = 1.5f;

    [Header("Geld (oben rechts)")]
    [Tooltip("Zeigt den Kontostand dauerhaft im Hub an.")]
    [SerializeField] private bool showCoins = true;
    [Tooltip("geld.png - Beutel und Schild in einem. Die Muenze steckt schon im Bild, " +
             "daneben kommt nichts mehr.")]
    [SerializeField] private Sprite moneySprite;
    [Tooltip("Abstand zur rechten und oberen Bildschirmkante.")]
    [SerializeField] private Vector2 coinMargin = new Vector2(4f, 4f);
    [Tooltip("Groesse des Bildes in Bildpixeln. 60x25 ist die Originalgroesse von geld.png - " +
             "so bleibt jeder gemalte Pixel ein Bildschirmpixel.")]
    [SerializeField] private Vector2 moneySize = new Vector2(60f, 25f);
    [Tooltip("1 = Originalgroesse. Groesser macht das Schild groesser, die Zahl waechst mit.")]
    [SerializeField] private float moneyScale = 1f;
    [Tooltip("Das helle Feld im Schild, gemessen in Pixeln von geld.png mit Nullpunkt " +
             "links oben. Da hinein kommt die Zahl - rundherum liegen die Nieten.")]
    [SerializeField] private Rect moneyTextArea = new Rect(21f, 9f, 29f, 10f);
    [SerializeField] private float coinFontSize = 8f;
    [Tooltip("So klein darf die Zahl werden, bevor sie ueber das Schild laeuft. " +
             "Sechsstellige Betraege brauchen das.")]
    [SerializeField, Range(3f, 8f)] private float coinMinFontSize = 5f;
    [Tooltip("Dunkel - das Feld im Schild ist hell.")]
    [SerializeField] private Color coinTextColor = new Color32(0x3B, 0x24, 0x33, 0xFF);

    [Header("Sound")]
    [Tooltip("Spielt bei jedem Linksklick in der Textbox, auch beim Schliessen.")]
    [SerializeField] private AudioClip pageTurnClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [Tooltip("Zufaellige Tonhoehe pro Klick, damit das Blaettern nicht stumpf wird. 0 = aus.")]
    [SerializeField, Range(0f, 0.5f)] private float sfxPitchJitter = 0.08f;

    GameObject dialogueRoot, promptRoot, coinRoot;
    TextMeshProUGUI bodyText, hintText, promptLabel, coinValueText;

    // [E]-Hinweis: Tafel, Tastenkappe, Aktion
    RectTransform promptRect, keyCapRect, keyFaceRect, promptLabelRect;
    TextMeshProUGUI keyText;
    string promptShownText;
    bool promptDirty;
    float promptShownAt;

    // Dialog-Extras: getippter Text, Weiter-Pfeil, Namensschild, Portrait
    DialogueText typer;
    RectTransform bodyRect, arrowRect, nameTagRect, portraitRect;
    Image arrowImage, portraitImage;
    TextMeshProUGUI nameText;
    Texture2D arrowTexture;
    Sprite arrowSprite;
    Sprite portraitIdle, portraitTalk;
    int shownCoins = int.MinValue;

    string[] pages;
    int pageIndex;
    bool promptRequestedThisFrame;
    bool built;

    MonoBehaviour playerController;   // eingefrorenes Steuerskript

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        // Unlocks, die ein alter Spielstand schon verdient hat, leise nachholen.
        Unlocks.CatchUp();
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

    void OnEnable()
    {
        // Kaufen, Cheats, Belohnungen - alles laeuft ueber dasselbe Ereignis.
        // Damit muss die Anzeige nichts pro Frame nachschlagen.
        Shop.Changed += RefreshCoins;
        RefreshCoins();
    }

    void OnDisable() => Shop.Changed -= RefreshCoins;

    void OnDestroy()
    {
        if (_instance == this) { _instance = null; DialogueOpen = false; openModals = 0; }
        if (arrowSprite != null) Destroy(arrowSprite);
        if (arrowTexture != null) Destroy(arrowTexture);
    }

    void RefreshCoins()
    {
        if (coinValueText == null) return;

        int coins = Shop.Currency;
        if (coins == shownCoins) return;

        shownCoins = coins;
        coinValueText.text = coins.ToString();
    }

    /// <summary>
    /// Fuer eigene Fenster: friert den Spieler mit derselben Logik ein wie die
    /// Textbox, inklusive Animator-Parameter.
    /// </summary>
    public void SetPlayerFrozen(bool frozen) => FreezePlayer(frozen);

    // ---------------------------------------------------------------- Aufbau

    void Build()
    {
        if (built) return;
        built = true;

        int uiLayer = LayerMask.NameToLayer("UI");

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        if (uiLayer >= 0) canvasGO.layer = uiLayer;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // ---- Textbox ----------------------------------------------------
        dialogueRoot = NewRect("Dialogue", canvasGO.transform);
        var dRect = (RectTransform)dialogueRoot.transform;
        dRect.anchorMin = new Vector2(0f, 0f);
        dRect.anchorMax = new Vector2(1f, 0f);
        dRect.pivot     = new Vector2(0.5f, 0f);
        dRect.offsetMin = new Vector2(boxMarginX, boxMarginY);
        dRect.offsetMax = new Vector2(-boxMarginX, boxMarginY + boxHeight);

        dialogueRoot.AddComponent<Image>().color = borderColor;

        var panel = NewRect("Panel", dialogueRoot.transform);
        Stretch((RectTransform)panel.transform, borderWidth);
        panel.AddComponent<Image>().color = panelColor;

        bodyText = NewText("Body", panel.transform, bodyFontSize, textColor);
        var bRect = (RectTransform)bodyText.transform;
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        // unten mehr Luft: dort sitzt die Seitenzahl, sonst laeuft Text darunter
        bRect.offsetMin = new Vector2(padding, padding + hintFontSize + 2f);
        bRect.offsetMax = new Vector2(-padding, -padding);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.lineSpacing = lineSpacing;
        bodyRect = bRect;
        typer = bodyText.gameObject.AddComponent<DialogueText>();

        // ---- Portrait (links in der Box, nur wenn eins mitkommt) ----------
        var portraitGO = NewRect("Portrait", panel.transform);
        portraitRect = (RectTransform)portraitGO.transform;
        portraitRect.anchorMin = portraitRect.anchorMax = portraitRect.pivot = new Vector2(0f, 1f);
        portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
        portraitRect.anchoredPosition = new Vector2(padding, -padding);
        portraitImage = portraitGO.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        portraitGO.SetActive(false);

        // ---- Weiter-Pfeil (unten rechts, wippt wenn die Seite fertig ist) --
        arrowTexture = new Texture2D(5, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        string[] arrowRows = { "..#..", ".###.", "#####" }; // von unten nach oben
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 5; x++)
                arrowTexture.SetPixel(x, y, arrowRows[y][x] == '#' ? Color.white : Color.clear);
        arrowTexture.Apply();
        arrowSprite = Sprite.Create(arrowTexture, new Rect(0, 0, 5, 3), new Vector2(0.5f, 0.5f), 100f);

        var arrowGO = NewRect("ContinueArrow", dialogueRoot.transform);
        arrowRect = (RectTransform)arrowGO.transform;
        arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(1f, 0f);
        arrowRect.sizeDelta = new Vector2(5f, 3f);
        arrowImage = arrowGO.AddComponent<Image>();
        arrowImage.sprite = arrowSprite;
        arrowImage.color = hintColor;
        arrowImage.raycastTarget = false;
        arrowGO.SetActive(false);

        // ---- Namensschild (Reiter oben links auf der Box) ------------------
        var tagGO = NewRect("NameTag", dialogueRoot.transform);
        nameTagRect = (RectTransform)tagGO.transform;
        nameTagRect.anchorMin = nameTagRect.anchorMax = nameTagRect.pivot = new Vector2(0f, 0f);
        tagGO.AddComponent<Image>().color = borderColor;
        var tagPanel = NewRect("Panel", tagGO.transform);
        Stretch((RectTransform)tagPanel.transform, borderWidth);
        tagPanel.AddComponent<Image>().color = panelColor;
        nameText = NewText("Name", tagPanel.transform, hintFontSize + 1f, borderColor);
        Stretch((RectTransform)nameText.transform, 0f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        tagGO.SetActive(false);

        hintText = NewText("Hint", dialogueRoot.transform, hintFontSize, hintColor);
        var hRect = (RectTransform)hintText.transform;
        hRect.anchorMin = new Vector2(1f, 0f);
        hRect.anchorMax = new Vector2(1f, 0f);
        hRect.pivot     = new Vector2(1f, 0f);
        hRect.sizeDelta = new Vector2(120f, 10f);
        // Platz rechts lassen fuer den Weiter-Pfeil
        hRect.anchoredPosition = new Vector2(-padding - 8f, padding * 0.4f);
        hintText.alignment = TextAlignmentOptions.BottomRight;

        dialogueRoot.SetActive(false);

        // ---- [E]-Hinweis -------------------------------------------------
        BuildPrompt(canvasGO.transform);

        // ---- Muenzanzeige ------------------------------------------------
        BuildCoins(canvasGO.transform);
    }

    /// <summary>
    /// Das Schild fuer den [E]-Hinweis: dunkle Tafel mit hellem Rand wie die
    /// Textbox, links eine Tastenkappe mit der Taste, rechts die Aktion. Die
    /// Breite richtet sich nach dem Text (siehe <see cref="LayoutPrompt"/>).
    /// </summary>
    void BuildPrompt(Transform parent)
    {
        promptRoot = NewRect("Prompt", parent);
        promptRect = (RectTransform)promptRoot.transform;
        promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(0.5f, 0f);

        // Aussen 1 px dunkel, dann 1 px heller Rand: steht so auf hellem
        // Boden genauso wie auf dunklem.
        promptRoot.AddComponent<Image>().color = promptOutlineColor;
        var rim = NewRect("Rim", promptRoot.transform);
        Stretch((RectTransform)rim.transform, 1f);
        rim.AddComponent<Image>().color = borderColor;
        var panel = NewRect("Panel", rim.transform);
        Stretch((RectTransform)panel.transform, 1f);
        panel.AddComponent<Image>().color = panelColor;

        // Tastenkappe: dunkler Sockel, darauf die goldene Flaeche. Beim
        // "Druecken" rutscht die Flaeche 1 px auf den Sockel.
        var cap = NewRect("Key", panel.transform);
        keyCapRect = (RectTransform)cap.transform;
        keyCapRect.anchorMin = keyCapRect.anchorMax = keyCapRect.pivot = new Vector2(0f, 0.5f);
        cap.AddComponent<Image>().color = keyShadowColor;

        var face = NewRect("Face", cap.transform);
        keyFaceRect = (RectTransform)face.transform;
        keyFaceRect.anchorMin = new Vector2(0f, 1f);
        keyFaceRect.anchorMax = new Vector2(1f, 1f);
        keyFaceRect.pivot     = new Vector2(0.5f, 1f);
        face.AddComponent<Image>().color = hintColor;

        keyText = NewText("Label", face.transform, promptKeyFontSize, panelColor);
        Stretch((RectTransform)keyText.transform, 0f);
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.textWrappingMode = TextWrappingModes.NoWrap;

        promptLabel = NewText("Label", panel.transform, promptFontSize, textColor);
        promptLabelRect = (RectTransform)promptLabel.transform;
        promptLabelRect.anchorMin = promptLabelRect.anchorMax = promptLabelRect.pivot = new Vector2(0f, 0.5f);
        promptLabel.alignment = TextAlignmentOptions.MidlineLeft;
        promptLabel.textWrappingMode = TextWrappingModes.NoWrap;

        promptRoot.SetActive(false);
    }

    /// <summary>
    /// Zerlegt "[E] Lesen" in Taste und Aktion und passt das Schild an. Ohne
    /// [..] vorne bleibt die Kappe weg und nur der Text steht auf der Tafel.
    /// </summary>
    void LayoutPrompt(string text)
    {
        string key = null, action = text ?? "";
        string trimmed = action.TrimStart();
        if (trimmed.StartsWith("["))
        {
            int close = trimmed.IndexOf(']');
            if (close > 1)
            {
                key = trimmed.Substring(1, close - 1).Trim();
                action = trimmed.Substring(close + 1).Trim();
            }
        }

        const float frame = 2f;      // 1 px dunkel + 1 px Rand
        const float padX = 4f, gap = 4f, keyH = 11f;

        bool hasKey = !string.IsNullOrEmpty(key);
        keyCapRect.gameObject.SetActive(hasKey);
        float x = padX;
        if (hasKey)
        {
            keyText.text = key;
            float keyW = Mathf.Max(keyH, Mathf.Ceil(keyText.GetPreferredValues(key).x) + 4f);
            keyCapRect.sizeDelta = new Vector2(keyW, keyH);
            keyCapRect.anchoredPosition = new Vector2(x, 0f);
            keyFaceRect.sizeDelta = new Vector2(0f, keyH - 2f);
            x += keyW + gap;
        }

        promptLabel.text = action;
        float labelW = Mathf.Ceil(promptLabel.GetPreferredValues(action).x);
        promptLabelRect.sizeDelta = new Vector2(labelW + 2f, keyH + 4f);
        promptLabelRect.anchoredPosition = new Vector2(x, 0f);

        // Gerade Breite: das Schild steht mittig, sonst saesse es auf halben Pixeln.
        float w = 2f * frame + x + labelW + padX;
        w = Mathf.Ceil(w / 2f) * 2f;
        promptRect.sizeDelta = new Vector2(w, 2f * frame + keyH + 6f);
    }

    /// <summary>Kurz hochploppen beim Erscheinen, danach drueckt die Kappe ab und zu.</summary>
    void AnimatePrompt()
    {
        float t = Time.unscaledTime - promptShownAt;
        float rise = Mathf.Clamp01(t / 0.14f);
        rise = 1f - (1f - rise) * (1f - rise);
        float y = promptY - Mathf.Round((1f - rise) * 6f);
        promptRect.anchoredPosition = new Vector2(0f, y);

        bool pressed = t > 0.3f && Mathf.Repeat(t - 0.3f, 1.4f) < 0.16f;
        keyFaceRect.anchoredPosition = new Vector2(0f, pressed ? -1f : 0f);
    }

    /// <summary>
    /// Das Geldschild in der oberen rechten Ecke. Das ist ein einziges Bild -
    /// Beutel, Rahmen und die Muenze sind schon hineingemalt, daneben steht
    /// nichts mehr. Dieses Skript legt nur die Zahl in das helle Feld.
    ///
    /// Der Kasten fuer die Zahl steht in Pixeln von geld.png im Inspector und
    /// wird hier in Anker umgerechnet. Dadurch wandert die Zahl automatisch
    /// mit, wenn das Schild ueber <see cref="moneyScale"/> groesser wird.
    /// </summary>
    void BuildCoins(Transform parent)
    {
        coinRoot = NewRect("Money", parent);

        // Eigene Sortierung ueber dem Pausenmenue (200): dessen Abdunklung legt
        // sich sonst auch ueber den Kontostand, und der soll dort stehenbleiben.
        // Alle anderen Hub-Fenster blenden die Ecke ohnehin aus (siehe
        // LateUpdate), es kommt also nichts anderes darueber.
        var coinCanvas = coinRoot.AddComponent<Canvas>();
        coinCanvas.overrideSorting = true;
        coinCanvas.sortingOrder = 205;

        var cRect = (RectTransform)coinRoot.transform;
        cRect.anchorMin = cRect.anchorMax = cRect.pivot = new Vector2(1f, 1f);
        cRect.sizeDelta = moneySize * Mathf.Max(0.01f, moneyScale);
        cRect.anchoredPosition = new Vector2(-coinMargin.x, -coinMargin.y);

        var plate = coinRoot.AddComponent<Image>();
        plate.sprite = moneySprite;
        plate.enabled = moneySprite != null;
        plate.raycastTarget = false;
        // Das Bild fuellt den Kasten genau aus. Nicht das Seitenverhaeltnis halten:
        // sonst sitzt das Schild bei einer krummen moneySize kleiner in der Mitte,
        // waehrend die Zahl weiter am Kasten klebt - und steht dann daneben.
        plate.preserveAspect = false;

        coinValueText = NewText("Value", coinRoot.transform, coinFontSize, coinTextColor);
        PlaceInSprite((RectTransform)coinValueText.transform, moneyTextArea);
        coinValueText.alignment = TextAlignmentOptions.Center;

        // Lieber kleiner werden als ueber die Nieten laufen: 500.000 ist breiter
        // als das Feld, eine zweite Zeile passt in 10 Pixel Hoehe aber nicht.
        coinValueText.textWrappingMode = TextWrappingModes.NoWrap;
        coinValueText.enableAutoSizing = true;
        coinValueText.fontSizeMin = Mathf.Min(coinMinFontSize, coinFontSize);
        coinValueText.fontSizeMax = coinFontSize;

        RefreshCoins();
        coinRoot.SetActive(showCoins);
    }

    /// <summary>
    /// Rechnet einen Kasten in Bildpixeln (Nullpunkt links oben) in Anker um.
    /// Anker sind 0..1 und zaehlen von unten - deshalb das Umdrehen von y.
    /// </summary>
    void PlaceInSprite(RectTransform r, Rect area)
    {
        float w = Mathf.Max(1f, moneySize.x);
        float h = Mathf.Max(1f, moneySize.y);

        r.anchorMin = new Vector2(area.x / w, 1f - (area.y + area.height) / h);
        r.anchorMax = new Vector2((area.x + area.width) / w, 1f - area.y / h);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    GameObject NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
        go.transform.SetParent(parent, false);
        return go;
    }

    TextMeshProUGUI NewText(string n, Transform parent, float size, Color color)
    {
        var go = NewRect(n, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    // -------------------------------------------------------------- Laufzeit

    /// <summary>Jeden Frame aus Update() rufen, solange der Hinweis stehen soll.</summary>
    public void RequestPrompt(string text)
    {
        promptRequestedThisFrame = true;
        if (text == promptShownText) return;
        promptShownText = text;
        promptDirty = true;
    }

    /// <summary>
    /// Oeffnet die Textbox. Seiten duerfen &lt;wave&gt;Wort&lt;/wave&gt; enthalten.
    /// Sprecher und Portrait sind optional: ohne Namen kein Namensschild, ohne
    /// Portrait nutzt der Text die volle Breite. Mit Sprech-Portrait wechselt
    /// beim Tippen der Mund.
    /// </summary>
    public void ShowDialogue(string[] newPages, string speaker = null,
                             Sprite portrait = null, Sprite portraitTalking = null)
    {
        if (newPages == null || newPages.Length == 0) return;

        pages = newPages;
        pageIndex = 0;
        DialogueOpen = true;
        promptRoot.SetActive(false);
        dialogueRoot.SetActive(true);
        SetSpeaker(speaker, portrait, portraitTalking);
        ShowPage();
        FreezePlayer(true);

        // Auch die erste Seite klingt - sonst kommt der Ton erst ab dem zweiten Blaettern
        PlaySfx(pageTurnClip);
    }

    void SetSpeaker(string speaker, Sprite portrait, Sprite portraitTalking)
    {
        bool hasName = !string.IsNullOrWhiteSpace(speaker);
        nameTagRect.gameObject.SetActive(hasName);
        if (hasName)
        {
            nameText.text = speaker;
            float w = Mathf.Ceil(nameText.GetPreferredValues(speaker).x) + 8f + 2f * borderWidth;
            nameTagRect.sizeDelta = new Vector2(w, hintFontSize + 5f + 2f * borderWidth);
            // Reiter sitzt auf der Oberkante, die Rahmen ueberlappen sich
            nameTagRect.anchoredPosition = new Vector2(padding, boxHeight - borderWidth);
        }

        portraitIdle = portrait;
        portraitTalk = portraitTalking != null ? portraitTalking : portrait;
        bool hasPortrait = portrait != null;
        portraitRect.gameObject.SetActive(hasPortrait);
        if (hasPortrait) portraitImage.sprite = portrait;

        float left = hasPortrait ? padding + portraitSize + 6f : padding;
        bodyRect.offsetMin = new Vector2(left, bodyRect.offsetMin.y);
    }

    void ShowPage()
    {
        typer.Play(pages[pageIndex]);
        bool last = pageIndex >= pages.Length - 1;
        string action = last ? Loc.Get("ui.dialogue.close", "Klick zum Schließen")
                             : Loc.Get("ui.dialogue.next", "Klick für weiter");
        hintText.text = (pageIndex + 1) + "/" + pages.Length + "   " + action;
    }

    void CloseDialogue()
    {
        DialogueOpen = false;
        dialogueRoot.SetActive(false);
        pages = null;
        FreezePlayer(false);
    }

    void Update()
    {
        // Escape im Hub oeffnet das Pausenmenue. InputBlocked deckt beide Faelle
        // ab, in denen es nicht darf: ein anderes Fenster ist offen (die
        // schliessen sich mit Escape selbst) oder die Textbox laeuft - und
        // solange das Menue steht, zaehlt es als Modal und sperrt sich hier
        // selbst aus.
        if (!InputBlocked && Time.frameCount != lastModalClosedFrame
            && Input.GetKeyDown(KeyCode.Escape))
            PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Hub);

        if (!DialogueOpen) return;

        // Hinweis blinken lassen
        float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
        Color c = hintColor;
        c.a *= a;
        hintText.color = c;

        AnimateExtras();

        if (Input.GetMouseButtonDown(0))
        {
            PlaySfx(pageTurnClip);

            // Erster Klick waehrend des Tippens zeigt die Seite komplett,
            // erst der naechste blaettert weiter.
            if (!typer.IsDone)
            {
                typer.Complete();
                return;
            }

            pageIndex++;
            if (pageIndex >= pages.Length) CloseDialogue();
            else ShowPage();
        }
    }

    void AnimateExtras()
    {
        float t = Time.unscaledTime;

        // Weiter-Pfeil: erst wenn alles dasteht, dann wippt er
        bool done = typer.IsDone;
        if (arrowRect.gameObject.activeSelf != done) arrowRect.gameObject.SetActive(done);
        if (done)
        {
            float bob = Mathf.Round(Mathf.Sin(t * 6f) * arrowBob);
            arrowRect.anchoredPosition = new Vector2(-padding + 1f, padding * 0.4f + 2f + bob);
        }

        // Portrait: beim Tippen Mund auf/zu und leichtes Wippen
        if (portraitRect.gameObject.activeSelf)
        {
            bool talking = typer.IsTyping;
            bool mouthOpen = talking && ((int)(t * mouthFlapRate) & 1) == 1;
            portraitImage.sprite = mouthOpen ? portraitTalk : portraitIdle;
            float bob = talking ? Mathf.Round(Mathf.Abs(Mathf.Sin(t * 12f)) * portraitBob) : 0f;
            portraitRect.anchoredPosition = new Vector2(padding, -padding + bob);
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        // PlayOneShot statt Play: ueberlappt sauber, wenn schnell geklickt wird
        sfxSource.pitch = 1f + Random.Range(-sfxPitchJitter, sfxPitchJitter);
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    void LateUpdate()
    {
        // Immediate Mode: wer den Hinweis will, meldet sich jeden Frame.
        // Meldet sich niemand, verschwindet er von selbst - so streiten sich
        // mehrere Objekte nicht darum, wer ihn wieder ausschaltet.
        // "Tipps" in den Optionen schaltet ihn ganz ab.
        bool promptWanted = promptRequestedThisFrame && GameSettings.Tips;
        if (promptRoot != null && promptRoot.activeSelf != promptWanted)
        {
            promptRoot.SetActive(promptWanted);
            if (promptWanted) promptShownAt = Time.unscaledTime;
        }
        if (promptWanted)
        {
            // Erst messen, wenn das Schild aktiv ist - TMP rechnet sonst u.U. mit 0.
            if (promptDirty) { promptDirty = false; LayoutPrompt(promptShownText); }
            AnimatePrompt();
        }
        promptRequestedThisFrame = false;

        // Shop, Skilltree, Konsole und die Textbox bringen ihre eigene Anzeige
        // mit oder wollen das Bild fuer sich - solange tritt die Ecke zurueck.
        // Das Pausenmenue ist die Ausnahme: seine Tafel steht mittig, die Ecke
        // bleibt frei und der Kontostand darf stehenbleiben.
        bool coinsWanted = showCoins && (!InputBlocked || PauseMenuPanel.IsOpen);
        if (coinRoot != null && coinRoot.activeSelf != coinsWanted)
            coinRoot.SetActive(coinsWanted);
    }

    void FreezePlayer(bool freeze)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        if (freeze)
        {
            playerController = p.GetComponent<SimplePlayerController>();

            // Erst die Animator-Parameter neutralisieren, dann das Steuerskript
            // abschalten - sonst friert der Keks mitten im Laufzyklus ein und
            // tritt auf der Stelle weiter.
            var anim = p.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat("MoveX", 0f);
                anim.SetFloat("MoveY", 0f);
                anim.SetBool("moving", false);
            }

            var rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            if (playerController != null) playerController.enabled = false;
        }
        else
        {
            if (playerController != null) playerController.enabled = true;
            playerController = null;
        }
    }
}
