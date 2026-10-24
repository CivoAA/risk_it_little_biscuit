using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Koop-Lobby, im Stil von "UI 2.0" (<see cref="OptionsKit"/>). Sie deckt
/// den ganzen Bildschirm mit einer eigenen Kulisse ab - vom Hub, der dahinter
/// geladen bleibt (Skilltree, Werkbank, Shop, Levelauswahl leben dort), sieht
/// man nichts.
///
/// Zwei Seiten:
///
///   START    noch keine Sitzung: LOBBY ERSTELLEN, Code eingeben + BEITRETEN,
///            ZURUECK (ins Hauptmenue). Darunter laufen alle Charaktere vorbei.
///            Auch Fehler ("Lobby nicht gefunden") landen hier.
///
///   LOBBY    oben vier kleine Buehnen, eine je Platz: der gewaehlte Charakter
///            steht im Licht seiner Farbe (echte Lauf-Animation, bereit =
///            trippelt), darunter Name und Charakter. Dann die Keksdose mit
///            allen Charakteren - jeder nur einmal, vergebene tragen den Namen
///            des Spielers. Unten links SKILLTREE / WERKBANK / SHOP und
///            VERLASSEN, in der Mitte Welt, Modus und Schwierigkeit (stellt der
///            Host ein), rechts der grosse BEREIT-Knopf. Code und Status oben.
///
/// Jeder Text hat eine feste Breite und wird notfalls mit "..." gekuerzt -
/// lange Namen ("ZWIEBELRITTER") laufen nie ueber einen Rahmen.
///
/// Sind alle bereit, zaehlt der Host drei Sekunden runter (gross in der Mitte)
/// und der Lauf startet bei allen (<see cref="NetLobbyHub"/> uebernimmt). Wer
/// zum Skilltree, zur Werkbank oder in den Shop geht, ist danach nicht mehr
/// bereit - sonst startet der Lauf, waehrend er noch Punkte verteilt.
///
/// Die Lobby-Daten kommen aus <see cref="NetSession"/>; das Fenster zeichnet
/// nur und schickt Wuensche. Gebaut wird beim Oeffnen und nach jedem
/// Sprachwechsel, aktualisiert jeden Frame (Countdown, Hover, Animation).
/// </summary>
public class NetLobbyPanel : MonoBehaviour
{
    // ==================================================================
    //  Masse (Seitenpixel, Ursprung oben links)
    // ==================================================================

    private static readonly RectInt Content = new RectInt(0, 0, OptionsKit.RefW, OptionsKit.RefH);

    private const int RibbonY = 2, TopY = 6;

    // Buehnen der vier Plaetze
    private const int CardY = 30, CardW = 108, CardH = 120, CardGap = 4;
    private const int CardX = (OptionsKit.RefW - (4 * CardW + 3 * CardGap)) / 2;
    private const int StageX = 4, StageY = 4, StageW = 100, StageH = 88, StageFloor = 16;
    private const int FeetY = StageY + StageH - StageFloor + 1;
    private const int CharTarget = 64;
    private const int NameY = 94, CharNameY = 106;

    // Keksdose
    private const int SectionY = 153, TileY = 165, TileSize = 48, TileGap = 2, InfoY = 214;
    private const int MaxTiles = 9;
    private const int Win = 42;

    // Fuss
    private const int RowA = 232, RowB = 251, BtnH = 16;
    private const int LeftX = 18, MidX = 186, ReadyX = 370, ReadyW = 92;

    // Start
    private const int StartX = 120, StartY = 52, StartW = 240, StartH = 150;
    private const int ParadeFeet = 260, ParadeChar = 34;

    // Rueckfrage
    private const int AskX = 150, AskY = 104, AskW = 180, AskH = 74;

    private const float PortraitFps = 8f;

    private static readonly Color32 SkyTop = GameHudSkin.Hex(0x1a1020), SkyBottom = GameHudSkin.Hex(0x3d1f35);

    // ==================================================================

    private static NetLobbyPanel instance;
    public static bool IsOpen => instance != null;

    private enum PageKind { Start, Lobby }

    private class SlotView
    {
        public RectTransform Root;
        public Image Frame, Stage, Spot, Pool, Char, Question, Crown, Pill;
        public TextMeshProUGUI Name, CharName, Level, Ping, PillText;
        public SkinButton Invite, Kick;
        public int ShownCharacter = -2;
        public float ChangedAt = -9f, ReadyAt = -9f;
        public bool WasReady;
        public Color SpotColor = Color.clear;
    }

    private class TileView
    {
        public RectTransform Root;
        public Image Frame, Back, Portrait, Shade, Lock, OwnerChip;
        public TextMeshProUGUI Owner;
        public Rect Area;
    }

    /// <summary>
    /// Bildquelle fuer einen Charakter: Animator + unsichtbarer SpriteRenderer,
    /// gelesen wird nur das aktuelle Bild (wie in der Charakterauswahl im Hub).
    /// </summary>
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
            p.Source.enabled = false;
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

    private struct Sprinkle
    {
        public Image Img;
        public float X, Y, Speed, Sway, Phase;
    }

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page, backRoot;
    private CanvasScaler scaler;
    private Canvas canvas;
    private Vector2Int lastScreen;
    private PageKind kind;
    private HubUI hub;
    private int openedFrame;

    // Kulisse
    private RawImage pattern;
    private Image beamL, beamR;
    private readonly List<Sprinkle> sprinkles = new List<Sprinkle>();
    private static Texture2D skyTex, patternTex, vignetteTex;

    // Start
    private TMP_InputField codeField;
    private TextMeshProUGUI startStatus;
    private SkinButton createBtn, joinBtn, startBackBtn;
    private string startMessage = "";
    private bool startError;
    private readonly List<ParadeView> parade = new List<ParadeView>();
    private GameObject paradeRoot;

    private sealed class ParadeView
    {
        public Puppet Puppet;
        public Image Char, Shadow, Pool, Spot;
        public Color Accent;
        public float X;
    }

    // Lobby
    private readonly List<SlotView> slots = new List<SlotView>();
    private readonly List<TileView> tiles = new List<TileView>();
    private readonly Puppet[] puppets = new Puppet[NetSession.MaxPlayers];
    private GameObject puppetRoot;
    private TextMeshProUGUI infoText, statusText, codeText, worldValue, diffValue, sectionRight;
    private readonly Image[] pips = new Image[NetSession.MaxPlayers];
    private SkinButton readyBtn, leaveBtn, copyBtn, inviteBtn, storyBtn, endlessBtn;
    private SkinButton worldPrev, worldNext, diffPrev, diffNext;
    private RectTransform countRoot;
    private TextMeshProUGUI countNumber, countLabel;
    private int countShown = -1;
    private float countAt;
    private float copiedUntil;
    private int hoverTile = -1;
    private int cursorTile;
    private int perPage = 1, tilePage;
    private SkinButton pagePrev, pageNext;
    private RectTransform ask;
    private bool hiddenForStation;

    // ==================================================================
    //  Oeffnen / Schliessen
    // ==================================================================

    public static void Open()
    {
        if (instance != null) return;
        var go = new GameObject("NetLobbyPanel");
        instance = go.AddComponent<NetLobbyPanel>();
    }

    public static void Close()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    /// <summary>Fehler oder Hinweis auf der Startseite zeigen (nach Sitzungsende).</summary>
    public static void ShowMessage(string text, bool error)
    {
        if (instance == null) return;
        instance.startMessage = text;
        instance.startError = error;
        if (instance.startStatus != null) instance.RefreshStart();
    }

    private void Awake()
    {
        instance = this;
        openedFrame = Time.frameCount;
        textFont = PixelUI.FindTextFont();
        pixelFont = PixelUI.FindPixelFont();
        kind = NetSession.Active ? PageKind.Lobby : PageKind.Start;

        puppetRoot = new GameObject("NetLobbyPuppets");
        for (int i = 0; i < puppets.Length; i++) puppets[i] = Puppet.Create(puppetRoot.transform, "Puppet" + i);

        Build();
        BlockHub(true);
    }

    private void OnEnable()
    {
        Loc.LanguageChanged += Build;
        NetSession.Changed += OnSessionChanged;
        NetSession.Ended += OnSessionEnded;
    }

