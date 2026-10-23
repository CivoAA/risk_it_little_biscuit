using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bonbons, Herzen und Magnete der anderen.
///
/// Was ein Gegner fallen laesst, liegt beim Besitzer des Gegners. Steht ein
/// Mitspieler daneben, bekommt er eine Kopie geschickt und kann sie einsammeln:
/// die Erfahrung geht ohnehin in den gemeinsamen Topf (mit SEINEM
/// XP-Multiplikator), ein Herz heilt IHN, ein Magnet zieht SEINE Bonbons.
/// Danach sagt er dem Besitzer Bescheid, der das Original wegraeumt - ohne es
/// selbst noch einmal anzurechnen.
///
/// Geschickt wird nur, was im Bild des Mitspielers liegt. Zusammengelegte
/// Bonbons (mehr XP, groesseres Bild) gehen als Aenderung hinterher.
/// </summary>
public class NetPickups : MonoBehaviour
{
    public enum Kind : byte { Xp = 0, Heart = 1, Magnet = 2, GoldenHeart = 3 }

    private const float Tick = 0.1f;
    private const float Margin = 3f;

    public static NetPickups Active { get; private set; }

    // ---------- Besitzer ----------

    private sealed class Local
    {
        public int Id;
        public Component Source;   // ExpPickup oder PickUps
        public Kind Kind;
        public int Value;
        public bool Lucky;
        public readonly bool[] Entered = new bool[NetSession.MaxPlayers];
        public readonly int[] SentValue = new int[NetSession.MaxPlayers];
    }

    private static readonly List<Local> locals = new List<Local>();
    private static readonly Dictionary<Component, Local> localBy = new Dictionary<Component, Local>();
    private static int nextId = 1;

    // ---------- Empfaenger ----------

    private static readonly Dictionary<long, Component> proxies = new Dictionary<long, Component>();

    private NetSession session;
    private NetRun run;
    private float timer;
    private readonly NetWriter enterW = new NetWriter(1024), leaveW = new NetWriter(256);

