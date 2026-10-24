using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Der laufende Koop-Lauf. Entsteht mit GameCore, wenn die Sitzung gerade
/// einen Lauf hat, und verschwindet mit ihm.
///
/// WER ENTSCHEIDET WAS
///
/// Jeder Spieler ist Herr ueber seine eigene Ecke: seine Figur, seine Waffen,
/// seine Gegner (sein eigener <see cref="SpawnDirector"/>), seine Bonbons.
/// Die anderen bekommen davon Bilder geschickt (<see cref="NetVisuals"/>)
/// und schicken Treffer zurueck. So reagiert alles in der eigenen Naehe ohne
/// Verzoegerung, und kein Rechner muss die ganze Welt rechnen.
///
/// Gemeinsam (und darum hier) ist nur, was alle betrifft:
///
///  * Start: alle laden, der Host wartet, bis alle stehen, dann geht es gleichzeitig los.
///  * Pause: hat irgendwer ein Auswahlfenster offen (Level-Up, Mixer, Evo,
///    Tutorial), steht das Spiel fuer alle. Das Pausenmenue haelt nichts an.
///  * Uhr: die des Hosts.
///  * Erfahrung: ein Topf fuer alle (<see cref="NetBalance.XpShare"/>), der Host
///    zaehlt. Weil alle denselben Stand und dieselbe Levelkurve haben, leveln
///    alle im selben Moment - und waehlen gleichzeitig.
///  * Niedergehen und Retten: wer stirbt, wird zum Geist. Ein Mitspieler, der
///    <see cref="NetBalance.ReviveSeconds"/> danebensteht, holt ihn zurueck. Nach
///    <see cref="NetBalance.BleedOutSeconds"/> ohne Rettung ist er raus und
///    schaut zu. Sind alle unten, ist der Lauf verloren.
///  * Bosse: der Host wuerfelt aus, bei wem ein Boss erscheint.
///  * Ende: Boss besiegt (Story) = gewonnen fuer alle, alle unten = verloren.
/// </summary>
[DefaultExecutionOrder(-800)]
public class NetRun : MonoBehaviour
{
    public enum PeerState : byte { Alive = 0, Downed = 1, Out = 2, Left = 3 }

    public enum Phase { Loading, Running, Ended }

    public sealed class Peer
    {
        public int Slot;
        /// <summary>Ist in diesem Lauf dabei (hatte beim Start einen Platz).</summary>
        public bool Playing;
        public bool Loaded;
        public bool Blocking;
        public float BlockingSince;
        public PeerState State;
        public int Character = -1;
        public string Name = "";
        public Vector2 Position, Velocity;
        public Vector2 ViewHalf = new Vector2(16f, 9f);
        /// <summary>
        /// Mitte seines Bildes - nicht immer seine Figur: wer raus ist, schaut
        /// einem anderen zu. Danach richten sich, was man ihm schickt und wo
        /// keine Gegner auftauchen duerfen.
        /// </summary>
        public Vector2 ViewCenter;
        public float Health = 1f, MaxHealth = 1f, Shield;
        public float Revive;
        public float Bleed;
        public float LastState = -999f;
    }

    /// <summary>So lange zaehlt ein offenes Auswahlfenster als Pause - danach geht es ohne ihn weiter.</summary>
    private const float MaxBlocking = 90f;

    /// <summary>So lange wartet der Host beim Laden auf Nachzuegler.</summary>
    private const float LoadTimeout = 40f;

    private const float StateInterval = 0.05f;

    public static NetRun Active { get; private set; }

    /// <summary>Kurze Meldung fuers HUD ("NICK IST NIEDERGEGANGEN!").</summary>
    public static event Action<string, Color> Toast;

    /// <summary>Ein Spieler hat seinen Zustand gewechselt: Platz, vorher, nachher (<see cref="NetRevive"/>).</summary>
    public static event Action<int, PeerState, PeerState> StateChanged;

    /// <summary>Meldung fuers HUD von aussen (Kreuz-Altar).</summary>
    public static void ShowToast(string text, Color color) => Toast?.Invoke(text, color);

    public Phase CurrentPhase { get; private set; } = Phase.Loading;
    public NetRunInfo Info { get; private set; }

    /// <summary>Spieler beim Start - danach aendert sich der Wert nicht, damit Balance und XP stabil bleiben.</summary>
    public int StartPlayers { get; private set; }

    public readonly Peer[] Peers = new Peer[NetSession.MaxPlayers];

    public Peer LocalPeer => session != null && session.LocalSlot >= 0 ? Peers[session.LocalSlot] : null;

    public bool LocalAlive => LocalPeer == null || LocalPeer.State == PeerState.Alive;
    public bool LocalDowned => LocalPeer != null && LocalPeer.State == PeerState.Downed;
    public bool LocalOut => LocalPeer != null && (LocalPeer.State == PeerState.Out || LocalPeer.State == PeerState.Left);

    /// <summary>Der Lauf ist zu Ende und war ein Sieg (fuer die Abrechnung).</summary>
    public bool Won { get; private set; }

    /// <summary>Gerade steht das Spiel, weil jemand waehlt.</summary>
    public bool Paused { get; private set; }

    /// <summary>Wen der eigene Bildschirm gerade begleitet, wenn man raus ist (-1 = niemand).</summary>
    public int Spectating { get; private set; } = -1;

