using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Baukasten fuer die Fenster im Stil von "UI 2.0" (Level-Up, Evo-Buch):
/// <see cref="OptionsPanel"/> und <see cref="FeedbackPanel"/>.
///
/// Alles liegt auf einer 480x270-Seite, Ursprung oben links, jede Zahl ein
/// Pixel. Die Seite wird ganzzahlig skaliert und auf ganze Bildpunkte
/// gesetzt - nie krumm, sonst werden Pixelkanten ungleich dick (siehe
/// Notiz "Pixel-UI auf ganzen Pixeln"). Grafik kommt aus
/// <see cref="GameHudSkin"/>, Bilddateien gibt es keine.
/// </summary>
public static class OptionsKit
{
    public const int RefW = 480, RefH = 270;

    public const float SizeText = 10f;
    public const float SizeTitle = 20f;

    // ==================================================================
    //  Leinwand
    // ==================================================================

    /// <summary>
    /// Canvas + Scaler + Raycaster auf <paramref name="go"/>, dazu die
    /// Abdunklung und die Seite. Die Seite muss danach per
    /// <see cref="Layout"/> gesetzt werden (und bei jeder Groessenaenderung).
    /// </summary>
    public static RectTransform CreatePage(GameObject go, int sortingOrder, float dim, out CanvasScaler scaler)
    {
        // Darf mehrfach laufen (Neuaufbau nach Sprachwechsel): Canvas & Co.
        // bleiben, nur Abdunklung und Seite kommen neu.
        Canvas canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = sortingOrder;

        scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Abdunklung faengt alle Klicks ab, damit nichts darunter reagiert.
        // Im linearen Farbraum wirkt Alpha schwaecher als eingestellt.
        Image back = Stretch("Dim", go.transform, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, dim));
        back.raycastTarget = true;

