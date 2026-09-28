using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Das Pausenmenue im Stil von "UI 2.0" - dieselbe dunkle Karte, dasselbe
/// Titelband und dieselben Holzknoepfe wie <see cref="OptionsPanel"/> und der
/// Level-Up-Bildschirm.
///
///   PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Run);   // im Lauf
///   PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Hub);   // im Hub
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, keine
/// Bilddatei (Grafik aus <see cref="GameHudSkin"/>, Bausteine aus
/// <see cref="OptionsKit"/>). Dadurch funktioniert derselbe Aufruf aus jeder
/// Szene.
///
/// Zwei Auspraegungen:
///   Run - breite Karte: links vier Knoepfe, rechts ZEIT / LEVEL / GOLD und
///         darunter die Stats in drei Spalten. Time.timeScale = 0.
///   Hub - schmale Karte, nur drei Knoepfe. Die Zeit laeuft weiter; gesperrt
///         wird der Hub wie bei Shop und Skilltree ueber HubUI.PushModal.
///
/// Masse in Seitenpixeln (480x270, Ursprung oben links), ganzzahlig skaliert.
/// Die Statwerte zieht <see cref="BuildStatModel"/> live aus PlayerController und
/// GameManager - dieselben Felder, die das alte UIPausPanleStats abgefragt hat.
/// </summary>
public class PauseMenuPanel : MonoBehaviour
{
    public enum PauseMode { Run, Hub }

    // ==================================================================
    //  Masse (Seitenpixel)
    // ==================================================================

    // ---- Karte im Lauf ----
    private const int CardX = 16, CardY = 40, CardW = 448, CardH = 150;
    private const int RibbonY = 28;
    private static readonly RectInt RunContent = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int BtnX = 32, BtnW = 128, BtnH = 20, BtnStep = 24, BtnY0 = 58;
    private const int ArrowGap = 3;
    private const int HintY = 160;

    private const int SideX = 174, SideW = 274;
    private const int ChipY = 58, ChipH = 18, ChipGap = 5;
    private const int StatsY = 82, StatsH = 96;
    private const int ColStep = 90, ColW = 88, ColPad = 4;
    private const int GroupY = 4, StatRowY = 18, StatRowH = 14;

    // ---- Karte im Hub ----
    private const int HubCardX = 156, HubCardY = 62, HubCardW = 168, HubCardH = 120;
    private const int HubRibbonY = 50;
    private static readonly RectInt HubContent = new RectInt(HubCardX - 12, HubRibbonY, HubCardW + 24, HubCardY + HubCardH - HubRibbonY);
    private const int HubBtnY0 = 80, HubHintY = 154;

    // ---- Bestaetigungsdialog ----
    private const int DlgX = 146, DlgY = 98, DlgW = 188, DlgH = 74;
    private const int DlgTextY = 108, DlgTextH = 28;
    private const int DlgBtnY = 144, DlgBtnW = 80, DlgBtnH = 18;

    // ==================================================================
    //  Inhalt
    // ==================================================================

    /// <summary>Ein Menueeintrag. Reihenfolge = Reihenfolge auf dem Brett.</summary>
    private enum Entry { Resume, Options, GiveUp, MainMenu }

    private class Row
    {
        public SkinButton Button;
        public Entry What;
    }

    /// <summary>Eine Statzeile. Value liest bei jedem Auffrischen neu.</summary>
    private class StatDef
    {
        public string Key;
        public string Fallback;
        public string Format;
        public System.Func<float> Value;
    }

    private class StatGroup
    {
        public string Key;
        public string Fallback;
        public Color32 Color;
        public StatDef[] Rows;
    }

    private class StatCells
    {
        public StatDef Def;
        public TMP_Text Name;
        public TMP_Text Value;
    }

    // ==================================================================
    //  Zustand
    // ==================================================================

    private static PauseMenuPanel instance;
    public static bool IsOpen => instance != null;

    private PauseMode mode;
    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Canvas canvas;
    private Vector2Int lastScreen;

    private readonly List<Row> rows = new List<Row>();
    private readonly List<StatCells> cells = new List<StatCells>();
    private StatGroup[] groups;
    private int selected;

    private TMP_Text[] chipValues;
    private Image arrow;

