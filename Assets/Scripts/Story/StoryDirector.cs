using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fuehrt Regie ueber die Story. Haengt sich beim Spielstart selbst an
/// (kein Szenen-Objekt) und ueberlebt Szenenwechsel.
///
/// DIE GESCHICHTE IN KURZ
///   Vor hundert Jahren rutschte ein Keks im Grossen Ofen hinters Blech -
///   die Kuechenuhr Pomo, die haette klingeln sollen, war eingeschlafen. Er
///   gluehte hundert Jahre und wurde zum Verkohlten. Jetzt heizt er den Ofen
///   an, um die ganze Kueche schwarz zu backen; sein Herz hat er in vier
///   Glutsplitter zerbrochen und vier Herrschern gegeben (Keks-Koenig,
///   Glutwurz, Eiskaiser, das Gespenst). Pomo schickt den Spieler los, die
///   Splitter zu holen - mit allen vier geht die Ofentuer (der Vulkan) auf.
///   Im Geisterwald kommt heraus, dass Pomo die Uhr war. Am Ende zerbricht
///   das Herz, Pomo klingelt endlich, und der Verkohlte ist fertig gebacken.
///
/// WANN WAS KOMMT
///   Hub, erster Besuch          Intro-Cutscene + Gespraech "intro"
///   Levelauswahl, SPIELEN       "brief.[Plan]" einmal je Welt (nur Story-Modus)
///   Lauf, Start                 Zwischenruf "start.[Plan]"
///   Lauf, Boss erscheint        Zwischenruf "boss.[Plan]"
///   Lauf, Boss faellt           "defeat.[Plan]", Splitter fliegt ein,
///                               "count.N", dann der Sieg-Bildschirm
///   Hub, zurueck                "debrief.[Plan]", bei vier Splittern
///                               "allshards" + Banner, Vulkan ist offen
///   Lauf Vulkan, Herz zerfaellt Finale-Cutscene, dann Sieg-Bildschirm
///   Hub, danach                 "epilogue"
///   Endless                     nichts - Endless ist der Story-Ausschalter.
///
/// TESTTASTEN (nur Editor und Development-Build)
///   F12          im Lauf: Level sofort gewonnen (Boss gilt als besiegt,
///                das ganze Story-Ende laeuft ab)
///                im Hub:  naechsten Splitter schenken (wie ein Sieg, das
///                Gespraech danach kommt sofort)
///   Shift + F12  Story ganz zuruecksetzen (Intro kommt wieder)
///   F11          im Hub: Intro-Cutscene noch einmal
///   Konsole: "story" zeigt den Stand, "story reset", "story kapitel N"
/// </summary>
public class StoryDirector : MonoBehaviour
{
    private const float HubSettle = 0.7f;

    public static StoryDirector Instance { get; private set; }

    /// <summary>
    /// Die Story hat das Ende dieses Laufs uebernommen: kein Finisher, kein Tod
    /// nach dem Boss - der Sieg-Bildschirm kommt, wenn die Story fertig ist.
    /// </summary>
    public static bool ClaimsWin { get; private set; }

    private SpawnDirector lastRun;
    private float runStartedAt = -1f;
    private bool startBarkDone;
    private float hubReadySince = -1f;
    private bool busy;
    private static bool DebugKeys => Application.isEditor || Debug.isDebugBuild;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("StoryDirector");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<StoryDirector>();

