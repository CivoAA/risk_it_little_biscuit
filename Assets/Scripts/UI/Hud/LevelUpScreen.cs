using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level-Up-Auswahl, Evo-Buch und Mixer-Power-ups im Stil des <see cref="GameHud"/>.
/// Der Mixer steht in LevelUpScreen.Mixer.cs, der Death-Screen in LevelUpScreen.Death.cs.
///
///   LevelUpScreen.Create(uiController);   // macht UIController.Awake selbst
///
/// Baut sich komplett per Code auf, die Grafik malt <see cref="GameHudSkin"/>.
/// Die Spiellogik bleibt, wo sie war: PlayerController.RandomWeapon bestueckt
/// die alten <see cref="LevelUpButton"/>s, UIController oeffnet und schliesst
/// LevelUpPanel und EvoPanel. Dieses Fenster schaut nur zu, welches der beiden
/// Panels gerade aktiv ist, zeichnet es neu und ruft beim Klick dieselben
/// Methoden auf wie frueher die Szenen-Knoepfe (SelectUpgrade, RerollLevelUp,
/// BanishLevelUp, EvoPanelOpen/Close). Die alten Panels laufen unsichtbar mit.
///
/// Gerechnet wird wie im HUD in Pixeln eines 480x270-Rasters, Ursprung oben
/// links, ganzzahlig skaliert. Die Maus wird selbst gegen diese Rechtecke
/// geprueft (wie in der Levelauswahl im Hub) - so haengt nichts an einem
/// EventSystem, und es gibt keine halben Pixel durch Layout-Gruppen.
///
/// Tasten: 1-4 waehlt eine Karte, Pfeile/A/D + Enter/Leertaste ebenso,
/// R = Reroll, B = Banish, E = Evo-Buch, Esc bricht Banish ab bzw. schliesst
/// das Buch.
/// </summary>
public partial class LevelUpScreen : MonoBehaviour
{
    // ==================================================================
    //  Masse (Pixel im 480x270-Raster)
    // ==================================================================

    private const int RefW = 480, RefH = 270;

    // Was tatsaechlich bemalt wird - danach richtet sich die Skalierung, nicht
    // nach den 480x270. So bekommt 1366x768 Stufe 3 statt 2.
    private const int ContentW = 440, ContentTop = 6, ContentBottom = 262;

    // ---- Kopf ----
    private const int TitleY = 8, TitleH = 22;
    private const int BadgeY = 26, BadgeH = 11;
    private const int SubtitleY = 40, SubtitleH = 11;

    // ---- Karten ----
    private const int CardW = 112, CardH = 146, CardGap = 10, CardsY = 56;
    private const int MaxCards = 4;

    // ---- Knopfleiste ----
    private const int ButtonsY = 211, ButtonW = 100, ButtonH = 20, ButtonGap = 8;

    // ---- Build-Leiste ----
    private const int StripY = 240, StripTile = 22, StripStep = 24, StripGroupGap = 12, StripMax = 8;

    // ---- Evo-Buch ----
    private const int EntryW = 212, EntryH = 40, EntryGapX = 8, EntryStepY = 44;
    private const int EntriesX = 24, EntriesY = 54, EntriesPerPage = 8;
    private const int BookFooterY = 234;

    // ---- Tempo ----
    private const float InputDelay = 0.3f;      // so lange nach dem Oeffnen zaehlt kein Klick
    private const float DealStagger = 0.06f;
    private const float DealTime = 0.16f;
    private const float PickTime = 0.2f;
    private const float ToastTime = 1.8f;

    private const float SizeText = 10f;
    private const float SizeTitle = 20f;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private enum Mode { Hidden, Choose, Book, Mixer, Death }
    private enum Kind { Weapon, Buff, Evo, Bonus }
    private enum TextStyle { Plain, Shadow, Outline }

    private UIController ui;
    private Canvas canvas;
    private CanvasScaler scaler;
    private TMP_FontAsset font, textFont;

    private RectTransform root, page, chooseLayer, bookLayer, mixerLayer;
    private Image backdrop;
    private Vector2Int lastScreen;
    private int scale = 1, pageX, pageY;

    private Mode mode = Mode.Hidden;
    private float openedAt, inputFrom;

    // Kopf
    private RectTransform ribbon, tailL, tailR, badge;
    private PixText titleText, badgeText, subtitle;
    private string toast;
    private float toastUntil;
    private bool toastBad;

    // Karten
    private readonly List<CardView> cards = new List<CardView>();
    private readonly List<Weapon> shown = new List<Weapon>();
    private readonly List<Weapon> offered = new List<Weapon>();
    private bool shownBanish;
    private bool forceDeal;
    private float dealAt;
    private int focus = -1;
    private bool keyboardFocus;
    private Vector2 lastMouse;
    private int pendingPick = -1;
    private float pendingAt;
    private int pressed = -1;

    // Knoepfe
    private ButtonView rerollButton, banishButton, evoButton;
    private readonly List<ButtonView> buttons = new List<ButtonView>();

    // Build-Leiste
    private readonly List<StripTileView> stripWeapons = new List<StripTileView>();
    private readonly List<StripTileView> stripBuffs = new List<StripTileView>();
    private readonly List<Weapon> ownedWeapons = new List<Weapon>();
    private readonly List<Weapon> ownedBuffs = new List<Weapon>();

    // Evo-Buch
    private RectTransform bookSign;
    private PixText bookTitle, bookSubtitle, slotsText, pageText;
    private RectTransform slotsChip;
    private ButtonView backButton, prevButton, nextButton;
    private readonly List<EntryView> entries = new List<EntryView>();
    private int bookPage;
    private int bookFocus = -1;

    private static readonly Regex ColorTags = new Regex("<color=[^>]*>|</color>", RegexOptions.Compiled);

    // ==================================================================
    //  Aufbau
    // ==================================================================

    public static LevelUpScreen Create(UIController ui)
    {
        var go = new GameObject("LevelUpScreen");
        // Wie das HUD: neue Objekte landen in der aktiven Szene (im Lauf der
        // Hub), das Fenster gehoert aber zum Lauf.
        if (ui.gameObject.scene.IsValid() && go.scene != ui.gameObject.scene)
            SceneManager.MoveGameObjectToScene(go, ui.gameObject.scene);

        LevelUpScreen screen = go.AddComponent<LevelUpScreen>();
        screen.ui = ui;
        screen.Build(ui.GetComponent<Canvas>());
        return screen;
    }

    private void Build(Canvas uiCanvas)
    {
        // Dieselbe Schriftwahl wie im HUD: ThaleahFat, Texte mit Umlauten
        // weichen einzeln auf Jersey10 aus.
        font = PixelUI.FindPixelFont();
        textFont = PixelUI.FindTextFont();
        if (font == null) font = textFont;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        // Ueber dem alten UI-Canvas (dort liegen die unsichtbaren Panels),
        // unter dem Pausenmenue (200).
        canvas.sortingOrder = uiCanvas != null ? uiCanvas.sortingOrder + 10 : 50;
        if (uiCanvas != null) canvas.sortingLayerID = uiCanvas.sortingLayerID;
        canvas.enabled = false;

        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        root = (RectTransform)transform;

        backdrop = Img("Backdrop", root, new RectInt(0, 0, 1, 1), GameHudSkin.White, new Color(0f, 0f, 0f, 0f));
        RectTransform b = backdrop.rectTransform;
        b.anchorMin = Vector2.zero;
        b.anchorMax = Vector2.one;
        b.offsetMin = b.offsetMax = Vector2.zero;

        page = NewRect("Page", root);
        Place(page, new RectInt(0, 0, RefW, RefH));

        chooseLayer = NewRect("Choose", page);
        Place(chooseLayer, new RectInt(0, 0, RefW, RefH));
        bookLayer = NewRect("Book", page);
        Place(bookLayer, new RectInt(0, 0, RefW, RefH));
        mixerLayer = NewRect("Mixer", page);
        Place(mixerLayer, new RectInt(0, 0, RefW, RefH));

        BuildHead();
        for (int i = 0; i < MaxCards; i++) cards.Add(new CardView(this, chooseLayer, i));
        BuildButtons();
        BuildBook();
        BuildMixer();
        BuildDeath();

        chooseLayer.gameObject.SetActive(false);
        bookLayer.gameObject.SetActive(false);
        mixerLayer.gameObject.SetActive(false);
        ApplyLayout(true);
    }

    private void BuildHead()
    {
        tailL = Img("TailL", chooseLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTail(true), Color.white).rectTransform;
        tailR = Img("TailR", chooseLayer, new RectInt(0, TitleY + 4, 12, 16), GameHudSkin.RibbonTail(false), Color.white).rectTransform;
        ribbon = Img("Ribbon", chooseLayer, new RectInt(180, TitleY, 120, TitleH), GameHudSkin.Ribbon, Color.white, true).rectTransform;
        titleText = Text("Title", ribbon, new RectInt(0, 0, 120, TitleH - 1), SizeTitle, GameHudSkin.Cream,
                         TextAlignmentOptions.Center, TextStyle.Shadow);

        badge = Img("Badge", chooseLayer, new RectInt(220, BadgeY, 40, BadgeH), GameHudSkin.Badge, Color.white, true).rectTransform;
        badgeText = Text("BadgeText", badge, new RectInt(0, 0, 40, BadgeH), SizeText, GameHudSkin.Ink,
                         TextAlignmentOptions.Center, TextStyle.Plain);

        subtitle = Text("Subtitle", chooseLayer, new RectInt(0, SubtitleY, RefW, SubtitleH), SizeText,
                        GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);
    }

    private void BuildButtons()
    {
        int total = 3 * ButtonW + 2 * ButtonGap;
        int x = (RefW - total) / 2;
        rerollButton = new ButtonView(this, chooseLayer, new RectInt(x, ButtonsY, ButtonW, ButtonH), GameHudSkin.Dice, "R");
        banishButton = new ButtonView(this, chooseLayer, new RectInt(x + ButtonW + ButtonGap, ButtonsY, ButtonW, ButtonH), GameHudSkin.Banish, "B");
        evoButton = new ButtonView(this, chooseLayer, new RectInt(x + 2 * (ButtonW + ButtonGap), ButtonsY, ButtonW, ButtonH), GameHudSkin.EvoArrow, "E");
        buttons.Add(rerollButton);
        buttons.Add(banishButton);
        buttons.Add(evoButton);
    }

    private void BuildBook()
    {
        Color shadow = new Color(0f, 0f, 0f, 0.35f);
        Img("SignShadow", bookLayer, new RectInt(0, 0, 1, 1), GameHudSkin.Sign, shadow, true);
        bookSign = Img("Sign", bookLayer, new RectInt(170, TitleY, 140, 24), GameHudSkin.Sign, Color.white, true).rectTransform;
        bookTitle = Text("Title", bookSign, new RectInt(0, 0, 140, 23), SizeTitle, GameHudSkin.Cream,
                         TextAlignmentOptions.Center, TextStyle.Shadow);
        bookSubtitle = Text("Subtitle", bookLayer, new RectInt(0, 38, RefW, SubtitleH), SizeText,
                            GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);

        for (int i = 0; i < EntriesPerPage; i++)
        {
            int col = i % 2, row = i / 2;
            var r = new RectInt(EntriesX + col * (EntryW + EntryGapX), EntriesY + row * EntryStepY, EntryW, EntryH);
            entries.Add(new EntryView(this, bookLayer, r));
        }

        slotsChip = Img("SlotsChip", bookLayer, new RectInt(EntriesX, BookFooterY + 4, 80, 13), GameHudSkin.Plate,
                        Color.white, true).rectTransform;
        slotsText = Text("Slots", slotsChip, new RectInt(0, 0, 80, 13), SizeText, GameHudSkin.JamLight,
                         TextAlignmentOptions.Center, TextStyle.Shadow);

        backButton = new ButtonView(this, bookLayer, new RectInt((RefW - ButtonW) / 2, BookFooterY, ButtonW, ButtonH),
                                    null, "ESC");

        int right = EntriesX + 2 * EntryW + EntryGapX;
        nextButton = new ButtonView(this, bookLayer, new RectInt(right - 22, BookFooterY, 22, ButtonH), GameHudSkin.Arrow, null);
        prevButton = new ButtonView(this, bookLayer, new RectInt(right - 22 - 50, BookFooterY, 22, ButtonH), GameHudSkin.Arrow, null);
        prevButton.FlipIcon();
        pageText = Text("Page", bookLayer, new RectInt(right - 50, BookFooterY, 28, ButtonH), SizeText,
                        GameHudSkin.Parchment, TextAlignmentOptions.Center, TextStyle.Outline);
    }

