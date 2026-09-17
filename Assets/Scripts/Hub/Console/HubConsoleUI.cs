using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Das Modal der Hub-Konsole: Rahmen, Ausgabebereich, Eingabezeile.
///
/// Baut sich wie das HubUI zur Laufzeit selbst auf - kein Prefab, das gepflegt
/// werden muss. Das Aussehen ist bewusst ein Platzhalter: alle Farben, Groessen
/// und optional zwei Sprites haengen im Inspector, das richtige Design kommt
/// spaeter einfach da rein.
///
/// Was getippt wird, landet bei <see cref="HubConsole"/> und damit in einer
/// festen Befehlstabelle. Kein eval, kein Dateizugriff - hier kann nichts
/// kaputtgehen.
/// </summary>
[DisallowMultipleComponent]
public class HubConsoleUI : MonoBehaviour, IHubConsoleSink
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Font")]
    [Tooltip("PixelArtFont. Leer = TMP-Standardfont.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Platzhalter-Design (Referenz 320 x 180)")]
    [Tooltip("Das ist nur ein Geruest. Farben und Groessen hier umstellen, " +
             "oder unten Sprites eintragen - dann gewinnen die.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [SerializeField] private Vector2 panelSize = new Vector2(268f, 150f);
    [SerializeField] private float borderWidth = 2f;
    [SerializeField] private float padding = 7f;
    [SerializeField] private float titleFontSize = 8f;
    [SerializeField] private float logFontSize = 7f;
    [SerializeField] private float inputFontSize = 7f;
    [SerializeField] private float lineSpacing = 4f;

    [Header("Farben")]
    [SerializeField] private Color dimColor = new Color32(0x08, 0x05, 0x04, 0xC8);
    [SerializeField] private Color borderColor = new Color32(0xF0, 0xD4, 0x9B, 0xFF);
    [SerializeField] private Color panelColor = new Color32(0x14, 0x12, 0x10, 0xFC);
    [SerializeField] private Color titleColor = new Color32(0xD9, 0xA4, 0x41, 0xFF);
    [SerializeField] private Color textColor = new Color32(0x9C, 0xE0, 0x8A, 0xFF);
    [SerializeField] private Color echoColor = new Color32(0xF7, 0xEC, 0xD6, 0xFF);
    [SerializeField] private Color errorColor = new Color32(0xE0, 0x6C, 0x5C, 0xFF);

    [Header("Grafik statt Farbflaeche (optional)")]
    [Tooltip("Leer = einfarbiger Rahmen. Wenn gesetzt, wird das Sprite 9-Slice gezogen.")]
    [SerializeField] private Sprite frameSprite;
    [Tooltip("Leer = einfarbige Flaeche hinter dem Text.")]
    [SerializeField] private Sprite panelSprite;

    [Header("Inhalt")]
    [SerializeField] private string titleText = "TERMINAL";
    [Tooltip("Steht beim Oeffnen da. Eine Zeile pro Eintrag.")]
    [SerializeField]
    private string[] greeting =
    {
        "RILB-OS v0.1 - nicht fuer den Produktivbetrieb",
        "'hilfe' listet die harmlosen Befehle.",
        "Alles andere musst du selbst herausfinden.",
        "",
    };
    [Tooltip("Zeichen links vor der Eingabezeile.")]
    [SerializeField] private string promptSymbol = ">";
    [Tooltip("Aeltere Zeilen fallen oben raus, damit der Text nicht endlos waechst.")]
    [SerializeField] private int maxLogLines = 120;
    [Tooltip("So viele Eingaben merkt sich Pfeil-hoch.")]
    [SerializeField] private int maxHistory = 30;

    [Header("Sound")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [Tooltip("Spielt bei Enter, nicht bei jedem Buchstaben.")]
    [SerializeField] private AudioClip submitClip;
    [SerializeField] private AudioClip errorClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    GameObject root;
    ScrollRect scroll;
    TextMeshProUGUI logText;
    TMP_InputField inputField;

    readonly List<string> logLines = new List<string>();
    readonly List<string> history = new List<string>();
    int historyCursor = -1;      // -1 = nichts ausgewaehlt, tippt gerade frisch
    bool focusArmed;             // erst nach dem ersten Frame Fokus nachziehen
    bool built;

    string textHex, echoHex, errorHex;

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
        // Szenenwechsel mit offenem Fenster: die Sperre wieder abmelden,
        // sonst reagiert der Hub beim naechsten Mal auf gar nichts mehr.
        if (!IsOpen) return;
        IsOpen = false;
        HubUI.PopModal();
    }

    void Build()
    {
        if (built) return;
        built = true;

        textHex  = "#" + ColorUtility.ToHtmlStringRGB(textColor);
        echoHex  = "#" + ColorUtility.ToHtmlStringRGB(echoColor);
        errorHex = "#" + ColorUtility.ToHtmlStringRGB(errorColor);

        var canvasGO = new GameObject("ConsoleCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        SetUiLayer(canvasGO);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // ueber der Textbox des HubUI (100), damit nichts durchscheint
        canvas.sortingOrder = 120;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root = NewRect("Console", canvasGO.transform);
        Stretch((RectTransform)root.transform, 0f);

        // Abdunklung - faengt gleichzeitig Klicks ab, die sonst im Hub landen
        var dim = NewRect("Dim", root.transform);
        Stretch((RectTransform)dim.transform, 0f);
        dim.AddComponent<Image>().color = dimColor;

        // ---- Rahmen -----------------------------------------------------
        var frame = NewRect("Frame", root.transform);
        var fRect = (RectTransform)frame.transform;
        fRect.anchorMin = fRect.anchorMax = fRect.pivot = new Vector2(0.5f, 0.5f);
        fRect.sizeDelta = panelSize;
        fRect.anchoredPosition = Vector2.zero;
        Paint(frame.AddComponent<Image>(), frameSprite, borderColor);

        var panel = NewRect("Panel", frame.transform);
        Stretch((RectTransform)panel.transform, borderWidth);
        Paint(panel.AddComponent<Image>(), panelSprite, panelColor);

        // ---- Titelzeile --------------------------------------------------
        var title = NewText("Title", panel.transform, titleFontSize, titleColor);
        var tRect = (RectTransform)title.transform;
        tRect.anchorMin = new Vector2(0f, 1f);
        tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot     = new Vector2(0.5f, 1f);
        tRect.offsetMin = new Vector2(padding, 0f);
        tRect.offsetMax = new Vector2(-padding, 0f);
        tRect.sizeDelta = new Vector2(tRect.sizeDelta.x, titleFontSize + 3f);
        tRect.anchoredPosition = new Vector2(0f, -padding * 0.6f);
        title.alignment = TextAlignmentOptions.TopLeft;
        title.text = titleText;

        float titleBlock = padding * 0.6f + titleFontSize + 3f;

        var rule = NewRect("Rule", panel.transform);
        var rRect = (RectTransform)rule.transform;
        rRect.anchorMin = new Vector2(0f, 1f);
        rRect.anchorMax = new Vector2(1f, 1f);
        rRect.pivot     = new Vector2(0.5f, 1f);
        rRect.offsetMin = new Vector2(padding, 0f);
        rRect.offsetMax = new Vector2(-padding, 0f);
        rRect.sizeDelta = new Vector2(rRect.sizeDelta.x, 1f);
        rRect.anchoredPosition = new Vector2(0f, -titleBlock);
        var ruleImage = rule.AddComponent<Image>();
        ruleImage.color = new Color(titleColor.r, titleColor.g, titleColor.b, titleColor.a * 0.45f);
        ruleImage.raycastTarget = false;

        float inputHeight = inputFontSize + 5f;

        // ---- Ausgabe (scrollbar) ----------------------------------------
        var logArea = NewRect("Log", panel.transform);
        var lRect = (RectTransform)logArea.transform;
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = Vector2.one;
        lRect.offsetMin = new Vector2(padding, padding + inputHeight + 2f);
        lRect.offsetMax = new Vector2(-padding, -(titleBlock + 3f));

        var viewport = NewRect("Viewport", logArea.transform);
        var vRect = (RectTransform)viewport.transform;
        Stretch(vRect, 0f);
        viewport.AddComponent<RectMask2D>();

        // Unsichtbare Flaeche, aber ein Raycast-Ziel - ohne die kommt das
        // Mausrad nie beim ScrollRect an.
        var catcher = viewport.AddComponent<Image>();
        catcher.color = Color.clear;
        catcher.raycastTarget = true;

        logText = NewText("Content", viewport.transform, logFontSize, textColor);
        var cRect = (RectTransform)logText.transform;
        cRect.anchorMin = new Vector2(0f, 1f);
        cRect.anchorMax = new Vector2(1f, 1f);
        cRect.pivot     = new Vector2(0.5f, 1f);
        cRect.offsetMin = new Vector2(0f, 0f);
        cRect.offsetMax = new Vector2(0f, 0f);
        cRect.anchoredPosition = Vector2.zero;
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.lineSpacing = lineSpacing;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.richText = true;

        // Hoehe kommt vom Text, die Breite vom Viewport - so scrollt der Inhalt
        var fitter = logText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        scroll = logArea.AddComponent<ScrollRect>();
        scroll.viewport = vRect;
        scroll.content = cRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = logFontSize + lineSpacing;

        // ---- Eingabezeile ------------------------------------------------
        var inputRow = NewRect("InputRow", panel.transform);
        var iRect = (RectTransform)inputRow.transform;
        iRect.anchorMin = new Vector2(0f, 0f);
        iRect.anchorMax = new Vector2(1f, 0f);
        iRect.pivot     = new Vector2(0.5f, 0f);
        iRect.offsetMin = new Vector2(padding, padding);
        iRect.offsetMax = new Vector2(-padding, padding + inputHeight);

        float symbolWidth = inputFontSize * 0.9f + 2f;

        var symbol = NewText("Symbol", inputRow.transform, inputFontSize, titleColor);
        var sRect = (RectTransform)symbol.transform;
        sRect.anchorMin = new Vector2(0f, 0f);
        sRect.anchorMax = new Vector2(0f, 1f);
        sRect.pivot     = new Vector2(0f, 0.5f);
        sRect.sizeDelta = new Vector2(symbolWidth, 0f);
        sRect.anchoredPosition = Vector2.zero;
        symbol.alignment = TextAlignmentOptions.Left;
        symbol.text = promptSymbol;

        var fieldGO = NewRect("Field", inputRow.transform);
        var fieldRect = (RectTransform)fieldGO.transform;
        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = new Vector2(symbolWidth, 0f);
        fieldRect.offsetMax = Vector2.zero;

        var textArea = NewRect("TextArea", fieldGO.transform);
        var taRect = (RectTransform)textArea.transform;
        Stretch(taRect, 0f);
        textArea.AddComponent<RectMask2D>();

        var fieldText = NewText("Text", textArea.transform, inputFontSize, echoColor);
        Stretch((RectTransform)fieldText.transform, 0f);
        fieldText.alignment = TextAlignmentOptions.Left;
        // Rohtext: was getippt wird, soll nie als TMP-Tag gelesen werden
        fieldText.richText = false;

        inputField = fieldGO.AddComponent<TMP_InputField>();
        inputField.textViewport = taRect;
        inputField.textComponent = fieldText;
        if (font != null) inputField.fontAsset = font;
        inputField.pointSize = inputFontSize;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = HubConsole.MaxInputLength;
        inputField.onFocusSelectAll = false;
        inputField.restoreOriginalTextOnEscape = false;
        inputField.richText = false;
        inputField.customCaretColor = true;
        inputField.caretColor = titleColor;
        inputField.caretWidth = 1;
        inputField.caretBlinkRate = 1.5f;
        inputField.selectionColor = new Color(titleColor.r, titleColor.g, titleColor.b, 0.35f);
        inputField.text = string.Empty;

        // Tastaturfilter: Zeichen ausserhalb der Positivliste kommen gar nicht
        // erst ins Feld. Damit sind auch TMP-Tags von vornherein vom Tisch.
        inputField.onValidateInput = ValidateChar;
        inputField.onSubmit.AddListener(OnSubmit);

        root.SetActive(false);
    }

    static char ValidateChar(string text, int charIndex, char addedChar) =>
        HubConsole.IsAllowedChar(addedChar) ? addedChar : '\0';

    void Paint(Image image, Sprite sprite, Color color)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = color;
        }
    }

    static void SetUiLayer(GameObject go)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
    }

    GameObject NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        SetUiLayer(go);
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

    // -------------------------------------------------------------- Oeffnen

    public void Open()
    {
        if (IsOpen) return;

        Build();
        HubUiKit.EnsureEventSystem();
        HubConsole.EnsureDefaults();

        IsOpen = true;
        root.SetActive(true);

        logLines.Clear();
        if (greeting != null)
            foreach (string line in greeting) Print(line);
        RedrawLog();

        historyCursor = -1;
        inputField.SetTextWithoutNotify(string.Empty);

        HubUI.PushModal();
        HubUI.Instance.SetPlayerFrozen(true);

        PlaySfx(openClip);

        focusArmed = false;
        StartCoroutine(TakeFocus());
    }

    /// <summary>
    /// Fokus erst im naechsten Frame holen und danach leeren: sonst landet das
    /// [E], mit dem man das Terminal aufgemacht hat, gleich als Buchstabe in
    /// der Eingabezeile.
    /// </summary>
    IEnumerator TakeFocus()
    {
        yield return null;
        if (!IsOpen) yield break;

        inputField.ActivateInputField();

        yield return null;
        if (!IsOpen) yield break;

        inputField.SetTextWithoutNotify(string.Empty);
        inputField.caretPosition = 0;
        focusArmed = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        focusArmed = false;

        inputField.DeactivateInputField();
        inputField.SetTextWithoutNotify(string.Empty);
        root.SetActive(false);

        HubUI.PopModal();
        HubUI.Instance.SetPlayerFrozen(false);

        PlaySfx(closeClip);
    }

    // ------------------------------------------------------------- Laufzeit

    void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))   StepHistory(+1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) StepHistory(-1);

        // Wer danebenklickt, soll nicht ins Leere tippen
        if (focusArmed && !inputField.isFocused)
        {
            inputField.ActivateInputField();
            inputField.caretPosition = inputField.text.Length;
        }
    }

    void OnSubmit(string value)
    {
        if (!IsOpen) return;

        string line = HubConsole.Sanitize(value).Trim();

        inputField.SetTextWithoutNotify(string.Empty);
        inputField.caretPosition = 0;
        historyCursor = -1;

        // Fokus geht beim Submit verloren - sofort zurueckholen
        inputField.ActivateInputField();

        if (line.Length == 0) return;

        PlaySfx(submitClip);
        Remember(line);

        Write(line, echoHex, promptSymbol + " ");
        HubConsole.Execute(line, this);
        RedrawLog();
    }

    void Remember(string line)
    {
        // Gleiche Zeile zweimal hintereinander muellt die Historie nur zu
        if (history.Count > 0 && history[history.Count - 1] == line) return;

        history.Add(line);
        if (history.Count > maxHistory) history.RemoveAt(0);
    }

    /// <summary>+1 = weiter zurueck, -1 = wieder nach vorn.</summary>
    void StepHistory(int direction)
    {
        if (history.Count == 0) return;

        historyCursor = Mathf.Clamp(historyCursor + direction, -1, history.Count - 1);

        string line = historyCursor < 0
            ? string.Empty
            : history[history.Count - 1 - historyCursor];

        inputField.SetTextWithoutNotify(line);
        inputField.caretPosition = line.Length;
        inputField.selectionAnchorPosition = line.Length;
        inputField.selectionFocusPosition = line.Length;
    }

    // ----------------------------------------------------- IHubConsoleSink

    public void Print(string line) => WriteAndRedraw(line, textHex, null);

    public void PrintError(string line)
    {
        PlaySfx(errorClip);
        WriteAndRedraw(line, errorHex, null);
    }

    public void Clear()
    {
        logLines.Clear();
        RedrawLog();
    }

    void WriteAndRedraw(string line, string hex, string prefix)
    {
        Write(line, hex, prefix);
        RedrawLog();
    }

    void Write(string line, string hex, string prefix)
    {
        if (line == null) line = string.Empty;
        if (!string.IsNullOrEmpty(prefix)) line = prefix + line;

        logLines.Add("<color=" + hex + ">" + line + "</color>");

        int overflow = logLines.Count - maxLogLines;
        if (overflow > 0) logLines.RemoveRange(0, overflow);
    }

    void RedrawLog()
    {
        if (logText == null) return;

        logText.text = string.Join("\n", logLines);

        // Erst das Layout neu rechnen lassen, sonst springt der Scrollbalken
        // auf die Hoehe von vorhin.
        Canvas.ForceUpdateCanvases();
        if (scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
