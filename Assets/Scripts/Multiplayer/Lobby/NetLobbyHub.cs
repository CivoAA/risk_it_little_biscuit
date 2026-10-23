using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Klebt die Koop-Lobby an den Rest des Spiels:
///
///  * MULTIPLAYER im Hauptmenue laedt den Hub und oeffnet dort die Lobby
///    (<see cref="OpenOnHub"/>).
///  * Kommt ein Koop-Lauf zurueck in den Hub, geht die Lobby von selbst
///    wieder auf - die Sitzung lebt weiter, dieselbe Gruppe spielt weiter.
///  * Startet der Host, schaltet jeder Spieler hier seinen Lauf an
///    (<see cref="RunLauncher"/>), mit seinem Charakter aus der Lobby.
///  * Steam-Einladungen: angenommen im Hauptmenue oder im Hub -> beitreten.
///
/// Lebt als unsichtbares Objekt ueber alle Szenen und schaut jeden Frame
/// nach, ob der Hub gerade "dran" ist (geladen, aktiv, kein Lauf, keine Blende).
/// </summary>
public class NetLobbyHub : MonoBehaviour
{
    /// <summary>Beim naechsten Hub-Besuch die Lobby oeffnen (gesetzt vom Hauptmenue).</summary>
    public static bool OpenOnHub;

    private static NetLobbyHub instance;
    private NetRunInfo pendingRun;
    private bool steamListening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("NetLobbyHub") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NetLobbyHub>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OpenOnHub = false;
        instance = null;
    }

    private void OnEnable()
    {
        NetSession.RunStarting += OnRunStarting;
        NetSession.Ended += OnEnded;
    }

    private void OnDisable()
    {
        NetSession.RunStarting -= OnRunStarting;
        NetSession.Ended -= OnEnded;
    }

    /// <summary>Der Hub ist da, wach und nicht von einem Lauf ueberlagert.</summary>
    public static bool HubIsCurrent
    {
        get
        {
            if (SceneFader.IsFading) return false;
            Scene hub = SceneManager.GetSceneByName(GameSession.HubScene);
            if (!hub.IsValid() || !hub.isLoaded) return false;
            Scene core = SceneManager.GetSceneByName(MapSceneSystem.CoreScene);
            if (core.IsValid() && core.isLoaded) return false;
            // Nicht HubUI.Instance: das legt eine neue an, wenn keine aktiv ist.
            HubUI ui = FindAnyObjectByType<HubUI>();
            return ui != null && ui.isActiveAndEnabled;
        }
    }

    private static bool InMainMenu
    {
        get
        {
            Scene menu = SceneManager.GetSceneByName(GameSession.MainMenuScene);
            return menu.IsValid() && menu.isLoaded && !SceneFader.IsFading;
        }
    }

    private void Update()
    {
        if (!steamListening && SteamManager.Initialized)
        {
            steamListening = true;
            NetSteamLobby.Listen();
        }

        // Ein gestarteter Lauf wartet, bis die Blende frei ist.
        if (pendingRun != null)
        {
            TryLaunch();
            return;
        }

        HandleInvite();

        if (!HubIsCurrent) return;

        bool wanted = OpenOnHub || NetSession.Active;
        if (wanted && !NetLobbyPanel.IsOpen && NetSession.Instance?.Run == null)
        {
            OpenOnHub = false;
            NetLobbyPanel.Open();
            NetLobbyPanel.EnsurePlayableWorld();
        }
    }

    private void HandleInvite()
    {
        if (NetSteamLobby.PendingInvite == 0) return;

        // Mitten im Lauf (allein oder Koop) wartet die Einladung.
        Scene core = SceneManager.GetSceneByName(MapSceneSystem.CoreScene);
        if (core.IsValid() && core.isLoaded) return;

        if (InMainMenu)
        {
            OpenOnHub = true;
            SceneFader.Load(GameSession.HubScene);
            return;
        }

        if (!HubIsCurrent) return;

        if (NetSession.Active)
        {
            // Schon in einer Lobby: die Einladung verfaellt, statt die Gruppe zu sprengen.
            NetSteamLobby.TakeInvite();
            return;
        }

        NetSession.JoinSteamLobby(NetSteamLobby.TakeInvite());
        if (!NetLobbyPanel.IsOpen) NetLobbyPanel.Open();
    }

    private void OnRunStarting(NetRunInfo info)
    {
        pendingRun = info;
        TryLaunch();
    }

    private void TryLaunch()
    {
        NetRunInfo info = pendingRun;
        if (info == null || SceneFader.IsFading) return;
        pendingRun = null;

        NetSession s = NetSession.Instance;
        if (s == null || s.Run != info) return;

        int character = info.Characters[Mathf.Clamp(s.LocalSlot, 0, NetSession.MaxPlayers - 1)];
        if (character >= 0) Shop.SkinIndex = character;
        GameSession.Chaos = 1f;

        Scene hub = SceneManager.GetSceneByName(GameSession.HubScene);
        string hubName = hub.IsValid() && hub.isLoaded ? hub.name : GameSession.HubScene;

        bool started = RunLauncher.Launch(info.MapId, info.Scene, info.Endless, hubName, NetLobbyPanel.Close);
        if (!started) pendingRun = info;
    }

    private void OnEnded(string reasonKey)
    {
        pendingRun = null;
    }

    public static void BackToMainMenu()
    {
        OpenOnHub = false;
        GameSession.LoadMainMenu();
    }

    public static void PlayDeny()
    {
        AudioController audio = AudioController.Instance;
        if (audio != null && audio.PlayerHit != null) audio.PlayerHit.Play();
    }
}
