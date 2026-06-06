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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
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
            UIController.Instance.UpdateTimer(gameTime);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Pause();
            } 
        }
        
    }

    public void GameOver()
    {
        AchievementManager.Instance.UnlockAchievement("First_Death");
        currency = (int)(((PlayerController.Instance.playerLevels
        .Take(Mathf.Max(0, PlayerController.Instance.currentLevel - 2))
        .Sum() / 100f + (10 * (PlayerController.Instance.currentLevel - 1)) + gameTime) / 2f)* currencyGainMultiplire);
        if (currency > 2500)
        {
            float overflow = currency - 2500;
            currency = (int)(2500 + (overflow * 0.1f));
        }
        SaveGame.Instance.AddCurrency(currency);
        gameActiv = false;
        if (!bossSpawned)
        {
            gainedThroughAchievements = SkillSaveManager.Instance.currentData.skillCurrency - skillCurrencyBeforeGame;
            SessionProgressTracker.Instance.EvaluateAfterGame();
            StartCoroutine(ShowGameOverScreen());
        }
        else
        {
            AchievementManager.Instance.UnlockAchievement("First_Win");
            gainedThroughAchievements = SkillSaveManager.Instance.currentData.skillCurrency - skillCurrencyBeforeGame;
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
        SaveGame.Instance.LoadGame();
        WM_UIController.Instance.UpdateCurrencyText();
        Time.timeScale = 1f;
        MenuManager.Instance.ActivateScene("World Map");
        MenuManager.Instance.UnloadScene("Game");
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
        AudioController.Instance.SwitchMusic("Main Menu");
        MenuManager.Instance.UnloadScene("Game");
        MenuManager.Instance.ActivateScene("Main Menu");
    }
}