    // Der Tastendruck, der das Menue aufgemacht hat, darf es nicht gleich
    // wieder zumachen.
    private int openedFrame;
    private float nextStatRefresh;

    // Optionen liegen ueber dem Menue und schliessen sich selbst mit ESC. In
    // welcher Reihenfolge die beiden Update() laufen, ist nicht festgelegt -
    // darum den Frame merken, in dem das Options-Fenster zuletzt offen war.
    private int optionsSeenFrame = -10;

    // Dialog
    private RectTransform dialog;
    private System.Action dialogConfirm;
    private RectTransform dialogPage;
    private SkinButton[] dialogButtons;
    private int dialogSelected;

    private HubUI hub;

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open(PauseMode pauseMode)
    {
        if (instance != null) return;
        GameObject go = new GameObject("PauseMenuPanel");
        PauseMenuPanel panel = go.AddComponent<PauseMenuPanel>();
        panel.mode = pauseMode;
        instance = panel;
        panel.Build();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    public static void Toggle(PauseMode pauseMode)
    {
        if (IsOpen) Close();
        else Open(pauseMode);
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
    }

    private void OnEnable() => Loc.LanguageChanged += Rebuild;

    private void OnDisable() => Loc.LanguageChanged -= Rebuild;

    private void OnDestroy()
    {
        if (mode == PauseMode.Run)
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.OnPauseMenuClosed();
        }
        else
        {
            BlockHub(false);
        }

        if (instance == this) instance = null;
    }

    /// <summary>
    /// Sperrt den Hub, solange das Menue offen ist - so wie Shop, Skilltree und
    /// das Erfolge-Buch es tun. Laeuft das Menue woanders, passiert nichts.
    /// </summary>
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
    //  Eingabe
    // ==================================================================

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (page != null && size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
            if (dialogPage != null) dialogPage.anchoredPosition = page.anchoredPosition;
        }

