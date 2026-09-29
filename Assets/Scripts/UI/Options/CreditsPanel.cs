using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Credits, geoeffnet aus den Optionen (Reiter SPIEL). Liegt wie das
/// <see cref="FeedbackPanel"/> deckungsgleich ueber der Options-Karte.
///
///   Titelband   CREDITS
///   Liste       je Kategorie eine goldene Ueberschrift, darunter die Namen
///   Fussleiste  ZURUECK
///
/// Die Namen stehen unten in <see cref="Sections"/> - dort eintragen.
/// </summary>
public class CreditsPanel : MonoBehaviour
{
    // ==================================================================
    //  Wer - hier pflegen
    // ==================================================================

    private struct Section
    {
        public string Key, Fallback;
        public string[] Names;
    }

    private static readonly Section[] Sections =
    {
        new Section { Key = "ui.credits.cat.dev",   Fallback = "DEVELOPER",     Names = new[] { "Name eintragen" } },
        new Section { Key = "ui.credits.cat.pixel", Fallback = "PIXEL ARTISTS", Names = new[] { "Name eintragen" } },
        new Section { Key = "ui.credits.cat.sound", Fallback = "SOUND DESIGN",  Names = new[] { "Name eintragen" } },
        new Section { Key = "ui.credits.cat.test",  Fallback = "TESTER",        Names = new[] { "Name eintragen" } },
    };

    // ==================================================================
    //  Masse (Seitenpixel) - Karte wie OptionsPanel
    // ==================================================================

    private const int CardX = 80, CardY = 40, CardW = 320, CardH = 208;
    private const int RibbonY = 28;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int WellX = 92, WellY = 56, WellW = 296, WellH = 160;
    private const int ViewX = WellX + 4, ViewY = WellY + 4, ViewW = WellW - 8, ViewH = WellH - 8;
    private const int HeadH = 14, NameH = 12, SectionGap = 8;

    private const int FootY = 223, FootH = 18, FootW = 86;

    // ==================================================================

    private static CreditsPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page, content;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private Image scrollHandle;
    private float contentHeight, scrollTop;
    private int openedFrame;

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("CreditsPanel");
        instance = go.AddComponent<CreditsPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Scroll(-wheel * NameH * 2);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        // Ueber den Optionen (210), gleiche Ebene wie das Feedback.
        page = OptionsKit.CreatePage(gameObject, 220, 0.6f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.credits.title", "CREDITS"),
                          pixelFont, textFont);

        OptionsKit.Img("Well", page, WellX, WellY, WellW, WellH, GameHudSkin.Well, true);

        RectTransform viewport = OptionsKit.Rect("Viewport", page, ViewX, ViewY, ViewW, ViewH);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = OptionsKit.Rect("Content", viewport, 0, 0, ViewW, ViewH);

        scrollHandle = OptionsKit.Img("ScrollHandle", page, WellX + WellW - 3, ViewY, 2, ViewH,
                                      GameHudSkin.White, GameHudSkin.Stone);

        BuildList();

        SkinButton.Create(page, CardX + (CardW - FootW) / 2, FootY, FootW, FootH,
                          Loc.Get("ui.credits.back", "ZURÜCK"), textFont, SkinButton.Kind.Primary, Close);

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    private void BuildList()
    {
        float y = 4f;

        foreach (Section section in Sections)
        {
            OptionsKit.ShadowLabel("Head", content, 0, y, ViewW, HeadH,
                                   Loc.Get(section.Key, section.Fallback), textFont, OptionsKit.SizeText,
                                   GameHudSkin.Gold, TextAlignmentOptions.Center);
            y += HeadH;

            foreach (string name in section.Names)
            {
                OptionsKit.Label("Name", content, 0, y, ViewW, NameH, name, textFont, OptionsKit.SizeText,
                                 GameHudSkin.Parchment, TextAlignmentOptions.Center);
                y += NameH;
            }

            y += SectionGap;
        }

        contentHeight = y - SectionGap + 4f;

        // Wenig Inhalt: mittig in die Senke statt oben angeklebt.
        if (contentHeight < ViewH)
            content.anchoredPosition = new Vector2(0f, -Mathf.Round((ViewH - contentHeight) / 2f));

        content.sizeDelta = new Vector2(ViewW, Mathf.Max(ViewH, contentHeight));
        UpdateScrollBar();
    }

    private void Scroll(float delta)
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        if (hidden <= 0f) return;

        scrollTop = Mathf.Clamp(scrollTop + delta, 0f, hidden);
        content.anchoredPosition = new Vector2(0f, Mathf.Round(scrollTop));
        UpdateScrollBar();
    }

    private void UpdateScrollBar()
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        scrollHandle.enabled = hidden > 0f;
        if (hidden <= 0f) return;

        float size = Mathf.Max(8f, Mathf.Round(ViewH * (ViewH / contentHeight)));
        float y = ViewY + Mathf.Round((ViewH - size) * (scrollTop / hidden));
        OptionsKit.Move(scrollHandle.rectTransform, WellX + WellW - 3, y, 2, size);
    }
}
