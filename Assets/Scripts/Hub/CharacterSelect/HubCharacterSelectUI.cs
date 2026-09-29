using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Charakterauswahl im Hub, im Stil von "UI 2.0" (Optionen, Level-Up):
///
///   - links die KEKSDOSE: ein 3x3-Raster, ein Fenster je Charakter aus
///     <see cref="Characters"/>, darin sein Portraet mit der echten
///     Idle-Animation. Leere Plaetze zeigen ein Fragezeichen; sind es mehr als
///     neun Charaktere, wird geblaettert (Pfeile, Mausrad, oder einfach mit den
///     Pfeiltasten weiterlaufen).
///   - rechts die BUEHNE: der Charakter unter dem Cursor gross im Spot auf
///     einer Tortenplatte, daneben Name, Spruch, Beschreibung, Startwaffe und
///     wie weit sein Skilltree je Kategorie ist.
///   - unten ZURUECK und WAEHLEN.
///
/// Fenster anklicken zeigt den Charakter nur auf der Buehne; gewaehlt wird mit
/// WAEHLEN, Enter/E oder einem zweiten Klick. Die Ziffern 1-9 waehlen den
/// Platz auf der aktuellen Seite direkt. Der Hub-Spieler zieht sich den
/// gewaehlten Charakter sofort an.
///
/// Alles liegt auf einer 480x270-Seite (Ursprung oben links, jede Zahl ein
/// Pixel), ganzzahlig skaliert wie bei <see cref="OptionsKit"/>. Treffer
/// rechnet das Skript selbst aus der Mausposition - wie die anderen
/// Hub-Fenster, siehe <see cref="HubLevelSelectUI"/>. Grafik kommt aus
/// <see cref="GameHudSkin"/>, Bilddateien gibt es keine.
///
/// Die Wahl liegt in Shop.SkinIndex, also im Spielstand. Das Setzen zieht
/// Skilltree und Verteiler des Charakters von allein nach.
///
/// EIN CHARAKTER DAZU: an dieser Datei nichts. Name, Spruch, Text, Startwaffe
/// in <see cref="Characters"/>, Animator und Farbe in <see cref="CharacterLooks"/>.
/// </summary>
[DisallowMultipleComponent]
public class HubCharacterSelectUI : MonoBehaviour
{
    /// <summary>Steht offen? Der Hub sperrt solange seine Interaktionen.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Tasten")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode confirmKeyAlt = KeyCode.E;

    [Header("Darstellung")]
    [Tooltip("Wie stark der Hub hinter dem Fenster abgedunkelt wird.")]
    [SerializeField, Range(0f, 1f)] private float dim = 0.86f;
    [Tooltip("Freie Plaetze der Keksdose als '?' zeigen - sonst bleiben sie weg.")]
    [SerializeField] private bool showEmptySlots = true;
    [Tooltip("Spot, Kruemel, Huepfer und Funken. Aus = alles steht still.")]
    [SerializeField] private bool animate = true;
    [Tooltip("Der Spieler im Hub traegt sofort den gewaehlten Charakter - auch schon beim Betreten des Hubs.")]
    [SerializeField] private bool dressHubPlayer = true;

    [Header("Ton")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip pickClip;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // ==================================================================
    //  Masse (Seitenpixel, oben links)
    // ==================================================================

    private const int Cols = 3, Rows = 3, PerPage = Cols * Rows;

    private const int RibbonY = 6;

    // Keksdose
    private const int RosterX = 10, RosterY = 34, RosterW = 168, RosterH = 204;
    private const int Slot = 48, SlotGap = 5, SlotInset = 3;
    private const int GridX = RosterX + (RosterW - (Cols * Slot + (Cols - 1) * SlotGap)) / 2;
    private const int GridY = RosterY + 22;
    private const int PagerY = RosterY + RosterH - 22;

    // rechte Karte mit Buehne und Steckbrief
    private const int CardX = 184, CardY = 34, CardW = 286, CardH = 204;
    private const int StageX = 190, StageY = 40, StageW = 136, StageH = 192, StageFloor = 44;
    private const int FeetY = StageY + 156;         // hier stehen die Fuesse auf der Platte
    private const int InfoX = 334, InfoW = 128;

    // Fusszeile
    private const int BarY = 244, BarH = 20;
    private const int BackX = 10, BackW = 80;
    private const int ChooseW = 110, ChooseX = CardX + CardW - ChooseW;

    private static readonly RectInt Content = new RectInt(8, 4, 464, 262);

    // Bildgroessen: Frames werden nur in ganzen Vielfachen (oder ganzen
    // Bruchteilen) ihrer Textur gezeichnet - 64er-Keks im Fenster 1:1, auf
    // der Buehne 2:1.
    private const float PortraitTarget = 64f, StageTarget = 128f;

    // Bilder pro Sekunde, wenn ein gemaltes Portraet unter dem Cursor laeuft
    private const float PortraitFps = 6f;

    private static readonly Color Silhouette = new Color(0.11f, 0.08f, 0.1f, 1f);

    // ==================================================================
    //  Zustand
    // ==================================================================

    private sealed class Puppet
    {
        public Animator Anim;
        public SpriteRenderer Source;
        private RuntimeAnimatorController controller;
        private float pendingPhase = -1f;

        public static Puppet Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = new Puppet { Source = go.AddComponent<SpriteRenderer>(), Anim = go.AddComponent<Animator>() };
            p.Source.enabled = false;      // nur Bildquelle, wird nie gezeichnet
            p.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            p.Anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            return p;
        }

        public void Set(RuntimeAnimatorController c, float phase)
        {
            if (c == controller) return;
            controller = c;
            Anim.runtimeAnimatorController = c;
            Source.sprite = null;
            pendingPhase = c != null ? phase : -1f;
        }

        /// <summary>Aktuelles Bild - Idle oder Laufen auf der Stelle, immer von vorn.</summary>
        public Sprite Frame(bool walking)
        {
            if (controller == null || !Anim.isActiveAndEnabled) return null;

            if (pendingPhase >= 0f)
            {
                Anim.Play("Idle", 0, pendingPhase);
                pendingPhase = -1f;
            }

            Anim.SetBool("moving", walking);
            Anim.SetFloat("MoveX", 0f);
            Anim.SetFloat("MoveY", -1f);
            Anim.SetFloat("LastMoveX", 0f);
            Anim.SetFloat("LastMoveY", -1f);
            return Source.sprite;
        }
    }

