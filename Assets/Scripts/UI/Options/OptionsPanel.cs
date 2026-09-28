using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Der Optionen-Bildschirm im Stil von "UI 2.0": dieselbe dunkle Karte, das
/// rote Titelband und die Holzknoepfe wie beim Level-Up und im Evo-Buch.
///
///   OptionsPanel.Open();
///
/// Aufbau (Seitenpixel, 480x270):
///   Titelband   OPTIONEN
///   Reiter      AUDIO | ANZEIGE                     ESC ZURUECK
///   Liste       eingelassene Senke, Zeilen im Zebra
///   Hinweis     eine Zeile zum aktiven Reiter
///   Fussleiste  STANDARD      FEEDBACK      FERTIG
///
/// Baut sich komplett per Code auf - kein Szenenobjekt, kein Prefab, keine
/// Bilddatei (Grafik aus <see cref="GameHudSkin"/>). Aufgerufen aus dem
/// Hauptmenue (<see cref="MainMenuController"/>) und aus dem Pausenmenue
/// (<see cref="PauseMenuPanel"/>).
///
/// Die Werte haengen an den vorhandenen Systemen: Ton am
/// <see cref="AudioSettingsManager"/>, Sprache an <see cref="Loc"/>, alles
/// andere an <see cref="GameSettings"/>. Das Fenster haelt selbst nichts.
/// Der FEEDBACK-Knopf oeffnet <see cref="FeedbackPanel"/> darueber.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel, Ursprung oben links)
    // ==================================================================

    private const int CardX = 80, CardY = 40, CardW = 320, CardH = 208;
    private const int RibbonY = 28;
    private static readonly RectInt Content = new RectInt(CardX - 12, RibbonY, CardW + 24, CardY + CardH - RibbonY);

    private const int TabY = 56, TabH = 16, TabX = 94, TabW = 66, TabGap = 4;

    private const int WellX = 92, WellY = 78, WellW = 296, WellH = 126;
    private const int RowX = WellX + 4, RowY0 = WellY + 4, RowW = WellW - 8, RowH = 16, RowStep = 17;
    private const int ViewH = WellH - 8;

    private const int DescY = 207;

    private const int FootY = 223, FootH = 18;

    private const int ChipH = 14, ChipGap = 3, ChipPad = 12, ChipMinW = 26;
    private const int CellCount = 10, CellW = 7, CellH = 9, CellStep = 8;
    private const int ValueW = 26;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private enum Tab { Audio, Display }

    private static OptionsPanel instance;
    public static bool IsOpen => instance != null;

    /// <summary>Reiter, der beim naechsten Oeffnen vorne liegt.</summary>
    private static Tab startTab = Tab.Audio;

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;

    private RectTransform viewport, content;
    private Image scrollHandle;

    private Tab tab;
    private SkinButton[] tabs;
    private TextMeshProUGUI description;

    private readonly List<System.Action> rows = new List<System.Action>();
    private float contentHeight, scrollTop;
    private int openedFrame;

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
        FeedbackPanel.Close();
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
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        tab = startTab;
        Build();
    }

    private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;

    private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

    private void OnDestroy()
    {
        startTab = tab;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
        }

        // Das Feedback-Fenster liegt darueber und hat die Tastatur.
        if (FeedbackPanel.IsOpen || Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OptionsKit.PlayClick();
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

    private void OnLanguageChanged()
    {
        // Alles neu: Titel, Reiter und Knoepfe aendern ihre Breite mit.
        OptionsKit.Clear(transform);
        rows.Clear();
        textFont = PixelUI.FindTextFont();
        Build();
    }

    private void Build()
    {
        // Ueber dem Pausenmenue (200), aus dem heraus die Optionen aufgehen.
        page = OptionsKit.CreatePage(gameObject, 210, 0.88f, out scaler);

        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, Loc.Get("ui.options.title", "OPTIONEN"),
                          pixelFont, textFont);

        BuildTabs();
        BuildList();
        BuildFooter();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        Rebuild();
    }

    private void BuildTabs()
    {
        string[] labels =
        {
            Loc.Get("ui.options.tab.audio", "AUDIO"),
            Loc.Get("ui.options.tab.display", "ANZEIGE"),
        };

        tabs = new SkinButton[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            Tab which = (Tab)i;
            tabs[i] = SkinButton.Create(page, TabX + i * (TabW + TabGap), TabY, TabW, TabH, labels[i],
                                        textFont, SkinButton.Kind.Wood, () => SetTab(which));
        }

        OptionsKit.Label("Esc", page, WellX + WellW - 120, TabY + 2, 118, 13,
                         Loc.Get("ui.options.esc", "ESC ZURÜCK"), textFont, OptionsKit.SizeText,
                         GameHudSkin.Stone, TextAlignmentOptions.Right);
    }

    private void SetTab(Tab which)
    {
        if (tab == which && rows.Count > 0) return;
        tab = which;
        Rebuild();
    }

    // ---------- Liste ----------

    private void BuildList()
    {
        OptionsKit.Img("Well", page, WellX, WellY, WellW, WellH, GameHudSkin.Well, true);

        viewport = OptionsKit.Rect("Viewport", page, RowX, RowY0, RowW, ViewH);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = OptionsKit.Rect("Content", viewport, 0, 0, RowW, ViewH);

        scrollHandle = OptionsKit.Img("ScrollHandle", page, WellX + WellW - 3, RowY0, 2, ViewH,
                                      GameHudSkin.White, GameHudSkin.Stone);

        description = OptionsKit.Label("Desc", page, WellX + 2, DescY, WellW - 4, 13, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.ParchDark, TextAlignmentOptions.Left);
    }

    private void Scroll(float delta)
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        if (hidden <= 0f) return;

        scrollTop = Mathf.Clamp(scrollTop + delta, 0f, hidden);
        content.anchoredPosition = new Vector2(0f, Mathf.Round(scrollTop));
        UpdateScrollBar();
    }

    private void UpdateScrollBar()
    {
        float hidden = Mathf.Max(0f, contentHeight - ViewH);
        scrollHandle.enabled = hidden > 0f;
        if (hidden <= 0f) return;

        float size = Mathf.Max(8f, Mathf.Round(ViewH * (ViewH / contentHeight)));
        float y = RowY0 + Mathf.Round((ViewH - size) * (scrollTop / hidden));
        OptionsKit.Move(scrollHandle.rectTransform, WellX + WellW - 3, y, 2, size);
    }

    // ---------- Fussleiste ----------

    private void BuildFooter()
    {
        const int w = 76;

        SkinButton.Create(page, WellX, FootY, w, FootH,
                          Loc.Get("ui.options.btn.default", "STANDARD"), textFont, SkinButton.Kind.Wood,
                          () =>
                          {
                              GameSettings.ResetToDefaults();
                              RefreshRows();
                          });

        SkinButton.Create(page, CardX + (CardW - 86) / 2, FootY, 86, FootH,
                          Loc.Get("ui.options.btn.feedback", "FEEDBACK"), textFont, SkinButton.Kind.Wood,
                          FeedbackPanel.Open, GameHudSkin.Bug);

        SkinButton.Create(page, WellX + WellW - w, FootY, w, FootH,
                          Loc.Get("ui.options.btn.done", "FERTIG"), textFont, SkinButton.Kind.Primary,
                          Close);
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    /// <summary>Baut die Liste fuer den aktiven Reiter neu auf.</summary>
    private void Rebuild()
    {
        // Erst abhaengen, dann zerstoeren: Destroy raeumt erst am Frame-Ende
        // auf - die alten Zeilen laegen sonst noch einen Frame lang darunter.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Destroy(old);
        }
        rows.Clear();
        scrollTop = 0f;
        content.anchoredPosition = Vector2.zero;

        for (int i = 0; i < tabs.Length; i++) tabs[i].Active = (int)tab == i;

        description.text = tab == Tab.Audio
            ? Loc.Get("ui.options.desc.audio", "Lautstärke von Musik und Geräuschen.")
            : Loc.Get("ui.options.desc.display", "Fenster, Auflösung und was im Spiel angezeigt wird.");

        if (tab == Tab.Audio) BuildAudioRows();
        else BuildDisplayRows();

        contentHeight = rows.Count * RowStep - (rows.Count > 0 ? RowStep - RowH : 0);
        content.sizeDelta = new Vector2(RowW, Mathf.Max(ViewH, contentHeight));
        UpdateScrollBar();

        RefreshRows();
    }

    private void BuildAudioRows()
    {
        // Nicht merken, sondern bei jedem Zugriff frisch holen: der Manager
        // liegt in der Szene nur im Hauptmenue, und ein einmal gemerktes null
        // haette alle drei Leisten fuer immer taub gemacht.
        BarRow(Loc.Get("ui.options.audio.master", "Gesamtlautstärke"),
               () => Audio != null ? Audio.currentSettings.masterVolume : GameSettings.DefaultVolume,
               v => { if (Audio != null) Audio.SetMasterVolume(v); });

        BarRow(Loc.Get("ui.options.audio.music", "Musik"),
               () => Audio != null ? Audio.currentSettings.musicVolume : GameSettings.DefaultVolume,
               v => { if (Audio != null) Audio.SetMusicVolume(v); });

        BarRow(Loc.Get("ui.options.audio.sfx", "Geräusche"),
               () => Audio != null ? Audio.currentSettings.effectsVolume : GameSettings.DefaultVolume,
               v => { if (Audio != null) Audio.SetEffectsVolume(v); });
    }

    private static AudioSettingsManager Audio => AudioSettingsManager.Instance;

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

        // Kurz als "1080p": vier volle "2560x1440"-Chips passen nicht in die Zeile.
        string[] resLabels = new string[GameSettings.ResolutionCount];
        for (int i = 0; i < GameSettings.Resolutions.Length; i++)
            resLabels[i] = GameSettings.Resolutions[i].y + "p";
        resLabels[GameSettings.NativeIndex] = Loc.Get("ui.options.res.native", "Nativ");

        ChipRow(Loc.Get("ui.options.display.resolution", "Auflösung"), resLabels,
                () => GameSettings.ResolutionIndex,
                i => GameSettings.ResolutionIndex = i);

        // VSync und FPS-Limit gibt es bewusst nicht: VSync ist fest an (siehe
        // GameSettings.ApplyFrameRate).

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

    /// <summary>Hintergrund (jede zweite Zeile) und Name; gibt das Zeilenrechteck zurueck.</summary>
    private RectTransform NewRow(string name)
    {
        int index = rows.Count;
        RectTransform row = OptionsKit.Rect("Row_" + name, content, 0, index * RowStep, RowW, RowH);

        if (index % 2 == 0)
            OptionsKit.Img("Zebra", row, 0, 0, RowW, RowH, GameHudSkin.White, (Color)GameHudSkin.CardFill);

        OptionsKit.Label("Name", row, 6, 1, 120, RowH - 2, name, textFont, OptionsKit.SizeText,
                         GameHudSkin.Parchment, TextAlignmentOptions.Left);
        return row;
    }

    /// <summary>Lautstaerke: zehn Zellen (klicken oder ziehen) und der Prozentwert.</summary>
    private void BarRow(string name, System.Func<float> get, System.Action<float> set)
    {
        RectTransform row = NewRow(name);

        float x0 = RowW - 4 - ValueW - 6 - (CellCount - 1) * CellStep - CellW;
        float cy = Mathf.Round((RowH - CellH) / 2f);

        Image[] cells = new Image[CellCount];
        int hovered = -1;

        TextMeshProUGUI value = OptionsKit.Label("Value", row, RowW - 4 - ValueW, 1, ValueW, RowH - 2, "",
                                                 textFont, OptionsKit.SizeText, GameHudSkin.Cream,
                                                 TextAlignmentOptions.Right);

        System.Action refresh = () =>
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(get()) * CellCount);
            for (int i = 0; i < cells.Length; i++)
            {
                GameHudSkin.CellKind kind = hovered >= 0 && i <= hovered ? GameHudSkin.CellKind.Hover
                                          : i < filled ? GameHudSkin.CellKind.Full
                                          : GameHudSkin.CellKind.Empty;
                cells[i].sprite = GameHudSkin.VolumeCell(kind);
            }
            value.text = (filled * 10) + "%";
        };

        for (int i = 0; i < CellCount; i++)
        {
            int step = i + 1;
            int index = i;
            Image img = OptionsKit.Img("Cell" + i, row, x0 + i * CellStep, cy, CellW, CellH,
                                       GameHudSkin.VolumeCell(GameHudSkin.CellKind.Empty));
            img.raycastTarget = true;
            cells[i] = img;

            PointerRelay relay = img.gameObject.AddComponent<PointerRelay>();
            relay.Down = () =>
            {
                set(step / 10f);
                OptionsKit.PlayClick();
                RefreshRows();
            };
            relay.Enter = () =>
            {
                hovered = index;
                // Mit gedrueckter Maustaste ueber die Leiste ziehen stellt direkt ein.
                if (Input.GetMouseButton(0)) set(step / 10f);
                RefreshRows();
            };
            relay.Exit = () =>
            {
                if (hovered == index) hovered = -1;
                refresh();
            };
        }

        // Ein Klick links neben die erste Zelle heisst stumm.
        Image mute = OptionsKit.Img("Mute", row, x0 - 8, 0, 7, RowH, GameHudSkin.White, Color.clear);
        mute.raycastTarget = true;
        mute.gameObject.AddComponent<PointerRelay>().Down = () =>
        {
            set(0f);
            OptionsKit.PlayClick();
            RefreshRows();
        };

        rows.Add(refresh);
    }

    /// <summary>
    /// Auswahl-Knoepfe, rechtsbuendig. Alle Knoepfe einer Zeile sind gleich
    /// breit - so ergibt sich eine ruhige Kante, egal wie lang die Beschriftung ist.
    /// </summary>
    private void ChipRow(string name, string[] options, System.Func<int> get, System.Action<int> set)
    {
        RectTransform row = NewRow(name);

        TextMeshProUGUI probe = OptionsKit.Label("Probe", row, 0, 0, 200, 14, "", textFont,
                                                 OptionsKit.SizeText, Color.clear, TextAlignmentOptions.Left);
        float width = ChipMinW;
        foreach (string option in options)
            width = Mathf.Max(width, Mathf.Ceil(OptionsKit.Measure(probe, option)) + ChipPad);
        probe.transform.SetParent(null, false);
        Destroy(probe.gameObject);

        float total = options.Length * width + (options.Length - 1) * ChipGap;
        float x = RowW - 3 - total;
        float y = Mathf.Round((RowH - ChipH) / 2f);

        SkinButton[] chips = new SkinButton[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            chips[i] = SkinButton.Create(row, x, y, width, ChipH, options[i], textFont, SkinButton.Kind.Wood,
                                         () =>
                                         {
                                             set(index);
                                             RefreshRows();
                                         });
            x += width + ChipGap;
        }

        rows.Add(() =>
        {
            int selected = get();
            for (int i = 0; i < chips.Length; i++)
                if (chips[i] != null) chips[i].Active = i == selected;
        });
    }

    /// <summary>
    /// Nach jeder Aenderung: alle Zeilen neu einlesen. Die Sprachumstellung
    /// baut ueber Loc.LanguageChanged ohnehin das ganze Fenster neu.
    /// </summary>
    private void RefreshRows()
    {
        foreach (System.Action row in rows) row();

        // Haelt alte VolumeSlider-Komponenten in Sicht, falls in einer Szene
        // noch welche haengen.
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.UpdateAllVolumeSliders();
    }
}
