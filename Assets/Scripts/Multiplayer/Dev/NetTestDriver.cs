using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatischer Koop-Test fuer zwei (oder mehr) Fenster auf einem Rechner.
/// Tut nur etwas mit dem Schalter "-nettest" und nur im Editor oder in
/// Development Builds - im Release ist es tot.
///
///   Spiel.exe -nettcp -nettest host -nettestchar 0 -nettestplayers 2 -nettestout C:\pfad
///   Spiel.exe -nettcp -nettest join -nettestaddr 127.0.0.1 -nettestchar 1 -nettestout C:\pfad
///
/// Ablauf: Hauptmenue -> Koop-Lobby im Hub -> Host macht auf bzw. Gast tritt
/// bei -> Charakter waehlen, bereit -> Lauf. Im Lauf laeuft ein Bot (der Gast
/// sucht die Naehe des Hosts, damit sich die Welten ueberlappen), waehlt
/// Level-Ups und Mixer, macht alle paar Sekunden ein Bildschirmfoto und
/// schreibt ein kurzes Protokoll. Nach "-nettestduration" Sekunden ist Schluss.
/// </summary>
public class NetTestDriver : MonoBehaviour
{
    private enum Step { Menu, Lobby, Connecting, Picking, Running, Done }

    private string role, addr, outDir;
    private float spawnBossAt = -1f, dieAt = -1f;
    private int character, players;
    private float duration, startedAt, nextShot, shotEvery = 6f, stepAt, botTurnAt;
    private Step step = Step.Menu;
    private Vector2 botDir = Vector2.right;
    private int shots;
    private StreamWriter log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!(Application.isEditor || Debug.isDebugBuild)) return;
        string role = NetSession.CommandLine("-nettest");
        if (string.IsNullOrEmpty(role)) return;

        var go = new GameObject("NetTestDriver") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        NetTestDriver d = go.AddComponent<NetTestDriver>();
        d.role = role.ToLowerInvariant();
    }

    private void Start()
    {
        addr = NetSession.CommandLine("-nettestaddr") ?? "127.0.0.1";
        outDir = NetSession.CommandLine("-nettestout") ?? Path.Combine(Application.persistentDataPath, "nettest");
        int.TryParse(NetSession.CommandLine("-nettestchar") ?? "0", out character);
        int.TryParse(NetSession.CommandLine("-nettestplayers") ?? "2", out players);
        god = NetSession.HasFlag("-nettestgod");
        noRescue = NetSession.HasFlag("-nettestnorescue");
        goAltar = NetSession.HasFlag("-nettestaltar");
        float.TryParse(NetSession.CommandLine("-nettestdieat") ?? "-1",
                       System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dieAt);
        float.TryParse(NetSession.CommandLine("-nettestbossat") ?? "-1",
                       System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spawnBossAt);
        float.TryParse(NetSession.CommandLine("-nettestduration") ?? "75",
                       System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out duration);
        Directory.CreateDirectory(outDir);
        log = new StreamWriter(Path.Combine(outDir, role + "_log.txt"), false) { AutoFlush = true };
        Log($"Test {role}, Charakter {character}, {players} Spieler, {duration}s");
        Application.logMessageReceived += OnUnityLog;
        stepAt = Time.unscaledTime + 3f;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnUnityLog;
        log?.Dispose();
    }

    private void OnUnityLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
            Log("UNITY " + type + ": " + condition + "\n" + stack);
        else if (condition.StartsWith("[Net"))
            Log(condition);
    }

    private void Log(string text)
    {
        log?.WriteLine($"{Time.unscaledTime,8:0.00}  {text}");
    }

    private void Update()
    {
        if (quitAt > 0f && Time.unscaledTime >= quitAt) { Quit(); return; }
        if (Time.unscaledTime < stepAt) return;

        switch (step)
        {
            case Step.Menu:
                if (SceneManager.GetSceneByName(GameSession.MainMenuScene).isLoaded && !SceneFader.IsFading)
                {
                    Log("Hauptmenue -> Koop");
                    NetLobbyHub.OpenOnHub = true;
                    SceneFader.Load(GameSession.HubScene);
                    step = Step.Lobby;
                    stepAt = Time.unscaledTime + 2f;
                }
                break;

            case Step.Lobby:
                if (!NetLobbyPanel.IsOpen) break;
                Shot("lobby_empty");
                if (role == "host") NetSession.Host();
                else NetSession.Join(addr);
                Log("Sitzung: " + role);
                step = Step.Connecting;
                stepAt = Time.unscaledTime + 1f;
                break;

            case Step.Connecting:
                if (NetSession.Instance == null || !NetSession.Instance.Joined) break;
                NetSession.Instance.PickCharacter(character);
                Shop.SkinIndex = character;
                step = Step.Picking;
                stepAt = Time.unscaledTime + 1f;
                break;

            case Step.Picking:
                NetSession s = NetSession.Instance;
                if (s == null) { Log("Sitzung weg"); step = Step.Done; break; }
                if (s.Run != null)
                {
                    Log("Lauf startet");
                known.Clear();
                    step = Step.Running;
                    startedAt = Time.unscaledTime;
                    nextShot = startedAt + 8f;
                    break;
                }
                if (role == "host" && s.PlayerCount < players) break;
                if (s.Countdown >= 0f && s.Countdown < 2.2f && !shotCountdown)
                {
                    shotCountdown = true;
                    Shot("lobby_countdown");
                }
                NetSlot me = s.LocalSlotData;
                if (me != null && me.Character == character && !me.Ready)
                {
                    Shot("lobby_" + s.PlayerCount);
                    s.SetReady(true);
                    Log("Bereit");
                }
                stepAt = Time.unscaledTime + 0.5f;
                break;

            case Step.Running:
                RunBot();
                break;
        }
    }

    private float quitAt = -1f;
    private bool god, noRescue, goAltar, shotCountdown;

    // Spawn-Pruefung: jeder neue eigene Gegner darf in keinem Bild auftauchen.
    private readonly System.Collections.Generic.HashSet<Enemy> known = new System.Collections.Generic.HashSet<Enemy>();
    private int spawnsSeen, spawnsInSight;

    private void RunBot()
    {
        float now = Time.unscaledTime;
        PlayerController self = PlayerController.Instance;
        if (god && self != null) self.GodMode = true;
        CheckSpawns();
        if (dieAt > 0f && now - startedAt > dieAt && self != null)
        {
            dieAt = -1f;
            Log("Testtod");
            self.GrantImmunity(0f);
            self.shield = 0f;
            self.playerHealth = 1f;
            self.TakeDamage(9999f);
        }
        if (spawnBossAt > 0f && now - startedAt > spawnBossAt && SpawnDirector.Active != null)
        {
            spawnBossAt = -1f;
            if (System.Enum.TryParse(NetSession.CommandLine("-nettestboss"), out EnemyId boss))
            {
                Log("Testboss " + boss);
                SpawnDirector.Active.DebugSpawnBoss(boss);
            }
        }
        if (now >= nextShot)
        {
            nextShot = now + shotEvery;
            Shot("run_" + Mathf.RoundToInt(now - startedAt));
            NetRun run = NetRun.Active;
            NetSession s = NetSession.Instance;
            if (run != null && s != null)
                Log($"Zustand {run.LocalPeer?.State}, Rettung {run.LocalPeer?.Revive:0.00}, Phase {run.CurrentPhase}, Pause {run.Paused}, Mitspieler-Figuren {NetPlayers.Avatars.Count}, " +
                    $"Gegner {Enemy.Alive.Count}, out {s.BytesOutPerSecond} B/s, in {s.BytesInPerSecond} B/s, " +
                    $"Level {(PlayerController.Instance != null ? PlayerController.Instance.currentLevel : 0)}, " +
                    $"Treffer fremd raus {NetVisuals.Active?.HitsSent}, rein {NetVisuals.Active?.HitsReceived}, " +
                    $"Bilanz {StatsLine()}, Spawns {spawnsSeen} davon im Bild {spawnsInSight}, " +
                    $"Zuschauer {run.Spectating}, fremde Gegner hier {RemoteEnemies()}");
            NetEmotes.Active?.Send((NetEmotes.Kind)(shots % NetEmotes.Count));
        }

        // Auswahlfenster: einfach die erste Karte nehmen.
        UIController ui = UIController.Instance;
        if (ui != null)
        {
            if (TutorialPanel.Blocking) TutorialPanel.Close();
            if (ui.LevelUpPanel != null && ui.LevelUpPanel.activeSelf && ui.levelUpButtons.Length > 0)
                ui.levelUpButtons[0].SelectUpgrade();
            else if (ui.PowerUpPanel != null && ui.PowerUpPanel.activeSelf && ui.PowerUpButtons.Length > 0)
                ui.PowerUpButtons[0].SelectUpgrade();
        }

        if (now - startedAt > duration)
        {
            Log("Ende des Tests");
            Shot("end");
            step = Step.Done;
            quitAt = now + 1.5f;
        }
    }

    private void LateUpdate()
    {
        if (step != Step.Running) return;
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeInHierarchy) return;

        float now = Time.unscaledTime;
        Vector2 me = pc.transform.position;

        // Der Gast bleibt beim Host, damit sich beide Welten ueberlappen; der
        // Host zieht Kreise.
        NetAvatar mate = NetPlayers.Avatars.Count > 0 ? NetPlayers.Avatars[0] : null;
        bool mateDown = !noRescue && mate != null && mate.State == NetRun.PeerState.Downed;
        if (goAltar && NetRevive.Active != null && NetRevive.Active.TryNearestAltar(me, out Vector2 altar))
        {
            // Zum Keksglas und mitten in den Kreis stellen.
            Vector2 to = altar - me;
            pc.playerMoveDirection = to.magnitude > 1.5f ? to.normalized : Vector2.zero;
            return;
        }
        if (mate != null && (role != "host" || mateDown))
        {
            // Liegt der andere am Boden: hin und draufstellen (Rettung testen).
            Vector2 to = (Vector2)mate.transform.position - me;
            float keep = mateDown ? 0.4f : 3f;
            botDir = to.magnitude > keep ? to.normalized : mateDown ? Vector2.zero : Rotate(botDir, 90f * Time.unscaledDeltaTime);
            if (mateDown && to.magnitude <= keep) { pc.playerMoveDirection = Vector2.zero; return; }
        }
        else if (now >= botTurnAt)
        {
            botTurnAt = now + 1.2f;
            botDir = Rotate(botDir, 50f);
        }
        pc.playerMoveDirection = botDir.normalized;
    }

    private void CheckSpawns()
    {
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.IsRemote || !known.Add(e)) continue;
            spawnsSeen++;
            Vector2 pos = e.transform.position;
            bool own = ViewBounds.TryGetWorldRect(out Rect view) && view.Contains(pos);
            bool mate = NetPlayers.TryMateView(pos, 0f, out _);
            if (!own && !mate) continue;
            spawnsInSight++;
            Log($"Spawn im Bild: {e.name} bei {pos} (eigenes Bild {own}, Mitspieler {mate})");
        }
        known.RemoveWhere(x => x == null);
    }

    private static int RemoteEnemies()
    {
        int n = 0;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++) if (alive[i] != null && alive[i].IsRemote) n++;
        return n;
    }

    private static string StatsLine()
    {
        NetStats st = NetStats.Active;
        if (st == null) return "-";
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < st.Lines.Length; i++)
            parts.Add($"[{i}: {st.Lines[i].Damage:0} dmg, {st.Lines[i].Kills} kills, {st.Lines[i].Revives} rev, {st.Lines[i].Downs} down]");
        return string.Join(" ", parts);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    private void Shot(string name)
    {
        string file = Path.Combine(outDir, $"{role}_{shots++:00}_{name}.png");
        ScreenCapture.CaptureScreenshot(file);
        Log("Bild " + file);
    }

    private void Quit()
    {
        NetSession.Leave();
        Application.Quit();
    }
}
