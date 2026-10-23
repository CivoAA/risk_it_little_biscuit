using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

/// <summary>
/// Verbindung ueber Steam Networking Sockets (P2P ueber das Steam Datagram
/// Relay). Niemand braucht eine IP oder eine Portfreigabe - der Host wird
/// ueber seine SteamID gefunden, die die Lobby verraet
/// (<see cref="NetSteamLobby"/>).
///
/// Die Rueckrufe (Verbindung kommt, geht) liefert Steam in
/// SteamAPI.RunCallbacks, das der <see cref="SteamManager"/> jeden Frame
/// aufruft. Daten holt <see cref="Poll"/> selbst ab.
/// </summary>
public sealed class SteamNetTransport : INetTransport
{
    private const int MaxMessagesPerPoll = 256;

    private readonly bool isHost;
    private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
    private HSteamNetPollGroup pollGroup = HSteamNetPollGroup.Invalid;
    private readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback;

    private readonly Dictionary<uint, HSteamNetConnection> connections = new Dictionary<uint, HSteamNetConnection>();
    private readonly HashSet<uint> announced = new HashSet<uint>();
    private readonly Queue<(int conn, bool up, string reason)> pending = new Queue<(int, bool, string)>();
    private readonly IntPtr[] messages = new IntPtr[MaxMessagesPerPoll];
    private byte[] scratch = new byte[2048];
    private IntPtr sendBuffer = IntPtr.Zero;
    private int sendBufferSize;
    private bool shut;
    private readonly string description;

    public static bool Available => SteamManager.Initialized;

    private SteamNetTransport(bool host, string text)
    {
        isHost = host;
        description = text;
        SteamNetworkingUtils.InitRelayNetworkAccess();
        statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        pollGroup = SteamNetworkingSockets.CreatePollGroup();
    }

    public static SteamNetTransport Host()
    {
        var t = new SteamNetTransport(true, "Steam Host");
        t.listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
        return t;
    }

    public static SteamNetTransport Join(CSteamID host)
    {
        var t = new SteamNetTransport(false, "Steam -> " + host.m_SteamID);
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(host);
        HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
        t.connections[conn.m_HSteamNetConnection] = conn;
        SteamNetworkingSockets.SetConnectionPollGroup(conn, t.pollGroup);
        return t;
    }

    public string Describe() => description;

    /// <summary>Die SteamID hinter einer Verbindung (fuer Namen und Doppelte).</summary>
    public CSteamID RemoteId(int connection)
    {
        if (!connections.TryGetValue((uint)connection, out HSteamNetConnection conn)) return CSteamID.Nil;
        if (!SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info)) return CSteamID.Nil;
        return info.m_identityRemote.GetSteamID();
    }

    private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t cb)
    {
        if (shut) return;

        HSteamNetConnection conn = cb.m_hConn;
        uint id = conn.m_HSteamNetConnection;
        ESteamNetworkingConnectionState state = cb.m_info.m_eState;

        switch (state)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                // Nur der Host nimmt an - und nur auf seinem Lausch-Socket.
                if (isHost && cb.m_info.m_hListenSocket == listenSocket)
                {
                    if (SteamNetworkingSockets.AcceptConnection(conn) == EResult.k_EResultOK)
                    {
                        connections[id] = conn;
                        SteamNetworkingSockets.SetConnectionPollGroup(conn, pollGroup);
                    }
                    else
                    {
                        SteamNetworkingSockets.CloseConnection(conn, 0, "accept failed", false);
                    }
                }
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                if (connections.ContainsKey(id) && announced.Add(id)) pending.Enqueue(((int)id, true, null));
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                string reason = cb.m_info.m_szEndDebug;
                SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                if (connections.Remove(id) || announced.Contains(id))
                {
                    announced.Remove(id);
                    pending.Enqueue(((int)id, false, string.IsNullOrEmpty(reason) ? state.ToString() : reason));
                }
                break;
        }
    }

    public void Poll(INetTransportListener l)
    {
        if (shut) return;

        while (pending.Count > 0)
        {
            var (conn, up, reason) = pending.Dequeue();
            if (up) l.OnTransportConnected(conn);
            else l.OnTransportDisconnected(conn, reason);
        }

        int count;
        do
        {
            count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, MaxMessagesPerPoll);
            for (int i = 0; i < count; i++)
            {
                SteamNetworkingMessage_t msg = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                int size = msg.m_cbSize;
                if (scratch.Length < size) scratch = new byte[Mathf.NextPowerOfTwo(size)];
                Marshal.Copy(msg.m_pData, scratch, 0, size);
                int conn = (int)msg.m_conn.m_HSteamNetConnection;
                SteamNetworkingMessage_t.Release(messages[i]);

                // Kopie, weil der Empfaenger die Bytes evtl. aufbewahrt.
                var copy = new byte[size];
                Buffer.BlockCopy(scratch, 0, copy, 0, size);
                l.OnTransportData(conn, copy, 0, size);
            }
        }
        while (count == MaxMessagesPerPoll);
    }

    public void Send(int connection, byte[] data, int length, bool reliable)
    {
        if (shut || !connections.TryGetValue((uint)connection, out HSteamNetConnection conn)) return;

        if (sendBufferSize < length)
        {
            if (sendBuffer != IntPtr.Zero) Marshal.FreeHGlobal(sendBuffer);
            sendBufferSize = Mathf.NextPowerOfTwo(Mathf.Max(1024, length));
            sendBuffer = Marshal.AllocHGlobal(sendBufferSize);
        }
        Marshal.Copy(data, 0, sendBuffer, length);

        int flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable
                             : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
        SteamNetworkingSockets.SendMessageToConnection(conn, sendBuffer, (uint)length, flags, out long _);
    }

    public void Disconnect(int connection)
    {
        if (!connections.TryGetValue((uint)connection, out HSteamNetConnection conn)) return;
        SteamNetworkingSockets.CloseConnection(conn, 0, "bye", true);
        connections.Remove((uint)connection);
        if (announced.Remove((uint)connection)) pending.Enqueue((connection, false, "disconnect"));
    }

    public void Shutdown()
    {
        if (shut) return;
        shut = true;

        foreach (HSteamNetConnection conn in connections.Values)
            SteamNetworkingSockets.CloseConnection(conn, 0, "shutdown", true);
        connections.Clear();

        if (listenSocket != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listenSocket);
        if (pollGroup != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(pollGroup);
        statusCallback?.Dispose();

        if (sendBuffer != IntPtr.Zero) Marshal.FreeHGlobal(sendBuffer);
        sendBuffer = IntPtr.Zero;
    }
}
