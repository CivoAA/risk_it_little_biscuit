using System;
using System.Collections.Generic;
using UnityEngine;

public enum NetRole { None, Host, Client }

/// <summary>Ein Platz in der Lobby (0 = Host, bis zu <see cref="NetSession.MaxPlayers"/>).</summary>
public sealed class NetSlot
{
    public bool Used;
    /// <summary>Verbindung beim Host. -1 = der Host selbst oder frei.</summary>
    public int Connection = -1;
    public string Name = "";
    public ulong SteamId;
    /// <summary>Gewaehlter Charakter, -1 = noch keiner.</summary>
    public int Character = -1;
    public bool Ready;
    /// <summary>Charakter-Level je Charakter aus dem Spielstand des Spielers (fuer die Anzeige).</summary>
    public int[] CharLevels = new int[0];
    /// <summary>Bit i = Charakter i ist bei diesem Spieler freigeschaltet.</summary>
    public int UnlockedMask;
    /// <summary>Gerade in einem Lauf (oder auf dem Rueckweg) statt in der Lobby.</summary>
    public bool InRun;
    public int PingMs;

    // Nur beim Host gebraucht.
    public float LastHeard;

    public int LevelOf(int character) =>
        character >= 0 && character < CharLevels.Length ? CharLevels[character] : 0;

    public bool HasUnlocked(int character) => character >= 0 && character < 31 && (UnlockedMask & (1 << character)) != 0;

    public void Clear()
    {
        Used = false;
        Connection = -1;
        Name = "";
        SteamId = 0;
        Character = -1;
        Ready = false;
        CharLevels = new int[0];
        UnlockedMask = 0;
        InRun = false;
        PingMs = 0;
    }
}

/// <summary>Was der Host fuer den naechsten Lauf eingestellt hat.</summary>
public sealed class NetLobbySettings
{
    /// <summary>Index in <see cref="HubLevelSelectUI.Levels"/>.</summary>
    public int Level;
    public bool Endless;
    public Difficulty.Level Difficulty = global::Difficulty.Level.Normal;
}

/// <summary>Alles, womit ein Koop-Lauf startet - kommt vom Host, ist bei allen gleich.</summary>
public sealed class NetRunInfo
{
    public int Seed;
    public int Level;
    public int MapId;
    public string Scene = "";
    public bool Endless;
    public Difficulty.Level Difficulty;
    /// <summary>Charakter je Platz, -1 = Platz frei.</summary>
    public int[] Characters = new int[NetSession.MaxPlayers];
    public int PlayerCount;
}