    private void OnDisable()
    {
        Loc.LanguageChanged -= Build;
        NetSession.Changed -= OnSessionChanged;
        NetSession.Ended -= OnSessionEnded;
    }

    private void OnDestroy()
    {
        BlockHub(false);
        if (puppetRoot != null) Destroy(puppetRoot);
        if (paradeRoot != null) Destroy(paradeRoot);
        if (instance == this) instance = null;
    }

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

    private void OnSessionChanged()
    {
        if (kind != PageKind.Lobby && NetSession.Active)
        {
            kind = PageKind.Lobby;
            Build();
            return;
        }
        if (kind == PageKind.Lobby) RefreshLobby();
    }

    private void OnSessionEnded(string reasonKey)
    {
        kind = PageKind.Start;
        bool error = reasonKey != "net.end.left";
        startMessage = error ? NetText.Reason(reasonKey) : "";
        startError = error;
        Build();
    }

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Build()
    {
        OptionsKit.Clear(transform);
        slots.Clear();
        tiles.Clear();
        pagePrev = pageNext = null;
        parade.Clear();
        if (paradeRoot != null) Destroy(paradeRoot);
        sprinkles.Clear();
        ask = null;
        codeField = null;
        countRoot = null;
        countShown = -1;

        // Unter Shop (130), Werkbank (155) und Optionen (210) - die gehen ueber der Lobby auf.
        // Ganz deckend: vom Hub dahinter soll nichts durchscheinen.
        page = OptionsKit.CreatePage(gameObject, 120, 1f, out scaler);
        canvas = GetComponent<Canvas>();
        BuildBackdrop();

        if (kind == PageKind.Start) BuildStart();
        else BuildLobby();

        OptionsKit.Layout(page, scaler, Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // ---------- Kulisse ----------

    /// <summary>
    /// Nachthimmel-Verlauf ueber den ganzen Schirm, darauf ein langsam
    /// wanderndes Streuselmuster, zwei Lichtkegel von oben, fallende Streusel
    /// und eine Vignette. Liegt zwischen Abdunklung und Seite.
    /// </summary>
    private void BuildBackdrop()
    {
        backRoot = OptionsKit.Rect("Backdrop", transform, 0, 0, 1, 1);
        backRoot.anchorMin = Vector2.zero;
        backRoot.anchorMax = Vector2.one;
        backRoot.offsetMin = backRoot.offsetMax = Vector2.zero;
        backRoot.SetSiblingIndex(1);

        StretchRaw("Sky", backRoot, SkyTexture());
        pattern = StretchRaw("Pattern", backRoot, PatternTexture());

        Sprite beam = GameHudSkin.Spotlight(120, 230);
        beamL = OptionsKit.Img("BeamL", backRoot, 0, 0, 120, 230, beam, OptionsKit.WithAlpha(GameHudSkin.Gold, 0.10f));
        beamR = OptionsKit.Img("BeamR", backRoot, 0, 0, 120, 230, beam, OptionsKit.WithAlpha(GameHudSkin.JamLight, 0.08f));

        Color32[] colors = { GameHudSkin.Jam, GameHudSkin.Gold, GameHudSkin.Icing, GameHudSkin.Mint, GameHudSkin.Grape, GameHudSkin.Cream };
        var rng = new System.Random(7);
        for (int i = 0; i < 40; i++)
        {
            bool tall = rng.Next(2) == 0;
            Image img = OptionsKit.Img("Sprinkle" + i, backRoot, 0, 0, tall ? 1 : 2, tall ? 2 : 1, GameHudSkin.White,
                                       OptionsKit.WithAlpha(colors[rng.Next(colors.Length)], 0.35f + 0.3f * (float)rng.NextDouble()));
            sprinkles.Add(new Sprinkle
            {
                Img = img,
                X = (float)rng.NextDouble(),
                Y = (float)rng.NextDouble(),
                Speed = 5f + 10f * (float)rng.NextDouble(),
                Sway = 2f + 5f * (float)rng.NextDouble(),
                Phase = (float)rng.NextDouble() * 6.28f,
            });
        }

        StretchRaw("Vignette", backRoot, VignetteTexture());
    }

    private static RawImage StretchRaw(string name, Transform parent, Texture tex)
    {
        RectTransform rt = OptionsKit.Rect(name, parent, 0, 0, 1, 1);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private void AnimateBackdrop(float now)
    {
        if (backRoot == null) return;
        float s = Mathf.Max(1f, scaler.scaleFactor);
        float w = Screen.width / s, h = Screen.height / s;

        // Muster: 24er-Kachel, wandert schraeg nach unten links, auf ganze Pixel.
        if (pattern != null)
        {
            float off = Mathf.Floor(now * 6f) / 24f;
            pattern.uvRect = new Rect(off, off * 0.5f, w / 24f, h / 24f);
        }

        // Lichtkegel schwenken sacht hin und her (ganze Pixel).
        float swayL = Mathf.Round(Mathf.Sin(now * 0.35f) * 18f);
        float swayR = Mathf.Round(Mathf.Sin(now * 0.29f + 2f) * 18f);
        beamL.rectTransform.anchoredPosition = new Vector2(Mathf.Round(w * 0.22f - 60f + swayL), 0f);
        beamR.rectTransform.anchoredPosition = new Vector2(Mathf.Round(w * 0.78f - 60f + swayR), 0f);

        for (int i = 0; i < sprinkles.Count; i++)
        {
            Sprinkle p = sprinkles[i];
            float y = Mathf.Repeat(p.Y * h + now * p.Speed, h + 4f) - 2f;
            float x = p.X * w + Mathf.Sin(now * 0.8f + p.Phase) * p.Sway;
            p.Img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        }
    }

    private static Texture2D SkyTexture()
    {
        if (skyTex != null) return skyTex;
        const int h = 64;
        skyTex = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "LobbySky",
        };
        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            // y = 0 unten: unten warm, oben Nacht.
            float t = y / (float)(h - 1);
            px[y] = Color32.Lerp(SkyBottom, SkyTop, t * t * (3f - 2f * t));
        }
        skyTex.SetPixels32(px);
        skyTex.Apply(false, true);
        return skyTex;
    }

    private static Texture2D PatternTexture()
    {
        if (patternTex != null) return patternTex;
        const int s = 24;
        patternTex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            name = "LobbyPattern",
        };
        var px = new Color32[s * s];
        var dot = new Color32(255, 244, 224, 16);
        var bright = new Color32(255, 244, 224, 26);
        void Diamond(int cx, int cy, Color32 c)
        {
            px[cy * s + cx] = c;
            px[cy * s + cx - 1] = c;
            px[cy * s + cx + 1] = c;
            px[(cy - 1) * s + cx] = c;
            px[(cy + 1) * s + cx] = c;
        }
        Diamond(5, 5, bright);
        Diamond(17, 17, dot);
        px[17 * s + 5] = dot;
        px[5 * s + 17] = dot;
        patternTex.SetPixels32(px);
        patternTex.Apply(false, true);
        return patternTex;
    }

