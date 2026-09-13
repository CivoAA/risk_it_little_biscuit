using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Vollbild-Optionen, komplett per Code gebaut - kein Szenen-Objekt, kein Prefab,
/// keine Grafikdatei. Aufgerufen wird das über OptionsPanel.Open(); existiert noch
/// keins, legt sich das Panel selbst an und räumt sich beim Schließen wieder weg.
///
/// Gebaut wird auf dem 320x180-Raster des Hauptmenüs, damit die Kanten auf ganzen
/// Pixeln sitzen. Statt Dropdowns und durchgehenden Reglern gibt es Pfeil-Auswahl
/// und Balken aus zehn Blöcken: beides bleibt beim Hochskalieren scharf und ist
/// das, was man in einem Pixel-Spiel erwartet.
/// </summary>
public class OptionsPanel : MonoBehaviour
{
    private const int RefWidth = 320;
    private const int RefHeight = 180;

    private const float ContentWidth = 240f;
    private const float RowHeight = 14f;
    private const float ControlWidth = 132f;
    private const float LabelWidth = 96f;
    private const float SizeText = 8f;
    private const float SizeTitle = 14f;

    private const string KeyFullscreen = "opt_fullscreen";
    private const string KeyResW = "opt_res_w";
    private const string KeyResH = "opt_res_h";
    private const string KeyVSync = "opt_vsync";

    private static OptionsPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset font;
    private RectTransform content;
    private float cursor;
    private readonly List<VolumeRow> volumeRows = new List<VolumeRow>();
    private List<Vector2Int> resolutions;

    private int fullscreenIndex;
    private int resIndex;
    private int vsyncIndex;

    // ---------- Öffnen / Schließen ----------

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
        font = PixelUI.FindPixelFont();
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ---------- Aufbau ----------

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Über dem Menü, damit nichts durchscheint.
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Vollflächiger Hintergrund - fängt auch die Klicks ab, die daneben gehen.
        Image backdrop = PixelUI.Panel("Backdrop", transform, Vector2.zero, Vector2.zero, PixelUI.Backdrop);
        RectTransform br = backdrop.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = Vector2.zero;
        br.anchoredPosition = Vector2.zero;
        backdrop.raycastTarget = true;

        // Spalte, in der alles von oben nach unten gestapelt wird.
        content = PixelUI.Rect("Content", transform, new Vector2(ContentWidth, RefHeight),
                               new Vector2(0f, -9f), new Vector2(0.5f, 1f));
        cursor = 0f;

        PixelUI.Label("Title", content, new Vector2(ContentWidth, SizeTitle), Top(),
                      "OPTIONEN", SizeTitle, PixelUI.TextAccent, TextAlignmentOptions.Center, font,
                      new Vector2(0.5f, 1f));
        Advance(SizeTitle, 6f);

        SectionHeader("ANZEIGE");
        BuildDisplayRows();
        Gap(5f);

        SectionHeader("TON");
        BuildAudioRows();
        Gap(6f);

