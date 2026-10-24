using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wen die Gegner jagen und wen ihre Attacken treffen koennen - der eigene
/// Spieler und im Koop die Figuren der Mitspieler (<see cref="NetAvatar"/>).
///
/// Allein ist das genau der <see cref="PlayerController"/> und alles laeuft
/// wie immer. Im Koop sucht sich jeder Gegner den naechsten Spieler, der noch
/// steht (nicht niedergegangen, nicht ausgeschieden). Schaden an einem
/// Mitspieler wird nicht hier abgezogen, sondern ihm geschickt - sein Leben
/// gehoert ihm (<see cref="Hurt"/>).
///
/// <c>PlayerController.Instance</c> bleibt ueberall "der eigene Spieler":
/// HUD, Waffen, Stats, Level-Up. Nur wer GEGNER steuert, fragt hier.
/// </summary>
public static class NetPlayers
{
    /// <summary>Ein Spieler, den ein Gegner angreifen kann.</summary>
    public struct Target
    {
        public Transform Transform;
        /// <summary>Platz in der Sitzung, -1 allein.</summary>
        public int Slot;
        public bool IsLocal;

        public bool Valid => Transform != null;
        public Vector2 Position => Transform != null ? (Vector2)Transform.position : Vector2.zero;
        public PlayerController Local => IsLocal ? PlayerController.Instance : null;
    }

    private static readonly List<NetAvatar> avatars = new List<NetAvatar>();

    /// <summary>Die Figuren der Mitspieler, die gerade im Lauf stehen (auch niedergegangene).</summary>
    public static IReadOnlyList<NetAvatar> Avatars => avatars;

    public static void Register(NetAvatar avatar)
    {
        if (avatar != null && !avatars.Contains(avatar)) avatars.Add(avatar);
    }

    public static void Unregister(NetAvatar avatar) => avatars.Remove(avatar);

    public static NetAvatar AvatarOf(int slot)
    {
        for (int i = 0; i < avatars.Count; i++)
            if (avatars[i] != null && avatars[i].Slot == slot) return avatars[i];
        return null;
    }