    private static Texture2D VignetteTexture()
    {
        if (vignetteTex != null) return vignetteTex;
        const int w = 64, h = 36;
        vignetteTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "LobbyVignette",
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                float a = Mathf.Clamp01((d - 0.55f) / 0.75f);
                px[y * w + x] = new Color32(8, 4, 8, (byte)(a * a * 200f));
            }
        }
        vignetteTex.SetPixels32(px);
        vignetteTex.Apply(false, true);
        return vignetteTex;
    }

    // ---------- Startseite ----------

    private void BuildStart()
    {
        OptionsKit.Img("Card", page, StartX, StartY, StartW, StartH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, StartX + StartW / 2f, StartY - 12, NetText.Get("net.title", "KOOP"), pixelFont, textFont);

        TextMeshProUGUI intro = OptionsKit.Label("Intro", page, StartX + 14, StartY + 16, StartW - 28, 26,
            NetText.Get("net.start.intro", "Spielt online zu zweit, zu dritt oder zu viert."),
            textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        intro.textWrappingMode = TextWrappingModes.Normal;

        createBtn = SkinButton.Create(page, StartX + 30, StartY + 44, StartW - 60, 18,
            NetText.Get("net.start.create", "LOBBY ERSTELLEN"), textFont, SkinButton.Kind.Primary, CreateLobby);

        TextMeshProUGUI or = OptionsKit.Label("Or", page, StartX + 10, StartY + 68, StartW - 20, 14,
            NetText.Get("net.start.or", "ODER MIT CODE BEITRETEN"), textFont, OptionsKit.SizeText,
            GameHudSkin.StoneLight, TextAlignmentOptions.Center);
        or.overflowMode = TextOverflowModes.Ellipsis;

        codeField = OptionsKit.Input(page, StartX + 30, StartY + 84, 116, 18, textFont,
            NetText.Get("net.start.code_hint", "CODE"), false, 32);
        codeField.onSubmit.AddListener(_ => JoinLobby());

        joinBtn = SkinButton.Create(page, StartX + 150, StartY + 84, 60, 18,
            NetText.Get("net.start.join", "BEITRETEN"), textFont, SkinButton.Kind.Wood, JoinLobby);

        startStatus = OptionsKit.Label("Status", page, StartX + 10, StartY + 106, StartW - 20, 14, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        startStatus.textWrappingMode = TextWrappingModes.Normal;

        startBackBtn = SkinButton.Create(page, StartX + (StartW - 80) / 2, StartY + StartH - 24, 80, 18,
            NetText.Get("net.start.back", "ZURÜCK"), textFont, SkinButton.Kind.Wood, BackFromStart);

        // Unter der Karte: alle Charaktere stehen frei auf der Rampe und tanzen eine Welle.
        int count = Characters.Count;
        paradeRoot = new GameObject("NetLobbyParade");
        int step = Mathf.Min(44, (OptionsKit.RefW - 40) / Mathf.Max(1, count));
        float left = (OptionsKit.RefW - (count - 1) * step) / 2f;
        for (int c = 0; c < count; c++)
        {
            float x = Mathf.Round(left + c * step);
            var v = new ParadeView { X = x, Accent = Color.Lerp(CharacterLooks.AccentFor(c), GameHudSkin.Cream, 0.35f) };
            v.Spot = OptionsKit.Img("ParadeSpot" + c, page, x - 20, ParadeFeet - 58, 40, 58,
                                    GameHudSkin.Spotlight(40, 58), Color.clear);
            v.Pool = OptionsKit.Img("ParadePool" + c, page, x - 20, ParadeFeet - 5, 40, 9,
                                    GameHudSkin.LightPool(40, 9), Color.clear);
            v.Shadow = OptionsKit.Img("ParadeShadow" + c, page, x - 11, ParadeFeet - 3, 22, 5,
                                      GameHudSkin.LightPool(22, 5), new Color(0.05f, 0.02f, 0.06f, 0.9f));
            v.Char = OptionsKit.Img("Parade" + c, page, x, ParadeFeet, 1, 1, null);
            v.Puppet = Puppet.Create(paradeRoot.transform, "ParadePuppet" + c);
            v.Puppet.Set(CharacterLooks.AnimatorFor(c), (c * 0.29f) % 1f);
            parade.Add(v);
        }

        RefreshStart();
    }

    private void AnimateParade(float now)
    {
        const float Period = 3.6f;
        for (int c = 0; c < parade.Count; c++)
        {
            ParadeView v = parade[c];
            bool open = Characters.IsAvailable(c);

            // Die Welle laeuft einmal durch die Reihe, dann kurze Pause.
            float t = ((now - c * 0.16f) % Period + Period) % Period;
            float hop = t < 0.5f ? Mathf.Sin(t / 0.5f * Mathf.PI) : 0f;
            bool dancing = open && t < 0.9f;

            Sprite frame = v.Puppet.Frame(dancing);
            Sprite[] icon = null;
            if (frame == null)
            {
                icon = CharacterLooks.PortraitFor(c);
                frame = icon != null ? icon[open ? Mathf.FloorToInt(now * PortraitFps) % icon.Length : 0] : null;
            }
            if (frame == null) { v.Char.enabled = false; continue; }

            v.Char.enabled = true;
            v.Char.sprite = frame;
            // Gesperrte Charaktere bleiben dunkle Schatten im Hintergrund.
            v.Char.color = open ? Color.white : new Color(0.13f, 0.09f, 0.16f, 0.95f);

            Vector2 size;
            float x, y;
            if (icon != null)
            {
                size = new Vector2(frame.rect.width, frame.rect.height);
                x = v.X - Mathf.Round(size.x / 2f);
                y = ParadeFeet - size.y;
            }
            else
            {
                size = FitSize(frame, ParadeChar);
                float k = size.y / frame.rect.height;
                Rect body = CharacterLooks.BodyRect(frame);
                y = ParadeFeet - Mathf.Round((frame.rect.height - body.y - CharacterLooks.FootRowsFor(c)) * k);
                x = v.X - Mathf.Round(body.width * k / 2f) - Mathf.Round(body.x * k);
            }
            int lift = open ? Mathf.RoundToInt(hop * 9f) : 0;
            OptionsKit.Move(v.Char.rectTransform, x, y - lift, size.x, size.y);

            // Schatten wird kleiner, je hoeher der Sprung; Licht flackert beim Tanzen auf.
            int sw = Mathf.RoundToInt(22f - lift);
            OptionsKit.Move(v.Shadow.rectTransform, v.X - Mathf.Round(sw / 2f), ParadeFeet - 3, sw, 5);
            float glow = open ? 0.3f + 0.45f * hop + 0.05f * Mathf.Sin(now * 2f + c) : 0.06f;
            v.Spot.color = OptionsKit.WithAlpha(open ? v.Accent : (Color)GameHudSkin.StoneLight, glow * 0.8f);
            v.Pool.color = OptionsKit.WithAlpha(open ? v.Accent : (Color)GameHudSkin.StoneLight, glow + 0.15f);
        }
    }

    private void RefreshStart()
    {
        bool connecting = NetSession.Instance != null && NetSession.Instance.Role != NetRole.None && !NetSession.Instance.Joined;
        if (createBtn != null) createBtn.Disabled = connecting;
        if (joinBtn != null) joinBtn.Disabled = connecting;
        if (startBackBtn != null)
            startBackBtn.SetText(connecting ? NetText.Get("net.start.cancel", "ABBRECHEN")
                                            : NetText.Get("net.start.back", "ZURÜCK"));

        if (startStatus == null) return;
        if (connecting)
        {
            int dots = Mathf.FloorToInt(Time.unscaledTime * 3f) % 4;
            startStatus.text = NetText.Get("net.start.connecting", "VERBINDE ...").TrimEnd('.', ' ') + " " + new string('.', dots);
            startStatus.color = GameHudSkin.Gold;
        }
        else if (string.IsNullOrEmpty(startMessage) && !NetSession.SteamWanted)
        {
            // Steam laeuft nicht (oder -nettcp): dann geht es nur per IP - das soll man sehen.
            startStatus.text = NetText.Get("net.start.no_steam", "Steam nicht verbunden - nur Direktverbindung per IP.");
            startStatus.color = GameHudSkin.Gold;
        }
        else
        {
            startStatus.text = startMessage;
            startStatus.color = startError ? (Color)GameHudSkin.JamLight : GameHudSkin.Parchment;
        }
    }

    private void CreateLobby()
    {
        if (NetSession.Active) return;
        startMessage = "";
        NetSession.Host();
        kind = PageKind.Lobby;
        Build();
    }

    private void JoinLobby()
    {
        if (NetSession.Active || codeField == null) return;
        string code = codeField.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            startMessage = NetText.Get("net.start.need_code", "Bitte erst einen Code eingeben.");
            startError = true;
            RefreshStart();
            return;
        }
        startMessage = "";
        NetSession.Join(code);
        RefreshStart();
    }

    private void BackFromStart()
    {
        if (NetSession.Instance != null && NetSession.Instance.Role != NetRole.None)
        {
            NetSession.Leave();
            return;
        }
        Close();
        NetLobbyHub.BackToMainMenu();
    }

    // ---------- Lobby ----------

    private void BuildLobby()
    {
        OptionsKit.Ribbon(page, OptionsKit.RefW / 2f, RibbonY, NetText.Get("net.title", "KOOP"), pixelFont, textFont);

        // Oben links: Code zum Weitergeben, oben rechts: Stand der Gruppe.
        codeText = OptionsKit.Label("Code", page, LeftX, TopY + 1, 112, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
        codeText.overflowMode = TextOverflowModes.Ellipsis;
        copyBtn = SkinButton.Create(page, LeftX + 114, TopY, 52, BtnH, NetText.Get("net.lobby.copy", "KOPIEREN"),
            textFont, SkinButton.Kind.Wood, CopyCode);

        statusText = OptionsKit.Label("Status", page, 286, TopY + 1, 156, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Right);
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        for (int i = 0; i < pips.Length; i++)
            pips[i] = OptionsKit.Img("Pip" + i, page, 446 + i * 4, TopY + 6, 3, 3, GameHudSkin.Disc(3), GameHudSkin.StoneDark);

        for (int i = 0; i < NetSession.MaxPlayers; i++) slots.Add(BuildSlot(i));

        OptionsKit.ShadowLabel("Section", page, LeftX, SectionY, 200, 14,
            NetText.Get("net.lobby.pick", "CHARAKTER WÄHLEN"), textFont, OptionsKit.SizeText,
            GameHudSkin.Gold, TextAlignmentOptions.Left);
        sectionRight = OptionsKit.Label("SectionRight", page, 262, SectionY, 200, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Right);

        // Passen nicht alle in eine Reihe, wird geblaettert (Pfeile links und rechts).
        int count = Characters.Count;
        perPage = count <= MaxTiles ? count : MaxTiles - 1;
        for (int i = 0; i < count; i++)
            tiles.Add(BuildTile(i, new Rect(0, TileY, TileSize, TileSize)));
        if (count > perPage)
        {
            int rowW = perPage * (TileSize + TileGap) - TileGap;
            int rx = (OptionsKit.RefW - rowW) / 2;
            pagePrev = SkinButton.Create(page, rx - 20, TileY + (TileSize - 18) / 2, 16, 18, "", textFont,
                SkinButton.Kind.Wood, () => StepPage(-1), GameHudSkin.ArrowLeft);
            pageNext = SkinButton.Create(page, rx + rowW + 4, TileY + (TileSize - 18) / 2, 16, 18, "", textFont,
                SkinButton.Kind.Wood, () => StepPage(1), GameHudSkin.Arrow);
        }

        infoText = OptionsKit.Label("Info", page, LeftX, InfoY, OptionsKit.RefW - 2 * LeftX, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        infoText.overflowMode = TextOverflowModes.Ellipsis;

        // Trennlinien des Fusses
        OptionsKit.Img("RuleH", page, LeftX, RowA - 4, OptionsKit.RefW - 2 * LeftX, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Cream, 0.12f));
        OptionsKit.Img("RuleV1", page, MidX - 5, RowA, 1, 35, GameHudSkin.White, OptionsKit.WithAlpha(GameHudSkin.Cream, 0.1f));
        OptionsKit.Img("RuleV2", page, ReadyX - 5, RowA, 1, 35, GameHudSkin.White, OptionsKit.WithAlpha(GameHudSkin.Cream, 0.1f));

        // Links: vorbereiten + verlassen
        SkinButton.Create(page, LeftX, RowA, 56, BtnH, NetText.Get("net.lobby.skilltree", "SKILLTREE"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Skilltree));
        SkinButton.Create(page, LeftX + 58, RowA, 58, BtnH, NetText.Get("net.lobby.workbench", "WERKBANK"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Workbench));
        SkinButton.Create(page, LeftX + 118, RowA, 44, BtnH, NetText.Get("net.lobby.shop", "SHOP"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Shop));
        leaveBtn = SkinButton.Create(page, LeftX, RowB, 76, BtnH, NetText.Get("net.lobby.leave", "VERLASSEN"),
            textFont, SkinButton.Kind.Danger, AskLeave);
        inviteBtn = SkinButton.Create(page, LeftX + 80, RowB, 82, BtnH, NetText.Get("net.lobby.invite", "EINLADEN"),
            textFont, SkinButton.Kind.Wood, Invite);

        // Mitte: Einstellungen des Hosts
        OptionsKit.Label("WorldLabel", page, MidX, RowA + 1, 30, 14, NetText.Get("net.lobby.world", "WELT"),
            textFont, OptionsKit.SizeText, GameHudSkin.StoneLight, TextAlignmentOptions.Left);
        worldPrev = SkinButton.Create(page, MidX + 30, RowA, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepWorld(-1), GameHudSkin.ArrowLeft);
        worldValue = OptionsKit.Label("World", page, MidX + 48, RowA + 1, 108, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        worldValue.overflowMode = TextOverflowModes.Ellipsis;
        worldNext = SkinButton.Create(page, MidX + 158, RowA, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepWorld(1), GameHudSkin.Arrow);

        storyBtn = SkinButton.Create(page, MidX, RowB, 44, BtnH, NetText.Get("net.lobby.story", "STORY"),
            textFont, SkinButton.Kind.Wood, () => SetEndless(false));
        endlessBtn = SkinButton.Create(page, MidX + 46, RowB, 50, BtnH, NetText.Get("net.lobby.endless", "ENDLESS"),
            textFont, SkinButton.Kind.Wood, () => SetEndless(true));
        diffPrev = SkinButton.Create(page, MidX + 100, RowB, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepDifficulty(-1), GameHudSkin.ArrowLeft);
        diffValue = OptionsKit.Label("Diff", page, MidX + 116, RowB + 1, 42, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        diffValue.overflowMode = TextOverflowModes.Ellipsis;
        diffNext = SkinButton.Create(page, MidX + 158, RowB, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepDifficulty(1), GameHudSkin.Arrow);

        // Rechts: der grosse Knopf
        readyBtn = SkinButton.Create(page, ReadyX, RowA, ReadyW, RowB + BtnH - RowA,
            NetText.Get("net.lobby.ready", "BEREIT"), textFont, SkinButton.Kind.Primary, ToggleReady);

        BuildCountdown();

        cursorTile = Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, Characters.Count - 1));
        tilePage = cursorTile / Mathf.Max(1, perPage);
        RefreshLobby();
    }

    private SlotView BuildSlot(int i)
    {
        int x = CardX + i * (CardW + CardGap);
        var v = new SlotView();
        v.Root = OptionsKit.Rect("Slot" + i, page, x, CardY, CardW, CardH);

        OptionsKit.Img("Back", v.Root, 2, 2, CardW - 4, CardH - 4, GameHudSkin.White, GameHudSkin.CardDeep);
        v.Stage = OptionsKit.Img("Stage", v.Root, StageX, StageY, StageW, StageH, GameHudSkin.Stage(StageW, StageH, StageFloor));
        v.Spot = OptionsKit.Img("Spot", v.Root, StageX + 20, StageY + 6, 60, StageH - StageFloor - 4,
                                GameHudSkin.Spotlight(60, StageH - StageFloor - 4), Color.clear);
        v.Pool = OptionsKit.Img("Pool", v.Root, StageX + 18, FeetY - 6, 64, 10, GameHudSkin.LightPool(64, 10), Color.clear);

        v.Char = OptionsKit.Img("Char", v.Root, 0, 0, 1, 1, null);
        v.Char.enabled = false;
        Sprite q = GameHudSkin.Question;
        v.Question = OptionsKit.Img("Question", v.Root, 0, 0, q.rect.width * 3, q.rect.height * 3, q,
                                    OptionsKit.WithAlpha(GameHudSkin.StoneLight, 0.8f));

        OptionsKit.Img("Valance", v.Root, StageX + 1, StageY + 1, StageW - 2, 9, GameHudSkin.Valance(StageW - 2));

        v.Level = OptionsKit.Label("Level", v.Root, StageX + 5, StageY + 11, 40, 12, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.GoldLight, TextAlignmentOptions.Left);
        v.Ping = OptionsKit.Label("Ping", v.Root, StageX + StageW - 45, StageY + 11, 40, 12, "", textFont, OptionsKit.SizeText,
                                  GameHudSkin.StoneLight, TextAlignmentOptions.Right);

        v.Pill = OptionsKit.Img("Pill", v.Root, StageX + 18, StageY + StageH - StageFloor + 2, StageW - 36, 12,
                                GameHudSkin.LevelChip, true);
        v.PillText = OptionsKit.Label("PillText", v.Root, StageX + 18, StageY + StageH - StageFloor + 1, StageW - 36, 12, "",
                                      textFont, OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        v.PillText.overflowMode = TextOverflowModes.Ellipsis;

        v.Frame = OptionsKit.Img("Frame", v.Root, 0, 0, CardW, CardH, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Stone), true);

        v.Name = OptionsKit.Label("Name", v.Root, 6, NameY, CardW - 12, 13, "", textFont, OptionsKit.SizeText,
                                  GameHudSkin.Cream, TextAlignmentOptions.Center);
        v.Name.overflowMode = TextOverflowModes.Ellipsis;
        v.CharName = OptionsKit.Label("CharName", v.Root, 6, CharNameY, CardW - 12, 12, "", textFont, OptionsKit.SizeText,
                                      GameHudSkin.StoneLight, TextAlignmentOptions.Center);
        v.CharName.overflowMode = TextOverflowModes.Ellipsis;

        Sprite crown = GameHudSkin.Crown;
        v.Crown = OptionsKit.Img("Crown", v.Root, 4, -4, crown.rect.width, crown.rect.height, crown);

        if (NetSession.IsHost && i > 0)
        {
            v.Invite = SkinButton.Create(v.Root, (CardW - 76) / 2, NameY + 2, 76, BtnH,
                NetText.Get("net.lobby.invite", "EINLADEN"), textFont, SkinButton.Kind.Wood, Invite);
            int slot = i;
            v.Kick = SkinButton.Create(v.Root, CardW - 16, 4, 12, 12, "x", textFont, SkinButton.Kind.Danger,
                () => NetSession.Instance?.Kick(slot));
        }
        return v;
    }

    private TileView BuildTile(int character, Rect a)
    {
        var t = new TileView { Area = a };
        t.Root = OptionsKit.Rect("Tile" + character, page, a.x, a.y, a.width, a.height);
        RectTransform rt = t.Root;

        int inset = (TileSize - Win) / 2;
        t.Back = OptionsKit.Img("Back", rt, inset, inset, Win, Win, GameHudSkin.SlotBack(Win, Win));
        t.Back.color = CharacterLooks.AccentFor(character);

        RectTransform window = OptionsKit.Rect("Window", rt, inset, inset, Win, Win);
        window.gameObject.AddComponent<RectMask2D>();
        t.Portrait = OptionsKit.Img("Portrait", window, 0, 0, Win, Win, null);

        t.Shade = OptionsKit.Img("Shade", rt, inset, inset, Win, Win, GameHudSkin.White, new Color(0.08f, 0.05f, 0.07f, 0.6f));
        t.Frame = OptionsKit.Img("Frame", rt, 0, 0, a.width, a.height, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Wood), true);

        Sprite lockSprite = GameHudSkin.Lock;
        t.Lock = OptionsKit.Img("Lock", rt, (TileSize - lockSprite.rect.width) / 2, (TileSize - lockSprite.rect.height) / 2,
                                lockSprite.rect.width, lockSprite.rect.height, lockSprite);

        t.OwnerChip = OptionsKit.Img("OwnerChip", rt, 2, TileSize - 14, TileSize - 4, 12, GameHudSkin.LevelChip, true);
        t.Owner = OptionsKit.Label("Owner", rt, 3, TileSize - 15, TileSize - 6, 12, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.Cream, TextAlignmentOptions.Center);
        t.Owner.overflowMode = TextOverflowModes.Ellipsis;

        // Klick und Hover auf das ganze Fenster.
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        PointerRelay relay = rt.gameObject.AddComponent<PointerRelay>();
        int c = character;
        relay.Enter = () => hoverTile = c;
        relay.Exit = () => { if (hoverTile == c) hoverTile = -1; };
        relay.Down = () => Pick(c);
        return t;
    }

    /// <summary>Grosser Countdown in der Mitte: dunkles Band, "START IN", die Zahl springt auf.</summary>
    private void BuildCountdown()
    {
        countRoot = OptionsKit.Rect("Countdown", page, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        OptionsKit.Img("Band", countRoot, -400, 104, OptionsKit.RefW + 800, 62, GameHudSkin.White,
                       new Color(0.07f, 0.04f, 0.07f, 0.88f));
        OptionsKit.Img("EdgeTop", countRoot, -400, 104, OptionsKit.RefW + 800, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Gold, 0.8f));
        OptionsKit.Img("EdgeBottom", countRoot, -400, 165, OptionsKit.RefW + 800, 1, GameHudSkin.White,
                       OptionsKit.WithAlpha(GameHudSkin.Gold, 0.8f));
        countLabel = OptionsKit.ShadowLabel("Label", countRoot, 0, 108, OptionsKit.RefW, 14,
            NetText.Get("net.lobby.countdown_label", "DER LAUF STARTET IN"), textFont, OptionsKit.SizeText,
            GameHudSkin.GoldLight, TextAlignmentOptions.Center);
        countNumber = OptionsKit.Label("Number", countRoot, OptionsKit.RefW / 2 - 40, 118, 80, 46, "", pixelFont,
                                       OptionsKit.SizeTitle * 2f, GameHudSkin.Cream, TextAlignmentOptions.Center);
        countNumber.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        countNumber.rectTransform.anchoredPosition = new Vector2(OptionsKit.RefW / 2f, -141f);
        countRoot.gameObject.SetActive(false);
    }

    // ==================================================================
    //  Anzeige
    // ==================================================================

    private void RefreshLobby()
    {
        NetSession s = NetSession.Instance;
        if (s == null || slots.Count == 0) return;

        for (int i = 0; i < slots.Count; i++) RefreshSlot(i, s.Slots[i], s);

        bool host = NetSession.IsHost;
        HubLevelSelectUI select = LevelSelect;
        int level = s.Settings.Level;
        worldValue.text = select != null && level >= 0 && level < select.Levels.Count
            ? select.LevelName(level).ToUpperInvariant()
            : "-";
        diffValue.text = DifficultyPanel.LevelName(s.Settings.Difficulty).ToUpperInvariant();
        storyBtn.Active = !s.Settings.Endless;
        endlessBtn.Active = s.Settings.Endless;
        storyBtn.Disabled = !host && s.Settings.Endless;
        endlessBtn.Disabled = !host && !s.Settings.Endless || host && !EndlessPossible(level);
        worldPrev.gameObject.SetActive(host);
        worldNext.gameObject.SetActive(host);
        diffPrev.gameObject.SetActive(host);
        diffNext.gameObject.SetActive(host);

        codeText.text = s.UsesSteam
            ? NetText.Get("net.lobby.code", "CODE") + " " + s.Code
            : NetText.Get("net.lobby.ip", "IP") + " " + s.Code;
        inviteBtn.gameObject.SetActive(s.UsesSteam && host);
        copyBtn.Disabled = string.IsNullOrEmpty(s.Code);

        NetSlot me = s.LocalSlotData;
        bool ready = me != null && me.Ready;
        readyBtn.SetText(ready ? NetText.Get("net.lobby.unready", "NICHT BEREIT")
                               : NetText.Get("net.lobby.ready", "BEREIT"));
        readyBtn.Active = ready;
        readyBtn.Disabled = me == null || me.Character < 0 || me.InRun;

        int used = 0;
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            if (!s.Slots[i].Used) continue;
            used++;
        }
        sectionRight.text = string.Format(NetText.Get("net.lobby.count", "{0}/{1} SPIELER"), used, NetSession.MaxPlayers);
    }

    private void RefreshSlot(int i, NetSlot data, NetSession s)
    {
        SlotView v = slots[i];
        bool used = data.Used;
        bool mine = i == s.LocalSlot;

        v.Frame.sprite = GameHudSkin.SlotFrame(!used ? GameHudSkin.SlotLook.Stone
                                               : mine ? GameHudSkin.SlotLook.Gold : GameHudSkin.SlotLook.Wood);
        v.Stage.color = used ? Color.white : new Color(0.55f, 0.5f, 0.55f, 1f);
        v.Crown.enabled = used && i == 0;
        v.Name.gameObject.SetActive(used);
        v.CharName.gameObject.SetActive(used);
        v.Level.gameObject.SetActive(used && data.Character >= 0);
        v.Ping.gameObject.SetActive(used && i > 0 && data.PingMs > 0);
        if (v.Invite != null) v.Invite.gameObject.SetActive(!used && s.UsesSteam);
        if (v.Kick != null) v.Kick.gameObject.SetActive(used && !data.InRun && s.Run == null);

        if (!used)
        {
            v.PillText.text = NetText.Get("net.lobby.free", "FREI");
            v.PillText.color = GameHudSkin.StoneLight;
            return;
        }

        v.Name.text = mine ? data.Name + " (" + NetText.Get("net.lobby.you", "DU") + ")" : data.Name;
        v.Name.color = mine ? (Color)GameHudSkin.GoldLight : GameHudSkin.Cream;
        v.CharName.text = data.Character >= 0
            ? Characters.DisplayName(data.Character).ToUpperInvariant()
            : NetText.Get("net.lobby.no_char", "KEIN CHARAKTER");
        v.CharName.color = data.Character >= 0 ? (Color)CharacterLooks.AccentFor(data.Character) : GameHudSkin.StoneLight;
        if (data.Character >= 0)
            v.Level.text = string.Format(NetText.Get("net.lobby.level", "LV {0}"), data.LevelOf(data.Character));
        v.Ping.text = data.PingMs > 0 ? data.PingMs + "MS" : "";
        v.Ping.color = data.PingMs > 150 ? (Color)GameHudSkin.JamLight
                     : data.PingMs > 80 ? (Color)GameHudSkin.Gold : GameHudSkin.MintLight;

        string status;
        Color color;
        if (data.InRun) { status = NetText.Get("net.lobby.in_run", "IM LAUF"); color = GameHudSkin.IcingLight; }
        else if (data.Ready) { status = NetText.Get("net.lobby.is_ready", "BEREIT"); color = GameHudSkin.MintLight; }
        else { status = NetText.Get("net.lobby.choosing", "WÄHLT ..."); color = GameHudSkin.Parchment; }
        v.PillText.text = status;
        v.PillText.color = color;
    }

    private void Update()
    {
        if (page == null) return;

        HandleStations();
        if (hiddenForStation) return;

        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreen)
        {
            lastScreen = size;
            OptionsKit.Layout(page, scaler, Content);
            if (ask != null && ask.childCount > 0) ((RectTransform)ask.GetChild(0)).anchoredPosition = page.anchoredPosition;
        }

        if (kind == PageKind.Start)
        {
            RefreshStart();
            if (codeField != null) OptionsKit.UpdateFocus(codeField);
            if (Time.frameCount != openedFrame && Input.GetKeyDown(KeyCode.Escape) && !TutorialPanel.Blocking)
            {
                OptionsKit.PlayClick();
                BackFromStart();
            }
            return;
        }

        HandleLobbyKeys();
    }

    // Nach den Animatoren: die Figuren auf den Buehnen lesen ihr aktuelles Bild.
    private void LateUpdate()
    {
        if (page == null || hiddenForStation) return;
        float now = Time.unscaledTime;
        AnimateBackdrop(now);
        if (kind == PageKind.Start) AnimateParade(now);
        else AnimateLobby(now);
    }

    private void AnimateLobby(float now)
    {
        NetSession s = NetSession.Instance;
        if (s == null) return;

        for (int i = 0; i < slots.Count; i++) AnimateSlot(slots[i], i, s.Slots[i], s, now);

        // Keksdose
        int mine = s.LocalSlotData != null ? s.LocalSlotData.Character : -1;
        for (int c = 0; c < tiles.Count; c++)
        {
            TileView t = tiles[c];
            int owner = OwnerOf(c, s);
            bool locked = !Characters.IsAvailable(c);
            bool taken = owner >= 0 && owner != s.LocalSlot;
            bool lit = c == hoverTile || c == cursorTile;

            Sprite[] icon = CharacterLooks.PortraitFor(c);
            if (icon != null)
            {
                int k = !locked && (lit || c == mine) ? Mathf.FloorToInt(now * PortraitFps) % icon.Length : 0;
                SetPortrait(t.Portrait, icon[k]);
                t.Portrait.color = locked ? new Color(0.22f, 0.2f, 0.24f, 1f) : Color.white;
            }
            t.Back.color = locked ? (Color)GameHudSkin.StoneDark : CharacterLooks.AccentFor(c);

            t.Shade.enabled = taken;
            t.Lock.enabled = locked;
            t.OwnerChip.enabled = owner >= 0;
            t.Owner.gameObject.SetActive(owner >= 0);
            if (owner >= 0)
            {
                t.Owner.text = owner == s.LocalSlot ? NetText.Get("net.lobby.you", "DU") : s.Slots[owner].Name;
                t.Owner.color = owner == s.LocalSlot ? (Color)GameHudSkin.Gold : GameHudSkin.Cream;
            }

            GameHudSkin.SlotLook look = locked ? GameHudSkin.SlotLook.Stone
                                      : c == mine ? GameHudSkin.SlotLook.Gold
                                      : lit && !taken ? GameHudSkin.SlotLook.Hover
                                      : GameHudSkin.SlotLook.Wood;
            Sprite frame = GameHudSkin.SlotFrame(look);
            if (t.Frame.sprite != frame) t.Frame.sprite = frame;

            // Nur die aktuelle Seite; unter dem Cursor hebt sich das Fenster ein Stueck.
            int onPage = c - tilePage * perPage;
            bool visible = onPage >= 0 && onPage < perPage;
            if (t.Root.gameObject.activeSelf != visible) t.Root.gameObject.SetActive(visible);
            if (!visible) continue;
            int shownCount = Mathf.Min(perPage, tiles.Count - tilePage * perPage);
            int rowW = shownCount * (TileSize + TileGap) - TileGap;
            int lift = lit && !locked && !taken ? 2 : 0;
            t.Root.anchoredPosition = new Vector2((OptionsKit.RefW - rowW) / 2 + onPage * (TileSize + TileGap), -(t.Area.y - lift));
        }
        if (pagePrev != null)
        {
            int pages = (tiles.Count + perPage - 1) / perPage;
            pagePrev.Disabled = tilePage <= 0;
            pageNext.Disabled = tilePage >= pages - 1;
        }

        // Info: der Charakter unter der Maus, sonst der eigene.
        int shown = hoverTile >= 0 ? hoverTile : mine >= 0 ? mine : cursorTile;
        if (shown >= 0 && shown < Characters.Count)
        {
            string text = Characters.DisplayName(shown).ToUpperInvariant() + "  -  " +
                          (Characters.IsAvailable(shown) ? Characters.Tagline(shown)
                                                         : NetText.Get("net.lobby.locked", "NOCH GESPERRT"));
            infoText.text = text;
        }

        // Stand oben rechts + Punkte je Spieler.
        int used = 0, ready = 0;
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            NetSlot slot = s.Slots[i];
            Color pip = !slot.Used ? (Color)GameHudSkin.StoneDark : slot.Ready || slot.InRun ? (Color)GameHudSkin.Mint : GameHudSkin.Gold;
            pips[i].color = pip;
            if (!slot.Used) continue;
            used++;
            if (slot.Ready) ready++;
        }
        if (now < copiedUntil)
        {
            statusText.text = NetText.Get("net.lobby.copied", "CODE KOPIERT!");
            statusText.color = GameHudSkin.MintLight;
        }
        else if (s.Countdown >= 0f)
        {
            statusText.text = string.Format(NetText.Get("net.lobby.countdown", "START IN {0}"), Mathf.CeilToInt(s.Countdown));
            statusText.color = GameHudSkin.Gold;
        }
        else
        {
            statusText.color = GameHudSkin.Parchment;
            statusText.text = LobbyStatus(s);
        }

        AnimateCountdown(s, now);
        AnimateReadyButton(s, now);
    }

    private void AnimateSlot(SlotView v, int i, NetSlot data, NetSession s, float now)
    {
        bool used = data.Used;
        int character = used ? data.Character : -1;
        Puppet puppet = puppets[i];

        if (character != v.ShownCharacter)
        {
            v.ShownCharacter = character;
            v.ChangedAt = now;
            puppet.Set(character >= 0 ? CharacterLooks.AnimatorFor(character) : null, (i * 0.37f) % 1f);
        }
        bool ready = used && (data.Ready || data.InRun);
        if (ready && !v.WasReady) v.ReadyAt = now;
        v.WasReady = ready;

        // Licht in der Farbe des Charakters, blendet beim Wechsel hinueber.
        Color target = character >= 0
            ? Color.Lerp(CharacterLooks.AccentFor(character), GameHudSkin.Cream, 0.4f)
            : (Color)GameHudSkin.StoneLight;
        v.SpotColor = Color.Lerp(v.SpotColor, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f));
        float flicker = 0.04f * Mathf.Sin(now * 1.7f + i) + 0.02f * Mathf.Sin(now * 5.3f + i * 2f);
        float strength = character >= 0 ? (ready ? 0.75f : 0.55f) : 0.15f;
        v.Spot.color = OptionsKit.WithAlpha(v.SpotColor, strength + flicker);
        v.Pool.color = OptionsKit.WithAlpha(v.SpotColor, strength + 0.15f + flicker);

        // Leerer Platz: ein Fragezeichen schwebt im Dunkeln.
        v.Question.enabled = character < 0;
        if (character < 0)
        {
            Rect q = v.Question.rectTransform.rect;
            float bob = Mathf.Round(Mathf.Sin(now * 2f + i) * 2f);
            v.Question.rectTransform.anchoredPosition = new Vector2(StageX + Mathf.Round((StageW - q.width) / 2f),
                                                                    -(StageY + 30 + bob));
            v.Question.color = OptionsKit.WithAlpha(used ? GameHudSkin.Gold : GameHudSkin.StoneLight, used ? 0.9f : 0.5f);
            v.Char.enabled = false;
        }
        else DrawCharacter(v, puppet, character, ready, now);

        // Status-Schild: bereit leuchtet, waehlt atmet.
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f + i);
        v.Pill.color = !used ? new Color(1f, 1f, 1f, 0.6f)
                     : ready ? Color.Lerp(Color.white, GameHudSkin.Mint, 0.35f + 0.15f * pulse)
                     : Color.white;
    }

    private void DrawCharacter(SlotView v, Puppet puppet, int character, bool ready, float now)
    {
        Sprite frame = puppet.Frame(ready);
        Sprite[] icon = null;
        if (frame == null)
        {
            // Ohne Animator: das gemalte Portraet.
            icon = CharacterLooks.PortraitFor(character);
            frame = icon != null ? icon[Mathf.FloorToInt(now * PortraitFps) % icon.Length] : null;
        }
        if (frame == null) { v.Char.enabled = false; return; }

        v.Char.enabled = true;
        v.Char.sprite = frame;
        v.Char.color = Color.white;

        Vector2 size;
        float x, y;
        if (icon != null)
        {
            size = new Vector2(frame.rect.width, frame.rect.height);
            x = StageX + Mathf.Round((StageW - size.x) / 2f);
            y = FeetY - size.y;
        }
        else
        {
            size = FitSize(frame, CharTarget);
            float k = size.y / frame.rect.height;
            Rect body = CharacterLooks.BodyRect(frame);
            y = FeetY - Mathf.Round((frame.rect.height - body.y - CharacterLooks.FootRowsFor(character)) * k);
            x = StageX + Mathf.Round((StageW - body.width * k) / 2f) - Mathf.Round(body.x * k);
        }

        // Neuer Charakter faellt auf die Buehne, bereit = Freudensprung.
        float t = (now - v.ChangedAt) / 0.28f;
        if (t < 1f) y -= Mathf.Round(Drop(t) * 16f);
        float h = (now - v.ReadyAt) / 0.42f;
        if (h < 1f) y -= Mathf.Round(Mathf.Sin(h * Mathf.PI) * 12f);

        OptionsKit.Move(v.Char.rectTransform, x, y, size.x, size.y);
    }

    private static float Drop(float t)
    {
        if (t < 0.6f) { float u = 1f - t / 0.6f; return u * u; }
        float v = (t - 0.6f) / 0.4f;
        return -Mathf.Sin(v * Mathf.PI) * 0.18f;
    }

    /// <summary>Groesstes ganzes Vielfaches (oder ganzer Bruchteil) des Koerpers, das ins Ziel passt.</summary>
    private static Vector2 FitSize(Sprite s, float target)
    {
        Rect body = CharacterLooks.BodyRect(s);
        float tex = Mathf.Max(body.width, body.height);
        float k = tex <= target ? Mathf.Floor(target / tex) : 1f / Mathf.Ceil(tex / target);
        return new Vector2(Mathf.Round(s.rect.width * k), Mathf.Round(s.rect.height * k));
    }

    private void AnimateCountdown(NetSession s, float now)
    {
        if (countRoot == null) return;
        bool on = s.Countdown >= 0f;
        if (countRoot.gameObject.activeSelf != on) countRoot.gameObject.SetActive(on);
        if (!on) { countShown = -1; return; }

        int n = Mathf.Max(1, Mathf.CeilToInt(s.Countdown));
        if (n != countShown)
        {
            countShown = n;
            countAt = now;
            countNumber.text = n.ToString();
            OptionsKit.PlayClick();
        }
        float t = now - countAt;
        float sc = t < 0.06f ? 1.6f : t < 0.12f ? 1.25f : 1f;
        countNumber.rectTransform.localScale = new Vector3(sc, sc, 1f);
        countNumber.color = n == 1 ? (Color)GameHudSkin.GoldLight : GameHudSkin.Cream;
    }

    private void AnimateReadyButton(NetSession s, float now)
    {
        NetSlot me = s.LocalSlotData;
        if (me == null || me.Ready || me.Character < 0 || me.InRun)
        {
            readyBtn.transform.localScale = Vector3.one;
            return;
        }
        // Noch nicht bereit: der Knopf atmet, damit man ihn findet.
        bool beat = Mathf.Repeat(now * 1.2f, 1f) < 0.12f;
        readyBtn.transform.localScale = beat ? new Vector3(1.04f, 1.06f, 1f) : Vector3.one;
    }

    private static string LobbyStatus(NetSession s)
    {
        int used = 0, ready = 0, inRun = 0;
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            NetSlot slot = s.Slots[i];
            if (!slot.Used) continue;
            used++;
            if (slot.Ready) ready++;
            if (slot.InRun) inRun++;
        }

        if (inRun > 0) return NetText.Get("net.lobby.wait_run", "WARTE AUF SPIELER IM LAUF ...");
        if (used < NetSession.MinPlayers) return NetText.Get("net.lobby.wait_players", "WARTE AUF MITSPIELER ...");
        NetSlot me = s.LocalSlotData;
        if (me != null && me.Character < 0) return NetText.Get("net.lobby.pick_first", "WÄHLE EINEN CHARAKTER");
        return string.Format(NetText.Get("net.lobby.ready_count", "{0}/{1} BEREIT"), ready, used);
    }

    private static int OwnerOf(int character, NetSession s)
    {
        for (int i = 0; i < NetSession.MaxPlayers; i++)
            if (s.Slots[i].Used && s.Slots[i].Character == character) return i;
        return -1;
    }

    private static void SetPortrait(Image img, Sprite sprite)
    {
        if (img.sprite == sprite) return;
        img.sprite = sprite;
        img.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
        // Mittig im 42er-Fenster, auf ganzen Pixeln.
        img.rectTransform.anchoredPosition = new Vector2(Mathf.Round((Win - sprite.rect.width) / 2f),
                                                         -Mathf.Round((Win - sprite.rect.height) / 2f));
    }

    // ==================================================================
    //  Bedienung
    // ==================================================================

    private void HandleLobbyKeys()
    {
        if (Time.frameCount == openedFrame || TutorialPanel.Blocking) return;

        if (ask != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { OptionsKit.PlayClick(); CloseAsk(); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { OptionsKit.PlayClick(); Leave(); }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { OptionsKit.PlayClick(); AskLeave(); return; }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveCursor(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveCursor(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            NetSlot me = NetSession.Instance?.LocalSlotData;
            if (me != null && me.Character == cursorTile) ToggleReady();
            else Pick(cursorTile);
        }
        if (Input.GetKeyDown(KeyCode.R)) ToggleReady();
    }

    private void MoveCursor(int delta)
    {
        int count = Characters.Count;
        if (count == 0) return;
        cursorTile = (cursorTile + delta + count) % count;
        tilePage = cursorTile / Mathf.Max(1, perPage);
        OptionsKit.PlayClick();
    }

    private void StepPage(int delta)
    {
        int pages = (tiles.Count + perPage - 1) / Mathf.Max(1, perPage);
        tilePage = Mathf.Clamp(tilePage + delta, 0, Mathf.Max(0, pages - 1));
        hoverTile = -1;
    }

    private void Pick(int character)
    {
        NetSession s = NetSession.Instance;
        if (s == null || s.LocalSlotData == null || s.LocalSlotData.InRun) return;
        cursorTile = character;

        if (!Characters.IsAvailable(character) || s.IsTaken(character, s.LocalSlot))
        {
            NetLobbyHub.PlayDeny();
            return;
        }

        OptionsKit.PlayClick();
        // Wie die Charakterauswahl im Hub: der Charakter gilt auch fuer
        // Skilltree, Werkbank und den Lauf.
        Shop.SkinIndex = character;
        Skills.SetActiveTreeForCharacter(character);
        s.PickCharacter(character);
    }

    private void ToggleReady()
    {
        NetSession s = NetSession.Instance;
        NetSlot me = s?.LocalSlotData;
        if (me == null || me.Character < 0 || me.InRun) return;
        OptionsKit.PlayClick();
        s.SetReady(!me.Ready);
    }

    private void CopyCode()
    {
        NetSession s = NetSession.Instance;
        if (s == null || string.IsNullOrEmpty(s.Code)) return;
        GUIUtility.systemCopyBuffer = s.Code;
        copiedUntil = Time.unscaledTime + 1.5f;
    }

    private void Invite()
    {
        if (!NetSteamLobby.OpenInviteDialog()) NetLobbyHub.PlayDeny();
    }

    private HubLevelSelectUI LevelSelect => FindAnyObjectByType<HubLevelSelectUI>(FindObjectsInactive.Include);

    /// <summary>Darf der Host diese Welt waehlen? Story offen (oder Endless offen) und es gibt die Szene.</summary>
    private bool LevelPossible(int index, bool endless)
    {
        HubLevelSelectUI select = LevelSelect;
        if (select == null || index < 0 || index >= select.Levels.Count) return false;
        HubLevelSelectUI.LevelEntry e = select.Levels[index];
        if (!select.IsLinked(e)) return false;
        return endless ? select.EndlessUnlocked(e) : select.StoryUnlocked(e);
    }

    private bool EndlessPossible(int index) => LevelPossible(index, true);

    private void StepWorld(int delta)
    {
        NetSession s = NetSession.Instance;
        HubLevelSelectUI select = LevelSelect;
        if (!NetSession.IsHost || select == null || select.Levels.Count == 0) return;

        int count = select.Levels.Count;
        int level = s.Settings.Level;
        for (int n = 0; n < count; n++)
        {
            level = (level + delta + count) % count;
            if (LevelPossible(level, false) || LevelPossible(level, true)) break;
        }
        bool endless = s.Settings.Endless && EndlessPossible(level) || !LevelPossible(level, false);
        OptionsKit.PlayClick();
        s.ChangeSettings(level, endless, s.Settings.Difficulty);
    }

    private void SetEndless(bool endless)
    {
        NetSession s = NetSession.Instance;
        if (!NetSession.IsHost) return;
        if (!LevelPossible(s.Settings.Level, endless)) { NetLobbyHub.PlayDeny(); return; }
        s.ChangeSettings(s.Settings.Level, endless, s.Settings.Difficulty);
    }

    private void StepDifficulty(int delta)
    {
        NetSession s = NetSession.Instance;
        if (!NetSession.IsHost) return;
        int next = Mathf.Clamp((int)s.Settings.Difficulty + delta, 0, 2);
        if (next == (int)s.Settings.Difficulty) return;
        OptionsKit.PlayClick();
        s.ChangeSettings(s.Settings.Level, s.Settings.Endless, (Difficulty.Level)next);
    }

    /// <summary>Beim Oeffnen der Lobby: stimmt die gewaehlte Welt nicht, die erste spielbare nehmen.</summary>
    public static void EnsurePlayableWorld()
    {
        NetSession s = NetSession.Instance;
        if (!NetSession.IsHost || instance == null) return;
        if (instance.LevelPossible(s.Settings.Level, s.Settings.Endless)) return;
        HubLevelSelectUI select = instance.LevelSelect;
        if (select == null) return;
        for (int i = 0; i < select.Levels.Count; i++)
        {
            if (instance.LevelPossible(i, false)) { s.ChangeSettings(i, false, s.Settings.Difficulty); return; }
        }
    }

    // ---------- Stationen ----------

    private enum Station { Skilltree, Workbench, Shop }

    private void OpenStation(Station station)
    {
        NetSession s = NetSession.Instance;
        // Wer noch Punkte verteilt, ist nicht bereit - sonst startet der Lauf mitten drin.
        if (s?.LocalSlotData != null && s.LocalSlotData.Ready) s.SetReady(false);

        switch (station)
        {
            case Station.Skilltree:
                HubSkilltreeUI tree = FindAnyObjectByType<HubSkilltreeUI>(FindObjectsInactive.Include);
                if (tree == null) { NetLobbyHub.PlayDeny(); return; }
                tree.Open();
                break;
            case Station.Workbench:
                WorkbenchPanel.Open();
                break;
            case Station.Shop:
                HubShopUI shop = FindAnyObjectByType<HubShopUI>(FindObjectsInactive.Include);
                if (shop == null) { NetLobbyHub.PlayDeny(); return; }
                shop.Open();
                break;
        }
        HandleStations();
    }

    private static bool StationOpen => HubSkilltreeUI.IsOpen || WorkbenchPanel.IsOpen || HubShopUI.IsOpen;

    /// <summary>Solange eine Station offen ist, tritt die Lobby zur Seite.</summary>
    private void HandleStations()
    {
        bool open = StationOpen;
        if (open == hiddenForStation) return;

        hiddenForStation = open;
        if (canvas != null) canvas.enabled = !open;

        if (!open)
        {
            // Zurueck: Level koennen sich geaendert haben, und die Station hat
            // den Hub-Spieler wieder losgelassen.
            NetSession.Instance?.SendProfile();
            if (hub != null) hub.SetPlayerFrozen(true);
            openedFrame = Time.frameCount;
            RefreshLobby();
        }
    }

    // ---------- Verlassen ----------

    private void AskLeave()
    {
        if (ask != null) return;

        Image shade = OptionsKit.Stretch("Ask", transform, GameHudSkin.White, new Color(0.06f, 0.04f, 0.05f, 0.7f));
        shade.raycastTarget = true;
        ask = shade.rectTransform;

        RectTransform p = OptionsKit.Rect("Page", ask, 0, 0, OptionsKit.RefW, OptionsKit.RefH);
        p.anchoredPosition = page.anchoredPosition;

        OptionsKit.Img("Card", p, AskX, AskY, AskW, AskH, GameHudSkin.Card, true);
        TextMeshProUGUI q = OptionsKit.Label("Question", p, AskX + 10, AskY + 10, AskW - 20, 30,
            NetSession.IsHost ? NetText.Get("net.lobby.ask_close", "Lobby schließen? Alle Mitspieler fliegen raus.")
                              : NetText.Get("net.lobby.ask_leave", "Lobby wirklich verlassen?"),
            textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        q.textWrappingMode = TextWrappingModes.Normal;

        SkinButton.Create(p, AskX + 10, AskY + AskH - 26, 74, 18, NetText.Get("net.lobby.ask_no", "BLEIBEN"),
            textFont, SkinButton.Kind.Wood, CloseAsk);
        SkinButton.Create(p, AskX + AskW - 84, AskY + AskH - 26, 74, 18, NetText.Get("net.lobby.ask_yes", "VERLASSEN"),
            textFont, SkinButton.Kind.Danger, Leave);
    }

    private void CloseAsk()
    {
        if (ask == null) return;
        ask.SetParent(null, false);
        Destroy(ask.gameObject);
        ask = null;
    }

    private void Leave()
    {
        CloseAsk();
        NetSession.Leave();
    }
}
