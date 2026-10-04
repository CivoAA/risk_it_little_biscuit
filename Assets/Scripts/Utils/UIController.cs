using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("HUD")]
    [Tooltip("Aus = das alte HUD (Slider, Timer-Text, Item-Leisten) statt des GameHud.")]
    [SerializeField] private bool useNewHud = true;
    [Tooltip("Aus = die alten Level-Up- und Evo-Panels aus der Szene statt des LevelUpScreen.")]
    [SerializeField] private bool useNewLevelUp = true;

    public LevelUpButton[] levelUpButtons;
    public List<Weapon> currentLevelUpWeapons = new List<Weapon>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (useNewHud) AttachNewHud();
        if (useNewLevelUp) AttachNewLevelUp();
    }

    void Start()
    {
        EnsureEventSystem();
    }

    /// <summary>
    /// Ohne aktives EventSystem kommt kein Klick an - Level-Up, Mixer und Evo
    /// waeren tot. Das EventSystem in GameCore ist absichtlich aus: kommt
    /// der Lauf aus dem Hauptmenue, liegt dessen EventSystem noch geladen daneben,
    /// und zwei aktive melden Warnungen. Startet der Hub dagegen allein (Editor)
    /// oder die Map direkt, wird hart per LoadScene(Single) gewechselt und es
    /// gibt keins. Dann das eigene einschalten.
    ///
    /// Laeuft auch bei jedem Oeffnen eines Auswahl-Panels, falls das geliehene
    /// EventSystem zwischendurch mit seiner Szene verschwunden ist.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;

        EventSystem active = FindAnyObjectByType<EventSystem>();   // nur aktive
        if (active != null)
        {
            EventSystem.current = active;
            return;
        }

        foreach (EventSystem es in FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
        {
            if (es.gameObject.scene != gameObject.scene) continue;
            es.gameObject.SetActive(true);
            es.enabled = true;
            EventSystem.current = es;
            return;
        }

        var go = new GameObject("EventSystem (Run)", typeof(EventSystem), typeof(StandaloneInputModule));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
    }

    /// <summary>
    /// Das GameHud zeichnet Leben, Erfahrung, Zeit, Phase und Items selbst. Die
    /// alten Anzeigen bleiben aktiv und unsichtbar: ItemMenu und ItemMenuBuffs
    /// rechnen in ihrem Update weiter die Slots aus, aus denen das HUD liest.
    /// </summary>
    private void AttachNewHud()
    {
        GameHud.Create(this);

        HideLegacy(playerHealthSlider != null ? playerHealthSlider.gameObject : null);
        HideLegacy(playerExperienceSlider != null ? playerExperienceSlider.gameObject : null);
        HideLegacy(timerText != null ? timerText.gameObject : null);

        Transform wave = transform.Find("Wave Text");
        if (wave != null) HideLegacy(wave.gameObject);

        ItemMenu items = FindAnyObjectByType<ItemMenu>(FindObjectsInactive.Include);
        if (items != null) HideLegacy(items.gameObject);

        ItemMenuBuffs buffs = FindAnyObjectByType<ItemMenuBuffs>(FindObjectsInactive.Include);
        if (buffs != null) HideLegacy(buffs.gameObject);
    }

    /// <summary>
    /// Der LevelUpScreen zeichnet Level-Up-Auswahl, Evo-Buch, Mixer und Death-Screen selbst. Die
    /// alten Panels bleiben der Schalter: RandomWeapon bestueckt weiter die
    /// LevelUpButtons, LevelUpPanelOpen/EvoPanelOpen machen sie an - der
    /// Screen schaut nur zu. Darum unsichtbar statt aus.
    /// </summary>
    private void AttachNewLevelUp()
    {
        LevelUpScreen.Create(this);
        HideLegacy(LevelUpPanel);
        HideLegacy(EvoPanel);
        HideLegacy(PowerUpPanel);
        HideLegacy(GameOverPanel);
        HideLegacy(WinPanel);
    }

    private static void HideLegacy(GameObject go)
    {
        if (go == null) return;
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
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
        EnsureEventSystem();
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
        EnsureEventSystem();
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
        EnsureEventSystem();
        PowerUpPanel.SetActive(true);
        RefreshRerollandBanish();
        Time.timeScale = 0f;
    }
    public void PowerUpPanelClose()
    {
        PowerUpPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void RerollLevelUp()
    {
        bool hasEvo = currentLevelUpWeapons.Any(w => PlayerController.Instance.activeEvos.Contains(w));

        if (hasEvo)
        {
            DamageNumberController.Instance.CreateText(Loc.Get("ui.levelup.evo_no_reroll", "Evos can't be rerolled"), PlayerController.Instance.transform.position);
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
            return;
        }
     
        if (PlayerController.Instance.rerollAmount > 0)
        {
            PlayerController.Instance.RandomWeapon();

            // Skilltree "Sparsamer Reroll": mit etwas Glueck kostet er nichts.
            if (Random.value * 100f < PlayerController.Instance.freeRerollChance)
                DamageNumberController.Instance?.CreateText(Loc.Get("ui.float.free_reroll", "Free Reroll!"), PlayerController.Instance.transform.position);
            else
                PlayerController.Instance.rerollAmount -= 1;

            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
        }
        else
        {
            DamageNumberController.Instance.CreateText(Loc.Get("ui.levelup.no_rerolls", "No rerolls left"), PlayerController.Instance.transform.position);
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
            DamageNumberController.Instance.CreateText(Loc.Get("ui.levelup.no_banish", "No banishes left"), PlayerController.Instance.transform.position);
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
        CookieSoulsGained_Text.text = "Char XP: +" + Mathf.RoundToInt(GameManager.Instance.charXpGained);
    }
    public void GameWinStats()
    {
        CurrencyGained_WIN_Text.text = "Currency: +" + GameManager.Instance.currency;
        CookieSoulsGained_WIN_Text.text = "Char XP: +" + Mathf.RoundToInt(GameManager.Instance.charXpGained);
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
        foreach (AchievementDef def in SessionProgressTracker.Instance.newAchievements)
        {
            if (def != null)
                unlockedVisuals.Add((def.Icon, def.Name));
        }

        foreach (string id in SessionProgressTracker.Instance.newUnlocks)
        {
            UnlockDef unlock = Unlocks.Find(id);
            if (unlock != null)
                unlockedVisuals.Add((unlock.Icon, unlock.Description));
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
