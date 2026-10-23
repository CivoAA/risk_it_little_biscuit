using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Was <see cref="NetSession"/> von einer Verbindung braucht: aufbauen,
/// Bytes schicken, Bytes und Ereignisse abholen. Zwei Umsetzungen:
///
///  * <see cref="SteamNetTransport"/> - das echte Spiel. Laeuft ueber das
///    Steam-Relay (keine Portfreigabe, keine IP-Adressen, NAT egal).
///  * <see cref="TcpNetTransport"/> - zum Testen ohne Steam: zwei Fenster auf
///    einem Rechner oder im LAN. Alles ist zuverlaessig, auch was als
///    "unzuverlaessig" geschickt wird - fuer Tests genau richtig.
///
/// Verbindungen heissen nach aussen nur Zahlen. Der Host hat viele, ein Client
/// genau eine (zum Host).
/// </summary>
public interface INetTransport
{
    /// <summary>Eingesammelte Ereignisse an den Empfaenger geben. Einmal pro Frame, im Hauptthread.</summary>
    void Poll(INetTransportListener listener);

    void Send(int connection, byte[] data, int length, bool reliable);

    void Disconnect(int connection);

    /// <summary>Alles schliessen. Danach ist der Transport unbrauchbar.</summary>
    void Shutdown();

    /// <summary>Kurzer Text fuer Logs und die Lobby ("Steam", "TCP 127.0.0.1:27777").</summary>
    string Describe();
}

public interface INetTransportListener
{
    void OnTransportConnected(int connection);
    void OnTransportDisconnected(int connection, string reason);
    void OnTransportData(int connection, byte[] data, int offset, int length);
}

/// <summary>
/// Test-Verbindung ueber TCP. Ein Host lauscht auf <see cref="DefaultPort"/>,
/// Clients verbinden sich mit IP:Port. Jede Nachricht geht mit vorangestellter
/// Laenge (4 Byte) raus. Empfangen wird in eigenen Threads, abgeholt in
/// <see cref="Poll"/> - die Spiellogik sieht also nur den Hauptthread.
/// </summary>
public sealed class TcpNetTransport : INetTransport
{
    public const int DefaultPort = 27777;

    /// <summary>Groesser ist keine echte Nachricht - schuetzt vor kaputten Laengen.</summary>
    private const int MaxMessage = 4 * 1024 * 1024;

    private enum Kind { Connected, Disconnected, Data }

    private struct Event
    {
        public Kind Kind;
        public int Connection;
        public byte[] Data;
        public string Reason;
    }

    private sealed class Peer
    {
        public int Id;
        public TcpClient Client;
        public NetworkStream Stream;
        public readonly object SendLock = new object();
        public volatile bool Closed;
    }

    private readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
    private readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
    private readonly object peersLock = new object();
    private TcpListener listener;
    private Thread acceptThread;
    private volatile bool running = true;
    private int nextId = 1;
    private string description = "TCP";

    public static TcpNetTransport Host(int port)
    {
        var t = new TcpNetTransport();
        t.listener = new TcpListener(IPAddress.Any, port);
        t.listener.Start();
        t.description = "TCP Host :" + port;
        t.acceptThread = new Thread(t.AcceptLoop) { IsBackground = true, Name = "NetAccept" };
        t.acceptThread.Start();
        return t;
    }

    /// <summary>Verbindet sich im Hintergrund - Erfolg oder Fehler kommen als Ereignis.</summary>
    public static TcpNetTransport Join(string host, int port)
    {
        var t = new TcpNetTransport { description = "TCP " + host + ":" + port };
        var thread = new Thread(() => t.ConnectLoop(host, port)) { IsBackground = true, Name = "NetConnect" };
        thread.Start();
        return t;
    }

    public string Describe() => description;

    private void AcceptLoop()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                Add(client);
            }
            catch (Exception)
            {
                if (!running) return;
            }
        }
    }

    private void ConnectLoop(string host, int port)
    {
        try
        {
            var client = new TcpClient();
            client.Connect(host, port);
            Add(client);
        }
        catch (Exception e)
        {
            events.Enqueue(new Event { Kind = Kind.Disconnected, Connection = 0, Reason = e.Message });
        }
    }

    private void Add(TcpClient client)
    {
        client.NoDelay = true;
        var peer = new Peer { Client = client, Stream = client.GetStream() };
        lock (peersLock)
        {
            peer.Id = nextId++;
            peers[peer.Id] = peer;
        }
        events.Enqueue(new Event { Kind = Kind.Connected, Connection = peer.Id });

        var thread = new Thread(() => ReadLoop(peer)) { IsBackground = true, Name = "NetRead" + peer.Id };
        thread.Start();
    }

    private void ReadLoop(Peer peer)
    {
        var header = new byte[4];
        string reason = "closed";
        try
        {
            while (running && !peer.Closed)
            {
                if (!ReadExactly(peer.Stream, header, 4)) break;
                int length = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
                if (length < 0 || length > MaxMessage) { reason = "bad length"; break; }

                var payload = new byte[length];
                if (!ReadExactly(peer.Stream, payload, length)) break;
                events.Enqueue(new Event { Kind = Kind.Data, Connection = peer.Id, Data = payload });
            }
        }
        catch (Exception e)
        {
            reason = e.Message;
        }

        Close(peer, reason);
    }

    private static bool ReadExactly(NetworkStream stream, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    private void Close(Peer peer, string reason)
    {
        if (peer.Closed) return;
        peer.Closed = true;
        try { peer.Client.Close(); } catch (Exception) { }
        lock (peersLock) peers.Remove(peer.Id);
        events.Enqueue(new Event { Kind = Kind.Disconnected, Connection = peer.Id, Reason = reason });
    }

    public void Poll(INetTransportListener l)
    {
        while (events.TryDequeue(out Event e))
        {
            switch (e.Kind)
            {
                case Kind.Connected: l.OnTransportConnected(e.Connection); break;
                case Kind.Disconnected: l.OnTransportDisconnected(e.Connection, e.Reason); break;
                case Kind.Data: l.OnTransportData(e.Connection, e.Data, 0, e.Data.Length); break;
            }
        }
    }

    public void Send(int connection, byte[] data, int length, bool reliable)
    {
        Peer peer;
        lock (peersLock) peers.TryGetValue(connection, out peer);
        if (peer == null || peer.Closed) return;

        try
        {
            lock (peer.SendLock)
            {
                var header = new byte[4]
                {
                    (byte)length, (byte)(length >> 8), (byte)(length >> 16), (byte)(length >> 24)
                };
                peer.Stream.Write(header, 0, 4);
                peer.Stream.Write(data, 0, length);
            }
        }
        catch (Exception e)
        {
            Close(peer, e.Message);
        }
    }

    public void Disconnect(int connection)
    {
        Peer peer;
        lock (peersLock) peers.TryGetValue(connection, out peer);
        if (peer != null) Close(peer, "disconnect");
    }

    public void Shutdown()
    {
        running = false;
        try { listener?.Stop(); } catch (Exception) { }

        List<Peer> all;
        lock (peersLock) all = new List<Peer>(peers.Values);
        foreach (Peer p in all)
        {
            p.Closed = true;
            try { p.Client.Close(); } catch (Exception) { }
        }
        lock (peersLock) peers.Clear();
    }
}