        RectTransform page = Rect("Page", go.transform, 0, 0, RefW, RefH);
        return page;
    }

    /// <summary>Alle Kinder weg - erst abhaengen, dann zerstoeren (Destroy wirkt erst am Frame-Ende).</summary>
    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            GameObject old = t.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Object.Destroy(old);
        }
    }

    /// <summary>
    /// Groesste ganze Stufe, bei der der bemalte Bereich
    /// (<paramref name="content"/>, in Seitenpixeln) noch ganz auf den
    /// Schirm passt - aber nicht groesser, als die 480x270-Seite es waere.
    /// Die Mitte des Bereichs landet auf ganzen Pixeln in der Bildschirmmitte.
    /// </summary>
    public static void Layout(RectTransform page, CanvasScaler scaler, RectInt content)
    {
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);

        int byPage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w / (float)RefW, h / (float)RefH)));
        int byContent = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(w / (float)content.width, h / (float)content.height)));
        int s = Mathf.Min(byPage, byContent);
        scaler.scaleFactor = s;

        int x = (w / s) / 2 - (content.x + content.width / 2);
        int y = (h / s) / 2 - (content.y + content.height / 2);
        page.anchoredPosition = new Vector2(x, -y);
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    /// <summary>Rechteck in Seitenpixeln, x/y von oben links.</summary>
    public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(Mathf.Round(w), Mathf.Round(h));
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        return rt;
    }

    public static void Move(RectTransform rt, float x, float y, float w, float h)
    {
        rt.sizeDelta = new Vector2(Mathf.Round(w), Mathf.Round(h));
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
    }

    public static Image Img(string name, Transform parent, float x, float y, float w, float h,
                            Sprite sprite, bool sliced = false)
    {
        return Img(name, parent, x, y, w, h, sprite, Color.white, sliced);
    }

    public static Image Img(string name, Transform parent, float x, float y, float w, float h,
                            Sprite sprite, Color color, bool sliced = false)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    public static Image Stretch(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Text ohne Umbruch und ohne Auto-Size. Die Hoehe muss groesser sein
    /// als die Schrift, sonst wirft TMP die Zeile still weg.
    /// </summary>
    public static TextMeshProUGUI Label(string name, Transform parent, float x, float y, float w, float h,
                                        string text, TMP_FontAsset font, float size, Color color,
                                        TextAlignmentOptions align)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = false;
        t.enableAutoSizing = false;
        t.margin = Vector4.zero;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Text mit 1-px-Schatten in Ink darunter - fuer Titel auf Band und Holz.</summary>
    public static TextMeshProUGUI ShadowLabel(string name, Transform parent, float x, float y, float w, float h,
                                              string text, TMP_FontAsset font, float size, Color color,
                                              TextAlignmentOptions align)
    {
        Label(name + "Shadow", parent, x, y + 1, w, h, text, font, size, GameHudSkin.Ink, align);
        return Label(name, parent, x, y, w, h, text, font, size, color, align);
    }

    /// <summary>
    /// Die Titelschrift (ThaleahFat), solange sie jedes Zeichen kennt - sonst
    /// Jersey10. ThaleahFat hat keine Umlaute.
    /// </summary>
    public static TMP_FontAsset PickFont(string text, TMP_FontAsset pixel, TMP_FontAsset fallback)
    {
        if (pixel == null) return fallback;
        if (fallback == null || string.IsNullOrEmpty(text)) return pixel;
        return pixel.HasCharacters(text, out uint[] _, false, true) ? pixel : fallback;
    }

    /// <summary>Breite eines Textes in Seitenpixeln.</summary>
    public static float Measure(TMP_Text probe, string text)
    {
        float width = probe.GetPreferredValues(text).x;
        return width > 0f ? width : text.Length * probe.fontSize * 0.5f;
    }

    /// <summary>
    /// Titelband wie beim Level-Up: rotes Band mit Schwalbenschwaenzen,
    /// mittig ueber <paramref name="centerX"/>; die Breite waechst mit dem Titel.
    /// </summary>
    public static void Ribbon(Transform parent, float centerX, float y, string text,
                              TMP_FontAsset pixel, TMP_FontAsset fallback)
    {
        TMP_FontAsset font = PickFont(text, pixel, fallback);

        // Erst messen, dann bauen: das Band waechst mit dem Titel.
        TextMeshProUGUI probe = Label("Probe", parent, 0, 0, 400, 30, text, font, SizeTitle, Color.clear,
                                      TextAlignmentOptions.Left);
        float textW = Mathf.Ceil(Measure(probe, text));
        Object.Destroy(probe.gameObject);

        float w = Mathf.Max(90f, textW + 32f);
        if (((int)w & 1) == 1) w += 1f;
        float x = Mathf.Round(centerX - w / 2f);
        const float h = 22f;

        Img("TailL", parent, x - 10, y + 4, 12, 16, GameHudSkin.RibbonTail(true));
        Img("TailR", parent, x + w - 2, y + 4, 12, 16, GameHudSkin.RibbonTail(false));
        Img("Ribbon", parent, x, y, w, h, GameHudSkin.Ribbon, true);
        ShadowLabel("Title", parent, x, y + 1, w, h - 2, text, font, SizeTitle, GameHudSkin.Cream,
                    TextAlignmentOptions.Center);
    }

    // ==================================================================
    //  Eingabefeld
    // ==================================================================

    /// <summary>
    /// TMP-Eingabefeld auf dem <see cref="GameHudSkin.Field"/>-Rahmen. Wird
    /// inaktiv zusammengesteckt und erst am Ende eingeschaltet - sonst laeuft
    /// OnEnable des Feldes ohne Textkomponente.
    /// </summary>
    public static TMP_InputField Input(Transform parent, float x, float y, float w, float h,
                                       TMP_FontAsset font, string placeholder, bool multiline, int limit)
    {
        RectTransform rt = Rect("Input", parent, x, y, w, h);
        rt.gameObject.SetActive(false);

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = GameHudSkin.Field(false);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;

        RectTransform area = Rect("TextArea", rt, 5, multiline ? 3 : 2, w - 10, h - (multiline ? 5 : 3));
        area.gameObject.AddComponent<RectMask2D>();

        TextAlignmentOptions align = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
        TextMeshProUGUI hint = Label("Placeholder", area, 0, 0, w - 10, h - 5, placeholder, font, SizeText,
                                     WithAlpha(GameHudSkin.Stone, 1f), align);
        TextMeshProUGUI text = Label("Text", area, 0, 0, w - 10, h - 5, "", font, SizeText,
                                     GameHudSkin.Cream, align);
        StretchIn(hint.rectTransform);
        StretchIn(text.rectTransform);
        if (multiline)
        {
            hint.textWrappingMode = TextWrappingModes.Normal;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        TMP_InputField field = rt.gameObject.AddComponent<TMP_InputField>();
        field.transition = Selectable.Transition.None;
        field.targetGraphic = bg;
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = hint;
        field.fontAsset = font;
        field.pointSize = SizeText;
        field.richText = false;
        field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        field.characterLimit = limit;
        field.customCaretColor = true;
        field.caretColor = GameHudSkin.Gold;
        field.caretWidth = 1;
        field.caretBlinkRate = 1.2f;
        field.selectionColor = WithAlpha(GameHudSkin.Gold, 0.35f);
        field.onFocusSelectAll = false;
        field.resetOnDeActivation = false;
        // ESC schliesst das Fenster - der Text soll dabei nicht zurueckspringen.
        field.restoreOriginalTextOnEscape = false;

        rt.gameObject.SetActive(true);
        return field;
    }

    private static void StretchIn(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Rahmen gold, solange das Feld den Fokus hat. Jeden Frame aufrufen.</summary>
    public static void UpdateFocus(TMP_InputField field)
    {
        if (field == null || field.targetGraphic == null) return;
        Sprite want = GameHudSkin.Field(field.isFocused);
        Image bg = (Image)field.targetGraphic;
        if (bg.sprite != want) bg.sprite = want;
    }

    // ==================================================================
    //  Kleinkram
    // ==================================================================

    public static Color WithAlpha(Color32 c, float a)
    {
        Color col = c;
        col.a = a;
        return col;
    }

    public static void PlayClick()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.MenuClick != null) audio.PalySound(audio.MenuClick);
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(es);
    }
}