/// <summary>
/// Die Koop-Sitzung: wer mitspielt, wer Host ist, wie Nachrichten laufen.
///
/// AUFBAU
///
/// Sternfoermig: jeder Client hat genau eine Verbindung, zum Host. Schickt ein
/// Client etwas an alle oder an einen anderen Client, reicht der Host es
/// weiter. Bei vier Spielern ist das einfacher und robuster als ein volles
/// Netz - und fuer ein Koop-Spiel ohne Wettkampf schnell genug.
///
/// Jede Nachricht beginnt mit drei Bytes: Art (<see cref="NetMsg"/>),
/// Absender-Platz, Ziel (ein Platz, <see cref="ToAll"/> oder
/// <see cref="ToHost"/>). Danach kommt der Inhalt, den die Systeme selbst
/// schreiben und lesen. Wer Nachrichten haben will, meldet sich per
/// <see cref="On"/> an.
///
/// LEBENSDAUER
///
/// Das Objekt entsteht, wenn jemand eine Lobby oeffnet oder beitritt, und
/// lebt ueber alle Szenenwechsel (DontDestroyOnLoad) - auch durch die Laeufe
/// hindurch. Es endet mit <see cref="Leave"/> oder wenn die Verbindung zum
/// Host reisst.
///
/// Die Lobby-Daten gehoeren dem Host. Clients schicken nur Wuensche
/// (Charakter, bereit) und bekommen den ganzen Stand zurueck.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class NetSession : MonoBehaviour, INetTransportListener
{
    /// <summary>Hochzaehlen, sobald sich ein Nachrichtenformat aendert.</summary>
    public const int Protocol = 3;   // 2: Kreuz-Altar (NetRevive), 3: Team-Bilanz + Emotes

    public const int MaxPlayers = 4;

    public const byte ToAll = 255;
    public const byte ToHost = 254;

    /// <summary>So lange ohne ein Lebenszeichen, dann gilt ein Mitspieler als weg.</summary>
    private const float Timeout = 20f;

    /// <summary>Countdown, sobald alle bereit sind.</summary>
    public const float StartCountdown = 3f;

    public static NetSession Instance { get; private set; }

    /// <summary>Gerade in einer Koop-Sitzung (Lobby oder Lauf)?</summary>
    public static bool Active => Instance != null && Instance.Role != NetRole.None;

    /// <summary>Gerade in einem Koop-Lauf? Fast alle Abzweigungen im Spiel fragen das.</summary>
    public static bool InRun => Active && Instance.Run != null;

    public static bool IsHost => Active && Instance.Role == NetRole.Host;

    /// <summary>Lobby-Stand hat sich geaendert (Plaetze, Einstellungen, Countdown).</summary>
    public static event Action Changed;

    /// <summary>Die Sitzung ist vorbei - mit Grund (Loc-Schluessel).</summary>
    public static event Action<string> Ended;

    /// <summary>Ein Mitspieler ist gegangen (Platz). Kommt vor <see cref="Changed"/>.</summary>
    public static event Action<int> SlotLeft;

    /// <summary>Der Host hat den Lauf gestartet.</summary>
    public static event Action<NetRunInfo> RunStarting;

    public NetRole Role { get; private set; }
    public int LocalSlot { get; private set; } = -1;
    public readonly NetSlot[] Slots = new NetSlot[MaxPlayers];
    public readonly NetLobbySettings Settings = new NetLobbySettings();

    /// <summary>Der laufende Koop-Lauf, sonst null.</summary>
    public NetRunInfo Run { get; private set; }

    /// <summary>Beitrittscode der Lobby (Steam) bzw. Adresse (TCP-Test).</summary>
    public string Code { get; private set; } = "";

    /// <summary>Laeuft ueber Steam (sonst TCP-Test).</summary>
    public bool UsesSteam { get; private set; }

    /// <summary>Restzeit des Start-Countdowns, negativ = keiner laeuft.</summary>
    public float Countdown { get; private set; } = -1f;

    /// <summary>Verbunden und vom Host aufgenommen (Clients erst nach Welcome).</summary>
    public bool Joined => Role == NetRole.Host || (Role == NetRole.Client && LocalSlot >= 0);

    public NetSlot LocalSlotData => LocalSlot >= 0 ? Slots[LocalSlot] : null;

    private INetTransport transport;
    private int hostConnection = -1;
    private readonly Dictionary<NetMsg, Action<int, NetReader>> handlers = new Dictionary<NetMsg, Action<int, NetReader>>();
    private readonly NetWriter writer = new NetWriter(2048);
    private readonly NetReader reader = new NetReader();
    private byte[] relay = new byte[2048];
    private float pingTimer;
    private float lastHeardFromHost;
    private bool lobbyDirty;
    private string endReason;

    // Zaehler fuer die Statusanzeige im Test.
    public int BytesOutPerSecond { get; private set; }
    public int BytesInPerSecond { get; private set; }
    private int bytesOut, bytesIn;
    private float rateTimer;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private static bool runInBackgroundBefore;

    private static NetSession Create()
    {
        if (Instance != null) return Instance;

        // Im Koop darf das Spiel nie anhalten, nur weil das Fenster den Fokus
        // verliert (Alt+Tab): die eigene Welt wuerde einfrieren, und ein Host
        // im Hintergrund liesse alle anderen nach ein paar Sekunden rausfliegen.
        runInBackgroundBefore = Application.runInBackground;
        Application.runInBackground = true;
        var go = new GameObject("NetSession");
        DontDestroyOnLoad(go);
        return go.AddComponent<NetSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        for (int i = 0; i < MaxPlayers; i++) Slots[i] = new NetSlot();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        ShutdownTransport();
        Instance = null;
        Application.runInBackground = runInBackgroundBefore;
    }

    private void OnApplicationQuit()
    {
        ShutdownTransport();
    }

    /// <summary>Eigener Anzeigename: Steam-Name, sonst Windows-Name (Test).</summary>
    public static string LocalName()
    {
        string arg = CommandLine("-netname");
        if (!string.IsNullOrEmpty(arg)) return arg;
        if (SteamManager.Initialized)
        {
            try { return Steamworks.SteamFriends.GetPersonaName(); }
            catch (Exception) { }
        }
        string user = Environment.UserName;
        return string.IsNullOrEmpty(user) ? "Spieler" : user;
    }

    /// <summary>Wert hinter einem Schalter in der Kommandozeile ("-netname Nick"), sonst null.</summary>
    public static string CommandLine(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    public static bool HasFlag(string key)
    {
        foreach (string a in Environment.GetCommandLineArgs())
            if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Steam benutzen? Nur wenn Steam laeuft und nicht per "-nettcp" der
    /// Test-Weg erzwungen ist.
    /// </summary>
    public static bool SteamWanted => SteamNetTransport.Available && !HasFlag("-nettcp");

    /// <summary>Lobby aufmachen. Ueber Steam, wenn es geht, sonst TCP.</summary>
    public static NetSession Host()
    {
        NetSession s = Create();
        s.StartHost();
        return s;
    }

    /// <summary>
    /// Beitreten. Sieht der Text wie eine Adresse aus (Punkt, Doppelpunkt,
    /// "localhost"), geht es per TCP - sonst ist es ein Steam-Lobbycode.
    /// </summary>
    public static NetSession Join(string codeOrAddress)
    {
        NetSession s = Create();
        s.StartJoin(codeOrAddress);
        return s;
    }

    /// <summary>Einer Steam-Lobby beitreten (Einladung angenommen).</summary>
    public static NetSession JoinSteamLobby(ulong lobbyId)
    {
        NetSession s = Create();
        s.ResetState();
        s.Role = NetRole.Client;
        s.UsesSteam = true;
        NetSteamLobby.JoinLobby(lobbyId, s.OnSteamLobbyEntered, s.OnSteamFailed);
        return s;
    }

    private void ResetState()
    {
        ShutdownTransport();
        for (int i = 0; i < MaxPlayers; i++) Slots[i].Clear();
        LocalSlot = -1;
        hostConnection = -1;
        Run = null;
        Countdown = -1f;
        Code = "";
        endReason = null;
        Settings.Level = 0;
        Settings.Endless = false;
        Settings.Difficulty = Difficulty.Current;
    }

    private void StartHost()
    {
        ResetState();
        Role = NetRole.Host;
        LocalSlot = 0;
        FillLocalSlot(Slots[0]);

        if (SteamWanted)
        {
            UsesSteam = true;
            transport = SteamNetTransport.Host();
            NetSteamLobby.CreateLobby(MaxPlayers, code =>
            {
                Code = code;
                lobbyDirty = true;
            }, OnSteamFailed);
        }
        else
        {
            UsesSteam = false;
            int port = TcpNetTransport.DefaultPort;
            string p = CommandLine("-netport");
            if (!string.IsNullOrEmpty(p)) int.TryParse(p, out port);
            transport = TcpNetTransport.Host(port);
            Code = LocalAddress() + ":" + port;
        }

        Debug.Log($"[Net] Lobby offen ({transport.Describe()}), Code {Code}");
        lobbyDirty = true;
    }

    private void StartJoin(string codeOrAddress)
    {
        ResetState();
        Role = NetRole.Client;
        string text = (codeOrAddress ?? "").Trim();

        if (LooksLikeAddress(text) || !SteamWanted)
        {
            UsesSteam = false;
            string host = text;
            int port = TcpNetTransport.DefaultPort;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text.Substring(colon + 1), out int parsed))
            {
                host = text.Substring(0, colon);
                port = parsed;
            }
            if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
            transport = TcpNetTransport.Join(host, port);
            Code = host + ":" + port;
            Debug.Log($"[Net] Verbinde per TCP mit {Code}");
        }
        else
        {
            UsesSteam = true;
            Code = text.ToUpperInvariant();
            NetSteamLobby.FindByCode(Code, lobby =>
                NetSteamLobby.JoinLobby(lobby, OnSteamLobbyEntered, OnSteamFailed), OnSteamFailed);
        }
    }

    private void OnSteamLobbyEntered(ulong lobby, ulong owner, string code)
    {
        if (Role != NetRole.Client) return;
        Code = code;
        transport = SteamNetTransport.Join(new Steamworks.CSteamID(owner));
        Debug.Log($"[Net] Steam-Lobby betreten, verbinde mit Host {owner}");
    }

    private void OnSteamFailed(string reasonKey)
    {
        Close(reasonKey);
    }

    private static bool LooksLikeAddress(string s) =>
        s.Contains(".") || s.Contains(":") || s.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static string LocalAddress()
    {
        try
        {
            foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return ip.ToString();
            }
        }
        catch (Exception) { }
        return "127.0.0.1";
    }

    private void FillLocalSlot(NetSlot slot)
    {
        slot.Used = true;
        slot.Connection = -1;
        slot.Name = LocalName();
        slot.SteamId = SteamManager.Initialized ? Steamworks.SteamUser.GetSteamID().m_SteamID : 0;
        slot.CharLevels = LocalCharLevels();
        slot.UnlockedMask = LocalUnlockedMask();
        slot.Character = -1;
        slot.Ready = false;
        slot.LastHeard = Time.unscaledTime;
    }

    public static int[] LocalCharLevels()
    {
        var levels = new int[Characters.Count];
        for (int i = 0; i < levels.Length; i++)
        {
            try { levels[i] = Skills.LevelOf(SkillTrees.ForCharacter(i)); }
            catch (Exception) { levels[i] = 0; }
        }
        return levels;
    }

    public static int LocalUnlockedMask()
    {
        int mask = 0;
        for (int i = 0; i < Characters.Count && i < 31; i++)
            if (Characters.IsAvailable(i)) mask |= 1 << i;
        return mask;
    }

    // ==================================================================
    //  Verlassen
    // ==================================================================

    /// <summary>Raus aus der Sitzung - als Host schliesst das die Lobby fuer alle.</summary>
    public static void Leave()
    {
        if (Instance == null) return;
        Instance.Close("net.end.left");
    }

    private void Close(string reasonKey)
    {
        if (Role == NetRole.None && transport == null) return;

        endReason = reasonKey;

        // Der Host sagt Bescheid, statt einfach zu verschwinden - sonst sehen
        // die anderen nur "Verbindung abgerissen".
        if (Role == NetRole.Host && transport != null)
        {
            writer.Clear();
            writer.Byte((byte)NetMsg.Reject);
            writer.Byte(0);
            writer.Byte(ToAll);
            writer.String("net.end.host_left");
            Route(writer.Data, writer.Length, ToAll, -1, true);
        }

        ShutdownTransport();
        NetSteamLobby.Leave();
        Role = NetRole.None;
        Run = null;
        Countdown = -1f;
        Debug.Log($"[Net] Sitzung beendet: {reasonKey}");

        Ended?.Invoke(reasonKey);
        Destroy(gameObject);
    }

    private void ShutdownTransport()
    {
        if (transport == null) return;
        try { transport.Shutdown(); }
        catch (Exception e) { Debug.LogWarning("[Net] Shutdown: " + e.Message); }
        transport = null;
    }

    // ==================================================================
    //  Nachrichten
    // ==================================================================

    /// <summary>Handler fuer eine Nachrichtenart setzen (ersetzt einen alten). from = Absender-Platz.</summary>
    public void On(NetMsg type, Action<int, NetReader> handler) => handlers[type] = handler;

    public void Off(NetMsg type) => handlers.Remove(type);

    /// <summary>
    /// Neue Nachricht anfangen. Den Inhalt in den zurueckgegebenen Writer
    /// schreiben und dann <see cref="Send"/> rufen. Nicht verschachteln -
    /// es gibt nur einen Writer.
    /// </summary>
    public NetWriter Begin(NetMsg type)
    {
        writer.Clear();
        writer.Byte((byte)type);
        writer.Byte((byte)Mathf.Max(0, LocalSlot));
        writer.Byte(ToAll);
        return writer;
    }

    /// <summary>
    /// Die mit <see cref="Begin"/> angefangene Nachricht abschicken.
    /// Unzuverlaessig (reliable = false) ist fuer Zustaende, die ohnehin gleich
    /// wieder neu kommen (Positionen) - geht eine verloren, macht das nichts.
    /// </summary>
    public void Send(byte to, bool reliable = true)
    {
        if (transport == null || writer.Length < 3) return;
        // Bit 7 des Absender-Bytes merkt sich "unzuverlaessig", damit der
        // Host beim Weiterreichen genauso schickt.
        writer.Data[1] = (byte)(Mathf.Max(0, LocalSlot) | (reliable ? 0 : UnreliableBit));
        writer.Data[2] = to;
        Route(writer.Data, writer.Length, to, -1, reliable);
    }

    private const byte UnreliableBit = 0x80;

    /// <summary>An alle anderen.</summary>
    public void SendAll(bool reliable = true) => Send(ToAll, reliable);

    /// <summary>
    /// Nachricht aus einem fertig geschriebenen Inhalt. Fuer Systeme, die
    /// beim Schreiben selbst Nachrichten verschicken muessen (Sprite-Nummern
    /// anmelden) und darum einen eigenen Writer brauchen.
    /// </summary>
    public void SendPayload(NetMsg type, NetWriter payload, byte to, bool reliable)
    {
        NetWriter w = Begin(type);
        w.Bytes(payload.Data, 0, payload.Length);
        Send(to, reliable);
    }

    /// <summary>An einen bestimmten Platz.</summary>
    public void SendTo(int slot, bool reliable = true) => Send((byte)slot, reliable);

    /// <summary>
    /// Nachricht zustellen. Beim Host: an die Verbindungen der Zielplaetze
    /// (ausser dem Absender). Beim Client: immer an den Host - der leitet weiter.
    /// </summary>
    private void Route(byte[] data, int length, byte to, int exceptSlot, bool reliable)
    {
        bytesOut += length;

        if (Role == NetRole.Client)
        {
            if (hostConnection >= 0) transport.Send(hostConnection, data, length, reliable);
            return;
        }

        if (Role != NetRole.Host) return;

        if (to == ToAll)
        {
            for (int i = 1; i < MaxPlayers; i++)
            {
                if (i == exceptSlot || !Slots[i].Used || Slots[i].Connection < 0) continue;
                transport.Send(Slots[i].Connection, data, length, reliable);
            }
        }
        else if (to < MaxPlayers && to != 0 && to != exceptSlot)
        {
            NetSlot slot = Slots[to];
            if (slot.Used && slot.Connection >= 0) transport.Send(slot.Connection, data, length, reliable);
        }
    }

    // ------------------------------------------------- vom Transport

    public void OnTransportConnected(int connection)
    {
        if (Role == NetRole.Client)
        {
            hostConnection = connection;
            lastHeardFromHost = Time.unscaledTime;
            NetWriter w = Begin(NetMsg.Hello);
            w.Int(Protocol);
            w.String(Application.version);
            w.String(LocalName());
            w.ULong(SteamManager.Initialized ? Steamworks.SteamUser.GetSteamID().m_SteamID : 0UL);
            WriteProfile(w);
            Send(ToHost);
            Debug.Log("[Net] Verbunden, sage Hallo.");
        }
        // Host: der Platz wird erst beim Hello vergeben.
    }

    public void OnTransportDisconnected(int connection, string reason)
    {
        if (Role == NetRole.Client)
        {
            if (connection == hostConnection || hostConnection < 0)
            {
                Debug.Log($"[Net] Verbindung zum Host weg: {reason}");
                Close(LocalSlot < 0 ? "net.end.connect_failed" : "net.end.host_lost");
            }
            return;
        }

        for (int i = 1; i < MaxPlayers; i++)
        {
            if (Slots[i].Used && Slots[i].Connection == connection)
            {
                Debug.Log($"[Net] {Slots[i].Name} (Platz {i}) ist weg: {reason}");
                DropSlot(i);
                break;
            }
        }
    }

    public void OnTransportData(int connection, byte[] data, int offset, int length)
    {
        if (length < 3) return;
        bytesIn += length;

        NetMsg type = (NetMsg)data[offset];
        bool reliable = (data[offset + 1] & UnreliableBit) == 0;
        int from = data[offset + 1] & 0x7F;
        byte to = data[offset + 2];

        if (Role == NetRole.Host)
        {
            int slot = SlotOfConnection(connection);
            if (slot < 0)
            {
                // Noch kein Platz: nur das Hallo zaehlt.
                if (type == NetMsg.Hello) HandleHello(connection, data, offset + 3, length - 3);
                return;
            }

            Slots[slot].LastHeard = Time.unscaledTime;
            from = slot;
            data[offset + 1] = (byte)(slot | (reliable ? 0 : UnreliableBit));

            // Weiterleiten, was nicht (nur) fuer den Host ist.
            if (to != ToHost && to != 0)
            {
                if (offset != 0)
                {
                    if (relay.Length < length) relay = new byte[Mathf.NextPowerOfTwo(length)];
                    Buffer.BlockCopy(data, offset, relay, 0, length);
                    Route(relay, length, to, slot, reliable);
                }
                else
                {
                    Route(data, length, to, slot, reliable);
                }
                if (to != ToAll) return;
            }
        }
        else
        {
            lastHeardFromHost = Time.unscaledTime;
        }

        Dispatch(type, from, data, offset + 3, length - 3);
    }

    private void Dispatch(NetMsg type, int from, byte[] data, int offset, int length)
    {
        reader.Reset(data, offset, length);
        try
        {
            switch (type)
            {
                case NetMsg.Welcome: HandleWelcome(reader); return;
                case NetMsg.Reject: Close(reader.String()); return;
                case NetMsg.LobbyState: HandleLobbyState(reader); return;
                case NetMsg.PickChar: HandlePickChar(from, reader); return;
                case NetMsg.SetReady: HandleSetReady(from, reader); return;
                case NetMsg.ProfileInfo: HandleProfile(from, reader); return;
                case NetMsg.StartRun: HandleStartRun(reader); return;
                case NetMsg.BackToLobby: HandleBackToLobby(from); return;
                case NetMsg.Ping: HandlePing(from, reader); return;
                case NetMsg.Pong: HandlePong(from, reader); return;
            }

            if (handlers.TryGetValue(type, out var handler)) handler(from, reader);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Net] Fehler bei {type} von {from}: {e}");
        }
    }

    private int SlotOfConnection(int connection)
    {
        for (int i = 1; i < MaxPlayers; i++)
            if (Slots[i].Used && Slots[i].Connection == connection) return i;
        return -1;
    }

    // ==================================================================
    //  Lobby-Protokoll
    // ==================================================================

    private void HandleHello(int connection, byte[] data, int offset, int length)
    {
        var r = new NetReader(data, offset, length);
        int protocol = r.Int();
        string version = r.String();
        string name = r.String();
        ulong steamId = r.ULong();

        string reject = null;
        if (protocol != Protocol || version != Application.version) reject = "net.reject.version";
        else if (Run != null) reject = "net.reject.running";

        int slot = -1;
        if (reject == null)
        {
            for (int i = 1; i < MaxPlayers; i++)
            {
                if (!Slots[i].Used) { slot = i; break; }
            }
            if (slot < 0) reject = "net.reject.full";
        }

        if (reject != null)
        {
            writer.Clear();
            writer.Byte((byte)NetMsg.Reject);
            writer.Byte(0);
            writer.Byte((byte)ToHost);
            writer.String(reject);
            transport.Send(connection, writer.Data, writer.Length, true);
            Debug.Log($"[Net] {name} abgewiesen: {reject}");
            return;
        }

        NetSlot s = Slots[slot];
        s.Clear();
        s.Used = true;
        s.Connection = connection;
        s.Name = string.IsNullOrWhiteSpace(name) ? "Spieler " + (slot + 1) : name.Trim();
        s.SteamId = steamId;
        s.LastHeard = Time.unscaledTime;
        ReadProfile(s, r);

        writer.Clear();
        writer.Byte((byte)NetMsg.Welcome);
        writer.Byte(0);
        writer.Byte((byte)slot);
        writer.Byte((byte)slot);
        transport.Send(connection, writer.Data, writer.Length, true);

        Debug.Log($"[Net] {s.Name} sitzt auf Platz {slot}.");
        CancelCountdown();
        lobbyDirty = true;
    }

    private void HandleWelcome(NetReader r)
    {
        LocalSlot = r.Byte();
        Debug.Log($"[Net] Aufgenommen auf Platz {LocalSlot}.");
        Changed?.Invoke();
    }

    private static void WriteProfile(NetWriter w)
    {
        int[] levels = LocalCharLevels();
        w.Byte((byte)levels.Length);
        foreach (int l in levels) w.VarUInt((uint)Mathf.Max(0, l));
        w.Int(LocalUnlockedMask());
    }

    private static void ReadProfile(NetSlot s, NetReader r)
    {
        int count = r.Byte();
        s.CharLevels = new int[count];
        for (int i = 0; i < count; i++) s.CharLevels[i] = (int)r.VarUInt();
        s.UnlockedMask = r.Int();
    }

    /// <summary>Nach Skilltree/Shop: neue Level an den Host melden.</summary>
    public void SendProfile()
    {
        if (!Joined) return;
        if (Role == NetRole.Host)
        {
            Slots[0].CharLevels = LocalCharLevels();
            Slots[0].UnlockedMask = LocalUnlockedMask();
            lobbyDirty = true;
            return;
        }
        WriteProfile(Begin(NetMsg.ProfileInfo));
        Send(ToHost);
    }

    private void HandleProfile(int from, NetReader r)
    {
        if (Role != NetRole.Host || from <= 0 || from >= MaxPlayers) return;
        ReadProfile(Slots[from], r);
        lobbyDirty = true;
    }

    /// <summary>Charakter waehlen. -1 = abwaehlen. Jeder Charakter nur einmal pro Lobby.</summary>
    public void PickCharacter(int character)
    {
        if (!Joined) return;
        if (Role == NetRole.Host) ApplyPick(0, character);
        else
        {
            Begin(NetMsg.PickChar).VarInt(character);
            Send(ToHost);
        }
    }

    private void HandlePickChar(int from, NetReader r)
    {
        if (Role != NetRole.Host) return;
        ApplyPick(from, r.VarInt());
    }

    private void ApplyPick(int slot, int character)
    {
        if (slot < 0 || slot >= MaxPlayers || !Slots[slot].Used || Run != null) return;
        if (character >= Characters.Count) return;
        if (character >= 0 && IsTaken(character, slot)) return;
        if (character >= 0 && !Slots[slot].HasUnlocked(character)) return;

        if (Slots[slot].Character != character)
        {
            Slots[slot].Character = character;
            Slots[slot].Ready = false;
            CancelCountdown();
            lobbyDirty = true;
        }
    }

    /// <summary>Hat schon jemand anderes diesen Charakter?</summary>
    public bool IsTaken(int character, int exceptSlot)
    {
        for (int i = 0; i < MaxPlayers; i++)
            if (i != exceptSlot && Slots[i].Used && Slots[i].Character == character) return true;
        return false;
    }

    public void SetReady(bool ready)
    {
        if (!Joined) return;
        if (Role == NetRole.Host) ApplyReady(0, ready);
        else
        {
            Begin(NetMsg.SetReady).Bool(ready);
            Send(ToHost);
        }
    }

    private void HandleSetReady(int from, NetReader r)
    {
        if (Role != NetRole.Host) return;
        ApplyReady(from, r.Bool());
    }

    private void ApplyReady(int slot, bool ready)
    {
        if (slot < 0 || slot >= MaxPlayers || !Slots[slot].Used || Run != null) return;
        if (ready && Slots[slot].Character < 0) return;
        if (Slots[slot].Ready == ready) return;
        Slots[slot].Ready = ready;
        if (!ready) CancelCountdown();
        lobbyDirty = true;
    }

    /// <summary>Nur der Host: Welt, Modus, Schwierigkeit.</summary>
    public void ChangeSettings(int level, bool endless, Difficulty.Level difficulty)
    {
        if (Role != NetRole.Host || Run != null) return;
        if (Settings.Level == level && Settings.Endless == endless && Settings.Difficulty == difficulty) return;
        Settings.Level = level;
        Settings.Endless = endless;
        Settings.Difficulty = difficulty;
        // Wer bereit war, hat unter anderen Bedingungen zugesagt.
        for (int i = 0; i < MaxPlayers; i++) Slots[i].Ready = false;
        CancelCountdown();
        lobbyDirty = true;
    }

    public int PlayerCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxPlayers; i++) if (Slots[i].Used) n++;
            return n;
        }
    }

    /// <summary>Alle Plaetze bereit, alle mit Charakter, alle in der Lobby?</summary>
    public bool AllReady
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxPlayers; i++)
            {
                NetSlot s = Slots[i];
                if (!s.Used) continue;
                if (!s.Ready || s.Character < 0 || s.InRun) return false;
                n++;
            }
            return n >= MinPlayers;
        }
    }

    /// <summary>
    /// Mindestens zwei - allein ist es kein Koop. In Testbuilds geht es auch
    /// allein (zum Ausprobieren ohne zweiten Rechner).
    /// </summary>
    public static int MinPlayers => Application.isEditor || Debug.isDebugBuild ? 1 : 2;

    private void CancelCountdown()
    {
        if (Countdown < 0f) return;
        Countdown = -1f;
        lobbyDirty = true;
    }

    private void WriteLobby(NetWriter w)
    {
        w.String(Code);
        w.VarUInt((uint)Mathf.Max(0, Settings.Level));
        w.Bool(Settings.Endless);
        w.Byte((byte)Settings.Difficulty);
        w.Float(Countdown);
        for (int i = 0; i < MaxPlayers; i++)
        {
            NetSlot s = Slots[i];
            w.Bool(s.Used);
            if (!s.Used) continue;
            w.String(s.Name);
            w.ULong(s.SteamId);
            w.VarInt(s.Character);
            w.Bool(s.Ready);
            w.Bool(s.InRun);
            w.VarUInt((uint)Mathf.Max(0, s.PingMs));
            w.Byte((byte)s.CharLevels.Length);
            foreach (int l in s.CharLevels) w.VarUInt((uint)Mathf.Max(0, l));
            w.Int(s.UnlockedMask);
        }
    }

    private void HandleLobbyState(NetReader r)
    {
        if (Role != NetRole.Client) return;
        Code = r.String();
        Settings.Level = (int)r.VarUInt();
        Settings.Endless = r.Bool();
        Settings.Difficulty = (Difficulty.Level)r.Byte();
        Countdown = r.Float();
        for (int i = 0; i < MaxPlayers; i++)
        {
            NetSlot s = Slots[i];
            bool used = r.Bool();
            if (!used)
            {
                if (s.Used) SlotLeft?.Invoke(i);
                s.Clear();
                continue;
            }
            s.Used = true;
            s.Name = r.String();
            s.SteamId = r.ULong();
            s.Character = r.VarInt();
            s.Ready = r.Bool();
            s.InRun = r.Bool();
            s.PingMs = (int)r.VarUInt();
            int count = r.Byte();
            s.CharLevels = new int[count];
            for (int k = 0; k < count; k++) s.CharLevels[k] = (int)r.VarUInt();
            s.UnlockedMask = r.Int();
        }
        Changed?.Invoke();
    }

    private void BroadcastLobby()
    {
        lobbyDirty = false;
        if (Role != NetRole.Host) return;
        WriteLobby(Begin(NetMsg.LobbyState));
        SendAll();
        NetSteamLobby.SetMemberCount(PlayerCount);
        Changed?.Invoke();
    }

    private void DropSlot(int slot)
    {
        if (slot <= 0 || slot >= MaxPlayers || !Slots[slot].Used) return;
        int connection = Slots[slot].Connection;
        Slots[slot].Clear();
        if (connection >= 0) transport?.Disconnect(connection);
        SlotLeft?.Invoke(slot);
        CancelCountdown();
        lobbyDirty = true;
    }

    // ------------------------------------------------------- Start

    private void BeginRunAsHost()
    {
        var info = new NetRunInfo
        {
            Seed = UnityEngine.Random.Range(1, int.MaxValue),
            Level = Settings.Level,
            Endless = Settings.Endless,
            Difficulty = Settings.Difficulty,
        };

        HubLevelSelectUI select = FindAnyObjectByType<HubLevelSelectUI>(FindObjectsInactive.Include);
        if (select != null && info.Level >= 0 && info.Level < select.Levels.Count)
        {
            HubLevelSelectUI.LevelEntry e = select.Levels[info.Level];
            info.MapId = e.mapId;
            info.Scene = MapSceneSystem.ResolveScene(e.sceneToLoad, e.mapId);
        }

        for (int i = 0; i < MaxPlayers; i++)
        {
            info.Characters[i] = Slots[i].Used ? Slots[i].Character : -1;
            if (Slots[i].Used) info.PlayerCount++;
        }

        NetWriter w = Begin(NetMsg.StartRun);
        WriteRunInfo(w, info);
        SendAll();
        StartRunLocally(info);
    }

    private static void WriteRunInfo(NetWriter w, NetRunInfo info)
    {
        w.Int(info.Seed);
        w.VarUInt((uint)Mathf.Max(0, info.Level));
        w.VarInt(info.MapId);
        w.String(info.Scene);
        w.Bool(info.Endless);
        w.Byte((byte)info.Difficulty);
        for (int i = 0; i < MaxPlayers; i++) w.VarInt(info.Characters[i]);
    }

    private void HandleStartRun(NetReader r)
    {
        if (Role != NetRole.Client) return;
        var info = new NetRunInfo
        {
            Seed = r.Int(),
            Level = (int)r.VarUInt(),
            MapId = r.VarInt(),
            Scene = r.String(),
            Endless = r.Bool(),
            Difficulty = (Difficulty.Level)r.Byte(),
        };
        for (int i = 0; i < MaxPlayers; i++)
        {
            info.Characters[i] = r.VarInt();
            if (info.Characters[i] >= 0) info.PlayerCount++;
        }
        StartRunLocally(info);
    }

    private void StartRunLocally(NetRunInfo info)
    {
        Run = info;
        Countdown = -1f;
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (!Slots[i].Used) continue;
            Slots[i].InRun = true;
            Slots[i].Ready = false;
        }
        Debug.Log($"[Net] Lauf startet: Welt {info.MapId} ({info.Scene}), {info.PlayerCount} Spieler, Seed {info.Seed}");
        RunStarting?.Invoke(info);
    }

    /// <summary>Der Lauf ist (fuer diesen Spieler) vorbei - zurueck in der Lobby.</summary>
    public void ReturnToLobby()
    {
        Run = null;
        if (LocalSlot >= 0) Slots[LocalSlot].InRun = false;

        if (Role == NetRole.Host) lobbyDirty = true;
        else if (Joined)
        {
            Begin(NetMsg.BackToLobby);
            Send(ToHost);
        }
        Changed?.Invoke();
    }

    private void HandleBackToLobby(int from)
    {
        if (Role != NetRole.Host || from <= 0 || from >= MaxPlayers) return;
        Slots[from].InRun = false;
        lobbyDirty = true;
    }

    // -------------------------------------------------------- Ping

    private void HandlePing(int from, NetReader r)
    {
        float sent = r.Float();
        Begin(NetMsg.Pong).Float(sent);
        SendTo(from);
    }

    // Nur der Host pingt (alle 2 s) - das ist zugleich das Lebenszeichen fuer
    // die Clients. Die Antwort ist das Lebenszeichen der Clients, und die
    // gemessene Zeit steht in der Lobby bei jedem Platz.
    private void HandlePong(int from, NetReader r)
    {
        if (Role != NetRole.Host || from <= 0 || from >= MaxPlayers) return;
        int ms = Mathf.RoundToInt((Time.unscaledTime - r.Float()) * 1000f);
        if (Mathf.Abs(ms - Slots[from].PingMs) > 15 && Run == null) lobbyDirty = true;
        Slots[from].PingMs = ms;
    }

    // ==================================================================
    //  Takt
    // ==================================================================

    private void Update()
    {
        if (transport != null)
        {
            try { transport.Poll(this); }
            catch (Exception e) { Debug.LogError("[Net] Poll: " + e); }
        }

        if (Role == NetRole.None) return;

        float now = Time.unscaledTime;

        if (Role == NetRole.Host)
        {
            // Stumme Mitspieler rauswerfen (Steam merkt das meist selbst, TCP nicht immer).
            for (int i = 1; i < MaxPlayers; i++)
                if (Slots[i].Used && now - Slots[i].LastHeard > Timeout) DropSlot(i);

            UpdateCountdown();
            if (lobbyDirty) BroadcastLobby();
        }
        else if (Joined && now - lastHeardFromHost > Timeout)
        {
            Close("net.end.host_lost");
            return;
        }

        pingTimer -= Time.unscaledDeltaTime;
        if (pingTimer <= 0f && Role == NetRole.Host)
        {
            pingTimer = 2f;
            Begin(NetMsg.Ping).Float(now);
            SendAll();
        }

        rateTimer += Time.unscaledDeltaTime;
        if (rateTimer >= 1f)
        {
            BytesOutPerSecond = Mathf.RoundToInt(bytesOut / rateTimer);
            BytesInPerSecond = Mathf.RoundToInt(bytesIn / rateTimer);
            bytesOut = bytesIn = 0;
            rateTimer = 0f;
        }
    }

    private void UpdateCountdown()
    {
        if (Run != null) return;

        if (!AllReady)
        {
            CancelCountdown();
            return;
        }

        if (Countdown < 0f)
        {
            Countdown = StartCountdown;
            lobbyDirty = true;
            return;
        }

        float before = Countdown;
        Countdown -= Time.unscaledDeltaTime;
        // Ganze Sekunden an alle, damit die Zahl ueberall gleich tickt.
        if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown)) lobbyDirty = true;

        if (Countdown <= 0f)
        {
            Countdown = -1f;
            BeginRunAsHost();
        }
    }

    /// <summary>Nur Host: einen Mitspieler aus der Lobby werfen.</summary>
    public void Kick(int slot)
    {
        if (Role != NetRole.Host || slot <= 0 || Run != null) return;
        if (!Slots[slot].Used) return;
        writer.Clear();
        writer.Byte((byte)NetMsg.Reject);
        writer.Byte(0);
        writer.Byte((byte)slot);
        writer.String("net.reject.kicked");
        if (Slots[slot].Connection >= 0) transport.Send(Slots[slot].Connection, writer.Data, writer.Length, true);
        DropSlot(slot);
    }

    /// <summary>Platz eines Mitspielers wird frei (Lauf: Abbruch oder Disconnect) - fuer Systeme im Lauf.</summary>
    public string EndReason => endReason;
}