    // ==================================================================
    //  Laufzeit
    // ==================================================================

    private void LateUpdate()
    {
        if (ui == null) return;

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        Mode want = WantedMode();
        if (want != mode) Enter(want, now);
        if (mode == Mode.Hidden) return;

        ApplyLayout(false);

        // Linearer Farbraum: Deckkraft wirkt schwaecher, als sie klingt.
        float dim = mode == Mode.Book ? 0.93f : 0.88f;
        Color bc = backdrop.color;
        bc = new Color(0.07f, 0.05f, 0.06f, Mathf.MoveTowards(bc.a, dim, dt * 5f));
        backdrop.color = bc;

        Vector2 mouse = MousePx();
        bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.25f;
        lastMouse = mouse;

        if (mode == Mode.Choose) UpdateChoose(now, mouse, mouseMoved);
        else if (mode == Mode.Mixer) UpdateMixer(now, mouse, mouseMoved);
        else if (mode == Mode.Death) UpdateDeath(now, mouse);
        else UpdateBook(now, mouse);
    }

    private Mode WantedMode()
    {
        if (ui.GameOverPanel != null && ui.GameOverPanel.activeInHierarchy) return Mode.Death;
        // Sieg: derselbe Abschluss, nur festlich (EnterDeath schaut auf WinPanel).
        if (ui.WinPanel != null && ui.WinPanel.activeInHierarchy) return Mode.Death;
        if (ui.EvoPanel != null && ui.EvoPanel.activeInHierarchy) return Mode.Book;
        if (ui.LevelUpPanel != null && ui.LevelUpPanel.activeInHierarchy) return Mode.Choose;
        if (ui.PowerUpPanel != null && ui.PowerUpPanel.activeInHierarchy) return Mode.Mixer;
        return Mode.Hidden;
    }

    private void Enter(Mode next, float now)
    {
        Mode prev = mode;
        mode = next;

        canvas.enabled = next != Mode.Hidden;
        chooseLayer.gameObject.SetActive(next == Mode.Choose);
        bookLayer.gameObject.SetActive(next == Mode.Book);
        mixerLayer.gameObject.SetActive(next == Mode.Mixer);
        deathLayer.gameObject.SetActive(next == Mode.Death);
        pressed = -1;

        if (next == Mode.Hidden)
        {
            backdrop.color = new Color(0.07f, 0.05f, 0.06f, 0f);
            pendingPick = -1;
            return;
        }

        ApplyLayout(true);
        lastMouse = MousePx();

        if (next == Mode.Mixer)
        {
            EnterMixer(now);
        }
        else if (next == Mode.Death)
        {
            EnterDeath(now);
        }
        else if (next == Mode.Choose)
        {
            // Aus dem Buch zurueck: kein neues Austeilen, nur kurz sperren.
            bool fresh = prev == Mode.Hidden;
            openedAt = now;
            inputFrom = now + (fresh ? InputDelay : 0.1f);
            pendingPick = -1;
            toastUntil = 0f;
            focus = -1;
            keyboardFocus = false;
            shown.Clear();
            forceDeal = true;
            dealAt = fresh ? now : now - 1f;
            RefreshHead();
            RefreshStrip();
        }
        else
        {
            inputFrom = now + 0.12f;
            bookPage = 0;
            bookFocus = -1;
            RefreshBook();
        }
    }

