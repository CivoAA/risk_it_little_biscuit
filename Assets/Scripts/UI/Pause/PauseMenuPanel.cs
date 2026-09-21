using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Das Pausenmenue: eine Pergamenttafel ueber dem abgedunkelten Bild.
///
///   PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Run);   // im Lauf
///   PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Hub);   // im Hub
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, nichts im
/// Inspector, genau wie <see cref="OptionsPanel"/>, <see cref="AchievementPanel"/>
/// und <see cref="WorkbenchPanel"/>. Dadurch funktioniert derselbe Aufruf aus
/// jeder Szene.
///
/// Zwei Auspraegungen:
///   Run - volle Tafel mit Kopfleiste (ZEIT / LEVEL / GOLD) und Stats-Spalte,
///         vier Eintraege, Time.timeScale = 0.
///   Hub - schmale Tafel, mittig, ohne Chips und ohne Stats, drei Eintraege.
///         Die Zeit laeuft weiter; gesperrt wird der Hub wie bei Shop und
///         Skilltree ueber HubUI.PushModal.
///
/// Die Grafik kommt aus Assets/Resources/PauseMenu/ui/ (siehe
/// Assets/Art/UI_Objects/PauseMenu/PAUSE_MENU_UI.md - dort stehen alle Masse,
/// Textzonen und 9-Slice-Raender, nach denen hier gerechnet wird). Jede Zahl in
/// diesem Skript ist ein Pixel im 320x180-Raster, Ursprung oben links. Landet
/// etwas auf einem halben Pixel, verschmiert es beim Hochskalieren.
///
/// Die Statwerte zieht <see cref="ReadStats"/> live aus PlayerController und
/// GameManager - dieselben Felder, die das alte UIPausPanleStats abgefragt hat.
/// </summary>
public class PauseMenuPanel : MonoBehaviour
{
    public enum PauseMode { Run, Hub }

    // ==================================================================
    //  Masse (alle aus PAUSE_MENU_UI.md)
    // ==================================================================

    private const int RefW = 320;
    private const int RefH = 180;
    private const string UiPath = "PauseMenu/ui/";

    // ---- Tafel im Lauf: 256x156 mittig, Sprite inkl. 2px Schlagschatten ----
    private const float BoardX = 32f, BoardY = 12f, BoardW = 258f, BoardH = 158f;
    private const float HeaderX = 33f, HeaderY = 13f, HeaderW = 254f, HeaderH = 22f;

    private static readonly Vector4 TitleZone = new Vector4(39f, 17f, 54f, 14f);
    private const float HdrLineX = 98f, HdrLineY = 23f, HdrLineW = 60f;

    private const float ChipY = 18f, ChipW = 38f, ChipH = 12f;
    private static readonly float[] ChipX = { 163f, 203f, 243f };

    private const float DividerX = 159f, DividerY = 35f, DividerH = 130f;
    private const float ColX = 161f, ColY = 35f, ColW = 126f, ColH = 130f;
    private const float StatsX = 164f, StatsY = 38f, StatsW = 120f, StatsH = 124f;

    // Innenflaeche der Stats-Tafel: 112 breit, 116 hoch, ab (168, 42).
    private const float ContentX = 168f, ContentY = 42f, ContentW = 112f;
    private const float StatsLineX = 206f, StatsLineY = 46f, StatsLineW = 74f;

    // Kopfzeile 8, danach drei Gruppen a 36 (Gruppenkopf 6 + 5 Zeilen a 6).
    private const float GroupStep = 36f, RowH = 6f, GroupHeadH = 6f;
    private const float NameDX = 2f, NameW = 60f;
    private const float LeadDX = 64f, LeadW = 18f;
    private const float ValueDX = 83f, ValueW = 28f;

    // ---- Menuespalte ----
    private const float BtnX = 46f, BtnW = 109f, BtnH = 17f, BtnStep = 20f;
    private const float BtnFirstY = 54f;
    private const float ArrowX = 39f, ArrowS = 5f, ArrowDY = 5f;
    private static readonly Vector4 HintZone = new Vector4(46f, 136f, 108f, 10f);