/// <summary>
/// Holzknopf aus <see cref="GameHudSkin.Button"/>: Hover, Druck (Beschriftung
/// rutscht 1 px nach unten), aktiv (Marmelade) und gesperrt. Optional mit
/// Symbol links vor dem Text.
///
/// Mit <see cref="UseSelection"/> leuchtet nicht der Knopf unter der Maus,
/// sondern der mit <see cref="Selected"/> - so fuehrt ein Menue mit
/// Pfeiltasten die Auswahl, und die Maus meldet sich nur ueber OnHover.
/// </summary>
public class SkinButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public enum Kind { Wood, Primary, Danger }

    private Image bg;
    private TextMeshProUGUI label;
    private Image icon;
    private Vector2 labelHome, iconHome;
    private Kind kind;
    private bool hover, down, active, disabled, selected;

    public System.Action OnClick;

    /// <summary>Maus ist auf den Knopf gekommen.</summary>
    public System.Action OnHover;

    public bool UseSelection;

    public bool Selected
    {
        get => selected;
        set { if (selected != value) { selected = value; Refresh(); } }
    }

    public bool Active
    {
        get => active;
        set { if (active != value) { active = value; Refresh(); } }
    }

    public bool Disabled
    {
        get => disabled;
        set { if (disabled != value) { disabled = value; Refresh(); } }
    }

    public TextMeshProUGUI Label => label;

    public static SkinButton Create(Transform parent, float x, float y, float w, float h, string text,
                                    TMP_FontAsset font, Kind kind, System.Action onClick, Sprite iconSprite = null)
    {
        RectTransform rt = OptionsKit.Rect("Btn_" + text, parent, x, y, w, h);
        SkinButton b = rt.gameObject.AddComponent<SkinButton>();
        b.kind = kind;
        b.OnClick = onClick;

        b.bg = rt.gameObject.AddComponent<Image>();
        b.bg.type = Image.Type.Sliced;
        b.bg.raycastTarget = true;

        // Das Band unten ist 2 px dunkel - die optische Mitte liegt darum 1 px hoeher.
        float textX = 0f, textW = w;
        if (iconSprite != null)
        {
            float iw = iconSprite.rect.width, ih = iconSprite.rect.height;
            float textWidth = Mathf.Ceil(OptionsKit.Measure(
                OptionsKit.Label("Probe", rt, 0, 0, 200, 14, text, font, OptionsKit.SizeText, Color.clear,
                                 TextAlignmentOptions.Left), text));
            Destroy(rt.Find("Probe").gameObject);

            float total = iw + 3f + textWidth;
            float left = Mathf.Round((w - total) / 2f);
            b.icon = OptionsKit.Img("Icon", rt, left, Mathf.Round((h - 1f - ih) / 2f), iw, ih, iconSprite);
            textX = left + iw + 3f;
            textW = textWidth + 2f;
        }

        b.label = OptionsKit.Label("Label", rt, textX, 0, textW, h - 1, text, font, OptionsKit.SizeText,
                                   GameHudSkin.Cream,
                                   iconSprite != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);

        b.labelHome = b.label.rectTransform.anchoredPosition;
        if (b.icon != null) b.iconHome = b.icon.rectTransform.anchoredPosition;
        b.Refresh();
        return b;
    }

    public void SetText(string text)
    {
        if (label != null) label.text = text;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; OnHover?.Invoke(); Refresh(); }
    public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { down = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { down = false; Refresh(); }

    public void OnPointerClick(PointerEventData e)
    {
        if (disabled || e.button != PointerEventData.InputButton.Left) return;
        OptionsKit.PlayClick();
        OnClick?.Invoke();
    }

    private void OnDisable()
    {
        hover = down = false;
        Refresh();
    }

    private void Refresh()
    {
        if (bg == null) return;

        bool lit = UseSelection ? selected : hover;

        GameHudSkin.ButtonLook look;
        if (disabled) look = GameHudSkin.ButtonLook.Disabled;
        else if (active) look = GameHudSkin.ButtonLook.Active;
        else if (down && hover) look = GameHudSkin.ButtonLook.Pressed;
        else if (kind == Kind.Primary) look = lit ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Gold;
        else if (kind == Kind.Danger) look = lit ? GameHudSkin.ButtonLook.DangerHover : GameHudSkin.ButtonLook.Active;
        else look = lit ? GameHudSkin.ButtonLook.Hover : GameHudSkin.ButtonLook.Wood;

        bg.sprite = GameHudSkin.Button(look);

        Color text;
        switch (look)
        {
            case GameHudSkin.ButtonLook.Gold:
            case GameHudSkin.ButtonLook.GoldHover:
            case GameHudSkin.ButtonLook.DangerHover: text = GameHudSkin.Ink; break;
            case GameHudSkin.ButtonLook.Pressed:   text = GameHudSkin.ParchDark; break;
            case GameHudSkin.ButtonLook.Disabled:  text = GameHudSkin.StoneLight; break;
            default:                               text = GameHudSkin.Cream; break;
        }
        if (label != null) label.color = text;

        Vector2 push = look == GameHudSkin.ButtonLook.Pressed ? new Vector2(0f, -1f) : Vector2.zero;
        if (label != null) label.rectTransform.anchoredPosition = labelHome + push;
        if (icon != null) icon.rectTransform.anchoredPosition = iconHome + push;
    }
}

/// <summary>Leitet Zeiger-Ereignisse an Delegaten weiter (Lautstaerke-Zellen).</summary>
public class PointerRelay : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public System.Action Enter, Exit, Down;

    public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) Down?.Invoke();
    }
}