    private static long Key(int owner, int id) => ((long)owner << 32) | (uint)id;

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        run = GetComponent<NetRun>();
        session.On(NetMsg.PickupEnter, OnEnter);
        session.On(NetMsg.PickupLeave, OnLeave);
        session.On(NetMsg.PickupTake, OnTake);
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.Off(NetMsg.PickupEnter);
            session.Off(NetMsg.PickupLeave);
            session.Off(NetMsg.PickupTake);
        }
        locals.Clear();
        localBy.Clear();
        proxies.Clear();
        if (Active == this) Active = null;
    }

    // ==================================================================
    //  Anmeldung (aus ExpPickup / PickUps)
    // ==================================================================

    public static void Register(ExpPickup xp)
    {
        if (Active == null || xp == null || xp.IsRemote || localBy.ContainsKey(xp)) return;
        var l = new Local { Id = nextId++, Source = xp, Kind = Kind.Xp };
        locals.Add(l);
        localBy[xp] = l;
    }

    public static void Register(PickUps pickup)
    {
        if (Active == null || pickup == null || pickup.IsRemote || localBy.ContainsKey(pickup)) return;
        Kind kind = pickup.PickUp_id == 0 ? Kind.Heart
                  : pickup.PickUp_id == 1 ? Kind.Magnet
                  : pickup.PickUp_id == PickUps.GoldenHeartId ? Kind.GoldenHeart
                  : (Kind)255;
        if ((byte)kind == 255) return;
        var l = new Local { Id = nextId++, Source = pickup, Kind = kind };
        locals.Add(l);
        localBy[pickup] = l;
    }

    /// <summary>Eingesammelt, zusammengelegt oder zurueck in den Pool - bei allen weg.</summary>
    public static void Unregister(Component source)
    {
        if (source == null || !localBy.TryGetValue(source, out Local l)) return;
        localBy.Remove(source);
        locals.Remove(l);
        Active?.SendLeave(l);
    }

    /// <summary>Eine Kopie wurde hier eingesammelt - der Besitzer raeumt das Original weg.</summary>
    public static void TakeRemote(int owner, int id, Component proxy)
    {
        if (proxy != null) proxies.Remove(Key(owner, id));
        if (Active == null || owner < 0) return;
        Active.session.Begin(NetMsg.PickupTake).VarUInt((uint)id);
        Active.session.SendTo(owner);
    }

    // ==================================================================
    //  Besitzer
    // ==================================================================

    private void LateUpdate()
    {
        if (run == null || run.CurrentPhase != NetRun.Phase.Running) return;
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = Tick;

        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            if (p == session.LocalSlot) continue;
            NetRun.Peer peer = run.Peers[p];
            if (!peer.Playing || peer.State == NetRun.PeerState.Left || peer.LastState < 0f) continue;
            SendTo(p, peer);
        }
    }

    private void SendTo(int p, NetRun.Peer peer)
    {
        Rect view = new Rect(peer.Position - peer.ViewHalf - Vector2.one * Margin, (peer.ViewHalf + Vector2.one * Margin) * 2f);

        enterW.Clear();
        leaveW.Clear();
        int enterAt = enterW.ReserveUShort(), leaveAt = leaveW.ReserveUShort();
        int enters = 0, leaves = 0;

        for (int i = locals.Count - 1; i >= 0; i--)
        {
            Local l = locals[i];
            if (l.Source == null)
            {
                locals.RemoveAt(i);
                continue;
            }

            Refresh(l);
            Vector2 pos = l.Source.transform.position;
            // Was schon auf den eigenen Spieler zufliegt, gehoert ihm.
            bool chasing = l.Source is ExpPickup xp && xp.IsChasing;
            bool want = !chasing && view.Contains(pos);

            if (want && (!l.Entered[p] || l.SentValue[p] != l.Value))
            {
                l.Entered[p] = true;
                l.SentValue[p] = l.Value;
                enterW.VarUInt((uint)l.Id);
                enterW.Byte((byte)l.Kind);
                enterW.Vector2(pos);
                enterW.VarUInt((uint)Mathf.Max(0, l.Value));
                enterW.Bool(l.Lucky);
                enters++;
            }
            else if (!want && l.Entered[p])
            {
                l.Entered[p] = false;
                leaveW.VarUInt((uint)l.Id);
                leaves++;
            }
        }

        if (leaves > 0)
        {
            leaveW.PatchUShort(leaveAt, (ushort)leaves);
            session.SendPayload(NetMsg.PickupLeave, leaveW, (byte)p, true);
        }
        if (enters > 0)
        {
            enterW.PatchUShort(enterAt, (ushort)enters);
            session.SendPayload(NetMsg.PickupEnter, enterW, (byte)p, true);
        }
    }

    private static void Refresh(Local l)
    {
        if (l.Source is ExpPickup xp)
        {
            l.Value = xp.xpValue;
            l.Lucky = xp.lucky;
        }
    }

    private void SendLeave(Local l)
    {
        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            if (!l.Entered[p]) continue;
            leaveW.Clear();
            leaveW.UShort(1);
            leaveW.VarUInt((uint)l.Id);
            session.SendPayload(NetMsg.PickupLeave, leaveW, (byte)p, true);
        }
    }

    private void OnTake(int from, NetReader r)
    {
        int id = (int)r.VarUInt();
        for (int i = 0; i < locals.Count; i++)
        {
            Local l = locals[i];
            if (l.Id != id) continue;
            // Der andere hat es schon angerechnet - hier nur wegraeumen.
            Component source = l.Source;
            locals.RemoveAt(i);
            localBy.Remove(source);
            for (int p = 0; p < NetSession.MaxPlayers; p++)
                if (p != from && l.Entered[p]) SendLeaveTo(p, l.Id);

            if (source is ExpPickup xp) xp.RemoveSilently();
            else if (source is PickUps pu) pu.RemoveSilently();
            return;
        }
    }

    private void SendLeaveTo(int p, int id)
    {
        leaveW.Clear();
        leaveW.UShort(1);
        leaveW.VarUInt((uint)id);
        session.SendPayload(NetMsg.PickupLeave, leaveW, (byte)p, true);
    }

    // ==================================================================
    //  Empfaenger
    // ==================================================================

    private void OnEnter(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            var kind = (Kind)r.Byte();
            Vector2 pos = r.Vector2();
            int value = (int)r.VarUInt();
            bool lucky = r.Bool();

            long key = Key(from, id);
            if (proxies.TryGetValue(key, out Component existing) && existing != null)
            {
                // Zusammengelegt: neuer Wert, neues Aussehen.
                if (existing is ExpPickup xp) xp.SetRemoteValue(value);
                continue;
            }

            Component proxy = Spawn(kind, pos, value, lucky);
            if (proxy == null) continue;
            if (proxy is ExpPickup e) { e.IsRemote = true; e.NetOwner = from; e.NetId = id; }
            else if (proxy is PickUps p) { p.IsRemote = true; p.NetOwner = from; p.NetId = id; }
            proxies[key] = proxy;
        }
    }

    private static Component Spawn(Kind kind, Vector2 pos, int value, bool lucky)
    {
        switch (kind)
        {
            case Kind.Xp:
                return SpawnExp.Instance != null ? SpawnExp.Instance.SpawnRemote(pos, value, lucky) : null;
            case Kind.Heart:
            case Kind.GoldenHeart:
            {
                PickUpManager pm = PickUpManager.Instance;
                GameObject go = pm != null ? pm.SpawnHeart(pos, kind == Kind.GoldenHeart) : null;
                if (go == null) return null;
                RunScene.Place(go, "Effekte");
                return go.GetComponent<PickUps>();
            }
            case Kind.Magnet:
            {
                PickUpManager pm = PickUpManager.Instance;
                if (pm == null || pm.magnet_PickUP == null) return null;
                GameObject go = Instantiate(pm.magnet_PickUP, pos, Quaternion.identity);
                RunScene.Place(go, "Effekte");
                return go.GetComponent<PickUps>();
            }
        }
        return null;
    }

    private void OnLeave(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            long key = Key(from, id);
            if (!proxies.TryGetValue(key, out Component proxy)) continue;
            proxies.Remove(key);
            if (proxy is ExpPickup xp) xp.RemoveSilently();
            else if (proxy is PickUps pu) pu.RemoveSilently();
        }
    }
}