    // ---- Tafel im Hub: 148x126 mittig ----
    private const float HubBoardX = 86f, HubBoardY = 27f, HubBoardW = 150f, HubBoardH = 128f;
    private const float HubHeaderX = 87f, HubHeaderY = 28f, HubHeaderW = 146f;
    private static readonly Vector4 HubTitleZone = new Vector4(87f, 32f, 146f, 14f);
    private const float HubBtnX = 109f, HubBtnFirstY = 64f, HubArrowX = 102f;
    private static readonly Vector4 HubHintZone = new Vector4(109f, 126f, 108f, 10f);

    // ---- Bestaetigungsdialog ----
    private const float DlgX = 95f, DlgY = 60f, DlgW = 132f, DlgH = 62f;
    private static readonly Vector4 DlgTextZone = new Vector4(103f, 68f, 114f, 24f);
    private const float DlgBtnY = 98f, DlgBtnW = 57f, DlgBtnH = 15f;
    private static readonly float[] DlgBtnX = { 102f, 162f };

    // ---- Schriftgroessen ----
    private const float SizeTitle = 14f;
    private const float SizeButton = 10f;
    private const float SizeHead = 8f;
    private const float SizeValue = 8f;
    private const float SizeName = 7f;
    private const float SizeSmall = 6f;

    // ---- Palette ----
    private static readonly Color TextOnWood   = Hex(0xf2dcbc);
    private static readonly Color TextOnActive = Hex(0xfff4e0);
    private static readonly Color TextChipKey  = Hex(0xd9b189);
    private static readonly Color TextInk      = Hex(0x3b2b33);
    private static readonly Color TextValue    = Hex(0x4d2e1e);
    private static readonly Color TextDim      = Hex(0x6f4630);
    private static readonly Color LineColor    = Hex(0xd9b189);
    private static readonly Color HdrLine      = Hex(0x5a3421);
    private static readonly Color HdrLineHi    = Hex(0x8a5a3d);
    private static readonly Color DimColor     = new Color32(0x2b, 0x20, 0x28, 0xCC); // 80 %

    // ==================================================================
    //  Inhalt
    // ==================================================================

    /// <summary>Ein Menueeintrag. Reihenfolge = Reihenfolge auf dem Brett.</summary>
    private enum Entry { Resume, Options, GiveUp, MainMenu }

    private class Row
    {
        public Image Frame;
        public TMP_Text Label;
        public RectTransform LabelRect;
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
        public string Dot;
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
    private TMP_FontAsset font;
    private RectTransform page;

    private readonly List<Row> rows = new List<Row>();
    private readonly List<StatCells> cells = new List<StatCells>();
    private StatGroup[] groups;
    private int selected;

    private TMP_Text[] chipValues;
    private Image arrow;
    private float arrowBaseX;

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
    private Image[] dialogFrames;
    private TMP_Text[] dialogLabels;
    private RectTransform[] dialogLabelRects;
    private Sprite[][] dialogSprites;
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
        font = FindFont();
    }

    private void OnEnable() => Loc.LanguageChanged += RefreshTexts;

