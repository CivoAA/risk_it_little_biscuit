using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Zeigt, was in der Welt der anderen passiert - und zeigt den anderen, was
/// in der eigenen passiert.
///
/// Jeder Rechner simuliert nur seine eigene Ecke (siehe <see cref="NetRun"/>).
/// Damit die anderen sie trotzdem sehen und darin mitkaempfen koennen, gehen
/// drei Arten von "Bildern" raus:
///
///   Player  die eigene Figur (Koerper, Schatten - ohne Waffen). Beim
///           Empfaenger eine <see cref="NetAvatar"/>, die die eigenen Gegner
///           jagen und die man wiederbeleben kann. Immer sichtbar.
///   Enemy   die eigenen Gegner. Beim Empfaenger ein Stellvertreter mit
///           <see cref="Enemy"/>-Komponente (IsRemote): seine Waffen treffen
///           ihn, Beruehrung tut weh - aber Leben, Laufen und Sterben bleiben
///           beim Besitzer. Treffer gehen per <see cref="NetMsg.EnemyHit"/>
///           zurueck. Bosse sind immer sichtbar (Boss-Leiste, Pfeil).
///   Fx      alles andere, was als SpriteRenderer im Lauf steht: Geschosse,
///           Klingen, Explosionen, Boss-Attacken. Nur Bild, keine Wirkung.
///
/// Geschickt wird nur, was im Bild des jeweiligen Mitspielers liegt (plus
/// Rand). Stehen alle weit auseinander, geht fast nichts ueber die Leitung.
///
/// Das Aussehen (welches Sprite, Farbe, Spiegelung, Sortierung, Lage der
/// Teile) geht zuverlaessig und nur bei Aenderung raus (<see cref="NetMsg.VisLook"/>),
/// die Position 20-mal pro Sekunde unzuverlaessig (<see cref="NetMsg.VisMotion"/>).
/// Der Empfaenger zeigt alles 100 ms in der Vergangenheit und gleitet zwischen
/// zwei Staenden - so ruckelt nichts, auch wenn mal ein Paket fehlt.
/// </summary>
[DefaultExecutionOrder(900)]
public class NetVisuals : MonoBehaviour
{
    public enum Kind : byte { Player = 0, Enemy = 1, Fx = 2 }

    private const float Tick = 0.05f;
    private const float InterpDelay = 0.1f;
    private const float EnemyMargin = 6f, FxMargin = 2.5f, LeaveExtra = 4f;
    private const int MaxFxPerPeer = 220;
    private const int MotionChunk = 1000;
    private const float PartsRescan = 0.5f;
    private const float FxNearDistance = 70f;

    public static NetVisuals Active { get; private set; }

    public NetSprites Sprites { get; private set; }

    private NetSession session;
    private NetRun run;
    private float tickTimer;

    // ==================================================================
    //  Besitzer
    // ==================================================================

    private struct PartState
    {
        public int Sprite, Material, Layer;
        public short Order;
        public Color32 Color;
        public byte Flags;
        public Vector2 Offset, Scale, Size;
        public float Rot;
        public byte DrawMode;
    }

    // Bits in PartState.Flags
    private const byte PartVisible = 1, PartFlipX = 2, PartFlipY = 4;

    // Masken-Bits fuer geaenderte Felder
    private const uint MSprite = 1, MColor = 2, MFlags = 4, MSort = 8, MOffset = 16, MRot = 32, MScale = 64,
                       MMaterial = 128, MSize = 256, MAll = 511;

    private sealed class Source
    {
        public int Id;
        public Object Key;
        public int CapturedTick = -1;
        public Kind Kind;
        public Transform Root;
        public Enemy Enemy;
        public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();
        public PartState[] Last = new PartState[0];
        public PartState[] Now = new PartState[0];
        public bool PartsChanged;
        public readonly bool[] Entered = new bool[NetSession.MaxPlayers];
        public bool EnteredAnywhere;
        public float NextPartsScan;
        public float LastHp = -1f, LastMaxHp = -1f;
        public byte LastEnemyFlags;
        public bool Seen;
    }

    private readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();
    private readonly Dictionary<Object, Source> sourceByKey = new Dictionary<Object, Source>();
    private readonly List<Source> sourceList = new List<Source>();
    private int nextId = 1;

    // FX-Einordnung je Renderer (Instanz-Id): true = ist ein Effekt.
    private readonly Dictionary<SpriteRenderer, bool> fxClass = new Dictionary<SpriteRenderer, bool>();
    private float fxClassResetAt;
    private SpriteRenderer[] scanBuffer = new SpriteRenderer[0];

    private readonly NetWriter enterW = new NetWriter(4096), leaveW = new NetWriter(512),
                               lookW = new NetWriter(4096), motionW = new NetWriter(MotionChunk + 64),
                               hitW = new NetWriter(512), ctrlW = new NetWriter(256);

    // Treffer an fremden Gegnern, gesammelt je Besitzer und einmal pro Frame verschickt.
    private struct Hit { public int Id; public float Damage, Knock, Slow; }
    private struct Ctrl { public int Id; public byte Kind; public float A, B, C; }
    private readonly List<Hit>[] hits = new List<Hit>[NetSession.MaxPlayers];
    private readonly List<Ctrl>[] ctrls = new List<Ctrl>[NetSession.MaxPlayers];

    // ==================================================================
    //  Empfaenger
    // ==================================================================

    private struct Snap
    {
        public float Time;
        public Vector2 Pos;
        public float Rot;
        public Vector2 Scale;
    }

    private sealed class Mirror
    {
        public int Owner, Id;
        public Kind Kind;
        public GameObject Go;
        public Transform T;
        public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>();
        public Enemy Enemy;
        public NetAvatar Avatar;
        public Rigidbody2D Rb;
        public readonly Snap[] Snaps = new Snap[4];
        public int SnapCount;
        public Vector2 Velocity;
        public Vector2 Target;
        public bool HasTarget;
    }

    private readonly Dictionary<long, Mirror> mirrors = new Dictionary<long, Mirror>();
    private readonly List<Mirror> mirrorList = new List<Mirror>();
    private Transform mirrorRoot;
    private readonly Dictionary<int, Transform> lurePoints = new Dictionary<int, Transform>();

    private static long Key(int owner, int id) => ((long)owner << 32) | (uint)id;

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        run = GetComponent<NetRun>();
        Sprites = new NetSprites(session);
        for (int i = 0; i < NetSession.MaxPlayers; i++)
        {
            hits[i] = new List<Hit>();
            ctrls[i] = new List<Ctrl>();
        }

