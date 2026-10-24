using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Welt fuer alle. Jeder Rechner baut die Karte selbst - damit sie bei
/// allen gleich aussieht und die Spieler sich darin treffen koennen, haengt
/// im Koop alles Zufaellige an der Stelle und am Seed des Laufs statt an
/// UnityEngine.Random und an der Reihenfolge, in der jemand irgendwo hinlaeuft:
///
///  * Boden-Chunks (<see cref="WorldManager3x3"/>): jede Zelle hat einen festen
///    Inhalt. Statt neun Chunks, die mit EINEM Spieler mitwandern, liegen um
///    jeden Spieler drei mal drei - wo sich zwei treffen, wird die Zelle nur
///    einmal belegt. Laufen alle zusammen, ist es genau das Bild von allein.
///  * Baeume und Steine darin (<see cref="ChunkPropRandomizer"/>): gewuerfelt aus Seed + Zelle.
///  * Mixer und Deko der Zufalls-Spawner: gewuerfelt aus Seed + Block, jeder
///    Block fuer sich (kein Abstand ueber Blockgrenzen, der von der
///    Reihenfolge abhinge). Die Spawner bauen Bloecke um alle Spieler herum.
///  * Mixer: wer einen benutzt, sagt es allen - bei ihnen ist er dann auch leer;
///    wer in dem Moment mit im Feld steht, bekommt seine eigene Auswahl.
///
/// Allein laeuft alles wie immer (Seed = keiner).
/// </summary>
public class NetWorld : MonoBehaviour
{
    public static NetWorld Active { get; private set; }

    /// <summary>Seed des Laufs, sonst null (allein).</summary>
    public static int? Seed => NetSession.InRun ? NetSession.Instance.Run.Seed : (int?)null;

    public static bool Deterministic => Seed.HasValue;

    private NetSession session;

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        session.On(NetMsg.MixerUsed, OnMixerUsed);
        ResetRun();
        PlayerController pc = PlayerController.Instance;
        if (pc != null) StartPoint = pc.transform.position;
    }

    private void OnDestroy()
    {
        session?.Off(NetMsg.MixerUsed);
        if (Active == this) Active = null;
    }

    // ==================================================================
    //  Zufall nach Ort
    // ==================================================================

    /// <summary>Stabiler Hash (nicht string.GetHashCode - der ist nicht ueberall gleich).</summary>
    public static int Hash(string text)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (char c in text ?? "") h = (h ^ c) * 16777619;
            return h;
        }
    }

    public static int Mix(int a, int b, int c = 0, int d = 0)
    {
        unchecked
        {
            int h = a * 73856093;
            h ^= b * 19349663;
            h ^= c * 83492791;
            h ^= d * 50331653;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h;
        }
    }

    /// <summary>Zufallsquelle fuer eine Stelle der Welt - bei allen Spielern dieselbe.</summary>
    public static System.Random RandomFor(int salt, int x, int y)
    {
        return new System.Random(Mix(Seed ?? 0, salt, x, y));
    }

    // ==================================================================
    //  Anker: um wen herum Welt gebraucht wird
    // ==================================================================

    private static readonly List<Vector2> anchors = new List<Vector2>();

    /// <summary>
    /// Alle Spieler, um die herum Boden und Objekte stehen muessen: der
    /// eigene (auch als Geist) und die Figuren der Mitspieler. Allein nur der
    /// eigene. Die Liste wird wiederverwendet - nicht aufbewahren.
    /// </summary>
    public static List<Vector2> Anchors()
    {
        anchors.Clear();
        PlayerController pc = PlayerController.Instance;
        if (pc != null && pc.gameObject.activeInHierarchy) anchors.Add(pc.transform.position);

        if (NetRun.Active != null)
        {
            foreach (NetAvatar a in NetPlayers.Avatars)
                if (a != null) anchors.Add(a.transform.position);

            // Wer raus ist und zuschaut, braucht den Boden um den, dem er zuschaut.
            if (anchors.Count == 0)
            {
                NetAvatar watched = NetPlayers.AvatarOf(NetRun.Active.Spectating);
                if (watched != null) anchors.Add(watched.transform.position);
            }
        }
        return anchors;
    }

    /// <summary>Wo der Spieler in GameCore steht, bevor die Koop-Plaetze verteilt werden - bei allen gleich.</summary>
    public static Vector2 StartPoint;

    /// <summary>Im Koop: nicht direkt auf die Startpunkte bauen (dort stehen alle beim Laden).</summary>
    public static bool NearStart(Vector2 position, float radius = 6f)
    {
        return (position - StartPoint).sqrMagnitude < radius * radius;
    }

    // ==================================================================
    //  Mixer
    // ==================================================================

    /// <summary>Schluessel eines Mixers: aus seiner Stelle - die ist bei allen gleich.</summary>
    public static long MixerKey(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x * 4f);
        int y = Mathf.RoundToInt(position.y * 4f);
        return ((long)x << 32) | (uint)y;
    }

    /// <summary>Ein eigener Mixer ist ausgeloest worden - bei allen ist er dann leer.</summary>
    public void LocalMixerUsed(MixerObject mixer)
    {
        if (mixer == null) return;
        NetWriter w = session.Begin(NetMsg.MixerUsed);
        w.Long(MixerKey(mixer.transform.position));
        session.SendAll();
    }

    private void OnMixerUsed(int from, NetReader r)
    {
        long key = r.Long();
        IReadOnlyList<MixerObject> all = MixerObject.All;
        for (int i = 0; i < all.Count; i++)
        {
            MixerObject m = all[i];
            if (m != null && MixerKey(m.transform.position) == key)
            {
                // Wer mit im Feld steht, bekommt auch eine Auswahl.
                m.TeammateFinished();
                return;
            }
        }
        // Noch nicht gebaut (weit weg): merken, der Spawner fragt beim Bauen nach.
        usedMixers.Add(key);
    }

    private static readonly HashSet<long> usedMixers = new HashSet<long>();

    /// <summary>Wurde dieser Mixer schon von einem Mitspieler geleert, bevor er bei uns stand?</summary>
    public static bool MixerAlreadyUsed(Vector2 position) => usedMixers.Contains(MixerKey(position));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => usedMixers.Clear();

    /// <summary>Beim Start eines Koop-Laufs gilt kein Mixer als benutzt.</summary>
    public static void ResetRun() => usedMixers.Clear();
}
