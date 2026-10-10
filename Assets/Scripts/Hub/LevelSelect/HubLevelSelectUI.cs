using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Die Levelauswahl im Hub, im Stil von "UI 2.0" (Optionen, Skilltree,
/// Charakterauswahl):
///
///   - links oben das PANORAMA: das breite Levelbild dreifach gross im
///     Holzrahmen. Es schwenkt langsam auf und ab und lebt - in der Kueche
///     tanzt Mehlstaub im Sonnenstrahl, im Wald fallen Blaetter und
///     Gluehwuermchen blinken. Beim Wechsel gleitet das neue Bild herein.
///     Gesperrt = dunkel mit Schloss, noch ohne Szene = Absperrband + BALD.
///   - darunter die REISEROUTE: jedes Level eine Station auf einem
///     Punktepfad. Auf der gewaehlten Station steht der eigene Charakter
///     (Shop.SkinIndex) und huepft zur naechsten, wenn man weitergeht.
///   - rechts der STECKBRIEF: Name, Text, der Boss aus dem Wellenplan (Bild
///     aus dem Bestiarium), daneben der Bestwert (<see cref="LevelRecords"/>:
///     Story = schnellster Sieg, Endless = laengster Lauf), Laufdauer,
///     Gegnerarten und der Schalter Story / Endless.
///   - unten ZURUECK und SPIELEN.
///
/// Stationen waehlen nur aus; gestartet wird ausschliesslich ueber SPIELEN
/// (oder Enter/E) - das war Nicks ausdruecklicher Wunsch.
///
/// Alles liegt auf einer 480x270-Seite (Ursprung oben links, jede Zahl ein
/// Pixel), ganzzahlig skaliert wie bei <see cref="OptionsKit"/>. Treffer
/// rechnet das Skript selbst aus der Mausposition - wie die anderen
/// Hub-Fenster. Grafik aus <see cref="GameHudSkin"/>, dazu die Levelbilder
/// aus dem Inspector und die Gegnerbilder aus Resources/Bestiary.
///
/// ---------------------------------------------------------------------------
///  EIN LEVEL ANBINDEN
/// ---------------------------------------------------------------------------
///  1. In der hub-Szene das Objekt "LevelSelectUI" waehlen, Liste "Levels" -
///     ein Eintrag ist eine Station, die Nummer ist die Position in der Liste.
///  2. Am Eintrag ausfuellen:
///       Map Id        Welche Welt gespielt wird: Map-ID 3 laedt "Map_World3"
///                     (MapSceneSystem). Die Szene MUSS in den Build Settings
///                     stehen, sonst zeigt die Station "BALD". -1 = noch keine Welt.
///       Plan Id       Wellenplan der Karte (wie MapDefinition.planId, z.B.
///                     World2) - daraus kommen Boss, Dauer, Arten.
///                     Leer = der Steckbrief zeigt dazu nichts.
///       Display Name / Description   Rueckfall, wenn die Uebersetzung
///                     (level.N.name / level.N.desc) fehlt.
///       Preview / Preview Wide       Bild der Station (50x50) und Panorama (82x46).
///       Ambience      Stimmung im Panorama. Auto = nach Plan (World1 Kueche,
///                     World2 Wald), sonst Staub. Volcano = Glut + Asche. Snow = Flocken + Polarlicht-Schimmer.
///  3. Sperren ist optional: Story Unlock Id / Endless Unlock Id leer = offen.
///  4. Play() macht daraus denselben Ablauf wie die World Map.
/// ---------------------------------------------------------------------------
/// </summary>
[DisallowMultipleComponent]
public class HubLevelSelectUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    public enum Ambience { Auto, None, Dust, Kitchen, Forest, Volcano, Snow, Ghost }

    /// <summary>Ein Level in der Auswahl.</summary>
    [System.Serializable]
    public class LevelEntry
    {
        [Tooltip("Name im Steckbrief - Rueckfall, wenn level.N.name nicht uebersetzt ist.")]
        public string displayName = "";

        [Tooltip("Text im Steckbrief - Rueckfall, wenn level.N.desc nicht uebersetzt ist.")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Bild der Station (50x50, die Mitte ist zu sehen).")]
        public Sprite preview;

        [Tooltip("Panorama (82x46, wird dreifach gezeichnet). Leer = das Stationsbild.")]
        public Sprite previewWide;

        [Tooltip("Welche Welt geladen wird: Map-ID 3 laedt die Szene Map_World3. " +
                 "-1 = dieses Level hat noch keine Welt und zeigt 'BALD'.")]
        public int mapId;

        [Tooltip("Rueckfallebene, falls es zur Map-ID keine Map-Szene gibt. Normalerweise leer.")]
        public string sceneToLoad = "";

        [Tooltip("Wellenplan (WavePlans), z.B. World1 oder World2 - fuer Boss, Dauer und Gegner im Steckbrief.")]
        public string planId = "";

        [Tooltip("Boss im Steckbrief. None = der Endboss aus dem Wellenplan (fuer Welten ohne Plan hier eintragen).")]
        public EnemyId boss = EnemyId.None;

        [Tooltip("Stimmung im Panorama. Auto = nach Plan.")]
        public Ambience ambience = Ambience.Auto;

        [Tooltip("Optional: Story ist erst offen, wenn diese Unlock-ID freigeschaltet ist. Leer = immer offen.")]
        public string storyUnlockId = "";

        [Tooltip("Hat dieses Level ueberhaupt einen Endless-Modus?")]
        public bool endlessAvailable = true;

        [Tooltip("Optional: Endless ist erst offen, wenn diese Unlock-ID freigeschaltet ist. Leer = immer offen.")]
        public string endlessUnlockId = "";
    }

    [Header("Inhalt")]
    [Tooltip("Reihenfolge der Stationen. Die Nummer ist die Position in dieser Liste.")]
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

    [Header("Darstellung")]
    [Tooltip("Wie stark der Hub hinter dem Fenster abgedunkelt wird.")]
    [SerializeField, Range(0f, 1f)] private float dim = 0.86f;
    [Tooltip("Schwenk, Stimmung, Huepfer. Aus = alles steht still.")]
    [SerializeField] private bool animate = true;

    [Header("Steuerung")]
    [SerializeField] private KeyCode playKey    = KeyCode.Return;
    [SerializeField] private KeyCode playKeyAlt = KeyCode.E;
    [SerializeField] private KeyCode endlessKey = KeyCode.Tab;
    [SerializeField] private KeyCode closeKey   = KeyCode.Escape;

    [Header("Sound")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [Tooltip("Beim Umschalten Story/Endless. Leer = der Ton vom Weitergehen.")]
    [SerializeField] private AudioClip toggleClip;
    [Tooltip("Leer = MenuClick aus dem AudioController.")]
    [SerializeField] private AudioClip startClip;
    [Tooltip("Wenn das Level zu ist oder noch keine Szene hat. Leer = PlayerHit aus dem AudioController.")]
    [SerializeField] private AudioClip denyClip;
    [Tooltip("Leer lassen - wird beim Start automatisch geholt oder angelegt.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ==================================================================
    //  Masse (Seitenpixel, oben links)
    // ==================================================================

    private const int RibbonY = 6;

    // linke Karte: Panorama + Reiseroute
    private const int LeftX = 10, LeftY = 34, LeftW = 264, LeftH = 206;
    private const int FrameX = 16, FrameY = 40, FrameW = 252, FrameH = 126;
    private const int WinX = FrameX + 3, WinY = FrameY + 3, WinW = FrameW - 6, WinH = FrameH - 6;

    // Reiseroute
    private const int Node = 30, NodeInset = 3, NodeStep = 52, NodeY = 202, NodeWave = 8;
    private const int PerPage = 5;
    private const int CharTarget = 40;           // Figur 1:1 (32er), der Ritter mit Schwert auch

    // rechte Karte: Steckbrief
    private const int InfoX = 280, InfoY = 34, InfoW = 190, InfoH = 206;
    private const int TextX = InfoX + 8, TextW = InfoW - 16;
    private const int BossTile = 56, BossY = InfoY + 103;
    private const int SideX = TextX + BossTile + 6, SideW = TextW - BossTile - 6;
    private const int ModeY = InfoY + 182, ModeH = 18, ModeW = (TextW - 6) / 2;

    // Fusszeile
    private const int BarY = 246, BarH = 20;
    private const int BackX = 10, BackW = 80;
    private const int PlayW = 110, PlayX = InfoX + InfoW - PlayW;

    private static readonly RectInt Content = new RectInt(8, 4, 464, 262);

    // Zeiten
    private const float SlideTime = 0.24f;
    private const float PanPeriod = 9f;
    private const float LaunchDelay = 0.32f;

    // ==================================================================
    //  Zustand
    // ==================================================================

    private sealed class Btn
    {
        public Rect Area;
        public Image Bg, Icon;
        public TextMeshProUGUI Label;
        public GameHudSkin.ButtonLook Normal, Hover;
        public bool Disabled, Active, Visible = true;
    }

    private sealed class Station
    {
        public int Level = -1;
        public Rect Area;
        public GameObject Root;
        public Image Glow, Back, Preview, Frame, Lock, Question, Chip;
        public TextMeshProUGUI Number;
        public Vector2 Home;             // Position ohne Aufprall-Versatz
    }

    private sealed class Mote
    {
        public Image Img;
        public Vector2 Seed;             // 0..1, 0..1
        public float Phase, Speed;
    }

    /// <summary>Plan-Auszug fuer den Steckbrief, einmal je Plan gerechnet.</summary>
    private sealed class PlanInfo
    {
        public EnemyId Boss = EnemyId.None;
        public int Kinds;
        public float Duration;
    }

    private TMP_FontAsset textFont, pixelFont;
    private GameObject root;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private Vector2 pageBase;

    // Panorama
    private RectTransform window;
    private Image panoEmpty, panoOld, panoNew, sunbeam, veil, bigLock, tape, soonChip;
    private TextMeshProUGUI soonText;
    private readonly List<Mote> motes = new List<Mote>();
    private int slideDir;
    private float slideAt = -10f;
    private Ambience moteKind = Ambience.None;

    // Reiseroute
    private readonly List<Station> stations = new List<Station>();
    private readonly List<Image> dots = new List<Image>();
    private readonly List<Vector2> dotPos = new List<Vector2>();
    private readonly List<int> dotSegment = new List<int>();
    private Btn prevBtn, nextBtn;
    private Image walker, walkerShadow;
    private Puppet puppet;
    private readonly List<Image> puffs = new List<Image>();
    private readonly List<Image> sparks = new List<Image>();

    // Steckbrief
    private TextMeshProUGUI mapLabel, nameText, nameShadow, descText, bossLabel, bossName, bossNone,
                            timeText, kindsText, statusText, recordLabel, recordValue, recordShadow;
    private Image statusChip, bossTile, bossIcon, bossQuestion, timeIcon, kindsIcon;
    private Image recordIcon;
    private Btn storyBtn, endlessBtn, playBtn, backBtn;

    private readonly Dictionary<string, PlanInfo> planCache = new Dictionary<string, PlanInfo>();

    private int selected;
    private int pageIndex;
    private bool endlessChosen;
    private int hoverStation = -1;
    private Btn hoverBtn, pressedBtn;
    private int openedOnFrame = -1;
    private bool built, launching;

    // Zeitmarken (unscaledTime)
    private float openedAt = -10f, hopAt = -10f, landAt = -10f, launchAt = -10f;
    private Vector2 hopFrom, hopTo;
    private float hopTime;

    LevelEntry Current => (selected >= 0 && selected < levels.Count) ? levels[selected] : null;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    void Awake()
    {
        EnsureSfxSource();
        Build();
    }

    void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;

    void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

    void EnsureSfxSource()
    {
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    void OnDestroy()
    {
        // Szenenwechsel mit offener Auswahl: die Sperre wieder abmelden, sonst
        // reagiert der Hub beim naechsten Mal auf gar nichts mehr.
        if (!IsOpen) return;
        IsOpen = false;
        HubUI.PopModal();
    }

    void OnLanguageChanged()
    {
        if (!built) return;
        bool wasOpen = root != null && root.activeSelf;
        if (root != null) Destroy(root);
        root = null;
        built = false;
        Build();
        if (wasOpen)
        {
            root.SetActive(true);
            ShowPage(selected / PerPage);
            Refresh();
            LayoutPage(true);
        }
    }

    void Build()
    {
        if (built) return;
        built = true;

        textFont = PixelUI.FindTextFont() ?? PixelUI.FindPixelFont();
        pixelFont = PixelUI.FindPixelFont() ?? textFont;

        // Eigene Leinwand statt OptionsKit.CreatePage: die legt ein
        // EventSystem an, und das braucht hier niemand - Treffer rechnen wir selbst.
        root = new GameObject("LevelSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) root.layer = uiLayer;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 135;       // ueber Textbox (100), Konsole (120) und Shop (130)

        scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        OptionsKit.Stretch("Dim", root.transform, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, dim));

        page = OptionsKit.Rect("Page", root.transform, 0, 0, OptionsKit.RefW, OptionsKit.RefH);

        OptionsKit.Ribbon(page, OptionsKit.RefW / 2f, RibbonY,
                          Loc.Get("ui.levelselect.title", "LEVELAUSWAHL"), pixelFont, textFont);

        OptionsKit.Img("LeftCard", page, LeftX, LeftY, LeftW, LeftH, GameHudSkin.Card, true);
        BuildPanorama();
        BuildRoute();
        BuildInfo();
        BuildBar();

        lastScreen = Vector2Int.zero;
        root.SetActive(false);
    }

    // ---------- Panorama ----------

    void BuildPanorama()
    {
        window = OptionsKit.Rect("Window", page, WinX, WinY, WinW, WinH);
        window.gameObject.AddComponent<RectMask2D>();

        panoEmpty = OptionsKit.Img("Empty", window, 0, 0, WinW, WinH, GameHudSkin.SlotEmpty(WinW, WinH));
        // Bleiben immer eingeschaltet und werden nur durchsichtig - siehe
        // Charakterauswahl: eingeschaltete Bilder blieben sonst unsichtbar.
        panoOld = OptionsKit.Img("PanoOld", window, 0, 0, WinW, WinH, null, Color.clear);
        panoNew = OptionsKit.Img("PanoNew", window, 0, 0, WinW, WinH, null, Color.clear);

        sunbeam = OptionsKit.Img("Sunbeam", window, 0, 0, 120, 110, GameHudSkin.Sunbeam(120, 110), Color.clear);

        motes.Clear();
        var rng = new System.Random(11);
        for (int i = 0; i < 26; i++)
        {
            Image img = OptionsKit.Img("Mote", window, 0, 0, 1, 1, GameHudSkin.White, Color.clear);
            motes.Add(new Mote
            {
                Img = img,
                Seed = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                Phase = (float)rng.NextDouble() * 6.28f,
                Speed = 0.6f + (float)rng.NextDouble() * 0.8f,
            });
        }

        OptionsKit.Img("Vignette", window, 0, 0, WinW, WinH, GameHudSkin.Vignette(WinW, WinH));

        veil = OptionsKit.Img("Veil", window, 0, 0, WinW, WinH, GameHudSkin.White, Color.clear);
        bigLock = OptionsKit.Img("Lock", window, (WinW - 28) / 2, (WinH - 36) / 2, 28, 36, GameHudSkin.Lock, Color.clear);

        // Absperrband quer durchs Bild, darauf ein Schild "BALD"
        tape = OptionsKit.Img("Tape", window, -4, WinH / 2 - 5, WinW + 8, 10, GameHudSkin.CautionTape, Color.clear);
        tape.type = Image.Type.Tiled;
        soonChip = OptionsKit.Img("SoonChip", window, 0, WinH / 2 - 9, 60, 18, GameHudSkin.Sign, Color.clear, true);
        soonText = OptionsKit.Label("SoonText", window, 0, WinH / 2 - 9, 60, 17, "", pixelFont,
                                    OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);

        // Rahmen zuletzt - er deckt die Kanten des Fensters
        OptionsKit.Img("Frame", page, FrameX, FrameY, FrameW, FrameH, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), true);
    }

    // ---------- Reiseroute ----------

    void BuildRoute()
    {
        // Punkte zuerst, damit die Stationen darueber liegen
        dots.Clear();
        for (int i = 0; i < 64; i++)
            dots.Add(OptionsKit.Img("Dot", page, 0, 0, 2, 2, GameHudSkin.TrailDot(true), Color.clear));

        stations.Clear();
        for (int i = 0; i < PerPage; i++) stations.Add(BuildStation(i));

        walkerShadow = OptionsKit.Img("WalkerShadow", page, 0, 0, 18, 5, GameHudSkin.FootShadow(18, 5), Color.clear);
        walker = OptionsKit.Img("Walker", page, 0, 0, 32, 32, null, Color.clear);

        puffs.Clear();
        for (int i = 0; i < 6; i++)
            puffs.Add(OptionsKit.Img("Puff", page, 0, 0, 2, 2, GameHudSkin.White, Color.clear));

        sparks.Clear();
        for (int i = 0; i < 12; i++)
        {
            bool star = i % 3 == 0;
            sparks.Add(OptionsKit.Img("Spark", page, 0, 0, star ? 9 : 5, star ? 9 : 5,
                                      star ? GameHudSkin.Star : GameHudSkin.Sparkle, Color.clear));
        }

        prevBtn = MakeButton("Prev", LeftX + 4, NodeY + 7, 18, 16, "", GameHudSkin.ArrowLeft, false);
        nextBtn = MakeButton("Next", LeftX + LeftW - 22, NodeY + 7, 18, 16, "", GameHudSkin.Arrow, false);

        puppet = Puppet.Create(root.transform, "WalkerPuppet");
    }

    Station BuildStation(int i)
    {
        var s = new Station();
        RectTransform rt = OptionsKit.Rect("Station " + (i + 1), page, 0, 0, Node, Node);
        s.Root = rt.gameObject;

        s.Glow = OptionsKit.Img("Glow", rt, -2, -2, Node + 4, Node + 4, GameHudSkin.Ring, Color.clear, true);

        int win = Node - 2 * NodeInset;
        s.Back = OptionsKit.Img("Back", rt, NodeInset, NodeInset, win, win, GameHudSkin.SlotEmpty(win, win));
        RectTransform w = OptionsKit.Rect("Window", rt, NodeInset, NodeInset, win, win);
        w.gameObject.AddComponent<RectMask2D>();
        s.Preview = OptionsKit.Img("Preview", w, 0, 0, 50, 50, null, Color.clear);
        RectTransform prt = s.Preview.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;

        s.Frame = OptionsKit.Img("Frame", rt, 0, 0, Node, Node, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), true);
        s.Lock = OptionsKit.Img("Lock", rt, (Node - 7) / 2, (Node - 9) / 2, 7, 9, GameHudSkin.Lock, Color.clear);
        s.Question = OptionsKit.Img("Question", rt, (Node - 7) / 2, (Node - 9) / 2, 7, 9, GameHudSkin.Question, Color.clear);

        // Nummernschild haengt unten mittig - oben steht die Figur
        s.Chip = OptionsKit.Img("Chip", rt, (Node - 11) / 2, Node - 5, 11, 12, GameHudSkin.LevelChip, true);
        s.Number = OptionsKit.Label("Number", rt, (Node - 11) / 2, Node - 6, 11, 13, "", pixelFont, OptionsKit.SizeText,
                                    GameHudSkin.Parchment, TextAlignmentOptions.Center);
        return s;
    }

    // ---------- Steckbrief ----------

    void BuildInfo()
    {
        OptionsKit.Img("InfoCard", page, InfoX, InfoY, InfoW, InfoH, GameHudSkin.Card, true);

        mapLabel = OptionsKit.Label("MapLabel", page, TextX, InfoY + 6, TextW, 13, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);

        statusChip = OptionsKit.Img("StatusChip", page, 0, InfoY + 5, 40, 14, GameHudSkin.LevelChip, true);
        statusText = OptionsKit.Label("StatusText", page, 0, InfoY + 5, 40, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Mint, TextAlignmentOptions.Center);

        nameShadow = OptionsKit.Label("NameShadow", page, TextX, InfoY + 19, TextW, 22, "", pixelFont,
                                      OptionsKit.SizeTitle, GameHudSkin.Ink, TextAlignmentOptions.Left);
        nameText = OptionsKit.Label("Name", page, TextX, InfoY + 18, TextW, 22, "", pixelFont,
                                    OptionsKit.SizeTitle, GameHudSkin.Cream, TextAlignmentOptions.Left);

        OptionsKit.Img("Rule", page, TextX, InfoY + 42, TextW, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Stone, 0.7f));

        descText = OptionsKit.Label("Desc", page, TextX, InfoY + 46, TextW, 40, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.TopLeft);
        descText.textWrappingMode = TextWrappingModes.Normal;

        // Eingelassene Flaeche fuer Boss, Bestwert und Zahlen
        OptionsKit.Img("BossWell", page, TextX - 3, BossY - 16, TextW + 6, BossTile + 34, GameHudSkin.Well, true);

        bossLabel = OptionsKit.Label("BossLabel", page, TextX + 1, BossY - 14, TextW, 13,
                                     Loc.Get("ui.levelselect.boss", "BOSS"), textFont,
                                     OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);

        bossTile = OptionsKit.Img("BossTile", page, TextX, BossY, BossTile, BossTile,
                                  GameHudSkin.Tile(BossTile, GameHudSkin.TileKind.Evo));
        bossIcon = OptionsKit.Img("BossIcon", page, TextX, BossY, 1, 1, null, Color.clear);
        bossQuestion = OptionsKit.Img("BossQuestion", page, TextX + (BossTile - 14) / 2, BossY + (BossTile - 18) / 2 - 1,
                                      14, 18, GameHudSkin.Question, Color.clear);

        bossName = OptionsKit.Label("BossName", page, SideX, BossY - 1, SideW, 13, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Left);
        bossNone = OptionsKit.Label("BossNone", page, SideX, BossY + 12, SideW, 30, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.TopLeft);
        bossNone.textWrappingMode = TextWrappingModes.Normal;

        // Bestwert: haengt am Schalter Story / Endless
        OptionsKit.Img("RecordRule", page, SideX, BossY + 16, SideW, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Stone, 0.5f));
        recordLabel = OptionsKit.Label("RecordLabel", page, SideX, BossY + 19, SideW, 13, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        recordIcon = OptionsKit.Img("RecordIcon", page, SideX, BossY + 37, 9, 9, GameHudSkin.Star);
        recordShadow = OptionsKit.Label("RecordShadow", page, SideX + 13, BossY + 32, SideW - 13, 22, "", pixelFont,
                                        OptionsKit.SizeTitle, GameHudSkin.Ink, TextAlignmentOptions.Left);
        recordValue = OptionsKit.Label("RecordValue", page, SideX + 13, BossY + 31, SideW - 13, 22, "", pixelFont,
                                       OptionsKit.SizeTitle, GameHudSkin.Gold, TextAlignmentOptions.Left);

        int statY = BossY + BossTile + 4;
        timeIcon = OptionsKit.Img("TimeIcon", page, TextX, statY + 2, 9, 9, GameHudSkin.Clock);
        timeText = OptionsKit.Label("Time", page, TextX + 12, statY, 70, 13, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Left);
        kindsIcon = OptionsKit.Img("KindsIcon", page, TextX + 88, statY + 2, 9, 9, GameHudSkin.Skull);
        kindsText = OptionsKit.Label("Kinds", page, TextX + 100, statY, TextW - 100, 13, "", textFont,
                                     OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Left);

        storyBtn = MakeButton("Story", TextX, ModeY, ModeW, ModeH, Loc.Get("ui.levelselect.story", "STORY"), null, false);
        endlessBtn = MakeButton("Endless", TextX + ModeW + 6, ModeY, ModeW, ModeH,
                                Loc.Get("ui.levelselect.endless", "ENDLESS"), null, false);
    }

    // ---------- Fusszeile ----------

    void BuildBar()
    {
        backBtn = MakeButton("Back", BackX, BarY, BackW, BarH, Loc.Get("ui.levelselect.back", "ZURÜCK"), null, false);
        playBtn = MakeButton("Play", PlayX, BarY, PlayW, BarH, "", null, true);
    }

    Btn MakeButton(string name, float x, float y, float w, float h, string text, Sprite icon, bool primary)
    {
        var b = new Btn
        {
            Area = new Rect(x, y, w, h),
            Normal = primary ? GameHudSkin.ButtonLook.Gold : GameHudSkin.ButtonLook.Wood,
            Hover = primary ? GameHudSkin.ButtonLook.GoldHover : GameHudSkin.ButtonLook.Hover,
        };
        RectTransform rt = OptionsKit.Rect("Btn_" + name, page, x, y, w, h);
        b.Bg = rt.gameObject.AddComponent<Image>();
        b.Bg.type = Image.Type.Sliced;
        b.Bg.raycastTarget = false;
        b.Icon = OptionsKit.Img("Icon", rt, 0, 0, 1, 1, null);
        b.Label = OptionsKit.Label("Label", rt, 0, 0, w, h - 1, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.Cream, TextAlignmentOptions.Left);
        SetButton(b, text, icon);
        return b;
    }

    /// <summary>Text und Symbol setzen - beides zusammen mittig, Symbol links.</summary>
    void SetButton(Btn b, string text, Sprite icon)
    {
        TMP_FontAsset font = OptionsKit.PickFont(text, pixelFont, textFont);
        b.Label.font = font;
        b.Label.text = text;

        float iw = icon != null ? icon.rect.width : 0f, ih = icon != null ? icon.rect.height : 0f;
        float tw = string.IsNullOrEmpty(text) ? 0f : Mathf.Ceil(OptionsKit.Measure(b.Label, text));
        float gap = icon != null && tw > 0f ? 3f : 0f;
        float left = Mathf.Round((b.Area.width - (iw + gap + tw)) / 2f);

        b.Icon.enabled = icon != null;
        if (icon != null)
        {
            b.Icon.sprite = icon;
            // Das Band unten ist 2 px dunkel - die optische Mitte liegt 1 px hoeher.
            OptionsKit.Move(b.Icon.rectTransform, left, Mathf.Round((b.Area.height - 1f - ih) / 2f), iw, ih);
        }
        OptionsKit.Move(b.Label.rectTransform, left + iw + gap, 0, tw + 2f, b.Area.height - 1f);
    }

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public void Open()
    {
        if (IsOpen) return;

        Build();

        IsOpen = true;
        launching = false;
        openedOnFrame = Time.frameCount;
        openedAt = Time.unscaledTime;
        root.SetActive(true);

        // Auf dem ersten offenen Level starten, damit man nie vor einer
        // gesperrten Station steht und sich fragt, warum nichts geht.
        selected = FirstUnlocked();

        // Endless steht vorn, wenn im Hauptmenue "Endless" gedrueckt wurde oder
        // wenn die Story dieses Levels noch zu ist und nur Endless offen steht.
        endlessChosen = EndlessUnlocked(Current) && (GameSession.IsEndless || !StoryUnlocked(Current));

        hoverStation = -1;
        hoverBtn = pressedBtn = null;
        hopAt = landAt = launchAt = slideAt = -10f;
        ShowPage(selected / PerPage);
        ShowPanorama(0);
        Refresh();
        LayoutPage(true);

        HubUI.PushModal();
        if (HubUI.Instance != null) HubUI.Instance.SetPlayerFrozen(true);

        PlaySfx(openClip, true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root.SetActive(false);

        HubUI.PopModal();
        if (HubUI.Instance != null) HubUI.Instance.SetPlayerFrozen(false);

        PlaySfx(closeClip, true);
    }

    int FirstUnlocked()
    {
        for (int i = 0; i < levels.Count; i++)
            if (StoryUnlocked(levels[i]) || EndlessUnlocked(levels[i])) return i;
        return 0;
    }

    // ==================================================================
    //  Eingabe
    // ==================================================================

    void Update()
    {
        if (!IsOpen) return;

        LayoutPage(false);

        // Levelstart laeuft schon - waehrend Absprung und Blende nichts mehr annehmen
        if (launching || SceneFader.IsFading) return;

        // Das [E], mit dem die Auswahl aufgeht, darf nicht gleich ein Level starten
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0)) MouseDown();
        if (Input.GetMouseButtonUp(0)) MouseUp();

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(+1);

        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.01f) Move(-1);
        else if (wheel < -0.01f) Move(+1);

        if (Input.GetKeyDown(endlessKey)) ToggleEndless();
        if (Input.GetKeyDown(playKey) || Input.GetKeyDown(playKeyAlt) || Input.GetKeyDown(KeyCode.KeypadEnter)) Play();

        // 1..9 springen direkt zur Station
        for (int i = 0; i < 9 && i < levels.Count; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            Select(i);
            break;
        }
    }

    void MouseDown()
    {
        if (hoverBtn != null) { pressedBtn = hoverBtn; Refresh(); return; }

        // Stationen waehlen nur aus - gestartet wird ueber SPIELEN.
        if (hoverStation >= 0) Select(stations[hoverStation].Level);
    }

    void MouseUp()
    {
        Btn b = pressedBtn;
        pressedBtn = null;
        if (b != null && b == hoverBtn && b.Visible && !b.Disabled)
        {
            if (b == backBtn) { Close(); return; }
            if (b == playBtn) Play();
            else if (b == storyBtn) SetEndless(false);
            else if (b == endlessBtn) SetEndless(true);
            else if (b == prevBtn) Select(Mathf.Max(0, (pageIndex - 1) * PerPage + PerPage - 1));
            else if (b == nextBtn) Select(Mathf.Min(levels.Count - 1, (pageIndex + 1) * PerPage));
        }
        Refresh();
    }

    void Move(int delta)
    {
        if (levels.Count == 0) return;
        Select(Mathf.Clamp(selected + delta, 0, levels.Count - 1));
    }

    void Select(int index)
    {
        if (index < 0 || index >= levels.Count || index == selected) return;

        int from = selected;
        Vector2 fromFeet = FeetOf(from);
        selected = index;

        bool flip = selected / PerPage != pageIndex;
        if (flip) ShowPage(selected / PerPage);

        // Die Figur huepft von Station zu Station - beim Blaettern taucht sie
        // am Rand der neuen Seite auf.
        hopFrom = flip ? FeetOf(selected) + new Vector2(index > from ? -40f : 40f, 0f) : fromFeet;
        hopTo = FeetOf(selected);
        hopTime = Mathf.Clamp(0.2f + Mathf.Abs(hopTo.x - hopFrom.x) / 400f, 0.22f, 0.5f);
        hopAt = Time.unscaledTime;
        landAt = hopAt + hopTime;

        // Ein Level, das nur noch Endless hat, setzt den Schalter selbst - und
        // eines ohne Endless nimmt ihn wieder weg.
        if (!StoryUnlocked(Current) && EndlessUnlocked(Current)) endlessChosen = true;
        if (!EndlessUnlocked(Current)) endlessChosen = false;

        ShowPanorama(index > from ? 1 : -1);
        PlaySfx(moveClip, false);
        Refresh();
    }

    void ToggleEndless() => SetEndless(!endlessChosen);

    void SetEndless(bool on)
    {
        LevelEntry e = Current;
        if (e == null || on == endlessChosen) return;

        if (on && !EndlessUnlocked(e)) { PlayDenySound(); return; }
        // Ohne offene Story bleibt Endless die einzige Wahl.
        if (!on && !StoryUnlocked(e)) { PlayDenySound(); return; }

        endlessChosen = on;
        PlaySfx(toggleClip != null ? toggleClip : moveClip, false);
        Refresh();
    }

    // ---------- Maus ----------

    /// <summary>Mausposition in Seitenpixeln (oben links) - dieselben Zahlen wie die Masse oben.</summary>
    bool MousePage(out Vector2 p)
    {
        p = default;
        if (scaler == null) return false;
        float s = Mathf.Max(1f, scaler.scaleFactor);
        Vector3 m = Input.mousePosition;
        p = new Vector2(m.x / s - pageBase.x, (Screen.height - m.y) / s + pageBase.y);
        return true;
    }

    void UpdateHover()
    {
        int wasStation = hoverStation;
        Btn wasBtn = hoverBtn;
        hoverStation = -1;
        hoverBtn = null;

        if (MousePage(out Vector2 m))
        {
            foreach (Btn b in new[] { backBtn, playBtn, storyBtn, endlessBtn, prevBtn, nextBtn })
            {
                if (b != null && b.Visible && b.Area.Contains(m)) { hoverBtn = b; break; }
            }

            if (hoverBtn == null)
            {
                for (int i = 0; i < stations.Count; i++)
                {
                    Station s = stations[i];
                    if (s.Level < 0) continue;
                    // Etwas grosszuegiger als die Station - samt Nummernschild
                    Rect hit = new Rect(s.Area.x - 4, s.Area.y - 4, s.Area.width + 8, s.Area.height + 8);
                    if (!hit.Contains(m)) continue;
                    hoverStation = i;
                    break;
                }
            }
        }

        if (hoverStation != wasStation || hoverBtn != wasBtn) Refresh();
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    int PageCount => Mathf.Max(1, (levels.Count + PerPage - 1) / PerPage);

    /// <summary>So viele Stationen stehen auf dieser Seite.</summary>
    int OnPage => Mathf.Clamp(levels.Count - pageIndex * PerPage, 0, PerPage);

    void ShowPage(int index)
    {
        pageIndex = Mathf.Clamp(index, 0, PageCount - 1);

        int n = OnPage;
        for (int i = 0; i < stations.Count; i++)
        {
            Station s = stations[i];
            s.Level = i < n ? pageIndex * PerPage + i : -1;
            s.Root.SetActive(s.Level >= 0);
            if (s.Level < 0) continue;

            Vector2 p = StationPos(i, n);
            s.Home = p;
            s.Area = new Rect(p.x, p.y, Node, Node);
            OptionsKit.Move((RectTransform)s.Root.transform, p.x, p.y, Node, Node);
        }

        BuildDots(n);
    }

    /// <summary>Oben links von Station i auf einer Seite mit n Stationen. Jede zweite liegt hoeher - ein Pfad statt einer Linie.</summary>
    Vector2 StationPos(int i, int n)
    {
        int span = Node + (Mathf.Max(1, n) - 1) * NodeStep;
        float x = LeftX + Mathf.Round((LeftW - span) / 2f) + i * NodeStep;
        float y = NodeY - (i % 2 == 1 ? NodeWave : 0);
        return new Vector2(x, y);
    }

    /// <summary>Wo die Figur auf Station <paramref name="level"/> mit den Fuessen steht.</summary>
    Vector2 FeetOf(int level)
    {
        int i = level - pageIndex * PerPage;
        if (i < 0 || i >= PerPage) i = Mathf.Clamp(i, 0, PerPage - 1);
        Vector2 p = StationPos(i, OnPage);
        return new Vector2(p.x + Node / 2f, p.y + 1f);
    }

    /// <summary>
    /// Die Punkte zwischen den Stationen: ein durchhaengender Bogen, alle 4 px
    /// ein Punkt. Wird nur beim Blaettern neu gesetzt.
    /// </summary>
    void BuildDots(int n)
    {
        dotPos.Clear();
        dotSegment.Clear();
        for (int i = 0; i + 1 < n; i++)
        {
            Vector2 a = StationPos(i, n) + new Vector2(Node / 2f, Node / 2f);
            Vector2 b = StationPos(i + 1, n) + new Vector2(Node / 2f, Node / 2f);
            Vector2 ctrl = (a + b) / 2f + new Vector2(0f, 10f);

            // Laenge grob messen, dann gleichmaessig verteilen
            float len = 0f;
            Vector2 last = a;
            for (int k = 1; k <= 20; k++)
            {
                Vector2 q = Bezier(a, ctrl, b, k / 20f);
                len += Vector2.Distance(last, q);
                last = q;
            }
            int count = Mathf.Max(2, Mathf.RoundToInt(len / 4f));
            for (int k = 1; k < count; k++)
            {
                Vector2 q = Bezier(a, ctrl, b, k / (float)count);
                // Nicht unter die Stationen malen
                if (Vector2.Distance(q, a) < Node * 0.6f || Vector2.Distance(q, b) < Node * 0.6f) continue;
                dotPos.Add(new Vector2(Mathf.Round(q.x - 1f), Mathf.Round(q.y - 1f)));
                dotSegment.Add(i);
            }
        }

        for (int i = 0; i < dots.Count; i++)
        {
            bool on = i < dotPos.Count;
            if (on) OptionsKit.Move(dots[i].rectTransform, dotPos[i].x, dotPos[i].y, 2, 2);
            else dots[i].color = Color.clear;
        }
    }

    static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    void Refresh()
    {
        if (page == null) return;

        // Stationen
        for (int i = 0; i < stations.Count; i++)
        {
            Station s = stations[i];
            if (s.Level < 0) continue;
            LevelEntry e = levels[s.Level];

            bool open = IsOpenLevel(e);
            bool linked = IsLinked(e);
            bool isSel = s.Level == selected;
            bool isHover = i == hoverStation;

            GameHudSkin.SlotLook look = isSel ? GameHudSkin.SlotLook.Gold
                : !open ? GameHudSkin.SlotLook.Stone
                : isHover ? GameHudSkin.SlotLook.Hover
                : GameHudSkin.SlotLook.Wood;
            s.Frame.sprite = GameHudSkin.SlotFrame(look);

            Sprite pic = e.preview != null ? e.preview : e.previewWide;
            if (pic != null)
            {
                s.Preview.sprite = pic;
                s.Preview.rectTransform.sizeDelta = new Vector2(pic.rect.width, pic.rect.height);
                s.Preview.color = open ? (linked ? Color.white : new Color(0.7f, 0.66f, 0.68f, 1f))
                                       : new Color(0.28f, 0.24f, 0.27f, 1f);
            }
            else s.Preview.color = Color.clear;

            s.Lock.color = !open ? Color.white : Color.clear;
            s.Question.color = open && (!linked || pic == null) ? OptionsKit.WithAlpha(GameHudSkin.Cream, 0.8f) : Color.clear;

            s.Number.text = (s.Level + 1).ToString();
            s.Number.color = isSel ? GameHudSkin.Gold : open ? GameHudSkin.Parchment : GameHudSkin.Stone;
        }

        // Punkte: golden, solange der Weg zu einer offenen Station fuehrt
        for (int i = 0; i < dotPos.Count && i < dots.Count; i++)
        {
            int next = pageIndex * PerPage + dotSegment[i] + 1;
            bool lit = next < levels.Count && IsOpenLevel(levels[next]) && IsLinked(levels[next]);
            dots[i].sprite = GameHudSkin.TrailDot(lit);
        }

        int pages = PageCount;
        prevBtn.Visible = nextBtn.Visible = pages > 1;
        prevBtn.Disabled = pageIndex == 0;
        nextBtn.Disabled = pageIndex >= pages - 1;

        RefreshInfo();
        RefreshPanoramaOverlay();

        foreach (Btn b in new[] { backBtn, playBtn, storyBtn, endlessBtn, prevBtn, nextBtn }) PaintButton(b);
    }

    void RefreshInfo()
    {
        LevelEntry e = Current;
        bool any = e != null;
        bool open = any && IsOpenLevel(e);
        bool linked = any && IsLinked(e);

        mapLabel.text = any ? string.Format(Loc.Get("ui.levelselect.map", "KARTE {0}"), selected + 1) : "";

        string name = !any ? "" : open ? LevelName(selected).ToUpperInvariant() : "???";
        if (open && string.IsNullOrWhiteSpace(LevelName(selected))) name = "???";
        FitTitle(nameText, nameShadow, name, TextW);

        descText.text = !any ? ""
                      : !open ? Loc.Get("ui.levelselect.lockeddesc", "Noch verschlossen.")
                      : !linked ? Loc.Get("ui.levelselect.soondesc", "Hier wird noch gebacken. Bald geht es weiter!")
                      : LevelDesc(selected);

        // Status oben rechts
        string status; Color statusCol;
        if (!any) { status = ""; statusCol = Color.clear; }
        else if (!open) { status = Loc.Get("ui.levelselect.locked", "GESPERRT"); statusCol = GameHudSkin.StoneLight; }
        else if (!linked) { status = Loc.Get("ui.levelselect.soon", "BALD"); statusCol = GameHudSkin.Gold; }
        else { status = Loc.Get("ui.levelselect.open", "OFFEN"); statusCol = GameHudSkin.Mint; }
        statusChip.enabled = statusText.enabled = status.Length > 0;
        if (status.Length > 0)
        {
            statusText.text = status;
            statusText.color = statusCol;
            float tw = Mathf.Ceil(OptionsKit.Measure(statusText, status));
            float w = tw + 12f;
            if (((int)w & 1) == 1) w += 1f;
            float x = TextX + TextW - w;
            OptionsKit.Move(statusChip.rectTransform, x, InfoY + 5, w, 14);
            OptionsKit.Move(statusText.rectTransform, x, InfoY + 5, w, 13);
        }

        // Boss & Co. aus dem Wellenplan
        PlanInfo plan = open ? PlanFor(e) : null;
        EnemyId boss = !open ? EnemyId.None : e.boss != EnemyId.None ? e.boss : plan != null ? plan.Boss : EnemyId.None;
        bool hasBoss = boss != EnemyId.None;
        Sprite bossSprite = hasBoss ? BossPortrait(boss) : null;

        bossTile.enabled = true;
        bossQuestion.color = bossSprite == null ? OptionsKit.WithAlpha(GameHudSkin.Cream, 0.55f) : Color.clear;
        if (bossSprite != null)
        {
            bossIcon.sprite = bossSprite;
            float w = bossSprite.rect.width, h = bossSprite.rect.height;
            float k = w <= BossTile - 4 && h <= BossTile - 4 ? 1f : 0.5f;
            w = Mathf.Round(w * k); h = Mathf.Round(h * k);
            // Ein Pixel hoeher als mittig: unten liegt die Farbkante der Kachel
            OptionsKit.Move(bossIcon.rectTransform, TextX + Mathf.Floor((BossTile - w) / 2f),
                            BossY + Mathf.Floor((BossTile - 3 - h) / 2f), w, h);
            bossIcon.color = Color.white;
        }
        else bossIcon.color = Color.clear;

        // Bestwert der Karte im gewaehlten Modus
        bool rec = open && linked && e.mapId >= 0;

        string none = hasBoss ? "" : !open ? "???" : Loc.Get("ui.levelselect.noboss", "Noch unbekannt.");
        // Steht darunter ein Bestwert (Welt ohne Wellenplan), ist unter dem Namen
        // kein Platz - der Hinweis rueckt dann in die Namenszeile.
        bossName.text = hasBoss ? Bestiary.NameOf(boss).ToUpperInvariant() : rec ? none : "";
        bossName.color = hasBoss ? GameHudSkin.Cream : GameHudSkin.Stone;
        bossNone.text = rec ? "" : none;
        float best = !rec ? 0f : endlessChosen ? LevelRecords.EndlessBest(e.mapId) : LevelRecords.StoryBest(e.mapId);
        recordLabel.text = !rec ? "" : endlessChosen
            ? Loc.Get("ui.levelselect.recordendless", "LÄNGSTES ÜBERLEBEN")
            : Loc.Get("ui.levelselect.recordstory", "SCHNELLSTER SIEG");
        string value = !rec ? "" : best > 0f ? LevelRecords.Format(best) : "--:--";
        recordValue.text = recordShadow.text = value;
        recordValue.color = best > 0f ? (Color)GameHudSkin.Gold : GameHudSkin.Stone;
        recordIcon.enabled = rec;
        recordIcon.color = best > 0f ? Color.white : new Color(0.55f, 0.5f, 0.52f, 1f);

        // Dauer und Gegnerarten
        bool stats = plan != null && plan.Duration > 0f;
        timeIcon.enabled = timeText.enabled = kindsIcon.enabled = kindsText.enabled = stats;
        if (stats)
        {
            timeText.text = endlessChosen
                ? Loc.Get("ui.levelselect.nolimit", "OHNE ENDE")
                : string.Format(Loc.Get("ui.levelselect.minutes", "~{0} MIN"), Mathf.RoundToInt(plan.Duration / 60f));
            kindsText.text = string.Format(Loc.Get("ui.levelselect.kinds", "{0} GEGNERARTEN"), plan.Kinds);
        }

        // Story / Endless
        bool storyOk = any && StoryUnlocked(e), endlessOk = any && EndlessUnlocked(e);
        storyBtn.Disabled = !storyOk;
        endlessBtn.Disabled = !endlessOk;
        storyBtn.Active = storyOk && !endlessChosen;
        endlessBtn.Active = endlessOk && endlessChosen;

        // Spielen
        bool canPlay = open && linked;
        playBtn.Disabled = !canPlay;
        if (!any || canPlay) SetButton(playBtn, Loc.Get("ui.levelselect.play", "SPIELEN"), GameHudSkin.Arrow);
        else if (!open) SetButton(playBtn, Loc.Get("ui.levelselect.locked", "GESPERRT"), GameHudSkin.Lock);
        else SetButton(playBtn, Loc.Get("ui.levelselect.soon", "BALD"), null);
    }

    void RefreshPanoramaOverlay()
    {
        LevelEntry e = Current;
        bool open = e != null && IsOpenLevel(e);
        bool linked = e != null && IsLinked(e);

        veil.color = !open ? new Color(0.07f, 0.05f, 0.06f, 0.72f) : Color.clear;
        bigLock.color = !open ? Color.white : Color.clear;

        bool soon = open && !linked;
        tape.color = soon ? Color.white : Color.clear;
        soonChip.color = soonText.color = soon ? Color.white : Color.clear;
        if (soon)
        {
            string label = Loc.Get("ui.levelselect.soon", "BALD");
            soonText.font = OptionsKit.PickFont(label, pixelFont, textFont);
            soonText.text = label;
            soonText.color = GameHudSkin.Cream;
            float w = Mathf.Ceil(OptionsKit.Measure(soonText, label)) + 16f;
            if (((int)w & 1) == 1) w += 1f;
            float x = Mathf.Round((WinW - w) / 2f);
            OptionsKit.Move(soonChip.rectTransform, x, WinH / 2 - 9, w, 18);
            OptionsKit.Move(soonText.rectTransform, x, WinH / 2 - 9, w, 17);
        }
    }

    /// <summary>Neues Panorama: das alte gleitet in <paramref name="dir"/>-Richtung hinaus, das neue herein. 0 = sofort.</summary>
    void ShowPanorama(int dir)
    {
        LevelEntry e = Current;
        Sprite next = e == null ? null : e.previewWide != null ? e.previewWide : e.preview;

        panoOld.sprite = panoNew.sprite;
        panoOld.color = panoNew.color;
        OptionsKit.Move(panoOld.rectTransform, panoNew.rectTransform.anchoredPosition.x,
                        -panoNew.rectTransform.anchoredPosition.y,
                        panoNew.rectTransform.sizeDelta.x, panoNew.rectTransform.sizeDelta.y);

        panoNew.sprite = next;
        panoNew.color = next != null ? Color.white : Color.clear;

        slideDir = animate ? dir : 0;
        slideAt = Time.unscaledTime;
        if (slideDir == 0) panoOld.color = Color.clear;

        moteKind = e != null && IsOpenLevel(e) ? AmbienceFor(e) : Ambience.None;
        PlacePanorama(Time.unscaledTime);
    }

    /// <summary>Schwenk und Hereingleiten in ganzen Seitenpixeln.</summary>
    void PlacePanorama(float now)
    {
        float t = Mathf.Clamp01((now - slideAt) / SlideTime);
        float ease = 1f - (1f - t) * (1f - t) * (1f - t);
        float shift = slideDir == 0 ? 0f : Mathf.Round((1f - ease) * WinW) * slideDir;

        if (panoNew.sprite == null) sunbeam.color = Color.clear;
        PlaceImage(panoNew, now, shift);
        if (slideDir != 0 && t < 1f) PlaceImage(panoOld, now, shift - slideDir * WinW);
        else panoOld.color = Color.clear;
    }

    void PlaceImage(Image img, float now, float shiftX)
    {
        Sprite s = img.sprite;
        if (s == null) return;

        // Groesste ganze Vergroesserung, die das Fenster ganz fuellt
        float sw = s.rect.width, sh = s.rect.height;
        int k = Mathf.Max(1, Mathf.Max(Mathf.CeilToInt(WinW / sw), Mathf.CeilToInt(WinH / sh)));
        float w = sw * k, h = sh * k;

        // Langsamer Schwenk auf und ab (sanft an den Enden)
        float range = h - WinH;
        float wave = animate ? 0.5f - 0.5f * Mathf.Cos((now - openedAt) * Mathf.PI * 2f / PanPeriod) : 0.5f;
        float y = -Mathf.Round(range * wave);
        float x = Mathf.Round((WinW - w) / 2f) + shiftX;
        OptionsKit.Move(img.rectTransform, x, y, w, h);

        if (img == panoNew)
        {
            // Der Sonnenstrahl gehoert zur Kueche und schwenkt mit dem Bild.
            bool beam = moteKind == Ambience.Kitchen && animate;
            float pulse = 0.55f + 0.15f * Mathf.Sin(now * 0.9f);
            sunbeam.color = beam ? new Color(1f, 0.95f, 0.8f, pulse) : Color.clear;
            OptionsKit.Move(sunbeam.rectTransform, x + w - 132, y + 6, 120, 110);
        }
    }

    // ==================================================================
    //  Bewegtes
    // ==================================================================

    void LateUpdate()
    {
        if (!IsOpen || page == null) return;

        float now = Time.unscaledTime;

        PlacePanorama(now);
        UpdateMotes(now);
        UpdateStations(now);
        UpdateDots(now);
        UpdateWalker(now);
    }

    void UpdateMotes(float now)
    {
        for (int i = 0; i < motes.Count; i++)
        {
            Mote m = motes[i];
            Image img = m.Img;
            if (!animate || moteKind == Ambience.None) { img.color = Color.clear; continue; }

            float x, y, a;
            switch (moteKind)
            {
                case Ambience.Forest:
                    if (i % 3 == 0)
                    {
                        // Gluehwuermchen: schweben unten im Gras, blinken
                        img.sprite = GameHudSkin.Firefly;
                        x = m.Seed.x * WinW + Mathf.Sin(now * 0.7f * m.Speed + m.Phase) * 10f;
                        y = WinH * (0.55f + 0.4f * m.Seed.y) + Mathf.Sin(now * 1.1f * m.Speed + m.Phase * 2f) * 4f;
                        a = Mathf.Clamp01(Mathf.Sin(now * 2.2f * m.Speed + m.Phase) * 1.4f);
                        SetMote(img, x, y, 3, 3, new Color(1f, 1f, 1f, a));
                    }
                    else
                    {
                        // Blaetter: fallen, schaukeln, wandern leicht nach links
                        img.sprite = GameHudSkin.Leaf(i % 3 == 1 ? i % 2 : 2);
                        float fall = Mathf.Repeat(m.Seed.y + now * 0.07f * m.Speed, 1f);
                        x = Mathf.Repeat(m.Seed.x * WinW - now * 6f * m.Speed + Mathf.Sin(now * 2f + m.Phase) * 6f, WinW + 10f) - 5f;
                        y = fall * (WinH + 8f) - 6f;
                        a = Mathf.Clamp01(fall * 8f) * Mathf.Clamp01((1f - fall) * 8f);
                        SetMote(img, x, y, 3, 2, new Color(1f, 1f, 1f, a));
                    }
                    break;

                case Ambience.Kitchen:
                    // Mehlstaub: steigt langsam, glitzert im Strahl
                    img.sprite = GameHudSkin.White;
                    float rise = Mathf.Repeat(m.Seed.y - now * 0.035f * m.Speed, 1f);
                    x = WinW * (0.35f + 0.6f * m.Seed.x) + Mathf.Sin(now * 0.8f + m.Phase) * 5f;
                    y = rise * WinH;
                    a = (0.35f + 0.35f * Mathf.Sin(now * 2.4f + m.Phase)) * Mathf.Clamp01(rise * 6f) * Mathf.Clamp01((1f - rise) * 6f);
                    SetMote(img, x, y, 1, 1, OptionsKit.WithAlpha(GameHudSkin.Cream, Mathf.Max(0f, a)));
                    break;

                case Ambience.Volcano:
                    img.sprite = GameHudSkin.White;
                    if (i % 4 == 0)
                    {
                        // Ascheflocken: sinken langsam, treiben nach rechts
                        float sink = Mathf.Repeat(m.Seed.y + now * 0.03f * m.Speed, 1f);
                        x = Mathf.Repeat(m.Seed.x * WinW + now * 4f * m.Speed + Mathf.Sin(now * 1.3f + m.Phase) * 5f, WinW);
                        y = sink * WinH;
                        a = 0.55f * Mathf.Clamp01(sink * 6f) * Mathf.Clamp01((1f - sink) * 6f);
                        SetMote(img, x, y, 2, 1, new Color(0.62f, 0.57f, 0.57f, a));
                    }
                    else
                    {
                        // Glut: steigt aus der Lava, flackert, verglueht nach oben
                        float life = Mathf.Repeat(m.Seed.y - now * 0.11f * m.Speed, 1f);   // 1 unten .. 0 oben
                        x = m.Seed.x * WinW + Mathf.Sin(now * 1.7f * m.Speed + m.Phase) * 6f
                            + (1f - life) * 14f * (m.Seed.x - 0.5f);
                        y = (0.3f + 0.7f * life) * WinH;
                        float flick = 0.65f + 0.35f * Mathf.Sin(now * 9f + m.Phase * 3f);
                        a = Mathf.Clamp01(life * 1.8f) * Mathf.Clamp01((1f - life) * 10f) * flick;
                        Color ember = Color.Lerp(new Color(1f, 0.34f, 0.1f), new Color(1f, 0.85f, 0.38f), life);
                        SetMote(img, x, y, 1, 1, OptionsKit.WithAlpha(ember, a));
                    }
                    break;

                case Ambience.Snow:
                    img.sprite = GameHudSkin.White;
                    if (i % 5 == 0)
                    {
                        // Polarlicht: mintgruene Funken, die oben langsam pulsieren
                        x = Mathf.Repeat(m.Seed.x * WinW + now * 3f * m.Speed, WinW);
                        y = WinH * 0.08f + m.Seed.y * WinH * 0.22f + Mathf.Sin(now * 0.6f + x * 0.05f) * 4f;
                        a = 0.35f * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(now * 1.4f * m.Speed + m.Phase));
                        SetMote(img, x, y, 2, 1, new Color(0.55f, 0.95f, 0.78f, a));
                    }
                    else
                    {
                        // Schneeflocken: fallen, schaukeln, treiben leicht nach links
                        float drop = Mathf.Repeat(m.Seed.y + now * 0.06f * m.Speed, 1f);
                        x = Mathf.Repeat(m.Seed.x * WinW - now * 3f * m.Speed + Mathf.Sin(now * 1.6f + m.Phase) * 5f, WinW);
                        y = drop * WinH;
                        a = 0.85f * Mathf.Clamp01(drop * 8f) * Mathf.Clamp01((1f - drop) * 8f);
                        float size = i % 3 == 0 ? 2f : 1f;
                        SetMote(img, x, y, size, size, new Color(1f, 1f, 1f, a));
                    }
                    break;

                case Ambience.Ghost:
                    if (i % 7 == 0)
                    {
                        // Fledermaus: flattert schnell quer durchs Bild, Fluegel als 3x1 / 1x1
                        img.sprite = GameHudSkin.White;
                        float fly = Mathf.Repeat(m.Seed.x + now * 0.09f * m.Speed, 1.4f) - 0.2f;
                        x = fly * WinW;
                        y = WinH * (0.1f + 0.3f * m.Seed.y) + Mathf.Sin(now * 5f + m.Phase) * 3f;
                        bool wingsUp = Mathf.Repeat(now * 9f + m.Phase, 1f) < 0.5f;
                        SetMote(img, x, y, wingsUp ? 3 : 1, 1, new Color(0.1f, 0.06f, 0.14f, 0.9f));
                    }
                    else
                    {
                        // Irrlichter: schweben in Kurven ueber dem Boden, glimmen auf und verloeschen
                        img.sprite = GameHudSkin.Firefly;
                        x = m.Seed.x * WinW + Mathf.Sin(now * 0.45f * m.Speed + m.Phase) * 14f;
                        y = WinH * (0.45f + 0.5f * m.Seed.y) + Mathf.Sin(now * 0.8f * m.Speed + m.Phase * 2f) * 6f;
                        a = Mathf.Clamp01(Mathf.Sin(now * 1.3f * m.Speed + m.Phase) * 1.6f);
                        Color wisp = (i % 3) switch
                        {
                            0 => new Color(0.5f, 1f, 0.9f),
                            1 => new Color(0.8f, 0.6f, 1f),
                            _ => new Color(1f, 0.75f, 0.4f),
                        };
                        SetMote(img, x, y, 3, 3, OptionsKit.WithAlpha(wisp, a));
                    }
                    break;

                default:
                    img.sprite = GameHudSkin.White;
                    float up = Mathf.Repeat(m.Seed.y - now * 0.025f * m.Speed, 1f);
                    x = m.Seed.x * WinW + Mathf.Sin(now * 0.6f + m.Phase) * 4f;
                    y = up * WinH;
                    a = 0.3f * Mathf.Clamp01(up * 6f) * Mathf.Clamp01((1f - up) * 6f);
                    SetMote(img, x, y, 1, 1, OptionsKit.WithAlpha(GameHudSkin.Cream, a));
                    break;
            }
        }
    }

    static void SetMote(Image img, float x, float y, float w, float h, Color c)
    {
        OptionsKit.Move(img.rectTransform, x, y, w, h);
        img.color = c;
    }

    void UpdateStations(float now)
    {
        for (int i = 0; i < stations.Count; i++)
        {
            Station s = stations[i];
            if (s.Level < 0) continue;

            // Beim Oeffnen fallen die Stationen nacheinander auf den Pfad
            float t = animate ? Mathf.Clamp01((now - openedAt - 0.05f * i) / 0.22f) : 1f;
            float drop = Mathf.Round((1f - t) * (1f - t) * 10f);

            // Die gewaehlte Station federt, wenn die Figur landet
            float land = (now - landAt) / 0.18f;
            if (s.Level == selected && animate && land >= 0f && land < 1f) drop -= Mathf.Round(Mathf.Sin(land * Mathf.PI) * 2f);

            OptionsKit.Move((RectTransform)s.Root.transform, s.Home.x, s.Home.y - drop, Node, Node);

            float glow = 0f;
            if (s.Level == selected)
            {
                glow = animate ? 0.4f + 0.25f * Mathf.Sin(now * 4f) : 0.5f;
                if (now - launchAt < 0.5f) glow = 1f;
            }
            else if (i == hoverStation) glow = 0.2f;
            s.Glow.color = OptionsKit.WithAlpha(GameHudSkin.Gold, glow);
        }
    }

    void UpdateDots(float now)
    {
        // Der Pfad zeichnet sich beim Oeffnen von links nach rechts
        float reveal = animate ? Mathf.Clamp01((now - openedAt - 0.1f) / 0.45f) : 1f;
        int show = Mathf.CeilToInt(reveal * dotPos.Count);
        for (int i = 0; i < dotPos.Count && i < dots.Count; i++)
            dots[i].color = i < show ? Color.white : Color.clear;
    }

    void UpdateWalker(float now)
    {
        int c = Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, Characters.Count - 1));
        puppet.Set(AnimatorFor(c), 0f);

        float hop = (now - hopAt) / Mathf.Max(0.01f, hopTime);
        bool hopping = animate && hop >= 0f && hop < 1f;
        Vector2 feet = FeetOf(selected);
        float lift = 0f;
        float dirX = 0f;

        if (hopping)
        {
            float e = hop * hop * (3f - 2f * hop);
            feet = Vector2.Lerp(hopFrom, hopTo, e);
            float height = 14f + Mathf.Min(24f, Mathf.Abs(hopTo.x - hopFrom.x) * 0.12f);
            lift = Mathf.Sin(hop * Mathf.PI) * height;
            dirX = Mathf.Sign(hopTo.x - hopFrom.x);
        }

        // Absprung beim Starten: hoch hinaus
        float l = (now - launchAt) / 0.5f;
        if (animate && l >= 0f && l < 1f) lift += Mathf.Sin(Mathf.Min(1f, l * 1.4f) * Mathf.PI * 0.5f) * 40f;

        Sprite frame = puppet.Frame(hopping, dirX);
        if (frame == null)
        {
            walker.color = walkerShadow.color = Color.clear;
        }
        else
        {
            walker.sprite = frame;
            Rect body = CharacterLooks.BodyRect(frame);
            float tex = Mathf.Max(body.width, body.height);
            float k = tex <= CharTarget ? Mathf.Max(1f, Mathf.Floor(CharTarget / tex)) : 1f / Mathf.Ceil(tex / CharTarget);
            if (k > 1f) k = 1f;          // auf dem Pfad 1:1 - die Stationen sind klein
            float w = Mathf.Round(frame.rect.width * k), h = Mathf.Round(frame.rect.height * k);

            float x = Mathf.Round(feet.x - body.width * k / 2f) - Mathf.Round(body.x * k);
            float y = Mathf.Round(feet.y - lift) - Mathf.Round((frame.rect.height - body.y - CharacterLooks.FootRowsFor(c)) * k);
            OptionsKit.Move(walker.rectTransform, x, y, w, h);
            walker.color = Characters.IsAvailable(c) ? Color.white : new Color(0.11f, 0.08f, 0.1f, 1f);

            float sa = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(lift / 30f));
            OptionsKit.Move(walkerShadow.rectTransform, Mathf.Round(feet.x - 9f), Mathf.Round(feet.y - 3f), 18, 5);
            walkerShadow.color = new Color(1f, 1f, 1f, sa);
        }

        // Staubwoelkchen bei der Landung
        float p = (now - landAt) / 0.3f;
        bool puff = animate && p >= 0f && p < 1f;
        for (int i = 0; i < puffs.Count; i++)
        {
            if (!puff) { puffs[i].color = Color.clear; continue; }
            float side = i % 2 == 0 ? -1f : 1f;
            float spread = 4f + (i / 2) * 3f;
            float px = hopTo.x + side * (spread + p * 8f);
            float py = hopTo.y - 1f - Mathf.Sin(p * Mathf.PI) * (2f + i / 2);
            OptionsKit.Move(puffs[i].rectTransform, Mathf.Round(px), Mathf.Round(py), 2, 2);
            puffs[i].color = OptionsKit.WithAlpha(GameHudSkin.Cream, 0.8f * (1f - p));
        }

        // Funken beim Absprung
        float st = now - launchAt;
        bool on = animate && st >= 0f && st < 0.7f;
        Vector2 centre = FeetOf(selected) + new Vector2(0f, -20f);
        for (int i = 0; i < sparks.Count; i++)
        {
            Image img = sparks[i];
            if (!on) { img.color = Color.clear; continue; }
            float ang = i / (float)sparks.Count * Mathf.PI * 2f + 0.3f;
            var dir = new Vector2(Mathf.Cos(ang), -Mathf.Abs(Mathf.Sin(ang)) - 0.3f).normalized;
            float speed = 60f + (i % 4) * 14f;
            Vector2 q = centre + dir * (10f + speed * st) + new Vector2(0f, 110f * st * st);
            float sw = img.sprite.rect.width, sh = img.sprite.rect.height;
            OptionsKit.Move(img.rectTransform, Mathf.Round(q.x - sw / 2f), Mathf.Round(q.y - sh / 2f), sw, sh);
            img.color = new Color(1f, 1f, 1f, 1f - st / 0.7f);
        }
    }

    // ==================================================================
    //  Hilfen
    // ==================================================================

    void PaintButton(Btn b)
    {
        b.Bg.gameObject.SetActive(b.Visible);
        if (!b.Visible) return;

        bool hover = b == hoverBtn;
        bool down = b == pressedBtn && hover;

        GameHudSkin.ButtonLook look;
        if (b.Disabled) look = GameHudSkin.ButtonLook.Disabled;
        else if (b.Active) look = GameHudSkin.ButtonLook.Active;
        else if (down) look = GameHudSkin.ButtonLook.Pressed;
        else look = hover ? b.Hover : b.Normal;
        b.Bg.sprite = GameHudSkin.Button(look);

        Color ink;
        switch (look)
        {
            case GameHudSkin.ButtonLook.Gold:
            case GameHudSkin.ButtonLook.GoldHover: ink = GameHudSkin.Ink; break;
            case GameHudSkin.ButtonLook.Pressed:   ink = GameHudSkin.ParchDark; break;
            case GameHudSkin.ButtonLook.Disabled:  ink = GameHudSkin.StoneLight; break;
            default:                               ink = GameHudSkin.Cream; break;
        }
        b.Label.color = ink;
        b.Icon.color = look == GameHudSkin.ButtonLook.Disabled ? new Color(0.7f, 0.65f, 0.68f, 1f) : Color.white;

        // Gedrueckt rutscht der Inhalt 1 px nach unten - wie bei SkinButton.
        float push = down ? 1f : 0f;
        Vector2 lp = b.Label.rectTransform.anchoredPosition;
        b.Label.rectTransform.anchoredPosition = new Vector2(lp.x, -push);
        if (b.Icon.enabled)
        {
            float ih = b.Icon.sprite.rect.height;
            Vector2 ip = b.Icon.rectTransform.anchoredPosition;
            b.Icon.rectTransform.anchoredPosition = new Vector2(ip.x, -Mathf.Round((b.Area.height - 1f - ih) / 2f) - push);
        }
    }

    /// <summary>Titelschrift in Titelgroesse, wenn es passt; sonst Jersey10; zur Not halb so gross.</summary>
    void FitTitle(TextMeshProUGUI t, TextMeshProUGUI shadow, string text, float width)
    {
        TMP_FontAsset font = OptionsKit.PickFont(text, pixelFont, textFont);
        float size = OptionsKit.SizeTitle;
        t.font = font;
        t.fontSize = size;
        if (OptionsKit.Measure(t, text) > width && font != textFont && textFont != null)
        {
            font = textFont;
            t.font = font;
        }
        if (OptionsKit.Measure(t, text) > width) size = OptionsKit.SizeText;

        t.fontSize = size;
        t.text = text;
        shadow.font = font;
        shadow.fontSize = size;
        shadow.text = text;
    }

    void LayoutPage(bool force)
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (force || size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
            pageBase = page.anchoredPosition;
        }

        // Beim Oeffnen 6 px von unten hereinfahren - ganze Pixel, versteht sich.
        float o = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.18f);
        float lift = animate ? Mathf.Round((1f - o) * (1f - o) * 6f) : 0f;
        page.anchoredPosition = pageBase + new Vector2(0f, -lift);
    }

    string LevelName(int index)
    {
        LevelEntry e = levels[index];
        // Uebersetzung nach Position in der Liste, der Inspector ist der Rueckfall
        return Loc.Get("level." + (index + 1) + ".name", e.displayName ?? "");
    }

    string LevelDesc(int index)
    {
        LevelEntry e = levels[index];
        return Loc.Get("level." + (index + 1) + ".desc", e.description ?? "");
    }

    Ambience AmbienceFor(LevelEntry e)
    {
        if (e.ambience != Ambience.Auto) return e.ambience;
        switch ((e.planId ?? "").Trim().ToLowerInvariant())
        {
            case "world1": return Ambience.Kitchen;
            case "world2": return Ambience.Forest;
            default: return Ambience.Dust;
        }
    }

    private static readonly Dictionary<EnemyId, Sprite> bossPortraits = new Dictionary<EnemyId, Sprite>();

    /// <summary>
    /// Boss-Bild fuer die Kachel: Resources/LevelSelect/Bosses/[EnemyId] (alle
    /// gleich gross, 48x48, aus Tools/boss_portraits.py), sonst das Bestiarium-Bild.
    /// </summary>
    static Sprite BossPortrait(EnemyId id)
    {
        if (bossPortraits.TryGetValue(id, out Sprite cached)) return cached;
        Sprite[] all = Resources.LoadAll<Sprite>("LevelSelect/Bosses/" + id);
        Sprite sprite = all != null && all.Length > 0 ? all[0] : Bestiary.Icon(id);
        bossPortraits[id] = sprite;
        return sprite;
    }

    /// <summary>Boss, Dauer und Arten aus dem Wellenplan. Null ohne (bekannten) Plan.</summary>
    PlanInfo PlanFor(LevelEntry e)
    {
        string id = (e.planId ?? "").Trim();
        if (id.Length == 0) return null;
        if (planCache.TryGetValue(id, out PlanInfo cached)) return cached;

        // WavePlans.For faellt bei Unbekanntem auf World1 zurueck - das soll
        // hier nicht als Steckbrief eines fremden Levels auftauchen.
        bool known = false;
        foreach (string known1 in WavePlans.AllIds)
            if (string.Equals(known1, id, System.StringComparison.OrdinalIgnoreCase)) known = true;

        PlanInfo info = null;
        if (known)
        {
            try
            {
                RunPlan plan = WavePlans.ForMap(id);
                info = new PlanInfo { Duration = plan.TotalDuration };
                var kinds = new HashSet<EnemyId>();
                bool bossIsReal = false;

                void Add(EnemyId enemy, bool isBossBeat)
                {
                    if (enemy == EnemyId.None) return;
                    EnemyDef def = EnemyCatalog.Get(enemy);
                    if (def != null && def.Role == EnemyRole.Blocker) return;   // Kaefig-Wand ist Kulisse
                    kinds.Add(enemy);
                    // Der Endboss zaehlt: ein Zwischenboss (Schleimkoenig im Eis) kommt
                    // zwar auch als Boss-Beat, wird aber vom echten Boss verdraengt.
                    bool realBoss = def != null && def.Role == EnemyRole.Boss;
                    if (realBoss && !bossIsReal) { info.Boss = enemy; bossIsReal = true; }
                    else if (isBossBeat && info.Boss == EnemyId.None) info.Boss = enemy;
                }

                void AddPhase(Phase phase)
                {
                    if (phase == null) return;
                    foreach (PoolEntry p in phase.Enemies) Add(p.Id, false);
                    foreach (Beat b in phase.Beats)
                    {
                        Add(b.Enemy, b.Kind == BeatKind.Boss);
                        Add(b.RingEnemy, false);
                    }
                }

                foreach (Phase phase in plan.Phases) AddPhase(phase);
                AddPhase(plan.Endless);
                info.Kinds = kinds.Count;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Levelauswahl] Wellenplan \"{id}\" nicht lesbar: {ex.Message}");
                info = null;
            }
        }

        planCache[id] = info;
        return info;
    }

    /// <summary>Animator des Charakters: erst aus <see cref="CharacterLooks"/>, sonst vom Hub-Spieler.</summary>
    static RuntimeAnimatorController AnimatorFor(int index)
    {
        RuntimeAnimatorController c = CharacterLooks.AnimatorFor(index);
        if (c != null) return c;

        WM_PlayerSkinSwitcher hub = WM_PlayerSkinSwitcher.Instance;
        return hub != null && index == 0 ? hub.NormalSkinOverride : null;
    }

    /// <summary>
    /// Unsichtbarer Animator als Bildquelle fuer die Figur auf dem Pfad - wie
    /// in der Charakterauswahl: er spielt Idle bzw. Laufen, gezeichnet wird
    /// sein aktuelles Sprite ins UI.
    /// </summary>
    private sealed class Puppet
    {
        private Animator anim;
        private SpriteRenderer source;
        private RuntimeAnimatorController controller;
        private bool pending;

        public static Puppet Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = new Puppet { source = go.AddComponent<SpriteRenderer>(), anim = go.AddComponent<Animator>() };
            p.source.enabled = false;      // nur Bildquelle, wird nie gezeichnet
            p.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            p.anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            return p;
        }

        public void Set(RuntimeAnimatorController c, float phase)
        {
            if (c == controller) return;
            controller = c;
            anim.runtimeAnimatorController = c;
            source.sprite = null;
            pending = c != null;
        }

        /// <summary>Aktuelles Bild: Idle von vorn, oder Laufen zur Seite.</summary>
        public Sprite Frame(bool walking, float dirX)
        {
            if (controller == null || !anim.isActiveAndEnabled) return null;

            if (pending)
            {
                anim.Play("Idle", 0, 0f);
                pending = false;
            }

            bool side = walking && dirX != 0f;
            anim.SetBool("moving", walking);
            anim.SetFloat("MoveX", side ? dirX : 0f);
            anim.SetFloat("MoveY", side ? 0f : -1f);
            anim.SetFloat("LastMoveX", side ? dirX : 0f);
            anim.SetFloat("LastMoveY", side ? 0f : -1f);
            return source.sprite;
        }
    }

    // ==================================================================
    //  Unlocks
    // ==================================================================

    bool IsOpenLevel(LevelEntry e) => StoryUnlocked(e) || EndlessUnlocked(e);

    bool StoryUnlocked(LevelEntry e)
    {
        if (e == null) return false;
        if (string.IsNullOrWhiteSpace(e.storyUnlockId)) return true;
        return Unlocks.IsUnlocked(e.storyUnlockId);
    }

    bool EndlessUnlocked(LevelEntry e)
    {
        if (e == null || !e.endlessAvailable) return false;
        if (string.IsNullOrWhiteSpace(e.endlessUnlockId)) return true;
        return Unlocks.IsUnlocked(e.endlessUnlockId);
    }

    /// <summary>
    /// Gibt es die Szene zu diesem Level schon? Ist sie nicht eingetragen oder
    /// fehlt sie in den Build Settings, bleibt die Station sichtbar und zeigt
    /// "BALD" - so kann man Level vorbereiten, bevor sie gebaut sind.
    /// </summary>
    bool IsLinked(LevelEntry e)
    {
        if (e == null || (e.mapId < 0 && string.IsNullOrWhiteSpace(e.sceneToLoad))) return false;

        // Nicht sceneToLoad direkt fragen: das Level liegt in seiner eigenen
        // Map-Szene (Map_World<mapId>), der Eintrag ist nur die Rueckfallebene.
        string scene = MapSceneSystem.ResolveScene(e.sceneToLoad, e.mapId);
        if (string.IsNullOrWhiteSpace(scene)) return false;

        return Application.CanStreamedLevelBeLoaded(scene);
    }

    // ==================================================================
    //  Starten
    // ==================================================================

    void Play()
    {
        LevelEntry e = Current;
        if (e == null || launching) return;

        bool mayPlay = endlessChosen ? EndlessUnlocked(e) : StoryUnlocked(e);
        if (!mayPlay) { PlayDenySound(); return; }

        // Welche Szene das Level wirklich laedt: die Map-Szene zur Map-ID
        // (Map_World<mapId>). Der Eintrag im Inspector ist nur noch die
        // Rueckfallebene fuer Welten ohne eigene Szene.
        string sceneToLoad = MapSceneSystem.ResolveScene(e.sceneToLoad, e.mapId);

        if (!IsLinked(e))
        {
            Debug.Log($"[Levelauswahl] Level {selected + 1} ist noch nicht verknuepft " +
                      $"(Szene \"{sceneToLoad}\"). Map-ID eintragen und die Szene " +
                      "in die Build Settings aufnehmen.");
            PlayDenySound();
            return;
        }

        // Erst springt die Figur ab, dann geht es los.
        launching = true;
        launchAt = Time.unscaledTime;
        hoverBtn = pressedBtn = null;
        Refresh();
        PlayStartSound();

        if (animate) StartCoroutine(LaunchAfter(LaunchDelay, e, sceneToLoad));
        else Launch(e, sceneToLoad);
    }

    System.Collections.IEnumerator LaunchAfter(float delay, LevelEntry e, string sceneToLoad)
    {
        float until = Time.unscaledTime + delay;
        while (Time.unscaledTime < until) yield return null;
        Launch(e, sceneToLoad);
    }

    void Launch(LevelEntry e, string sceneToLoad)
    {
        // Ab hier laeuft genau das ab, was in der World Map beim Betreten eines
        // Map-Punktes passiert (PlayerWorldInteraction, Fall "Map"). Wer beim
        // Anbinden eines Levels etwas vermisst, vergleicht am besten dort.

        // 1. Story oder Endless? Das liest der Rest des Spiels aus GameSession.
        GameSession.SelectedMode = endlessChosen ? GameMode.Endless : GameMode.Story;

        // 2. Stand sichern, bevor es losgeht: der Tracker merkt sich, welche
        //    Achievements und Unlocks vor dem Lauf schon offen waren, damit der
        //    Abschlussbildschirm nur die neuen zeigt. Ach.FirstGame wird hier
        //    bewusst NICHT freigeschaltet - das macht PlayerController.StartStats()
        //    in der Zielszene, wo auch das Charakter-Level vor dem Lauf gemerkt wird.
        SessionProgressTracker.Instance?.SnapshotBeforeGame();

        // 3. Skilltree auf den gewaehlten Charakter stellen.
        Skills.SetActiveTreeForCharacter(Shop.SkinIndex);

        // 4. Welche Welt gespielt wird, steht im MapsManager - er ueberlebt den
        //    Szenenwechsel. Kommt der Spieler aus dem Hub, legt Ensure() ihn an.
        MapsManager.Ensure().selectedMap = e.mapId;

        // 5. Shop-Stand fuer diesen Lauf einfrieren.
        Shop.CaptureRun();

        // 6. Umschalten. Der Hub-Name ist zugleich das Rueckreiseziel:
        //    GameManager.Restart() bringt den Spieler nach dem Lauf hierher zurueck.
        string hubScene = gameObject.scene.name;
        GameSession.ReturnScene = hubScene;

        // Wie Hauptmenue -> Hub: Bild und Hub-Musik blenden aus, der Wechsel
        // passiert hinter der Blende. Bis dahin bleibt die Auswahl offen und
        // haelt den Hub gesperrt.
        bool started = SceneFader.Switch(() => SwitchToLevel(sceneToLoad, hubScene),
                                         () => SceneManager.GetSceneByName(MapSceneSystem.CoreScene).isLoaded);
        if (!started) launching = false;
    }

    void SwitchToLevel(string sceneToLoad, string hubScene)
    {
        launching = false;
        Close();

        // Der uebliche Weg: additiv laden und den Hub stilllegen. Die Map-Szene
        // holt sich GameCore selbst dazu (MapBootstrap).
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ActivateScene(sceneToLoad);
            MenuManager.Instance.DeactivateScene(hubScene);
            return;
        }

        // Rueckfallebene ohne MenuManager (hub allein gestartet): hart umschalten.
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    // ==================================================================
    //  Ton
    // ==================================================================

    void PlaySfx(AudioClip clip, bool clickIfMissing)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
        else if (clickIfMissing) OptionsKit.PlayClick();
    }

    void PlayStartSound()
    {
        if (startClip != null) { PlaySfx(startClip, false); return; }
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();
    }

    void PlayDenySound()
    {
        if (denyClip != null) { PlaySfx(denyClip, false); return; }
        if (AudioController.Instance != null && AudioController.Instance.PlayerHit != null)
            AudioController.Instance.PlayerHit.Play();
    }
}
