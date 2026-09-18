using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Die Unlock-Anzeige. Zeigt dasselbe wie die Liste in der World Map - ein Gitter
/// aus Symbolen, daneben Name und Beschreibung des angeklickten Unlocks - baut
/// sich aber komplett per Code auf, genau wie <see cref="AchievementPanel"/>:
/// kein Szenenobjekt, kein Prefab, keine Verdrahtung im Inspector.
///
///   UnlockPanel.Toggle();
///
/// Damit lässt sie sich aus jeder Szene öffnen, ohne dass im Hub ein Canvas
/// gepflegt werden muss.
///
/// HINWEIS FÜR DEN NÄCHSTEN, DER DEN HUB ANFASST: das Fenster hängt im Hub noch
/// an keinem Objekt. Es soll bei den Achievements landen - dort einen Knopf
/// bauen, der <see cref="Toggle"/> ruft, mehr ist nicht nötig. Details stehen in
/// HUB_UNLOCKS_TODO.md.
///
/// Inhalt und Reihenfolge kommen aus <see cref="Unlocks"/> - dieses Skript kennt
/// keinen einzigen Unlock beim Namen. Woher ein Unlock kommt, holt es sich über
/// <see cref="Ach.FindByUnlock"/> aus dem Achievement-Katalog: vergibt ein
/// Achievement den Unlock, steht dessen Name - und bei einem Zähler sein Stand -
/// in der Detailspalte.
/// </summary>
public class UnlockPanel : MonoBehaviour
{
    private const int RefWidth = 320;
    private const int RefHeight = 180;

    private const float SizeTitle = 12f;
    private const float SizeText = 8f;
    private const float SizeSmall = 6f;

    private const float ListX = 6f;
    private const float ListWidth = 136f;
    private const float DetailX = 148f;
    private const float DetailWidth = 166f;
    private const float TopY = -20f;
    private const float BodyHeight = 138f;

    // 6 Spalten: 6*20 + 5*2 Abstand = 130 - passt mit Rand in die 134 des Feldes.
    private const int Columns = 6;
    private const float TileSize = 20f;
    private const float TileIcon = 16f;

    private static UnlockPanel instance;
    public static bool IsOpen => instance != null;

    private TMP_FontAsset font;
    private RectTransform gridContent;

    private readonly List<Tile> tiles = new List<Tile>();
    private UnlockDef selected;

    private Image detailIcon;
    private TMP_Text detailName;
    private TMP_Text detailDesc;
    private TMP_Text detailSource;
    private TMP_Text counter;

    // Nur gesetzt, wenn das Fenster im Hub aufgeht. Im Hauptmenü und im Spiel gibt
    // es kein HubUI - und es wird hier auch keines angelegt.
    private HubUI hub;

    private class Tile
    {
        public UnlockDef Def;
        public Image Frame;
        public Image Icon;
    }

    // ---------- Öffnen / Schliessen ----------

    public static void Open()
    {
        if (instance != null) return;
        GameObject go = new GameObject("UnlockPanel");
        instance = go.AddComponent<UnlockPanel>();
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
        BlockHub(true);
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Refresh;
        Unlocks.Granted += OnGranted;
        Achievements.Unlocked += OnAchievementUnlocked;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Refresh;
        Unlocks.Granted -= OnGranted;
        Achievements.Unlocked -= OnAchievementUnlocked;
    }