        Button back = AddButton("ZURUECK", 90f, 16f);
        back.onClick.AddListener(() =>
        {
            PlayClick();
            Close();
        });
    }

    private static void EnsureEventSystem()
    {
        // In der Menü-Szene gibt es bereits eins - dann nichts anfassen.
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    // ---------- Stapel-Hilfen ----------

    private Vector2 Top()
    {
        return new Vector2(0f, cursor);
    }

    private void Advance(float height, float gap)
    {
        cursor -= height + gap;
    }

    private void Gap(float g)
    {
        cursor -= g;
    }

    private void SectionHeader(string text)
    {
        PixelUI.Label("Header_" + text, content, new Vector2(ContentWidth, 9f), Top(),
                      text, SizeText, PixelUI.TextDim, TextAlignmentOptions.Left, font,
                      new Vector2(0.5f, 1f));
        Advance(9f, 1f);
    }

    private RectTransform AddRow(string name, string labelText)
    {
        RectTransform row = PixelUI.Rect(name, content, new Vector2(ContentWidth, RowHeight),
                                         Top(), new Vector2(0.5f, 1f));

        TMP_Text label = PixelUI.Label("Label", row, new Vector2(LabelWidth, RowHeight), Vector2.zero,
                                       labelText, SizeText, PixelUI.TextNormal,
                                       TextAlignmentOptions.Left, font, new Vector2(0f, 0.5f));
        label.rectTransform.anchoredPosition = new Vector2(2f, 0f);

        Advance(RowHeight, 2f);
        return row;
    }

    private Button AddButton(string text, float width, float height)
    {
        RectTransform holder = PixelUI.Rect("ButtonRow", content, new Vector2(ContentWidth, height),
                                            Top(), new Vector2(0.5f, 1f));
        Button b = PixelUI.TextButton("Back", holder, new Vector2(width, height), Vector2.zero, text,
                                      SizeText, font);
        Advance(height, 0f);
        return b;
    }

    private static RectTransform ControlArea(RectTransform row)
    {
        return PixelUI.Rect("Control", row, new Vector2(ControlWidth, RowHeight),
                            new Vector2(-2f, 0f), new Vector2(1f, 0.5f));
    }

    private static void PlayClick()
    {
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();
    }

    // ---------- Anzeige ----------

    private void BuildDisplayRows()
    {
        resolutions = BuildResolutionList();

        fullscreenIndex = Screen.fullScreen ? 0 : 1;
        vsyncIndex = QualitySettings.vSyncCount > 0 ? 0 : 1;

        resIndex = resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
        if (resIndex < 0) resIndex = resolutions.Count - 1;

        BuildSelectorRow("MODUS", new[] { "VOLLBILD", "FENSTER" }, fullscreenIndex, i =>
        {
            fullscreenIndex = i;
            ApplyDisplay();
        });

        string[] resLabels = new string[resolutions.Count];
        for (int i = 0; i < resolutions.Count; i++)
            resLabels[i] = resolutions[i].x + " x " + resolutions[i].y;

        BuildSelectorRow("AUFLOESUNG", resLabels, resIndex, i =>
        {
            resIndex = i;
            ApplyDisplay();
        });

        BuildSelectorRow("VSYNC", new[] { "AN", "AUS" }, vsyncIndex, i =>
        {
            vsyncIndex = i;
            QualitySettings.vSyncCount = i == 0 ? 1 : 0;
            PlayerPrefs.SetInt(KeyVSync, QualitySettings.vSyncCount);
            PlayerPrefs.Save();
        });
    }

    private static List<Vector2Int> BuildResolutionList()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            // Alles unter 640 Breite ist für dieses Spiel sinnlos klein.
            if (r.width < 640) continue;
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }

        // Im Editor liefert Screen.resolutions je nach Plattform gar nichts.
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));

        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    private void ApplyDisplay()
    {
        Vector2Int res = resolutions[Mathf.Clamp(resIndex, 0, resolutions.Count - 1)];
        FullScreenMode mode = fullscreenIndex == 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        Screen.SetResolution(res.x, res.y, mode);

        PlayerPrefs.SetInt(KeyFullscreen, fullscreenIndex == 0 ? 1 : 0);
        PlayerPrefs.SetInt(KeyResW, res.x);
        PlayerPrefs.SetInt(KeyResH, res.y);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Holt die gespeicherten Anzeige-Einstellungen zurück, bevor die erste Szene lädt.
    /// Wurde noch nie etwas gesetzt, bleibt alles so, wie das System es vorgibt.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettings()
    {
        if (PlayerPrefs.HasKey(KeyVSync)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVSync, 1);
        if (!PlayerPrefs.HasKey(KeyResW)) return;

        int w = PlayerPrefs.GetInt(KeyResW, Screen.width);
        int h = PlayerPrefs.GetInt(KeyResH, Screen.height);
        FullScreenMode mode = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Screen.SetResolution(w, h, mode);
    }

    // ---------- Ton ----------

    private void BuildAudioRows()
    {
        BuildVolumeRow("MASTER",
            () => AudioSettingsManager.Instance != null
                ? AudioSettingsManager.Instance.currentSettings.masterVolume : 1f,
            v =>
            {
                if (AudioSettingsManager.Instance != null) AudioSettingsManager.Instance.SetMasterVolume(v);
            });

        BuildVolumeRow("MUSIK",
            () => AudioSettingsManager.Instance != null
                ? AudioSettingsManager.Instance.currentSettings.musicVolume : 1f,
            v =>
            {
                if (AudioSettingsManager.Instance != null) AudioSettingsManager.Instance.SetMusicVolume(v);
            });

        BuildVolumeRow("EFFEKTE",
            () => AudioSettingsManager.Instance != null
                ? AudioSettingsManager.Instance.currentSettings.effectsVolume : 1f,
            v =>
            {
                if (AudioSettingsManager.Instance != null) AudioSettingsManager.Instance.SetEffectsVolume(v);
            });
    }

    // ---------- Zeilentypen ----------

    /// <summary>Beschriftung links, daneben der Wert zwischen zwei Pfeilen.</summary>
    private void BuildSelectorRow(string label, string[] options, int startIndex, System.Action<int> onChange)
    {
        RectTransform row = AddRow("Row_" + label, label);
        RectTransform area = ControlArea(row);

        RectTransform box = PixelUI.Framed("Box", area, new Vector2(104f, 12f), Vector2.zero,
                                           PixelUI.RowFill, PixelUI.Outline);
        TMP_Text value = PixelUI.Label("Value", box, new Vector2(102f, 12f), Vector2.zero,
                                       options[Mathf.Clamp(startIndex, 0, options.Length - 1)],
                                       SizeText, PixelUI.TextNormal, TextAlignmentOptions.Center, font);

        Selector sel = new Selector
        {
            Value = value,
            Options = options,
            Index = Mathf.Clamp(startIndex, 0, options.Length - 1),
            OnChange = onChange
        };

        Button prev = PixelUI.TextButton("Prev", area, new Vector2(12f, 12f), new Vector2(-60f, 0f),
                                         "<", SizeText, font);
        Button next = PixelUI.TextButton("Next", area, new Vector2(12f, 12f), new Vector2(60f, 0f),
                                         ">", SizeText, font);

        prev.onClick.AddListener(() =>
        {
            sel.Step(-1);
            PlayClick();
        });
        next.onClick.AddListener(() =>
        {
            sel.Step(+1);
            PlayClick();
        });
    }

    /// <summary>Beschriftung links, dann zehn anklickbare Blöcke und der Prozentwert.</summary>
    private void BuildVolumeRow(string label, System.Func<float> get, System.Action<float> set)
    {
        RectTransform row = AddRow("Row_" + label, label);
        RectTransform area = ControlArea(row);

        Image[] blocks = new Image[10];
        for (int i = 0; i < 10; i++)
        {
            int step = i + 1;
            // Blockraster: 10 px breit, 1 px Luft. Alle Kanten landen auf ganzen Pixeln.
            Image outer = PixelUI.Panel("Block" + i, area, new Vector2(10f, 10f),
                                        new Vector2(-61f + 11f * i, 0f), PixelUI.Outline);
            outer.raycastTarget = true;

            Image fill = PixelUI.Panel("Fill", outer.transform, new Vector2(8f, 8f), Vector2.zero,
                                       PixelUI.BarEmpty);
            blocks[i] = fill;

            Button b = outer.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = outer;
            b.onClick.AddListener(() =>
            {
                set(step / 10f);
                PlayClick();
                RefreshVolumes();
            });
        }

        TMP_Text percent = PixelUI.Label("Percent", area, new Vector2(20f, RowHeight), new Vector2(56f, 0f),
                                         "", SizeText, PixelUI.TextAccent, TextAlignmentOptions.Right, font);

        VolumeRow vr = new VolumeRow { Blocks = blocks, Percent = percent, Get = get };
        volumeRows.Add(vr);
        vr.Refresh();
    }

    private void RefreshVolumes()
    {
        foreach (VolumeRow r in volumeRows) r.Refresh();

        // Hält alte VolumeSlider-Komponenten in Sicht, falls in einer Szene noch welche hängen.
        if (AudioSettingsManager.Instance != null) AudioSettingsManager.Instance.UpdateAllVolumeSliders();
    }

    private class Selector
    {
        public TMP_Text Value;
        public string[] Options;
        public int Index;
        public System.Action<int> OnChange;

        public void Step(int delta)
        {
            if (Options == null || Options.Length == 0) return;
            Index = (Index + delta + Options.Length) % Options.Length;
            Value.text = Options[Index];
            if (OnChange != null) OnChange(Index);
        }
    }

    private class VolumeRow
    {
        public Image[] Blocks;
        public TMP_Text Percent;
        public System.Func<float> Get;

        public void Refresh()
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(Get()) * 10f);
            for (int i = 0; i < Blocks.Length; i++)
                Blocks[i].color = i < filled ? PixelUI.TextAccent : PixelUI.BarEmpty;
            Percent.text = (filled * 10) + "%";
        }
    }
}
