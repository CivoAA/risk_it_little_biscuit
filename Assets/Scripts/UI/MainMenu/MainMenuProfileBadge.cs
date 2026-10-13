using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Das Profil-Schild oben links im Hauptmenue: Gesicht des gewaehlten
/// Charakters, Profilname, Glutsplitter und Muenzen. Klick (oder TAB) oeffnet
/// die Profilauswahl (<see cref="ProfilePanel"/>).
///
/// Eigene Leinwand im 480x270-Raster wie die UI-2.0-Fenster, ganzzahlig
/// skaliert - so ist die Schrift genauso scharf wie in Optionen und
/// Profilauswahl. Legt <see cref="JuicyMainMenu"/> selbst an.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuProfileBadge : MonoBehaviour
{
    private const int Margin = 6;
    private const int H = 34, Avatar = 26, Crop = 8;
    private const float Delay = 0.45f, SlideTime = 0.5f;

    public System.Action OnOpen;

    private GameObject canvasGo;
    private CanvasScaler scaler;
    private RectTransform page, badge;
    private CanvasGroup group;
    private Image ring, avatar, bg;
    private TextMeshProUGUI hint;
    private CanvasGroup statsGroup, hintGroup;
    private readonly List<Sprite> faces = new List<Sprite>();
    private TMP_FontAsset textFont;
    private int width;
    private bool hover;
    private float elapsed, hoverT, flashT, hideT;
    private Image flash;
    private Vector2Int lastScreen;

    private void Start()
    {
        textFont = PixelUI.FindTextFont();

        canvasGo = new GameObject("ProfileBadgeCanvas");
        page = OptionsKit.CreatePage(canvasGo, 5, 0f, out scaler);
        // Die Abdunklung braucht das Schild nicht - und sie wuerde jeden Klick schlucken.
        Destroy(canvasGo.transform.Find("Dim").gameObject);

        Build();
        Layout();

        SaveProfiles.Changed += OnChanged;
        Shop.Changed += OnChanged;
        Loc.LanguageChanged += OnChanged;
    }

    private void OnDestroy()
    {
        SaveProfiles.Changed -= OnChanged;
        Shop.Changed -= OnChanged;
        Loc.LanguageChanged -= OnChanged;
        ClearFaces();
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void OnChanged()
    {
        if (page == null) return;
        OptionsKit.Clear(page);
        Build();
        flashT = 1f;
    }

    private void ClearFaces()
    {
        foreach (Sprite s in faces) if (s != null) Destroy(s);
        faces.Clear();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        int slot = SaveProfiles.Active;
        string name = SaveProfiles.NameOf(slot);
        ProfileSummary sum = ProfileSummary.Read(slot);
        // Der Shop im Speicher ist aktueller als die Datei.
        sum.Coins = Shop.Currency;
        sum.Character = Shop.SkinIndex;

        // Breite aus dem laengsten Text.
        TextMeshProUGUI probe = OptionsKit.Label("Probe", page, 0, 0, 300, 14, "", textFont, OptionsKit.SizeText,
                                                 Color.clear, TextAlignmentOptions.Left);
        string story = sum.FinaleDone ? "" : sum.Shards + "/" + StoryProgress.ShardWorlds.Length;
        string coins = ProfilePanel.FormatNumber(sum.Coins);
        string hintText = Loc.Get("ui.profile.badge.switch", "PROFIL WECHSELN");
        float nameW = OptionsKit.Measure(probe, name);
        float statsW = 9 + 3 + OptionsKit.Measure(probe, story) + 8 + 7 + 3 + OptionsKit.Measure(probe, coins);
        float hintW = OptionsKit.Measure(probe, hintText) + 9;
        probe.transform.SetParent(null, false);
        Destroy(probe.gameObject);

        int textX = 4 + Avatar + 6;
        width = Mathf.CeilToInt(textX + Mathf.Max(nameW, statsW, hintW) + 9);
        width = Mathf.Max(width, 96);

        badge = OptionsKit.Rect("Badge", page, Margin, Margin, width, H);
        group = badge.gameObject.AddComponent<CanvasGroup>();

        bg = OptionsKit.Img("Bg", badge, 0, 0, width, H, GameHudSkin.Card, true);
        bg.raycastTarget = true;
        PointerRelay relay = bg.gameObject.AddComponent<PointerRelay>();
        relay.Enter = () => hover = true;
        relay.Exit = () => hover = false;
        relay.Down = Open;

        ring = OptionsKit.Img("Ring", badge, 0, 0, width, H, GameHudSkin.Ring, OptionsKit.WithAlpha(GameHudSkin.Gold, 0f), true);

        // Gesicht: Mitte des 42er-Portraets, damit es ins kleine Schild passt.
        ClearFaces();
        Sprite[] frames = CharacterLooks.PortraitFor(sum.Character) ?? CharacterLooks.PortraitFor(0);
        if (frames != null)
            foreach (Sprite f in frames)
            {
                if (f == null) continue;
                Rect r = f.textureRect;
                faces.Add(Sprite.Create(f.texture, new Rect(r.x + Crop, r.y + Crop, r.width - 2 * Crop, r.height - 2 * Crop),
                                        new Vector2(0.5f, 0.5f), 100f));
            }

        OptionsKit.Img("AvatarFrame", badge, 3, 3, Avatar + 2, Avatar + 2, GameHudSkin.White, (Color)GameHudSkin.Ink);
        avatar = OptionsKit.Img("Avatar", badge, 4, 4, Avatar, Avatar, faces.Count > 0 ? faces[0] : null);
        if (faces.Count == 0) avatar.color = (Color)GameHudSkin.CardDeep;

        OptionsKit.ShadowLabel("Name", badge, textX, 4, width - textX - 4, 12, name, textFont, OptionsKit.SizeText,
                               GameHudSkin.Cream, TextAlignmentOptions.Left);

        // Zweite Zeile: Werte - unter dem Cursor weicht sie dem Hinweis.
        RectTransform stats = OptionsKit.Rect("Stats", badge, 0, 0, width, H);
        statsGroup = stats.gameObject.AddComponent<CanvasGroup>();
        statsGroup.blocksRaycasts = false;
        float x = textX;
        Sprite storyIcon = sum.FinaleDone ? GameHudSkin.Crown : GameHudSkin.Gem;
        OptionsKit.Img("StoryIcon", stats, x, 20 + Mathf.Floor((7 - storyIcon.rect.height) / 2), storyIcon.rect.width,
                       storyIcon.rect.height, storyIcon);
        x += storyIcon.rect.width + 3;
        if (story.Length > 0)
        {
            TextMeshProUGUI t = OptionsKit.Label("Story", stats, x, 17, 60, 12, story, textFont, OptionsKit.SizeText,
                                                 GameHudSkin.ParchDark, TextAlignmentOptions.Left);
            x += Mathf.Ceil(OptionsKit.Measure(t, story));
        }
        x += 8;
        OptionsKit.Img("CoinIcon", stats, x, 20, 7, 7, GameHudSkin.Coin);
        OptionsKit.Label("Coins", stats, x + 10, 17, 80, 12, coins, textFont, OptionsKit.SizeText,
                         GameHudSkin.ParchDark, TextAlignmentOptions.Left);

        RectTransform hintRoot = OptionsKit.Rect("Hint", badge, 0, 0, width, H);
        hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();
        hintGroup.blocksRaycasts = false;
        hintGroup.alpha = 0f;
        Sprite arrow = GameHudSkin.Arrow;
        OptionsKit.Img("Arrow", hintRoot, textX, 21, arrow.rect.width, arrow.rect.height, arrow);
        hint = OptionsKit.Label("HintText", hintRoot, textX + arrow.rect.width + 3, 17, 120, 12, hintText, textFont,
                                OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);

        flash = OptionsKit.Img("Flash", badge, 0, 0, width, H, GameHudSkin.Card, Color.clear, true);

        Animate(0f);
    }

    private void Layout()
    {
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        scaler.scaleFactor = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w / (float)OptionsKit.RefW, h / (float)OptionsKit.RefH)));
        // Seite an die linke obere Ecke - das Schild klebt dort, egal wie breit das Bild ist.
        page.anchoredPosition = Vector2.zero;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // ==================================================================
    //  Ablauf
    // ==================================================================

    private static bool OverlayOpen =>
        ProfilePanel.IsOpen || OptionsPanel.IsOpen || AchievementPanel.IsOpen || SceneFader.IsFading;

    private void Open()
    {
        if (OverlayOpen || elapsed < Delay + SlideTime * 0.5f) return;
        if (OnOpen != null) OnOpen();
        else ProfilePanel.Open();
    }

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

        if (!OverlayOpen && Input.GetKeyDown(KeyCode.Tab) && elapsed > Delay) Open();

        Animate(dt);
    }

    private void Animate(float dt)
    {
        if (badge == null) return;

        float s = Mathf.Clamp01((elapsed - Delay) / SlideTime);
        // Unter der Profilauswahl zeigt die Karte dasselbe - das Schild tritt zurueck.
        hideT = Mathf.Lerp(hideT, ProfilePanel.IsOpen ? 1f : 0f, 1f - Mathf.Exp(-16f * dt));
        group.alpha = Mathf.Clamp01(s * 3f) * (1f - hideT);
        group.blocksRaycasts = !ProfilePanel.IsOpen;
        float slide = Mathf.LerpUnclamped(-(width + Margin + 4), 0f, BackOut(s));

        bool lit = hover && !OverlayOpen;
        hoverT = Mathf.Lerp(hoverT, lit ? 1f : 0f, 1f - Mathf.Exp(-14f * dt));
        badge.anchoredPosition = new Vector2(Mathf.Round(Margin + slide + hoverT * 2f), -Margin);

        Color c = GameHudSkin.Gold;
        c.a = hoverT;
        ring.color = c;
        statsGroup.alpha = 1f - hoverT;
        hintGroup.alpha = hoverT;

        if (faces.Count > 1)
        {
            int frame = lit ? (int)(Time.unscaledTime * 7f) % faces.Count : 0;
            if (avatar.sprite != faces[frame]) avatar.sprite = faces[frame];
        }

        flashT = Mathf.Max(0f, flashT - dt * 2.5f);
        flash.color = new Color(1f, 0.95f, 0.8f, 0.5f * flashT * flashT);
    }

    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