    private NetSession session;
    private float loadStarted;
    private bool sentLoaded;
    private float stateTimer, clockTimer;
    private double teamXp, appliedXp;
    private bool xpDirty;
    private float xpSentAt;
    private float winAt = -1f;
    private bool bossDefeated;

    private readonly Dictionary<int, Vector2> driftOut = new Dictionary<int, Vector2>();
    private float driftTimer;
    private Vector2 driftIn;
    private float driftInUntil;

    private readonly List<GameObject> disabledForGhost = new List<GameObject>();
    private static readonly string[] GhostOff = { "Weapons", "EvoWeapons", "Buffs", "MaxLevelStuff", "PowerUps" };

    private static readonly Color GhostTint = new Color(0.7f, 0.85f, 1f, 0.5f);

    // ==================================================================
    //  Aufbau
    // ==================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MapSceneSystem.CoreScene || !NetSession.InRun || Active != null) return;

        var go = new GameObject("NetRun");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<NetRun>();
    }

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        Info = session.Run;
        loadStarted = Time.unscaledTime;

        for (int i = 0; i < Peers.Length; i++)
        {
            Peers[i] = new Peer { Slot = i };
            int character = Info.Characters[i];
            Peers[i].Playing = character >= 0 && session.Slots[i].Used;
            Peers[i].Character = character;
            Peers[i].Name = session.Slots[i].Name;
            Peers[i].State = Peers[i].Playing ? PeerState.Alive : PeerState.Left;
            if (Peers[i].Playing) StartPlayers++;
        }

        session.On(NetMsg.RunLoaded, OnRunLoaded);
        session.On(NetMsg.RunGo, OnRunGo);
        session.On(NetMsg.Clock, OnClock);
        session.On(NetMsg.PauseState, OnPauseState);
        session.On(NetMsg.XpGain, OnXpGain);
        session.On(NetMsg.TeamXp, OnTeamXp);
        session.On(NetMsg.PlayerState, OnPlayerState);
        session.On(NetMsg.PlayerEvent, OnPlayerEvent);
        session.On(NetMsg.BossOrder, OnBossOrder);
        session.On(NetMsg.BossAnnounce, OnBossAnnounce);
        session.On(NetMsg.BossDefeated, OnBossDefeated);
        session.On(NetMsg.RunEnd, OnRunEnd);
        session.On(NetMsg.PlayerLeftRun, OnPlayerLeftRun);
        session.On(NetMsg.PlayerHit, OnPlayerHit);
        session.On(NetMsg.PlayerDrift, OnPlayerDrift);
        session.On(NetMsg.PlayerFreeze, OnPlayerFreeze);

        NetSession.SlotLeft += OnSlotLeft;
        NetSession.Ended += OnSessionEnded;
        Enemy.BossDefeated += OnLocalBossDefeated;

        // Die Teile fuer Bilder, Bonbons und Welt haengen am selben Objekt.
        gameObject.AddComponent<NetVisuals>();
        gameObject.AddComponent<NetPickups>();
        gameObject.AddComponent<NetWorld>();
        gameObject.AddComponent<NetHud>();
        gameObject.AddComponent<NetRevive>();
        gameObject.AddComponent<NetStats>();
        gameObject.AddComponent<NetEmotes>();

        Time.timeScale = 0f;
        Debug.Log($"[NetRun] Koop-Lauf mit {StartPlayers} Spielern, warte auf alle.");
    }

    private void OnDestroy()
    {
        NetSession.SlotLeft -= OnSlotLeft;
        NetSession.Ended -= OnSessionEnded;
        Enemy.BossDefeated -= OnLocalBossDefeated;

        if (session != null)
        {
            foreach (NetMsg m in new[]
            {
                NetMsg.RunLoaded, NetMsg.RunGo, NetMsg.Clock, NetMsg.PauseState, NetMsg.XpGain, NetMsg.TeamXp,
                NetMsg.PlayerState, NetMsg.PlayerEvent, NetMsg.BossOrder, NetMsg.BossAnnounce, NetMsg.BossDefeated,
                NetMsg.RunEnd, NetMsg.PlayerLeftRun, NetMsg.PlayerHit, NetMsg.PlayerDrift, NetMsg.PlayerFreeze,
            }) session.Off(m);

            // Zurueck im Hub ist man wieder in der Lobby - dieselbe Gruppe spielt weiter.
            if (NetSession.Active && session.Run == Info) session.ReturnToLobby();
        }

        NetPlayers.Clear();
        if (Active == this) Active = null;
    }

    public PeerState StateOf(int slot) =>
        slot >= 0 && slot < Peers.Length ? Peers[slot].State : PeerState.Left;

    /// <summary>Spieler, die noch im Lauf sind (nicht gegangen) - fuer XP und Balance zaehlt <see cref="StartPlayers"/>.</summary>
    public int PlayersInRun
    {
        get
        {
            int n = 0;
            foreach (Peer p in Peers) if (p.Playing && p.State != PeerState.Left) n++;
            return n;
        }
    }

    // ==================================================================
    //  Start
    // ==================================================================

    private void PlaceLocalPlayer()
    {
        PlayerController pc = PlayerController.Instance;
        if (pc == null) return;
        NetWorld.StartPoint = pc.transform.position;
        if (StartPlayers <= 1) return;

        // Nebeneinander statt aufeinander: Platz 0..3 von links nach rechts.
        float x = (session.LocalSlot - (NetSession.MaxPlayers - 1) * 0.5f) * 1.2f;
        Vector3 p = pc.transform.position + new Vector3(x, 0f, 0f);
        pc.transform.position = p;
        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = p;
    }

    private void UpdateLoading()
    {
        Time.timeScale = 0f;

        if (!sentLoaded && PlayerController.Instance != null && GameManager.Instance != null && !SceneFader.IsFading)
        {
            sentLoaded = true;
            PlaceLocalPlayer();
            if (NetSession.IsHost) MarkLoaded(0);
            else
            {
                session.Begin(NetMsg.RunLoaded);
                session.Send(NetSession.ToHost);
            }
        }

        if (!NetSession.IsHost || !sentLoaded) return;

        bool all = true;
        foreach (Peer p in Peers) if (p.Playing && p.State != PeerState.Left && !p.Loaded) all = false;
        bool timeout = Time.unscaledTime - loadStarted > LoadTimeout;
        if (!all && !timeout) return;

        if (timeout)
        {
            foreach (Peer p in Peers)
            {
                if (!p.Playing || p.Loaded || p.State == PeerState.Left) continue;
                Debug.LogWarning($"[NetRun] {p.Name} hat nicht rechtzeitig geladen.");
                SetState(p.Slot, PeerState.Left, true);
            }
        }

        session.Begin(NetMsg.RunGo);
        session.SendAll();
        StartRunning();
    }

    private void MarkLoaded(int slot)
    {
        if (slot >= 0 && slot < Peers.Length) Peers[slot].Loaded = true;
    }

    private void OnRunLoaded(int from, NetReader r)
    {
        if (NetSession.IsHost) MarkLoaded(from);
    }

    private void OnRunGo(int from, NetReader r)
    {
        if (CurrentPhase == Phase.Loading) StartRunning();
    }

    private void StartRunning()
    {
        if (CurrentPhase != Phase.Loading) return;
        CurrentPhase = Phase.Running;
        Time.timeScale = 1f;

        // Start-XP aus dem Skilltree gehen in den gemeinsamen Topf.
        PlayerController pc = PlayerController.Instance;
        if (pc != null && pc.experience > 0f)
        {
            float start = pc.experience;
            pc.experience = 0f;
            AddXp(start * NetBalance.XpShare(StartPlayers));
        }

        Debug.Log("[NetRun] Los!");
    }

    // ==================================================================
    //  Takt
    // ==================================================================

    private void Update()
    {
        switch (CurrentPhase)
        {
            case Phase.Loading: UpdateLoading(); return;
            case Phase.Ended: return;
        }

        float dt = Time.deltaTime;

        if (winAt >= 0f && Time.unscaledTime >= winAt)
        {
            winAt = -1f;
            EndLocally(true);
            return;
        }

        UpdateDowned(dt);
        UpdateSpectate();

        if (NetSession.IsHost)
        {
            CheckTeamDown();

            clockTimer -= Time.unscaledDeltaTime;
            if (clockTimer <= 0f && GameManager.Instance != null)
            {
                clockTimer = 1f;
                session.Begin(NetMsg.Clock).Float(GameManager.Instance.gameTime);
                session.SendAll(false);
            }
        }
    }

    private void LateUpdate()
    {
        if (CurrentPhase != Phase.Running) return;

        UpdatePause();
        UpdateGhostLook();

        if (NetSession.IsHost && xpDirty && Time.unscaledTime - xpSentAt >= 0.1f)
        {
            xpDirty = false;
            xpSentAt = Time.unscaledTime;
            session.Begin(NetMsg.TeamXp).Double(teamXp);
            session.SendAll();
            ApplyTeamXp(teamXp);
        }

        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer <= 0f)
        {
            stateTimer = StateInterval;
            SendState();
        }

        driftTimer -= Time.unscaledDeltaTime;
        if (driftTimer <= 0f && driftOut.Count > 0)
        {
            driftTimer = 0.1f;
            foreach (var kv in driftOut)
            {
                session.Begin(NetMsg.PlayerDrift).Vector2(kv.Value);
                session.SendTo(kv.Key, false);
            }
            driftOut.Clear();
        }
    }

    private void FixedUpdate()
    {
        if (CurrentPhase != Phase.Running) return;
        if (Time.time < driftInUntil)
        {
            PlayerController pc = PlayerController.Instance;
            if (pc != null && LocalAlive) pc.AddDrift(driftIn);
        }
    }

    // ==================================================================
    //  Gemeinsame Pause
    // ==================================================================

    private static bool LocalBlocking()
    {
        if (TutorialPanel.Blocking) return true;
        UIController ui = UIController.Instance;
        if (ui == null) return false;
        return (ui.LevelUpPanel != null && ui.LevelUpPanel.activeSelf)
            || (ui.PowerUpPanel != null && ui.PowerUpPanel.activeSelf)
            || (ui.EvoPanel != null && ui.EvoPanel.activeSelf);
    }

    private void UpdatePause()
    {
        Peer me = LocalPeer;
        if (me != null)
        {
            bool blocking = LocalBlocking();
            if (blocking != me.Blocking)
            {
                me.Blocking = blocking;
                me.BlockingSince = Time.unscaledTime;
                session.Begin(NetMsg.PauseState).Bool(blocking);
                session.SendAll();
            }
        }

        Paused = false;
        foreach (Peer p in Peers)
        {
            if (!p.Playing || p.State == PeerState.Left || !p.Blocking) continue;
            if (Time.unscaledTime - p.BlockingSince > MaxBlocking && p != me) continue;
            Paused = true;
        }

        if (SceneFader.IsFading) return;
        // Das Pausenmenue und Zeitlupen (Zweite Chance) halten im Koop nichts an -
        // nur die Auswahlfenster, und die fuer alle.
        Time.timeScale = Paused ? 0f : 1f;
    }

    private void OnPauseState(int from, NetReader r)
    {
        if (from < 0 || from >= Peers.Length) return;
        Peers[from].Blocking = r.Bool();
        Peers[from].BlockingSince = Time.unscaledTime;
    }

    /// <summary>Namen derer, die gerade waehlen (fuer das HUD).</summary>
    public void CollectChoosing(List<string> names)
    {
        names.Clear();
        foreach (Peer p in Peers)
            if (p.Playing && p.State != PeerState.Left && p.Blocking && p != LocalPeer) names.Add(p.Name);
    }

    private void OnClock(int from, NetReader r)
    {
        float hostTime = r.Float();
        GameManager gm = GameManager.Instance;
        if (gm == null || NetSession.IsHost) return;
        if (Mathf.Abs(gm.gameTime - hostTime) > 0.35f) gm.gameTime = hostTime;
    }

    // ==================================================================
    //  Erfahrung
    // ==================================================================

    /// <summary>
    /// Der eigene Spieler hat Erfahrung eingesammelt (schon mit seinem
    /// XP-Multiplikator). Sie geht in den Topf des Teams - angerechnet wird
    /// sie bei allen erst, wenn der Host den neuen Stand schickt.
    /// </summary>
    public void LocalXp(float gained)
    {
        if (gained <= 0f) return;
        AddXp(gained * NetBalance.XpShare(StartPlayers));
    }

    private void AddXp(float amount)
    {
        if (amount <= 0f) return;
        if (NetSession.IsHost)
        {
            teamXp += amount;
            xpDirty = true;
            return;
        }
        session.Begin(NetMsg.XpGain).Float(amount);
        session.Send(NetSession.ToHost);
    }

    private void OnXpGain(int from, NetReader r)
    {
        if (!NetSession.IsHost) return;
        float amount = r.Float();
        if (amount <= 0f || float.IsNaN(amount)) return;
        teamXp += amount;
        xpDirty = true;
    }

    private void OnTeamXp(int from, NetReader r)
    {
        if (NetSession.IsHost) return;
        ApplyTeamXp(r.Double());
    }

    private void ApplyTeamXp(double total)
    {
        double delta = total - appliedXp;
        if (delta <= 0.0) return;
        appliedXp = total;

        PlayerController pc = PlayerController.Instance;
        if (pc == null) return;

        float gained = (float)delta;
        pc.experience += gained;

        // Charakter-XP wie allein (PlayerController.GetExperience): eigener
        // Bonus, Endless-Anteil, Nachhol-Bonus, Deckel pro Lauf.
        float charXp = gained * (1f + pc.charXpBonusPercent / 100f) * RunDifficulty.CharXpShare
                     * RunDifficulty.CharXpCatchUp;
        charXp = (float)Skills.AddXp(charXp);
        if (GameManager.Instance != null) GameManager.Instance.charXpGained += charXp;

        UIController.Instance?.UpdateExperienceSlider();
    }

    // ==================================================================
    //  Zustand der Spieler
    // ==================================================================

    private void SendState()
    {
        PlayerController pc = PlayerController.Instance;
        Peer me = LocalPeer;
        if (pc == null || me == null) return;

        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        me.Position = pc.transform.position;
        me.Velocity = rb != null ? rb.linearVelocity : Vector2.zero;
        me.Health = pc.playerHealth;
        me.MaxHealth = pc.playerMaxHealth;
        me.Shield = pc.shield;
        me.ViewHalf = LocalViewHalf();
        me.ViewCenter = LocalViewCenter(me.Position);

        NetWriter w = session.Begin(NetMsg.PlayerState);
        w.Vector2(me.Position);
        w.Vector2(me.Velocity);
        w.Byte((byte)me.State);
        w.Float(me.Health);
        w.Float(me.MaxHealth);
        w.Float(me.Shield);
        w.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(me.Revive * 255f), 0, 255));
        w.Byte((byte)Mathf.Clamp(Mathf.CeilToInt(me.Bleed), 0, 255));
        w.Byte((byte)Mathf.Clamp(Mathf.CeilToInt(me.ViewHalf.x), 1, 255));
        w.Byte((byte)Mathf.Clamp(Mathf.CeilToInt(me.ViewHalf.y), 1, 255));
        w.Vector2(me.ViewCenter);
        session.SendAll(false);
    }

    private static Vector2 LocalViewCenter(Vector2 fallback)
    {
        if (!ViewBounds.TryGetWorldRect(out Rect view)) return fallback;
        return view.center;
    }

    private static Vector2 LocalViewHalf()
    {
        if (!ViewBounds.TryGetWorldRect(out Rect view)) return new Vector2(16f, 9f);
        return view.size * 0.5f;
    }

    private void OnPlayerState(int from, NetReader r)
    {
        if (from < 0 || from >= Peers.Length) return;
        Peer p = Peers[from];
        p.Position = r.Vector2();
        p.Velocity = r.Vector2();
        r.Byte();   // Zustand - gilt nur, was zuverlaessig als PlayerEvent kommt (siehe unten)
        p.Health = r.Float();
        p.MaxHealth = r.Float();
        p.Shield = r.Float();
        p.Revive = r.Byte() / 255f;
        p.Bleed = r.Byte();
        p.ViewHalf = new Vector2(r.Byte(), r.Byte());
        p.ViewCenter = r.Vector2();
        p.LastState = Time.unscaledTime;

        // Den Zustand NICHT von hier uebernehmen: unzuverlaessige Pakete koennen
        // ein zuverlaessiges Ereignis ueberholen - ein alter Stand "lebt" hat einen
        // gerade Niedergegangenen sonst kurz wieder aufstehen lassen, ohne Meldung
        // und ohne StateChanged (der Kreuz-Altar verpasste ihn).
    }

    private void SetState(int slot, PeerState state, bool broadcast)
    {
        Peer p = Peers[slot];
        if (p.State == state) return;
        PeerState before = p.State;
        p.State = state;

        if (broadcast)
        {
            NetWriter w = session.Begin(NetMsg.PlayerEvent);
            w.Byte((byte)slot);
            w.Byte((byte)state);
            session.SendAll();
        }

        AnnounceState(p, before);
    }

    private void OnPlayerEvent(int from, NetReader r)
    {
        int slot = r.Byte();
        var state = (PeerState)r.Byte();
        if (slot < 0 || slot >= Peers.Length) return;
        if (slot == session.LocalSlot && state != PeerState.Left) return;   // ueber mich bestimme ich
        PeerState before = Peers[slot].State;
        if (before == state) return;
        Peers[slot].State = state;
        AnnounceState(Peers[slot], before);
        if (state == PeerState.Left) NetVisuals.Active?.RemoveOwner(slot);
    }

    private void AnnounceState(Peer p, PeerState before)
    {
        bool me = p == LocalPeer;
        string name = me ? NetText.Get("net.run.you", "DU") : p.Name.ToUpperInvariant();
        switch (p.State)
        {
            case PeerState.Downed:
                // Der eigene Zustand steht gross in der Mitte (NetHud) - keine zweite Meldung.
                if (!me)
                    Toast?.Invoke(string.Format(NetText.Get("net.run.down", "{0} IST NIEDERGEGANGEN!"), name),
                                  GameHudSkin.JamLight);
                break;
            case PeerState.Alive when before == PeerState.Downed || before == PeerState.Out:
                Toast?.Invoke(string.Format(NetText.Get("net.run.revived", "{0} IST WIEDER DA!"), name), GameHudSkin.Mint);
                break;
            case PeerState.Out:
                Toast?.Invoke(string.Format(NetText.Get("net.run.out", "{0} IST RAUS!"), name), GameHudSkin.StoneLight);
                break;
            case PeerState.Left:
                if (!me) Toast?.Invoke(string.Format(NetText.Get("net.run.left", "{0} HAT DEN LAUF VERLASSEN"), name),
                                       GameHudSkin.StoneLight);
                break;
        }

        StateChanged?.Invoke(p.Slot, before, p.State);
    }

    // ==================================================================
    //  Niedergehen, Retten, Raus
    // ==================================================================

    /// <summary>
    /// Der eigene Spieler haette eben sterben muessen (kein Fuchs, kein Glueck,
    /// keine Zweite Chance mehr). Im Koop wird er stattdessen zum Geist.
    /// True = erledigt, PlayerController macht kein GameOver.
    /// </summary>
    public bool HandleLocalDeath()
    {
        PlayerController pc = PlayerController.Instance;
        if (pc == null || CurrentPhase != Phase.Running) return false;
        Peer me = LocalPeer;
        if (me == null) return false;

        // Schon gewonnen: der Sieg-Bildschirm kommt gleich, sterben gilt nicht mehr.
        if (bossDefeated && !Info.Endless)
        {
            pc.playerHealth = 1f;
            return true;
        }

        if (me.State != PeerState.Alive) return true;

        pc.playerHealth = 0f;
        pc.shield = 0f;
        me.Bleed = NetBalance.BleedOutSeconds;
        me.Revive = 0f;
        SetGhost(pc, true);
        SetState(me.Slot, PeerState.Downed, true);
        AudioController.Instance?.PalySound(AudioController.Instance.GameOver);
        UIController.Instance?.UpdateHealthSlider();
        return true;
    }

    private void SetGhost(PlayerController pc, bool ghost)
    {
        pc.NetGhost = ghost;

        if (ghost)
        {
            disabledForGhost.Clear();
            foreach (string name in GhostOff)
            {
                Transform t = pc.transform.Find(name);
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    disabledForGhost.Add(t.gameObject);
                }
            }
            if (pc.PickupRange != null && pc.PickupRange.activeSelf)
            {
                pc.PickupRange.SetActive(false);
                disabledForGhost.Add(pc.PickupRange);
            }
        }
        else
        {
            foreach (GameObject go in disabledForGhost)
                if (go != null) go.SetActive(true);
            disabledForGhost.Clear();

            SpriteRenderer body = pc.BodyRenderer();
            if (body != null) body.color = Color.white;
        }

        if (SpawnDirector.Active != null) SpawnDirector.Active.Suspended = ghost;
    }

    /// <summary>Durchscheinend, solange man Geist ist (jeden Frame, der Treffer-Flash setzt die Farbe zurueck).</summary>
    private void UpdateGhostLook()
    {
        if (!LocalDowned) return;
        PlayerController pc = PlayerController.Instance;
        SpriteRenderer body = pc != null ? pc.BodyRenderer() : null;
        if (body == null) return;
        float bob = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
        body.color = new Color(GhostTint.r, GhostTint.g, GhostTint.b, GhostTint.a * bob);
    }

    private void UpdateDowned(float dt)
    {
        Peer me = LocalPeer;
        if (me == null || me.State != PeerState.Downed || dt <= 0f) return;

        PlayerController pc = PlayerController.Instance;
        if (pc == null) return;

        int helper = NearestHelper(pc.transform.position);

        if (helper >= 0)
        {
            me.Revive += dt / NetBalance.ReviveSeconds;
            if (me.Revive >= 1f)
            {
                NetStats.Active?.CreditRevive(helper);
                ReviveLocal();
                return;
            }
        }
        else
        {
            me.Revive = Mathf.Max(0f, me.Revive - dt / NetBalance.ReviveSeconds * 0.5f);
            me.Bleed -= dt;
            if (me.Bleed <= 0f) GoOut();
        }
    }

    /// <summary>Platz des naechsten stehenden Mitspielers in Rettungsweite, sonst -1.</summary>
    private static int NearestHelper(Vector2 at)
    {
        int best = -1;
        float bestD = NetBalance.ReviveRadius * NetBalance.ReviveRadius;
        foreach (NetAvatar a in NetPlayers.Avatars)
        {
            if (a == null || !a.Targetable) continue;
            float d = ((Vector2)a.transform.position - at).sqrMagnitude;
            if (d <= bestD) { bestD = d; best = a.Slot; }
        }
        return best;
    }

    private void ReviveLocal()
    {
        PlayerController pc = PlayerController.Instance;
        Peer me = LocalPeer;
        if (pc == null || me == null) return;

        me.Revive = 0f;
        me.Bleed = 0f;
        SetGhost(pc, false);
        pc.playerHealth = Mathf.Max(1f, pc.playerMaxHealth * NetBalance.ReviveHealth);
        pc.GrantImmunity(NetBalance.ReviveImmunity);
        UIController.Instance?.UpdateHealthSlider();
        DamageNumberController.Instance?.CreateText(NetText.Get("net.run.float_revived", "GERETTET!"), pc.transform.position);
        AudioController.Instance?.PalySound(AudioController.Instance.LevelUpSound);
        SetState(me.Slot, PeerState.Alive, true);
    }

    private void GoOut()
    {
        PlayerController pc = PlayerController.Instance;
        Peer me = LocalPeer;
        if (pc == null || me == null) return;

        me.Bleed = 0f;
        me.Revive = 0f;
        SetState(me.Slot, PeerState.Out, true);
        HandOffBosses();
        ClearOwnEnemies();
        pc.gameObject.SetActive(false);
        SpectateNext();
    }

    /// <summary>
    /// Kreuz-Altar (<see cref="NetRevive"/>): der eigene Spieler war raus und
    /// steht am Altar wieder auf - mit <paramref name="healthFraction"/> seiner
    /// Max-HP, kurz unverwundbar, die Kamera wieder bei ihm.
    /// </summary>
    public void ReviveFromOut(Vector2 at, float healthFraction)
    {
        PlayerController pc = PlayerController.Instance;
        Peer me = LocalPeer;
        if (pc == null || me == null || me.State != PeerState.Out || CurrentPhase != Phase.Running) return;

        pc.transform.position = new Vector3(at.x, at.y, pc.transform.position.z);
        pc.gameObject.SetActive(true);
        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = at;
            rb.linearVelocity = Vector2.zero;
        }

        me.Revive = 0f;
        me.Bleed = 0f;
        SetGhost(pc, false);
        pc.playerHealth = Mathf.Max(1f, pc.playerMaxHealth * healthFraction);
        pc.GrantImmunity(NetBalance.ReviveImmunity);
        UIController.Instance?.UpdateHealthSlider();

        Spectating = -1;
        CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
        if (cam != null) cam.Follow = pc.transform;

        DamageNumberController.Instance?.CreateText(NetText.Get("net.run.float_revived", "GERETTET!"), pc.transform.position);
        AudioController.Instance?.PalySound(AudioController.Instance.LevelUpSound);
        SetState(me.Slot, PeerState.Alive, true);
    }

    /// <summary>Wer raus ist, nimmt seine normalen Gegner mit - um ihn herum spielt ja niemand mehr.</summary>
    private static void ClearOwnEnemies()
    {
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Enemy e = alive[i];
            if (e == null || e.IsRemote || e.IsBoss) continue;
            Destroy(e.gameObject);
        }
    }

    /// <summary>
    /// Host: liegen alle am Boden (oder sind raus/weg), ist der Lauf verloren -
    /// es ist niemand mehr da, der retten koennte.
    /// </summary>
    private void CheckTeamDown()
    {
        if (winAt >= 0f) return;
        bool anyPlaying = false;
        foreach (Peer p in Peers)
        {
            if (!p.Playing) continue;
            anyPlaying = true;
            if (p.State == PeerState.Alive) return;
        }
        if (!anyPlaying) return;

        session.Begin(NetMsg.RunEnd).Byte(0);
        session.SendAll();
        EndLocally(false);
    }

    // ---------- Zuschauen ----------

    private void UpdateSpectate()
    {
        if (!LocalOut) return;

        if (Spectating < 0 || StateOf(Spectating) == PeerState.Left || NetPlayers.AvatarOf(Spectating) == null)
            SpectateNext();

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) SpectateNext();
    }

    public void SpectateNext()
    {
        int start = Spectating;
        for (int n = 1; n <= Peers.Length; n++)
        {
            int slot = ((start < 0 ? session.LocalSlot : start) + n) % Peers.Length;
            if (slot == session.LocalSlot) continue;
            Peer p = Peers[slot];
            if (!p.Playing || p.State == PeerState.Left) continue;
            NetAvatar a = NetPlayers.AvatarOf(slot);
            if (a == null) continue;
            Spectating = slot;
            CinemachineCamera cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null) cam.Follow = a.transform;
            return;
        }
    }

    // ==================================================================
    //  Treffer an mir (von Bossen anderer)
    // ==================================================================

    public void SendPlayerHit(int slot, float damage)
    {
        if (slot < 0 || slot == session.LocalSlot) return;
        session.Begin(NetMsg.PlayerHit).Float(damage);
        session.SendTo(slot);
    }

    public void SendPlayerDrift(int slot, Vector2 velocity)
    {
        if (slot < 0 || slot == session.LocalSlot) return;
        driftOut[slot] = velocity;
    }

    public void SendPlayerFreeze(int slot, float seconds)
    {
        if (slot < 0 || slot == session.LocalSlot) return;
        session.Begin(NetMsg.PlayerFreeze).Float(seconds);
        session.SendTo(slot);
    }

    private void OnPlayerHit(int from, NetReader r)
    {
        float damage = r.Float();
        PlayerController pc = PlayerController.Instance;
        if (pc != null && LocalAlive && pc.gameObject.activeInHierarchy) pc.TakeDamage(damage);
    }

    private void OnPlayerDrift(int from, NetReader r)
    {
        driftIn = r.Vector2();
        driftInUntil = Time.time + 0.15f;
    }

    private void OnPlayerFreeze(int from, NetReader r)
    {
        float seconds = r.Float();
        PlayerController pc = PlayerController.Instance;
        if (pc != null && LocalAlive) pc.FrozenUntil = Mathf.Max(pc.FrozenUntil, Time.time + seconds);
    }

    // ==================================================================
    //  Bosse
    // ==================================================================

    /// <summary>
    /// Host: ein Boss-Beat ist dran. Wuerfelt aus, bei wem der Boss erscheint
    /// (nur wer noch steht). True = beim Host selbst - dann setzt ihn der
    /// eigene Director wie allein. Sonst bekommt der Glueckliche den Auftrag.
    /// </summary>
    public bool AssignBoss(EnemyId id, float pressureScale, float duration, string announce, float healthFraction = 1f)
    {
        var candidates = new List<int>();
        foreach (Peer p in Peers)
            if (p.Playing && p.State == PeerState.Alive) candidates.Add(p.Slot);
        if (candidates.Count == 0)
            foreach (Peer p in Peers)
                if (p.Playing && (p.State == PeerState.Alive || p.State == PeerState.Downed)) candidates.Add(p.Slot);
        if (candidates.Count == 0) return true;

        int target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (target == session.LocalSlot) return true;

        NetWriter w = session.Begin(NetMsg.BossOrder);
        w.VarInt((int)id);
        w.Float(pressureScale);
        w.Float(duration);
        w.String(announce ?? "");
        w.Float(healthFraction);
        session.SendTo(target);
        return false;
    }

    private void OnBossOrder(int from, NetReader r)
    {
        var id = (EnemyId)r.VarInt();
        float pressure = r.Float();
        float duration = r.Float();
        string announce = r.String();
        float health = r.Float();

        // Ein Client gibt seinen Boss ab (raus oder aufgegeben): der Host verteilt neu.
        if (pressure < 0f)
        {
            if (NetSession.IsHost) AssignBossFromHandoff(id, health);
            return;
        }
        SpawnDirector.Active?.SpawnOrderedBoss(id, pressure, duration, announce, health);
    }

    /// <summary>Der eigene Director hat einen Boss gesetzt - allen sagen, bei wem.</summary>
    public void AnnounceBoss(EnemyId id)
    {
        NetWriter w = session.Begin(NetMsg.BossAnnounce);
        w.VarInt((int)id);
        session.SendAll();
    }

    private void OnBossAnnounce(int from, NetReader r)
    {
        var id = (EnemyId)r.VarInt();
        if (from < 0 || from >= Peers.Length) return;
        string text = string.Format(NetText.Get("net.run.boss_at", "{0} BEI {1}!"),
                                    Bestiary.NameOf(id).ToUpperInvariant(), Peers[from].Name.ToUpperInvariant());
        SpawnDirector.Active?.Say(text);
    }

    /// <summary>
    /// Raus oder weg mit einem Boss im Rucksack: der Host setzt ihn bei
    /// jemand anderem neu hin, mit dem Leben, das er noch hatte.
    /// </summary>
    private void HandOffBosses()
    {
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Enemy e = alive[i];
            if (e == null || e.IsRemote || !e.IsBoss) continue;
            EnemyId id = e.Identity;
            float fraction = e.HealthFraction;
            Destroy(e.gameObject);
            if (id == EnemyId.None) continue;

            if (NetSession.IsHost) AssignBossFromHandoff(id, fraction);
            else
            {
                NetWriter w = session.Begin(NetMsg.BossOrder);
                w.VarInt((int)id);
                w.Float(-1f);   // -1 = Uebergabe, der Host verteilt neu
                w.Float(0f);
                w.String("");
                w.Float(fraction);
                session.Send(NetSession.ToHost);
            }
        }
    }

    private void AssignBossFromHandoff(EnemyId id, float fraction)
    {
        if (AssignBoss(id, 1f, 0f, "", fraction))
            SpawnDirector.Active?.SpawnOrderedBoss(id, 1f, 0f, "", fraction);
    }

    /// <summary>Host: ein Spieler mit Boss ist weg - der Boss lebt beim Host als Bild weiter, das reicht zum Neusetzen.</summary>
    private void HandOffBossesOf(int slot)
    {
        if (!NetSession.IsHost || NetVisuals.Active == null) return;
        foreach (var (id, fraction) in NetVisuals.Active.BossesOf(slot))
            AssignBossFromHandoff(id, fraction);
    }

    private void OnLocalBossDefeated(Enemy boss)
    {
        if (boss == null || boss.IsRemote || boss.Role != EnemyRole.Boss) return;
        session.Begin(NetMsg.BossDefeated).VarInt((int)boss.Identity);
        session.SendAll();
        TeamBossDefeated(true);
    }

    private void OnBossDefeated(int from, NetReader r)
    {
        TeamBossDefeated(false);
    }

    private void TeamBossDefeated(bool local)
    {
        if (bossDefeated && !Info.Endless) return;
        bossDefeated = true;

        GameManager gm = GameManager.Instance;
        if (gm != null) gm.bossSpawned = true;

        if (!local) SpawnDirector.Active?.NetBossDefeated();

        if (!Info.Endless)
        {
            Toast?.Invoke(NetText.Get("net.run.won", "GESCHAFFT!"), GameHudSkin.Gold);
            winAt = Time.unscaledTime + NetBalance.WinDelay;
            PlayerController pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.Untouchable = true;
                pc.attractAllXP = true;
            }
        }
    }

    // ==================================================================
    //  Ende
    // ==================================================================

    private void OnRunEnd(int from, NetReader r)
    {
        if (CurrentPhase == Phase.Ended) return;
        EndLocally(false);
    }

    /// <summary>Der eigene Lauf endet - Abrechnung und Abschlussbildschirm wie allein.</summary>
    private void EndLocally(bool won)
    {
        if (CurrentPhase == Phase.Ended) return;
        NetStats.Active?.Flush();
        CurrentPhase = Phase.Ended;
        Won = won;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        if (won) gm.bossSpawned = true;
        gm.NetFinishRun();
    }

    /// <summary>
    /// Pausenmenue "Aufgeben": als Client geht man allein, die anderen spielen
    /// weiter. Als Host endet der Lauf fuer alle - ohne Host gibt es keine Sitzung.
    /// </summary>
    public void OnLocalGiveUp()
    {
        if (CurrentPhase == Phase.Ended) return;
        CurrentPhase = Phase.Ended;

        if (NetSession.IsHost)
        {
            session.Begin(NetMsg.RunEnd).Byte(1);
            session.SendAll();
        }
        else
        {
            HandOffBosses();
            session.Begin(NetMsg.PlayerLeftRun);
            session.SendAll();
        }
    }

    private void OnPlayerLeftRun(int from, NetReader r)
    {
        if (from < 0 || from >= Peers.Length) return;
        PeerState before = Peers[from].State;
        Peers[from].State = PeerState.Left;
        AnnounceState(Peers[from], before);
        NetVisuals.Active?.RemoveOwner(from);
    }

    private void OnSlotLeft(int slot)
    {
        if (slot < 0 || slot >= Peers.Length || Peers[slot].State == PeerState.Left) return;
        HandOffBossesOf(slot);
        PeerState before = Peers[slot].State;
        Peers[slot].State = PeerState.Left;
        AnnounceState(Peers[slot], before);
        NetVisuals.Active?.RemoveOwner(slot);
        if (NetSession.IsHost)
        {
            NetWriter w = session.Begin(NetMsg.PlayerEvent);
            w.Byte((byte)slot);
            w.Byte((byte)PeerState.Left);
            session.SendAll();
        }
    }

    private void OnSessionEnded(string reason)
    {
        if (CurrentPhase == Phase.Ended) return;
        Toast?.Invoke(NetText.Reason(reason), GameHudSkin.JamLight);
        EndLocally(false);
    }
}
