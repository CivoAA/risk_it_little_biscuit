using UnityEngine;

/// <summary>
/// Die Team-Abrechnung: wer wie viel Schaden gemacht, wie viele Gegner
/// erledigt, wen gerettet hat und wie oft er selbst am Boden lag.
///
/// Jeder zaehlt nur seine eigene Zeile und schickt sie den anderen, sobald
/// sie sich aendert (hoechstens zweimal pro Sekunde, zum Ende sofort). Zwei
/// Faelle brauchen den Mitspieler:
///
///  * Kills an fremden Gegnern: sterben tut das Original beim Besitzer. Der
///    weiss, wer zuletzt getroffen hat (<see cref="Enemy.LastHitBy"/>) und
///    schreibt es ihm gut (<see cref="NetMsg.KillCredit"/>).
///  * Rettungen: der Gerettete weiss, wer neben ihm stand, und sagt es ihm
///    (<see cref="NetMsg.ReviveCredit"/>).
///
/// Gezeigt wird die Abrechnung auf dem Abschlussbildschirm (LevelUpScreen.Coop).
/// </summary>
public class NetStats : MonoBehaviour
{
    public sealed class Line
    {
        public float Damage;
        public int Kills, Revives, Downs;

        /// <summary>Punkte fuer die MVP-Krone: Schaden zaehlt, Kills und vor allem Rettungen auch.</summary>
        public float Score => Damage + Kills * 40f + Revives * 1500f - Downs * 300f;
    }

    private const float SendInterval = 0.5f;
    private const float CreditInterval = 0.25f;

    public static NetStats Active { get; private set; }

    public readonly Line[] Lines = new Line[NetSession.MaxPlayers];

    private NetSession session;
    private NetRun run;
    private readonly int[] creditOut = new int[NetSession.MaxPlayers];
    private bool dirty, creditPending;
    private float sendTimer, creditTimer;

    private Line Mine => session != null && session.LocalSlot >= 0 ? Lines[session.LocalSlot] : null;

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        run = GetComponent<NetRun>();
        for (int i = 0; i < Lines.Length; i++) Lines[i] = new Line();

        session.On(NetMsg.KillCredit, OnKillCredit);
        session.On(NetMsg.ReviveCredit, OnReviveCredit);
        session.On(NetMsg.TeamStats, OnTeamStats);
        Enemy.DamageDealt += OnDamageDealt;
        Enemy.Killed += OnKilled;
        NetRun.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.Off(NetMsg.KillCredit);
            session.Off(NetMsg.ReviveCredit);
            session.Off(NetMsg.TeamStats);
        }
        Enemy.DamageDealt -= OnDamageDealt;
        Enemy.Killed -= OnKilled;
        NetRun.StateChanged -= OnStateChanged;
        if (Active == this) Active = null;
    }

    private bool Counting => run != null && run.CurrentPhase == NetRun.Phase.Running;

    // ==================================================================
    //  Eigene Zeile
    // ==================================================================

    private void OnDamageDealt(Enemy enemy, float damage)
    {
        Line me = Mine;
        if (me == null || !Counting || damage <= 0f || float.IsNaN(damage)) return;
        me.Damage += damage;
        dirty = true;
    }

    private void OnKilled(Enemy enemy)
    {
        if (enemy == null || enemy.IsRemote || !Counting) return;
        int by = enemy.LastHitBy;
        if (by < 0 || by == session.LocalSlot)
        {
            Line me = Mine;
            if (me == null) return;
            me.Kills++;
            dirty = true;
            return;
        }
        if (by >= creditOut.Length) return;
        creditOut[by]++;
        creditPending = true;
    }

    private void OnStateChanged(int slot, NetRun.PeerState before, NetRun.PeerState after)
    {
        if (slot != session.LocalSlot || after != NetRun.PeerState.Downed || !Counting) return;
        Line me = Mine;
        if (me == null) return;
        me.Downs++;
        dirty = true;
    }

    /// <summary>Der eigene Spieler wurde eben von <paramref name="helper"/> aufgehoben.</summary>
    public void CreditRevive(int helper)
    {
        if (helper < 0 || helper >= Lines.Length || helper == session.LocalSlot) return;
        session.Begin(NetMsg.ReviveCredit);
        session.SendTo(helper);
    }

    // ==================================================================
    //  Senden
    // ==================================================================

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        creditTimer -= dt;
        if (creditPending && creditTimer <= 0f)
        {
            creditTimer = CreditInterval;
            creditPending = false;
            for (int p = 0; p < creditOut.Length; p++)
            {
                if (creditOut[p] <= 0) continue;
                session.Begin(NetMsg.KillCredit).VarInt(creditOut[p]);
                session.SendTo(p);
                creditOut[p] = 0;
            }
        }

        sendTimer -= dt;
        if (dirty && sendTimer <= 0f) SendMine();
    }

    /// <summary>Eigene Zeile sofort an alle (zum Laufende, damit die Abrechnung stimmt).</summary>
    public void Flush()
    {
        creditTimer = 0f;
        if (creditPending) LateUpdate();
        SendMine();
    }

    private void SendMine()
    {
        Line me = Mine;
        if (me == null) return;
        dirty = false;
        sendTimer = SendInterval;
        NetWriter w = session.Begin(NetMsg.TeamStats);
        w.Float(me.Damage);
        w.VarInt(me.Kills);
        w.VarInt(me.Revives);
        w.VarInt(me.Downs);
        session.SendAll();
    }

    // ==================================================================
    //  Empfangen
    // ==================================================================

    private void OnKillCredit(int from, NetReader r)
    {
        int count = r.VarInt();
        Line me = Mine;
        if (me == null || count <= 0 || count > 10000) return;
        me.Kills += count;
        dirty = true;
    }

    private void OnReviveCredit(int from, NetReader r)
    {
        Line me = Mine;
        if (me == null) return;
        me.Revives++;
        dirty = true;
        PlayerController pc = PlayerController.Instance;
        if (pc != null)
            DamageNumberController.Instance?.CreateText(NetText.Get("net.run.float_rescuer", "RETTER!"), pc.transform.position);
    }

    private void OnTeamStats(int from, NetReader r)
    {
        if (from < 0 || from >= Lines.Length || from == session.LocalSlot) return;
        Line l = Lines[from];
        l.Damage = r.Float();
        l.Kills = r.VarInt();
        l.Revives = r.VarInt();
        l.Downs = r.VarInt();
    }

    /// <summary>Platz mit den meisten Punkten (<see cref="Line.Score"/>) unter denen, die mitgespielt haben; -1 = keiner.</summary>
    public int Mvp()
    {
        if (run == null) return -1;
        int best = -1;
        float bestScore = 0f;
        for (int i = 0; i < Lines.Length; i++)
        {
            if (!run.Peers[i].Playing) continue;
            float s = Lines[i].Score;
            if (best < 0 || s > bestScore) { best = i; bestScore = s; }
        }
        return best;
    }
}