        var root = new GameObject("NetMirrors");
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        mirrorRoot = root.transform;

        session.On(NetMsg.VisEnter, OnEnter);
        session.On(NetMsg.VisLeave, OnLeave);
        session.On(NetMsg.VisLook, OnLook);
        session.On(NetMsg.VisMotion, OnMotion);
        session.On(NetMsg.EnemyHit, OnEnemyHit);
        session.On(NetMsg.EnemyCtrl, OnEnemyCtrl);
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.Off(NetMsg.VisEnter);
            session.Off(NetMsg.VisLeave);
            session.Off(NetMsg.VisLook);
            session.Off(NetMsg.VisMotion);
            session.Off(NetMsg.EnemyHit);
            session.Off(NetMsg.EnemyCtrl);
        }
        Sprites?.Dispose();
        if (Active == this) Active = null;
    }

    /// <summary>Ein Spieler ist weg: alles von ihm verschwindet, und er bekommt nichts mehr.</summary>
    public void RemoveOwner(int slot)
    {
        for (int i = mirrorList.Count - 1; i >= 0; i--)
        {
            Mirror m = mirrorList[i];
            if (m.Owner != slot) continue;
            DestroyMirror(m);
        }
        foreach (Source s in sourceList) s.Entered[slot] = false;
        Sprites.ForgetPeer(slot);
        hits[slot].Clear();
        ctrls[slot].Clear();
    }

    /// <summary>Host: Bosse, die dieser Spieler besitzt (fuer die Uebergabe, wenn er geht).</summary>
    public List<(EnemyId, float)> BossesOf(int slot)
    {
        var list = new List<(EnemyId, float)>();
        foreach (Mirror m in mirrorList)
        {
            if (m.Owner != slot || m.Enemy == null || !m.Enemy.IsBoss) continue;
            EnemyId id = m.Enemy.Identity;
            if (id != EnemyId.None) list.Add((id, m.Enemy.HealthFraction));
        }
        return list;
    }

    // ==================================================================
    //  Takt
    // ==================================================================

    private void LateUpdate()
    {
        if (run == null || run.CurrentPhase == NetRun.Phase.Loading) return;

        FlushHits();

        tickTimer -= Time.unscaledDeltaTime;
        if (tickTimer <= 0f && run.CurrentPhase == NetRun.Phase.Running)
        {
            tickTimer = Tick;
            OwnerTick();
        }

        UpdateMirrors();
    }

    private void FixedUpdate()
    {
        // Stellvertreter-Gegner gleiten mit der Physik, damit sie den eigenen
        // Spieler wie echte Gegner wegschieben und Kontakte stimmen.
        foreach (Mirror m in mirrorList)
        {
            if (m.Rb == null || !m.HasTarget) continue;
            m.Rb.MovePosition(m.Target);
        }
    }

    // ==================================================================
    //  Besitzer: was gibt es, wer bekommt was
    // ==================================================================

    private void OwnerTick()
    {
        tickCount++;
        foreach (Source s in sourceList) s.Seen = false;

        CollectPlayer();
        CollectEnemies();
        if (FxWanted()) CollectFx();

        // Was nicht mehr da ist, verschwindet bei allen.
        for (int i = sourceList.Count - 1; i >= 0; i--)
        {
            Source s = sourceList[i];
            if (s.Seen) continue;
            byte reason = s.Enemy != null && s.Enemy.NetDied ? (byte)2 : (byte)1;
            Vector2 pos = s.Root != null ? (Vector2)s.Root.position : Vector2.zero;
            for (int p = 0; p < NetSession.MaxPlayers; p++)
                if (s.Entered[p]) QueueLeave(p, s.Id, reason, pos);
            RemoveSource(s, i);
        }

        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            if (!Receives(p)) continue;
            SendTo(p);
        }

        // Neuer Stand ist jetzt der verschickte.
        foreach (Source s in sourceList)
        {
            if (!s.EnteredAnywhere) continue;
            if (s.Last.Length != s.Now.Length) s.Last = new PartState[s.Now.Length];
            System.Array.Copy(s.Now, s.Last, s.Now.Length);
            s.PartsChanged = false;
            if (s.Enemy != null)
            {
                s.LastHp = s.Enemy.CurrentHealth;
                s.LastMaxHp = s.Enemy.MaxHealth;
                s.LastEnemyFlags = EnemyFlags(s.Enemy);
            }
        }

        FlushLeaves();
    }

    /// <summary>Bekommt dieser Platz Bilder von mir?</summary>
    private bool Receives(int slot)
    {
        if (slot == session.LocalSlot || run == null) return false;
        NetRun.Peer p = run.Peers[slot];
        return p.Playing && p.State != NetRun.PeerState.Left && session.Slots[slot].Used;
    }

    private Source Ensure(Object key, Kind kind, Transform root)
    {
        if (!sourceByKey.TryGetValue(key, out Source s) || s.Root == null)
        {
            if (s != null) RemoveSource(s, sourceList.IndexOf(s));
            s = new Source { Id = nextId++, Kind = kind, Root = root, Key = key };
            sourceByKey[key] = s;
            sources[s.Id] = s;
            sourceList.Add(s);
        }
        s.Seen = true;
        return s;
    }

    private void RemoveSource(Source s, int index)
    {
        if (index >= 0 && index < sourceList.Count && sourceList[index] == s) sourceList.RemoveAt(index);
        else sourceList.Remove(s);
        sources.Remove(s.Id);
        // Der Schluessel kann schon zerstoert sein - Unity-null ist trotzdem ein gueltiger Schluessel.
        if (!ReferenceEquals(s.Key, null) && sourceByKey.TryGetValue(s.Key, out Source current) && current == s)
            sourceByKey.Remove(s.Key);
        if (s.Enemy != null) s.Enemy.NetId = 0;
    }

    // ---------- Spieler ----------

    private static readonly string[] NotBody = { "Weapons", "EvoWeapons", "Buffs", "MaxLevelStuff", "PowerUps" };

    private void CollectPlayer()
    {
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeInHierarchy) return;
        Source s = Ensure(pc, Kind.Player, pc.transform);

        if (Time.unscaledTime >= s.NextPartsScan)
        {
            s.NextPartsScan = Time.unscaledTime + PartsRescan;
            var found = new List<SpriteRenderer>();
            foreach (SpriteRenderer sr in pc.GetComponentsInChildren<SpriteRenderer>(true))
                if (IsBodyPart(pc.transform, sr)) found.Add(sr);
            ReplaceParts(s, found);
        }
    }

    private static bool IsBodyPart(Transform player, SpriteRenderer sr)
    {
        Transform t = sr.transform;
        while (t != null && t != player)
        {
            if (t.parent == player && System.Array.IndexOf(NotBody, t.name) >= 0) return false;
            t = t.parent;
        }
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.PickupRange != null && sr.transform.IsChildOf(pc.PickupRange.transform)) return false;
        return true;
    }

    // ---------- Gegner ----------

    private void CollectEnemies()
    {
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.IsRemote) continue;
            Source s = Ensure(e, Kind.Enemy, e.transform);
            s.Enemy = e;
            if (e.NetId == 0) e.NetId = s.Id;

            if (Time.unscaledTime >= s.NextPartsScan)
            {
                s.NextPartsScan = Time.unscaledTime + PartsRescan + (s.Id % 7) * 0.03f;
                var found = new List<SpriteRenderer>();
                SpriteRenderer main = e.MainRenderer;
                if (main != null) found.Add(main);
                foreach (SpriteRenderer sr in e.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr != main) found.Add(sr);
                ReplaceParts(s, found);
            }
        }
    }

    private static void ReplaceParts(Source s, List<SpriteRenderer> found)
    {
        bool same = found.Count == s.Parts.Count;
        for (int i = 0; same && i < found.Count; i++) same = found[i] == s.Parts[i];
        if (same) return;
        s.Parts.Clear();
        s.Parts.AddRange(found);
        s.PartsChanged = true;
    }

    // ---------- Effekte ----------

    /// <summary>
    /// Effekte suchen kostet: alle SpriteRenderer der Szene durchgehen. Das
    /// lohnt nur, wenn ueberhaupt ein Mitspieler in der Naehe ist (oder einer
    /// meiner Bosse bei ihm).
    /// </summary>
    private bool FxWanted()
    {
        PlayerController pc = PlayerController.Instance;
        Vector2 me = pc != null ? (Vector2)pc.transform.position : Vector2.zero;
        float near2 = FxNearDistance * FxNearDistance;

        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            if (!Receives(p)) continue;
            NetRun.Peer peer = run.Peers[p];
            if (peer.LastState < 0f) continue;
            if ((peer.Position - me).sqrMagnitude < near2) return true;
        }

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
            if (alive[i] != null && !alive[i].IsRemote && alive[i].IsBoss) return true;
        return false;
    }

    private void CollectFx()
    {
        if (Time.unscaledTime >= fxClassResetAt)
        {
            fxClassResetAt = Time.unscaledTime + 3f;
            fxClass.Clear();
        }

        scanBuffer = FindObjectsByType<SpriteRenderer>();
        string mapScene = MapSceneSystem.RunMapScene;

        foreach (SpriteRenderer sr in scanBuffer)
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (!fxClass.TryGetValue(sr, out bool isFx))
            {
                isFx = ClassifyFx(sr, mapScene);
                fxClass[sr] = isFx;
            }
            if (!isFx) continue;

            Source s = Ensure(sr, Kind.Fx, sr.transform);
            if (s.Parts.Count != 1 || s.Parts[0] != sr)
            {
                s.Parts.Clear();
                s.Parts.Add(sr);
                s.PartsChanged = true;
            }
        }
    }

    private bool ClassifyFx(SpriteRenderer sr, string mapScene)
    {
        GameObject go = sr.gameObject;
        Scene scene = go.scene;
        if (!scene.IsValid() || scene.name == "DontDestroyOnLoad") return false;
        if (!string.IsNullOrEmpty(mapScene) && scene.name == mapScene) return false;

        Transform t = sr.transform;
        if (mirrorRoot != null && t.IsChildOf(mirrorRoot)) return false;

        // Feste Welt (Spawner-Objekte, Mixer), Bonbons und Pickups haben eigene Wege.
        for (Transform p = t; p != null; p = p.parent)
        {
            string n = p.name;
            if (n == "Map-Objekte" || n == "Mixer" || n == "Exp") return false;
        }
        if (sr.GetComponentInParent<ExpPickup>(true) != null) return false;
        if (sr.GetComponentInParent<PickUps>(true) != null) return false;
        if (sr.GetComponentInParent<MixerObject>(true) != null) return false;
        if (sr.GetComponentInParent<Canvas>(true) != null) return false;
        if (sr.GetComponentInParent<Camera>(true) != null) return false;
        if (sr.GetComponentInParent<NetLocalOnly>(true) != null) return false;

        Enemy enemy = sr.GetComponentInParent<Enemy>(true);
        if (enemy != null) return false;   // Teil des Gegners (oder ein Stellvertreter)

        PlayerController pc = sr.GetComponentInParent<PlayerController>(true);
        if (pc != null) return !IsBodyPart(pc.transform, sr);

        return true;
    }

    // ---------- Senden ----------

    private void SendTo(int p)
    {
        NetRun.Peer peer = run.Peers[p];
        bool known = peer.LastState >= 0f;
        Rect view = new Rect(peer.Position - peer.ViewHalf, peer.ViewHalf * 2f);

        enterW.Clear();
        lookW.Clear();
        int enterCount = 0, lookCount = 0, fxCount = 0;
        int enterAt = enterW.ReserveUShort();
        int lookAt = lookW.ReserveUShort();

        foreach (Source s in sourceList)
        {
            bool want;
            switch (s.Kind)
            {
                case Kind.Player:
                    want = true;
                    break;
                case Kind.Enemy:
                    want = s.Enemy != null && (s.Enemy.IsBoss || known && Inside(view, s.Root.position,
                               EnemyMargin + (s.Entered[p] ? LeaveExtra : 0f)));
                    break;
                default:
                    want = known && fxCount < MaxFxPerPeer &&
                           Inside(view, s.Root.position, FxMargin + (s.Entered[p] ? LeaveExtra * 0.5f : 0f));
                    if (want) fxCount++;
                    break;
            }

            if (!want)
            {
                if (s.Entered[p])
                {
                    s.Entered[p] = false;
                    QueueLeave(p, s.Id, 0, s.Root.position);
                }
                continue;
            }

            Capture(s);

            if (!s.Entered[p])
            {
                s.Entered[p] = true;
                s.EnteredAnywhere = true;
                EnsureSprites(p, s, true);
                WriteEnter(enterW, s);
                enterCount++;
                continue;
            }

            if (WantsLook(s))
            {
                EnsureSprites(p, s, s.PartsChanged);
                if (WriteLook(lookW, s)) lookCount++;
            }
        }

        // Vorher die Abgaenge, damit eine Nummer nie doppelt lebt.
        FlushLeaves(p);

        if (enterCount > 0)
        {
            enterW.PatchUShort(enterAt, (ushort)enterCount);
            session.SendPayload(NetMsg.VisEnter, enterW, (byte)p, true);
        }
        if (lookCount > 0)
        {
            lookW.PatchUShort(lookAt, (ushort)lookCount);
            session.SendPayload(NetMsg.VisLook, lookW, (byte)p, true);
        }

        SendMotion(p, peer.Position);
    }

    private static bool Inside(Rect view, Vector3 pos, float margin) =>
        pos.x >= view.xMin - margin && pos.x <= view.xMax + margin &&
        pos.y >= view.yMin - margin && pos.y <= view.yMax + margin;

    /// <summary>Aktuellen Stand der Teile in s.Now schreiben (einmal pro Takt).</summary>
    private int tickCount;

    private void Capture(Source s)
    {
        // Einmal pro Takt reicht, auch wenn mehrere Mitspieler es sehen.
        if (s.CapturedTick == tickCount) return;
        s.CapturedTick = tickCount;
        if (s.Now.Length != s.Parts.Count) s.Now = new PartState[s.Parts.Count];
        Transform root = s.Root;
        Vector3 rootPos = root.position;

        for (int i = 0; i < s.Parts.Count; i++)
        {
            SpriteRenderer sr = s.Parts[i];
            ref PartState st = ref s.Now[i];
            if (sr == null)
            {
                st = default;
                continue;
            }

            st.Sprite = Sprites.IdOf(sr.sprite);
            st.Material = Sprites.IdOf(sr.sharedMaterial);
            st.Color = sr.color;
            st.Flags = (byte)((sr.enabled && sr.gameObject.activeInHierarchy ? PartVisible : 0)
                            | (sr.flipX ? PartFlipX : 0) | (sr.flipY ? PartFlipY : 0));
            st.Layer = sr.sortingLayerID;
            st.Order = (short)Mathf.Clamp(sr.sortingOrder, short.MinValue, short.MaxValue);
            st.DrawMode = (byte)sr.drawMode;
            st.Size = sr.drawMode != SpriteDrawMode.Simple ? sr.size : Vector2.zero;

            Transform t = sr.transform;
            if (s.Kind == Kind.Fx)
            {
                // Fx: der Renderer ist die Wurzel - Lage und Groesse kommen mit der Bewegung.
                st.Offset = Vector2.zero;
                st.Rot = 0f;
                st.Scale = Vector2.one;
            }
            else
            {
                st.Offset = (Vector2)(t.position - rootPos);
                st.Rot = t.eulerAngles.z;
                Vector3 ls = t.lossyScale;
                st.Scale = new Vector2(ls.x, ls.y);
            }
        }
    }

    private static bool WantsLook(Source s)
    {
        if (s.PartsChanged || s.Now.Length != s.Last.Length) return true;
        for (int i = 0; i < s.Now.Length; i++)
            if (Changed(ref s.Now[i], ref s.Last[i]) != 0) return true;
        if (s.Enemy != null)
        {
            if (Mathf.Abs(s.Enemy.CurrentHealth - s.LastHp) > 0.01f || Mathf.Abs(s.Enemy.MaxHealth - s.LastMaxHp) > 0.01f)
                return true;
            if (EnemyFlags(s.Enemy) != s.LastEnemyFlags) return true;
        }
        return false;
    }

    private static uint Changed(ref PartState a, ref PartState b)
    {
        uint m = 0;
        if (a.Sprite != b.Sprite) m |= MSprite;
        if (!a.Color.Equals(b.Color)) m |= MColor;
        if (a.Flags != b.Flags) m |= MFlags;
        if (a.Layer != b.Layer || a.Order != b.Order) m |= MSort;
        if ((a.Offset - b.Offset).sqrMagnitude > 0.0001f) m |= MOffset;
        if (Mathf.Abs(Mathf.DeltaAngle(a.Rot, b.Rot)) > 1f) m |= MRot;
        if ((a.Scale - b.Scale).sqrMagnitude > 0.0001f) m |= MScale;
        if (a.Material != b.Material) m |= MMaterial;
        if (a.DrawMode != b.DrawMode || (a.Size - b.Size).sqrMagnitude > 0.0001f) m |= MSize;
        return m;
    }

    private void EnsureSprites(int p, Source s, bool all)
    {
        for (int i = 0; i < s.Parts.Count; i++)
        {
            SpriteRenderer sr = s.Parts[i];
            if (sr == null) continue;
            if (all || s.Last.Length != s.Now.Length || s.Now[i].Sprite != s.Last[i].Sprite)
                Sprites.Ensure(p, sr.sprite, s.Now[i].Sprite);
            if (all || s.Last.Length != s.Now.Length || s.Now[i].Material != s.Last[i].Material)
                Sprites.Ensure(p, sr.sharedMaterial, s.Now[i].Material);
        }
    }

    private static void WritePart(NetWriter w, ref PartState st, uint mask)
    {
        w.VarUInt(mask);
        if ((mask & MSprite) != 0) w.VarUInt((uint)st.Sprite);
        if ((mask & MColor) != 0) w.Color32(st.Color);
        if ((mask & MFlags) != 0) w.Byte(st.Flags);
        if ((mask & MSort) != 0) { w.Int(st.Layer); w.Short(st.Order); }
        if ((mask & MOffset) != 0) w.CompactPos(st.Offset, Vector2.zero);
        if ((mask & MRot) != 0) w.Angle(st.Rot);
        if ((mask & MScale) != 0) { w.Scale(st.Scale.x); w.Scale(st.Scale.y); }
        if ((mask & MMaterial) != 0) w.VarUInt((uint)st.Material);
        if ((mask & MSize) != 0) { w.Byte(st.DrawMode); w.Scale(st.Size.x); w.Scale(st.Size.y); }
    }

    private static void ReadPart(NetReader r, ref PartState st)
    {
        uint mask = r.VarUInt();
        if ((mask & MSprite) != 0) st.Sprite = (int)r.VarUInt();
        if ((mask & MColor) != 0) st.Color = r.Color32();
        if ((mask & MFlags) != 0) st.Flags = r.Byte();
        if ((mask & MSort) != 0) { st.Layer = r.Int(); st.Order = r.Short(); }
        if ((mask & MOffset) != 0) st.Offset = r.CompactPos(Vector2.zero);
        if ((mask & MRot) != 0) st.Rot = r.Angle();
        if ((mask & MScale) != 0) st.Scale = new Vector2(r.Scale(), r.Scale());
        if ((mask & MMaterial) != 0) st.Material = (int)r.VarUInt();
        if ((mask & MSize) != 0) { st.DrawMode = r.Byte(); st.Size = new Vector2(r.Scale(), r.Scale()); }
    }

    private static byte EnemyFlags(Enemy e) =>
        (byte)((e.Untouchable ? 1 : 0) | (e.IsFrozen ? 2 : 0) | (e.IsBubbled ? 4 : 0) | (e.HoldDeath ? 8 : 0));

    private void WriteEnter(NetWriter w, Source s)
    {
        w.VarUInt((uint)s.Id);
        w.Byte((byte)s.Kind);
        w.Vector2(s.Root.position);

        if (s.Kind == Kind.Enemy)
        {
            Enemy e = s.Enemy;
            w.VarInt((int)e.Identity);
            w.Byte((byte)e.Role);
            w.Float(e.MaxHealth);
            w.Float(e.CurrentHealth);
            w.Float(e.ContactDamage);
            w.Byte(EnemyFlags(e));
            w.Byte((byte)e.gameObject.layer);
            w.String(e.DisplayName ?? "");
            WriteCollider(w, e);
        }
        else if (s.Kind == Kind.Fx)
        {
            w.Angle(s.Root.eulerAngles.z);
            Vector3 ls = s.Root.lossyScale;
            w.Scale(ls.x);
            w.Scale(ls.y);
        }

        w.VarUInt((uint)s.Now.Length);
        for (int i = 0; i < s.Now.Length; i++) WritePart(w, ref s.Now[i], MAll);
    }

    /// <summary>Form des Koerpers (Hauptcollider) in Weltmassen relativ zur Wurzel.</summary>
    private static void WriteCollider(NetWriter w, Enemy e)
    {
        Collider2D c = null;
        foreach (Collider2D col in e.GetComponents<Collider2D>())
        {
            if (col.isTrigger) continue;
            c = col;
            break;
        }
        if (c == null) c = e.GetComponent<Collider2D>();

        Vector3 ls = e.transform.lossyScale;
        Vector2 scale = new Vector2(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
        switch (c)
        {
            case CircleCollider2D circle:
                w.Byte(1);
                w.Vector2(Vector2.Scale(circle.offset, scale));
                w.Vector2(new Vector2(circle.radius * Mathf.Max(scale.x, scale.y), 0f));
                w.Byte((byte)(circle.isTrigger ? 1 : 0));
                break;
            case BoxCollider2D box:
                w.Byte(2);
                w.Vector2(Vector2.Scale(box.offset, scale));
                w.Vector2(Vector2.Scale(box.size, scale));
                w.Byte((byte)(box.isTrigger ? 1 : 0));
                break;
            case CapsuleCollider2D cap:
                w.Byte(3);
                w.Vector2(Vector2.Scale(cap.offset, scale));
                w.Vector2(Vector2.Scale(cap.size, scale));
                w.Byte((byte)((cap.isTrigger ? 1 : 0) | (cap.direction == CapsuleDirection2D.Horizontal ? 2 : 0)));
                break;
            default:
                // Kein bekannter Collider: Kreis aus den Bildgrenzen.
                w.Byte(1);
                w.Vector2(Vector2.zero);
                w.Vector2(new Vector2(c != null ? Mathf.Max(0.2f, c.bounds.extents.x) : 0.4f, 0f));
                w.Byte(0);
                break;
        }
    }

    private bool WriteLook(NetWriter w, Source s)
    {
        byte flags = 0;
        bool reset = s.PartsChanged || s.Now.Length != s.Last.Length;
        if (reset) flags |= 1;
        bool hp = s.Enemy != null && (Mathf.Abs(s.Enemy.CurrentHealth - s.LastHp) > 0.01f ||
                                      Mathf.Abs(s.Enemy.MaxHealth - s.LastMaxHp) > 0.01f);
        if (hp) flags |= 2;
        bool ef = s.Enemy != null && EnemyFlags(s.Enemy) != s.LastEnemyFlags;
        if (ef) flags |= 4;

        int changed = 0;
        if (!reset)
            for (int i = 0; i < s.Now.Length; i++)
                if (Changed(ref s.Now[i], ref s.Last[i]) != 0) changed++;

        if (!reset && !hp && !ef && changed == 0) return false;

        w.VarUInt((uint)s.Id);
        w.Byte(flags);
        if (reset)
        {
            w.VarUInt((uint)s.Now.Length);
            for (int i = 0; i < s.Now.Length; i++) WritePart(w, ref s.Now[i], MAll);
        }
        else
        {
            w.VarUInt((uint)changed);
            for (int i = 0; i < s.Now.Length; i++)
            {
                uint mask = Changed(ref s.Now[i], ref s.Last[i]);
                if (mask == 0) continue;
                w.VarUInt((uint)i);
                WritePart(w, ref s.Now[i], mask);
            }
        }
        if (hp)
        {
            w.Float(s.Enemy.CurrentHealth);
            w.Float(s.Enemy.MaxHealth);
        }
        if (ef) w.Byte(EnemyFlags(s.Enemy));
        return true;
    }

    private void SendMotion(int p, Vector2 origin)
    {
        motionW.Clear();
        int countAt = -1, count = 0;

        foreach (Source s in sourceList)
        {
            if (!s.Entered[p] || s.Root == null) continue;

            if (countAt < 0)
            {
                motionW.Vector2(origin);
                countAt = motionW.ReserveUShort();
            }

            Vector2 pos = s.Root.position;
            bool far = Mathf.Abs(pos.x - origin.x) > 500f || Mathf.Abs(pos.y - origin.y) > 500f;
            bool fx = s.Kind == Kind.Fx;
            Vector3 ls = fx ? s.Root.lossyScale : Vector3.one;
            bool scaled = fx && (Mathf.Abs(ls.x - 1f) > 0.004f || Mathf.Abs(ls.y - 1f) > 0.004f);

            motionW.VarUInt((uint)s.Id);
            motionW.Byte((byte)((far ? 1 : 0) | (fx ? 2 : 0) | (scaled ? 4 : 0)));
            if (far) motionW.Vector2(pos);
            else motionW.CompactPos(pos, origin);
            if (fx) motionW.Angle(s.Root.eulerAngles.z);
            if (scaled)
            {
                motionW.Scale(ls.x);
                motionW.Scale(ls.y);
            }
            count++;

            if (motionW.Length >= MotionChunk)
            {
                motionW.PatchUShort(countAt, (ushort)count);
                session.SendPayload(NetMsg.VisMotion, motionW, (byte)p, false);
                motionW.Clear();
                countAt = -1;
                count = 0;
            }
        }

        if (count > 0)
        {
            motionW.PatchUShort(countAt, (ushort)count);
            session.SendPayload(NetMsg.VisMotion, motionW, (byte)p, false);
        }
    }

    // ---------- Abgaenge ----------

    private readonly List<(int slot, int id, byte reason, Vector2 pos)> leaves = new List<(int, int, byte, Vector2)>();

    private void QueueLeave(int slot, int id, byte reason, Vector2 pos) => leaves.Add((slot, id, reason, pos));

    private void FlushLeaves(int onlySlot = -1)
    {
        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            if (onlySlot >= 0 && p != onlySlot) continue;
            leaveW.Clear();
            int at = leaveW.ReserveUShort();
            int count = 0;
            for (int i = leaves.Count - 1; i >= 0; i--)
            {
                var l = leaves[i];
                if (l.slot != p) continue;
                leaveW.VarUInt((uint)l.id);
                leaveW.Byte(l.reason);
                if (l.reason == 2) leaveW.Vector2(l.pos);
                count++;
                leaves.RemoveAt(i);
            }
            if (count == 0 || !Receives(p)) continue;
            leaveW.PatchUShort(at, (ushort)count);
            session.SendPayload(NetMsg.VisLeave, leaveW, (byte)p, true);
        }
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

            long key = Key(from, id);
            if (mirrors.TryGetValue(key, out Mirror old)) DestroyMirror(old);

            var m = new Mirror { Owner = from, Id = id, Kind = kind };
            m.Go = new GameObject(kind + " " + from + ":" + id);
            m.Go.SetActive(false);
            m.T = m.Go.transform;
            m.T.SetParent(mirrorRoot, false);
            m.T.position = pos;
            m.Go.AddComponent<NetLocalOnly>();

            float rot = 0f;
            Vector2 scale = Vector2.one;
            switch (kind)
            {
                case Kind.Enemy: ReadEnemyHeader(r, m, from, id); break;
                case Kind.Fx:
                    rot = r.Angle();
                    scale = new Vector2(r.Scale(), r.Scale());
                    m.T.rotation = Quaternion.Euler(0f, 0f, rot);
                    m.T.localScale = new Vector3(scale.x, scale.y, 1f);
                    break;
            }

            int parts = (int)r.VarUInt();
            for (int i = 0; i < parts && !r.Failed; i++)
            {
                PartState st = default;
                st.Scale = Vector2.one;
                ReadPart(r, ref st);
                SpriteRenderer sr = AddPart(m, i);
                ApplyPart(m, sr, ref st);
                partStates[sr] = st;
            }

            if (kind == Kind.Player)
            {
                m.Avatar = m.Go.AddComponent<NetAvatar>();
                m.Avatar.Init(from);
            }

            m.Go.SetActive(true);
            if (m.Enemy != null && m.Parts.Count > 0) m.Enemy.SetProxyRenderer(m.Parts[0]);

            m.Snaps[0] = new Snap { Time = Time.unscaledTime, Pos = pos, Rot = rot, Scale = scale };
            m.SnapCount = 1;
            mirrors[key] = m;
            mirrorList.Add(m);
        }
    }

    private void ReadEnemyHeader(NetReader r, Mirror m, int owner, int id)
    {
        var identity = (EnemyId)r.VarInt();
        var role = (EnemyRole)r.Byte();
        float maxHp = r.Float();
        float hp = r.Float();
        float damage = r.Float();
        byte flags = r.Byte();
        int layer = r.Byte();
        string displayName = r.String();

        byte shape = r.Byte();
        Vector2 offset = r.Vector2();
        Vector2 size = r.Vector2();
        byte colFlags = r.Byte();

        m.Go.layer = layer;
        // Alle Waffen erkennen Gegner am Tag - ohne ihn flogen die Treffer durch die Gegner der anderen.
        m.Go.tag = "Enemy";
        m.Rb = m.Go.AddComponent<Rigidbody2D>();
        m.Rb.bodyType = RigidbodyType2D.Kinematic;
        m.Rb.useFullKinematicContacts = true;
        m.Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        m.Rb.gravityScale = 0f;

        bool trigger = (colFlags & 1) != 0;
        switch (shape)
        {
            case 2:
                BoxCollider2D box = m.Go.AddComponent<BoxCollider2D>();
                box.offset = offset;
                box.size = size;
                box.isTrigger = trigger;
                break;
            case 3:
                CapsuleCollider2D cap = m.Go.AddComponent<CapsuleCollider2D>();
                cap.offset = offset;
                cap.size = size;
                cap.direction = (colFlags & 2) != 0 ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                cap.isTrigger = trigger;
                break;
            default:
                CircleCollider2D circle = m.Go.AddComponent<CircleCollider2D>();
                circle.offset = offset;
                circle.radius = Mathf.Max(0.05f, size.x);
                circle.isTrigger = trigger;
                break;
        }

        m.Enemy = m.Go.AddComponent<Enemy>();
        m.Enemy.ConfigureProxy(owner, id, identity, role, maxHp, hp, damage, displayName);
        m.Enemy.SetProxyFlags(flags);
    }

    private SpriteRenderer AddPart(Mirror m, int index)
    {
        var go = new GameObject("Part" + index);
        go.transform.SetParent(m.T, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        m.Parts.Add(sr);
        return sr;
    }

    private void ApplyPart(Mirror m, SpriteRenderer sr, ref PartState st)
    {
        sr.sprite = Sprites.Sprite(m.Owner, st.Sprite);
        Material mat = Sprites.Material(m.Owner, st.Material);
        if (mat != null && sr.sharedMaterial != mat) sr.sharedMaterial = mat;
        sr.color = st.Color;
        sr.enabled = (st.Flags & PartVisible) != 0;
        sr.flipX = (st.Flags & PartFlipX) != 0;
        sr.flipY = (st.Flags & PartFlipY) != 0;
        sr.sortingLayerID = st.Layer;
        sr.sortingOrder = st.Order;
        sr.drawMode = (SpriteDrawMode)st.DrawMode;
        if (sr.drawMode != SpriteDrawMode.Simple) sr.size = st.Size;

        Transform t = sr.transform;
        t.localPosition = st.Offset;
        t.localRotation = Quaternion.Euler(0f, 0f, st.Rot);
        t.localScale = new Vector3(st.Scale.x, st.Scale.y, 1f);
    }

    /// <summary>Teile des Empfaengers tragen ihren letzten Stand, damit Deltas darauf aufbauen.</summary>
    private readonly Dictionary<SpriteRenderer, PartState> partStates = new Dictionary<SpriteRenderer, PartState>();

    private void OnLook(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            byte flags = r.Byte();
            mirrors.TryGetValue(Key(from, id), out Mirror m);

            if ((flags & 1) != 0)
            {
                int parts = (int)r.VarUInt();
                if (m != null)
                {
                    foreach (SpriteRenderer old in m.Parts)
                        if (old != null) { partStates.Remove(old); Destroy(old.gameObject); }
                    m.Parts.Clear();
                }
                for (int i = 0; i < parts && !r.Failed; i++)
                {
                    PartState st = default;
                    st.Scale = Vector2.one;
                    ReadPart(r, ref st);
                    if (m == null) continue;
                    SpriteRenderer sr = AddPart(m, i);
                    ApplyPart(m, sr, ref st);
                    partStates[sr] = st;
                }
                if (m != null && m.Enemy != null && m.Parts.Count > 0) m.Enemy.SetProxyRenderer(m.Parts[0]);
            }
            else
            {
                int changed = (int)r.VarUInt();
                for (int i = 0; i < changed && !r.Failed; i++)
                {
                    int index = (int)r.VarUInt();
                    SpriteRenderer sr = m != null && index < m.Parts.Count ? m.Parts[index] : null;
                    PartState st = default;
                    st.Scale = Vector2.one;
                    if (sr != null && !partStates.TryGetValue(sr, out st)) st = CaptureMirrorPart(sr);
                    ReadPart(r, ref st);
                    if (sr == null) continue;
                    ApplyPart(m, sr, ref st);
                    partStates[sr] = st;
                }
            }

            if ((flags & 2) != 0)
            {
                float hp = r.Float();
                float max = r.Float();
                if (m != null && m.Enemy != null) m.Enemy.NetSetHealth(hp, max);
            }
            if ((flags & 4) != 0)
            {
                byte ef = r.Byte();
                if (m != null && m.Enemy != null) m.Enemy.SetProxyFlags(ef);
            }
        }
    }

    private PartState CaptureMirrorPart(SpriteRenderer sr)
    {
        // Rueckfall, falls ein Teil beim Eintritt nicht im Speicher landete.
        var st = new PartState
        {
            Color = sr.color,
            Flags = (byte)((sr.enabled ? PartVisible : 0) | (sr.flipX ? PartFlipX : 0) | (sr.flipY ? PartFlipY : 0)),
            Layer = sr.sortingLayerID,
            Order = (short)sr.sortingOrder,
            Offset = sr.transform.localPosition,
            Rot = sr.transform.localEulerAngles.z,
            Scale = sr.transform.localScale,
            DrawMode = (byte)sr.drawMode,
            Size = sr.drawMode != SpriteDrawMode.Simple ? sr.size : Vector2.zero,
        };
        return st;
    }

    private void OnLeave(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            byte reason = r.Byte();
            if (reason == 2) r.Vector2();
            if (!mirrors.TryGetValue(Key(from, id), out Mirror m)) continue;

            if (reason == 2 && m.Enemy != null) AudioController.Instance?.PlayEnemyDeath();
            DestroyMirror(m);
        }
    }

    private void OnMotion(int from, NetReader r)
    {
        Vector2 origin = r.Vector2();
        int count = r.UShort();
        float now = Time.unscaledTime;

        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            byte flags = r.Byte();
            Vector2 pos = (flags & 1) != 0 ? r.Vector2() : r.CompactPos(origin);
            float rot = (flags & 2) != 0 ? r.Angle() : 0f;
            Vector2 scale = (flags & 4) != 0 ? new Vector2(r.Scale(), r.Scale()) : Vector2.one;

            if (!mirrors.TryGetValue(Key(from, id), out Mirror m)) continue;

            // Neuer Stand hinten an, der aelteste faellt raus.
            if (m.SnapCount == m.Snaps.Length)
            {
                for (int i = 1; i < m.Snaps.Length; i++) m.Snaps[i - 1] = m.Snaps[i];
                m.SnapCount--;
            }
            m.Snaps[m.SnapCount++] = new Snap { Time = now, Pos = pos, Rot = rot, Scale = scale };
        }
    }

    private void UpdateMirrors()
    {
        float renderTime = Time.unscaledTime - InterpDelay;

        for (int i = mirrorList.Count - 1; i >= 0; i--)
        {
            Mirror m = mirrorList[i];
            if (m.Go == null)
            {
                mirrorList.RemoveAt(i);
                mirrors.Remove(Key(m.Owner, m.Id));
                continue;
            }
            if (m.SnapCount == 0) continue;

            Snap a = m.Snaps[0], b = m.Snaps[0];
            for (int k = 0; k < m.SnapCount; k++)
            {
                if (m.Snaps[k].Time <= renderTime) a = m.Snaps[k];
                if (m.Snaps[k].Time >= renderTime) { b = m.Snaps[k]; break; }
                b = m.Snaps[k];
            }

            Vector2 pos;
            float rot;
            Vector2 scale;
            if (b.Time > a.Time)
            {
                float t = Mathf.Clamp01((renderTime - a.Time) / (b.Time - a.Time));
                pos = Vector2.LerpUnclamped(a.Pos, b.Pos, t);
                rot = Mathf.LerpAngle(a.Rot, b.Rot, t);
                scale = Vector2.Lerp(a.Scale, b.Scale, t);
                m.Velocity = (b.Pos - a.Pos) / (b.Time - a.Time);
            }
            else
            {
                pos = b.Pos;
                rot = b.Rot;
                scale = b.Scale;
            }

            // Weite Spruenge (Teleport, Nachziehen) nicht durchs Bild gleiten lassen.
            if (((Vector2)m.T.position - pos).sqrMagnitude > 64f && m.Rb != null) m.Rb.position = pos;

            if (m.Rb != null)
            {
                m.Target = pos;
                m.HasTarget = true;
                if (m.Enemy != null) m.Enemy.ProxyVelocity = m.Velocity;
            }
            else
            {
                m.T.position = new Vector3(pos.x, pos.y, m.T.position.z);
            }

            if (m.Kind == Kind.Fx)
            {
                m.T.rotation = Quaternion.Euler(0f, 0f, rot);
                m.T.localScale = new Vector3(scale.x, scale.y, 1f);
            }

            if (m.Avatar != null)
            {
                // Wer raus ist, ist unsichtbar (seine Figur ist bei ihm aus).
                bool show = m.Avatar.State != NetRun.PeerState.Out && m.Avatar.State != NetRun.PeerState.Left;
                foreach (SpriteRenderer sr in m.Parts)
                    if (sr != null && !show) sr.enabled = false;
            }
        }
    }

    private void DestroyMirror(Mirror m)
    {
        mirrors.Remove(Key(m.Owner, m.Id));
        mirrorList.Remove(m);
        foreach (SpriteRenderer sr in m.Parts) if (sr != null) partStates.Remove(sr);
        if (m.Go != null) Destroy(m.Go);
    }

    // ==================================================================
    //  Treffer an fremden Gegnern
    // ==================================================================

    /// <summary>Ein eigener Treffer an einem Stellvertreter - geht gesammelt an seinen Besitzer.</summary>
    public void QueueHit(Enemy proxy, float damage, float knockback, float? slow)
    {
        if (proxy == null || !proxy.IsRemote) return;
        int owner = proxy.NetOwner;
        if (owner < 0 || owner >= NetSession.MaxPlayers) return;
        hits[owner].Add(new Hit { Id = proxy.NetId, Damage = damage, Knock = knockback, Slow = slow ?? -1f });
        HitsSent++;
    }

    /// <summary>Zaehler fuer den Koop-Test: Treffer an fremden Gegnern raus / an eigenen rein.</summary>
    public int HitsSent { get; private set; }
    public int HitsReceived { get; private set; }

    /// <summary>Bremsen/Festkleben/Ziehen/Locken an einem Stellvertreter.</summary>
    public void QueueCtrl(Enemy proxy, byte kind, float a, float b = 0f, float c = 0f)
    {
        if (proxy == null || !proxy.IsRemote) return;
        int owner = proxy.NetOwner;
        if (owner < 0 || owner >= NetSession.MaxPlayers) return;
        ctrls[owner].Add(new Ctrl { Id = proxy.NetId, Kind = kind, A = a, B = b, C = c });
    }

    public const byte CtrlFreeze = 1, CtrlSlow = 2, CtrlPull = 3, CtrlLure = 4, CtrlEndFreeze = 5;

    private void FlushHits()
    {
        for (int p = 0; p < NetSession.MaxPlayers; p++)
        {
            List<Hit> list = hits[p];
            if (list.Count > 0)
            {
                hitW.Clear();
                hitW.UShort((ushort)Mathf.Min(list.Count, ushort.MaxValue));
                foreach (Hit h in list)
                {
                    hitW.VarUInt((uint)h.Id);
                    hitW.Float(h.Damage);
                    hitW.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(h.Knock * 100f), 0, 255));
                    hitW.Byte(h.Slow < 0f ? (byte)255 : (byte)Mathf.Clamp(Mathf.RoundToInt(h.Slow * 200f), 0, 254));
                }
                list.Clear();
                session.SendPayload(NetMsg.EnemyHit, hitW, (byte)p, true);
            }

            List<Ctrl> cl = ctrls[p];
            if (cl.Count > 0)
            {
                ctrlW.Clear();
                ctrlW.UShort((ushort)Mathf.Min(cl.Count, ushort.MaxValue));
                foreach (Ctrl c in cl)
                {
                    ctrlW.VarUInt((uint)c.Id);
                    ctrlW.Byte(c.Kind);
                    ctrlW.Float(c.A);
                    ctrlW.Float(c.B);
                    ctrlW.Float(c.C);
                }
                cl.Clear();
                session.SendPayload(NetMsg.EnemyCtrl, ctrlW, (byte)p, true);
            }
        }
    }

    private void OnEnemyHit(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            float damage = r.Float();
            float knock = r.Byte() / 100f;
            byte slowByte = r.Byte();
            float? slow = slowByte == 255 ? (float?)null : slowByte / 200f;

            if (!sources.TryGetValue(id, out Source s) || s.Enemy == null) continue;
            s.Enemy.NetReceiveHit(from, damage, knock, slow);
            HitsReceived++;
        }
    }

    private void OnEnemyCtrl(int from, NetReader r)
    {
        int count = r.UShort();
        for (int n = 0; n < count && !r.Failed; n++)
        {
            int id = (int)r.VarUInt();
            byte kind = r.Byte();
            float a = r.Float(), b = r.Float(), c = r.Float();
            if (!sources.TryGetValue(id, out Source s) || s.Enemy == null) continue;
            Enemy e = s.Enemy;

            switch (kind)
            {
                case CtrlFreeze: e.Freeze(a, b); break;
                case CtrlEndFreeze: e.EndFreeze(); break;
                case CtrlSlow: e.ApplySlow(a, b); break;
                case CtrlPull: e.ApplyPull(new Vector2(a, b), c); break;
                case CtrlLure: e.Lure(LurePoint(from, new Vector2(a, b)), c); break;
            }
        }
    }

    /// <summary>Ein Punkt, zu dem Gegner gelockt werden (die Musikbox eines Mitspielers).</summary>
    private Transform LurePoint(int from, Vector2 at)
    {
        if (!lurePoints.TryGetValue(from, out Transform t) || t == null)
        {
            var go = new GameObject("Lockpunkt " + from);
            go.transform.SetParent(mirrorRoot, false);
            t = go.transform;
            lurePoints[from] = t;
        }
        t.position = at;
        return t;
    }
}

/// <summary>
/// Markiert Objekte, die nur hier zu sehen sein sollen und nie an
/// Mitspieler gehen (Stellvertreter, Ortsanzeigen, eigene HUD-Sprites in der Welt).
/// </summary>
public class NetLocalOnly : MonoBehaviour { }
