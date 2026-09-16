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

    GameObject dialogueRoot, promptRoot;
    TextMeshProUGUI bodyText, hintText, promptLabel;

    string[] pages;
    int pageIndex;
    bool promptRequestedThisFrame;
    bool built;

    MonoBehaviour playerController;   // eingefrorenes Steuerskript

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (_instance == this) { _instance = null; DialogueOpen = false; }
    }

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

        hintText = NewText("Hint", dialogueRoot.transform, hintFontSize, hintColor);
        var hRect = (RectTransform)hintText.transform;
        hRect.anchorMin = new Vector2(1f, 0f);
        hRect.anchorMax = new Vector2(1f, 0f);
        hRect.pivot     = new Vector2(1f, 0f);
        hRect.sizeDelta = new Vector2(120f, 10f);
        hRect.anchoredPosition = new Vector2(-padding, padding * 0.4f);
        hintText.alignment = TextAlignmentOptions.BottomRight;

        dialogueRoot.SetActive(false);

        // ---- [E]-Hinweis -------------------------------------------------
        promptRoot = NewRect("Prompt", canvasGO.transform);
        var pRect = (RectTransform)promptRoot.transform;
        pRect.anchorMin = new Vector2(0.5f, 0f);
        pRect.anchorMax = new Vector2(0.5f, 0f);
        pRect.pivot     = new Vector2(0.5f, 0f);
        pRect.sizeDelta = new Vector2(referenceResolution.x, 14f);
        pRect.anchoredPosition = new Vector2(0f, boxMarginY + 4f);

        promptLabel = NewText("Label", promptRoot.transform, hintFontSize + 1f, borderColor);
        Stretch((RectTransform)promptLabel.transform, 0f);
        promptLabel.alignment = TextAlignmentOptions.Center;

        promptRoot.SetActive(false);
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
        if (promptLabel != null && promptLabel.text != text) promptLabel.text = text;
    }

    public void ShowDialogue(string[] newPages)
    {
        if (newPages == null || newPages.Length == 0) return;

        pages = newPages;
        pageIndex = 0;
        DialogueOpen = true;
        promptRoot.SetActive(false);
        dialogueRoot.SetActive(true);
        ShowPage();
        FreezePlayer(true);
    }

    void ShowPage()
    {
        bodyText.text = pages[pageIndex];
        bool last = pageIndex >= pages.Length - 1;
        string action = last ? "Klick zum Schliessen" : "Klick fuer weiter";
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
        if (!DialogueOpen) return;

        // Hinweis blinken lassen
        float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
        Color c = hintColor;
        c.a *= a;
        hintText.color = c;

        if (Input.GetMouseButtonDown(0))
        {
            pageIndex++;
            if (pageIndex >= pages.Length) CloseDialogue();
            else ShowPage();
        }
    }

    void LateUpdate()
    {
        // Immediate Mode: wer den Hinweis will, meldet sich jeden Frame.
        // Meldet sich niemand, verschwindet er von selbst - so streiten sich
        // mehrere Objekte nicht darum, wer ihn wieder ausschaltet.
        if (promptRoot != null && promptRoot.activeSelf != promptRequestedThisFrame)
            promptRoot.SetActive(promptRequestedThisFrame);
        promptRequestedThisFrame = false;
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
