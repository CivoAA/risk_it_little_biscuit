using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Schwierigkeitswahl am Chili-Regal in Ricos Laden, im Stil der Optionen
/// (<see cref="OptionsKit"/>).
///
///   Titelband   SCHWIERIGKEIT
///   Knoepfe     LEICHT  NORMAL  SCHWER   (gewaehlter = Marmelade)
///   Senke       Beschreibung der Wahl
///   Fussleiste  SCHLIESSEN
///
/// Die Wahl landet in <see cref="Difficulty.Current"/> und gilt ab dem naechsten
/// Lauf. Pfeiltasten/A-D wechseln, Esc/E/Enter schliessen. Beim ersten Oeffnen
/// kommt das Tutorial (<see cref="TutorialTopic.Difficulty"/>).
/// </summary>
public class DifficultyPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int CardX = 110, CardY = 60, CardW = 260, CardH = 152;
    private const int RibbonY = 48;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int IntroY = 78;
    private const int BtnY = 96, BtnW = 72, BtnH = 18, BtnGap = 8;
    private const int BtnX = CardX + (CardW - 3 * BtnW - 2 * BtnGap) / 2;
    private const int WellX = CardX + 14, WellY = 122, WellW = CardW - 28, WellH = 36;
    private const int HintY = 162;
    private const int FootY = 184, FootH = 18, FootW = 86;

    private static readonly string[] NameKeys = { "ui.difficulty.easy", "ui.difficulty.normal", "ui.difficulty.hard" };
    private static readonly string[] NameFallbacks = { "LEICHT", "NORMAL", "SCHWER" };
    private static readonly string[] DescKeys = { "ui.difficulty.desc.easy", "ui.difficulty.desc.normal", "ui.difficulty.desc.hard" };
    private static readonly string[] DescFallbacks =
    {
        "Ein wenig leichter: Gegner halten weniger aus und treffen schwächer.",
        "Das Spiel, wie es gedacht ist.",
        "Deutlich schwerer: Gegner halten mehr aus und treffen härter.",
    };

    // ==================================================================

    private static DifficultyPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private readonly SkinButton[] buttons = new SkinButton[3];
    private TextMeshProUGUI description;
    private int openedFrame;
    private HubUI hub;

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("DifficultyPanel");
        instance = go.AddComponent<DifficultyPanel>();
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
        BlockHub(true);
        TutorialPanel.ShowOnce(TutorialTopic.Difficulty);
    }

    private void OnEnable() => Loc.LanguageChanged += Build;
    private void OnDisable() => Loc.LanguageChanged -= Build;

    private void OnDestroy()
    {
        BlockHub(false);
        if (instance == this) instance = null;
    }

    /// <summary>Sperrt den Hub wie Werkbank und Shop, solange das Fenster offen ist.</summary>
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

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        // Der Tastendruck, der das Fenster geoeffnet hat, darf es nicht gleich schliessen.
        if (Time.frameCount == openedFrame) return;
        if (TutorialPanel.Blocking) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
    }

    private void Step(int delta)
    {
        int next = Mathf.Clamp((int)Difficulty.Current + delta, 0, 2);
        if (next == (int)Difficulty.Current) return;
        OptionsKit.PlayClick();
        Select((Difficulty.Level)next);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        OptionsKit.Clear(transform);

        // Ueber Shop (130) und Werkbank (155), unter den Optionen (210).
        page = OptionsKit.CreatePage(gameObject, 160, 0.6f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.difficulty.title", "SCHWIERIGKEIT"),
                          pixelFont, textFont);

        OptionsKit.Label("Intro", page, CardX, IntroY, CardW, 14,
                         Loc.Get("ui.difficulty.intro", "Wie scharf darf's sein?"), textFont, OptionsKit.SizeText,
                         GameHudSkin.Parchment, TextAlignmentOptions.Center);

        for (int i = 0; i < 3; i++)
        {
            var level = (Difficulty.Level)i;
            buttons[i] = SkinButton.Create(page, BtnX + i * (BtnW + BtnGap), BtnY, BtnW, BtnH,
                                           Loc.Get(NameKeys[i], NameFallbacks[i]), textFont,
                                           SkinButton.Kind.Wood, () => Select(level));
        }

        OptionsKit.Img("Well", page, WellX, WellY, WellW, WellH, GameHudSkin.Well, true);
        description = OptionsKit.Label("Desc", page, WellX + 6, WellY + 3, WellW - 12, WellH - 6, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        description.textWrappingMode = TextWrappingModes.Normal;

        OptionsKit.Label("Hint", page, CardX, HintY, CardW, 14,
                         Loc.Get("ui.difficulty.stars", "Story-Siege bringen Sterne - je schärfer, desto mehr."), textFont,
                         OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        SkinButton.Create(page, CardX + (CardW - FootW) / 2, FootY, FootW, FootH,
                          Loc.Get("ui.difficulty.close", "SCHLIESSEN"), textFont, SkinButton.Kind.Primary, Close);

        Refresh();
        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    private void Select(Difficulty.Level level)
    {
        Difficulty.Current = level;
        Refresh();
    }

    private void Refresh()
    {
        int current = (int)Difficulty.Current;
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].Active = i == current;

        if (description != null)
            description.text = Loc.Get(DescKeys[current], DescFallbacks[current]);
    }
}
