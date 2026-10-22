using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Schwierigkeitswahl am Chili-Regal in Ricos Laden, im Stil der Optionen
/// (<see cref="OptionsKit"/>).
///
///   Titelband   SCHWIERIGKEIT
///   Knoepfe     NORMAL  SCHWER  SEHR SCHWER   (gewaehlter = Marmelade)
///   Senke       Werte der Wahl (Leben, Schaden, XP, Scoreboard)
///   Fussleiste  SCHLIESSEN
///
/// Angezeigt heissen die Stufen NORMAL / SCHWER / SEHR SCHWER; im Code bleiben
/// sie <see cref="Difficulty.Level"/> Easy / Normal / Hard (Saves, Scoreboard).
/// Die Beschreibungen nennen die echten Faktoren aus <see cref="Difficulty.EnemyFactor"/>
/// und <see cref="EndlessScore.DifficultyFactor"/> - aendern die sich, den Text in
/// de/en.json mitziehen. Der Wechsel auf Sehr schwer fragt erst nach.
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

    private const int CardX = 100, CardY = 60, CardW = 280, CardH = 176;
    private const int RibbonY = 48;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int IntroY = 78;
    private const int BtnY = 96, BtnW = 84, BtnH = 18, BtnGap = 8;
    private const int BtnX = CardX + (CardW - 3 * BtnW - 2 * BtnGap) / 2;
    private const int WellX = CardX + 14, WellY = 122, WellW = CardW - 28, WellH = 62;
    private const int HintY = 188;
    private const int FootY = 210, FootH = 18, FootW = 86;

    // Rueckfrage beim Wechsel auf Sehr schwer
    private const int WarnX = 150, WarnY = 108, WarnW = 180, WarnH = 78;
    private const int WarnTextY = 116, WarnTextH = 34;
    private const int WarnBtnY = 158, WarnBtnW = 76, WarnBtnH = 18;

    private static readonly string[] NameKeys = { "ui.difficulty.easy", "ui.difficulty.normal", "ui.difficulty.hard" };
    private static readonly string[] NameFallbacks = { "NORMAL", "SCHWER", "SEHR SCHWER" };
    private static readonly string[] DescKeys = { "ui.difficulty.desc.easy", "ui.difficulty.desc.normal", "ui.difficulty.desc.hard" };
    private static readonly string[] DescFallbacks =
    {
        "Gegner haben 0,75-faches Leben und machen 0,75-fachen Schaden - bei gleicher XP. Scoreboard-Punkte zählen nur halb.",
        "Das Spiel, wie es gebaut ist: Gegner haben normales Leben und machen normalen Schaden. Normale Scoreboard-Punkte.",
        "Gegner haben 2,5-faches Leben und machen 2,5-fachen Schaden - bei gleicher XP. Dafür 5-fache Scoreboard-Punkte. " +
        "Nur für die Rangliste gedacht - Schwer ist schon schwer genug.",
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
    private RectTransform warning, warningPage;

    /// <summary>Anzeigename einer Stufe (LEICHT/NORMAL/SCHWER), auch fuer die Levelauswahl.</summary>
    public static string LevelName(Difficulty.Level level) => Loc.Get(NameKeys[(int)level], NameFallbacks[(int)level]);

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
            if (warningPage != null) warningPage.anchoredPosition = page.anchoredPosition;
        }

        // Der Tastendruck, der das Fenster geoeffnet hat, darf es nicht gleich schliessen.
        if (Time.frameCount == openedFrame) return;
        if (TutorialPanel.Blocking) return;

        if (warning != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                OptionsKit.PlayClick();
                CloseWarning();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OptionsKit.PlayClick();
                ConfirmHard();
            }
            return;
        }

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
        warning = warningPage = null;

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
        if (level == Difficulty.Level.Hard && Difficulty.Current != Difficulty.Level.Hard)
        {
            OpenWarning();
            return;
        }

        Difficulty.Current = level;
        Refresh();
    }

    // ---------- Rueckfrage vor Sehr schwer ----------

    private void OpenWarning()
    {
        if (warning != null) return;

        // Eigene Ebene ueber der Karte; die Abdunklung faengt jeden Klick daneben ab.
        Image shade = OptionsKit.Stretch("Warning", transform, GameHudSkin.White,
                                         new Color(0.06f, 0.04f, 0.05f, 0.7f));
        shade.raycastTarget = true;
        warning = shade.rectTransform;

        warningPage = OptionsKit.Rect("Page", warning, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        warningPage.anchoredPosition = page.anchoredPosition;

        OptionsKit.Img("Card", warningPage, WarnX, WarnY, WarnW, WarnH, GameHudSkin.Card, true);

        TextMeshProUGUI question = OptionsKit.Label("Question", warningPage, WarnX + 10, WarnTextY, WarnW - 20, WarnTextH,
            Loc.Get("ui.difficulty.warn.question", "Bist du dir sicher, dass du Sehr schwer spielen willst?"),
            textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        question.textWrappingMode = TextWrappingModes.Normal;

        int gap = WarnW - 20 - 2 * WarnBtnW;
        SkinButton.Create(warningPage, WarnX + 10, WarnBtnY, WarnBtnW, WarnBtnH,
                          Loc.Get("ui.difficulty.warn.cancel", "ZURÜCK"), textFont, SkinButton.Kind.Wood,
                          CloseWarning);
        SkinButton.Create(warningPage, WarnX + 10 + WarnBtnW + gap, WarnBtnY, WarnBtnW, WarnBtnH,
                          Loc.Get("ui.difficulty.warn.confirm", "JA"), textFont, SkinButton.Kind.Danger,
                          ConfirmHard);
    }

    private void CloseWarning()
    {
        if (warning == null) return;
        warning.SetParent(null, false);
        Destroy(warning.gameObject);
        warning = warningPage = null;
    }

    private void ConfirmHard()
    {
        CloseWarning();
        Difficulty.Current = Difficulty.Level.Hard;
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