    /// <summary>Ganzzahlige Skalierung, Seite mittig auf ganzen Pixeln.</summary>
    private void ApplyLayout(bool force)
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && size == lastScreen) return;
        lastScreen = size;

        // Gerundet wie das HUD - passt die Seite dann nicht mehr ganz auf den
        // Schirm, eine Stufe kleiner. Die Karten muessen komplett sichtbar sein.
        int s = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(size.x / (float)RefW, size.y / (float)RefH)));
        while (s > 1 && (ContentW * s > size.x || (ContentBottom - ContentTop) * s > size.y)) s--;
        scale = s;
        scaler.scaleFactor = s;

        // Mitte des bemalten Bereichs auf die Bildschirmmitte, ganze Pixel.
        pageX = (size.x / s - RefW) / 2;
        pageY = (size.y / s) / 2 - (ContentTop + ContentBottom) / 2;
        page.anchoredPosition = new Vector2(pageX, -pageY);
    }

    /// <summary>Maus in Seitenpixeln, Ursprung oben links.</summary>
    private Vector2 MousePx()
    {
        Vector3 m = Input.mousePosition;
        return new Vector2(m.x / scale - pageX, (Screen.height - m.y) / scale - pageY);
    }

    private static bool Hit(RectInt r, Vector2 p)
    {
        return p.x >= r.x && p.x < r.xMax && p.y >= r.y && p.y < r.yMax;
    }

    // ==================================================================
    //  Auswahl
    // ==================================================================

    private void UpdateChoose(float now, Vector2 mouse, bool mouseMoved)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        CollectOffered();
        if (forceDeal || !SameList(offered, shown) || shownBanish != ui.banish)
        {
            bool newDeal = forceDeal || !SameList(offered, shown);
            if (newDeal && !forceDeal) dealAt = now;   // Reroll: neu austeilen
            forceDeal = false;
            shown.Clear();
            shown.AddRange(offered);
            shownBanish = ui.banish;
            LayoutCards();
            if (focus >= shown.Count) focus = shown.Count - 1;
        }

        // Ausgewaehlt: kurz aufblitzen lassen, dann erst die Logik rufen.
        if (pendingPick >= 0)
        {
            if (now >= pendingAt) CommitPick();
            AnimateCards(now);
            UpdateButtons(now, mouse, false);
            UpdateHeadAnim(now);
            UpdateStrip(now);
            return;
        }

        bool inputOpen = now >= inputFrom;

        // ---- Fokus per Maus ----
        int hover = -1;
        for (int i = 0; i < shown.Count; i++)
            if (Hit(cards[i].Rect, mouse)) hover = i;
        if (mouseMoved && !(keyboardFocus && hover < 0))
        {
            keyboardFocus = false;
            focus = hover;
        }
        else if (!keyboardFocus)
        {
            focus = hover;
        }

        if (inputOpen)
        {
            HandleCardMouse(hover);
            HandleChooseKeys();
        }

        AnimateCards(now);
        UpdateButtons(now, mouse, inputOpen);
        UpdateHeadAnim(now);
        UpdateStrip(now);
    }

    private void HandleCardMouse(int hover)
    {
        if (Input.GetMouseButtonDown(0)) pressed = hover;
        if (Input.GetMouseButtonUp(0))
        {
            if (pressed >= 0 && pressed == hover) Pick(hover);
            pressed = -1;
        }
        // Rechtsklick: Banish-Modus verlassen, wie Esc.
        if (Input.GetMouseButtonDown(1) && ui.banish) ToggleBanish();
    }

    private void HandleChooseKeys()
    {
        for (int i = 0; i < shown.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                focus = i;
                keyboardFocus = true;
                Pick(i);
                return;
            }
        }

        if (shown.Count > 0)
        {
            int move = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
            if (move != 0)
            {
                focus = focus < 0 ? (move > 0 ? 0 : shown.Count - 1) : (focus + move + shown.Count) % shown.Count;
                keyboardFocus = true;
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                 || Input.GetKeyDown(KeyCode.Space)) && focus >= 0)
            {
                Pick(focus);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.R)) Reroll();
        if (Input.GetKeyDown(KeyCode.B)) ToggleBanish();
        if (Input.GetKeyDown(KeyCode.E)) OpenBook();
        if (Input.GetKeyDown(KeyCode.Escape) && ui.banish) ToggleBanish();
    }

    // ---------- Aktionen ----------

    private void Pick(int index)
    {
        if (index < 0 || index >= shown.Count) return;
        CardView card = cards[index];
        Weapon w = shown[index];

        if (ui.banish)
        {
            // Dieselben Regeln wie LevelUpButton.SelectUpgrade - hier vorher
            // geprueft, damit die Karte selbst "nein" sagen kann.
            if (IsEvo(w))
            {
                Deny(card, Loc.Get("ui.levelup.cant_banish_evo", "Evos can't be banished"));
                return;
            }
            if (w.weaponLevel >= 0)
            {
                Deny(card, Loc.Get("ui.levelup.cant_banish_owned", "You can't banish what you own"));
                return;
            }
        }

        pendingPick = index;
        pendingAt = Time.unscaledTime + PickTime;
        card.PickedAt = Time.unscaledTime;
    }

    private void CommitPick()
    {
        int index = pendingPick;
        pendingPick = -1;

        LevelUpButton button = index < ui.levelUpButtons.Length ? ui.levelUpButtons[index] : null;
        if (button == null) return;
        button.SelectUpgrade();
    }

    private void Reroll()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        if (HasEvoOffer())
        {
            DenyButton(rerollButton, Loc.Get("ui.levelup.evo_no_reroll", "Evos can't be rerolled"));
            return;
        }
        if (p.rerollAmount <= 0)
        {
            DenyButton(rerollButton, Loc.Get("ui.levelup.no_rerolls", "No rerolls left"));
            return;
        }

        rerollButton.Bump();
        forceDeal = true;
        dealAt = Time.unscaledTime;
        ui.RerollLevelUp();
    }

    private void ToggleBanish()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        if (!ui.banish && p.banishAmount <= 0)
        {
            DenyButton(banishButton, Loc.Get("ui.levelup.no_banish", "No banishes left"));
            return;
        }

        banishButton.Bump();
        ui.BanishLevelUp();
    }

    private void OpenBook()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || p.EvoCombinations == null || p.EvoCombinations.Count == 0) return;
        if (ui.banish) ui.Panelback();
        Click();
        ui.EvoPanelOpen();
    }

    private void Deny(CardView card, string message)
    {
        card.ShakeUntil = Time.unscaledTime + 0.25f;
        ShowToast(message, true);
        Click();
    }

    private void DenyButton(ButtonView button, string message)
    {
        button.ShakeUntil = Time.unscaledTime + 0.25f;
        ShowToast(message, true);
        Click();
    }

    private void ShowToast(string message, bool bad)
    {
        toast = message;
        toastBad = bad;
        toastUntil = Time.unscaledTime + ToastTime;
    }

    private static void Click()
    {
        if (AudioController.Instance != null)
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
    }

    // ---------- Daten ----------

    /// <summary>
    /// Was die alten Knoepfe gerade anbieten: RandomWeapon schaltet Knopf i an
    /// und gibt ihm currentLevelUpWeapons[i].
    /// </summary>
    private void CollectOffered()
    {
        offered.Clear();
        LevelUpButton[] b = ui.levelUpButtons;
        List<Weapon> list = ui.currentLevelUpWeapons;
        if (b == null || list == null) return;

        for (int i = 0; i < b.Length && i < MaxCards; i++)
        {
            if (b[i] == null || !b[i].gameObject.activeSelf) break;
            if (i >= list.Count || list[i] == null) break;
            offered.Add(list[i]);
        }
    }

    private static bool SameList(List<Weapon> a, List<Weapon> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private bool HasEvoOffer()
    {
        foreach (Weapon w in shown)
            if (IsEvo(w)) return true;
        return false;
    }

    private static bool IsEvo(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null || w == null) return false;
        if (p.activeEvos != null && System.Array.IndexOf(p.activeEvos, w) >= 0) return true;
        if (p.EvoCombinations != null)
            foreach (EvoRecipe r in p.EvoCombinations)
                if (r != null && r.EvoWeapon == w) return true;
        return false;
    }

    private static Kind KindOf(Weapon w)
    {
        PlayerController p = PlayerController.Instance;
        if (IsEvo(w)) return Kind.Evo;
        if (p != null && p.activeBuffs != null && System.Array.IndexOf(p.activeBuffs, w) >= 0) return Kind.Buff;
        if (p != null && p.activeWeapon != null && System.Array.IndexOf(p.activeWeapon, w) >= 0) return Kind.Weapon;
        return Kind.Bonus;
    }

    /// <summary>
    /// Name in Grossbuchstaben: ThaleahFat zeichnet ohnehin nur Versalien, und
    /// ein Name mit Umlaut, der auf Jersey10 ausweicht, saehe sonst anders aus.
    /// </summary>
    private static string NameOf(Weapon w)
    {
        if (w == null) return "";
        string name;
        if (w is CurrencyReward) name = Loc.Get("ui.levelup.bonus.name", "Coin Pouch");
        else if (string.IsNullOrEmpty(w.weaponID)) name = w.name;
        else name = Loc.Get("weapon." + w.weaponID + ".name", w.name);
        return name.ToUpperInvariant();
    }

    private static bool Owned(Weapon w) => w != null && w.weaponLevel >= 0 && !w.hasBeenRemoved;
    private static bool Maxed(Weapon w) => w != null && w.weaponLevel >= w.maxweaponLevel;

    // ---------- Kopf ----------

    private void RefreshHead()
    {
        PlayerController p = PlayerController.Instance;

        titleText.Set(Loc.Get("ui.levelup.title", "LEVEL UP!"));
        int w = Mathf.CeilToInt(titleText.Width) + 24;
        w += w & 1;
        int x = (RefW - w) / 2;
        ribbon.sizeDelta = new Vector2(w, TitleH);
        ribbon.anchoredPosition = new Vector2(x, -TitleY);
        titleText.Resize(w, TitleH - 1);
        tailL.anchoredPosition = new Vector2(x - 8, -(TitleY + 4));
        tailR.anchoredPosition = new Vector2(x + w - 4, -(TitleY + 4));

        int level = p != null ? p.currentLevel : 1;
        badgeText.Set(Loc.Get("ui.hud.level", "LV") + " " + level);
        int bw = Mathf.CeilToInt(badgeText.Width) + 8;
        bw += bw & 1;
        badge.sizeDelta = new Vector2(bw, BadgeH);
        badge.anchoredPosition = new Vector2((RefW - bw) / 2, -BadgeY);
        badgeText.Resize(bw, BadgeH);
    }

    private void UpdateHeadAnim(float now)
    {
        float age = now - openedAt;

        // Das Band faellt in drei Stufen herein und wippt dann sacht.
        int drop = age < 0.05f ? -12 : age < 0.1f ? -6 : age < 0.15f ? 1 : 0;
        int bob = age > 0.6f && Mathf.Repeat(now * 1.2f, 1f) < 0.5f ? 1 : 0;
        int y = TitleY + drop + bob;
        SetPos(ribbon, ribbon.anchoredPosition.x, -y);
        SetPos(tailL, tailL.anchoredPosition.x, -(y + 4));
        SetPos(tailR, tailR.anchoredPosition.x, -(y + 4));
        SetPos(badge, badge.anchoredPosition.x, -(BadgeY + drop + bob));

        // Titel blinkt am Anfang zwischen Creme und Gold.
        bool flash = age < 0.6f && Mathf.Repeat(age * 8f, 1f) < 0.5f;
        titleText.SetColor(flash ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Cream);

        // Unterzeile: Meldung, Banish-Hinweis oder die normale Aufforderung.
        string text;
        Color color;
        int shake = 0;
        if (now < toastUntil)
        {
            text = toast;
            color = toastBad ? GameHudSkin.JamLight : GameHudSkin.GoldLight;
            float left = toastUntil - now;
            if (left > ToastTime - 0.25f) shake = Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1;
        }
        else if (ui.banish)
        {
            text = Loc.Get("ui.levelup.banish_hint", "Banish: pick what should vanish for this run");
            color = Mathf.Repeat(now * 2f, 1f) < 0.5f ? GameHudSkin.JamLight : GameHudSkin.Rose;
        }
        else
        {
            text = Loc.Get("ui.levelup.pick", "Choose an upgrade");
            color = GameHudSkin.Parchment;
        }
        subtitle.Set(text);
        subtitle.SetColor(color);
        subtitle.Move(shake, SubtitleY);
    }

    // ---------- Karten ----------

    private void LayoutCards()
    {
        int n = shown.Count;
        int total = n * CardW + Mathf.Max(0, n - 1) * CardGap;
        int x0 = (RefW - total) / 2;

        for (int i = 0; i < cards.Count; i++)
        {
            CardView c = cards[i];
            bool on = i < n;
            c.Root.gameObject.SetActive(on);
            if (!on) continue;
            c.Rect = new RectInt(x0 + i * (CardW + CardGap), CardsY, CardW, CardH);
            c.Fill(shown[i], ui.banish);
        }
    }

    private void AnimateCards(float now)
    {
        for (int i = 0; i < shown.Count; i++)
        {
            CardView c = cards[i];
            float t = now - dealAt - i * DealStagger;
            c.Animate(now, t, i == focus && pendingPick < 0, i == pressed, pendingPick, i, ui.banish);
        }
    }

    // ---------- Knoepfe ----------

    private void UpdateButtons(float now, Vector2 mouse, bool inputOpen)
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        bool evoOffer = HasEvoOffer();
        rerollButton.SetLabel(Loc.Get("ui.levelup.reroll", "REROLL"));
        rerollButton.SetCount(p.rerollAmount.ToString(), false);
        rerollButton.Disabled = p.rerollAmount <= 0 || evoOffer;

        banishButton.SetLabel(Loc.Get("ui.levelup.banish", "BANISH"));
        banishButton.SetCount(p.banishAmount.ToString(), false);
        banishButton.Disabled = p.banishAmount <= 0 && !ui.banish;
        banishButton.Active = ui.banish;

        int ready = CountReadyEvos(p);
        evoButton.SetLabel(Loc.Get("ui.levelup.evos", "EVOS"));
        evoButton.SetCount(ready > 0 ? ready.ToString() : "", ready > 0);
        evoButton.Disabled = p.EvoCombinations == null || p.EvoCombinations.Count == 0;

        bool blocked = pendingPick >= 0 || !inputOpen;
        foreach (ButtonView b in buttons)
        {
            bool clicked = b.Update(now, mouse, !blocked);
            if (!clicked) continue;
            if (b == rerollButton) Reroll();
            else if (b == banishButton) ToggleBanish();
            else if (b == evoButton) OpenBook();
        }
    }

    private static int CountReadyEvos(PlayerController p)
    {
        if (p.EvoCombinations == null) return 0;
        int n = 0;
        foreach (EvoRecipe r in p.EvoCombinations)
            if (r != null && r.EvoWeapon != null && r.EvoWeapon.weaponLevel < 0
                && Maxed(r.RequiredWeapon1) && Maxed(r.RequiredWeapon2)
                && Owned(r.RequiredWeapon1) && Owned(r.RequiredWeapon2))
                n++;
        return n;
    }

    // ---------- Build-Leiste ----------

    private void RefreshStrip()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        ownedWeapons.Clear();
        ownedBuffs.Clear();
        if (p.activeEvos != null)
            foreach (Weapon w in p.activeEvos) if (Owned(w)) ownedWeapons.Add(w);
        if (p.activeWeapon != null)
            foreach (Weapon w in p.activeWeapon) if (Owned(w)) ownedWeapons.Add(w);
        if (p.activeBuffs != null)
            foreach (Weapon w in p.activeBuffs) if (Owned(w)) ownedBuffs.Add(w);

        int wn = Mathf.Clamp(Mathf.Max(p.WeaponSlots, ownedWeapons.Count), 0, StripMax);
        int bn = Mathf.Clamp(Mathf.Max(p.BuffSlots, ownedBuffs.Count), 0, StripMax);

        int wWidth = wn > 0 ? wn * StripStep - (StripStep - StripTile) : 0;
        int bWidth = bn > 0 ? bn * StripStep - (StripStep - StripTile) : 0;
        int total = wWidth + bWidth + (wn > 0 && bn > 0 ? StripGroupGap : 0);
        int x = (RefW - total) / 2;

        FillStripRow(stripWeapons, ownedWeapons, wn, x, GameHudSkin.TileKind.Weapon, p.EvoSlots);
        FillStripRow(stripBuffs, ownedBuffs, bn, x + wWidth + (wn > 0 ? StripGroupGap : 0), GameHudSkin.TileKind.Buff, 0);
    }

    private void FillStripRow(List<StripTileView> views, List<Weapon> items, int count, int x,
                              GameHudSkin.TileKind kind, int evoSlots)
    {
        while (views.Count < count) views.Add(new StripTileView(this, chooseLayer));
        for (int i = 0; i < views.Count; i++)
        {
            bool on = i < count;
            views[i].Root.gameObject.SetActive(on);
            if (!on) continue;
            Weapon w = i < items.Count ? items[i] : null;
            bool evoTile = w != null ? IsEvo(w) : i < evoSlots;
            views[i].Set(x + i * StripStep, StripY, w, w == null ? GameHudSkin.TileKind.Empty
                                                   : evoTile ? GameHudSkin.TileKind.Evo : kind);
        }
    }

    /// <summary>
    /// Die Leiste zeigt, was die Karte unter dem Zeiger bewirkt: die Kachel,
    /// die aufsteigt, leuchtet gold; bei etwas Neuem blinkt es im naechsten
    /// freien Platz; eine Evo markiert die beiden Zutaten, die sie frisst.
    /// </summary>
    private void UpdateStrip(float now)
    {
        Weapon target = focus >= 0 && focus < shown.Count && pendingPick < 0 ? shown[focus] : null;
        if (pendingPick >= 0 && pendingPick < shown.Count) target = shown[pendingPick];
        bool banish = ui.banish;
        bool blink = Mathf.Repeat(now * 3f, 1f) < 0.6f;

        EvoRecipe recipe = null;
        if (target != null && IsEvo(target))
        {
            PlayerController p = PlayerController.Instance;
            if (p != null && p.EvoCombinations != null)
                foreach (EvoRecipe r in p.EvoCombinations)
                    if (r != null && r.EvoWeapon == target) { recipe = r; break; }
        }

        Kind kind = target != null ? KindOf(target) : Kind.Bonus;
        bool isNew = target != null && !Owned(target) && kind != Kind.Bonus && !banish;
        List<StripTileView> row = kind == Kind.Buff ? stripBuffs : stripWeapons;
        int ghostSlot = -1;
        if (isNew)
        {
            for (int i = 0; i < row.Count; i++)
                if (row[i].Root.gameObject.activeSelf && row[i].Item == null) { ghostSlot = i; break; }
        }

        UpdateStripRow(stripWeapons, target, recipe, banish, blink, now, row == stripWeapons ? ghostSlot : -1);
        UpdateStripRow(stripBuffs, target, recipe, banish, blink, now, row == stripBuffs ? ghostSlot : -1);
    }

    private void UpdateStripRow(List<StripTileView> views, Weapon target, EvoRecipe recipe, bool banish,
                                bool blink, float now, int ghostSlot)
    {
        for (int i = 0; i < views.Count; i++)
        {
            StripTileView v = views[i];
            if (!v.Root.gameObject.activeSelf) continue;

            Color ring = Color.clear;
            bool levelUp = false;
            Sprite ghost = null;

            if (target != null && v.Item == target && !banish)
            {
                ring = GameHudSkin.Gold;
                levelUp = true;
            }
            else if (recipe != null && (v.Item == recipe.RequiredWeapon1 || v.Item == recipe.RequiredWeapon2))
            {
                ring = GameHudSkin.JamLight;
            }
            else if (i == ghostSlot)
            {
                ring = GameHudSkin.Mint;
                ghost = target.weaponIcon;
            }

            v.Highlight(ring, blink, levelUp, ghost, now);
        }
    }

    // ==================================================================
    //  Evo-Buch
    // ==================================================================

    private List<EvoRecipe> Recipes()
    {
        var list = new List<EvoRecipe>();
        PlayerController p = PlayerController.Instance;
        if (p == null || p.EvoCombinations == null) return list;
        foreach (EvoRecipe r in p.EvoCombinations)
            if (r != null && r.EvoWeapon != null) list.Add(r);

        // Demo: die Evos, die hier ueberhaupt entstehen koennen, stehen vorn -
        // sonst in der alten Reihenfolge (stabil, darum kein List.Sort).
        if (Demo.Active)
        {
            var possible = list.FindAll(DemoPossible);
            list.RemoveAll(DemoPossible);
            list.InsertRange(0, possible);
        }
        return list;
    }

    private static bool DemoPossible(EvoRecipe r) =>
        r.RequiredWeapon1 != null && r.RequiredWeapon2 != null &&
        Demo.IsEvoPossible(r.RequiredWeapon1.weaponID, r.RequiredWeapon2.weaponID);

    private int PageCount(int n) => Mathf.Max(1, (n + EntriesPerPage - 1) / EntriesPerPage);

    private void RefreshBook()
    {
        PlayerController p = PlayerController.Instance;

        bookTitle.Set(Loc.Get("ui.evobook.title", "EVOLUTIONS"));
        int w = Mathf.CeilToInt(bookTitle.Width) + 28;
        w += w & 1;
        bookSign.sizeDelta = new Vector2(w, 24);
        bookSign.anchoredPosition = new Vector2((RefW - w) / 2, -TitleY);
        bookTitle.Resize(w, 23);
        RectTransform shadow = (RectTransform)bookLayer.Find("SignShadow");
        if (shadow != null)
        {
            shadow.sizeDelta = new Vector2(w, 24);
            shadow.anchoredPosition = new Vector2((RefW - w) / 2 + 1, -(TitleY + 1));
        }

        List<EvoRecipe> recipes = Recipes();
        int pages = PageCount(recipes.Count);
        bookPage = Mathf.Clamp(bookPage, 0, pages - 1);

        int evoSlots = p != null ? p.EvoSlots : 0;
        int evoUsed = 0;
        foreach (EvoRecipe r in recipes) if (Owned(r.EvoWeapon)) evoUsed++;
        bool slotFree = evoUsed < evoSlots;

        for (int i = 0; i < entries.Count; i++)
        {
            int idx = bookPage * EntriesPerPage + i;
            bool on = idx < recipes.Count;
            entries[i].Root.gameObject.SetActive(on);
            if (on) entries[i].Fill(recipes[idx], slotFree);
        }

        slotsText.Set(Loc.Get("ui.evobook.slots", "EVO SLOTS") + " " + evoUsed + "/" + evoSlots);
        int sw = Mathf.CeilToInt(slotsText.Width) + 10;
        sw += sw & 1;
        slotsChip.sizeDelta = new Vector2(sw, 13);
        slotsText.Resize(sw, 13);
        slotsText.SetColor(evoSlots <= 0 ? GameHudSkin.Rose : slotFree ? GameHudSkin.MintLight : GameHudSkin.JamLight);

        backButton.SetLabel(Loc.Get("ui.evobook.back", "BACK"));
        backButton.SetCount("", false);

        bool paged = pages > 1;
        prevButton.Root.gameObject.SetActive(paged);
        nextButton.Root.gameObject.SetActive(paged);
        pageText.SetActive(paged);
        pageText.Set((bookPage + 1) + "/" + pages);
        prevButton.Disabled = bookPage <= 0;
        nextButton.Disabled = bookPage >= pages - 1;
    }

    private void UpdateBook(float now, Vector2 mouse)
    {
        bool inputOpen = now >= inputFrom;

        int hover = -1;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Root.gameObject.activeSelf && Hit(entries[i].Rect, mouse)) hover = i;
        bookFocus = hover;

        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Root.gameObject.activeSelf) entries[i].Animate(now, i == bookFocus);

        // Unterzeile: unter dem Zeiger die Zutaten mit Stufe, sonst der Tipp.
        if (bookFocus >= 0) bookSubtitle.Set(entries[bookFocus].Detail());
        else bookSubtitle.Set(Loc.Get("ui.evobook.hint", "Max out both ingredients - the evo shows up on your next level up"));
        bookSubtitle.SetColor(bookFocus >= 0 ? (Color)GameHudSkin.Cream : (Color)GameHudSkin.Parchment);

        bool back = backButton.Update(now, mouse, inputOpen);
        if (prevButton.Root.gameObject.activeSelf && prevButton.Update(now, mouse, inputOpen)) Flip(-1);
        if (nextButton.Root.gameObject.activeSelf && nextButton.Update(now, mouse, inputOpen)) Flip(1);

        if (inputOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.Backspace) || Input.GetMouseButtonDown(1))
                back = true;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Flip(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Flip(1);
        }

        if (back)
        {
            Click();
            ui.EvoPanelClose();
        }
    }

    private void Flip(int dir)
    {
        int pages = PageCount(Recipes().Count);
        int next = Mathf.Clamp(bookPage + dir, 0, pages - 1);
        if (next == bookPage) return;
        bookPage = next;
        Click();
        RefreshBook();
    }

    // ==================================================================
    //  Beschreibungen
    // ==================================================================

    private struct StatLine
    {
        public string Label;
        public string Value;
        public string Short;   // nur der neue Wert, falls Value nicht in die Zeile passt
    }

    private const string DimHex = "#9c8a91";
    private const string ArrowHex = "#f2c14e";
    private const string UpHex = "#b2ebb9";
    private const string PlainHex = "#fff4e0";

    private static string Num(float v)
    {
        float r = Mathf.Round(v);
        if (Mathf.Abs(v - r) < 0.001f) return ((int)r).ToString(CultureInfo.InvariantCulture);
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Pct(float v) => Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";

    private static string Single(string value, bool up)
    {
        return "<color=" + (up ? UpHex : PlainHex) + ">" + value + "</color>";
    }

    private static string Change(string from, string to)
    {
        return "<color=" + DimHex + ">" + from + "</color> <color=" + ArrowHex + ">></color> <color=" + UpHex + ">" + to + "</color>";
    }

    private static string StatLabel(string id, string fallback) => Loc.Get("ui.levelup.stat." + id, fallback);

    /// <summary>
    /// Bis zu drei Zeilen, was die Wahl bringt. Waffen und Evos vergleichen die
    /// beiden Stufen aus <see cref="Weapon.stats"/>; ist die Waffe neu, stehen
    /// ihre Grundwerte da. Buffs lesen ihren Wert so, wie ihr Skript ihn
    /// verbucht (siehe <see cref="BuffLines"/>).
    /// </summary>
    private static List<StatLine> Describe(Weapon w, Kind kind, out string description)
    {
        var lines = new List<StatLine>();
        description = null;
        if (w == null) return lines;

        if (w is CurrencyReward)
        {
            lines.Add(new StatLine { Label = Loc.Get("ui.levelup.stat.coins", "Coins"), Value = Single("+25", true) });
            return lines;
        }

        if (w.stats == null || w.stats.Count == 0) return lines;

        int cur = w.weaponLevel;
        int next = Mathf.Clamp(cur + 1, 0, w.stats.Count - 1);
        WeaponStats to = w.stats[next];
        WeaponStats from = cur >= 0 && cur < w.stats.Count ? w.stats[cur] : null;

        if (kind == Kind.Buff) BuffLines(w, from, to, lines);
        else WeaponLines(from, to, lines);

        if (lines.Count == 0 && IsRealText(to.description)) description = to.description.Trim();
        return lines;
    }

    private static void WeaponLines(WeaponStats from, WeaponStats to, List<StatLine> lines)
    {
        if (from == null)
        {
            if (to.damage > 0f) lines.Add(new StatLine { Label = StatLabel("damage", "Damage"), Value = Single(Num(to.damage), false) });
            if (to.shots > 0f) lines.Add(new StatLine { Label = StatLabel("shots", "Projectiles"), Value = Single(Num(to.shots), false) });
            if (to.cooldown > 0f) lines.Add(new StatLine { Label = StatLabel("cooldown", "Cooldown"), Value = Single(Num(to.cooldown), false) });
            if (lines.Count < 3 && to.range > 0f) lines.Add(new StatLine { Label = StatLabel("range", "Range"), Value = Single(Num(to.range), false) });
            return;
        }

        AddDiff(lines, "damage", "Damage", from.damage, to.damage, "");
        AddDiff(lines, "shots", "Projectiles", from.shots, to.shots, "");
        AddDiff(lines, "cooldown", "Cooldown", from.cooldown, to.cooldown, "");
        AddDiff(lines, "range", "Range", from.range, to.range, "");
        AddDiff(lines, "duration", "Duration", from.duration, to.duration, "");
        AddDiff(lines, "speed", "Tick", from.AttackSpeed, to.AttackSpeed, "");
        if (lines.Count > 3) lines.RemoveRange(3, lines.Count - 3);
    }

    private static void AddDiff(List<StatLine> lines, string id, string fallback, float a, float b, string unit)
    {
        if (Mathf.Abs(a - b) < 0.0001f) return;
        lines.Add(new StatLine
        {
            Label = StatLabel(id, fallback),
            Value = Change(Num(a) + unit, Num(b) + unit),
            Short = Single(Num(b) + unit, true),
        });
    }

    private enum BuffMode { AddFlat, AddPct, AbsFlat, AbsPct, AbsPctMinus }

    /// <summary>
    /// Wie die Buffs ihre Stufen verbuchen - nachgelesen in Weapons/2Buffs:
    /// die meisten addieren stats[stufe].damage bei jedem Aufstieg (Add),
    /// Cooldown, Wirkdauer und Ruestung setzen den Wert der Stufe (Abs).
    /// </summary>
    private static readonly Dictionary<string, (string id, string fallback, BuffMode mode)> BuffSpecs =
        new Dictionary<string, (string, string, BuffMode)>
        {
            { "buff_damage",       ("damage",     "Damage",       BuffMode.AddPct) },
            { "buff_crit_chance",  ("critchance", "Crit chance",  BuffMode.AddPct) },
            { "buff_crit_damage",  ("critdamage", "Crit damage",  BuffMode.AddPct) },
            { "buff_aoe_range",    ("area",       "Area",         BuffMode.AddPct) },
            { "buff_extra_shot",   ("extrashots", "Extra shots",  BuffMode.AddFlat) },
            { "buff_max_hp",       ("maxhp",      "Max HP",       BuffMode.AddFlat) },
            { "buff_regeneration", ("regen",      "Regeneration", BuffMode.AddFlat) },
            { "buff_armor",        ("armor",      "Armor",        BuffMode.AbsFlat) },
            { "buff_dodge",        ("dodge",      "Dodge",        BuffMode.AddPct) },
            { "buff_xp_gain",      ("xp",         "XP gain",      BuffMode.AddPct) },
            { "buff_currency",     ("gold",       "Coin gain",    BuffMode.AddPct) },
            { "buff_luck",         ("luck",       "Luck",         BuffMode.AddFlat) },
            { "buff_move_speed",   ("movespeed",  "Speed",        BuffMode.AddFlat) },
            { "buff_pickup_range", ("pickup",     "Pickup range", BuffMode.AddPct) },
            { "buff_cooldown",     ("cooldown",   "Cooldown",     BuffMode.AbsPctMinus) },
            { "buff_duration",     ("duration",   "Duration",     BuffMode.AbsPct) },
        };

    private static void BuffLines(Weapon w, WeaponStats from, WeaponStats to, List<StatLine> lines)
    {
        string id = w.weaponID ?? "";

        if (id == "buff_glass_cannon")
        {
            AbsLine(lines, "damage", "Damage", from != null ? from.damage : (float?)null, to.damage, BuffMode.AbsPct);
            AbsLine(lines, "maxhp", "Max HP", from != null ? from.range : (float?)null, to.range, BuffMode.AbsPctMinus);
            return;
        }
        if (id == "buff_second_chance")
        {
            AbsLine(lines, "revives", "Revives", from != null ? from.damage : (float?)null, to.damage, BuffMode.AbsFlat);
            AbsLine(lines, "revivehp", "Revive HP", from != null ? from.range : (float?)null, to.range, BuffMode.AbsPct);
            return;
        }
        if (id == "buff_life_steal")
        {
            if (to.range > 0f) lines.Add(new StatLine { Label = StatLabel("lifesteal", "Life steal"), Value = Single("+" + Num(to.range), true) });
            if (to.damage > 0f) lines.Add(new StatLine { Label = StatLabel("lifestealpower", "Heal power"), Value = Single("+" + Pct(to.damage), true) });
            return;
        }

        if (!BuffSpecs.TryGetValue(id, out var spec)) return;

        switch (spec.mode)
        {
            case BuffMode.AddFlat:
                if (to.damage != 0f)
                    lines.Add(new StatLine { Label = StatLabel(spec.id, spec.fallback), Value = Single("+" + Num(to.damage), true) });
                break;
            case BuffMode.AddPct:
                if (to.damage != 0f)
                    lines.Add(new StatLine { Label = StatLabel(spec.id, spec.fallback), Value = Single("+" + Pct(to.damage), true) });
                break;
            default:
                AbsLine(lines, spec.id, spec.fallback, from != null ? from.damage : (float?)null, to.damage, spec.mode);
                break;
        }
    }

    private static void AbsLine(List<StatLine> lines, string id, string fallback, float? from, float to, BuffMode mode)
    {
        string Fmt(float v)
        {
            switch (mode)
            {
                case BuffMode.AbsPct: return "+" + Pct(v);
                case BuffMode.AbsPctMinus: return "-" + Pct(v);
                default: return Num(v);
            }
        }

        if (from.HasValue && Mathf.Abs(from.Value - to) < 0.0001f) return;
        lines.Add(new StatLine
        {
            Label = StatLabel(id, fallback),
            Value = from.HasValue ? Change(Fmt(from.Value), Fmt(to)) : Single(Fmt(to), true),
            Short = Single(Fmt(to), true),
        });
    }

    /// <summary>
    /// Im Prefab stehen viele Platzhalter ("XX", "GAGA", "POWER"). Echter Text
    /// hat ein Leerzeichen, eine Ziffer oder ein Prozentzeichen.
    /// </summary>
    private static bool IsRealText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.Length < 4) return false;
        if (s.Contains(" ") || s.Contains("%")) return true;
        foreach (char ch in s) if (char.IsDigit(ch)) return true;
        return false;
    }

    // ==================================================================
    //  Bausteine
    // ==================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Oben links verankert, Pixel im Raster des Elternteils.</summary>
    private static void Place(RectTransform r, RectInt px)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(px.width, px.height);
        r.anchoredPosition = new Vector2(px.x, -px.y);
    }

    private static void SetPos(RectTransform r, float x, float y)
    {
        var p = new Vector2(x, y);
        if (r.anchoredPosition != p) r.anchoredPosition = p;
    }

    private static void SetScale(RectTransform r, float s)
    {
        var v = new Vector3(s, s, 1f);
        if (r.localScale != v) r.localScale = v;
    }

    private static void SetColor(Graphic g, Color c)
    {
        if (g.color != c) g.color = c;
    }

    private static void SetActive(Component c, bool on)
    {
        if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private Image Img(string name, Transform parent, RectInt px, Sprite sprite, Color color, bool sliced = false)
    {
        RectTransform r = NewRect(name, parent);
        Place(r, px);
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }
        return img;
    }

    private PixText Text(string name, Transform parent, RectInt px, float size, Color color,
                         TextAlignmentOptions align, TextStyle style = TextStyle.Shadow, bool wrap = false)
    {
        return new PixText(this, name, parent, px, size, color, align, style, wrap);
    }

    private TextMeshProUGUI NewLabel(string name, Transform parent, RectInt px, float size, Color color,
                                     TextAlignmentOptions align, bool wrap)
    {
        RectTransform r = NewRect(name, parent);
        Place(r, px);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableAutoSizing = false;
        t.richText = true;
        t.margin = Vector4.zero;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>
    /// Text mit hartem Schatten oder 1-px-Kontur aus versetzten Kopien, wie im
    /// HUD. Alles haengt an einem eigenen Halter, damit es sich als Ganzes
    /// verschieben laesst. Farb-Tags gelten nur fuer den Vordergrund - die
    /// Kopien dahinter bekommen den Text ohne Tags und bleiben dunkel.
    /// </summary>
    private sealed class PixText
    {
        public readonly TextMeshProUGUI Main;
        public readonly RectTransform Holder;
        private readonly LevelUpScreen owner;
        private readonly List<TextMeshProUGUI> backs = new List<TextMeshProUGUI>();
        private readonly int offsetY;
        private string text;

        private static readonly Vector2Int[] ShadowOffsets = { new Vector2Int(0, 1) };
        private static readonly Vector2Int[] OutlineOffsets =
        {
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(1, 1),
        };

        public PixText(LevelUpScreen owner, string name, Transform parent, RectInt px, float size, Color color,
                       TextAlignmentOptions align, TextStyle style, bool wrap)
        {
            this.owner = owner;
            Holder = NewRect(name, parent);
            Place(Holder, px);

            Vector2Int[] offsets = style == TextStyle.Outline ? OutlineOffsets
                                 : style == TextStyle.Shadow ? ShadowOffsets
                                 : new Vector2Int[0];
            var local = new RectInt(0, 0, px.width, px.height);
            foreach (Vector2Int o in offsets)
                backs.Add(owner.NewLabel("Back", Holder, new RectInt(o.x, o.y, px.width, px.height), size,
                                         GameHudSkin.Ink, align, wrap));
            Main = owner.NewLabel("Text", Holder, local, size, color, align, wrap);
        }

        public float Width
        {
            get
            {
                Main.ForceMeshUpdate();
                return Main.preferredWidth;
            }
        }

        /// <summary>Setzt den Text; true, wenn er sich geaendert hat.</summary>
        public bool Set(string value)
        {
            value ??= "";
            if (value == text) return false;
            text = value;

            string plain = ColorTags.Replace(value, "");
            TMP_FontAsset f = PickFont(plain);
            if (f != null && Main.font != f)
            {
                Main.font = f;
                foreach (TextMeshProUGUI b in backs) b.font = f;
            }

            Main.text = value;
            foreach (TextMeshProUGUI b in backs) b.text = plain;
            return true;
        }

        private TMP_FontAsset PickFont(string value)
        {
            if (owner.font == null || string.IsNullOrEmpty(value)) return owner.font;
            if (owner.textFont == null || owner.textFont == owner.font) return owner.font;
            return owner.font.HasCharacters(value, out uint[] _, false, true) ? owner.font : owner.textFont;
        }

        public void SetColor(Color c)
        {
            if (Main.color != c) Main.color = c;
        }

        public void SetAlpha(float a)
        {
            Color c = Main.color;
            if (!Mathf.Approximately(c.a, a)) { c.a = a; Main.color = c; }
            foreach (TextMeshProUGUI b in backs)
            {
                Color bc = b.color;
                if (!Mathf.Approximately(bc.a, a)) { bc.a = a; b.color = bc; }
            }
        }

        public void Move(float x, float y) => SetPos(Holder, x, -y);

        public void Resize(float w, float h)
        {
            Holder.sizeDelta = new Vector2(w, h);
            Main.rectTransform.sizeDelta = new Vector2(w, h);
            foreach (TextMeshProUGUI b in backs) b.rectTransform.sizeDelta = new Vector2(w, h);
        }

        public void SetActive(bool on)
        {
            if (Holder.gameObject.activeSelf != on) Holder.gameObject.SetActive(on);
        }
    }

    // ==================================================================
    //  Karte
    // ==================================================================

    private sealed class CardView
    {
        public readonly RectTransform Root;
        public RectInt Rect;
        public float PickedAt = -1f, ShakeUntil;

        private readonly LevelUpScreen s;
        private readonly int index;
        private readonly CanvasGroup group;
        private readonly RectTransform body;
        private readonly Image shade, ring, flash, headMain, headHi, headLo, plate, icon, divider, dividerHi, well;
        private readonly RectTransform newTag, keyChip;
        private readonly PixText headText, newText, nameText, desc, footLabel, keyText;
        private readonly List<Image> pips = new List<Image>();
        private readonly Image pipStar;
        private readonly PixText[] labels = new PixText[3];
        private readonly PixText[] values = new PixText[3];
        private readonly Image footA, footPlus, footB, footArrow;
        private readonly Image[] sparkles = new Image[2];

        private Weapon weapon;
        private Kind kind;
        private bool banishable, banishMode, evoReady, isNew;
        private int nextPip = -1, pipCount;
        private readonly List<EvoRecipe> recipes = new List<EvoRecipe>();
        private int recipeShown = -1;

        private const int PlateX = 36, PlateY = 21;
        private const int PipY = 79;
        private const int StatY = 92, StatStep = 11;
        private const int WellY = 124, WellH = 18;

        public CardView(LevelUpScreen s, Transform parent, int index)
        {
            this.s = s;
            this.index = index;
            Root = NewRect("Card" + index, parent);
            Place(Root, new RectInt(0, CardsY, CardW, CardH));
            group = Root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            body = NewRect("Body", Root);
            Place(body, new RectInt(0, 0, CardW, CardH));

            s.Img("Frame", body, new RectInt(0, 0, CardW, CardH), GameHudSkin.Card, Color.white, true);

            headMain = s.Img("HeadMain", body, new RectInt(2, 2, CardW - 4, 13), GameHudSkin.White, Color.white);
            headHi = s.Img("HeadHi", body, new RectInt(3, 2, CardW - 6, 1), GameHudSkin.White, Color.white);
            headLo = s.Img("HeadLo", body, new RectInt(2, 14, CardW - 4, 1), GameHudSkin.White, Color.white);
            s.Img("HeadEdge", body, new RectInt(2, 15, CardW - 4, 1), GameHudSkin.White, GameHudSkin.Ink);
            headText = s.Text("Kind", body, new RectInt(6, 2, CardW - 12, 13), SizeText, GameHudSkin.Ink,
                              TextAlignmentOptions.Left, TextStyle.Plain);

            newTag = s.Img("New", body, new RectInt(CardW - 30, 3, 26, 11), GameHudSkin.Ribbon, Color.white, true).rectTransform;
            newText = s.Text("NewText", newTag, new RectInt(0, 0, 26, 11), SizeText, GameHudSkin.Cream,
                             TextAlignmentOptions.Center, TextStyle.Shadow);

            plate = s.Img("Plate", body, new RectInt(PlateX, PlateY, 40, 40), GameHudSkin.IconPlate(false), Color.white);
            icon = s.Img("Icon", body, new RectInt(PlateX + 4, PlateY + 4, 32, 32), null, Color.white);
            icon.preserveAspect = true;

            for (int i = 0; i < 2; i++)
            {
                sparkles[i] = s.Img("Sparkle" + i, body, new RectInt(0, 0, 3, 3), GameHudSkin.Sparkle, Color.white);
                sparkles[i].enabled = false;
            }

            nameText = s.Text("Name", body, new RectInt(3, 63, CardW - 6, 12), SizeText, GameHudSkin.Cream,
                              TextAlignmentOptions.Center, TextStyle.Outline);

            for (int i = 0; i < 8; i++)
                pips.Add(s.Img("Pip" + i, body, new RectInt(0, PipY, 5, 5), GameHudSkin.Pip(GameHudSkin.PipKind.Empty), Color.white));
            pipStar = s.Img("PipStar", body, new RectInt(0, PipY - 2, 9, 9), GameHudSkin.Star, Color.white);

            Color line = GameHudSkin.Ink; line.a = 0.8f;
            divider = s.Img("Divider", body, new RectInt(8, 88, CardW - 16, 1), GameHudSkin.White, line);
            Color lineHi = GameHudSkin.Cream; lineHi.a = 0.06f;
            dividerHi = s.Img("DividerHi", body, new RectInt(8, 89, CardW - 16, 1), GameHudSkin.White, lineHi);

            for (int i = 0; i < 3; i++)
            {
                labels[i] = s.Text("Label" + i, body, new RectInt(7, StatY + i * StatStep, 60, 11), SizeText,
                                   GameHudSkin.ParchDark, TextAlignmentOptions.Left, TextStyle.Shadow);
                values[i] = s.Text("Value" + i, body, new RectInt(38, StatY + i * StatStep, CardW - 45, 11), SizeText,
                                   GameHudSkin.Cream, TextAlignmentOptions.Right, TextStyle.Shadow);
            }
            desc = s.Text("Desc", body, new RectInt(6, StatY, CardW - 12, 32), SizeText, GameHudSkin.Parchment,
                          TextAlignmentOptions.Center, TextStyle.Shadow, true);

            well = s.Img("Well", body, new RectInt(4, WellY, CardW - 8, WellH), GameHudSkin.Well, Color.white, true);
            footLabel = s.Text("FootLabel", body, new RectInt(0, WellY + 1, 40, 16), SizeText, GameHudSkin.JamLight,
                               TextAlignmentOptions.Left, TextStyle.Shadow);
            footA = s.Img("FootA", body, new RectInt(0, WellY + 1, 16, 16), null, Color.white);
            footPlus = s.Img("FootPlus", body, new RectInt(0, WellY + 5, 7, 7), GameHudSkin.Plus, Color.white);
            footB = s.Img("FootB", body, new RectInt(0, WellY + 1, 16, 16), null, Color.white);
            footArrow = s.Img("FootArrow", body, new RectInt(0, WellY + 5, 9, 7), GameHudSkin.Arrow, Color.white);
            footA.preserveAspect = footB.preserveAspect = true;

            // Gesperrt (Banish): deckend bleiben, nur abdunkeln - sonst scheint das Spielfeld durch.
            shade = s.Img("Shade", body, new RectInt(1, 1, CardW - 2, CardH - 2), GameHudSkin.White, Color.clear);
            ring = s.Img("Ring", body, new RectInt(0, 0, CardW, CardH), GameHudSkin.Ring, Color.clear, true);
            flash = s.Img("Flash", body, new RectInt(1, 1, CardW - 2, CardH - 2), GameHudSkin.White, new Color(1f, 1f, 1f, 0f));

            keyChip = s.Img("Key", Root, new RectInt(CardW / 2 - 5, CardH - 5, 10, 10), GameHudSkin.LevelChip,
                            Color.white, true).rectTransform;
            keyText = s.Text("KeyText", keyChip, new RectInt(0, 0, 10, 10), SizeText, GameHudSkin.Parchment,
                             TextAlignmentOptions.Center, TextStyle.Plain);
            keyText.Set((index + 1).ToString());
        }

        public void Fill(Weapon w, bool banish)
        {
            weapon = w;
            kind = KindOf(w);
            isNew = !Owned(w) && kind != Kind.Bonus && kind != Kind.Evo;
            banishMode = banish;
            banishable = kind != Kind.Evo && w.weaponLevel < 0;
            PickedAt = -1f;

            // ---- Kopf ----
            Color main, hi, lo, textColor;
            string head;
            if (banish && banishable)
            {
                main = GameHudSkin.Jam; hi = GameHudSkin.JamLight; lo = GameHudSkin.JamDark; textColor = GameHudSkin.Cream;
                head = Loc.Get("ui.levelup.kind.banish", "BANISH?");
            }
            else if (banish)
            {
                main = GameHudSkin.Stone; hi = GameHudSkin.StoneLight; lo = GameHudSkin.StoneDark; textColor = GameHudSkin.Parchment;
                head = Loc.Get("ui.levelup.kind.kept", "STAYS");
            }
            else
            {
                switch (kind)
                {
                    case Kind.Buff:
                        main = GameHudSkin.Mint; hi = GameHudSkin.MintLight; lo = GameHudSkin.MintDark; textColor = GameHudSkin.Ink;
                        head = Loc.Get("ui.levelup.kind.buff", "BUFF");
                        break;
                    case Kind.Evo:
                        main = GameHudSkin.Jam; hi = GameHudSkin.JamLight; lo = GameHudSkin.JamDark; textColor = GameHudSkin.Cream;
                        head = Loc.Get("ui.levelup.kind.evo", "EVOLUTION");
                        break;
                    case Kind.Bonus:
                        main = GameHudSkin.ParchMid; hi = GameHudSkin.Cream; lo = GameHudSkin.ParchDark; textColor = GameHudSkin.Ink;
                        head = Loc.Get("ui.levelup.kind.bonus", "BONUS");
                        break;
                    default:
                        main = GameHudSkin.Gold; hi = GameHudSkin.GoldLight; lo = GameHudSkin.GoldDark; textColor = GameHudSkin.Ink;
                        head = Loc.Get("ui.levelup.kind.weapon", "WEAPON");
                        break;
                }
            }
            headMain.color = main;
            headHi.color = hi;
            headLo.color = lo;
            headText.Set(head);
            headText.SetColor(textColor);

            bool showNew = isNew && !banish;
            SetActive(newTag, showNew);
            if (showNew)
            {
                newText.Set(Loc.Get("ui.levelup.new", "NEW"));
                int nw = Mathf.CeilToInt(newText.Width) + 8;
                newTag.sizeDelta = new Vector2(nw, 11);
                newTag.anchoredPosition = new Vector2(CardW - 4 - nw, -3);
                newText.Resize(nw, 11);
            }

            // ---- Symbol und Name ----
            plate.sprite = GameHudSkin.IconPlate(kind == Kind.Evo);
            icon.sprite = w.weaponIcon != null ? w.weaponIcon : w.weaponImage;
            icon.enabled = icon.sprite != null;
            nameText.Set(NameOf(w));

            // ---- Stufen ----
            pipCount = kind == Kind.Evo || kind == Kind.Bonus ? 0 : Mathf.Clamp(w.maxweaponLevel + 1, 0, pips.Count);
            nextPip = Mathf.Clamp(w.weaponLevel + 1, 0, Mathf.Max(0, pipCount - 1));
            bool nextIsMax = pipCount > 0 && w.weaponLevel + 1 >= w.maxweaponLevel;
            int pw = pipCount * 7 - 2;
            int px0 = (CardW - pw) / 2;
            for (int i = 0; i < pips.Count; i++)
            {
                bool on = i < pipCount && !(nextIsMax && i == nextPip);
                SetActive(pips[i], on);
                if (!on) continue;
                SetPos(pips[i].rectTransform, px0 + i * 7, -PipY);
                pips[i].sprite = GameHudSkin.Pip(i < nextPip ? GameHudSkin.PipKind.Full : GameHudSkin.PipKind.Empty);
            }
            SetActive(pipStar, nextIsMax && pipCount > 0);
            if (nextIsMax) SetPos(pipStar.rectTransform, px0 + nextPip * 7 - 2, -(PipY - 2));

            // Evo und Bonus haben keine Stufen: Werte ruecken hoch.
            int statY = pipCount > 0 ? StatY : StatY - 8;
            SetPos(divider.rectTransform, 8, -(statY - 4));
            SetPos(dividerHi.rectTransform, 8, -(statY - 3));

            // ---- Werte ----
            List<StatLine> lines = Describe(w, kind, out string description);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < lines.Count;
                labels[i].SetActive(on);
                values[i].SetActive(on);
                if (!on) continue;
                labels[i].Move(7, statY + i * StatStep);
                values[i].Move(38, statY + i * StatStep);
                labels[i].Set(lines[i].Label);
                values[i].Set(lines[i].Value);

                // Zu lang fuer eine Zeile ("Max. Leben -12% > -20%")? Dann nur
                // der neue Wert - der alte steht ohnehin im Pausenmenue.
                if (lines[i].Short != null && labels[i].Width + values[i].Width + 4 > CardW - 14)
                    values[i].Set(lines[i].Short);
            }
            desc.SetActive(description != null);
            if (description != null)
            {
                desc.Move(6, statY);
                desc.Set(description);
            }

            // ---- Evo-Hinweis ----
            recipes.Clear();
            PlayerController p = PlayerController.Instance;
            if (p != null && p.EvoCombinations != null)
            {
                foreach (EvoRecipe r in p.EvoCombinations)
                {
                    if (r == null || r.EvoWeapon == null) continue;
                    if (kind == Kind.Evo ? r.EvoWeapon == w
                        : (r.RequiredWeapon1 == w || r.RequiredWeapon2 == w) && !Owned(r.EvoWeapon) && !r.EvoWeapon.hasBeenRemoved)
                        recipes.Add(r);
                }
            }
            recipeShown = -1;
            ShowRecipe(0);
        }

        /// <summary>Fusszeile: Evo-Partner der Waffe, bzw. bei einer Evo die beiden Zutaten.</summary>
        private void ShowRecipe(int i)
        {
            bool on = recipes.Count > 0 && !banishMode;
            if (!on)
            {
                SetFooter(false);
                evoReady = false;
                recipeShown = -1;
                return;
            }

            i %= recipes.Count;
            if (i == recipeShown) return;
            recipeShown = i;
            SetFooter(true);
            EvoRecipe r = recipes[i];

            if (kind == Kind.Evo)
            {
                footLabel.Set(Loc.Get("ui.levelup.replaces", "REPLACES"));
                footLabel.SetColor(GameHudSkin.Parchment);
                footA.sprite = r.RequiredWeapon1 != null ? r.RequiredWeapon1.weaponIcon : null;
                footB.sprite = r.RequiredWeapon2 != null ? r.RequiredWeapon2.weaponIcon : null;
                footA.color = footB.color = Color.white;
                SetActive(footArrow, false);
                LayoutFooter(new Component[] { footLabel.Holder, footA, footPlus, footB });
                return;
            }

            Weapon partner = r.RequiredWeapon1 == weapon ? r.RequiredWeapon2 : r.RequiredWeapon1;
            bool willMax = weapon.weaponLevel + 1 >= weapon.maxweaponLevel;
            evoReady = willMax && Owned(partner) && Maxed(partner);

            footLabel.Set(evoReady ? Loc.Get("ui.levelup.evo_ready", "EVO!") : Loc.Get("ui.levelup.evo", "EVO"));
            footLabel.SetColor(evoReady ? GameHudSkin.GoldLight : GameHudSkin.JamLight);
            footA.sprite = partner != null ? partner.weaponIcon : null;
            footA.color = Owned(partner) ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            footB.sprite = r.EvoWeapon.weaponIcon;
            footB.color = Color.white;
            SetActive(footB, true);
            LayoutFooter(new Component[] { footLabel.Holder, footPlus, footA, footArrow, footB });
        }

        private void SetFooter(bool on)
        {
            SetActive(well, on);
            footLabel.SetActive(on);
            SetActive(footA, on);
            SetActive(footPlus, on);
            SetActive(footB, on);
            SetActive(footArrow, on);
        }

        private void LayoutFooter(Component[] items)
        {
            const int gap = 3;
            int total = 0;
            var widths = new int[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == footLabel.Holder)
                {
                    widths[i] = Mathf.CeilToInt(footLabel.Width);
                    footLabel.Resize(widths[i] + 2, 16);
                }
                else widths[i] = Mathf.RoundToInt(((RectTransform)items[i].transform).sizeDelta.x);
                total += widths[i] + (i > 0 ? gap : 0);
            }

            int x = (CardW - total) / 2;
            for (int i = 0; i < items.Length; i++)
            {
                var r = (RectTransform)items[i].transform;
                float h = r.sizeDelta.y;
                int y = WellY + (WellH - Mathf.RoundToInt(h)) / 2;
                SetPos(r, x, -y);
                x += widths[i] + gap;
            }
        }

        public void Animate(float now, float dealT, bool focused, bool held, int picked, int myIndex, bool banish)
        {
            // ---- Austeilen: von unten in Pixelstufen, dazu einblenden ----
            int rise;
            float alpha;
            if (dealT < 0f) { rise = 24; alpha = 0f; }
            else if (dealT < DealTime)
            {
                float k = dealT / DealTime;
                rise = k < 0.34f ? 16 : k < 0.67f ? 6 : -2;
                alpha = k < 0.34f ? 0.35f : k < 0.67f ? 0.75f : 1f;
            }
            else { rise = 0; alpha = 1f; }

            bool dim = banishMode && !banishable;
            Color sc = GameHudSkin.Night;
            sc.a = dim ? 0.6f : 0f;
            SetColor(shade, sc);

            // ---- Auswahl: gewaehlte Karte blitzt, die anderen treten zurueck ----
            bool isPicked = picked == myIndex;
            if (picked >= 0 && !isPicked) alpha *= 0.3f;

            int lift = (focused || isPicked) ? 3 : 0;
            if (held && focused) lift = 1;

            int shake = now < ShakeUntil ? (Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1) : 0;

            SetPos(Root, Rect.x + shake, -(Rect.y + rise - lift));
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;

            // ---- Rahmen ----
            Color ringColor = Color.clear;
            if (isPicked) ringColor = GameHudSkin.Cream;
            else if (focused) ringColor = banish ? (dim ? (Color)GameHudSkin.StoneLight : (Color)GameHudSkin.JamLight)
                                        : kind == Kind.Evo ? (Color)GameHudSkin.JamLight
                                        : (Color)GameHudSkin.GoldLight;
            else if (kind == Kind.Evo || evoReady)
            {
                // Evos pulsieren auch ohne Zeiger ganz leise.
                Color c = kind == Kind.Evo ? (Color)GameHudSkin.Jam : (Color)GameHudSkin.Gold;
                c.a = Mathf.Repeat(now * 1.5f, 1f) < 0.5f ? 0.55f : 0.25f;
                ringColor = c;
            }
            SetColor(ring, ringColor);

            float f = isPicked ? Mathf.Clamp01(1f - (now - PickedAt) / PickTime) : 0f;
            SetColor(flash, new Color(1f, 1f, 1f, Mathf.Round(f * 3f) / 3f * 0.7f));

            // ---- Symbol wippt unter dem Zeiger ----
            int bob = focused && Mathf.Repeat(now * 2.5f, 1f) < 0.5f ? -1 : 0;
            SetPos(icon.rectTransform, PlateX + 4, -(PlateY + 4 + bob));

            if (newTag.gameObject.activeSelf)
            {
                int tagBob = Mathf.Repeat(now * 1.6f + index * 0.3f, 1f) < 0.5f ? 0 : 1;
                SetPos(newTag, newTag.anchoredPosition.x, -(3 + tagBob));
            }

            // ---- Naechste Stufe blinkt ----
            if (pipCount > 0 && nextPip < pips.Count && pips[nextPip].gameObject.activeSelf)
            {
                bool on = Mathf.Repeat(now * 3f, 1f) < 0.55f;
                pips[nextPip].sprite = GameHudSkin.Pip(on ? GameHudSkin.PipKind.Next : GameHudSkin.PipKind.Full);
            }
            if (pipStar.gameObject.activeSelf)
            {
                int starBob = Mathf.Repeat(now * 2f, 1f) < 0.5f ? 0 : 1;
                SetPos(pipStar.rectTransform, pipStar.rectTransform.anchoredPosition.x, -(PipY - 2 - starBob));
            }

            // ---- Mehrere Evo-Partner: alle 2,5 s weiter ----
            if (recipes.Count > 1) ShowRecipe(Mathf.FloorToInt(now / 2.5f));

            UpdateSparkles(now);

            keyText.SetColor(focused ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Parchment);
        }

        private void UpdateSparkles(float now)
        {
            bool on = (kind == Kind.Evo || evoReady) && !banishMode;
            for (int i = 0; i < sparkles.Length; i++)
            {
                float cycle = now * 0.9f + i * 0.5f + index * 0.21f;
                bool vis = on && Mathf.Repeat(cycle, 1f) < 0.25f;
                sparkles[i].enabled = vis;
                if (!vis) continue;
                int corner = (Mathf.FloorToInt(cycle) + i * 2) % 4;
                Vector2Int pos = corner == 0 ? new Vector2Int(PlateX - 2, PlateY - 1)
                               : corner == 1 ? new Vector2Int(PlateX + 39, PlateY + 3)
                               : corner == 2 ? new Vector2Int(PlateX + 38, PlateY + 37)
                               : new Vector2Int(PlateX - 1, PlateY + 34);
                SetPos(sparkles[i].rectTransform, pos.x, -pos.y);
            }
        }
    }

    // ==================================================================
    //  Knopf
    // ==================================================================

    private sealed class ButtonView
    {
        public readonly RectTransform Root;
        public bool Disabled, Active;
        public float ShakeUntil;

        private readonly RectInt rect;
        private readonly Image bg, icon;
        private readonly RectTransform countChip, keyChip;
        private readonly PixText label, countText, keyText;
        private readonly bool iconOnly;
        private string lastLabel, lastCount;
        private bool countHot;
        private bool down;
        private float bumpUntil;

        public ButtonView(LevelUpScreen s, Transform parent, RectInt r, Sprite iconSprite, string key)
        {
            rect = r;
            iconOnly = iconSprite != null && r.width < 40;

            Root = NewRect("Button", parent);
            Place(Root, r);
            bg = s.Img("Bg", Root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Button(GameHudSkin.ButtonLook.Wood),
                       Color.white, true);

            if (iconSprite != null)
            {
                Rect sr = iconSprite.rect;
                int iw = Mathf.RoundToInt(sr.width), ih = Mathf.RoundToInt(sr.height);
                int ix = iconOnly ? (r.width - iw) / 2 : 7;
                icon = s.Img("Icon", Root, new RectInt(ix, (r.height - 1 - ih) / 2, iw, ih), iconSprite, Color.white);
            }

            int lx = iconSprite != null && !iconOnly ? 19 : 0;
            label = s.Text("Label", Root, new RectInt(lx, 0, r.width - lx, r.height - 1), SizeText, GameHudSkin.Cream,
                           iconSprite != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, TextStyle.Shadow);

            countChip = s.Img("Count", Root, new RectInt(r.width - 18, 5, 12, 10), GameHudSkin.LevelChip, Color.white, true).rectTransform;
            countText = s.Text("CountText", countChip, new RectInt(0, 0, 12, 10), SizeText, GameHudSkin.Cream,
                               TextAlignmentOptions.Center, TextStyle.Plain);
            countChip.gameObject.SetActive(false);

            if (!string.IsNullOrEmpty(key))
            {
                int kw = key.Length > 1 ? 20 : 10;
                keyChip = s.Img("Key", Root, new RectInt((r.width - kw) / 2, r.height - 4, kw, 10), GameHudSkin.LevelChip,
                                Color.white, true).rectTransform;
                keyText = s.Text("KeyText", keyChip, new RectInt(0, 0, kw, 10), SizeText, GameHudSkin.Parchment,
                                 TextAlignmentOptions.Center, TextStyle.Plain);
                keyText.Set(key);
            }
        }

        public void FlipIcon()
        {
            if (icon != null) icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            if (icon != null) icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            if (icon != null) icon.rectTransform.anchoredPosition += new Vector2(icon.rectTransform.sizeDelta.x * 0.5f, 0f);
        }

        public void SetLabel(string text)
        {
            if (text == lastLabel) return;
            lastLabel = text;
            label.Set(text);
        }

        public void SetCount(string text, bool hot)
        {
            countHot = hot;
            if (text == lastCount) return;
            lastCount = text;
            bool on = !string.IsNullOrEmpty(text);
            countChip.gameObject.SetActive(on);
            if (!on) return;
            countText.Set(text);
            int w = Mathf.Max(10, Mathf.CeilToInt(countText.Width) + 5);
            countChip.sizeDelta = new Vector2(w, 10);
            countChip.anchoredPosition = new Vector2(rect.width - 6 - w, -5);
            countText.Resize(w, 10);
        }

        public void Bump() => bumpUntil = Time.unscaledTime + 0.12f;

        /// <summary>Zeichnet den Knopf und meldet true, wenn er geklickt wurde.</summary>
        public bool Update(float now, Vector2 mouse, bool enabled)
        {
            bool hover = enabled && Hit(rect, mouse);
            bool clicked = false;

            if (hover && Input.GetMouseButtonDown(0)) down = true;
            if (Input.GetMouseButtonUp(0))
            {
                if (down && hover) clicked = true;
                down = false;
            }
            if (!enabled) down = false;

            GameHudSkin.ButtonLook look = Disabled ? GameHudSkin.ButtonLook.Disabled
                                        : Active ? GameHudSkin.ButtonLook.Active
                                        : (down && hover) || now < bumpUntil ? GameHudSkin.ButtonLook.Pressed
                                        : hover ? GameHudSkin.ButtonLook.Hover
                                        : GameHudSkin.ButtonLook.Wood;
            Sprite sp = GameHudSkin.Button(look);
            if (bg.sprite != sp) bg.sprite = sp;

            int press = look == GameHudSkin.ButtonLook.Pressed ? 1 : 0;
            int shake = now < ShakeUntil ? (Mathf.FloorToInt(now * 30f) % 2 == 0 ? 1 : -1) : 0;
            SetPos(Root, rect.x + shake, -(rect.y + press));

            label.SetColor(Disabled ? (Color)GameHudSkin.StoneLight : hover ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Cream);
            if (icon != null) SetColor(icon, Disabled ? new Color(1f, 1f, 1f, 0.45f) : Color.white);
            if (countChip.gameObject.activeSelf)
            {
                bool blink = countHot && Mathf.Repeat(now * 2.5f, 1f) < 0.5f;
                countText.SetColor(Disabled ? (Color)GameHudSkin.StoneLight
                                 : countHot ? (blink ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Gold)
                                 : (Color)GameHudSkin.Cream);
            }
            if (keyText != null) keyText.SetColor(hover ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Parchment);

            // Auch gesperrt melden: Reroll und Banish sagen dann selbst, warum nicht.
            return clicked;
        }
    }

    // ==================================================================
    //  Kachel in der Build-Leiste
    // ==================================================================

    private sealed class StripTileView
    {
        public readonly RectTransform Root;
        public Weapon Item { get; private set; }

        private readonly Image tile, icon, ring, star;
        private readonly RectTransform chip;
        private readonly PixText level;
        private GameHudSkin.TileKind kind = (GameHudSkin.TileKind)(-1);
        private int x, y;

        public StripTileView(LevelUpScreen s, Transform parent)
        {
            Root = NewRect("StripTile", parent);
            Place(Root, new RectInt(0, StripY, StripTile, StripTile));
            tile = s.Img("Tile", Root, new RectInt(0, 0, StripTile, StripTile), null, Color.white);
            icon = s.Img("Icon", Root, new RectInt(3, 2, 16, 16), null, Color.white);
            icon.preserveAspect = true;
            ring = s.Img("Ring", Root, new RectInt(-1, -1, StripTile + 2, StripTile + 2), GameHudSkin.Ring, Color.clear, true);
            chip = s.Img("Chip", Root, new RectInt(StripTile - 6, StripTile - 8, 8, 10), GameHudSkin.LevelChip, Color.white, true).rectTransform;
            level = s.Text("Level", chip, new RectInt(0, 0, 8, 10), SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center, TextStyle.Plain);
            star = s.Img("Max", Root, new RectInt(StripTile - 8, StripTile - 8, 9, 9), GameHudSkin.Star, Color.white);
        }

        public void Set(int px, int py, Weapon w, GameHudSkin.TileKind k)
        {
            x = px; y = py;
            SetPos(Root, x, -y);
            Item = w;
            if (k != kind)
            {
                kind = k;
                tile.sprite = GameHudSkin.Tile(StripTile, k);
            }
            icon.sprite = w != null ? w.weaponIcon : null;
            icon.enabled = icon.sprite != null;
            SetColor(icon, Color.white);
            ShowLevel(w, false);
        }

        private void ShowLevel(Weapon w, bool preview)
        {
            bool counts = w != null && w.maxweaponLevel > 0;
            int lvl = w != null ? w.weaponLevel + (preview ? 1 : 0) : 0;
            bool maxed = counts && lvl >= w.maxweaponLevel;
            SetActive(star, maxed);
            bool number = counts && !maxed;
            SetActive(chip, number);
            if (!number) return;
            if (level.Set((lvl + 1).ToString()))
            {
                int cw = Mathf.CeilToInt(level.Width) + 4;
                chip.sizeDelta = new Vector2(cw, 10);
                chip.anchoredPosition = new Vector2(StripTile + 2 - cw, -(StripTile - 8));
                level.Resize(cw, 10);
            }
            level.SetColor(preview ? (Color)GameHudSkin.GoldLight : (Color)GameHudSkin.Cream);
        }

        public void Highlight(Color ringColor, bool blink, bool levelUp, Sprite ghost, float now)
        {
            if (ringColor.a > 0f && !blink) ringColor.a *= 0.45f;
            SetColor(ring, ringColor);

            if (ghost != null)
            {
                icon.sprite = ghost;
                icon.enabled = true;
                SetColor(icon, new Color(1f, 1f, 1f, blink ? 0.7f : 0.35f));
            }
            else if (Item == null && icon.enabled)
            {
                icon.enabled = false;
                icon.sprite = null;
            }

            if (Item != null) ShowLevel(Item, levelUp && blink);
            int lift = levelUp || ghost != null ? 1 : 0;
            SetPos(Root, x, -(y - lift));
        }
    }

    // ==================================================================
    //  Eintrag im Evo-Buch
    // ==================================================================

    private sealed class EntryView
    {
        public readonly RectTransform Root;
        public readonly RectInt Rect;

        private readonly CanvasGroup group;
        private readonly Image ring, iconEvo, plus, arrow, check;
        private readonly Image[] sparkles = new Image[2];
        private readonly PixText name, status;
        private readonly IngredientView a, b;
        private EvoRecipe recipe;
        private bool ready, active;

        public EntryView(LevelUpScreen s, Transform parent, RectInt r)
        {
            Rect = r;
            Root = NewRect("Entry", parent);
            Place(Root, r);
            group = Root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            s.Img("Frame", Root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Card, Color.white, true);

            a = new IngredientView(s, Root, 8, 9);
            plus = s.Img("Plus", Root, new RectInt(33, 16, 7, 7), GameHudSkin.Plus, Color.white);
            b = new IngredientView(s, Root, 43, 9);
            arrow = s.Img("Arrow", Root, new RectInt(69, 16, 9, 7), GameHudSkin.Arrow, Color.white);

            s.Img("EvoTile", Root, new RectInt(82, 7, 26, 26), GameHudSkin.Tile(26, GameHudSkin.TileKind.Evo), Color.white);
            iconEvo = s.Img("EvoIcon", Root, new RectInt(87, 11, 16, 16), null, Color.white);
            iconEvo.preserveAspect = true;
            check = s.Img("Check", Root, new RectInt(100, 3, 9, 8), GameHudSkin.Check, Color.white);

            for (int i = 0; i < 2; i++)
            {
                sparkles[i] = s.Img("Sparkle" + i, Root, new RectInt(0, 0, 3, 3), GameHudSkin.Sparkle, Color.white);
                sparkles[i].enabled = false;
            }

            name = s.Text("Name", Root, new RectInt(114, 7, r.width - 118, 12), SizeText, GameHudSkin.Cream,
                          TextAlignmentOptions.Left, TextStyle.Outline);
            status = s.Text("Status", Root, new RectInt(114, 21, r.width - 118, 12), SizeText, GameHudSkin.Parchment,
                            TextAlignmentOptions.Left, TextStyle.Shadow);

            ring = s.Img("Ring", Root, new RectInt(0, 0, r.width, r.height), GameHudSkin.Ring, Color.clear, true);
        }

        public void Fill(EvoRecipe r, bool slotFree)
        {
            recipe = r;
            Weapon evo = r.EvoWeapon;
            a.Set(r.RequiredWeapon1);
            b.Set(r.RequiredWeapon2);
            iconEvo.sprite = evo.weaponIcon;
            iconEvo.enabled = iconEvo.sprite != null;
            name.Set(NameOf(evo));

            active = Owned(evo);
            bool maxA = Owned(r.RequiredWeapon1) && Maxed(r.RequiredWeapon1);
            bool maxB = Owned(r.RequiredWeapon2) && Maxed(r.RequiredWeapon2);
            bool gone = !active && (evo.hasBeenRemoved
                        || (r.RequiredWeapon1 != null && r.RequiredWeapon1.hasBeenRemoved)
                        || (r.RequiredWeapon2 != null && r.RequiredWeapon2.hasBeenRemoved));
            ready = !active && !gone && maxA && maxB;

            string text;
            Color color;
            if (active)
            {
                text = Loc.Get("ui.evobook.active", "ACTIVE");
                color = GameHudSkin.MintLight;
            }
            else if (gone)
            {
                text = Loc.Get("ui.evobook.banished", "BANISHED");
                color = GameHudSkin.StoneLight;
            }
            else if (ready && slotFree)
            {
                text = Loc.Get("ui.evobook.ready", "READY!");
                color = GameHudSkin.GoldLight;
            }
            else if (ready)
            {
                text = Loc.Get("ui.evobook.no_slot", "NO EVO SLOT");
                color = GameHudSkin.JamLight;
            }
            else
            {
                int n = (maxA ? 1 : 0) + (maxB ? 1 : 0);
                text = n + "/2 " + Loc.Get("ui.evobook.maxed", "MAXED");
                color = GameHudSkin.ParchDark;
            }
            status.Set(text);
            status.SetColor(color);

            check.enabled = active;
            float dim = gone ? 0.45f : 1f;
            group.alpha = dim;
            float used = active ? 0.35f : 1f;
            a.SetFade(used);
            b.SetFade(used);
            SetColor(plus, new Color(1f, 1f, 1f, used));
            SetColor(arrow, new Color(1f, 1f, 1f, used));
        }

        public string Detail()
        {
            if (recipe == null) return "";
            return Part(recipe.RequiredWeapon1) + "  +  " + Part(recipe.RequiredWeapon2);
        }

        private static string Part(Weapon w)
        {
            if (w == null) return "?";
            string lvl = Owned(w) ? (w.weaponLevel + 1) + "/" + (w.maxweaponLevel + 1)
                                  : Loc.Get("ui.evobook.missing", "missing");
            return NameOf(w) + " (" + lvl + ")";
        }

        public void Animate(float now, bool focused)
        {
            Color rc = Color.clear;
            if (focused) rc = GameHudSkin.GoldLight;
            else if (ready)
            {
                rc = GameHudSkin.Gold;
                rc.a = Mathf.Repeat(now * 1.5f, 1f) < 0.5f ? 0.7f : 0.3f;
            }
            SetColor(ring, rc);

            int lift = focused ? 1 : 0;
            SetPos(Root, Rect.x, -(Rect.y - lift));

            for (int i = 0; i < sparkles.Length; i++)
            {
                float cycle = now * 0.8f + i * 0.5f + Rect.x * 0.01f + Rect.y * 0.013f;
                bool vis = (ready || active) && Mathf.Repeat(cycle, 1f) < 0.22f;
                sparkles[i].enabled = vis;
                if (!vis) continue;
                int corner = (Mathf.FloorToInt(cycle) + i) % 3;
                Vector2Int pos = corner == 0 ? new Vector2Int(81, 5) : corner == 1 ? new Vector2Int(106, 30) : new Vector2Int(105, 6);
                SetPos(sparkles[i].rectTransform, pos.x, -pos.y);
            }
        }
    }

    /// <summary>Zutat im Evo-Buch: Kachel, Symbol, Stufe bzw. Stern.</summary>
    private sealed class IngredientView
    {
        public readonly Image Icon;
        private readonly Image tile, star;
        private readonly RectTransform chip;
        private readonly PixText level;
        private readonly int x, y;

        public IngredientView(LevelUpScreen s, Transform parent, int x, int y)
        {
            this.x = x; this.y = y;
            tile = s.Img("Tile", parent, new RectInt(x, y, StripTile, StripTile), GameHudSkin.Tile(StripTile, GameHudSkin.TileKind.Weapon), Color.white);
            Icon = s.Img("Icon", parent, new RectInt(x + 3, y + 2, 16, 16), null, Color.white);
            Icon.preserveAspect = true;
            chip = s.Img("Chip", parent, new RectInt(x + StripTile - 6, y + StripTile - 8, 8, 10), GameHudSkin.LevelChip, Color.white, true).rectTransform;
            level = s.Text("Level", chip, new RectInt(0, 0, 8, 10), SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center, TextStyle.Plain);
            star = s.Img("Max", parent, new RectInt(x + StripTile - 8, y + StripTile - 8, 9, 9), GameHudSkin.Star, Color.white);
        }

        public void Set(Weapon w)
        {
            bool owned = Owned(w);
            GameHudSkin.TileKind k = !owned ? GameHudSkin.TileKind.Empty : IsBuff(w) ? GameHudSkin.TileKind.Buff : GameHudSkin.TileKind.Weapon;
            tile.sprite = GameHudSkin.Tile(StripTile, k);
            Icon.sprite = w != null ? w.weaponIcon : null;
            Icon.enabled = Icon.sprite != null;
            SetColor(Icon, owned ? Color.white : new Color(1f, 1f, 1f, 0.4f));

            bool maxed = owned && Maxed(w);
            SetActive(star, maxed);
            bool number = owned && !maxed;
            SetActive(chip, number);
            if (!number) return;
            level.Set((w.weaponLevel + 1).ToString());
            int cw = Mathf.CeilToInt(level.Width) + 4;
            chip.sizeDelta = new Vector2(cw, 10);
            chip.anchoredPosition = new Vector2(x + StripTile + 2 - cw, -(y + StripTile - 8));
            level.Resize(cw, 10);
        }

        public void SetFade(float a)
        {
            SetColor(tile, new Color(1f, 1f, 1f, a));
            if (Icon.color.a > a) SetColor(Icon, new Color(1f, 1f, 1f, a));
        }

        private static bool IsBuff(Weapon w)
        {
            PlayerController p = PlayerController.Instance;
            return p != null && p.activeBuffs != null && System.Array.IndexOf(p.activeBuffs, w) >= 0;
        }
    }
}
