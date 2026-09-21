using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Der Optionen-Bildschirm: dieselbe Pergamenttafel wie das Pausenmenü, links
/// eine schmale Reiterleiste (AUDIO / ANZEIGE), rechts die Einstellungsliste,
/// unten eine Fußleiste mit STANDARD und ÜBERNEHMEN.
///
///   OptionsPanel.Open();
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, nichts im
/// Inspector. Aufgerufen aus dem Hauptmenü (<see cref="MainMenuController"/>)
/// und aus dem Pausenmenü (<see cref="PauseMenuPanel"/>), in beiden
/// Ausprägungen.
///
/// Die Grafik kommt aus Assets/Resources/OptionsMenu/ui/; die Tafel selbst
/// (Overlay, board_panel, header_bar, btn_normal, btn_active) wird aus
/// Assets/Resources/PauseMenu/ui/ mitbenutzt - es ist dieselbe Tafel, und
/// zwei Kopien derselben Pixel würden nur auseinanderlaufen. Alle Maße stehen
/// in Assets/Art/UI_Objects/OptionsMenu/OPTIONS_UI.md. Jede Zahl hier ist ein
/// Pixel im 320x180-Raster, Ursprung oben links.
///
/// Die Werte hängen an den vorhandenen Systemen: Ton am
/// <see cref="AudioSettingsManager"/>, Sprache an <see cref="Loc"/>, alles
/// andere an <see cref="GameSettings"/>. Das Fenster hält selbst nichts.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (alle aus OPTIONS_UI.md)
    // ==================================================================

    private const int RefW = 320;
    private const int RefH = 180;

    private const string UiPath = "OptionsMenu/ui/";
    private const string SharedPath = "PauseMenu/ui/";

    private const float BoardX = 32f, BoardY = 12f, BoardW = 258f, BoardH = 158f;
    private const float HeaderX = 33f, HeaderY = 13f, HeaderW = 254f, HeaderH = 22f;
    private static readonly Vector4 TitleZone = new Vector4(39f, 17f, 60f, 14f);
    private const float HdrLineX = 104f, HdrLineY = 23f, HdrLineW = 118f;
    private static readonly Vector4 EscZone = new Vector4(227f, 18f, 54f, 12f);

    private const float RailX = 33f, RailY = 35f, RailW = 62f, RailH = 110f;
    private const float DivX = 95f;
    private const float TabX = 36f, TabW = 54f, TabH = 16f, TabY0 = 39f, TabStep = 18f;
    private const float TabDotDX = 4f, TabDotDY = 5f, TabDotS = 5f;
    private const float TabTextDX = 12f, TabTextDY = 3f, TabTextW = 38f;
    private const float DivHX = 36f, DivHY = 116f, DivHW = 54f;
    private static readonly Vector4 DescZone = new Vector4(36f, 119f, 54f, 23f);

    private const float RowX = 101f, RowY0 = 40f, RowW = 176f, RowH = 12f, RowStep = 13f;
    private const float ViewH = 100f;
    private const float ScrollX = 280f, ScrollW = 2f;

    private const float NameDX = 3f, NameW = 50f;
    private const float BarDX = 84f, BarDY = 3f, CellW = 5f, CellH = 6f, CellStep = 6f;
    private const int CellCount = 10;
    private const float ValueDX = 148f, ValueW = 25f;
    private const float ChipRight = 173f, ChipDY = 1f, ChipH = 10f, ChipGap = 2f;
    private const float ChipPad = 8f, ChipMinW = 18f;

    private const float FooterX = 33f, FooterY = 145f, FooterW = 254f, FooterH = 20f;
    private const float BtnStdX = 38f, BtnY = 147f, BtnStdW = 41f, BtnH = 15f;
    private const float BtnApplyX = 229f, BtnApplyW = 53f;

    private const float SizeTitle = 14f, SizeTab = 8f, SizeButton = 8f;
    private const float SizeRow = 7f, SizeChip = 7f, SizeEsc = 7f, SizeDesc = 6f;

    private static readonly Color TextOnWood   = Hex(0xf2dcbc);
    private static readonly Color TextOnActive = Hex(0xfff4e0);
    private static readonly Color TextInk      = Hex(0x3b2b33);
    private static readonly Color TextValue    = Hex(0x4d2e1e);
    private static readonly Color TextDim      = Hex(0x6f4630);
    private static readonly Color TextFaint    = Hex(0xd9b189);
    private static readonly Color HdrLineCol   = Hex(0x5a3421);
    private static readonly Color HdrLineHi    = Hex(0x8a5a3d);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private enum Tab { Audio, Display }

    private static OptionsPanel instance;
    public static bool IsOpen => instance != null;

    /// <summary>Reiter, der beim naechsten Oeffnen vorne liegt.</summary>
    private static Tab startTab = Tab.Audio;

    private TMP_FontAsset font;
    private RectTransform page;
    private RectTransform viewport;
    private RectTransform content;
    private RectTransform scrollHandle;
    private Image scrollTrack;

    private Tab tab;
    private Image[] tabFrames;
    private TMP_Text[] tabLabels;
    private TMP_Text description;

    private readonly List<Row> rows = new List<Row>();
    private float contentHeight;
    private float scrollTop;
    private int openedFrame;

    /// <summary>Eine Zeile der Liste. Refresh liest den Wert neu ein.</summary>
    private class Row
    {
        public System.Action Refresh;
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("OptionsPanel");
        instance = go.AddComponent<OptionsPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        font = FindFont();
        tab = startTab;
        Build();
    }

    private void OnEnable() => Loc.LanguageChanged += Rebuild;

    private void OnDisable() => Loc.LanguageChanged -= Rebuild;

    private void OnDestroy()
    {
        startTab = tab;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PlayClick();
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) SetTab(tab == Tab.Audio ? Tab.Display : Tab.Audio);

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Scroll(-wheel * RowStep);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Ueber allem, damit nichts durchscheint - auch ueber dem Pausenmenue
        // (200), aus dem heraus die Optionen aufgehen.
        canvas.sortingOrder = 210;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        BuildBackdrop();

        page = Rect("Page", transform, 0f, 0f, RefW, RefH);
        page.anchorMin = page.anchorMax = page.pivot = new Vector2(0.5f, 0.5f);
        page.anchoredPosition = Vector2.zero;

        Img("Board", page, BoardX, BoardY, BoardW, BoardH, Shared("board_panel"));
        Img("Header", page, HeaderX, HeaderY, HeaderW, HeaderH, Shared("header_bar"));

        Label("Title", page, TitleZone.x, TitleZone.y, TitleZone.z, TitleZone.w,
              Loc.Get("ui.options.title", "OPTIONEN"), SizeTitle, TextOnWood,
              TextAlignmentOptions.Left);

        Solid("HdrLine", page, HdrLineX, HdrLineY, HdrLineW, 1f, HdrLineCol);
        Solid("HdrLineHi", page, HdrLineX, HdrLineY + 1f, HdrLineW, 1f, HdrLineHi);

        Label("Esc", page, EscZone.x, EscZone.y, EscZone.z, EscZone.w,
              Loc.Get("ui.options.esc", "ESC ZURÜCK"), SizeEsc, TextFaint,
              TextAlignmentOptions.Right);

        BuildRail();
        BuildList();
        BuildFooter();

        Rebuild();
    }

    /// <summary>Dieselben vier Lagen wie im Pausenmenue.</summary>
    private void BuildBackdrop()
    {
        Image dim = Stretch("Dim", transform, Shared("overlay_dim"));
        dim.raycastTarget = true;

        Image scan = Stretch("Scanlines", transform, Shared("overlay_scanlines"));
        scan.type = Image.Type.Tiled;

        Stretch("Vignette", transform, Shared("overlay_vignette"));

        Image frame = Stretch("Frame", transform, Shared("overlay_frame"));
        frame.type = Image.Type.Sliced;
    }

    // ---------- Reiterleiste ----------

    private void BuildRail()
    {
        Img("Rail", page, RailX, RailY, RailW, RailH, Gfx("rail_panel"));
        Img("DividerV", page, DivX, RailY, 1f, RailH, Gfx("divider_v"));

        string[] keys = { "ui.options.tab.audio", "ui.options.tab.display" };
        string[] fallbacks = { "AUDIO", "ANZEIGE" };
        string[] dots = { "dot_audio", "dot_display" };

        tabFrames = new Image[2];
        tabLabels = new TMP_Text[2];

        for (int i = 0; i < 2; i++)
        {
            float y = TabY0 + i * TabStep;

            RectTransform rt = Rect("Tab_" + fallbacks[i], page, TabX, y, TabW, TabH);
            Image frame = rt.gameObject.AddComponent<Image>();
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = true;

            Img("Dot", rt, TabDotDX, TabDotDY, TabDotS, TabDotS, Gfx(dots[i]));
            tabLabels[i] = Label("Label", rt, TabTextDX, TabTextDY, TabTextW, 10f,
                                 Loc.Get(keys[i], fallbacks[i]), SizeTab, TextOnWood,
                                 TextAlignmentOptions.Left);

            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;
            Tab which = (Tab)i;
            button.onClick.AddListener(() => SetTab(which));

            tabFrames[i] = frame;
        }

        Img("DividerH", page, DivHX, DivHY, DivHW, 1f, Gfx("divider_h"));

        description = Label("Desc", page, DescZone.x, DescZone.y, DescZone.z, DescZone.w,
                            "", SizeDesc, TextDim, TextAlignmentOptions.TopLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
    }

    private void SetTab(Tab which)
    {
        if (tab == which && rows.Count > 0) return;
        tab = which;
        PlayClick();
        Rebuild();
    }

    // ---------- Liste ----------

    private void BuildList()
    {
        // Sichtfenster mit Maske: laeuft der Inhalt ueber, wird er hier
        // abgeschnitten - die Tafel bleibt gleich gross.
        viewport = Rect("Viewport", page, RowX, RowY0, RowW, ViewH);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = Rect("Content", viewport, 0f, 0f, RowW, ViewH);

        scrollTrack = Img("ScrollTrack", page, ScrollX, RowY0, ScrollW, ViewH,
                          Gfx("scroll_track"));
        Image handle = Img("ScrollHandle", page, ScrollX, RowY0, ScrollW, ViewH,
                           Gfx("scroll_handle"));
        scrollHandle = handle.rectTransform;
    }

    private void Scroll(float delta)
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        if (hidden <= 0f) return;

        scrollTop = Mathf.Clamp(scrollTop + delta, 0f, hidden);
        // Immer auf ganze Pixel: alles andere verschmiert beim Hochskalieren.
        content.anchoredPosition = new Vector2(0f, Mathf.Round(scrollTop));
        UpdateScrollBar();
    }

    private void UpdateScrollBar()
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        bool needed = hidden > 0f;

        scrollTrack.enabled = needed;
        scrollHandle.gameObject.SetActive(needed);
        if (!needed) return;

        float size = Mathf.Max(8f, Mathf.Round(ViewH * (ViewH / contentHeight)));
        float travel = ViewH - size;
        float y = RowY0 + Mathf.Round(travel * (scrollTop / hidden));

        scrollHandle.sizeDelta = new Vector2(ScrollW, size);
        scrollHandle.anchoredPosition = new Vector2(ScrollX, -y);
    }

    // ---------- Fussleiste ----------

    private void BuildFooter()
    {
        Img("Footer", page, FooterX, FooterY, FooterW, FooterH, Gfx("footer_bar"));

        PixelButton(page, BtnStdX, BtnY, BtnStdW, BtnH, 34f,
                    Loc.Get("ui.options.btn.default", "STANDARD"),
                    Shared("btn_normal"), Shared("btn_active"), TextOnWood, TextOnActive,
                    () =>
                    {
                        GameSettings.ResetToDefaults();
                        Rebuild();
                    });

        PixelButton(page, BtnApplyX, BtnY, BtnApplyW, BtnH, 46f,
                    Loc.Get("ui.options.btn.apply", "ÜBERNEHMEN"),
                    Gfx("btn_primary"), Gfx("btn_primary_active"), TextValue, TextInk,
                    Close);
    }

    /// <summary>
    /// Knopf mit Schlagschatten: der aktive Zustand sitzt 1px nach rechts
    /// unten, die Beschriftung wandert mit.
    /// </summary>
    private void PixelButton(Transform parent, float x, float y, float w, float h, float textW,
                             string text, Sprite normal, Sprite active,
                             Color textNormal, Color textActive, System.Action onClick)
    {
        RectTransform rt = Rect("Btn_" + text, parent, x, y, w, h);
        Image frame = rt.gameObject.AddComponent<Image>();
        frame.sprite = normal;
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = true;

        TMP_Text label = Label("Label", rt, 3f, 2f, textW, 10f, text, SizeButton, textNormal,
                               TextAlignmentOptions.Center);

        Button button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            onClick();
        });

        SpriteHover hover = rt.gameObject.AddComponent<SpriteHover>();
        hover.Setup(frame, label, normal, active, textNormal, textActive);
    }

    /// <summary>
    /// Tauscht Sprite und Textfarbe, solange die Maus draufliegt. Farben
    /// setzen statt tinten: Unitys Tint multipliziert und trifft nie die
    /// Farbe, die gemeint ist.
    /// </summary>
    private class SpriteHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image frame;
        private TMP_Text label;
        private Sprite normal, active;
        private Color textNormal, textActive;
        private RectTransform labelRect;
        private Vector2 labelHome;

        public void Setup(Image img, TMP_Text text, Sprite off, Sprite on, Color cOff, Color cOn)
        {
            frame = img;
            label = text;
            labelRect = text != null ? text.rectTransform : null;
            labelHome = labelRect != null ? labelRect.anchoredPosition : Vector2.zero;
            normal = off;
            active = on;
            textNormal = cOff;
            textActive = cOn;
        }

        public void OnPointerEnter(PointerEventData e) => Apply(true);
        public void OnPointerExit(PointerEventData e) => Apply(false);
        private void OnDisable() => Apply(false);

        private void Apply(bool on)
        {
            if (frame == null) return;
            frame.sprite = on ? active : normal;
            if (label != null) label.color = on ? textActive : textNormal;
            if (labelRect != null)
                labelRect.anchoredPosition = on ? labelHome + new Vector2(1f, -1f) : labelHome;
        }
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    /// <summary>Baut die Liste fuer den aktiven Reiter neu auf.</summary>
    private void Rebuild()
    {
        // Erst abhaengen, dann zerstoeren: Destroy raeumt erst am Frame-Ende
        // auf - die alten Zeilen laegen sonst noch einen Frame lang unter den
        // neuen.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Destroy(old);
        }
        rows.Clear();
        scrollTop = 0f;
        content.anchoredPosition = Vector2.zero;

        for (int i = 0; i < 2; i++)
        {
            bool on = (int)tab == i;
            tabFrames[i].sprite = Gfx(on ? "tab_active" : "tab_inactive");
            tabLabels[i].color = on ? TextInk : TextOnWood;
        }

        description.text = tab == Tab.Audio
            ? Loc.Get("ui.options.desc.audio", "Lautstärke von Musik und Geräuschen.")
            : Loc.Get("ui.options.desc.display", "Fenster, Auflösung und was im Spiel angezeigt wird.");

        if (tab == Tab.Audio) BuildAudioRows();
        else BuildDisplayRows();

        contentHeight = rows.Count * RowStep - (rows.Count > 0 ? RowStep - RowH : 0f);
        content.sizeDelta = new Vector2(RowW, Mathf.Max(ViewH, contentHeight));
        UpdateScrollBar();

        foreach (Row row in rows) row.Refresh();
    }

    private void BuildAudioRows()
    {
        AudioSettingsManager audio = AudioSettingsManager.Instance;

        BarRow(Loc.Get("ui.options.audio.master", "Gesamtlautstärke"),
               () => audio != null ? audio.currentSettings.masterVolume : GameSettings.DefaultVolume,
               v => { if (audio != null) audio.SetMasterVolume(v); });

        BarRow(Loc.Get("ui.options.audio.music", "Musik"),
               () => audio != null ? audio.currentSettings.musicVolume : GameSettings.DefaultVolume,
               v => { if (audio != null) audio.SetMusicVolume(v); });

        BarRow(Loc.Get("ui.options.audio.sfx", "Geräusche"),
               () => audio != null ? audio.currentSettings.effectsVolume : GameSettings.DefaultVolume,
               v => { if (audio != null) audio.SetEffectsVolume(v); });
    }

    private void BuildDisplayRows()
    {
        ChipRow(Loc.Get("ui.options.display.mode", "Modus"),
                new[]
                {
                    Loc.Get("ui.options.mode.window", "Fenster"),
                    Loc.Get("ui.options.mode.borderless", "Randlos"),
                    Loc.Get("ui.options.mode.fullscreen", "Vollbild"),
                },
                () => (int)GameSettings.Mode,
                i => GameSettings.Mode = (GameSettings.DisplayMode)i);

        string[] resLabels = new string[GameSettings.Resolutions.Length];
        for (int i = 0; i < resLabels.Length; i++)
            resLabels[i] = GameSettings.Resolutions[i].x + "x" + GameSettings.Resolutions[i].y;

        ChipRow(Loc.Get("ui.options.display.resolution", "Auflösung"), resLabels,
                () => GameSettings.ResolutionIndex,
                i => GameSettings.ResolutionIndex = i);

        ChipRow(Loc.Get("ui.options.display.vsync", "V-Sync"), OnOff(),
                () => GameSettings.VSync ? 0 : 1,
                i => GameSettings.VSync = i == 0);

        ChipRow(Loc.Get("ui.options.display.fps", "FPS-Limit"),
                new[] { "60", "120", Loc.Get("ui.options.fps.none", "Ohne") },
                () => GameSettings.FpsIndex,
                i => GameSettings.FpsIndex = i);

        ChipRow(Loc.Get("ui.options.display.damage", "Schadenszahlen"), OnOff(),
                () => GameSettings.DamageNumbers ? 0 : 1,
                i => GameSettings.DamageNumbers = i == 0);

        ChipRow(Loc.Get("ui.options.display.tips", "Tipps"), OnOff(),
                () => GameSettings.Tips ? 0 : 1,
                i => GameSettings.Tips = i == 0);

        // Die Sprachkuerzel bleiben unuebersetzt - "DE" heisst in jeder Sprache DE.
        ChipRow(Loc.Get("ui.options.display.language", "Sprache"),
                new[] { "DE", "EN" },
                () => Loc.Language == "de" ? 0 : 1,
                i => Loc.SetLanguage(i == 0 ? "de" : "en"));
    }

    private static string[] OnOff()
    {
        return new[] { Loc.Get("ui.options.on", "An"), Loc.Get("ui.options.off", "Aus") };
    }

    // ---------- Zeilentypen ----------

    /// <summary>Legt Hintergrund und Namen einer Zeile an und gibt ihr Rechteck zurueck.</summary>
    private RectTransform NewRow(string name)
    {
        int index = rows.Count;
        RectTransform row = Rect("Row_" + name, content, 0f, index * RowStep, RowW, RowH);

        // Jede zweite Zeile bekommt den Zebra-Hintergrund.
        if (index % 2 == 1) Img("Zebra", row, 0f, 0f, RowW, RowH, Gfx("row_zebra"));

        Label("Name", row, NameDX, 1f, NameW, 10f, name, SizeRow, TextValue,
              TextAlignmentOptions.Left);
        return row;
    }

    /// <summary>Lautstaerke: zehn anklickbare Zellen und der Prozentwert daneben.</summary>
    private void BarRow(string name, System.Func<float> get, System.Action<float> set)
    {
        RectTransform row = NewRow(name);

        Image[] cells = new Image[CellCount];
        for (int i = 0; i < CellCount; i++)
        {
            int step = i + 1;
            RectTransform rt = Rect("Cell" + i, row, BarDX + i * CellStep, BarDY, CellW, CellH);
            Image img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            cells[i] = img;

            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;
            button.onClick.AddListener(() =>
            {
                set(step / 10f);
                PlayClick();
                RefreshRows();
            });
        }

        TMP_Text value = Label("Value", row, ValueDX, 1f, ValueW, 10f, "", SizeRow, TextDim,
                               TextAlignmentOptions.Right);

        Row entry = new Row();
        entry.Refresh = () =>
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(get()) * CellCount);
            for (int i = 0; i < cells.Length; i++)
                cells[i].sprite = Gfx(i < filled ? "bar_cell_full" : "bar_cell_empty");
            value.text = (filled * 10) + "%";
        };
        rows.Add(entry);
    }

    /// <summary>
    /// Auswahl-Chips, rechtsbuendig in einer Zeile. Alle Chips einer Zeile sind
    /// gleich breit - so ergibt sich eine ruhige Kante, egal wie lang die
    /// einzelne Beschriftung ist.
    /// </summary>
    private void ChipRow(string name, string[] options, System.Func<int> get, System.Action<int> set)
    {
        RectTransform row = NewRow(name);

        float width = ChipMinW;
        foreach (string option in options)
            width = Mathf.Max(width, Mathf.Ceil(Measure(option, SizeChip)) + ChipPad);

        float total = options.Length * width + (options.Length - 1) * ChipGap;
        float x = ChipRight - total;

        Image[] frames = new Image[options.Length];
        TMP_Text[] labels = new TMP_Text[options.Length];

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;

            RectTransform rt = Rect("Chip" + i, row, x, ChipDY, width, ChipH);
            Image img = rt.gameObject.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            frames[i] = img;

            labels[i] = Label("Label", rt, 0f, 0f, width, ChipH, options[i], SizeChip, TextDim,
                              TextAlignmentOptions.Center);

            Button button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;
            button.onClick.AddListener(() =>
            {
                set(index);
                PlayClick();
                RefreshRows();
            });

            x += width + ChipGap;
        }

        Row entry = new Row();
        entry.Refresh = () =>
        {
            int selected = get();
            for (int i = 0; i < frames.Length; i++)
            {
                bool on = i == selected;
                frames[i].sprite = Gfx(on ? "chip_on" : "chip_off");
                labels[i].color = on ? TextOnActive : TextDim;
            }
        };
        rows.Add(entry);
    }

    /// <summary>
    /// Nach jeder Aenderung: alle Zeilen neu einlesen. Die Sprachumstellung
    /// baut ueber Loc.LanguageChanged ohnehin das ganze Fenster neu.
    /// </summary>
    private void RefreshRows()
    {
        foreach (Row row in rows) row.Refresh();

        // Haelt alte VolumeSlider-Komponenten in Sicht, falls in einer Szene
        // noch welche haengen.
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.UpdateAllVolumeSliders();
    }

    // ==================================================================
    //  Kleinkram
    // ==================================================================

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private static Sprite Load(string path, string name)
    {
        string key = path + name;
        if (spriteCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Sprite s = Resources.Load<Sprite>(path + name);
        if (s == null)
        {
            Debug.LogWarning($"[Optionen] Sprite '{name}' fehlt - erwartet wird " +
                             $"Assets/Resources/{path}{name}.png");
        }
        spriteCache[key] = s;
        return s;
    }

    private static Sprite Gfx(string name) => Load(UiPath, name);

    /// <summary>Tafel und Overlay teilt sich dieses Fenster mit dem Pausenmenue.</summary>
    private static Sprite Shared(string name) => Load(SharedPath, name);

    /// <summary>
    /// Breite einer Beschriftung in Pixeln. TMP kann das nur an einem echten
    /// Textobjekt - dafuer haelt das Fenster eines im Verborgenen.
    /// </summary>
    private float Measure(string text, float size)
    {
        if (ruler == null)
        {
            // Muss aktiv sein: auf einem abgeschalteten Objekt laeuft TMPs
            // Awake nie, und GetPreferredValues liefert dann 0. Unsichtbar
            // wird es ueber die Deckkraft.
            RectTransform rt = Rect("Ruler", transform, 0f, 0f, 1000f, 20f);
            ruler = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) ruler.font = font;
            ruler.color = new Color(0f, 0f, 0f, 0f);
            ruler.raycastTarget = false;
            ruler.enableAutoSizing = false;
            ruler.textWrappingMode = TextWrappingModes.NoWrap;
        }

        ruler.fontSize = size;
        float width = ruler.GetPreferredValues(text).x;

        // Sollte TMP nichts liefern (fehlende Schrift), lieber grosszuegig schaetzen.
        return width > 0f ? width : text.Length * size * 0.5f;
    }

    private TextMeshProUGUI ruler;

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

        Debug.LogWarning("[Optionen] Keine geladene Schrift kann Umlaute - der Text bekommt " +
                         "Luecken. Erwartet wird Jersey10 aus Assets/Imports/Jersey10_PixelFont.");
        return PixelUI.FindPixelFont();
    }

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