        // Direkt im Hub gestartet (Editor): sceneLoaded ist da schon vorbei.
        Scene hub = SceneManager.GetSceneByName(GameSession.HubScene);
        if (hub.IsValid() && hub.isLoaded) StoryPomoNpc.EnsureIn(hub);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        ClaimsWin = false;
    }

    private void OnEnable()
    {
        Enemy.BossDefeated += OnBossDefeated;
        SpawnDirector.BossSpawned += OnBossSpawned;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        Enemy.BossDefeated -= OnBossDefeated;
        SpawnDirector.BossSpawned -= OnBossSpawned;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameSession.HubScene) StoryPomoNpc.EnsureIn(scene);
    }

    // ==================================================================
    //  Zustand
    // ==================================================================

    /// <summary>
    /// Wellenplan des laufenden Laufs, so wie die Levelauswahl ihn nennt
    /// (World1 ...), leer ohne Lauf. Gefragt wird der SpawnDirector: die
    /// Kuechen-Karte heisst "World0" und laeuft mit Plan World1.
    /// </summary>
    private static string RunPlan => SpawnDirector.Active != null ? SpawnDirector.Active.PlanId : "";

    private static bool InRun => GameManager.Instance != null && SpawnDirector.Active != null;

    /// <summary>Laeuft dieser Lauf als Story-Kapitel, das noch offen ist?</summary>
    private static bool ChapterOpen(string plan)
    {
        if (GameSession.IsEndless || string.IsNullOrEmpty(plan)) return false;
        if (StoryProgress.IsShardWorld(plan)) return !StoryProgress.HasShard(plan);
        if (string.Equals(plan, StoryProgress.FinaleWorld, StringComparison.OrdinalIgnoreCase))
            return !StoryProgress.FinaleDone;
        return false;
    }

    private static bool IsFinale(string plan) =>
        string.Equals(plan, StoryProgress.FinaleWorld, StringComparison.OrdinalIgnoreCase);

    // ==================================================================
    //  Update
    // ==================================================================

    private void Update()
    {
        TrackRun();
        if (DebugKeys) DebugInput();
        UpdateHub();
    }

    private void TrackRun()
    {
        SpawnDirector run = SpawnDirector.Active;
        if (run != lastRun)
        {
            lastRun = run;
            ClaimsWin = false;
            startBarkDone = false;
            runStartedAt = run != null ? Time.time : -1f;
        }
        if (run == null || startBarkDone) return;

        if (Time.time - runStartedAt > 2.5f)
        {
            startBarkDone = true;
            string plan = RunPlan;
            if (ChapterOpen(plan)) StoryBark.Show(StoryTexts.Bark("start." + plan));
        }
    }

    // ------------------------------------------------------------------ Hub

    private bool HubReady()
    {
        if (InRun || GameManager.Instance != null) return false;
        Scene hubScene = SceneManager.GetSceneByName(GameSession.HubScene);
        if (!hubScene.IsValid() || !hubScene.isLoaded) return false;
        HubUI ui = FindAnyObjectByType<HubUI>();
        if (ui == null || !ui.isActiveAndEnabled) return false;
        if (SceneFader.IsFading || HubUI.InputBlocked) return false;
        if (StoryDialog.IsOpen || StoryCutscene.IsPlaying || HubLevelSelectUI.IsOpen) return false;
        return true;
    }

    private void UpdateHub()
    {
        if (busy) return;
        if (!HubReady())
        {
            hubReadySince = -1f;
            return;
        }
        if (hubReadySince < 0f) hubReadySince = Time.unscaledTime;
        if (Time.unscaledTime - hubReadySince < HubSettle) return;

        if (!StoryProgress.IntroSeen)
        {
            StartCoroutine(Intro());
            return;
        }

        if (StoryProgress.FinaleDone && !StoryProgress.EpilogueSeen)
        {
            busy = true;
            StoryDialog.Play("epilogue", () => { StoryProgress.MarkEpilogueSeen(); busy = false; }, HubOptions());
            return;
        }

        string pending = StoryProgress.PendingDebrief;
        if (pending != null)
        {
            busy = true;
            StoryDialog.Play("debrief." + pending, () =>
            {
                StoryProgress.MarkDebriefed(pending);
                busy = false;
            }, HubOptions());
            return;
        }

        if (StoryProgress.HasAllShards && !StoryProgress.AllShardsSeen)
        {
            busy = true;
            StoryDialog.Play("allshards", () =>
            {
                StoryProgress.MarkAllShardsSeen();
                StoryReveal.Banner(StoryTexts.T("story.ui.volcano"), StoryTexts.T("story.ui.volcanosub"), () => busy = false);
            }, HubOptions());
        }
    }

    private IEnumerator Intro()
    {
        busy = true;
        bool done = false;
        StoryMusic.BeginIntro();
        StoryCutscene.Play(StoryCutscene.Kind.Intro, () => done = true);
        while (!done) yield return null;
        // Das Intro ist weg: sein Stueck klingt aus, die Hub-Musik kommt zurueck.
        StoryMusic.EndIntro();
        yield return null;
        done = false;
        StoryDialog.Play("intro", () => done = true, HubOptions());
        while (!done) yield return null;
        StoryProgress.MarkIntroSeen();
        busy = false;
    }

    private static StoryDialog.Options HubOptions() =>
        new StoryDialog.Options { FreezeTime = false, BlockHub = true, Dim = 0.55f };

    // ------------------------------------------------------- Levelauswahl

    /// <summary>
    /// Aus der Levelauswahl vor dem Start: braucht diese Welt im Story-Modus
    /// noch ihr Briefing, laeuft es jetzt und <paramref name="launch"/> danach.
    /// Gibt false zurueck, wenn nichts zu erzaehlen ist (dann selbst starten).
    /// </summary>
    public static bool TryBrief(string plan, bool endless, Action launch)
    {
        if (endless || string.IsNullOrWhiteSpace(plan) || StoryDialog.IsOpen) return false;
        if (!StoryProgress.IsStoryWorld(plan) || StoryProgress.WasBriefed(plan)) return false;
        if (IsFinale(plan) && StoryProgress.FinaleDone) return false;
        if (StoryProgress.IsShardWorld(plan) && StoryProgress.HasShard(plan)) return false;
        StoryTexts.Line[] lines = StoryTexts.Convo("brief." + plan.Trim());
        if (lines == null) return false;

        StoryDialog.Play(lines, () =>
        {
            StoryProgress.MarkBriefed(plan);
            launch?.Invoke();
        }, new StoryDialog.Options { FreezeTime = false, BlockHub = false, Dim = 0.45f });
        return true;
    }

    // ------------------------------------------------------------------ Lauf

    private void OnBossSpawned(Enemy boss)
    {
        if (boss == null || boss.Role != EnemyRole.Boss) return;
        string plan = RunPlan;
        if (!ChapterOpen(plan)) return;
        StoryBark.Show(StoryTexts.Bark("boss." + plan));
    }

    private void OnBossDefeated(Enemy boss)
    {
        if (ClaimsWin || !InRun) return;
        string plan = RunPlan;
        if (!ChapterOpen(plan)) return;

        ClaimsWin = true;
        Vector3 at = boss != null ? boss.transform.position : Vector3.zero;
        if (IsFinale(plan)) StartCoroutine(FinaleEnding());
        else StartCoroutine(ShardEnding(plan, at));
    }

    private IEnumerator ShardEnding(string plan, Vector3 bossPos)
    {
        StoryBark.HideNow();
        // Gleich eintragen - der Splitter geht nicht verloren, egal was jetzt passiert.
        StoryProgress.AddShard(plan);
        int count = StoryProgress.ShardCount;
        Shield(true);

        // Kurz durchatmen: die Beute fliegt zu
        float until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until) yield return null;
        yield return WaitForFreeScreen();
        if (!RunStillOn()) yield break;

        Time.timeScale = 0f;
        bool done = false;
        StoryDialog.Play("defeat." + plan, () => done = true, RunOptions());
        while (!done) yield return null;

        done = false;
        StoryReveal.Shard(bossPos, count, () => done = true);
        while (!done) yield return null;

        done = false;
        StoryDialog.Play("count." + Mathf.Clamp(count, 1, 4), () => done = true, RunOptions());
        while (!done) yield return null;

        EndRun();
    }

    private IEnumerator FinaleEnding()
    {
        StoryBark.HideNow();
        StoryProgress.MarkFinaleDone();
        Shield(true);

        // Das Herz zerfaellt noch - erst wenn die Kammer dunkel ist, beginnt das Finale.
        float until = Time.realtimeSinceStartup + 2.2f;
        while (Time.realtimeSinceStartup < until) yield return null;
        yield return WaitForFreeScreen();
        if (!RunStillOn()) yield break;

        bool done = false;
        StoryCutscene.Play(StoryCutscene.Kind.Finale, () => done = true);
        while (!done) yield return null;
        EndRun();
    }

    private static StoryDialog.Options RunOptions() =>
        new StoryDialog.Options { FreezeTime = false, BlockHub = false, Dim = 0.6f };

    private static void Shield(bool on)
    {
        if (PlayerController.Instance != null) PlayerController.Instance.Untouchable = on;
    }

    private static bool RunStillOn() => GameManager.Instance != null && GameManager.Instance.gameActiv;

    /// <summary>Wartet, bis kein Level-Up, PowerUp oder Evo-Fenster mehr offen ist.</summary>
    private static IEnumerator WaitForFreeScreen()
    {
        while (true)
        {
            UIController ui = UIController.Instance;
            bool busyScreen = ui != null && (ui.LevelUpPanel.activeSelf || ui.PowerUpPanel.activeSelf ||
                                             ui.EvoPanel.activeSelf || ui.GameOverPanel.activeSelf ||
                                             ui.WinPanel.activeSelf);
            if (!busyScreen && !PauseMenuPanel.IsOpen) yield break;
            if (ui != null && (ui.GameOverPanel.activeSelf || ui.WinPanel.activeSelf)) yield break;
            yield return null;
        }
    }

    /// <summary>Lauf als Sieg beenden: Sieg-Bildschirm wie nach jedem Boss.</summary>
    private static void EndRun()
    {
        Time.timeScale = 1f;
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.gameActiv) return;
        gm.bossSpawned = true;
        gm.GameOver();
    }

    // ==================================================================
    //  Testtasten
    // ==================================================================

    private void DebugInput()
    {
        if (!Input.GetKeyDown(KeyCode.F12) && !Input.GetKeyDown(KeyCode.F11)) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.F12) && shift)
        {
            StoryProgress.ResetAll();
            Debug.Log("[Story] (Shift+F12) Story zurueckgesetzt - im Hub kommt gleich das Intro.");
            return;
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            if (InRun || busy || StoryCutscene.IsPlaying) return;
            StartCoroutine(Intro());
            return;
        }

        if (InRun) WinNow();
        else DebugNextShard();
    }

    /// <summary>F12 im Lauf: als waere der Boss dieser Karte gerade gefallen.</summary>
    public void WinNow()
    {
        if (!RunStillOn() || ClaimsWin || StoryDialog.IsOpen || StoryCutscene.IsPlaying) return;
        string plan = RunPlan;
        Debug.Log($"[Story] (F12) Level \"{plan}\" sofort gewonnen.");

        // Lebende Bosse raeumen, ohne dass ihr Tod etwas ausloest.
        Vector3 at = PlayerController.Instance != null ? PlayerController.Instance.transform.position + new Vector3(4f, 2f, 0f) : Vector3.zero;
        foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (e == null || !EnemyCatalog.IsBossRole(e.Role)) continue;
            if (e.Role == EnemyRole.Boss) at = e.transform.position;
            Destroy(e.gameObject);
        }

        if (!ChapterOpen(plan))
        {
            EndRun();
            return;
        }

        ClaimsWin = true;
        GameManager.Instance.bossSpawned = true;
        if (IsFinale(plan)) StartCoroutine(FinaleEnding());
        else StartCoroutine(ShardEnding(plan, at));
    }

    /// <summary>F12 im Hub: der naechste fehlende Splitter, als haette man die Welt gewonnen.</summary>
    private void DebugNextShard()
    {
        if (!StoryProgress.IntroSeen) StoryProgress.MarkIntroSeen();
        foreach (string w in StoryProgress.ShardWorlds)
        {
            if (StoryProgress.HasShard(w)) continue;
            StoryProgress.MarkBriefed(w);
            StoryProgress.AddShard(w);
            Debug.Log($"[Story] (F12) Splitter aus {w} geschenkt ({StoryProgress.ShardCount}/4).");
            return;
        }
        if (!StoryProgress.FinaleDone)
        {
            if (!StoryProgress.AllShardsSeen) StoryProgress.MarkAllShardsSeen();
            StoryProgress.MarkBriefed(StoryProgress.FinaleWorld);
            StoryProgress.MarkFinaleDone();
            Debug.Log("[Story] (F12) Finale als gesehen markiert - der Epilog kommt.");
        }
    }
}
