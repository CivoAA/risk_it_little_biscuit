using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Das Scoreboard im Hub - geht am Terminal mit [E] auf
/// (<see cref="HubConsoleTerminal"/>). Inhalt wie im Hauptmenue
/// (<see cref="ScoreboardView"/>), hier als Fenster mit Band und Knopf.
///
/// Tastatur: Links/Rechts (A/D) wechselt die Welt, Hoch/Runter (W/S) oder Tab
/// zwischen Freunden und weltweit, Escape/E/Enter schliesst.
/// </summary>
public class HubScoreboardPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    private const int ViewW = 200;
    private const int CardW = ViewW + 20, CardX = (OptionsKit.RefW - CardW) / 2;
    private const int RibbonY = 14, CardY = 24;
    private const int ViewY = CardY + 14;
    private const int FootY = ViewY + ScoreboardView.Height + 6, FootH = 18, FootW = 86;
    private const int CardH = FootY + FootH + 8 - CardY;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    // ==================================================================

    private static HubScoreboardPanel instance;
    public static bool IsOpen => instance != null;

    private ScoreboardView view;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private int openedFrame;
    private HubUI hub;

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("HubScoreboardPanel");
        instance = go.AddComponent<HubScoreboardPanel>();
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
        view = new ScoreboardView(ViewW, () => true);
        Build();
        BlockHub(true);
        Loc.LanguageChanged += Build;
    }

    private void OnDestroy()
    {
        Loc.LanguageChanged -= Build;
        view?.Dispose();
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

    private void Build()
    {
        OptionsKit.Clear(transform);

        // Wie das Schwierigkeits-Fenster: ueber Shop und Werkbank, unter den Optionen.
        page = OptionsKit.CreatePage(gameObject, 160, 0.6f, out scaler);

        TMPro.TMP_FontAsset textFont = PixelUI.FindTextFont();
        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.scoreboard.title", "HIGH SCORES"),
                          PixelUI.FindPixelFont(), textFont);

        view.Build(page, CardX + 10, ViewY, textFont);

        SkinButton.Create(page, CardX + (CardW - FootW) / 2, FootY, FootW, FootH,
                          Loc.Get("ui.difficulty.close", "SCHLIESSEN"), textFont, SkinButton.Kind.Primary, Close);

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
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

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OptionsKit.PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(() => view.StepMap(-1));
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(() => view.StepMap(1));
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Tab))
            Step(view.ToggleScope);
    }

    private static void Step(System.Action a)
    {
        OptionsKit.PlayClick();
        a();
    }
}