    private sealed class SlotView
    {
        public Rect Area;
        public int Character = -1;       // -1 = leerer Platz
        public GameObject Root;
        public Image Frame, Back, Portrait, Lock, Question, Check, KeyChip, Glow;
        public TextMeshProUGUI Key;
        public Puppet Puppet;
    }

    private sealed class Btn
    {
        public Rect Area;
        public Image Bg, Icon;
        public TextMeshProUGUI Label;
        public GameHudSkin.ButtonLook Normal, Hover;
        public bool Disabled, Active, Visible = true;
    }

    private sealed class SkillRow
    {
        public GameObject Root;
        public Image Icon, Fill;
        public TextMeshProUGUI Count;
    }

    private sealed class Spark
    {
        public Image Img;
        public Vector2 Offset, Velocity;
    }

    private TMP_FontAsset textFont, pixelFont;
    private GameObject root;
    private RectTransform page;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;
    private Vector2 pageBase;

    private readonly List<SlotView> slots = new List<SlotView>();
    private Btn backBtn, chooseBtn, prevBtn, nextBtn;
    private TextMeshProUGUI rosterCount, pageLabel;

    // Buehne
    private Image stageSpot, stagePool, stageChar, stageLock, statusChip, statusIcon, keyChip;
    private TextMeshProUGUI statusText, stageKey;
    private Puppet stagePuppet;
    private readonly List<Image> crumbs = new List<Image>();
    private readonly List<Vector3> crumbState = new List<Vector3>();   // x, y, Phase
    private readonly List<Spark> sparks = new List<Spark>();
    private TextMeshProUGUI floater, floaterShadow;

    // Steckbrief
    private TextMeshProUGUI nameText, nameShadow, taglineText, descText, weaponLabel, weaponName, skillLabel, skillTotal, skillNone;
    private Image weaponTile, weaponIcon;
    private readonly List<SkillRow> skillRows = new List<SkillRow>();

    private int cursor;              // Charakter auf der Buehne
    private int pageIndex;
    private int hoverSlot = -1;      // Platz auf der Seite unter der Maus
    private Btn hoverBtn, pressedBtn;
    private int openedOnFrame = -1;
    private bool built;

    // Zeitmarken fuer die kleinen Auftritte (unscaledTime)
    private float openedAt = -10f, cursorAt = -10f, chosenAt = -10f;
    private float lastClickAt = -10f;
    private int lastClickSlot = -1;
    private Color spotColor, spotTarget;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    void Awake()
    {
        EnsureSfxSource();
        Build();
        DressHubPlayer(Shop.SkinIndex);
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
            ShowPage(pageIndex);
            Refresh();
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
        root = new GameObject("CharacterSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) root.layer = uiLayer;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 135;       // dieselbe Etage wie Levelauswahl und Skilltree

        scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        OptionsKit.Stretch("Dim", root.transform, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, dim));

        page = OptionsKit.Rect("Page", root.transform, 0, 0, OptionsKit.RefW, OptionsKit.RefH);

        OptionsKit.Ribbon(page, OptionsKit.RefW / 2f, RibbonY,
                          Loc.Get("ui.charselect.title", "CHARAKTER"), pixelFont, textFont);

        BuildRoster();
        BuildStage();
        BuildInfo();
        BuildBar();

        lastScreen = Vector2Int.zero;
        root.SetActive(false);
    }

    // ---------- Keksdose ----------

    void BuildRoster()
    {
        OptionsKit.Img("RosterCard", page, RosterX, RosterY, RosterW, RosterH, GameHudSkin.Card, true);

        string title = Loc.Get("ui.charselect.roster", "KEKSDOSE");
        OptionsKit.ShadowLabel("RosterTitle", page, RosterX + 8, RosterY + 5, 100, 13, title,
                               OptionsKit.PickFont(title, pixelFont, textFont), OptionsKit.SizeText,
                               GameHudSkin.Parchment, TextAlignmentOptions.Left);
        rosterCount = OptionsKit.Label("RosterCount", page, RosterX + RosterW - 58, RosterY + 5, 50, 13, "",
                                       textFont, OptionsKit.SizeText, GameHudSkin.StoneLight,
                                       TextAlignmentOptions.Right);

        slots.Clear();
        for (int i = 0; i < PerPage; i++)
        {
            int col = i % Cols, row = i / Cols;
            var area = new Rect(GridX + col * (Slot + SlotGap), GridY + row * (Slot + SlotGap), Slot, Slot);
            slots.Add(BuildSlot(i, area));
        }

        prevBtn = MakeButton("Prev", RosterX + 30, PagerY, 22, 16, "", GameHudSkin.ArrowLeft, false);
        nextBtn = MakeButton("Next", RosterX + RosterW - 52, PagerY, 22, 16, "", GameHudSkin.Arrow, false);
        pageLabel = OptionsKit.Label("Page", page, RosterX + 56, PagerY + 1, RosterW - 112, 14, "",
                                     textFont, OptionsKit.SizeText, GameHudSkin.Parchment,
                                     TextAlignmentOptions.Center);
    }

