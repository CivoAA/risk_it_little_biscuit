using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Die Koop-Lobby im Hub, im Stil von "UI 2.0" (<see cref="OptionsKit"/>).
///
/// Zwei Seiten:
///
///   START    noch keine Sitzung: LOBBY ERSTELLEN, Code eingeben + BEITRETEN,
///            ZURUECK (ins Hauptmenue). Auch Fehler ("Lobby nicht gefunden")
///            landen hier.
///
///   LOBBY    oben die vier Plaetze (Portraet des gewaehlten Charakters, Name,
///            Charakter-Level, Status), darunter die Keksdose mit allen
///            Charakteren - jeder nur einmal, vergebene tragen den Namen des
///            Spielers. Links unten SKILLTREE / WERKBANK / SHOP und der Code
///            zum Weitergeben, rechts Welt, Modus und Schwierigkeit (nur der
///            Host stellt sie ein), unten VERLASSEN und BEREIT.
///
/// Sind alle bereit, zaehlt der Host drei Sekunden runter und der Lauf
/// startet bei allen (<see cref="NetLobbyHub"/> uebernimmt). Wer zum Skilltree,
/// zur Werkbank oder in den Shop geht, ist danach nicht mehr bereit - sonst
/// startet der Lauf, waehrend er noch Punkte verteilt.
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

    // Lobby
    private const int CardX = 12, CardY = 36, CardW = 456, CardH = 228;
    private const int RibbonY = 24;
    private static readonly RectInt Content = new RectInt(0, RibbonY, 480, CardY + CardH - RibbonY);

    private const int SlotX = 18, SlotY = 46, SlotW = 108, SlotH = 62, SlotGap = 4;
    private const int Win = 42;

    private const int TileY = 124, TileSize = 48, TileGap = 2;
    private const int SectionY = 111;
    private const int InfoY = 174;

    private const int ColHeadY = 190, RowA = 203, RowB = 222, BtnH = 16;
    private const int LeftX = 18, RightX = 246;

    private const int FootY = 244;

    // Start
    private const int StartX = 120, StartY = 60, StartW = 240, StartH = 160;
    private static readonly RectInt StartContent = new RectInt(StartX - 12, StartY - 12, StartW + 24, StartH + 12);

    // Rueckfrage
    private const int AskX = 150, AskY = 104, AskW = 180, AskH = 74;

    private const float PortraitFps = 8f;

    // ==================================================================

    private static NetLobbyPanel instance;
    public static bool IsOpen => instance != null;

    private enum PageKind { Start, Lobby }

    private class SlotView
    {
        public RectTransform Root;
        public Image Frame, Back, Portrait, Crown, Chip;
        public TextMeshProUGUI Name, Level, Status, Ping, Empty;
        public SkinButton Invite, Kick;
    }

    private class TileView
    {
        public Image Frame, Back, Portrait, Shade, Lock, OwnerChip;
        public TextMeshProUGUI Owner;
        public Rect Area;
    }

    private TMP_FontAsset textFont, pixelFont;
    private RectTransform page;
    private CanvasScaler scaler;
    private Canvas canvas;
    private Vector2Int lastScreen;
    private PageKind kind;
    private HubUI hub;
    private int openedFrame;

    // Start
    private TMP_InputField codeField;
    private TextMeshProUGUI startStatus;
    private SkinButton createBtn, joinBtn, startBackBtn;
    private string startMessage = "";
    private bool startError;

    // Lobby
    private readonly List<SlotView> slots = new List<SlotView>();
    private readonly List<TileView> tiles = new List<TileView>();
    private TextMeshProUGUI infoText, statusText, codeText, worldValue, diffValue, sectionRight;
    private SkinButton readyBtn, leaveBtn, copyBtn, inviteBtn, storyBtn, endlessBtn;
    private SkinButton worldPrev, worldNext, diffPrev, diffNext;
    private int hoverTile = -1;
    private int cursorTile;
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
        ask = null;
        codeField = null;

        // Unter Shop (130), Werkbank (155) und Optionen (210) - die gehen ueber der Lobby auf.
        page = OptionsKit.CreatePage(gameObject, 120, 0.9f, out scaler);
        canvas = GetComponent<Canvas>();

        if (kind == PageKind.Start) BuildStart();
        else BuildLobby();

        OptionsKit.Layout(page, scaler, kind == PageKind.Start ? StartContent : Content);
        lastScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // ---------- Startseite ----------

    private void BuildStart()
    {
        OptionsKit.Img("Card", page, StartX, StartY, StartW, StartH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, StartX + StartW / 2f, StartY - 12, NetText.Get("net.title", "KOOP"), pixelFont, textFont);

        TextMeshProUGUI intro = OptionsKit.Label("Intro", page, StartX + 14, StartY + 18, StartW - 28, 26,
            NetText.Get("net.start.intro", "Spielt online zu zweit, zu dritt oder zu viert."),
            textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        intro.textWrappingMode = TextWrappingModes.Normal;

        createBtn = SkinButton.Create(page, StartX + 30, StartY + 46, StartW - 60, 18,
            NetText.Get("net.start.create", "LOBBY ERSTELLEN"), textFont, SkinButton.Kind.Primary, CreateLobby);

        OptionsKit.Label("Or", page, StartX, StartY + 72, StartW, 14,
            NetText.Get("net.start.or", "ODER MIT CODE BEITRETEN"), textFont, OptionsKit.SizeText,
            GameHudSkin.Stone, TextAlignmentOptions.Center);

        codeField = OptionsKit.Input(page, StartX + 30, StartY + 88, 116, 18, textFont,
            NetText.Get("net.start.code_hint", "CODE"), false, 32);
        codeField.onSubmit.AddListener(_ => JoinLobby());

        joinBtn = SkinButton.Create(page, StartX + 150, StartY + 88, 60, 18,
            NetText.Get("net.start.join", "BEITRETEN"), textFont, SkinButton.Kind.Wood, JoinLobby);

        startStatus = OptionsKit.Label("Status", page, StartX + 10, StartY + 112, StartW - 20, 14, "", textFont,
                                       OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        startStatus.textWrappingMode = TextWrappingModes.Normal;

        startBackBtn = SkinButton.Create(page, StartX + (StartW - 80) / 2, StartY + StartH - 26, 80, 18,
            NetText.Get("net.start.back", "ZURÜCK"), textFont, SkinButton.Kind.Wood, BackFromStart);

        RefreshStart();
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
            startStatus.text = NetText.Get("net.start.connecting", "VERBINDE ...");
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
        OptionsKit.Img("Card", page, CardX, CardY, CardW, CardH, GameHudSkin.Card, true);
        OptionsKit.Ribbon(page, CardX + CardW / 2f, RibbonY, NetText.Get("net.title", "KOOP"), pixelFont, textFont);

        for (int i = 0; i < NetSession.MaxPlayers; i++) slots.Add(BuildSlot(i));

        OptionsKit.ShadowLabel("Section", page, LeftX, SectionY, 200, 14,
            NetText.Get("net.lobby.pick", "CHARAKTER WÄHLEN"), textFont, OptionsKit.SizeText,
            GameHudSkin.Gold, TextAlignmentOptions.Left);
        sectionRight = OptionsKit.Label("SectionRight", page, 250, SectionY, 212, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Right);

        int count = Characters.Count;
        int tilesW = count * TileSize + (count - 1) * TileGap;
        int tx = CardX + (CardW - tilesW) / 2;
        for (int i = 0; i < count; i++)
            tiles.Add(BuildTile(i, new Rect(tx + i * (TileSize + TileGap), TileY, TileSize, TileSize)));

        infoText = OptionsKit.Label("Info", page, CardX + 10, InfoY, CardW - 20, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);

        // Links: vorbereiten + Code
        OptionsKit.Label("PrepHead", page, LeftX, ColHeadY, 218, 12,
            NetText.Get("net.lobby.prepare", "VORBEREITEN"), textFont, OptionsKit.SizeText,
            GameHudSkin.Stone, TextAlignmentOptions.Left);
        SkinButton.Create(page, LeftX, RowA, 72, BtnH, NetText.Get("net.lobby.skilltree", "SKILLTREE"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Skilltree));
        SkinButton.Create(page, LeftX + 76, RowA, 72, BtnH, NetText.Get("net.lobby.workbench", "WERKBANK"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Workbench));
        SkinButton.Create(page, LeftX + 152, RowA, 66, BtnH, NetText.Get("net.lobby.shop", "SHOP"),
            textFont, SkinButton.Kind.Wood, () => OpenStation(Station.Shop));

        codeText = OptionsKit.Label("Code", page, LeftX, RowB + 1, 104, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Gold, TextAlignmentOptions.Left);
        copyBtn = SkinButton.Create(page, LeftX + 106, RowB, 54, BtnH, NetText.Get("net.lobby.copy", "KOPIEREN"),
            textFont, SkinButton.Kind.Wood, CopyCode);
        inviteBtn = SkinButton.Create(page, LeftX + 164, RowB, 54, BtnH, NetText.Get("net.lobby.invite", "EINLADEN"),
            textFont, SkinButton.Kind.Wood, Invite);

        // Rechts: Einstellungen des Hosts
        OptionsKit.Label("SetHead", page, RightX, ColHeadY, 216, 12,
            NetSession.IsHost ? NetText.Get("net.lobby.settings", "LAUF")
                              : NetText.Get("net.lobby.settings_client", "LAUF (STELLT DER HOST EIN)"),
            textFont, OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Left);

        OptionsKit.Label("WorldLabel", page, RightX, RowA + 1, 40, 14, NetText.Get("net.lobby.world", "WELT"),
            textFont, OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Left);
        worldPrev = SkinButton.Create(page, RightX + 42, RowA, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepWorld(-1), GameHudSkin.ArrowLeft);
        worldValue = OptionsKit.Label("World", page, RightX + 60, RowA + 1, 136, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        worldNext = SkinButton.Create(page, RightX + 198, RowA, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepWorld(1), GameHudSkin.Arrow);

        storyBtn = SkinButton.Create(page, RightX, RowB, 50, BtnH, NetText.Get("net.lobby.story", "STORY"),
            textFont, SkinButton.Kind.Wood, () => SetEndless(false));
        endlessBtn = SkinButton.Create(page, RightX + 52, RowB, 54, BtnH, NetText.Get("net.lobby.endless", "ENDLESS"),
            textFont, SkinButton.Kind.Wood, () => SetEndless(true));
        diffPrev = SkinButton.Create(page, RightX + 110, RowB, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepDifficulty(-1), GameHudSkin.ArrowLeft);
        diffValue = OptionsKit.Label("Diff", page, RightX + 128, RowB + 1, 68, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Cream, TextAlignmentOptions.Center);
        diffNext = SkinButton.Create(page, RightX + 198, RowB, 16, BtnH, "", textFont, SkinButton.Kind.Wood,
            () => StepDifficulty(1), GameHudSkin.Arrow);

        // Fusszeile
        leaveBtn = SkinButton.Create(page, LeftX, FootY, 80, BtnH, NetText.Get("net.lobby.leave", "VERLASSEN"),
            textFont, SkinButton.Kind.Danger, AskLeave);
        statusText = OptionsKit.Label("Status", page, LeftX + 84, FootY + 1, 276, 14, "", textFont,
            OptionsKit.SizeText, GameHudSkin.Parchment, TextAlignmentOptions.Center);
        readyBtn = SkinButton.Create(page, CardX + CardW - 6 - 78, FootY, 78, BtnH,
            NetText.Get("net.lobby.ready", "BEREIT"), textFont, SkinButton.Kind.Primary, ToggleReady);

        cursorTile = Mathf.Clamp(Shop.SkinIndex, 0, Mathf.Max(0, Characters.Count - 1));
        RefreshLobby();
    }

    private SlotView BuildSlot(int i)
    {
        int x = SlotX + i * (SlotW + SlotGap);
        var v = new SlotView();
        v.Root = OptionsKit.Rect("Slot" + i, page, x, SlotY, SlotW, SlotH);

        v.Frame = OptionsKit.Img("Frame", v.Root, 0, 0, SlotW, SlotH, GameHudSkin.SlotFrame(GameHudSkin.SlotLook.Stone), true);
        v.Back = OptionsKit.Img("Back", v.Root, 6, 6, Win, Win, GameHudSkin.SlotBack(Win, Win));

        RectTransform window = OptionsKit.Rect("Window", v.Root, 6, 6, Win, Win);
        window.gameObject.AddComponent<RectMask2D>();
        v.Portrait = OptionsKit.Img("Portrait", window, 0, 0, Win, Win, null);

        Sprite crown = GameHudSkin.Crown;
        v.Crown = OptionsKit.Img("Crown", v.Root, 3, -3, crown.rect.width, crown.rect.height, crown);

        v.Name = OptionsKit.Label("Name", v.Root, 52, 6, SlotW - 56, 14, "", textFont, OptionsKit.SizeText,
                                  GameHudSkin.Cream, TextAlignmentOptions.Left);
        v.Name.overflowMode = TextOverflowModes.Ellipsis;
        v.Level = OptionsKit.Label("Level", v.Root, 52, 19, SlotW - 56, 14, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.Parchment, TextAlignmentOptions.Left);
        v.Chip = OptionsKit.Img("Chip", v.Root, 51, 35, SlotW - 57, 13, GameHudSkin.LevelChip, true);
        v.Status = OptionsKit.Label("Status", v.Root, 51, 34, SlotW - 57, 14, "", textFont, OptionsKit.SizeText,
                                    GameHudSkin.Cream, TextAlignmentOptions.Center);
        v.Ping = OptionsKit.Label("Ping", v.Root, 6, SlotH - 16, Win, 12, "", textFont, OptionsKit.SizeText,
                                  GameHudSkin.Stone, TextAlignmentOptions.Center);
        v.Ping.rectTransform.anchoredPosition += new Vector2(0f, 0f);

        v.Empty = OptionsKit.Label("Empty", v.Root, 0, 14, SlotW, 14, NetText.Get("net.lobby.free", "FREI"), textFont,
                                   OptionsKit.SizeText, GameHudSkin.Stone, TextAlignmentOptions.Center);

        if (NetSession.IsHost && i > 0)
        {
            v.Invite = SkinButton.Create(v.Root, (SlotW - 64) / 2, 34, 64, BtnH,
                NetText.Get("net.lobby.invite", "EINLADEN"), textFont, SkinButton.Kind.Wood, Invite);
            int slot = i;
            v.Kick = SkinButton.Create(v.Root, SlotW - 15, 3, 12, 12, "x", textFont, SkinButton.Kind.Danger,
                () => NetSession.Instance?.Kick(slot));
        }
        return v;
    }

    private TileView BuildTile(int character, Rect a)
    {
        var t = new TileView { Area = a };
        RectTransform rt = OptionsKit.Rect("Tile" + character, page, a.x, a.y, a.width, a.height);

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
        t.Owner = OptionsKit.Label("Owner", rt, 2, TileSize - 15, TileSize - 4, 12, "", textFont, OptionsKit.SizeText,
                                   GameHudSkin.Cream, TextAlignmentOptions.Center);
        t.Owner.overflowMode = TextOverflowModes.Truncate;

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

        int readyCount = 0, used = 0;
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            if (!s.Slots[i].Used) continue;
            used++;
            if (s.Slots[i].Ready) readyCount++;
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
        v.Empty.gameObject.SetActive(!used);
        v.Back.gameObject.SetActive(used);
        v.Portrait.enabled = used && data.Character >= 0;
        v.Crown.enabled = used && i == 0;
        v.Name.gameObject.SetActive(used);
        v.Level.gameObject.SetActive(used);
        v.Chip.gameObject.SetActive(used);
        v.Status.gameObject.SetActive(used);
        v.Ping.gameObject.SetActive(used && i > 0);
        if (v.Invite != null) v.Invite.gameObject.SetActive(!used && s.UsesSteam);
        if (v.Kick != null) v.Kick.gameObject.SetActive(used && !data.InRun && s.Run == null);

        if (!used) return;

        v.Name.text = data.Name;
        v.Back.color = data.Character >= 0 ? CharacterLooks.AccentFor(data.Character) : (Color)GameHudSkin.StoneDark;
        v.Level.text = data.Character >= 0
            ? Characters.DisplayName(data.Character).ToUpperInvariant() + "  " +
              string.Format(NetText.Get("net.lobby.level", "LV {0}"), data.LevelOf(data.Character))
            : NetText.Get("net.lobby.no_char", "KEIN CHARAKTER");

        string status;
        Color color;
        if (data.InRun) { status = NetText.Get("net.lobby.in_run", "IM LAUF"); color = GameHudSkin.Icing; }
        else if (data.Ready) { status = NetText.Get("net.lobby.is_ready", "BEREIT"); color = GameHudSkin.Mint; }
        else { status = NetText.Get("net.lobby.choosing", "WÄHLT ..."); color = GameHudSkin.StoneLight; }
        v.Status.text = status;
        v.Status.color = color;

        v.Ping.text = data.PingMs > 0 ? data.PingMs + " ms" : "";
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
            OptionsKit.Layout(page, scaler, kind == PageKind.Start ? StartContent : Content);
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

        AnimateLobby();
        HandleLobbyKeys();
    }

    private void AnimateLobby()
    {
        NetSession s = NetSession.Instance;
        if (s == null) return;
        float now = Time.unscaledTime;

        // Plaetze: Portraet des gewaehlten Charakters, der eigene huepft.
        for (int i = 0; i < slots.Count; i++)
        {
            NetSlot data = s.Slots[i];
            if (!data.Used || data.Character < 0) continue;
            Sprite[] icon = CharacterLooks.PortraitFor(data.Character);
            if (icon == null) continue;
            int k = data.Ready || i == s.LocalSlot ? Mathf.FloorToInt(now * PortraitFps) % icon.Length : 0;
            SetPortrait(slots[i].Portrait, icon[k]);
        }

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
        }

        // Info: der Charakter unter der Maus, sonst der eigene.
        int shown = hoverTile >= 0 ? hoverTile : mine >= 0 ? mine : cursorTile;
        if (shown >= 0 && shown < Characters.Count)
        {
            string text = Characters.DisplayName(shown).ToUpperInvariant() + "  -  " + Characters.Tagline(shown);
            if (!Characters.IsAvailable(shown)) text = Characters.DisplayName(shown).ToUpperInvariant() + "  -  " +
                                                      NetText.Get("net.lobby.locked", "NOCH GESPERRT");
            infoText.text = text;
        }

        // Status in der Fusszeile.
        if (s.Countdown >= 0f)
        {
            statusText.text = string.Format(NetText.Get("net.lobby.countdown", "START IN {0}"), Mathf.CeilToInt(s.Countdown));
            statusText.color = GameHudSkin.Gold;
        }
        else
        {
            statusText.color = GameHudSkin.Parchment;
            statusText.text = LobbyStatus(s);
        }
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
        OptionsKit.PlayClick();
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
        statusText.text = NetText.Get("net.lobby.copied", "CODE KOPIERT!");
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