    private void OnDestroy()
    {
        BlockHub(false);
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnGranted(UnlockDef def) => Refresh();

    // Vergibt ein Achievement einen Unlock mit, feuert Unlocks.Granted ohnehin.
    // Hier geht es um den Rest: die Herkunftszeile eines noch gesperrten Unlocks
    // nennt ein Achievement, und dessen Stand hat sich gerade geändert.
    private void OnAchievementUnlocked(AchievementDef def) => Refresh();

    /// <summary>
    /// Sperrt den Hub, solange das Fenster offen ist - so wie Shop und Skilltree
    /// es tun. Läuft das Fenster woanders, passiert nichts.
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

    // ---------- Aufbau ----------

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Über der Achievement-Liste (100): von dort aus geht dieses Fenster im
        // Hub auf, es muss also darüber liegen.
        canvas.sortingOrder = 150;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Image backdrop = PixelUI.Panel("Backdrop", transform, Vector2.zero, Vector2.zero, PixelUI.Backdrop);
        RectTransform br = backdrop.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = Vector2.zero;
        br.anchoredPosition = Vector2.zero;
        backdrop.raycastTarget = true;

        BuildHeader();
        BuildGrid();
        BuildDetail();

        Refresh();
        Select(FirstDef());
    }

    private void BuildHeader()
    {
        PixelUI.Label("Title", transform, new Vector2(RefWidth, SizeTitle), new Vector2(0f, -4f),
                      Loc.Get("ui.unlocks.title", "UNLOCKS"), SizeTitle, PixelUI.TextAccent,
                      TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));

        counter = PixelUI.Label("Counter", transform, new Vector2(60f, 10f), new Vector2(-6f, -5f),
                                "", SizeText, PixelUI.TextDim, TextAlignmentOptions.Right, font,
                                new Vector2(1f, 1f));