    SlotView BuildSlot(int i, Rect a)
    {
        var s = new SlotView { Area = a };
        RectTransform rt = OptionsKit.Rect("Slot " + (i + 1), page, a.x, a.y, a.width, a.height);
        s.Root = rt.gameObject;

        // Leuchtsaum um den Platz unter dem Cursor - atmet nur ueber die Deckkraft.
        s.Glow = OptionsKit.Img("Glow", rt, -2, -2, a.width + 4, a.height + 4, GameHudSkin.Ring, true);
        s.Glow.color = OptionsKit.WithAlpha(GameHudSkin.Gold, 0f);

        int win = Slot - 2 * SlotInset;
        s.Back = OptionsKit.Img("Back", rt, SlotInset, SlotInset, win, win, GameHudSkin.SlotBack(win, win));

        RectTransform window = OptionsKit.Rect("Window", rt, SlotInset, SlotInset, win, win);
        window.gameObject.AddComponent<RectMask2D>();
        s.Portrait = OptionsKit.Img("Portrait", window, 0, 0, 64, 64, null);
        RectTransform prt = s.Portrait.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        s.Portrait.enabled = false;

        s.Frame = OptionsKit.Img("Frame", rt, 0, 0, a.width, a.height, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), true);

        s.Question = OptionsKit.Img("Question", rt, (Slot - 14) / 2, (Slot - 18) / 2, 14, 18, GameHudSkin.Question);
        s.Lock = OptionsKit.Img("Lock", rt, (Slot - 14) / 2, (Slot - 18) / 2 + 2, 14, 18, GameHudSkin.Lock);
        s.Check = OptionsKit.Img("Check", rt, Slot - 12, -2, 9, 8, GameHudSkin.Check);

        s.KeyChip = OptionsKit.Img("KeyChip", rt, 1, Slot - 12, 10, 11, GameHudSkin.LevelChip, true);
        s.Key = OptionsKit.Label("Key", rt, 1, Slot - 13, 10, 12, (i + 1).ToString(), pixelFont,
                                 OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);