        // Solange die Optionen offen sind, verschwindet das Menue ganz - sie
        // liegen sonst als zweite Karte darueber. Danach ist es wieder da.
        bool optionsOpen = OptionsPanel.IsOpen;
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.enabled == optionsOpen) canvas.enabled = !optionsOpen;

        if (optionsOpen)
        {
            optionsSeenFrame = Time.frameCount;
            return;
        }

        if (Time.frameCount == openedFrame) return;

        // Der ESC, der die Optionen geschlossen hat, darf hier nicht noch
        // einmal zaehlen.
        bool escSwallowed = Time.frameCount - optionsSeenFrame <= 1;

        if (dialog != null)
        {
            UpdateDialogInput(escSwallowed);
            return;
        }

        RefreshStatsPeriodically();

        if (Input.GetKeyDown(KeyCode.Escape) && !escSwallowed)
        {
            OptionsKit.PlayClick();
            Activate(Entry.Resume);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            if (selected >= 0 && selected < rows.Count)
            {
                OptionsKit.PlayClick();
                Activate(rows[selected].What);
            }
        }
    }

    private void Move(int delta)
    {
        if (rows.Count == 0) return;
        Select((selected + delta + rows.Count) % rows.Count);
        OptionsKit.PlayClick();
    }

    private void UpdateDialogInput(bool escSwallowed)
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !escSwallowed)
        {
            CloseDialog();
            OptionsKit.PlayClick();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectDialog(0);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectDialog(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            OptionsKit.PlayClick();
            if (dialogSelected == 1) ConfirmDialog();
            else CloseDialog();
        }
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private RectInt Content => mode == PauseMode.Run ? RunContent : HubContent;

    private void Build()
    {
        BuildPage();

        if (mode == PauseMode.Run) Time.timeScale = 0f;
        else BlockHub(true);
    }

    /// <summary>Alles Sichtbare - laeuft nach einem Sprachwechsel noch einmal.</summary>
    private void BuildPage()
    {
        // Ueber allem, was im Hub aufgehen kann (Shop 130, Levelauswahl 135,
        // Skilltree 140, Erfolge-Buch 150). Nur die Optionen liegen darueber.
        page = OptionsKit.CreatePage(gameObject, 200, 0.8f, out scaler);

        rows.Clear();
        cells.Clear();
        chipValues = null;
        dialog = null;
        dialogPage = null;
        dialogButtons = null;

        if (mode == PauseMode.Run) BuildRunCard();
        else BuildHubCard();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        Select(selected);
        RefreshStats();
    }

    private void Rebuild()
    {
        if (page == null) return;
        OptionsKit.Clear(transform);
        BuildPage();
    }

    // ---------- Lauf ----------

    private void BuildRunCard()
    {
        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.pause.title", "PAUSE"),
                          pixelFont, textFont);

        BuildMenu(new[] { Entry.Resume, Entry.Options, Entry.GiveUp, Entry.MainMenu }, BtnX, BtnY0);
        Hint(BtnX, HintY, BtnW);

        BuildChips();
        BuildStats();
    }

    /// <summary>ZEIT / LEVEL / GOLD als drei gleich breite Schildchen.</summary>
    private void BuildChips()
    {
        string[] keys = { "ui.pause.chip.time", "ui.pause.chip.level", "ui.pause.chip.gold" };
        string[] fallbacks = { "ZEIT", "LEVEL", "GOLD" };
        Sprite[] icons = { GameHudSkin.Clock, GameHudSkin.Gem, GameHudSkin.Coin };

        int w = (SideW - 2 * ChipGap) / 3;
        chipValues = new TMP_Text[3];

        for (int i = 0; i < 3; i++)
        {
            int x = SideX + i * (w + ChipGap);
            OptionsKit.Img("Chip_" + fallbacks[i], page, x, ChipY, w, ChipH, GameHudSkin.Well, true);

            Sprite icon = icons[i];
            float iw = icon.rect.width, ih = icon.rect.height;
            OptionsKit.Img("Icon", page, x + 4, ChipY + Mathf.Round((ChipH - ih) / 2f), iw, ih, icon);

            OptionsKit.Label("Key", page, x + 7 + iw, ChipY + 2, w - 11 - iw, ChipH - 3,
                             Loc.Get(keys[i], fallbacks[i]), textFont, OptionsKit.SizeText,
                             GameHudSkin.Stone, TextAlignmentOptions.Left);
            chipValues[i] = OptionsKit.Label("Value", page, x + 4, ChipY + 2, w - 9, ChipH - 3, "-", textFont,
                                             OptionsKit.SizeText, GameHudSkin.Cream,
                                             TextAlignmentOptions.Right);
        }
    }

    /// <summary>Die Stats in drei Spalten - je Gruppe eine, mit farbigem Kopf.</summary>
    private void BuildStats()
    {
        OptionsKit.Img("StatsWell", page, SideX, StatsY, SideW, StatsH, GameHudSkin.Well, true);

        groups = BuildStatModel();

        for (int g = 0; g < groups.Length; g++)
        {
            StatGroup group = groups[g];
            int cx = SideX + ColPad + g * ColStep;
            int cy = StatsY;

            OptionsKit.Img("Dot_" + g, page, cx + 3, cy + GroupY + 4, 5, 5, GameHudSkin.White, (Color)group.Color);
            OptionsKit.Label("Group_" + g, page, cx + 11, cy + GroupY, ColW - 11, 13,
                             Loc.Get(group.Key, group.Fallback), textFont, OptionsKit.SizeText,
                             group.Color, TextAlignmentOptions.Left);

            for (int r = 0; r < group.Rows.Length; r++)
            {
                int ry = cy + StatRowY + r * StatRowH;

                if (r % 2 == 0)
                    OptionsKit.Img("Zebra_" + g + "_" + r, page, cx, ry, ColW, StatRowH,
                                   GameHudSkin.White, (Color)GameHudSkin.CardFill);

                StatCells cell = new StatCells
                {
                    Def = group.Rows[r],
                    Name = OptionsKit.Label("Name_" + g + "_" + r, page, cx + 3, ry, ColW - 6, StatRowH,
                                            Loc.Get(group.Rows[r].Key, group.Rows[r].Fallback), textFont,
                                            OptionsKit.SizeText, GameHudSkin.Parchment,
                                            TextAlignmentOptions.Left),
                    Value = OptionsKit.Label("Value_" + g + "_" + r, page, cx + 3, ry, ColW - 6, StatRowH,
                                             "-", textFont, OptionsKit.SizeText, GameHudSkin.Cream,
                                             TextAlignmentOptions.Right),
                };
                cells.Add(cell);
            }
        }
    }

    // ---------- Hub ----------

    private void BuildHubCard()
    {
        OptionsKit.Img("Card", page, HubCardX, HubCardY, HubCardW, HubCardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, HubCardX + HubCardW / 2f, HubRibbonY, Loc.Get("ui.pause.title", "PAUSE"),
                          pixelFont, textFont);

        int x = HubCardX + (HubCardW - BtnW) / 2;
        BuildMenu(new[] { Entry.Resume, Entry.Options, Entry.MainMenu }, x, HubBtnY0);
        Hint(x, HubHintY, BtnW);
    }

    /// <summary>
    /// Die kleine Zeile unter den Knoepfen. "Tipps" in den Optionen laesst sie
    /// weg - wer das abschaltet, kennt die Tastenbelegung.
    /// </summary>
    private void Hint(int x, int y, int w)
    {
        if (!GameSettings.Tips) return;

        OptionsKit.Label("Hint", page, x - 20, y, w + 40, 13,
                         Loc.Get("ui.pause.hint", "ESC schließt das Menü"), textFont, OptionsKit.SizeText,
                         GameHudSkin.Stone, TextAlignmentOptions.Center);
    }

    // ---------- Menuespalte ----------

    private void BuildMenu(Entry[] entries, int x, int firstY)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            Entry what = entries[i];
            int index = i;

            SkinButton button = SkinButton.Create(page, x, firstY + i * BtnStep, BtnW, BtnH, EntryText(what),
                                                  textFont, SkinButton.Kind.Wood, () => Activate(what));
            // Die Auswahl fuehrt: Pfeiltasten und Maus bewegen denselben Zeiger.
            button.UseSelection = true;
            button.OnHover = () => Select(index);

            rows.Add(new Row { Button = button, What = what });
        }

        Sprite a = GameHudSkin.Arrow;
        arrow = OptionsKit.Img("Arrow", page, 0, 0, a.rect.width, a.rect.height, a);
    }

    private void Select(int index)
    {
        if (rows.Count == 0) return;
        selected = Mathf.Clamp(index, 0, rows.Count - 1);

        for (int i = 0; i < rows.Count; i++) rows[i].Button.Selected = i == selected;

        if (arrow != null)
        {
            // Der Pfeil zeigt links auf den gewaehlten Knopf.
            RectTransform b = (RectTransform)rows[selected].Button.transform;
            Rect r = arrow.sprite.rect;
            float x = b.anchoredPosition.x - ArrowGap - r.width;
            float y = -b.anchoredPosition.y + Mathf.Round((BtnH - 1 - r.height) / 2f);
            OptionsKit.Move(arrow.rectTransform, x, y, r.width, r.height);
        }
    }

    // ==================================================================
    //  Eintraege
    // ==================================================================

    private static string EntryText(Entry what)
    {
        switch (what)
        {
            case Entry.Resume:   return Loc.Get("ui.pause.btn.resume", "WEITER");
            case Entry.Options:  return Loc.Get("ui.pause.btn.options", "OPTIONEN");
            case Entry.GiveUp:   return Loc.Get("ui.pause.btn.giveup", "RUN AUFGEBEN");
            default:             return Loc.Get("ui.pause.btn.mainmenu", "ZURÜCK ZUM HAUPTMENÜ");
        }
    }

    /// <summary>Den Klick-Sound spielt der Aufrufer (SkinButton bzw. die Tastatur).</summary>
    private void Activate(Entry what)
    {
        switch (what)
        {
            case Entry.Resume:
                Close();
                break;

            case Entry.Options:
                OptionsPanel.Open();
                optionsSeenFrame = Time.frameCount;
                // Sofort weg, nicht erst im naechsten Update.
                if (canvas == null) canvas = GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                break;

            case Entry.GiveUp:
                OpenDialog(Loc.Get("ui.pause.dialog.giveup", "Lauf wirklich aufgeben?"),
                           GiveUpRun);
                break;

            case Entry.MainMenu:
                OpenDialog(mode == PauseMode.Run
                        ? Loc.Get("ui.pause.dialog.mainmenu",
                                  "Zurück zum Hauptmenü? Der Lauf geht verloren.")
                        : Loc.Get("ui.pause.dialog.mainmenu.hub", "Zurück zum Hauptmenü?"),
                    GoToMainMenu);
                break;
        }
    }

    /// <summary>
    /// Lauf abbrechen. GameManager.Restart bringt den Spieler dorthin zurueck,
    /// wo der Lauf gestartet wurde - Hub oder World Map.
    /// </summary>
    private static void GiveUpRun()
    {
        GameManager manager = GameManager.Instance;
        Close();

        if (manager != null) manager.Restart();
        else Time.timeScale = 1f;
    }

    private static void GoToMainMenu()
    {
        Close();

        // Ein Weg fuer beide Auspraegungen: GameSession.LoadMainMenu laedt das
        // Hauptmenue hart mit Single und raeumt damit alles ab, was sonst noch
        // geladen ist - Level, Map-Szene, Hub oder World Map.
        GameSession.LoadMainMenu();
    }

    // ==================================================================
    //  Bestaetigungsdialog
    // ==================================================================

    private void OpenDialog(string question, System.Action onConfirm)
    {
        if (dialog != null) return;

        dialogConfirm = onConfirm;

        // Eigene Ebene ueber der Karte: als letztes Kind gezeichnet, und die
        // vollflaechige Abdunklung faengt jeden Klick daneben ab.
        Image shade = OptionsKit.Stretch("Dialog", transform, GameHudSkin.White,
                                         new Color(0.06f, 0.04f, 0.05f, 0.7f));
        shade.raycastTarget = true;
        dialog = shade.rectTransform;

        // Gleiche Seite wie die Karte darunter, damit der Dialog pixelgenau mittig sitzt.
        dialogPage = OptionsKit.Rect("Page", dialog, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        dialogPage.anchorMin = dialogPage.anchorMax = new Vector2(0f, 1f);
        dialogPage.anchoredPosition = page.anchoredPosition;

        OptionsKit.Img("Card", dialogPage, DlgX, DlgY, DlgW, DlgH, GameHudSkin.Card, true);
        TextMeshProUGUI q = OptionsKit.Label("Question", dialogPage, DlgX + 10, DlgTextY, DlgW - 20, DlgTextH,
                                             question, textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                             TextAlignmentOptions.Center);
        q.textWrappingMode = TextWrappingModes.Normal;

        int gap = DlgW - 20 - 2 * DlgBtnW;
        dialogButtons = new SkinButton[2];
        dialogButtons[0] = SkinButton.Create(dialogPage, DlgX + 10, DlgBtnY, DlgBtnW, DlgBtnH,
                                             Loc.Get("ui.pause.dialog.cancel", "ABBRECHEN"), textFont,
                                             SkinButton.Kind.Wood, CloseDialog);
        dialogButtons[1] = SkinButton.Create(dialogPage, DlgX + 10 + DlgBtnW + gap, DlgBtnY, DlgBtnW, DlgBtnH,
                                             Loc.Get("ui.pause.dialog.confirm", "BESTÄTIGEN"), textFont,
                                             SkinButton.Kind.Danger, ConfirmDialog);
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            dialogButtons[i].UseSelection = true;
            dialogButtons[i].OnHover = () => SelectDialog(index);
        }

        SelectDialog(0);
    }

    private void SelectDialog(int index)
    {
        if (dialogButtons == null) return;
        dialogSelected = Mathf.Clamp(index, 0, 1);
        for (int i = 0; i < 2; i++) dialogButtons[i].Selected = i == dialogSelected;
    }

    private void CloseDialog()
    {
        if (dialog == null) return;
        dialog.SetParent(null, false);
        Destroy(dialog.gameObject);
        dialog = null;
        dialogPage = null;
        dialogButtons = null;
        dialogConfirm = null;
    }

    private void ConfirmDialog()
    {
        System.Action action = dialogConfirm;
        CloseDialog();
        action?.Invoke();
    }

    // ==================================================================
    //  Werte
    // ==================================================================

    /// <summary>
    /// Die fuenfzehn Werte, die frueher in UIPausPanleStats per Index in eine
    /// Textliste geschrieben wurden - jetzt mit Namen, Gruppe und Format an
    /// einer Stelle. Gelesen wird immer live, nichts steht hier fest.
    /// </summary>
    private static StatGroup[] BuildStatModel()
    {
        PlayerController p() => PlayerController.Instance;

        return new[]
        {
            new StatGroup
            {
                Key = "ui.pause.group.survival", Fallback = "ÜBERLEBEN", Color = GameHudSkin.JamLight,
                Rows = new[]
                {
                    Stat("maxhp",     "Max HP",          "F0", () => p() != null ? p().playerMaxHealth : 0f),
                    Stat("regen",     "HP-Regeneration", "F1", () => p() != null ? p().playerHealthReg : 0f),
                    Stat("armor",     "Rüstung",         "F0", () => p() != null ? p().playerArmor : 0f),
                    Stat("dodge",     "Ausweichchance",  "F0", () => p() != null ? p().dodgeChance * 100f : 0f),
                    Stat("lifesteal", "Lebensraub",      "F0", () => p() != null ? p().lifeStealChance : 0f),
                },
            },
            new StatGroup
            {
                Key = "ui.pause.group.offense", Fallback = "OFFENSIVE", Color = GameHudSkin.Gold,
                Rows = new[]
                {
                    Stat("damage",     "Schaden",       "F1", () => p() != null ? p().damageMultiplier : 0f),
                    Stat("critchance", "Krit-Chance",   "F0", () => p() != null ? p().critChance * 100f : 0f),
                    Stat("critdamage", "Krit-Schaden",  "F1", () => p() != null ? p().critDamage : 0f),
                    Stat("size",       "Größe",         "F1", () => p() != null ? p().AOERange : 0f),
                    Stat("shots",      "Extra-Schüsse", "F1", () => p() != null ? p().playerShots : 0f),
                },
            },
            new StatGroup
            {
                Key = "ui.pause.group.utility", Fallback = "NUTZEN", Color = GameHudSkin.Mint,
                Rows = new[]
                {
                    Stat("speed",  "Tempo",           "F1", () => p() != null ? p().moveSpeed : 0f),
                    Stat("luck",   "Glück",           "F0", () => p() != null ? p().luck : 0f),
                    Stat("pickup", "Aufsammelradius", "F1", () => p() != null ? p().pickupRange : 0f),
                    Stat("xp",     "XP-Gewinn",       "F2", () => p() != null ? p().experienceMultiplier : 0f),
                    Stat("gold",   "Gold-Gewinn",     "F1",
                         () => GameManager.Instance != null
                             ? GameManager.Instance.currencyGainMultiplire : 0f),
                },
            },
        };
    }

    private static StatDef Stat(string id, string fallback, string format, System.Func<float> value)
    {
        return new StatDef
        {
            Key = "ui.pause.stat." + id,
            Fallback = fallback,
            Format = format,
            Value = value,
        };
    }

    /// <summary>
    /// Cheats und Buffs koennen die Werte auch waehrend der Pause aendern -
    /// darum alle 0,25 Sekunden nachsehen. Jeden Frame waere Stringmuell fuer
    /// nichts, einmal beim Oeffnen zu wenig.
    /// </summary>
    private void RefreshStatsPeriodically()
    {
        if (mode != PauseMode.Run) return;
        if (Time.unscaledTime < nextStatRefresh) return;

        nextStatRefresh = Time.unscaledTime + 0.25f;
        RefreshStats();
    }

    private void RefreshStats()
    {
        if (groups == null || mode != PauseMode.Run) return;

        int i = 0;
        foreach (StatGroup group in groups)
        {
            foreach (StatDef def in group.Rows)
            {
                if (i >= cells.Count) return;

                float value = def.Value();
                string text = value.ToString(def.Format);

                // Nullwerte gedimmt, aber weiterhin lesbar.
                bool zero = Mathf.Approximately(value, 0f);

                StatCells cell = cells[i++];
                if (cell.Value.text != text) cell.Value.text = text;
                cell.Value.color = zero ? GameHudSkin.Stone : GameHudSkin.Cream;
                cell.Name.color = zero ? GameHudSkin.Stone : GameHudSkin.Parchment;
            }
        }

        RefreshChips();
    }

    private void RefreshChips()
    {
        if (chipValues == null) return;

        GameManager manager = GameManager.Instance;
        PlayerController player = PlayerController.Instance;

        float time = manager != null ? manager.gameTime : 0f;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        chipValues[0].text = minutes + ":" + seconds.ToString("00");
        chipValues[1].text = player != null ? player.currentLevel.ToString() : "-";
        chipValues[2].text = manager != null ? manager.EstimateCurrency().ToString() : "-";
    }
}
