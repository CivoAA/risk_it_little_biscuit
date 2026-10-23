using System;
using Steamworks;
using UnityEngine;

/// <summary>
/// Die Steam-Seite einer Koop-Lobby: anlegen, ueber einen Code finden,
/// beitreten, Freunde einladen, Einladungen annehmen.
///
/// Die Lobby ist "unsichtbar" (k_ELobbyTypeInvisible): sie taucht in keiner
/// Liste auf und ist fuer Freunde nicht sichtbar - finden kann man sie nur mit
/// dem Code (Lobby-Daten "code") oder ueber eine Einladung. Oeffentliche
/// Gruppen gibt es also nicht.
///
/// Die eigentliche Verbindung laeuft danach ueber <see cref="SteamNetTransport"/>
/// zum Besitzer der Lobby. Die Lobby selbst ist nur das Telefonbuch.
/// </summary>
public static class NetSteamLobby
{
    private const string CodeKey = "code";
    private const string VersionKey = "ver";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 6;

    private static CSteamID current = CSteamID.Nil;
    private static CallResult<LobbyCreated_t> created;
    private static CallResult<LobbyMatchList_t> listed;
    private static CallResult<LobbyEnter_t> entered;
    private static Callback<GameLobbyJoinRequested_t> joinRequested;
    private static bool launchChecked;

    /// <summary>Eine Einladung wurde angenommen, waehrend das Spiel lief oder es gestartet hat.</summary>
    public static ulong PendingInvite { get; private set; }

    public static bool InLobby => current != CSteamID.Nil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = CSteamID.Nil;
        created = null;
        listed = null;
        entered = null;
        joinRequested = null;
        launchChecked = false;
        PendingInvite = 0;
    }

    /// <summary>
    /// Einladungen abonnieren. Einmal nach dem Steam-Start aufrufen (das
    /// Hauptmenue macht das). Kam das Spiel ueber eine Einladung hoch
    /// ("+connect_lobby id" in der Kommandozeile), merkt es sich die Lobby.
    /// </summary>
    public static void Listen()
    {
        if (!SteamManager.Initialized) return;

        if (joinRequested == null)
        {
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(cb =>
            {
                PendingInvite = cb.m_steamIDLobby.m_SteamID;
                Debug.Log($"[Net] Einladung angenommen: Lobby {PendingInvite}");
            });
        }

        if (!launchChecked)
        {
            launchChecked = true;
            string arg = NetSession.CommandLine("+connect_lobby");
            if (!string.IsNullOrEmpty(arg) && ulong.TryParse(arg, out ulong id)) PendingInvite = id;
        }
    }

    /// <summary>Die gemerkte Einladung abholen (danach ist sie weg).</summary>
    public static ulong TakeInvite()
    {
        ulong id = PendingInvite;
        PendingInvite = 0;
        return id;
    }

    public static string NewCode()
    {
        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];
        return new string(chars);
    }

    /// <summary>Normalisiert eine Eingabe: Grossbuchstaben, ohne Leerzeichen und Striche, 0->O wird nicht geraten.</summary>
    public static string CleanCode(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in (text ?? "").ToUpperInvariant())
            if (CodeAlphabet.IndexOf(c) >= 0) sb.Append(c);
        return sb.ToString();
    }

    public static void CreateLobby(int maxMembers, Action<string> onCode, Action<string> onFail)
    {
        if (!SteamManager.Initialized) { onFail?.Invoke("net.end.no_steam"); return; }

        created = CallResult<LobbyCreated_t>.Create((cb, failure) =>
        {
            if (failure || cb.m_eResult != EResult.k_EResultOK)
            {
                onFail?.Invoke("net.end.lobby_failed");
                return;
            }

            current = new CSteamID(cb.m_ulSteamIDLobby);
            string code = NewCode();
            SteamMatchmaking.SetLobbyData(current, CodeKey, code);
            SteamMatchmaking.SetLobbyData(current, VersionKey, NetSession.Protocol + "/" + Application.version);
            SteamMatchmaking.SetLobbyJoinable(current, true);
            // Freunde sehen "Spiel beitreten" ueber Rich Presence nicht - nur Einladungen und Code.
            Debug.Log($"[Net] Steam-Lobby {current.m_SteamID} angelegt, Code {code}");
            onCode?.Invoke(code);
        });
        created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, maxMembers));
    }

    /// <summary>Lobby mit diesem Code suchen.</summary>
    public static void FindByCode(string code, Action<ulong> onFound, Action<string> onFail)
    {
        if (!SteamManager.Initialized) { onFail?.Invoke("net.end.no_steam"); return; }

        code = CleanCode(code);
        if (code.Length != CodeLength) { onFail?.Invoke("net.end.bad_code"); return; }

        SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(4);

        listed = CallResult<LobbyMatchList_t>.Create((cb, failure) =>
        {
            if (failure || cb.m_nLobbiesMatching == 0)
            {
                onFail?.Invoke("net.end.not_found");
                return;
            }
            onFound?.Invoke(SteamMatchmaking.GetLobbyByIndex(0).m_SteamID);
        });
        listed.Set(SteamMatchmaking.RequestLobbyList());
    }

    /// <summary>Beitreten. onEntered bekommt Lobby, Besitzer (Host) und Code.</summary>
    public static void JoinLobby(ulong lobbyId, Action<ulong, ulong, string> onEntered, Action<string> onFail)
    {
        if (!SteamManager.Initialized) { onFail?.Invoke("net.end.no_steam"); return; }

        entered = CallResult<LobbyEnter_t>.Create((cb, failure) =>
        {
            if (failure || cb.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                onFail?.Invoke("net.end.join_failed");
                return;
            }

            current = new CSteamID(cb.m_ulSteamIDLobby);
            string version = SteamMatchmaking.GetLobbyData(current, VersionKey);
            if (!string.IsNullOrEmpty(version) && version != NetSession.Protocol + "/" + Application.version)
            {
                Leave();
                onFail?.Invoke("net.reject.version");
                return;
            }

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(current);
            onEntered?.Invoke(current.m_SteamID, owner.m_SteamID, SteamMatchmaking.GetLobbyData(current, CodeKey));
        });
        entered.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobbyId)));
    }

    /// <summary>Steam-Overlay mit der Freundesliste zum Einladen.</summary>
    public static bool OpenInviteDialog()
    {
        if (!SteamManager.Initialized || current == CSteamID.Nil) return false;
        SteamFriends.ActivateGameOverlayInviteDialog(current);
        return true;
    }

    /// <summary>Volle Lobby nicht mehr betretbar machen (Host).</summary>
    public static void SetMemberCount(int count)
    {
        if (!SteamManager.Initialized || current == CSteamID.Nil) return;
        SteamMatchmaking.SetLobbyJoinable(current, count < NetSession.MaxPlayers);
    }

    public static void Leave()
    {
        if (!SteamManager.Initialized || current == CSteamID.Nil)
        {
            current = CSteamID.Nil;
            return;
        }
        SteamMatchmaking.LeaveLobby(current);
        current = CSteamID.Nil;
    }
}
