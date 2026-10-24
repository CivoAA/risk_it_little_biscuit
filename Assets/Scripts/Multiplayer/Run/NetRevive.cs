using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koop: der Kreuz-Altar. Wer niedergegangen ist und in der Ausblutzeit
/// (<see cref="NetBalance.BleedOutSeconds"/>) nicht gerettet wurde, ist raus -
/// aber nicht verloren: ein Mixer etwa <see cref="AltarDistanceFields"/>
/// Map-Felder entfernt wird zum Kreuz, an dem seine Seele haengt
/// (<see cref="ReviveAltar"/>). Ein Totenkopf-Pfeil zeigt den Lebenden den Weg.
///
/// Um das Kreuz liegt ein Kreis, <see cref="RadiusFactor"/>-mal so gross wie
/// ein Mixer-Feld. Die Rettung dauert <see cref="BaseSeconds"/> plus
/// <see cref="SecondsPerPlayer"/> je Spieler im Lauf (der Tote zaehlt mit).
/// Die Uhr laeuft nur, solange Lebende im Kreis stehen - und je Lebendem
/// einmal: zwei Spieler eine Minute = zwei Minuten geschafft. Solange jemand
/// drin steht, gibt es <see cref="Chaos"/> mehr Chaos (ohne mehr Belohnung).
/// Geschafft: der Tote steht am Kreuz mit <see cref="ReviveHealth"/> seiner
/// Max-HP wieder auf.
///
/// WER ENTSCHEIDET
///
/// Der Host. Er sieht den Spieler rausgehen, sucht den Mixer aus (aus den
/// geplanten Mixern der Zufalls-Spawner - die sind bei allen gleich), zaehlt
/// mit den Positionen aller, wer im Kreis steht, und meldet Fortschritt und
/// Ende. Alle anderen zeigen nur an.
/// </summary>
public class NetRevive : MonoBehaviour
{
    /// <summary>So viele Map-Felder (Boden-Chunks) liegt der Altar ungefaehr vom Toten entfernt.</summary>
    public const float AltarDistanceFields = 3f;

    /// <summary>Rettung: so viele Sekunden ...</summary>
    public const float BaseSeconds = 1f;

    /// <summary>... plus so viele je Spieler im Lauf.</summary>
    public const float SecondsPerPlayer = 60f;

    /// <summary>Mehr Chaos, solange am Kreuz gerettet wird.</summary>
    public const float Chaos = 0.4f;

    /// <summary>Kreis um das Kreuz, in Mixer-Feldern.</summary>
    public const float RadiusFactor = 2.5f;

    /// <summary>Mit diesem Anteil der Max-HP steht man am Kreuz wieder auf.</summary>
    public const float ReviveHealth = 0.8f;

    private const float FallbackFieldSize = 46f;       // Boden-Chunk 52x40
    private const float FallbackMixerRadius = 4.25f;   // Feld des Mixer-Prefabs
    private const float ProgressInterval = 0.2f;
    private const float ProgressTimeout = 1f;

    public static NetRevive Active { get; private set; }

    // Stellen (MixerKey), an denen ein Mixer zum Kreuz wurde - dort baut kein Spawner mehr einen Mixer.
    private static readonly HashSet<long> altarSpots = new HashSet<long>();

    /// <summary>Steht hier ein Kreuz statt des Mixers? Fragen die Zufalls-Spawner im Koop.</summary>
    public static bool IsAltarSpot(Vector2 mixerPosition) =>
        altarSpots.Count > 0 && altarSpots.Contains(NetWorld.MixerKey(mixerPosition));

    private sealed class Altar
    {
        public int Slot;
        public Vector2 Position;
        public float Radius;
        public float Required;
        public float Elapsed;
        public int Inside;
        public float LastHeard;
        public ReviveAltar View;
    }

    private readonly Dictionary<int, Altar> altars = new Dictionary<int, Altar>();

    /// <summary>Haengt dieser Spieler gerade an einem Kreuz? Fortschritt 0..1 (fuer das HUD).</summary>
    public bool TryGetAltar(int slot, out float progress, out int inside)
    {
        progress = 0f;
        inside = 0;
        if (!altars.TryGetValue(slot, out Altar a)) return false;
        progress = Mathf.Clamp01(a.Elapsed / a.Required);
        inside = a.Inside;
        return true;
    }
    /// <summary>Naechstes Keksglas von <paramref name="from"/> aus (fuer den Test-Bot).</summary>
    public bool TryNearestAltar(Vector2 from, out Vector2 position)
    {
        position = default;
        float best = float.MaxValue;
        foreach (Altar a in altars.Values)
        {
            float d = (a.Position - from).sqrMagnitude;
            if (d < best) { best = d; position = a.Position; }
        }
        return best < float.MaxValue;
    }

    private readonly List<int> finished = new List<int>();
    private NetSession session;
    private float progressTimer;
    private AltarPointer pointer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => altarSpots.Clear();

