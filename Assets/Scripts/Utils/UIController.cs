using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class UIController : MonoBehaviour
{
    public static UIController Instance;
    [SerializeField] private Slider playerHealthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Slider playerExperienceSlider;
    [SerializeField] private TMP_Text ExperienceText;
    public GameObject GameOverPanel;
    public GameObject WinPanel;
    public GameObject PausePanel;
    public GameObject LevelUpPanel;
    public GameObject GambaPanel;
    public GameObject PowerUpPanel;
    public GameObject EvoPanel;
    public PowerUpDatabase PowerUpDatabase; // deine PowerUp-Liste
    public PowerUpButton[] PowerUpButtons;  // deine 3 UI Buttons
    public TMP_Text RerollText;
    public TMP_Text BanishText;
    public bool banish = false;
    [Header("🔻 Reward-Visuals für GameOver")]
    public Image Achivment_Unlocks_iconImage_GameOver;
    public TMP_Text Achivment_Unlocks_Text_GameOver;
    public TMP_Text CurrencyGained_Text;
    public TMP_Text CookieSoulsGained_Text;

    [Header("🔺 Reward-Visuals für Win")]
    public Image Achivment_Unlocks_iconImage_Win;
    public TMP_Text Achivment_Unlocks_Text_Win;
    public TMP_Text CurrencyGained_WIN_Text;
    public TMP_Text CookieSoulsGained_WIN_Text;
    private List<(Sprite, string)> unlockedVisuals = new();
    private int currentIndex = 0;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text MultiplireTextGamba;

    public LevelUpButton[] levelUpButtons;
    public LevelUpButton GambaButtons;
    public List<Weapon> currentLevelUpWeapons = new List<Weapon>();

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
    public void UpdateHealthSlider()
    {
        playerHealthSlider.maxValue = PlayerController.Instance.playerMaxHealth;
        playerHealthSlider.value = PlayerController.Instance.playerHealth;
        healthText.text = Mathf.FloorToInt(playerHealthSlider.value) + " | " + Mathf.FloorToInt(playerHealthSlider.maxValue);
    }
    public void UpdateExperienceSlider()
    {
        playerExperienceSlider.maxValue = PlayerController.Instance.playerLevels[PlayerController.Instance.currentLevel - 1];
        playerExperienceSlider.value = PlayerController.Instance.experience;
        ExperienceText.text = PlayerController.Instance.currentLevel.ToString();
    }

    public void UpdateTimer(float timer)
    {
        float min = Mathf.FloorToInt(timer / 60f);
        float sec = Mathf.FloorToInt(timer % 60f);

        timerText.text = min + ":" + sec.ToString("00");
    }

    public void LevelUpPanelOpen()
    {
        LevelUpPanel.SetActive(true);
        RefreshRerollandBanish();
        Time.timeScale = 0f;
    }
    public void LevelUpPanelClose()
    {
        LevelUpPanel.SetActive(false);
        Time.timeScale = 1f;
    }
    public void EvoPanelOpen()
    {
        EvoPanel.SetActive(true);
        LevelUpPanel.SetActive(false);
    }
    public void EvoPanelClose()
    {
        EvoPanel.SetActive(false);
        LevelUpPanel.SetActive(true);
    }
    public void PowerUpPanelOpen()
    {
        PowerUpPanel.SetActive(true);
        RefreshRerollandBanish();
        Time.timeScale = 0f;
    }
    public void PowerUpPanelClose()
    {
        PowerUpPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void GambaPanelOpen()
    {
        AudioController.Instance.PalySound(AudioController.Instance.LevelUpSound);
        GambaPanel.SetActive(true);
        Time.timeScale = 0f;
        Gamba.Instance.wins = 1;
        MultiplireTextGamba.text = "x1";
    }
    public void GambaPanelClose()
    {
        GambaPanel.SetActive(false);
        Time.timeScale = 1f;
    }
    public void RerollLevelUp()
    {
        bool hasEvo = currentLevelUpWeapons.Any(w => PlayerController.Instance.activeEvos.Contains(w));

        if (hasEvo)
        {
            DamageNumberController.Instance.CreateText("Can't reroll Evo weapons!", PlayerController.Instance.transform.position);
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
            return;
        }
     
        if (PlayerController.Instance.rerollAmount > 0)
        {
            PlayerController.Instance.RandomWeapon();
            PlayerController.Instance.rerollAmount -= 1;
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
        }
        else
        {
            DamageNumberController.Instance.CreateText("No more Rerolles left", PlayerController.Instance.transform.position);
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
        }
        RefreshRerollandBanish();
    }
    public void BanishLevelUp()
    {
        if (PlayerController.Instance.banishAmount > 0)
        {
            if (banish == false)
            {
                banish = true;
                LevelUpPanel.GetComponent<Image>().color = new Color32(255, 0, 0, 187);
            }
            else
            {
                banish = false;
                ColorUtility.TryParseHtmlString("#404D52BB", out Color c); LevelUpPanel.GetComponent<Image>().color = c;
            }
        }
        else
        {
            DamageNumberController.Instance.CreateText("No more Banishes left", PlayerController.Instance.transform.position);
        }
        AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
        RefreshRerollandBanish();
    }
    public void RefreshRerollandBanish()
    {
        RerollText.text = "Reroll: " + PlayerController.Instance.rerollAmount;
        BanishText.text = "Banish: " + PlayerController.Instance.banishAmount;
    }

    public void Panelback()
    {
        banish = false;
        ColorUtility.TryParseHtmlString("#404D52BB", out Color c); LevelUpPanel.GetComponent<Image>().color = c;
    }

    public void GameOverStats()
    {
        CurrencyGained_Text.text = "Currency: +" + GameManager.Instance.currency;
        CookieSoulsGained_Text.text = "Cookie Souls: +" + GameManager.Instance.gainedThroughAchievements;
    }
    public void GameWinStats()
    {
        CurrencyGained_WIN_Text.text = "Currency: +" + GameManager.Instance.currency;
        CookieSoulsGained_WIN_Text.text = "Cookie Souls: +" + GameManager.Instance.gainedThroughAchievements;
    }


    public void StartRewardCycleDisplay_GameOver()
    {
        StartCoroutine(CycleDisplay(Achivment_Unlocks_iconImage_GameOver, Achivment_Unlocks_Text_GameOver));
    }

    public void StartRewardCycleDisplay_Win()
    {
        StartCoroutine(CycleDisplay(Achivment_Unlocks_iconImage_Win, Achivment_Unlocks_Text_Win));
    }

    void CollectVisuals()
    {
        foreach (string id in SessionProgressTracker.Instance.newAchievements)
        {
            var ach = AchievementManager.Instance.achievements.Find(a => a.id == id);
            if (ach != null)
                unlockedVisuals.Add((ach.icon, ach.AchievmentName));
        }

        foreach (string id in SessionProgressTracker.Instance.newUnlocks)
        {
            var unlock = UnlockManager.Instance.unlocks.Find(u => u.id == id);
            if (unlock != null)
                unlockedVisuals.Add((unlock.unlockedIcon, unlock.displayName));
        }
    }

    private IEnumerator CycleDisplay(Image iconTarget, TMP_Text textTarget)
    {
        unlockedVisuals.Clear();
        currentIndex = 0;

        CollectVisuals();

        if (unlockedVisuals.Count == 0)
        {
            iconTarget.gameObject.SetActive(false);
            textTarget.gameObject.SetActive(false);
            yield break;
        }

        iconTarget.gameObject.SetActive(true);
        textTarget.gameObject.SetActive(true);

        while (true)
        {
            var item = unlockedVisuals[currentIndex];
            iconTarget.sprite = item.Item1;
            textTarget.text = item.Item2;

            currentIndex = (currentIndex + 1) % unlockedVisuals.Count;
            yield return new WaitForSeconds(3f);
        }
    }

}
