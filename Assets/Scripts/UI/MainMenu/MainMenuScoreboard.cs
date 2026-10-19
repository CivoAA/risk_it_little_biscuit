using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Das Endless-Scoreboard unten rechts im Hauptmenue. Der Inhalt (Welt,
/// Freunde | Weltweit, Zeilen) ist <see cref="ScoreboardView"/> - dasselbe wie
/// im Hub am Terminal (<see cref="HubScoreboardPanel"/>).
///
/// Klick auf die Kopfzeile klappt es ein und aus - zugeklappt bleibt nur die
/// Kopfzeile am unteren Rand stehen. Standard ist zu; ob es offen ist, merkt
/// sich das Profil (<see cref="ProfilePrefs"/>).
///
/// Eigene Leinwand im 480x270-Raster wie <see cref="MainMenuProfileBadge"/>,
/// ganzzahlig skaliert. Legt <see cref="JuicyMainMenu"/> selbst an.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuScoreboard : MonoBehaviour
{
    private const int MarginX = 6;

    /// <summary>Abstand zum unteren Rand - darunter steht die Versionsnummer.</summary>
    private const int MarginBottom = 24;

    private const int W = 152;   // breiter ragt es ins Menue
    private const int HeaderH = 17;
    private const int ViewX = 4, ViewY = HeaderH + 2;
    private const int H = ViewY + ScoreboardView.Height + 4;

    private const float Delay = 0.6f, SlideTime = 0.5f;

    private const string OpenKey = "scoreboard_open";

    private GameObject canvasGo;
    private CanvasScaler scaler;
    private RectTransform page, board, content;
    private Image bg;
    private CanvasGroup group;
    private TMP_FontAsset textFont;
    private SkinButton toggleButton;
    private ScoreboardView view;
    private float elapsed, hideT;
    private Vector2Int lastScreen;
    private int screenW, screenH;
    private bool open;

    private void Start()
    {
        textFont = PixelUI.FindTextFont();

        canvasGo = new GameObject("ScoreboardCanvas");
        page = OptionsKit.CreatePage(canvasGo, 5, 0f, out scaler);
        // Die Abdunklung braucht das Board nicht - und sie wuerde jeden Klick schlucken.
        Destroy(canvasGo.transform.Find("Dim").gameObject);

        open = ProfilePrefs.GetInt(OpenKey, 0) != 0;   // Standard: eingeklappt
        view = new ScoreboardView(W - 2 * ViewX, () => Ready);
        Build();
        Layout();

        SaveProfiles.Changed += OnProfileChanged;
        Loc.LanguageChanged += Rebuild;
    }

    private void OnDestroy()
    {
        SaveProfiles.Changed -= OnProfileChanged;
        Loc.LanguageChanged -= Rebuild;
        view?.Dispose();
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void OnProfileChanged()
    {
        open = ProfilePrefs.GetInt(OpenKey, 0) != 0;
        Rebuild();
    }

    private void Rebuild()
    {
        if (page == null) return;
        OptionsKit.Clear(page);
        Build();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private int Height => open ? H : HeaderH;

    private void Build()
    {
        board = OptionsKit.Rect("Scoreboard", page, 0, 0, W, H);
        group = board.gameObject.AddComponent<CanvasGroup>();

        bg = OptionsKit.Img("Bg", board, 0, 0, W, Height, GameHudSkin.Card, true);

        // Kopfzeile: ganze Breite klickbar, klappt ein und aus.
        Image head = OptionsKit.Img("Header", board, 0, 0, W, HeaderH, GameHudSkin.White, Color.clear);
        head.raycastTarget = true;
        PointerRelay relay = head.gameObject.AddComponent<PointerRelay>();
        relay.Down = () => { if (Ready) OptionsKit.PlayClick(); Toggle(); };

        Sprite crown = GameHudSkin.Crown;
        OptionsKit.Img("Crown", board, 6, 6, crown.rect.width, crown.rect.height, crown);
        OptionsKit.ShadowLabel("Title", board, 6 + crown.rect.width + 4, 2, W - 40, 12,
                               Loc.Get("ui.scoreboard.title", "ENDLESS SCORES"), textFont, OptionsKit.SizeText,
                               GameHudSkin.Gold, TextAlignmentOptions.Left);
        toggleButton = SkinButton.Create(board, W - 18, 2, 14, 13, open ? "-" : "+", textFont, SkinButton.Kind.Wood, Toggle);

        content = OptionsKit.Rect("Content", board, 0, 0, W, H);
        view.Build(content, ViewX, ViewY, textFont);
        content.gameObject.SetActive(open);

        Animate(0f);
    }

    private void Layout()
    {
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        int s = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w / (float)OptionsKit.RefW, h / (float)OptionsKit.RefH)));
        scaler.scaleFactor = s;
        // Seite an die linke obere Ecke, das Board an die rechte untere - egal wie gross das Bild ist.
        page.anchoredPosition = Vector2.zero;
        screenW = w / s;
        screenH = h / s;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private bool Ready => !OverlayOpen && elapsed > Delay + SlideTime * 0.5f;

    private void Toggle()
    {
        if (!Ready) return;
        open = !open;
        ProfilePrefs.SetInt(OpenKey, open ? 1 : 0);
        content.gameObject.SetActive(open);
        OptionsKit.Move(bg.rectTransform, 0, 0, W, Height);
        toggleButton.SetText(open ? "-" : "+");
    }

    private static bool OverlayOpen =>
        ProfilePanel.IsOpen || OptionsPanel.IsOpen || AchievementPanel.IsOpen || SceneFader.IsFading;

    private void Update()
    {
        if (page == null) return;
        if (new Vector2Int(Screen.width, Screen.height) != lastScreen) Layout();

        if (StudioSplash.IsShowing)
        {
            group.alpha = 0f;
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        elapsed += dt;
        Animate(dt);
    }

    private void Animate(float dt)
    {
        if (board == null) return;

        float s = Mathf.Clamp01((elapsed - Delay) / SlideTime);
        // Unter der Profilauswahl tritt das Board zurueck wie das Profil-Schild.
        hideT = Mathf.Lerp(hideT, ProfilePanel.IsOpen ? 1f : 0f, 1f - Mathf.Exp(-16f * dt));
        group.alpha = Mathf.Clamp01(s * 3f) * (1f - hideT);
        group.blocksRaycasts = !OverlayOpen;
        // Kommt von rechts herein.
        float slide = Mathf.LerpUnclamped(W + MarginX + 4, 0f, BackOut(s));
        board.anchoredPosition = new Vector2(Mathf.Round(screenW - MarginX - W + slide), -(screenH - MarginBottom - Height));
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
