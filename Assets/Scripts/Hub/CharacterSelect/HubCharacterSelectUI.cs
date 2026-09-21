using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Charakterauswahl im Hub - bewusst schlicht: ein Knopf je Charakter aus
/// <see cref="Characters"/>, mehr nicht. Wer draufklickt, spielt ihn; am Hub
/// selbst aendert sich dadurch nichts.
///
/// Aufbau wie bei der Levelauswahl: der Canvas entsteht zur Laufzeit auf
/// 320x180, alle Kaesten stehen als <see cref="Rect"/> in Pixeln dieser Vorlage
/// mit Nullpunkt links oben, und die Treffer rechnet dieses Skript selbst aus
/// der Mausposition aus - in <see cref="HubLevelSelectUI"/> ist das ausfuehrlich
/// erklaert.
///
/// Die Wahl liegt in Shop.SkinIndex, also im Spielstand. Das Setzen zieht schon
/// von allein den Skilltree des Charakters nach (Shop.SkinIndex ->
/// Skills.SetActiveTreeForCharacter) und ebenso seinen Verteiler
/// (-> Loadout.SyncCharacter). Skilltree-Fenster und Werkbank rufen beim
/// Oeffnen ohnehin noch einmal nach - jeder Charakter ist damit ueber seinen
/// eigenen Baum und seinen eigenen Build erreichbar, ohne dass hier etwas
/// dafuer noetig waere.
///
/// EIN CHARAKTER DAZU: nichts an dieser Datei - nur in Characters.cs eintragen.
/// Die Knoepfe kommen aus Characters.Count.
/// </summary>
[DisallowMultipleComponent]
public class HubCharacterSelectUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Beschriftungen")]
    [SerializeField] private string titleLabel = "CHARAKTER";
    [SerializeField] private string backLabel = "ZURÜCK";
    [Tooltip("Steht unter den Knoepfen. {0} = Name des gewaehlten Charakters.")]
    [SerializeField] private string currentFormat = "Gewählt: {0}";

    [Header("Grafik")]
    [Tooltip("Pixelschrift des Panels - wie bei der Levelauswahl Jersey10, weil die " +
             "anderen Pixelschriften keine Umlaute kennen. Leer = die zuerst gefundene.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Kaesten (Pixel im 320x180-Bild, Nullpunkt links oben)")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(320f, 180f);
    [SerializeField] private Rect bannerArea = new Rect(84f, 14f, 152f, 18f);
    [Tooltip("Der erste Knopf. Die weiteren liegen darunter, je um Hoehe + Abstand versetzt.")]
    [SerializeField] private Rect buttonArea = new Rect(100f, 42f, 120f, 20f);
    [SerializeField] private float buttonGap = 5f;
    [SerializeField] private Rect currentArea = new Rect(84f, 148f, 152f, 10f);
    [SerializeField] private Rect backArea = new Rect(8f, 151f, 62f, 15f);

    [Header("Schriftgroessen")]
    [SerializeField] private float titleFontSize = 8f;
    [SerializeField] private float buttonFontSize = 8f;
    [SerializeField] private float smallFontSize = 7f;

    [Header("Farben")]
    [SerializeField] private Color backdropColor = new Color32(0x21, 0x1A, 0x1C, 0xFF);
    [SerializeField] private Color panelFill = new Color32(0xE8, 0xDE, 0xC2, 0xFF);
    [SerializeField] private Color panelBorder = new Color32(0xC0, 0xAE, 0x8A, 0xFF);
    [SerializeField] private Color panelInk = new Color32(0x33, 0x26, 0x2B, 0xFF);
    [SerializeField] private Color panelInkDim = new Color32(0x6B, 0x51, 0x47, 0xFF);
    [Tooltip("Flaeche des Knopfes, auf dem der gerade gespielte Charakter steht.")]
    [SerializeField] private Color chosenFill = new Color32(0x33, 0x26, 0x2B, 0xFF);
    [Tooltip("Flaeche des Knopfes unter der Maus.")]
    [SerializeField] private Color hoverFill = new Color32(0xF6, 0xEF, 0xDA, 0xFF);

    [Header("Tasten")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode confirmKeyAlt = KeyCode.E;

    [Header("Ton")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip pickClip;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    /// <summary>Ein Knopf - Flaeche, Rahmen und Beschriftung.</summary>
    class Button
    {
        public Image Fill, Frame;
        public TextMeshProUGUI Label;
    }

    readonly List<Button> buttons = new List<Button>();
    readonly HubPixelSprites pixels = new HubPixelSprites();

    GameObject root;
    RectTransform screen;
    Image backFill;
    TextMeshProUGUI backText, currentText;

    int hoverIndex = -1;   // -1 = kein Knopf unter der Maus
    bool hoverBack;
    int cursor;            // Auswahl der Tastatur
    int openedOnFrame = -1;
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

        var canvasGO = new GameObject("CharacterSelectCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) canvasGO.layer = uiLayer;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // dieselbe Etage wie Levelauswahl und Skilltree
        canvas.sortingOrder = 135;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root = HubUiKit.NewRect("CharacterSelect", canvasGO.transform);
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

        BuildBanner();
        BuildButtons();
        BuildFooter();

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

    void BuildButtons()
    {
        buttons.Clear();

        for (int i = 0; i < Characters.Count; i++)
        {
            Rect area = ButtonRect(i);

            var b = new Button
            {
                Fill = Fill("Char " + i + " Fill", screen, area, panelFill),
                Frame = Frame("Char " + i + " Frame", screen, area, panelBorder)
            };
            b.Label = HubUiKit.NewText("Char " + i + " Label", b.Fill.transform, font,
                                       buttonFontSize, panelInk, TextAlignmentOptions.Center);
            HubUiKit.Stretch((RectTransform)b.Label.transform);
            b.Label.text = Characters.NameOf(i);

            buttons.Add(b);
        }
    }

    void BuildFooter()
    {
        currentText = HubUiKit.NewText("Current", screen, font, smallFontSize,
                                       panelFill, TextAlignmentOptions.Center);
        HubUiKit.Place((RectTransform)currentText.transform, currentArea);

        backFill = Fill("Back", screen, backArea, panelFill);
        Frame("BackFrame", screen, backArea, panelInk);
        backText = HubUiKit.NewText("BackLabel", backFill.transform, font, smallFontSize,
                                    panelInk, TextAlignmentOptions.Center);
        HubUiKit.Stretch((RectTransform)backText.transform);
        backText.text = backLabel;
    }

    /// <summary>Wo Knopf i liegt - der erste steht im Inspector, der Rest darunter.</summary>
    Rect ButtonRect(int index) =>
        new Rect(buttonArea.x,
                 buttonArea.y + index * (buttonArea.height + buttonGap),
                 buttonArea.width, buttonArea.height);

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

    // --------------------------------------------------------------- Oeffnen

    public void Open()
    {
        if (IsOpen) return;

        Build();

        IsOpen = true;
        openedOnFrame = Time.frameCount;
        root.SetActive(true);

        cursor = Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, buttons.Count - 1));
        hoverIndex = -1;
        hoverBack = false;
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

        // Das [E], mit dem die Auswahl aufgeht, darf nicht gleich durchschlagen
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0))
        {
            if (hoverBack) { Close(); return; }
            if (hoverIndex >= 0) Choose(hoverIndex);
        }

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);

        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt)) Choose(cursor);

        // 1..9 waehlen direkt - so viele Charaktere, so viele Zifferntasten.
        for (int i = 0; i < buttons.Count && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Choose(i);
    }

    void Move(int step)
    {
        if (buttons.Count == 0) return;

        int next = Mathf.Clamp(cursor + step, 0, buttons.Count - 1);
        if (next == cursor) return;

        cursor = next;
        PlaySfx(moveClip);
        Refresh();
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
        int wasIndex = hoverIndex;
        bool wasBack = hoverBack;

        hoverIndex = -1;
        hoverBack = false;

        if (MousePixel(out Vector2 m))
        {
            if (backArea.Contains(m))
            {
                hoverBack = true;
            }
            else
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (!ButtonRect(i).Contains(m)) continue;
                    hoverIndex = i;
                    break;
                }
            }
        }

        // Die Maus fuehrt die Tastaturauswahl mit, sonst zeigen beide woanders hin.
        if (hoverIndex >= 0) cursor = hoverIndex;

        if (hoverIndex != wasIndex || hoverBack != wasBack) Refresh();
    }

    void Choose(int index)
    {
        if (index < 0 || index >= buttons.Count) return;

        cursor = index;

        // Das Setzen stellt zugleich den Skilltree auf diesen Charakter um.
        if (Shop.SkinIndex != index) Shop.SkinIndex = index;

        PlaySfx(pickClip != null ? pickClip : moveClip);
        Refresh();
    }

    // -------------------------------------------------------------- Anzeige

    void Refresh()
    {
        int chosen = Shop.SkinIndex;

        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            bool isChosen = i == chosen;
            bool isHover = i == cursor;

            b.Fill.color = isChosen ? chosenFill : (isHover ? hoverFill : panelFill);
            b.Label.color = isChosen ? panelFill : panelInk;
            b.Frame.color = isHover ? panelInk : panelBorder;
        }

        if (currentText != null)
            currentText.text = string.Format(currentFormat, Characters.NameOf(chosen));

        if (backFill != null) backFill.color = hoverBack ? hoverFill : panelFill;
        if (backText != null) backText.color = hoverBack ? panelInk : panelInkDim;
    }

    // ----------------------------------------------------------------- Ton

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
