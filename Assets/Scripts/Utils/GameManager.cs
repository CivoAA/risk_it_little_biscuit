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
    public int skillCurrencyBeforeGame;
    public int gainedThroughAchievements;

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
            }

            // Das Pausenmenue hoert selbst auf Escape - sonst wuerde es sich
            // im selben Frame wieder schliessen.
            if (Input.GetKeyDown(KeyCode.Escape) && !PauseMenuPanel.IsOpen)
            {
                Pause();
            }
        }

    }

    public void GameOver()
    {
        Achievements.Unlock(Ach.FirstDeath);
        currency = EstimateCurrency();
        Shop.AddCurrency(currency);
        gameActiv = false;

        // Der Lauf ist vorbei - was sich an Skillpunkten angesammelt hat, jetzt
        // wegschreiben statt auf den naechsten 5-Sekunden-Takt zu warten.
        Skills.Flush();
        if (!bossSpawned)
        {
            gainedThroughAchievements = Skills.Currency - skillCurrencyBeforeGame;
            SessionProgressTracker.Instance.EvaluateAfterGame();
            StartCoroutine(ShowGameOverScreen());
        }
        else
        {
            Achievements.Unlock(Ach.FirstWin);
            gainedThroughAchievements = Skills.Currency - skillCurrencyBeforeGame;
            SessionProgressTracker.Instance.EvaluateAfterGame();
            StartCoroutine(ShowWinScreen());
        }
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

        int level = player.currentLevel;
        float fromLevels = player.playerLevels != null
            ? player.playerLevels.Take(Mathf.Max(0, level - 2)).Sum() / 100f
            : 0f;

        int value = (int)(((fromLevels + (10 * (level - 1)) + gameTime) / 2f) * currencyGainMultiplire);

        // Ueber 2500 zaehlt nur noch ein Zehntel weiter.
        if (value > 2500) value = (int)(2500 + ((value - 2500) * 0.1f));
        return Mathf.Max(0, value);
    }

    IEnumerator ShowGameOverScreen()
    {
        AudioController.Instance.PalySound(AudioController.Instance.GameOver);
        yield return new WaitForSeconds(1.5f);
        UIController.Instance.GameOverPanel.SetActive(true);
        UIController.Instance.GameOverStats();
        UIController.Instance.StartRewardCycleDisplay_GameOver();
    }
    IEnumerator ShowWinScreen()
    {
        AudioController.Instance.PalySound(AudioController.Instance.WinSound);
        yield return new WaitForSeconds(1.5f);
        UIController.Instance.WinPanel.SetActive(true);
        UIController.Instance.GameWinStats();
        UIController.Instance.StartRewardCycleDisplay_Win();
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

    private void RestartNow()
    {
        Time.timeScale = 1f;

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
    /// Level-Up, PowerUp, Evo, Gamba und die Abschlussbildschirme.
    /// </summary>
    private bool CanPause()
    {
        UIController ui = UIController.Instance;
        if (ui == null) return false;

        return !ui.GameOverPanel.activeSelf
            && !ui.WinPanel.activeSelf
            && !ui.LevelUpPanel.activeSelf
            && !ui.PowerUpPanel.activeSelf
            && !ui.EvoPanel.activeSelf
            && !ui.GambaPanel.activeSelf;
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