    /// <summary>Darf der eigene Spieler gerade angegriffen werden (aktiv, nicht als Geist unterwegs)?</summary>
    public static bool LocalTargetable
    {
        get
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null || !pc.gameObject.activeInHierarchy) return false;
            return NetRun.Active == null || NetRun.Active.LocalAlive;
        }
    }

    public static Target LocalTarget()
    {
        PlayerController pc = PlayerController.Instance;
        return new Target
        {
            Transform = pc != null ? pc.transform : null,
            Slot = NetSession.Instance != null ? NetSession.Instance.LocalSlot : -1,
            IsLocal = true,
        };
    }

    /// <summary>Alle angreifbaren Spieler, der eigene zuerst.</summary>
    public static void Collect(List<Target> into)
    {
        into.Clear();
        if (LocalTargetable) into.Add(LocalTarget());
        for (int i = 0; i < avatars.Count; i++)
        {
            NetAvatar a = avatars[i];
            if (a == null || !a.Targetable) continue;
            into.Add(new Target { Transform = a.transform, Slot = a.Slot, IsLocal = false });
        }
    }

    /// <summary>
    /// Der naechste angreifbare Spieler. Allein der eigene (auch wenn er
    /// gerade nicht angreifbar ist - dann gilt invalid). False = keiner da.
    /// </summary>
    public static bool TryNearest(Vector2 from, out Target target)
    {
        target = default;
        float best = float.MaxValue;

        if (LocalTargetable)
        {
            target = LocalTarget();
            best = ((Vector2)target.Transform.position - from).sqrMagnitude;
        }

        for (int i = 0; i < avatars.Count; i++)
        {
            NetAvatar a = avatars[i];
            if (a == null || !a.Targetable) continue;
            float d = ((Vector2)a.transform.position - from).sqrMagnitude;
            if (d < best)
            {
                best = d;
                target = new Target { Transform = a.transform, Slot = a.Slot, IsLocal = false };
            }
        }

        return target.Valid;
    }

    /// <summary>
    /// Quadrierter Abstand zum naechsten Spieler, der noch steht - allein der
    /// zum eigenen. Fuer den Director: ein Gegner, der gerade einen Mitspieler
    /// jagt, ist nicht "zu weit weg", nur weil er weit vom eigenen weg ist.
    /// </summary>
    public static float NearestSqr(Vector2 from, Vector2 fallback)
    {
        if (TryNearest(from, out Target t)) return (t.Position - from).sqrMagnitude;
        return (fallback - from).sqrMagnitude;
    }

    /// <summary>
    /// Liegt der Punkt im Bild eines Mitspielers (plus Rand)? Allein immer false.
    /// Zaehlt jedes Bild, nicht nur das von Stehenden: auch wer am Boden liegt
    /// oder raus ist und zuschaut, sieht etwas. Gemessen wird an der Bildmitte,
    /// die er schickt, in seine Laufrichtung gestreckt (sein Stand ist bei uns
    /// ein paar Pakete alt).
    /// </summary>
    public static bool SeenByMate(Vector2 pos, float margin)
    {
        return TryMateView(pos, margin, out _);
    }

    /// <summary>Wie <see cref="SeenByMate"/>, liefert dazu das Bild (mit Rand), in dem der Punkt liegt.</summary>
    public static bool TryMateView(Vector2 pos, float margin, out Rect view)
    {
        view = default;
        NetRun run = NetRun.Active;
        NetSession s = NetSession.Instance;
        if (run == null || s == null) return false;
        for (int slot = 0; slot < run.Peers.Length; slot++)
        {
            if (slot == s.LocalSlot) continue;
            NetRun.Peer p = run.Peers[slot];
            if (!p.Playing || p.State == NetRun.PeerState.Left || p.LastState < 0f) continue;

            // Das Bild jetzt und dort, wo es gleich ist: in Laufrichtung gestreckt.
            Vector2 lead = p.Velocity * MateLead;
            Vector2 center = p.ViewCenter + lead * 0.5f;
            Vector2 half = p.ViewHalf + new Vector2(margin + Mathf.Abs(lead.x) * 0.5f, margin + Mathf.Abs(lead.y) * 0.5f);
            Vector2 d = pos - center;
            if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.y) > half.y) continue;
            view = new Rect(center - half, half * 2f);
            return true;
        }
        return false;
    }

    /// <summary>So weit (Sekunden Laufweg) wird das Bild eines Mitspielers vorgezogen.</summary>
    private const float MateLead = 0.35f;

    /// <summary>Kurzform: Position des naechsten Spielers, sonst die des eigenen (oder from).</summary>
    public static Vector2 NearestPosition(Vector2 from)
    {
        if (TryNearest(from, out Target t)) return t.Position;
        PlayerController pc = PlayerController.Instance;
        return pc != null ? (Vector2)pc.transform.position : from;
    }

    /// <summary>Gehoert dieser Collider zu einem Spieler (eigener Koerper/Hitbox oder Mitspieler-Figur)?</summary>
    public static bool TryGetTarget(Collider2D other, out Target target)
    {
        target = default;
        if (other == null) return false;

        if (other.CompareTag("Player") || other.CompareTag("PlayerHitbox"))
        {
            if (!LocalTargetable) return false;
            target = LocalTarget();
            return true;
        }

        NetAvatar a = other.GetComponentInParent<NetAvatar>();
        if (a == null || !a.Targetable) return false;
        target = new Target { Transform = a.transform, Slot = a.Slot, IsLocal = false };
        return true;
    }

    /// <summary>
    /// Schaden an einem Spieler: der eigene nimmt ihn direkt
    /// (<see cref="PlayerController.TakeDamage"/>), ein Mitspieler bekommt eine
    /// Nachricht und zieht ihn bei sich selbst ab - dort zaehlen dann auch
    /// seine Ruestung, sein Ausweichen und seine Unverwundbarkeit.
    /// </summary>
    public static void Hurt(Target target, float damage)
    {
        if (!target.Valid || damage <= 0f) return;
        if (target.IsLocal)
        {
            PlayerController pc = PlayerController.Instance;
            if (pc != null) pc.TakeDamage(damage);
            return;
        }
        NetRun.Active?.SendPlayerHit(target.Slot, damage);
    }

    /// <summary>Fremder Zug (Sog des Milchpanzers). Jeden Physikschritt neu setzen, wie <see cref="PlayerController.AddDrift"/>.</summary>
    public static void Drift(Target target, Vector2 velocity)
    {
        if (!target.Valid) return;
        if (target.IsLocal)
        {
            PlayerController pc = PlayerController.Instance;
            if (pc != null) pc.AddDrift(velocity);
            return;
        }
        NetRun.Active?.SendPlayerDrift(target.Slot, velocity);
    }

    /// <summary>Einfrieren bis in <paramref name="seconds"/> Sekunden (Frostring des Eiskaisers).</summary>
    public static void Freeze(Target target, float seconds)
    {
        if (!target.Valid || seconds <= 0f) return;
        if (target.IsLocal)
        {
            PlayerController pc = PlayerController.Instance;
            if (pc != null) pc.FrozenUntil = Mathf.Max(pc.FrozenUntil, Time.time + seconds);
            return;
        }
        NetRun.Active?.SendPlayerFreeze(target.Slot, seconds);
    }

    /// <summary>Das Ziel hinter einem Transform (eigener Spieler oder Figur eines Mitspielers).</summary>
    public static bool TryGetTarget(Transform t, out Target target)
    {
        target = default;
        if (t == null) return false;
        PlayerController pc = PlayerController.Instance;
        if (pc != null && t == pc.transform)
        {
            target = LocalTarget();
            return true;
        }
        NetAvatar a = t.GetComponent<NetAvatar>();
        if (a == null) return false;
        target = new Target { Transform = t, Slot = a.Slot, IsLocal = false };
        return true;
    }

    /// <summary>
    /// Laeuft dieser Spieler gerade (fuer den Frostblick der Stracciatella)?
    /// Beim eigenen zaehlt die Eingabe, bei Mitspielern ihr letztes gemeldetes Tempo.
    /// </summary>
    public static bool IsMoving(Target target)
    {
        if (!target.Valid) return false;
        if (target.IsLocal)
        {
            PlayerController pc = PlayerController.Instance;
            return pc != null && !pc.IsFrozen && pc.playerMoveDirection.sqrMagnitude > 0.01f;
        }
        NetRun run = NetRun.Active;
        if (run == null || target.Slot < 0 || target.Slot >= run.Peers.Length) return false;
        return run.Peers[target.Slot].Velocity.sqrMagnitude > 0.25f;
    }

    /// <summary>Wird ein Lauf beendet, bleiben keine alten Figuren haengen.</summary>
    public static void Clear() => avatars.Clear();
}