    // ==================================================================
    //  Aufbau
    // ==================================================================

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        altarSpots.Clear();
        session.On(NetMsg.ReviveAltar, OnReviveAltar);
        session.On(NetMsg.ReviveProgress, OnReviveProgress);
        session.On(NetMsg.ReviveDone, OnReviveDone);
        NetRun.StateChanged += OnStateChanged;
        pointer = AltarPointer.Create(transform);
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.Off(NetMsg.ReviveAltar);
            session.Off(NetMsg.ReviveProgress);
            session.Off(NetMsg.ReviveDone);
        }
        NetRun.StateChanged -= OnStateChanged;
        foreach (Altar a in altars.Values)
            if (a.View != null) Destroy(a.View.gameObject);
        altars.Clear();
        altarSpots.Clear();
        RunDifficulty.ReviveChaos = 0f;
        if (Active == this) Active = null;
    }

    // ==================================================================
    //  Raus -> Kreuz
    // ==================================================================

    private void OnStateChanged(int slot, NetRun.PeerState before, NetRun.PeerState after)
    {
        // Weg oder anders gerettet: das Kreuz hat keinen mehr, auf den es wartet.
        if (after == NetRun.PeerState.Left || after == NetRun.PeerState.Alive)
        {
            if (altars.TryGetValue(slot, out Altar a)) RemoveAltar(a, false);
            return;
        }

        if (after == NetRun.PeerState.Out && NetSession.IsHost) HostCreateAltar(slot);
    }

    private void HostCreateAltar(int slot)
    {
        NetRun run = NetRun.Active;
        if (run == null || altars.ContainsKey(slot)) return;

        Vector2 death = run.Peers[slot].Position;
        Vector2 spot = PickSpot(death);
        float required = BaseSeconds + SecondsPerPlayer * Mathf.Max(1, run.PlayersInRun);

        NetWriter w = session.Begin(NetMsg.ReviveAltar);
        w.Byte((byte)slot);
        w.Vector2(spot);
        w.Float(required);
        session.SendAll();

        SpawnAltar(slot, spot, required);
    }

    /// <summary>
    /// Der Mixer, dessen Abstand zum Toten am naechsten an
    /// <see cref="AltarDistanceFields"/> Map-Feldern liegt - aus den
    /// geplanten Mixern, also auch dort, wo noch keiner war. Gibt es keinen,
    /// steht das Kreuz einfach in dieser Entfernung.
    /// </summary>
    private static Vector2 PickSpot(Vector2 death)
    {
        float target = AltarDistanceFields * FieldSize();
        var area = new Rect(death - Vector2.one * target * 1.6f, Vector2.one * target * 3.2f);

        var candidates = new List<Vector2>();
        RandomObjectSpawner3x3.PlannedMixers(area, candidates);
        RandomObjectSpawner.PlannedMixers(area, candidates);

        bool found = false;
        Vector2 best = Vector2.zero;
        float bestScore = float.MaxValue;
        foreach (Vector2 c in candidates)
        {
            if (IsAltarSpot(c)) continue;
            float d = Vector2.Distance(c, death);
            if (d < target * 0.5f) continue;
            float score = Mathf.Abs(d - target);
            if (score < bestScore) { bestScore = score; best = c; found = true; }
        }
        if (found) return best;

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        return death + dir.normalized * target;
    }

    /// <summary>Groesse eines Map-Felds (Boden-Chunk) in Welt-Einheiten.</summary>
    private static float FieldSize()
    {
        WorldManager3x3 world = WorldManager3x3.Current;
        return world != null ? (world.chunkWidth + world.chunkHeight) * 0.5f : FallbackFieldSize;
    }

    private static float AltarRadius()
    {
        float r = FallbackMixerRadius;
        var mixers = MixerObject.All;
        for (int i = 0; i < mixers.Count; i++)
            if (mixers[i] != null) { r = MixerPlacement.FieldRadius(mixers[i].gameObject); break; }
        return r * RadiusFactor;
    }

    private void OnReviveAltar(int from, NetReader r)
    {
        int slot = r.Byte();
        Vector2 spot = r.Vector2();
        float required = r.Float();
        if (NetSession.IsHost) return;
        SpawnAltar(slot, spot, required);
    }

    private void SpawnAltar(int slot, Vector2 spot, float required)
    {
        NetRun run = NetRun.Active;
        if (run == null || slot < 0 || slot >= run.Peers.Length || altars.ContainsKey(slot)) return;

        // Der Mixer an dieser Stelle verwandelt sich: weg damit, und kein Spawner baut ihn neu.
        long key = NetWorld.MixerKey(spot);
        altarSpots.Add(key);
        var mixers = MixerObject.All;
        for (int i = mixers.Count - 1; i >= 0; i--)
            if (mixers[i] != null && NetWorld.MixerKey(mixers[i].transform.position) == key)
                Destroy(mixers[i].gameObject);

        var altar = new Altar
        {
            Slot = slot,
            Position = spot,
            Radius = AltarRadius(),
            Required = Mathf.Max(1f, required),
            LastHeard = Time.unscaledTime,
        };
        altar.View = ReviveAltar.Create(spot, altar.Radius, run.Peers[slot].Character);
        altars[slot] = altar;

        bool me = slot == session.LocalSlot;
        string name = run.Peers[slot].Name.ToUpperInvariant();
        NetRun.ShowToast(me
            ? NetText.Get("net.revive.altar_self", "DU STECKST IM KEKSGLAS - DEIN TEAM KANN DICH BEFREIEN")
            : string.Format(NetText.Get("net.revive.altar_other", "{0} STECKT IM KEKSGLAS - FOLGT DEM PFEIL!"), name),
            GameHudSkin.IcingLight);
        Debug.Log($"[NetRevive] Keksglas fuer {name} bei {spot}, Rettung {altar.Required:0} s.");
    }

    // ==================================================================
    //  Retten
    // ==================================================================

    private void Update()
    {
        NetRun run = NetRun.Active;
        if (run == null || run.CurrentPhase != NetRun.Phase.Running)
        {
            RunDifficulty.ReviveChaos = 0f;
            return;
        }

        if (NetSession.IsHost) HostTick(run);

        int active = 0;
        foreach (Altar a in altars.Values)
        {
            // Bleibt der Stand des Hosts aus, gilt keiner mehr als drin.
            if (!NetSession.IsHost && Time.unscaledTime - a.LastHeard > ProgressTimeout) a.Inside = 0;
            if (a.Inside > 0) active++;
            if (a.View != null) a.View.SetProgress(a.Elapsed / a.Required, a.Inside > 0);
        }
        RunDifficulty.ReviveChaos = active * Chaos;
    }

    private void HostTick(NetRun run)
    {
        float dt = Time.deltaTime;   // steht bei gemeinsamer Pause still
        progressTimer -= Time.unscaledDeltaTime;
        bool send = progressTimer <= 0f;
        if (send) progressTimer = ProgressInterval;

        finished.Clear();
        foreach (Altar a in altars.Values)
        {
            a.Inside = CountInside(run, a);
            a.Elapsed += dt * a.Inside;
            if (a.Elapsed >= a.Required) { finished.Add(a.Slot); continue; }

            if (send)
            {
                NetWriter w = session.Begin(NetMsg.ReviveProgress);
                w.Byte((byte)a.Slot);
                w.Float(a.Elapsed);
                w.Byte((byte)a.Inside);
                session.SendAll(false);
            }
        }

        foreach (int slot in finished)
        {
            session.Begin(NetMsg.ReviveDone).Byte((byte)slot);
            session.SendAll();
            Finish(slot);
        }
    }

    /// <summary>Lebende im Kreis - der Tote zaehlt nicht, Geister auch nicht.</summary>
    private int CountInside(NetRun run, Altar a)
    {
        int n = 0;
        float r2 = a.Radius * a.Radius;
        PlayerController pc = PlayerController.Instance;
        foreach (NetRun.Peer p in run.Peers)
        {
            if (!p.Playing || p.State != NetRun.PeerState.Alive || p.Slot == a.Slot) continue;
            Vector2 at = p.Slot == session.LocalSlot && pc != null ? (Vector2)pc.transform.position : p.Position;
            if ((at - a.Position).sqrMagnitude <= r2) n++;
        }
        return n;
    }

    private void OnReviveProgress(int from, NetReader r)
    {
        int slot = r.Byte();
        float elapsed = r.Float();
        int inside = r.Byte();
        if (NetSession.IsHost || !altars.TryGetValue(slot, out Altar a)) return;
        a.Elapsed = elapsed;
        a.Inside = inside;
        a.LastHeard = Time.unscaledTime;
    }

    private void OnReviveDone(int from, NetReader r)
    {
        int slot = r.Byte();
        if (!NetSession.IsHost) Finish(slot);
    }

    private void Finish(int slot)
    {
        if (!altars.TryGetValue(slot, out Altar a)) return;
        Vector2 at = a.Position;
        RemoveAltar(a, true);

        // Der Gerettete steht vor dem Kreuz auf - ueber sich selbst bestimmt er.
        if (slot == session.LocalSlot) NetRun.Active?.ReviveFromOut(at + Vector2.down * 1.2f, ReviveHealth);
    }

    private void RemoveAltar(Altar a, bool rescued)
    {
        altars.Remove(a.Slot);
        if (a.View != null) a.View.Release(rescued);
    }

    // ==================================================================
    //  Totenkopf-Pfeil
    // ==================================================================

    private void LateUpdate()
    {
        PlayerController pc = PlayerController.Instance;
        NetRun run = NetRun.Active;
        bool canShow = pc != null && pc.gameObject.activeInHierarchy && run != null && run.LocalAlive;

        Altar nearest = null;
        if (canShow)
        {
            float best = float.MaxValue;
            foreach (Altar a in altars.Values)
            {
                float d = ((Vector2)pc.transform.position - a.Position).sqrMagnitude;
                if (d < best) { best = d; nearest = a; }
            }
        }
        pointer.Track(canShow ? pc : null, nearest != null ? nearest.Position : (Vector2?)null);
    }
}