        Button back = PixelUI.TextButton("Back", transform, new Vector2(44f, 11f), new Vector2(6f, -4f),
                                         Loc.Get("ui.unlocks.close", "BACK"), SizeSmall, font,
                                         new Vector2(0f, 1f));
        back.onClick.AddListener(() =>
        {
            PlayClick();
            Close();
        });
    }

    private void BuildGrid()
    {
        RectTransform frame = PixelUI.Framed("Grid", transform, new Vector2(ListWidth, BodyHeight),
                                             new Vector2(ListX, TopY), PixelUI.PanelFill, PixelUI.Outline,
                                             new Vector2(0f, 1f));
        frame.GetComponent<Image>().raycastTarget = true;

        RectTransform viewport = PixelUI.Rect("Viewport", frame, new Vector2(ListWidth - 4f, BodyHeight - 4f),
                                              Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();

        gridContent = PixelUI.Rect("Content", viewport, new Vector2(ListWidth - 4f, 0f), Vector2.zero,
                                   new Vector2(0.5f, 1f));

        GridLayoutGroup layout = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(TileSize, TileSize);
        layout.spacing = new Vector2(2f, 2f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.padding = new RectOffset(1, 1, 1, 1);

        ContentSizeFitter fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = gridContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 12f;

        foreach (UnlockDef def in Unlocks.All) AddTile(def);
    }

    private void AddTile(UnlockDef def)
    {
        Image frame = PixelUI.Panel("Unlock_" + def.Id, gridContent, new Vector2(TileSize, TileSize),
                                    Vector2.zero, PixelUI.RowFill);
        frame.raycastTarget = true;

        Image icon = PixelUI.Panel("Icon", frame.transform, new Vector2(TileIcon, TileIcon),
                                   Vector2.zero, Color.white);
        icon.preserveAspect = true;

        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;

        UnlockDef captured = def;
        button.onClick.AddListener(() =>
        {
            PlayClick();
            Select(captured);
        });

        tiles.Add(new Tile { Def = def, Frame = frame, Icon = icon });
    }

    private void BuildDetail()
    {
        RectTransform frame = PixelUI.Framed("Detail", transform, new Vector2(DetailWidth, BodyHeight),
                                             new Vector2(DetailX, TopY), PixelUI.PanelFill, PixelUI.Outline,
                                             new Vector2(0f, 1f));

        detailIcon = PixelUI.Panel("Icon", frame, new Vector2(32f, 32f), new Vector2(0f, -8f),
                                   Color.white, new Vector2(0.5f, 1f));
        detailIcon.preserveAspect = true;

        detailName = PixelUI.Label("Name", frame, new Vector2(DetailWidth - 12f, 12f),
                                   new Vector2(0f, -44f), "", SizeText, PixelUI.TextAccent,
                                   TextAlignmentOptions.Center, font, new Vector2(0.5f, 1f));

        detailDesc = PixelUI.Label("Desc", frame, new Vector2(DetailWidth - 16f, 48f),
                                   new Vector2(0f, -58f), "", SizeSmall, PixelUI.TextNormal,
                                   TextAlignmentOptions.Top, font, new Vector2(0.5f, 1f));
        detailDesc.textWrappingMode = TextWrappingModes.Normal;

        detailSource = PixelUI.Label("Source", frame, new Vector2(DetailWidth - 16f, 24f),
                                     new Vector2(0f, -110f), "", SizeSmall, PixelUI.TextDim,
                                     TextAlignmentOptions.Top, font, new Vector2(0.5f, 1f));
        detailSource.textWrappingMode = TextWrappingModes.Normal;
    }

    // ---------- Inhalt ----------

    private static UnlockDef FirstDef()
    {
        IReadOnlyList<UnlockDef> all = Unlocks.All;
        return all.Count > 0 ? all[0] : null;
    }

    public void Refresh()
    {
        counter.text = string.Format(Loc.Get("ui.unlocks.counter", "{0} / {1}"),
                                     Unlocks.UnlockedCount, Unlocks.TotalCount);

        foreach (Tile tile in tiles)
        {
            bool open = tile.Def.IsUnlocked;
            Sprite icon = tile.Def.Icon;

            tile.Icon.enabled = icon != null;
            tile.Icon.sprite = icon;
            tile.Icon.color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        UpdateSelectionTint();
        ShowDetail(selected);
    }

    private void Select(UnlockDef def)
    {
        selected = def;
        UpdateSelectionTint();
        ShowDetail(def);
    }

    private void UpdateSelectionTint()
    {
        foreach (Tile tile in tiles)
        {
            tile.Frame.color = tile.Def == selected ? PixelUI.RowHover : PixelUI.RowFill;
        }
    }

    private void ShowDetail(UnlockDef def)
    {
        if (def == null)
        {
            detailName.text = Loc.Get("ui.unlocks.empty", "Nothing here yet.");
            detailDesc.text = "";
            detailSource.text = "";
            detailIcon.enabled = false;
            return;
        }

        bool open = def.IsUnlocked;
        Sprite icon = def.Icon;

        detailIcon.enabled = icon != null;
        detailIcon.sprite = icon;
        detailIcon.color = open ? Color.white : new Color(1f, 1f, 1f, 0.35f);

        // Gesperrt bleibt gesperrt: wie in der World Map verrät die Anzeige weder
        // Name noch Beschreibung, bevor der Unlock aufgegangen ist.
        detailName.text = open ? def.Name : "???";
        detailName.color = open ? PixelUI.TextAccent : PixelUI.TextDim;
        detailDesc.text = open ? def.Description : "";
        detailSource.text = SourceLine(def, open);
    }

    /// <summary>
    /// Die Zeile unter der Beschreibung: woher der Unlock kommt. Vergibt ein
    /// Achievement ihn, steht dessen Name hier - bei einem Zähler mit Stand, damit
    /// man sieht, wie weit es noch ist. Ein verstecktes Achievement bleibt dabei
    /// versteckt, das regelt <see cref="AchievementDef.Name"/> selbst.
    /// </summary>
    private static string SourceLine(UnlockDef def, bool open)
    {
        AchievementDef source = Ach.FindByUnlock(def.Id);

        if (source == null)
        {
            return open ? "" : Loc.Get("ui.unlocks.hint", "Keep playing to find this one.");
        }

        string line = string.Format(Loc.Get("ui.unlocks.from", "From: {0}"), source.Name);

        if (!source.IsUnlocked && source.HasProgress)
        {
            line += $"  {Mathf.FloorToInt(source.Value)} / {Mathf.FloorToInt(source.Goal)}";
        }

        return line;
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