        s.Puppet = Puppet.Create(root.transform, "SlotPuppet " + (i + 1));
        return s;
    }

    // ---------- Buehne ----------

    void BuildStage()
    {
        OptionsKit.Img("InfoCard", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);

        OptionsKit.Img("Stage", page, StageX, StageY, StageW, StageH, GameHudSkin.Stage(StageW, StageH, StageFloor));

        const int spotW = 120, spotH = 150;
        stageSpot = OptionsKit.Img("Spot", page, StageX + (StageW - spotW) / 2, StageY + 8, spotW, spotH,
                                   GameHudSkin.Spotlight(spotW, spotH));
        const int poolW = 124, poolH = 20;
        stagePool = OptionsKit.Img("Pool", page, StageX + (StageW - poolW) / 2, FeetY - 6, poolW, poolH,
                                   GameHudSkin.LightPool(poolW, poolH));

        // Kruemel, die im Licht schweben
        crumbs.Clear();
        crumbState.Clear();
        var rng = new System.Random(7);
        for (int i = 0; i < 14; i++)
        {
            Image c = OptionsKit.Img("Crumb", page, 0, 0, 1, 1, GameHudSkin.White);
            crumbs.Add(c);
            crumbState.Add(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() * 6.28f));
        }

        OptionsKit.Img("Pedestal", page, StageX + (StageW - 104) / 2, FeetY - 6, 104, 22, GameHudSkin.Pedestal);

        // Bleibt immer eingeschaltet und wird nur durchsichtig: ein Bild, das
        // waehrend des Oeffnens erst eingeschaltet wird, blieb im Canvas-Batch
        // unsichtbar, bis sich die Hierarchie aenderte.
        stageChar = OptionsKit.Img("Character", page, StageX, StageY, 128, 128, null, Color.clear);
        stageLock = OptionsKit.Img("StageLock", page, StageX + (StageW - 28) / 2, FeetY - 70, 28, 36, GameHudSkin.Lock);

        // Vorhang oben, ueber allem
        OptionsKit.Img("Valance", page, StageX + 1, StageY + 1, StageW - 2, 9, GameHudSkin.Valance(StageW - 2));

        // Nummer oben links - die Ziffer, mit der man ihn direkt waehlt
        keyChip = OptionsKit.Img("StageKeyChip", page, StageX + 5, StageY + 13, 12, 13, GameHudSkin.LevelChip, true);
        stageKey = OptionsKit.Label("StageKey", page, StageX + 5, StageY + 13, 12, 13, "", pixelFont,
                                    OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);

        // Schild unten: AKTIV / GESPERRT
        statusChip = OptionsKit.Img("StatusChip", page, StageX, StageY + StageH - 20, 60, 14, GameHudSkin.LevelChip, true);
        statusIcon = OptionsKit.Img("StatusIcon", page, StageX, StageY + StageH - 18, 9, 8, GameHudSkin.Check);
        statusText = OptionsKit.Label("StatusText", page, StageX, StageY + StageH - 20, 60, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Mint, TextAlignmentOptions.Left);

        sparks.Clear();
        for (int i = 0; i < 12; i++)
        {
            Image img = OptionsKit.Img("Spark", page, 0, 0, i % 3 == 0 ? 9 : 5, i % 3 == 0 ? 9 : 5,
                                       i % 3 == 0 ? GameHudSkin.Star : GameHudSkin.Sparkle);
            img.color = Color.clear;
            float angle = (i / 12f) * Mathf.PI * 2f + 0.3f;
            float speed = 46f + (i % 4) * 12f;
            // Platzen als Ring um die Figur auf, nicht mitten aus dem Gesicht.
            var dir = new Vector2(Mathf.Cos(angle), -Mathf.Abs(Mathf.Sin(angle)) - 0.35f).normalized;
            sparks.Add(new Spark { Img = img, Offset = dir * 46f, Velocity = dir * speed });
        }

        string picked = Loc.Get("ui.charselect.picked", "GEWÄHLT!");
        TMP_FontAsset pickedFont = OptionsKit.PickFont(picked, pixelFont, textFont);
        floaterShadow = OptionsKit.Label("FloaterShadow", page, StageX, StageY + 31, StageW, 22, picked, pickedFont,
                                         OptionsKit.SizeTitle, GameHudSkin.Ink, TextAlignmentOptions.Center);
        floater = OptionsKit.Label("Floater", page, StageX, StageY + 30, StageW, 22, picked, pickedFont,
                                   OptionsKit.SizeTitle, GameHudSkin.Gold, TextAlignmentOptions.Center);
        floater.alpha = floaterShadow.alpha = 0f;

        stagePuppet = Puppet.Create(root.transform, "StagePuppet");
    }

    // ---------- Steckbrief ----------

    void BuildInfo()
    {
        nameShadow = OptionsKit.Label("NameShadow", page, InfoX, StageY + 3, InfoW, 22, "", pixelFont,
                                      OptionsKit.SizeTitle, GameHudSkin.Ink, TextAlignmentOptions.Left);
        nameText = OptionsKit.Label("Name", page, InfoX, StageY + 2, InfoW, 22, "", pixelFont,
                                    OptionsKit.SizeTitle, GameHudSkin.Cream, TextAlignmentOptions.Left);

        taglineText = OptionsKit.Label("Tagline", page, InfoX, StageY + 24, InfoW, 13, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);

        OptionsKit.Img("Rule", page, InfoX, StageY + 39, InfoW, 1, GameHudSkin.White, OptionsKit.WithAlpha(GameHudSkin.Stone, 0.7f));

        descText = OptionsKit.Label("Desc", page, InfoX, StageY + 43, InfoW, 40, "", textFont,
                                    OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.TopLeft);
        descText.textWrappingMode = TextWrappingModes.Normal;

        string wl = Loc.Get("ui.charselect.weapon", "STARTWAFFE");
        // Das 14er-Werkbanksymbol 1:1 - groessere Symbole gibt es nicht fuer jede Waffe,
        // und hochskaliert wird das 14er matschig.
        weaponTile = OptionsKit.Img("WeaponTile", page, InfoX, StageY + 88, 24, 24, GameHudSkin.Tile(24, GameHudSkin.TileKind.Weapon));
        weaponIcon = OptionsKit.Img("WeaponIcon", page, InfoX + 5, StageY + 92, 14, 14, null);
        weaponLabel = OptionsKit.Label("WeaponLabel", page, InfoX + 30, StageY + 87, InfoW - 30, 13, wl, textFont,
                                       OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        weaponName = OptionsKit.Label("WeaponName", page, InfoX + 30, StageY + 99, InfoW - 30, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Left);

        string sl = Loc.Get("ui.charselect.skills", "SKILLTREE");
        skillLabel = OptionsKit.Label("SkillLabel", page, InfoX, StageY + 124, InfoW, 13, sl, textFont,
                                      OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        skillTotal = OptionsKit.Label("SkillTotal", page, InfoX, StageY + 124, InfoW, 13, "", textFont,
                                      OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Right);
        skillNone = OptionsKit.Label("SkillNone", page, InfoX, StageY + 138, InfoW, 13,
                                     Loc.Get("ui.charselect.noskills", "Eigener Baum folgt."), textFont,
                                     OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Left);

        skillRows.Clear();
        for (int i = 0; i < 4; i++)
        {
            int y = StageY + 138 + i * 12;
            RectTransform rowRt = OptionsKit.Rect("SkillRow " + i, page, InfoX, y, InfoW, 12);
            var row = new SkillRow { Root = rowRt.gameObject };
            row.Icon = OptionsKit.Img("Icon", rowRt, 0, 0, 12, 12, null);
            OptionsKit.Img("Bar", rowRt, 16, 3, 78, 7, GameHudSkin.BarFrame, true);
            row.Fill = OptionsKit.Img("Fill", rowRt, 17, 4, 0, 4, GameHudSkin.White);
            row.Count = OptionsKit.Label("Count", rowRt, 96, -1, InfoW - 96, 13, "", textFont,
                                         OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Right);
            skillRows.Add(row);
        }
    }

    // ---------- Fusszeile ----------

    void BuildBar()
    {
        backBtn = MakeButton("Back", BackX, BarY, BackW, BarH, Loc.Get("ui.charselect.back", "ZURÜCK"), null, false);

        chooseBtn = MakeButton("Choose", ChooseX, BarY, ChooseW, BarH, "", null, true);
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
        openedOnFrame = Time.frameCount;
        openedAt = Time.unscaledTime;
        root.SetActive(true);

        cursor = Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, Characters.Count - 1));
        hoverSlot = -1;
        hoverBtn = pressedBtn = null;
        lastClickSlot = -1;
        spotColor = spotTarget = SpotColorFor(cursor);
        cursorAt = -10f;
        chosenAt = -10f;
        ShowPage(cursor / PerPage);
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

    // ==================================================================
    //  Eingabe
    // ==================================================================

    void Update()
    {
        if (!IsOpen) return;

        LayoutPage(false);

        // Das [E], mit dem die Auswahl aufgeht, darf nicht gleich durchschlagen
        if (Time.frameCount == openedOnFrame) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0)) MouseDown();
        if (Input.GetMouseButtonUp(0)) MouseUp();

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(+1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-Cols);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+Cols);
        if (Input.GetKeyDown(KeyCode.PageUp)) FlipPage(-1);
        if (Input.GetKeyDown(KeyCode.PageDown)) FlipPage(+1);

        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.01f) FlipPage(-1);
        else if (wheel < -0.01f) FlipPage(+1);

        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(confirmKeyAlt) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Choose(cursor);

        // 1..9 waehlen den Platz auf dieser Seite direkt.
        for (int i = 0; i < PerPage; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            int c = slots[i].Character;
            if (c < 0) break;
            SetCursor(c, false);
            Choose(c);
            break;
        }
    }

    void MouseDown()
    {
        if (hoverBtn != null) { pressedBtn = hoverBtn; Refresh(); return; }
        if (hoverSlot < 0) return;

        int c = slots[hoverSlot].Character;
        if (c < 0) return;

        // Zweiter Klick auf denselben Platz (oder Doppelklick) waehlt.
        bool again = c == cursor && (lastClickSlot == hoverSlot || Time.unscaledTime - lastClickAt < 0.35f);
        lastClickSlot = hoverSlot;
        lastClickAt = Time.unscaledTime;

        if (again) Choose(c);
        else SetCursor(c, true);
    }

    void MouseUp()
    {
        Btn b = pressedBtn;
        pressedBtn = null;
        if (b != null && b == hoverBtn && b.Visible && !b.Disabled)
        {
            if (b == backBtn) { Close(); return; }
            if (b == chooseBtn) Choose(cursor);
            else if (b == prevBtn) FlipPage(-1);
            else if (b == nextBtn) FlipPage(+1);
        }
        Refresh();
    }

    void Move(int step)
    {
        if (Characters.Count == 0) return;
        int next = Mathf.Clamp(cursor + step, 0, Characters.Count - 1);
        if (next == cursor) return;
        SetCursor(next, true);
    }

    void FlipPage(int dir)
    {
        int pages = PageCount;
        if (pages <= 1) return;
        int target = Mathf.Clamp(pageIndex + dir, 0, pages - 1);
        if (target == pageIndex) return;

        // Der Cursor wandert mit, auf denselben Platz der neuen Seite.
        int within = cursor - pageIndex * PerPage;
        SetCursor(Mathf.Min(target * PerPage + within, Characters.Count - 1), true);
    }

    void SetCursor(int index, bool sound)
    {
        if (index == cursor && index / PerPage == pageIndex) return;
        cursor = index;
        cursorAt = Time.unscaledTime;
        spotTarget = SpotColorFor(cursor);
        if (cursor / PerPage != pageIndex) ShowPage(cursor / PerPage);
        if (sound) PlaySfx(moveClip, false);
        Refresh();
    }

    void Choose(int index)
    {
        if (index < 0 || index >= Characters.Count) return;
        if (!Characters.IsAvailable(index)) { PlaySfx(moveClip, true); return; }

        if (index != cursor) SetCursor(index, false);

        // Das Setzen stellt zugleich Skilltree und Verteiler auf diesen Charakter um.
        if (Shop.SkinIndex != index) Shop.SkinIndex = index;
        DressHubPlayer(index);

        chosenAt = Time.unscaledTime;
        PlaySfx(pickClip != null ? pickClip : moveClip, true);
        Refresh();
    }

    void DressHubPlayer(int index)
    {
        if (!dressHubPlayer) return;
        WM_PlayerSkinSwitcher hubPlayer = WM_PlayerSkinSwitcher.Instance;
        if (hubPlayer != null) hubPlayer.skinIndex = index;
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
        int wasSlot = hoverSlot;
        Btn wasBtn = hoverBtn;
        hoverSlot = -1;
        hoverBtn = null;

        if (MousePage(out Vector2 m))
        {
            foreach (Btn b in new[] { backBtn, chooseBtn, prevBtn, nextBtn })
            {
                if (b != null && b.Visible && b.Area.Contains(m)) { hoverBtn = b; break; }
            }

            if (hoverBtn == null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Character < 0 || !slots[i].Area.Contains(m)) continue;
                    hoverSlot = i;
                    break;
                }
            }
        }

        if (hoverSlot != wasSlot || hoverBtn != wasBtn) Refresh();
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    int PageCount => Mathf.Max(1, (Characters.Count + PerPage - 1) / PerPage);

    void ShowPage(int index)
    {
        pageIndex = Mathf.Clamp(index, 0, PageCount - 1);

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView s = slots[i];
            int c = pageIndex * PerPage + i;
            s.Character = c < Characters.Count ? c : -1;
            s.Root.SetActive(s.Character >= 0 || showEmptySlots);

            // Versetzte Startphase - sonst atmen alle Kekse im Gleichtakt.
            s.Puppet.Set(s.Character >= 0 ? AnimatorFor(s.Character) : null, (i * 0.37f) % 1f);
        }
    }

    void Refresh()
    {
        if (page == null) return;

        int chosen = Shop.SkinIndex;

        // Keksdose
        int available = 0;
        for (int i = 0; i < Characters.Count; i++) if (Characters.IsAvailable(i)) available++;
        rosterCount.text = available + "/" + Characters.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView s = slots[i];
            bool real = s.Character >= 0;
            bool open = real && Characters.IsAvailable(s.Character);
            bool isCursor = real && s.Character == cursor;
            bool isHover = i == hoverSlot;

            GameHudSkin.SlotLook look = !real ? GameHudSkin.SlotLook.Stone
                : isCursor ? GameHudSkin.SlotLook.Gold
                : isHover ? GameHudSkin.SlotLook.Hover
                : GameHudSkin.SlotLook.Wood;
            s.Frame.sprite = GameHudSkin.SlotFrame(look);

            int win = Slot - 2 * SlotInset;
            s.Back.sprite = real ? GameHudSkin.SlotBack(win, win) : GameHudSkin.SlotEmpty(win, win);
            s.Back.color = real && open ? Color.Lerp(Color.white, CharacterLooks.AccentFor(s.Character), 0.3f)
                         : real ? new Color(0.55f, 0.5f, 0.52f, 1f) : Color.white;

            s.Question.enabled = !real;
            s.Question.color = OptionsKit.WithAlpha(GameHudSkin.Cream, 0.55f);
            s.Lock.enabled = real && !open;
            s.Check.enabled = real && s.Character == chosen;
            s.KeyChip.enabled = s.Key.enabled = real;
            s.Key.color = isCursor ? GameHudSkin.Gold : GameHudSkin.Parchment;
            if (!real) s.Portrait.enabled = false;
        }

        int pages = PageCount;
        prevBtn.Visible = nextBtn.Visible = pages > 1;
        prevBtn.Disabled = pageIndex == 0;
        nextBtn.Disabled = pageIndex >= pages - 1;
        pageLabel.text = pages > 1 ? (pageIndex + 1) + " / " + pages : "";

        RefreshInfo(chosen);

        // Knoepfe
        if (Characters.Count == 0)
        {
            chooseBtn.Disabled = true;
            SetButton(chooseBtn, Loc.Get("ui.charselect.choose", "WÄHLEN"), null);
        }
        else if (!Characters.IsAvailable(cursor))
        {
            chooseBtn.Disabled = true;
            chooseBtn.Active = false;
            SetButton(chooseBtn, Loc.Get("ui.charselect.locked", "GESPERRT"), GameHudSkin.Lock);
        }
        else if (cursor == chosen)
        {
            chooseBtn.Disabled = false;
            chooseBtn.Active = true;
            SetButton(chooseBtn, Loc.Get("ui.charselect.chosen", "GEWÄHLT"), GameHudSkin.Check);
        }
        else
        {
            chooseBtn.Disabled = false;
            chooseBtn.Active = false;
            SetButton(chooseBtn, Loc.Get("ui.charselect.choose", "WÄHLEN"), null);
        }

        foreach (Btn b in new[] { backBtn, chooseBtn, prevBtn, nextBtn }) PaintButton(b);
    }

    void RefreshInfo(int chosen)
    {
        bool any = Characters.Count > 0;
        int c = Mathf.Clamp(cursor, 0, Mathf.Max(0, Characters.Count - 1));
        bool open = any && Characters.IsAvailable(c);

        // Name: Titelschrift, solange sie passt - sonst die schmalere.
        string name = !any ? "" : open ? Characters.DisplayName(c).ToUpperInvariant() : "???";
        FitTitle(nameText, nameShadow, name, InfoW);

        Color accent = CharacterLooks.AccentFor(c);
        taglineText.text = open ? Characters.Tagline(c) : "";
        taglineText.color = Color.Lerp(accent, GameHudSkin.Cream, 0.35f);
        descText.text = !any ? "" : open ? Characters.Description(c)
                      : Loc.Get("ui.charselect.lockedhint", "Noch nicht freigeschaltet.");

        // Startwaffe
        WeaponDef weapon = open ? WeaponCatalog.Find(Characters.StartWeaponId(c)) : null;
        bool hasWeapon = weapon != null;
        weaponLabel.enabled = weaponTile.enabled = weaponName.enabled = hasWeapon;
        weaponIcon.enabled = hasWeapon && weapon.Icon != null;
        if (hasWeapon)
        {
            weaponIcon.sprite = weapon.Icon;
            weaponName.text = weapon.Name;
        }

        // Skilltree: je Kategorie ein Balken
        SkillTreeDef tree = null;
        if (open)
        {
            try { tree = SkillTrees.ForCharacter(c); }
            catch (System.Exception e) { Debug.LogWarning($"[CharacterSelect] Kein Skilltree: {e.Message}"); }
        }
        bool ownTree = tree != null && (tree.CharacterIndex == c || tree.Id == "char_" + c);

        skillLabel.enabled = open;
        skillNone.enabled = open && !ownTree;
        int sumDone = 0, sumAll = 0;
        for (int i = 0; i < skillRows.Count; i++)
        {
            SkillRow row = skillRows[i];
            SkillBranchDef branch = ownTree && i < tree.Branches.Count ? tree.Branches[i] : null;
            row.Root.SetActive(branch != null);
            if (branch == null) continue;

            int done = 0, all = 0;
            foreach (SkillNodeDef node in branch.Nodes)
            {
                if (node.IsStart) continue;
                all++;
                if (Skills.IsUnlocked(node)) done++;
            }
            sumDone += done;
            sumAll += all;

            row.Icon.sprite = branch.Icon;
            row.Icon.enabled = branch.Icon != null;
            if (branch.Icon != null)
            {
                float w = branch.Icon.rect.width, h = branch.Icon.rect.height;
                OptionsKit.Move(row.Icon.rectTransform, Mathf.Round((12 - w) / 2f), Mathf.Round((12 - h) / 2f), w, h);
            }
            row.Fill.color = branch.Color;
            float frac = all > 0 ? done / (float)all : 0f;
            OptionsKit.Move(row.Fill.rectTransform, 17, 4, Mathf.Round(76f * frac), 5);
            row.Count.text = done + "/" + all;
            row.Count.color = all > 0 && done == all ? GameHudSkin.Gold : GameHudSkin.Parchment;
        }
        skillTotal.enabled = ownTree && sumAll > 0;
        skillTotal.text = sumDone + "/" + sumAll;

        // Buehne: Nummer und Schild
        int key = c - pageIndex * PerPage + 1;
        keyChip.enabled = stageKey.enabled = any && key >= 1 && key <= PerPage;
        stageKey.text = key.ToString();

        bool isChosen = any && c == chosen;
        bool showStatus = any && (isChosen || !open);
        statusChip.enabled = statusText.enabled = statusIcon.enabled = showStatus;
        stageLock.enabled = any && !open;
        if (showStatus)
        {
            string label = isChosen ? Loc.Get("ui.charselect.active", "AKTIV") : Loc.Get("ui.charselect.locked", "GESPERRT");
            statusText.text = label;
            statusText.color = isChosen ? GameHudSkin.Mint : GameHudSkin.StoneLight;
            statusIcon.sprite = isChosen ? GameHudSkin.Check : GameHudSkin.Lock;

            float iw = statusIcon.sprite.rect.width, ih = statusIcon.sprite.rect.height;
            float tw = Mathf.Ceil(OptionsKit.Measure(statusText, label));
            float w = 6f + iw + 3f + tw + 5f;
            if (((int)w & 1) == 1) w += 1f;
            float x = StageX + Mathf.Round((StageW - w) / 2f);
            float y = StageY + StageH - 19;
            OptionsKit.Move(statusChip.rectTransform, x, y, w, 14);
            OptionsKit.Move(statusIcon.rectTransform, x + 6, y + Mathf.Round((13 - ih) / 2f), iw, ih);
            OptionsKit.Move(statusText.rectTransform, x + 6 + iw + 3, y, tw + 2, 13);
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
        if (shadow != null)
        {
            shadow.font = font;
            shadow.fontSize = size;
            shadow.text = text;
        }
    }

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

    // ==================================================================
    //  Bewegtes (nach den Animatoren, darum LateUpdate)
    // ==================================================================

    void LateUpdate()
    {
        if (!IsOpen || page == null) return;

        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        UpdateSlots(now);
        UpdateStage(now, dt);
        UpdateSparks(now);
    }

    void UpdateSlots(float now)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            SlotView s = slots[i];
            if (s.Character < 0)
            {
                // Leerer Platz - auch nach dem Blaettern darf hier nichts nachleuchten.
                s.Glow.color = Color.clear;
                continue;
            }

            bool isCursor = s.Character == cursor;
            bool available = Characters.IsAvailable(s.Character);

            // Gemaltes Portraet (CharacterLooks.portrait): steht still, unter dem
            // Cursor huepft die Figur und der Strahlenkranz dreht sich. Gesperrte
            // Charaktere bleiben beim Schatten aus der Animation - vom Portraet
            // wuerde nur ein dunkles Quadrat uebrig.
            Sprite[] icon = available ? CharacterLooks.PortraitFor(s.Character) : null;
            if (icon != null)
            {
                int k = isCursor && animate ? Mathf.FloorToInt(now * PortraitFps) % icon.Length : 0;
                s.Portrait.enabled = true;
                s.Portrait.sprite = icon[k];
                s.Portrait.color = Color.white;
                s.Portrait.rectTransform.sizeDelta = new Vector2(icon[k].rect.width, icon[k].rect.height);
                s.Portrait.rectTransform.anchoredPosition = Vector2.zero;
            }

            // Der Keks unter dem Cursor trippelt auf der Stelle - er will los.
            Sprite frame = icon != null ? null : s.Puppet.Frame(isCursor && animate);
            if (icon == null) s.Portrait.enabled = frame != null;
            if (frame != null)
            {
                s.Portrait.sprite = frame;
                Vector2 size = FitSize(frame, PortraitTarget);
                s.Portrait.rectTransform.sizeDelta = size;
                // Etwas tiefer als mittig: der Kopf soll ganz ins Fenster, die Fuesse duerfen raus.
                s.Portrait.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Round(size.y * 6f / 64f));
                s.Portrait.color = Characters.IsAvailable(s.Character) ? Color.white : Silhouette;
            }

            float glow = 0f;
            if (isCursor) glow = animate ? 0.35f + 0.25f * Mathf.Sin(now * 4f) : 0.45f;
            else if (i == hoverSlot) glow = 0.18f;
            s.Glow.color = OptionsKit.WithAlpha(GameHudSkin.Gold, glow);

            // Der Haken am gewaehlten Keks springt beim Waehlen kurz doppelt so gross auf.
            bool pop = s.Character == Shop.SkinIndex && now - chosenAt < 0.14f;
            OptionsKit.Move(s.Check.rectTransform, pop ? Slot - 16 : Slot - 12, pop ? -5 : -2, pop ? 18 : 9, pop ? 16 : 8);
        }
    }

    void UpdateStage(float now, float dt)
    {
        bool any = Characters.Count > 0;
        int c = Mathf.Clamp(cursor, 0, Mathf.Max(0, Characters.Count - 1));

        stagePuppet.Set(any ? AnimatorFor(c) : null, 0f);
        Sprite frame = stagePuppet.Frame(false);

        // Licht blendet zur Farbe des neuen Charakters hinueber
        spotColor = Color.Lerp(spotColor, spotTarget, animate ? 1f - Mathf.Exp(-dt * 10f) : 1f);
        float flicker = animate ? 0.04f * Mathf.Sin(now * 1.7f) + 0.02f * Mathf.Sin(now * 5.3f) : 0f;
        stageSpot.color = OptionsKit.WithAlpha(spotColor, 0.55f + flicker);
        stagePool.color = OptionsKit.WithAlpha(spotColor, 0.7f + flicker);

        if (frame == null) stageChar.color = Color.clear;
        else
        {
            stageChar.sprite = frame;
            Vector2 size = FitSize(frame, StageTarget);

            // Fuesse auf die Platte: leere Zeilen unter den Fuessen stehen in CharacterLooks.
            float k = size.y / frame.rect.height;
            float y = FeetY - Mathf.Round((frame.rect.height - CharacterLooks.FootRowsFor(c)) * k);
            if (animate)
            {
                // Neuer Charakter faellt von oben auf die Platte und federt nach.
                float t = (now - cursorAt) / 0.28f;
                if (t < 1f) y -= Mathf.Round(Drop(t) * 16f);

                // Gewaehlt: ein Freudensprung.
                float h = (now - chosenAt) / 0.42f;
                if (h < 1f) y -= Mathf.Round(Mathf.Sin(h * Mathf.PI) * 18f);
            }

            OptionsKit.Move(stageChar.rectTransform, StageX + Mathf.Round((StageW - size.x) / 2f), y, size.x, size.y);
            stageChar.color = Characters.IsAvailable(c) ? Color.white : Silhouette;
        }

        // Kruemel steigen im Licht langsam auf
        for (int i = 0; i < crumbs.Count; i++)
        {
            Vector3 st = crumbState[i];
            float rise = animate ? now * (0.05f + 0.02f * (i % 3)) : 0f;
            float fy = Mathf.Repeat(st.y - rise, 1f);
            float half = Mathf.Lerp(0.14f, 0.5f, fy) * 120f;
            float fx = StageX + StageW / 2f + (st.x * 2f - 1f) * half * 0.8f
                     + (animate ? Mathf.Sin(now * 0.8f + st.z) * 2f : 0f);
            float py = StageY + 14 + fy * 140f;
            OptionsKit.Move(crumbs[i].rectTransform, fx, py, 1, 1);

            float a = 0.25f + 0.3f * Mathf.Sin(now * 2f + st.z);
            // Oben und unten ausblenden, damit sie nicht aufploppen
            a *= Mathf.Clamp01(fy * 6f) * Mathf.Clamp01((1f - fy) * 6f);
            crumbs[i].color = OptionsKit.WithAlpha(GameHudSkin.Cream, animate ? Mathf.Max(0f, a) : 0f);
        }
    }

    void UpdateSparks(float now)
    {
        float t = now - chosenAt;
        bool on = animate && t < 0.7f;
        Vector2 center = new Vector2(StageX + StageW / 2f, FeetY - 60f);

        for (int i = 0; i < sparks.Count; i++)
        {
            Spark s = sparks[i];
            if (!on) { s.Img.color = Color.clear; continue; }

            Vector2 p = center + s.Offset + s.Velocity * t + new Vector2(0f, 90f * t * t);
            float w = s.Img.sprite.rect.width, h = s.Img.sprite.rect.height;
            OptionsKit.Move(s.Img.rectTransform, p.x - Mathf.Round(w / 2f), p.y - Mathf.Round(h / 2f), w, h);
            s.Img.color = new Color(1f, 1f, 1f, 1f - t / 0.7f);
        }

        bool floatOn = animate && t < 0.9f;
        if (!floatOn) floater.alpha = floaterShadow.alpha = 0f;
        else
        {
            float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.3f;
            float y = StageY + 26 - Mathf.Round(t * 14f);
            OptionsKit.Move(floater.rectTransform, StageX, y, StageW, 22);
            OptionsKit.Move(floaterShadow.rectTransform, StageX, y + 1, StageW, 22);
            floater.alpha = floaterShadow.alpha = a;
        }
    }

    // ==================================================================
    //  Hilfen
    // ==================================================================

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

    /// <summary>Faellt von 1 auf 0 und federt einmal nach (0..1 -> Hoehe).</summary>
    static float Drop(float t)
    {
        if (t < 0.6f) { float u = 1f - t / 0.6f; return u * u; }
        float v = (t - 0.6f) / 0.4f;
        return Mathf.Sin(v * Mathf.PI) * 0.18f;
    }

    /// <summary>
    /// Groesse, in der ein Frame gezeichnet wird: das groesste ganze Vielfache
    /// (oder der groesste ganze Bruchteil) seiner Textur, das in das Ziel passt.
    /// </summary>
    static Vector2 FitSize(Sprite s, float target)
    {
        float tex = Mathf.Max(s.rect.width, s.rect.height);
        float k = tex <= target ? Mathf.Floor(target / tex) : 1f / Mathf.Ceil(tex / target);
        return new Vector2(Mathf.Round(s.rect.width * k), Mathf.Round(s.rect.height * k));
    }

    Color SpotColorFor(int index) => Color.Lerp(CharacterLooks.AccentFor(index), GameHudSkin.Cream, 0.45f);

    /// <summary>Animator des Charakters: erst aus <see cref="CharacterLooks"/>, sonst vom Hub-Spieler.</summary>
    static RuntimeAnimatorController AnimatorFor(int index)
    {
        RuntimeAnimatorController c = CharacterLooks.AnimatorFor(index);
        if (c != null) return c;

        WM_PlayerSkinSwitcher hub = WM_PlayerSkinSwitcher.Instance;
        if (hub == null) return null;
        switch (index)
        {
            case 0: return hub.NormalSkinOverride;
            default: return null;
        }
    }

    void PlaySfx(AudioClip clip, bool clickIfMissing)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, sfxVolume);
        else if (clickIfMissing) OptionsKit.PlayClick();
    }
}
