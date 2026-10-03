using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Begruessung der Demo: erscheint einmal pro Spielstart, sobald man zum
/// ersten Mal im Hub steht. Sieht aus wie die Optionen (UI 2.0, siehe
/// <see cref="OptionsKit"/>).
///
///   Titelband   DANKE FUERS SPIELEN!
///   links       Buehne mit dem gewaehlten Charakter (Portraet aus der Keksdose)
///               und dem Schild DEMO-VERSION
///   rechts      Ueberschrift, Text, Hinweis auf den Feedback-Knopf
///   Fussleiste  FEEDBACK   SCHLIESSEN
///
/// Nur wenn <see cref="Demo.Active"/>. Die Texte stehen unter
/// <c>ui.demo.welcome.*</c> in de/en.json.
/// </summary>
public class DemoWelcomePanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int CardX = 80, CardY = 40, CardW = 320;
    private const int RibbonY = 28;

    // Buehne links: Portraet 42x42 doppelt gezeichnet, darunter das Schild.
    private const int StageX = CardX + 14, StageY = CardY + 18, StageW = 104, StageH = 122;
    private const int PortraitScale = 2;
    private const int BadgeH = 14;

    // Textspalte rechts.
    private const int ColX = StageX + StageW + 12, ColW = CardX + CardW - 14 - ColX;
    private const int HeadH = 14, Gap = 6;
    private const int TipPad = 5, TipIcon = 7;

    private const int FootH = 18, FootW = 92, FootGap = 8;

    /// <summary>Kurz warten, bis die Szene eingeblendet ist.</summary>
    private const float ShowDelay = 0.6f;

    private const float PortraitFps = 6f;

    // ==================================================================

    private static DemoWelcomePanel instance;
    private static bool shownThisSession;

    public static bool IsOpen => instance != null;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private RectInt content;
    private Vector2Int lastScreen;

    private Image portrait;
    private Sprite[] portraitFrames;
    private Vector2 portraitHome;
    private Image[] sparkles;
    private Vector2[] sparkleHome;

    private HubUI hub;
    private bool built;
    private float showAt;
    private int builtFrame = -1, feedbackFrame = -1;

    // ==================================================================
    //  Ausloeser: erster Hub-Besuch pro Spielstart
    // ==================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Im Editor direkt im Hub gestartet: sceneLoaded kam schon vorher.
        TryShow(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryShow(scene);

    private static void TryShow(Scene scene)
    {
        if (!Demo.Active || shownThisSession || instance != null) return;
        if (scene.name != GameSession.HubScene) return;

        shownThisSession = true;
        instance = new GameObject("DemoWelcomePanel").AddComponent<DemoWelcomePanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private void Awake()
    {
        instance = this;
        showAt = Time.unscaledTime + ShowDelay;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
    }

    private void OnDestroy()
    {
        if (built) BlockHub(false);
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!built)
        {
            // Ein anderes Fenster war schneller (Pause, Dialog) - hinten anstellen.
            if (Time.unscaledTime < showAt || HubUI.InputBlocked) return;
            Build();
            return;
        }

        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, content);
        }

        Animate();

        // Das Feedback liegt obendrauf und schliesst sich selbst mit ESC -
        // derselbe Tastendruck darf hier nicht auch noch zumachen.
        if (FeedbackPanel.IsOpen) { feedbackFrame = Time.frameCount; return; }
        if (Time.frameCount == builtFrame || Time.frameCount - feedbackFrame <= 1) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OptionsKit.PlayClick();
            Close();
        }
    }

    private void BlockHub(bool blocked)
    {
        if (blocked)
        {
            hub = FindAnyObjectByType<HubUI>();
            if (hub == null) return;

            HubUI.PushModal();
            hub.SetPlayerFrozen(true);
            return;
        }

        if (hub == null) return;

        HubUI.PopModal();
        hub.SetPlayerFrozen(false);
        hub = null;
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        built = true;
        builtFrame = Time.frameCount;
        BlockHub(true);

        // Unter dem Feedback (220), damit es sich darueber oeffnen kann.
        page = OptionsKit.CreatePage(gameObject, 215, 0.6f, out scaler);

        // Erst die Textspalte messen - die Karte waechst mit ihr.
        RectTransform col = OptionsKit.Rect("Column", page, ColX, 0, ColW, 10);
        int colH = BuildColumn(col);
        int bodyH = Mathf.Max(StageH, colH);
        int cardH = 18 + bodyH + 12 + FootH + 12;

        Image card = OptionsKit.Img("Card", page, CardX, CardY, CardW, cardH, GameHudSkin.Card, true);
        card.transform.SetAsFirstSibling();

        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY,
                          Loc.Get("ui.demo.welcome.title", "DANKE FÜRS SPIELEN!"), pixelFont, textFont);

        BuildStage(StageY + (bodyH - StageH) / 2);
        OptionsKit.Move(col, ColX, StageY + (bodyH - colH) / 2, ColW, colH);

        // Fussleiste
        int footY = StageY + bodyH + 12;
        int fx = CardX + (CardW - (FootW * 2 + FootGap)) / 2;
        SkinButton.Create(page, fx, footY, FootW, FootH,
                          Loc.Get("ui.demo.welcome.btn.feedback", "FEEDBACK"), textFont,
                          SkinButton.Kind.Wood, FeedbackPanel.Open, GameHudSkin.Bug);
        SkinButton.Create(page, fx + FootW + FootGap, footY, FootW, FootH,
                          Loc.Get("ui.demo.welcome.btn.close", "SCHLIESSEN"), textFont,
                          SkinButton.Kind.Primary, Close);

        content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + cardH - RibbonY);
        OptionsKit.Layout(page, scaler, content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    /// <summary>Buehne: Senke mit Charakter, Funkeln und dem Demo-Schild.</summary>
    private void BuildStage(int y)
    {
        OptionsKit.Img("Stage", page, StageX, y, StageW, StageH, GameHudSkin.Well, true);

        int skin = Shop.SkinIndex;
        portraitFrames = CharacterLooks.PortraitFor(skin) ?? CharacterLooks.PortraitFor(0);

        int areaH = StageH - BadgeH - 10;
        if (portraitFrames != null && portraitFrames.Length > 0)
        {
            Sprite first = portraitFrames[0];
            int w = Mathf.RoundToInt(first.rect.width) * PortraitScale;
            int h = Mathf.RoundToInt(first.rect.height) * PortraitScale;
            portrait = OptionsKit.Img("Portrait", page, StageX + (StageW - w) / 2, y + 4 + (areaH - h) / 2, w, h,
                                      first);
            portraitHome = portrait.rectTransform.anchoredPosition;
        }

        // Funkeln rundherum - blinkt versetzt.
        Vector2Int[] spots =
        {
            new Vector2Int(8, 10), new Vector2Int(StageW - 14, 16),
            new Vector2Int(12, areaH - 14), new Vector2Int(StageW - 12, areaH - 6),
        };
        sparkles = new Image[spots.Length];
        sparkleHome = new Vector2[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            sparkles[i] = OptionsKit.Img("Sparkle", page, StageX + spots[i].x, y + spots[i].y, 6, 6,
                                         GameHudSkin.Sparkle);
            sparkleHome[i] = sparkles[i].rectTransform.anchoredPosition;
        }

        // Schild unten in der Senke.
        string badge = Loc.Get("ui.demo.welcome.badge", "DEMO-VERSION");
        int bw = StageW - 16;
        OptionsKit.Img("Badge", page, StageX + 8, y + StageH - BadgeH - 6, bw, BadgeH, GameHudSkin.Badge, true);
        OptionsKit.Label("BadgeText", page, StageX + 8, y + StageH - BadgeH - 6, bw, BadgeH - 1, badge,
                         textFont, OptionsKit.SizeText, GameHudSkin.Ink,
                         TextAlignmentOptions.Center);
    }

    /// <summary>Textspalte, oben beginnend in <paramref name="col"/>. Gibt die Hoehe zurueck.</summary>
    private int BuildColumn(RectTransform col)
    {
        int y = 0;

        OptionsKit.ShadowLabel("Head", col, 0, y, ColW, HeadH,
                               Loc.Get("ui.demo.welcome.head", "Schön, dass du da bist!"), textFont,
                               OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
        y += HeadH + 2;

        y += Paragraph(col, "Body", 0, y, ColW,
                       Loc.Get("ui.demo.welcome.body",
                               "Vielen Dank, dass du unsere Demo spielst! Einige Inhalte sind noch nicht " +
                               "freigeschaltet, und manches funktioniert vielleicht noch nicht ganz einwandfrei."),
                       GameHudSkin.Parchment);
        y += Gap + 2;

        // Hinweiskasten mit Marienkaefer: wo das Feedback steckt.
        RectTransform tip = OptionsKit.Rect("Tip", col, 0, y, ColW, 10);
        Image tipBg = OptionsKit.Img("TipBg", tip, 0, 0, ColW, 10, GameHudSkin.LevelChip, true);
        int textX = TipPad + TipIcon + 5;
        int tipTextH = Paragraph(tip, "TipText", textX, TipPad, ColW - textX - TipPad,
                                 Loc.Get("ui.demo.welcome.feedback",
                                         "Fehler gefunden oder eine Idee? Im Pausenmenü (ESC) unter OPTIONEN " +
                                         "findest du unten den Knopf FEEDBACK - wir lesen alles!"),
                                 GameHudSkin.ParchDark);
        int tipH = tipTextH + TipPad * 2;
        OptionsKit.Move(tipBg.rectTransform, 0, 0, ColW, tipH);
        OptionsKit.Img("TipIcon", tip, TipPad, TipPad + 2, TipIcon, TipIcon, GameHudSkin.Bug);
        y += tipH;

        return y;
    }

    /// <summary>Umbrechender Absatz; gibt seine Hoehe in ganzen Pixeln zurueck.</summary>
    private int Paragraph(Transform parent, string name, float x, float y, float w, string text, Color color)
    {
        TextMeshProUGUI t = OptionsKit.Label(name, parent, x, y, w, 10, text, textFont, OptionsKit.SizeText,
                                             color, TextAlignmentOptions.TopLeft);
        t.textWrappingMode = TextWrappingModes.Normal;
        int h = Mathf.CeilToInt(t.GetPreferredValues(text, w, 0f).y) + 1;
        OptionsKit.Move(t.rectTransform, x, y, w, h);
        return h;
    }

    // ==================================================================
    //  Bewegung
    // ==================================================================

    private void Animate()
    {
        float now = Time.unscaledTime;

        if (portrait != null && portraitFrames != null)
        {
            int k = Mathf.FloorToInt(now * PortraitFps) % portraitFrames.Length;
            portrait.sprite = portraitFrames[k];
            // Ganze Seitenpixel - sonst wird die Pixelkante weich.
            float bob = Mathf.Round(Mathf.Sin(now * 2.2f) * 1.5f);
            portrait.rectTransform.anchoredPosition = portraitHome + new Vector2(0f, bob);
        }

        for (int i = 0; i < sparkles.Length; i++)
        {
            float s = Mathf.Sin(now * 2.6f + i * 1.7f);
            sparkles[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(s * 1.4f));
            sparkles[i].rectTransform.anchoredPosition = sparkleHome[i] + new Vector2(0f, s > 0.6f ? 1f : 0f);
        }
    }
}