    private void OnDisable() => Loc.LanguageChanged -= RefreshTexts;

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
        if (OptionsPanel.IsOpen)
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
            Activate(Entry.Resume);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            if (selected >= 0 && selected < rows.Count) Activate(rows[selected].What);
        }
    }

    private void Move(int delta)
    {
        if (rows.Count == 0) return;
        Select((selected + delta + rows.Count) % rows.Count);
        PlayClick();
    }

    private void UpdateDialogInput(bool escSwallowed)
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !escSwallowed)
        {
            CloseDialog();
            PlayClick();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectDialog(0);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectDialog(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            if (dialogSelected == 1) ConfirmDialog();
            else CloseDialog();
        }
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Ueber allem, was im Hub aufgehen kann (Shop 130, Levelauswahl 135,
        // Skilltree 140, Erfolge-Buch 150). Nur die Optionen liegen darueber.
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        BuildBackdrop();

        // Die Tafel bleibt immer exakt im 320x180-Raster und mittig, egal wie
        // breit der Bildschirm ist.
        page = Rect("Page", transform, 0f, 0f, RefW, RefH);
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        page.anchoredPosition = Vector2.zero;

        if (mode == PauseMode.Run)
        {
            BuildRunBoard();
            Time.timeScale = 0f;
        }
        else
        {
            BuildHubBoard();
            BlockHub(true);
        }

        Select(0);
    }

    /// <summary>
    /// Abdunklung, Scanlines, Vignette und der umlaufende Rahmen - drei Lagen
    /// plus Rahmen, damit sich jede einzeln abschalten laesst.
    /// </summary>
    private void BuildBackdrop()
    {
        Image dim = Stretch("Dim", transform, Gfx("overlay_dim"));
        dim.raycastTarget = true;   // faengt Klicks ab, die neben die Tafel gehen

        // Gekachelt statt gedehnt: nur so bleiben die Streifen bei jedem
        // Seitenverhaeltnis genau einen Pixel hoch.
        Image scan = Stretch("Scanlines", transform, Gfx("overlay_scanlines"));
        scan.type = Image.Type.Tiled;

        Stretch("Vignette", transform, Gfx("overlay_vignette"));

        Image frame = Stretch("Frame", transform, Gfx("overlay_frame"));
        frame.type = Image.Type.Sliced;
    }

    // ---------- Lauf ----------

    private void BuildRunBoard()
    {
        Img("Board", page, BoardX, BoardY, BoardW, BoardH, Gfx("board_panel"));
        Img("Header", page, HeaderX, HeaderY, HeaderW, HeaderH, Gfx("header_bar"));

        Label("Title", page, TitleZone.x, TitleZone.y, TitleZone.z, TitleZone.w,
              Loc.Get("ui.pause.title", "PAUSE"), SizeTitle, TextOnWood,
              TextAlignmentOptions.Left);

        Solid("HdrLine", page, HdrLineX, HdrLineY, HdrLineW, 1f, HdrLine);
        Solid("HdrLineHi", page, HdrLineX, HdrLineY + 1f, HdrLineW, 1f, HdrLineHi);

        BuildChips();

        Img("Column", page, ColX, ColY, ColW, ColH, Gfx("column_right"));
        Img("Divider", page, DividerX, DividerY, 2f, DividerH, Gfx("divider_v"));

        BuildStats();
        BuildMenu(new[] { Entry.Resume, Entry.Options, Entry.GiveUp, Entry.MainMenu },
                  BtnX, BtnFirstY, ArrowX);

        Hint(HintZone);

        RefreshStats();
    }

    private void BuildChips()
    {
        string[] keys = { "ui.pause.chip.time", "ui.pause.chip.level", "ui.pause.chip.gold" };
        string[] fallbacks = { "ZEIT", "LEVEL", "GOLD" };

        chipValues = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform chip = Rect("Chip_" + fallbacks[i], page, ChipX[i], ChipY, ChipW, ChipH);
            Image img = chip.gameObject.AddComponent<Image>();
            img.sprite = Gfx("header_chip");
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            Label("Key", chip, 2f, 1f, 14f, 10f, Loc.Get(keys[i], fallbacks[i]),
                  SizeSmall, TextChipKey, TextAlignmentOptions.Left);
            chipValues[i] = Label("Value", chip, 17f, 1f, 19f, 10f, "-", SizeValue,
                                  TextOnActive, TextAlignmentOptions.Right);
        }
    }

    private void BuildStats()
    {
        Img("StatsPanel", page, StatsX, StatsY, StatsW, StatsH, Gfx("stats_panel"));
        Label("StatsHead", page, ContentX, ContentY, 34f, 8f,
              Loc.Get("ui.pause.stats.title", "STATS"), SizeHead, TextInk,
              TextAlignmentOptions.Left);
        Solid("StatsHeadLine", page, StatsLineX, StatsLineY, StatsLineW, 1f, LineColor);

        groups = BuildStatModel();

        for (int g = 0; g < groups.Length; g++)
        {
            StatGroup group = groups[g];
            float gy = ContentY + 8f + g * GroupStep;

            Img("Dot_" + g, page, ContentX, gy + 1f, 4f, 4f, Gfx(group.Dot));
            Label("Group_" + g, page, ContentX + 6f, gy - 2f, ContentW - 6f, 10f,
                  Loc.Get(group.Key, group.Fallback), SizeHead, TextInk,
                  TextAlignmentOptions.Left);

            for (int r = 0; r < group.Rows.Length; r++)
            {
                float ry = gy + GroupHeadH + r * RowH;

                // Zebra auf jeder zweiten Zeile.
                if (r % 2 == 1)
                    Img("Zebra_" + g + "_" + r, page, ContentX, ry, ContentW, RowH,
                        Gfx("stats_row_zebra"));

                Solid("Lead_" + g + "_" + r, page, ContentX + LeadDX, ry + 3f, LeadW, 1f,
                      LineColor);

                StatCells cell = new StatCells
                {
                    Def = group.Rows[r],
                    Name = Label("Name_" + g + "_" + r, page, ContentX + NameDX, ry - 2f,
                                 NameW, 10f, Loc.Get(group.Rows[r].Key, group.Rows[r].Fallback),
                                 SizeName, TextValue, TextAlignmentOptions.Left),
                    Value = Label("Value_" + g + "_" + r, page, ContentX + ValueDX, ry - 2f,
                                  ValueW, 10f, "-", SizeValue, TextValue,
                                  TextAlignmentOptions.Right),
                };
                cells.Add(cell);
            }
        }
    }

    // ---------- Hub ----------

    private void BuildHubBoard()
    {
        Image board = Img("Board", page, HubBoardX, HubBoardY, HubBoardW, HubBoardH,
                          Gfx("board_panel"));
        board.type = Image.Type.Sliced;

        Image header = Img("Header", page, HubHeaderX, HubHeaderY, HubHeaderW, HeaderH,
                           Gfx("header_bar"));
        header.type = Image.Type.Sliced;

        // Ohne Chips ist die Leiste leer - dann steht der Titel mittig.
        Label("Title", page, HubTitleZone.x, HubTitleZone.y, HubTitleZone.z, HubTitleZone.w,
              Loc.Get("ui.pause.title", "PAUSE"), SizeTitle, TextOnWood,
              TextAlignmentOptions.Center);

        BuildMenu(new[] { Entry.Resume, Entry.Options, Entry.MainMenu },
                  HubBtnX, HubBtnFirstY, HubArrowX);

        Hint(HubHintZone);
    }

    /// <summary>
    /// Die kleine Zeile unter den Knoepfen. "Tipps" in den Optionen laesst sie
    /// weg - wer das abschaltet, kennt die Tastenbelegung.
    /// </summary>
    private void Hint(Vector4 zone)
    {
        if (!GameSettings.Tips) return;

        Label("Hint", page, zone.x, zone.y, zone.z, zone.w,
              Loc.Get("ui.pause.hint", "ESC schließt das Menü"), SizeSmall, TextDim,
              TextAlignmentOptions.Center);
    }

    // ---------- Menuespalte ----------

    private void BuildMenu(Entry[] entries, float x, float firstY, float arrowX)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            float y = firstY + i * BtnStep;
            Entry what = entries[i];

            RectTransform rt = Rect("Btn_" + what, page, x, y, BtnW, BtnH);
            Image frame = rt.gameObject.AddComponent<Image>();
            frame.sprite = Gfx("btn_normal");
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = true;

            // Die sichtbare Flaeche ist 108x16 und beginnt bei (0,0); im
            // aktiven Zustand liegt sie um 1px nach rechts unten versetzt.
            // Die Beschriftung wandert in Select() mit.
            TMP_Text label = Label("Label", rt, 4f, 3f, 100f, 10f, EntryText(what),
                                   SizeButton, TextOnWood, TextAlignmentOptions.Left);

            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;
            int index = i;
            button.onClick.AddListener(() => Activate(entries[index]));

            HoverRelay relay = rt.gameObject.AddComponent<HoverRelay>();
            relay.Owner = this;
            relay.Index = i;

            rows.Add(new Row
            {
                Frame = frame,
                Label = label,
                LabelRect = label.rectTransform,
                What = what,
            });
        }

        arrowBaseX = arrowX;
        arrow = Img("Arrow", page, arrowX, firstY + ArrowDY, ArrowS, ArrowS, Gfx("arrow_select"));
    }

    /// <summary>Meldet, ueber welchem Eintrag die Maus steht.</summary>
    private class HoverRelay : MonoBehaviour, IPointerEnterHandler
    {
        public PauseMenuPanel Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Owner != null) Owner.Select(Index);
        }
    }

    private void Select(int index)
    {
        if (rows.Count == 0) return;
        selected = Mathf.Clamp(index, 0, rows.Count - 1);

        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i == selected;
            Row row = rows[i];
            row.Frame.sprite = Gfx(on ? "btn_active" : "btn_normal");
            row.Label.color = on ? TextOnActive : TextOnWood;
            // Der aktive Knopf sitzt 1px nach rechts unten - die Schrift auch.
            row.LabelRect.anchoredPosition = new Vector2(on ? 5f : 4f, on ? -4f : -3f);
        }

        if (arrow != null)
        {
            // Der Pfeil zeigt immer auf den aktiven Knopf - und ruckt mit ihm
            // um denselben Pixel nach rechts unten.
            float top = rows[selected].Frame.rectTransform.anchoredPosition.y;
            arrow.rectTransform.anchoredPosition =
                new Vector2(arrowBaseX + 1f, top - ArrowDY - 1f);
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

    private void Activate(Entry what)
    {
        PlayClick();

        switch (what)
        {
            case Entry.Resume:
                Close();
                break;

            case Entry.Options:
                OptionsPanel.Open();
                optionsSeenFrame = Time.frameCount;
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
        dialogSelected = 0;

        // Eigene Ebene ueber der Tafel: als letztes Kind gezeichnet, und der
        // vollflaechige Schatten faengt jeden Klick daneben ab.
        dialog = Rect("Dialog", transform, 0f, 0f, RefW, RefH);
        dialog.anchorMin = Vector2.zero;
        dialog.anchorMax = Vector2.one;
        dialog.sizeDelta = Vector2.zero;
        dialog.anchoredPosition = Vector2.zero;
        dialog.pivot = new Vector2(0.5f, 0.5f);

        Image shade = dialog.gameObject.AddComponent<Image>();
        shade.color = DimColor;
        shade.raycastTarget = true;

        RectTransform inner = Rect("Page", dialog, 0f, 0f, RefW, RefH);
        inner.anchorMin = inner.anchorMax = inner.pivot = new Vector2(0.5f, 0.5f);
        inner.anchoredPosition = Vector2.zero;

        Img("Panel", inner, DlgX, DlgY, DlgW, DlgH, Gfx("dialog_panel"));
        Label("Question", inner, DlgTextZone.x, DlgTextZone.y, DlgTextZone.z, DlgTextZone.w,
              question, SizeHead, TextInk, TextAlignmentOptions.Center).textWrappingMode
            = TextWrappingModes.Normal;

        dialogFrames = new Image[2];
        dialogLabels = new TMP_Text[2];
        dialogLabelRects = new RectTransform[2];
        dialogSprites = new[]
        {
            new[] { Gfx("btn_normal"), Gfx("btn_active") },
            new[] { Gfx("btn_danger"), Gfx("btn_danger_active") },
        };

        string[] labels =
        {
            Loc.Get("ui.pause.dialog.cancel", "ABBRECHEN"),
            Loc.Get("ui.pause.dialog.confirm", "BESTÄTIGEN"),
        };

        for (int i = 0; i < 2; i++)
        {
            RectTransform rt = Rect("Btn_" + i, inner, DlgBtnX[i], DlgBtnY, DlgBtnW, DlgBtnH);
            Image img = rt.gameObject.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            TMP_Text label = Label("Label", rt, 3f, 2f, 50f, 10f, labels[i], SizeValue,
                                   TextOnWood, TextAlignmentOptions.Center);

            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;
            int index = i;
            button.onClick.AddListener(() =>
            {
                PlayClick();
                if (index == 1) ConfirmDialog();
                else CloseDialog();
            });

            DialogHover relay = rt.gameObject.AddComponent<DialogHover>();
            relay.Owner = this;
            relay.Index = i;

            dialogFrames[i] = img;
            dialogLabels[i] = label;
            dialogLabelRects[i] = label.rectTransform;
        }

        SelectDialog(0);
    }

    private class DialogHover : MonoBehaviour, IPointerEnterHandler
    {
        public PauseMenuPanel Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Owner != null) Owner.SelectDialog(Index);
        }
    }

    private void SelectDialog(int index)
    {
        if (dialogFrames == null) return;
        dialogSelected = Mathf.Clamp(index, 0, 1);

        for (int i = 0; i < 2; i++)
        {
            bool on = i == dialogSelected;
            dialogFrames[i].sprite = dialogSprites[i][on ? 1 : 0];
            dialogLabels[i].color = on ? TextOnActive : TextOnWood;
            dialogLabelRects[i].anchoredPosition = new Vector2(on ? 4f : 3f, on ? -3f : -2f);
        }
    }

    private void CloseDialog()
    {
        if (dialog == null) return;
        Destroy(dialog.gameObject);
        dialog = null;
        dialogFrames = null;
        dialogLabels = null;
        dialogLabelRects = null;
        dialogConfirm = null;
    }

    private void ConfirmDialog()
    {
        System.Action action = dialogConfirm;
        CloseDialog();
        PlayClick();
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
                Key = "ui.pause.group.survival", Fallback = "ÜBERLEBEN", Dot = "group_dot_red",
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
                Key = "ui.pause.group.offense", Fallback = "OFFENSIVE", Dot = "group_dot_yellow",
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
                Key = "ui.pause.group.utility", Fallback = "NUTZEN", Dot = "group_dot_green",
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
        if (groups == null) return;

        int i = 0;
        foreach (StatGroup group in groups)
        {
            foreach (StatDef def in group.Rows)
            {
                if (i >= cells.Count) return;

                float value = def.Value();
                string text = value.ToString(def.Format);

                // Nullwerte gedimmt, aber weiterhin gut lesbar - nicht heller.
                Color color = Mathf.Approximately(value, 0f) ? TextDim : TextValue;

                StatCells cell = cells[i++];
                if (cell.Value.text != text) cell.Value.text = text;
                cell.Value.color = color;
                cell.Name.color = color;
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

    /// <summary>Nach einem Sprachwechsel: alles Beschriftete neu setzen.</summary>
    private void RefreshTexts()
    {
        foreach (Row row in rows) row.Label.text = EntryText(row.What);
        foreach (StatCells cell in cells) cell.Name.text = Loc.Get(cell.Def.Key, cell.Def.Fallback);
        RefreshStats();
    }

    // ==================================================================
    //  Kleinkram
    // ==================================================================

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private static Sprite Gfx(string name)
    {
        // != null statt TryGetValue allein: raeumt Unity die Resources zwischen
        // zwei Szenen auf, steht im Cache eine zerstoerte Referenz.
        if (spriteCache.TryGetValue(name, out Sprite cached) && cached != null) return cached;

        Sprite s = Resources.Load<Sprite>(UiPath + name);
        if (s == null)
        {
            Debug.LogWarning($"[Pause] Sprite '{name}' fehlt - erwartet wird " +
                             $"Assets/Resources/{UiPath}{name}.png");
        }
        spriteCache[name] = s;
        return s;
    }

    /// <summary>
    /// Jersey10 zuerst: die andere Pixelfont des Projekts (ThaleahFat) kann
    /// keine Umlaute, und hier steht deutscher Text.
    /// </summary>
    private static TMP_FontAsset FindFont()
    {
        TMP_FontAsset best = null;

        foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (!HasUmlaut(f)) continue;
            if (f.name.StartsWith("Jersey10")) return f;
            if (best == null) best = f;
        }

        if (best != null) return best;

        Debug.LogWarning("[Pause] Keine geladene Schrift kann Umlaute - der Text bekommt " +
                         "Luecken. Erwartet wird Jersey10 aus Assets/Imports/Jersey10_PixelFont.");
        return PixelUI.FindPixelFont();
    }

    /// <summary>
    /// Dynamische Schriften melden ein Zeichen erst, wenn es im Atlas liegt -
    /// deshalb mit tryAddCharacter fragen, sonst sagen alle erstmal nein.
    /// </summary>
    private static bool HasUmlaut(TMP_FontAsset font)
    {
        try
        {
            return font.HasCharacter('ü', true, true);
        }
        catch
        {
            return font.HasCharacter('ü');
        }
    }

    private static Color Hex(int rgb) => new Color32(
        (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

    /// <summary>Rechteck im 320x180-Raster: x/y zaehlen von oben links.</summary>
    private static RectTransform Rect(string name, Transform parent, float x, float y,
                                      float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    private static Image Img(string name, Transform parent, float x, float y, float w, float h,
                             Sprite sprite)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Einfarbige Flaeche - fuer die 1px-Linien, die kein Sprite brauchen.</summary>
    private static Image Solid(string name, Transform parent, float x, float y, float w, float h,
                               Color color)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Image Stretch(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Die Hoehe muss groesser sein als die Schriftgroesse. Ist das Rechteck zu
    /// flach, wirft TextMeshPro die Zeile still weg und es steht gar nichts da.
    /// </summary>
    private TMP_Text Label(string name, Transform parent, float x, float y, float w, float h,
                           string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(name, parent, x, y, w, h);

        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static void PlayClick()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.MenuClick != null) audio.PalySound(audio.MenuClick);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }
}
