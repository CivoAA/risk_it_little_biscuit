using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public float gameTime;
    public bool gameActiv;
    public bool bossSpawned = false;
    public int currency;
    public float currencyGainMultiplire = 1;
    public ItemMenu itemMenu;
    public ItemMenuBuffs itemMenuBuffs;
    /// <summary>Charakter-Level beim Start des Laufs - für "Level-Up!" im Abschluss.</summary>
    public int charLevelBeforeGame;
    /// <summary>Charakter-XP, die dieser Lauf eingebracht hat.</summary>
    public float charXpGained;

    /// <summary>Besiegte Gegner in diesem Lauf - fuer die Bilanz im Death-Screen.</summary>
    public int kills;

    /// <summary>
    /// Endless: Waehrungswert (vor der Obergrenze) beim Beginn des Endlos-Teils.
    /// Was danach dazukommt, zaehlt nur zu <see cref="RunDifficulty.EndlessRewardShare"/>.
    /// Negativ = der Endlos-Teil hat noch nicht begonnen.
    /// </summary>
    private float endlessCurrencyStart = -1f;

    /// <summary>
    /// Story-Sieg, danach im Endless-Modus weitergespielt: so viel Waehrung
    /// wurde beim Sieg schon gutgeschrieben - am Ende zaehlt nur der Rest.
    /// </summary>
    private int currencyPaid;

    /// <summary>Der letzte Abschluss war ein Story-Sieg (Sieg-Bildschirm offen).</summary>
    private bool storyWon;

    /// <summary>
    /// Dieser Lauf war Story und laeuft nach dem Sieg als Endless weiter.
    /// Zurueck im Hub gilt dann wieder Story.
    /// </summary>
    public bool ContinuedAsEndless { get; private set; }

    private int lastTimerSecond = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    void Start()
    {
        gameActiv = true;
        RunMetrics.RunStarted();
        SecretAchievements.RunStarted();
        RunChallenges.RunStarted();
        EndlessScore.RunStarted();
    }

    void Update()
    {
        if (gameActiv)
        {
            gameTime += Time.deltaTime;

            // Timer-Text nur aktualisieren, wenn sich die angezeigte Sekunde ändert
            // (vermeidet String-Allokation in jedem Frame)
            int currentSecond = Mathf.FloorToInt(gameTime);
            if (currentSecond != lastTimerSecond)
            {
                lastTimerSecond = currentSecond;
                UIController.Instance.UpdateTimer(gameTime);
                CheckEndlessGoal();
                SecretAchievements.Tick(gameTime, kills);
            }

            // Das Pausenmenue hoert selbst auf Escape - sonst wuerde es sich
            // im selben Frame wieder schliessen.
            if (Input.GetKeyDown(KeyCode.Escape) && !PauseMenuPanel.IsOpen)
            {
                Pause();
            }
        }

    }

    /// <summary>
    /// Endless-Erfolg der Welt: <see cref="Ach.EndlessMinutes"/> Minuten im
    /// Endless-Modus durchgehalten. Nur auf echten Karten, nicht in der Test-Szene.
    /// </summary>
    private void CheckEndlessGoal()
    {
        if (!GameSession.IsEndless || gameTime < Ach.EndlessMinutes * 60f) return;
        if (MapDefinition.Active == null || SpawnDirector.Active == null) return;
        AchievementDef def = Ach.EndlessFor(SpawnDirector.Active.PlanId);
        if (def != null && !def.IsUnlocked) Achievements.Unlock(def);
    }

    public void GameOver() => EndRun(false);

    /// <summary>
    /// Lauf aufgeben (Pausenmenue): rechnet ab wie ein Tod - Waehrung, Char-XP,
    /// Bestwerte -, sonst muesste man auf den Tod warten, um sie zu bekommen.
    /// </summary>
    public void GiveUp()
    {
        if (!gameActiv) return;
        // Koop: den anderen Bescheid sagen (als Host endet der Lauf fuer alle).
        NetRun.Active?.OnLocalGiveUp();
        EndRun(true);
    }

    /// <summary>
    /// Koop: der Lauf ist vorbei (alle unten, Boss besiegt oder Host weg) -
    /// abrechnen wie nach einem Tod. Gewonnen ist es, wenn vorher
    /// <see cref="bossSpawned"/> gesetzt wurde (Story).
    /// </summary>
    public void NetFinishRun()
    {
        if (!gameActiv) return;
        EndRun(false);
    }

    private void EndRun(bool gaveUp)
    {
        // Ein Endless-Lauf endet immer mit dem Tod - auch wenn der Boss vorher
        // gefallen ist (der setzt bossSpawned), gibt es keinen Sieg-Bildschirm.
        // Aufgegeben ist nie gewonnen.
        bool won = !gaveUp && bossSpawned && !GameSession.IsEndless;

        if (!gaveUp) Achievements.Unlock(Ach.FirstDeath);
        if (!gaveUp && !won) SecretAchievements.Died(gameTime);
        RunChallenges.RunEnded(won);
        currency = EstimateCurrency();
        // Nach "Endless weiterspielen" ist der Story-Teil schon ausgezahlt.
        int payout = Mathf.Max(0, currency - currencyPaid);
        Shop.AddCurrency(payout);
        if (MapDefinition.Active != null) Achievements.Progress(Ach.Gold1M, payout);
        currencyPaid = Mathf.Max(currencyPaid, currency);
        gameActiv = false;
        storyWon = won;
        RunMetrics.RunEnded(won, gameTime, ContinuedAsEndless);
        // Punktestand ans Steam-Scoreboard der Welt (Story und Endless).
        EndlessScore.RunEnded(gameTime);

        // Der Lauf ist vorbei - was sich an Charakter-XP angesammelt hat, jetzt
        // wegschreiben statt auf den naechsten 5-Sekunden-Takt zu warten.
        Skills.Flush();
        Bestiary.Flush();

        // Bestwerte fuer die Levelauswahl: Story = schnellster Sieg, Endless =
        // laengster Lauf. Nur auf echten Karten (nicht in der Test-Szene) und
        // nicht im Koop - der zaehlt nicht fuer Bestwerte und Freischaltungen.
        if (MapDefinition.Active != null && !NetSession.InRun)
        {
            LevelRecords.ReportRun(LevelRecords.CurrentMapId(), GameSession.IsEndless, won, gameTime);
            // Story-Sieg: die naechste Welt geht auf, Sterne nach Schwierigkeit.
            if (won)
            {
                Unlocks.ReportStoryWin(LevelRecords.CurrentMapId());
                LevelRecords.ReportWin(LevelRecords.CurrentMapId(), RunDifficulty.Level);
            }
        }

        // Charakter-Level nach dem Lauf: ab Level 10 gibt es den Begleiter im Shop.
        Unlocks.CheckCharLevels();

        // Endless: der Welt-Boss ist gefallen - das Level ist geschafft, auch wenn
        // der Lauf danach mit dem Tod endet. Zaehlt fuer die Sieg-Erfolge.
        if (GameSession.IsEndless && bossSpawned) GrantWinAchievements();

        if (!won)
        {
            // Tot ist tot: das Spiel steht sofort still - kein Geschoss, das
            // danach noch den Boss erwischt, kein Schaden, keine XP mehr.
            Time.timeScale = 0f;
            SessionProgressTracker.Instance.EvaluateAfterGame();
            StartCoroutine(ShowGameOverScreen());
        }
        else
        {
            // Gewonnen: ab hier kann nichts mehr passieren - kein spaeter Tod,
            // der den Sieg in einen zweiten Abschluss verwandelt.
            if (PlayerController.Instance != null) PlayerController.Instance.Untouchable = true;
            GrantWinAchievements();
            SecretAchievements.LevelBeaten();
            SessionProgressTracker.Instance.EvaluateAfterGame();
            StartCoroutine(ShowWinScreen());
        }
    }

    private static void GrantWinAchievements()
    {
        Achievements.Unlock(Ach.FirstWin);
        // Sieg mit diesem Charakter - zaehlt auf den Tofu (Ach.AllCharacters).
        Achievements.Unlock(Ach.CharacterWin(Shop.RunSkinIndex));
    }

    /// <summary>
    /// Was der Lauf bis jetzt an Waehrung wert waere. Genau die Formel, mit der
    /// <see cref="GameOver"/> abrechnet - damit der GOLD-Chip im Pausenmenue
    /// nicht auseinanderlaeuft mit dem, was am Ende gutgeschrieben wird.
    /// </summary>
    public int EstimateCurrency()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return currency;

        float raw = RawCurrency(player);

        // Endless: was nach dem Story-Teil dazukommt, zaehlt nur zu einem Bruchteil.
        if (endlessCurrencyStart >= 0f)
        {
            raw = endlessCurrencyStart + Mathf.Max(0f, raw - endlessCurrencyStart) * RunDifficulty.EndlessRewardShare;
        }

        int value = (int)raw;

        // Ueber 2500 zaehlt nur noch ein Zehntel weiter.
        if (value > 2500) value = (int)(2500 + ((value - 2500) * 0.1f));
        return Mathf.Max(0, value);
    }

    /// <summary>
    /// Endless: der Story-Teil ist vorbei. Merkt sich den Waehrungsstand -
    /// alles darueber zaehlt ab jetzt nur noch anteilig (siehe <see cref="EstimateCurrency"/>).
    /// </summary>
    public void BeginEndlessRewards()
    {
        PlayerController player = PlayerController.Instance;
        if (endlessCurrencyStart >= 0f || player == null) return;
        endlessCurrencyStart = RawCurrency(player);
    }

    /// <summary>Die Waehrungsformel ohne Endless-Anteil und ohne Obergrenze.</summary>
    private float RawCurrency(PlayerController player)
    {
        int level = player.currentLevel;
        float fromLevels = player.playerLevels != null
            ? player.playerLevels.Take(Mathf.Max(0, level - 2)).Sum() / 100f
            : 0f;

        return ((fromLevels + (10 * (level - 1)) + gameTime) / 2f) * currencyGainMultiplire;
    }

    /// <summary>
    /// Kann der Sieg-Bildschirm "Endless weiterspielen" anbieten? Nur nach
    /// einem Story-Sieg auf einer echten Karte, deren Plan noch laeuft, und
    /// erst wenn Endless dort offen ist (Story durch oder zwei Siege auf der Welt).
    /// </summary>
    public bool CanContinueAsEndless =>
        !NetSession.InRun && storyWon && !gameActiv && !GameSession.IsEndless
        && (Unlocks.Endless.IsUnlocked || LevelRecords.EndlessByWins(LevelRecords.CurrentMapId()))
        && MapDefinition.Active != null && SpawnDirector.Active != null
        && PlayerController.Instance != null && !SceneFader.IsFading;

    /// <summary>
    /// Story-Sieg, aber der Lauf geht weiter: Sieg-Bildschirm zu, Spieler
    /// zurueck ins Spiel, und der Endlos-Teil beginnt direkt - wie in einem
    /// Endless-Lauf nach dem Boss. Waehrung und Bestwerte des Siegs sind schon
    /// verbucht; der Tod am Ende zaehlt als Endless-Lauf dieser Karte.
    /// </summary>
    public void ContinueAsEndless()
    {
        if (!CanContinueAsEndless) return;

        GameSession.SelectedMode = GameMode.Endless;
        ContinuedAsEndless = true;
        EndlessScore.ContinuedAsEndless();
        storyWon = false;
        bossSpawned = false;

        UIController ui = UIController.Instance;
        if (ui != null)
        {
            ui.WinPanel.SetActive(false);
            ui.GameOverPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        gameActiv = true;
        lastTimerSecond = -1;

        PlayerController.Instance.ReviveForEndless();
        SpawnDirector.Active.ContinueAsEndless();

        // Vulkan-Finale: der Sieg fiel im Herz des Verkohlten - erst raus in die Welt.
        if (VerkohlterHerzkammer.Active != null) VerkohlterHerzkammer.Active.LeaveForEndless();
    }

    /// <summary>
    /// Testtaste F11 (<see cref="RunDebugKeys"/>): sofort in den Endlos-Teil.
    /// Ein Story-Lauf wird dabei zum Endless-Lauf wie nach "Endless
    /// weiterspielen" - zurueck im Hub gilt wieder Story.
    /// </summary>
    public void DebugSkipToEndless()
    {
        if (!gameActiv || SpawnDirector.Active == null) return;

        if (!GameSession.IsEndless)
        {
            GameSession.SelectedMode = GameMode.Endless;
            ContinuedAsEndless = true;
        }
        SpawnDirector.Active.ContinueAsEndless();
    }

    IEnumerator ShowGameOverScreen()
    {
        AudioController.Instance.PalySound(AudioController.Instance.GameOver);
        // Echtzeit: das Spiel steht seit dem Tod (Time.timeScale = 0).
        yield return new WaitForSecondsRealtime(1.5f);
        UIController.Instance.GameOverPanel.SetActive(true);
        UIController.Instance.GameOverStats();
        UIController.Instance.StartRewardCycleDisplay_GameOver();
    }
    IEnumerator ShowWinScreen()
    {
        // Bewusst ohne WinSound - der Sieg-Bildschirm kommt still.
        yield return new WaitForSeconds(1.5f);
        if (gameActiv) yield break;   // schon im Endless-Modus weiter
        UIController.Instance.WinPanel.SetActive(true);
        UIController.Instance.GameWinStats();
        UIController.Instance.StartRewardCycleDisplay_Win();

        // Der Sieg-Bildschirm haelt das Spiel an: keine Gegner, die weiterlaufen,
        // kein Laufen mehr. Der Bildschirm selbst rechnet auf unskalierter Zeit;
        // Restart() und GoToMainMenu() setzen die Zeit beim Umschalten zurueck.
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Lauf beenden und dorthin zurück, wo er gestartet wurde. Drei Wege, je
    /// nachdem wie der Lauf überhaupt zustande kam:
    ///
    ///  1. Ziel liegt geladen daneben (der Normalfall: Hub oder World Map
    ///     haben additiv gestartet) - nur umschalten, das Ziel behält seinen
    ///     Zustand.
    ///  2. Ziel ist bekannt, aber nicht geladen (Hub direkt aus dem Editor
    ///     gestartet, also ohne MenuManager - dann lädt die Levelauswahl hart
    ///     mit Single) - also auch hart zurück.
    ///  3. Kein Ziel bekannt (Test-Szene, Map-Szene direkt gestartet) - neu
    ///     laden, es gibt kein Zurück.
    ///
    /// Alles davon läuft hinter dem <see cref="SceneFader"/>, wie der Weg
    /// Hauptmenü -> Hub. Fertig ist der Wechsel, sobald dieser Manager mit
    /// seiner Szene weg ist.
    /// </summary>
    public void Restart()
    {
        if (SceneFader.IsFading) return;

        // Während der Blende steht das Spiel - erst beim Umschalten läuft die Zeit weiter.
        Time.timeScale = 0f;
        SceneFader.Switch(RestartNow, () => this == null);
    }

    /// <summary>
    /// Derselbe Lauf noch einmal (Abschlussbildschirm "NEUSTART"): gleiche
    /// Karte, gleicher Charakter, gleicher Shop-Stand - ohne Umweg ueber den
    /// Hub (<see cref="RunRestarter"/>). Ohne Map-Szene (Test-Szene, Level
    /// direkt aus dem Editor) wird einfach die aktive Szene neu geladen.
    /// </summary>
    public void RestartRun()
    {
        if (SceneFader.IsFading) return;

        // Koop: neu gestartet wird in der Lobby, gemeinsam.
        if (NetSession.Active)
        {
            Restart();
            return;
        }

        string map = MapSceneSystem.RunMapScene;
        string core = gameObject.scene.name;
        bool split = !string.IsNullOrEmpty(map) && core == MapSceneSystem.CoreScene
                     && Application.CanStreamedLevelBeLoaded(map);

        Time.timeScale = 0f;
        SceneFader.Switch(() =>
        {
            Time.timeScale = 1f;
            // Aus der Story heraus weitergespielt: der Neustart ist wieder Story.
            if (ContinuedAsEndless) GameSession.SelectedMode = GameMode.Story;

            if (split) RunRestarter.Begin(core, map);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }, () => split ? RunRestarter.Done : this == null);
    }

    private void RestartNow()
    {
        Time.timeScale = 1f;

        // Aus der Story heraus weitergespielt: im Hub gilt wieder Story.
        if (ContinuedAsEndless) GameSession.SelectedMode = GameMode.Story;

        // Der Shop-Stand ist ohnehin aktuell - nur die Anzeige muss nachziehen.
        // Der WM_UIController haengt in der World Map und fehlt, wenn der Lauf
        // aus dem Hub kam - die ?. fangen das ab.
        WM_UIController.Instance?.UpdateCurrencyText();
        WM_UIController.Instance?.RefreshButtonTexts();

        string back = GameSession.ReturnScene;
        // Nicht GetActiveScene(): additiv geladen bleibt der Hub bzw. die World
        // Map die aktive Szene - gemeint ist die Szene, in der dieser Manager
        // steht, also die des laufenden Levels.
        string here = gameObject.scene.name;

        Scene target = string.IsNullOrEmpty(back) ? default : SceneManager.GetSceneByName(back);
        bool targetLoaded = target.IsValid() && target.isLoaded && target.name != here;

        // Zurueck in Hub/World Map: der Lauf ist vorbei, ab jetzt gilt wieder
        // der gewaehlte Charakter. Ohne Ziel (Szene direkt aus dem Editor)
        // startet der Lauf neu und behaelt seinen Stand.
        if (!string.IsNullOrEmpty(back)) Shop.ReleaseRun();

        if (MenuManager.Instance != null && targetLoaded)
        {
            // Reihenfolge: erst das Level stilllegen, dann das Ziel anschalten,
            // dann entladen. Jede Szene bringt ihr eigenes Global Light 2D mit
            // - waeren beide gleichzeitig aktiv, meldet das 2D-Licht "More than
            // one global light on layer ...". Das Entladen allein deckt die
            // Luecke nicht ab, weil UnloadSceneAsync erst am Frame-Ende fertig
            // ist. Im neuen System besteht ein Lauf aus zwei Szenen: GameCore
            // (hier) und die Map-Szene daneben - wer nur GameCore wegraeumt,
            // laesst die Welt stehen.
            MenuManager.Instance.DeactivateScene(here);
            MapSceneSystem.DeactivateRunMap();
            MenuManager.Instance.ActivateScene(back);
            // Es wird nichts geladen - von allein schaltet der AudioController
            // die Lauf-Musik hier nicht ab, und die Hub-Musik startet hart.
            AudioController.Instance?.ReturnFromRun(target);
            MenuManager.Instance.UnloadScene(here);
            MapSceneSystem.UnloadRunMap();
            return;
        }

        if (!string.IsNullOrEmpty(back) && Application.CanStreamedLevelBeLoaded(back))
        {
            SceneManager.LoadScene(back, LoadSceneMode.Single);
            return;
        }

        if (!string.IsNullOrEmpty(back))
        {
            Debug.LogWarning($"[GameManager] Rueckreiseziel \"{back}\" steht nicht in den " +
                             "Build Settings - der Lauf startet stattdessen neu.");
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Pause()
    {
        // Mitten in der Blende zurück zum Hub geht kein Pausenmenü mehr auf
        if (SceneFader.IsFading) return;

        // Offen? Dann macht Escape wieder zu - das Menue raeumt beim Zerstoeren
        // selbst auf (Time.timeScale, OnPauseMenuClosed).
        if (PauseMenuPanel.IsOpen)
        {
            PauseMenuPanel.Close();
            return;
        }

        if (!CanPause()) return;

        AudioController.Instance.PalySound(AudioController.Instance.pause);
        ShowItemLevels(true);
        PauseMenuPanel.Open(PauseMenuPanel.PauseMode.Run);
    }

    /// <summary>
    /// Alles, was das Bild schon fuer sich beansprucht, blockt die Pause:
    /// Level-Up, PowerUp, Evo und die Abschlussbildschirme.
    /// </summary>
    private bool CanPause()
    {
        UIController ui = UIController.Instance;
        if (ui == null) return false;

        return !StoryDialog.IsOpen
            && !StoryCutscene.IsPlaying
            && !ui.GameOverPanel.activeSelf
            && !ui.WinPanel.activeSelf
            && !ui.LevelUpPanel.activeSelf
            && !ui.PowerUpPanel.activeSelf
            && !ui.EvoPanel.activeSelf
            && !SacrificeShrine.PanelOpen;
    }

    /// <summary>
    /// Das Pausenmenue hat zugemacht - egal ob ueber "WEITER", Escape oder weil
    /// die Szene wechselt.
    /// </summary>
    public void OnPauseMenuClosed()
    {
        ShowItemLevels(false);
        if (AudioController.Instance != null)
            AudioController.Instance.PalySound(AudioController.Instance.unpause);
    }

    /// <summary>
    /// Die Stufenzahlen an den Waffen- und Buff-Symbolen. Im Lauf stoeren sie,
    /// in der Pause will man sie sehen.
    /// </summary>
    private void ShowItemLevels(bool visible)
    {
        float alpha = visible ? 1f : 0f;

        if (itemMenu != null)
            foreach (TMP_Text t in itemMenu.levelTexts)
                if (t != null) t.alpha = alpha;

        if (itemMenuBuffs != null)
            foreach (TMP_Text t in itemMenuBuffs.levelTexts)
                if (t != null) t.alpha = alpha;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void GoToMainMenu()
    {
        // Hart mit Single statt Szene fuer Szene: das raeumt Level, Map-Szene
        // und Hub in einem Rutsch ab. Der alte Weg ueber MenuManager hat das
        // Hauptmenue additiv neben den noch geladenen Hub gelegt - und ohne
        // MenuManager (Hub direkt aus dem Editor gestartet) gar nichts getan.
        GameSession.LoadMainMenu();
    }
}
