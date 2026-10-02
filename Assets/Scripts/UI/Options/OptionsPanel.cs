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
///   Reiter      AUDIO | ANZEIGE | SPIEL             ESC ZURUECK
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
/// Der FEEDBACK-Knopf oeffnet <see cref="FeedbackPanel"/> darueber. Der
/// Reiter SPIEL zeigt die Version, oeffnet <see cref="CreditsPanel"/> und
/// setzt ueber <see cref="SaveReset"/> den Spielstand zurueck (mit Rueckfrage).
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
    private const int MuteW = 30;
    private const int RowBtnMinW = 60;

    // Rueckfrage wie im Pausenmenue, etwas hoeher fuer drei Zeilen Text.
    private const int DlgX = 146, DlgY = 92, DlgW = 188, DlgH = 86;
    private const int DlgTextY = 101, DlgTextH = 40;
    private const int DlgBtnY = 150, DlgBtnW = 80, DlgBtnH = 18;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private enum Tab { Audio, Display, Game }
    private const int TabCount = 3;

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

    private RectTransform dialog, dialogPage;
    private System.Action dialogConfirm;

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
        CreditsPanel.Close();
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
            if (dialogPage != null) dialogPage.anchoredPosition = page.anchoredPosition;
        }

        // Feedback und Credits liegen darueber und haben die Tastatur.
        if (FeedbackPanel.IsOpen || CreditsPanel.IsOpen || Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OptionsKit.PlayClick();
            if (dialog != null) CloseDialog();
            else Close();
            return;
        }

        if (dialog != null) return;

        if (Input.GetKeyDown(KeyCode.Tab)) SetTab((Tab)(((int)tab + 1) % TabCount));

        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f)) Scroll(-wheel * RowStep);
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void OnLanguageChanged()
    {
        // Alles neu: Titel, Reiter und Knoepfe aendern ihre Breite mit.
        CloseDialog();
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
            Loc.Get("ui.options.tab.game", "SPIEL"),
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

        switch (tab)
        {
            case Tab.Audio:
                description.text = Loc.Get("ui.options.desc.audio", "Lautstärke von Musik und Geräuschen.");
                BuildAudioRows();
                break;

            case Tab.Display:
                description.text = Loc.Get("ui.options.desc.display",
                                           "Fenster, Auflösung und was im Spiel angezeigt wird.");
                BuildDisplayRows();
                break;

            default:
                description.text = SaveReset.Allowed
                    ? Loc.Get("ui.options.desc.game", "Version, Credits und dein Spielstand.")
                    : Loc.Get("ui.options.desc.game.run", "Zurücksetzen geht nicht während eines Laufs.");
                BuildGameRows();
                break;
        }

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
               v => { if (Audio != null) Audio.SetMasterVolume(v); },
               () => Audio != null && Audio.currentSettings.masterMuted,
               m => { if (Audio != null) Audio.SetMasterMuted(m); });

        BarRow(Loc.Get("ui.options.audio.music", "Musik"),
               () => Audio != null ? Audio.currentSettings.musicVolume : GameSettings.DefaultVolume,
               v => { if (Audio != null) Audio.SetMusicVolume(v); },
               () => Audio != null && Audio.currentSettings.musicMuted,
               m => { if (Audio != null) Audio.SetMusicMuted(m); });

        BarRow(Loc.Get("ui.options.audio.sfx", "Geräusche"),
               () => Audio != null ? Audio.currentSettings.effectsVolume : GameSettings.DefaultVolume,
               v => { if (Audio != null) Audio.SetEffectsVolume(v); },
               () => Audio != null && Audio.currentSettings.effectsMuted,
               m => { if (Audio != null) Audio.SetEffectsMuted(m); });
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

    private void BuildGameRows()
    {
        TextRow(Loc.Get("ui.options.game.version", "Version"), Application.version);

        ButtonRow(Loc.Get("ui.options.game.credits", "Credits"),
                  Loc.Get("ui.options.game.credits.btn", "ANZEIGEN"), SkinButton.Kind.Wood, CreditsPanel.Open);

        SkinButton reset = ButtonRow(Loc.Get("ui.options.game.reset", "Spielstand zurücksetzen"),
                                     Loc.Get("ui.options.game.reset.btn", "ZURÜCKSETZEN"), SkinButton.Kind.Danger,
                                     () => OpenDialog(Loc.Get("ui.options.game.reset.confirm",
                                                              "Wirklich alles löschen? Münzen, Upgrades, Skills und Erfolge sind danach weg."),
                                                      ResetSave));
        reset.Disabled = !SaveReset.Allowed;
    }

    private static void ResetSave()
    {
        // Das Pausenmenue (Hub) liegt versteckt darunter - mit weg, bevor die Szene wechselt.
        Close();
        PauseMenuPanel.Close();
        SaveReset.ResetAll();
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

    /// <summary>
    /// Lautstaerke: AUS-Knopf, zehn Zellen (klicken oder ziehen) und der
    /// Prozentwert. AUS schaltet den Kanal stumm, ohne die Stufe zu vergessen;
    /// ein Klick in die Leiste schaltet ihn wieder ein.
    /// </summary>
    private void BarRow(string name, System.Func<float> get, System.Action<float> set,
                        System.Func<bool> getMuted, System.Action<bool> setMuted)
    {
        RectTransform row = NewRow(name);

        float x0 = RowW - 4 - ValueW - 6 - (CellCount - 1) * CellStep - CellW;
        float cy = Mathf.Round((RowH - CellH) / 2f);

        Image[] cells = new Image[CellCount];
        int hovered = -1;

        TextMeshProUGUI value = OptionsKit.Label("Value", row, RowW - 4 - ValueW, 1, ValueW, RowH - 2, "",
                                                 textFont, OptionsKit.SizeText, GameHudSkin.Cream,
                                                 TextAlignmentOptions.Right);

        SkinButton mute = SkinButton.Create(row, x0 - 6 - MuteW, Mathf.Round((RowH - ChipH) / 2f), MuteW, ChipH,
                                            Loc.Get("ui.options.audio.mute", "AUS"), textFont, SkinButton.Kind.Wood,
                                            () =>
                                            {
                                                setMuted(!getMuted());
                                                RefreshRows();
                                            });

        System.Action refresh = () =>
        {
            bool muted = getMuted();
            mute.Active = muted;

            int filled = muted ? 0 : Mathf.RoundToInt(Mathf.Clamp01(get()) * CellCount);
            for (int i = 0; i < cells.Length; i++)
            {
                GameHudSkin.CellKind kind = hovered >= 0 && i <= hovered ? GameHudSkin.CellKind.Hover
                                          : i < filled ? GameHudSkin.CellKind.Full
                                          : GameHudSkin.CellKind.Empty;
                cells[i].sprite = GameHudSkin.VolumeCell(kind);
            }
            value.text = muted ? Loc.Get("ui.options.audio.mute", "AUS") : (filled * 10) + "%";
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

    /// <summary>Nur Anzeige: Wert rechtsbuendig.</summary>
    private void TextRow(string name, string value)
    {
        RectTransform row = NewRow(name);
        OptionsKit.Label("Value", row, RowW - 4 - 160, 1, 160, RowH - 2, value, textFont, OptionsKit.SizeText,
                         GameHudSkin.Cream, TextAlignmentOptions.Right);
        rows.Add(() => { });
    }

    /// <summary>Ein Knopf rechtsbuendig, so breit wie seine Beschriftung.</summary>
    private SkinButton ButtonRow(string name, string label, SkinButton.Kind kind, System.Action onClick)
    {
        RectTransform row = NewRow(name);

        TextMeshProUGUI probe = OptionsKit.Label("Probe", row, 0, 0, 200, 14, "", textFont,
                                                 OptionsKit.SizeText, Color.clear, TextAlignmentOptions.Left);
        float width = Mathf.Max(RowBtnMinW, Mathf.Ceil(OptionsKit.Measure(probe, label)) + ChipPad);
        probe.transform.SetParent(null, false);
        Destroy(probe.gameObject);

        SkinButton button = SkinButton.Create(row, RowW - 3 - width, Mathf.Round((RowH - ChipH) / 2f), width, ChipH,
                                              label, textFont, kind, onClick);
        rows.Add(() => { });
        return button;
    }

    // ---------- Rueckfrage ----------

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

        dialogPage = OptionsKit.Rect("Page", dialog, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        dialogPage.anchoredPosition = page.anchoredPosition;

        OptionsKit.Img("Card", dialogPage, DlgX, DlgY, DlgW, DlgH, GameHudSkin.Card, true);
        TextMeshProUGUI q = OptionsKit.Label("Question", dialogPage, DlgX + 10, DlgTextY, DlgW - 20, DlgTextH,
                                             question, textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                             TextAlignmentOptions.Center);
        q.textWrappingMode = TextWrappingModes.Normal;

        int gap = DlgW - 20 - 2 * DlgBtnW;
        SkinButton.Create(dialogPage, DlgX + 10, DlgBtnY, DlgBtnW, DlgBtnH,
                          Loc.Get("ui.pause.dialog.cancel", "ABBRECHEN"), textFont, SkinButton.Kind.Wood,
                          CloseDialog);
        SkinButton.Create(dialogPage, DlgX + 10 + DlgBtnW + gap, DlgBtnY, DlgBtnW, DlgBtnH,
                          Loc.Get("ui.pause.dialog.confirm", "BESTÄTIGEN"), textFont, SkinButton.Kind.Danger,
                          ConfirmDialog);
    }

    private void CloseDialog()
    {
        if (dialog == null) return;
        dialog.SetParent(null, false);
        Destroy(dialog.gameObject);
        dialog = null;
        dialogPage = null;
        dialogConfirm = null;
    }

    private void ConfirmDialog()
    {
        System.Action action = dialogConfirm;
        CloseDialog();
        action?.Invoke();
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
