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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Pause();
            }
        }

    }

    public void GameOver()
    {
        Achievements.Unlock(Ach.FirstDeath);
        currency = (int)(((PlayerController.Instance.playerLevels
        .Take(Mathf.Max(0, PlayerController.Instance.currentLevel - 2))
        .Sum() / 100f + (10 * (PlayerController.Instance.currentLevel - 1)) + gameTime) / 2f)* currencyGainMultiplire);
        if (currency > 2500)
        {
            float overflow = currency - 2500;
            currency = (int)(2500 + (overflow * 0.1f));
        }
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

    public void Restart()
    {
        // Test-Szene (läuft ohne World Map): einfach die aktive Szene neu laden.
        if (MenuManager.Instance == null)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        // Der Shop-Stand ist ohnehin aktuell - nur die Anzeige muss nachziehen.
        // Der WM_UIController haengt in der World Map und fehlt, wenn der Lauf
        // aus dem Hub kam - die ?. fangen das ab.
        WM_UIController.Instance?.UpdateCurrencyText();
        WM_UIController.Instance?.RefreshButtonTexts();
        Time.timeScale = 1f;

        // Zurueck dorthin, wo der Lauf gestartet wurde: Hub oder World Map.
        // Den Eintrag setzt die Levelauswahl bzw. PlayerWorldInteraction.
        // Nicht GetActiveScene(): additiv geladen bleibt der Hub bzw. die World
        // Map die aktive Szene - gemeint ist die Szene, in der dieser Manager
        // steht, also die des laufenden Levels.
        string back = string.IsNullOrEmpty(GameSession.ReturnScene) ? GameSession.HubScene : GameSession.ReturnScene;
        string here = gameObject.scene.name;

        // Reihenfolge: erst das Level stilllegen, dann das Ziel anschalten, dann
        // entladen. Jede Szene bringt ihr eigenes Global Light 2D mit - waeren
        // beide gleichzeitig aktiv, meldet das 2D-Licht "More than one global
        // light on layer ...". Das Entladen allein deckt die Luecke nicht ab,
        // weil UnloadSceneAsync erst am Frame-Ende fertig ist. So laeuft es
        // herum wie beim Betreten, wo ebenfalls erst die Startszene ausgeht.
        MenuManager.Instance.DeactivateScene(here);
        MenuManager.Instance.ActivateScene(back);
        MenuManager.Instance.UnloadScene(here);
    }
    public void Pause()
    {
        bool canToggle = true;
        if (UIController.Instance.GameOverPanel.activeSelf == true)
        {
            canToggle = false;
        }
        else if (UIController.Instance.LevelUpPanel.activeSelf == true)
        {
            canToggle = false;
        }
        else if (UIController.Instance.GambaPanel.activeSelf == true)
        {
            canToggle = false;
        }
        else if (UIController.Instance.PowerUpPanel.activeSelf == true)
        {
            canToggle = false;
        }
        else if (UIController.Instance.WinPanel.activeSelf == true)
        {
            canToggle = false;
        }
        else if (UIController.Instance.EvoPanel.activeSelf == true)
        {
            canToggle = false;
        }
        

        if (canToggle)
        {
            bool isPaused = UIController.Instance.PausePanel.activeSelf;

            if (!isPaused)
            {
                // Pause aktivieren
                UIController.Instance.PausePanel.SetActive(true);
                Time.timeScale = 0f;
                AudioController.Instance.PalySound(AudioController.Instance.pause);

                if (itemMenu != null)
                {
                    foreach (TMP_Text t in itemMenu.levelTexts)
                    {
                        if (t != null) t.alpha = 1f; // Text wieder einblenden
                    }
                }
                if (itemMenuBuffs != null)
                {
                    foreach (TMP_Text t in itemMenuBuffs.levelTexts)
                    {
                        if (t != null) t.alpha = 1f; // Text wieder einblenden
                    }
                }
            }
            else
            {
                UIController.Instance.PausePanel.SetActive(false);
                Time.timeScale = 1f;
                AudioController.Instance.PalySound(AudioController.Instance.unpause);

                // 🔹 LevelTexts wieder sichtbar machen
                if (itemMenu != null)
                {
                    foreach (TMP_Text t in itemMenu.levelTexts)
                    {
                        if (t != null) t.alpha = 0f; // Text ausblenden
                    }
                }
                if (itemMenuBuffs != null)
                {
                    foreach (TMP_Text t in itemMenuBuffs.levelTexts)
                    {
                        if (t != null) t.alpha = 0f; // Text ausblenden
                    }
                }
            }
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        // Test-Szene (läuft ohne MenuManager): nichts zu entladen.
        if (MenuManager.Instance == null) return;

        AudioController.Instance.SwitchMusic("Main Menu");
        MenuManager.Instance.UnloadScene("Game");
        MenuManager.Instance.ActivateScene("Main Menu");
    }
}
