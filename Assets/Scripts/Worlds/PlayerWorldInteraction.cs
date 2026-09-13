using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerWorldInteraction : MonoBehaviour
{
    [SerializeField] private LevelPoint currentPoint;
    [SerializeField] private MapName currentMap;

    public GameObject rogueLikeCanvas; // Skill Tree!!! 
    public GameObject toolTipCanvas;
    public GameObject pauseCanvas;
    public GameObject pressEImage;
    public GameObject shopCanvas;
    public GameObject CharCaves;
    public GameObject AchivementCaves;
    public GameObject UnlocksCaves;
    public GameObject blackScreen; // globaler Black Screen für Teleport
    private TeleportPoint currentTeleport;

    private enum InteractionType { None, Map, Shop, RogueLike, CharSelecter, Achiv, Unlocks }
    private InteractionType currentInteraction = InteractionType.None;
    private List<GameObject> closeablePanels;


    void Start()
    {
        closeablePanels = new List<GameObject>
        {
            CharCaves,
            rogueLikeCanvas,
            shopCanvas,
            AchivementCaves,
            UnlocksCaves
        };
    }
    void Update()
    {
        // Taste E drücken
        if (Input.GetKeyDown(KeyCode.E))
        {
            // 🔹 Teleport-Interaktion
            if (currentTeleport != null)
            {
                StartCoroutine(TeleportToPoint(currentTeleport));
                return;
            }
            else
            {
                AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
            }

            switch (currentInteraction)
            {
                case InteractionType.Map:
                    SessionProgressTracker.Instance.SnapshotBeforeGame(); //Sichern der Achivments und Unlocks bevor das spiel Startet
                    SkillSaveManager.Instance.ApplyToScene();
                    AchievementManager.Instance.UnlockAchievement("First_Game");
                    MapsManager.Instance.selectedMap = currentMap.mapID;
                    MapsManager.Instance.extraData = LevelPoint.Instance.extraData;
                    MenuManager.Instance.ActivateScene(currentMap.sceneToLoad);
                    MenuManager.Instance.DeactivateScene("World Map");
                    break;

                case InteractionType.Shop:
                    if (shopCanvas.activeSelf)
                    {
                        shopCanvas.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    else
                    {
                        WM_GameManager gameManager = FindAnyObjectByType<WM_GameManager>();
                        if (gameManager != null)
                        {
                            gameManager.RefreshUnlockButtons();
                        }
                        shopCanvas.SetActive(true);
                        Time.timeScale = 0f;
                    }
                    break;

                case InteractionType.RogueLike:
                    if (rogueLikeCanvas.activeSelf)
                    {
                        rogueLikeCanvas.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    else
                    {
                        if (SkillSaveManager.Instance != null)
                            SkillSaveManager.Instance.ApplyToScene();
                        rogueLikeCanvas.SetActive(true);
                        Time.timeScale = 0f;
                    }
                    break;

                case InteractionType.CharSelecter:
                    if (CharCaves.activeSelf)
                    {
                        CharCaves.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    else
                    {
                        CharCaves.SetActive(true);
                        Time.timeScale = 0f;
                        WM_UIController.Instance.currentIndex = (int)LevelPoint.Instance.extraData[0];
                        WM_UIController.Instance.UpdateCarousel();
                    }
                    break;

                case InteractionType.Achiv:
                    if (AchivementCaves.activeSelf)
                    {
                        AchivementCaves.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    else
                    {
                        AchivementCaves.SetActive(true);
                        Achievement_UI_Manager.Instance?.RefreshProgressUI();
                        Time.timeScale = 0f;
                    }
                    break;

                case InteractionType.Unlocks:
                    if (UnlocksCaves.activeSelf)
                    {
                        UnlocksCaves.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    else
                    {
                        UnlocksCaves.SetActive(true);
                        UnlockUIManager.Instance?.RefreshUI();
                        Time.timeScale = 0f;
                    }
                    break;
            }
        }

        // ESC zum Pause-Menü
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
            if (TryCloseOpenPanel())
            {
                // Ein Panel wurde geschlossen → fertig
                return;
            }

            // Kein Panel offen → Pause-Menü toggeln
            if (pauseCanvas.activeSelf)
                PauseCanvasClose();
            else
                PauseCanvasOpen();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        MapName map = other.GetComponent<MapName>();
        if (map != null)
        {
            currentMap = map;
            currentInteraction = InteractionType.Map;
            pressEImage.SetActive(true);
            return;
        }

        if (other.CompareTag("Shop"))
        {
            currentInteraction = InteractionType.Shop;
            pressEImage.SetActive(true);
            return;
        }

        if (other.CompareTag("RogueLike"))
        {
            currentInteraction = InteractionType.RogueLike;
            pressEImage.SetActive(true);
            return;
        }

        if (other.CompareTag("CharSelecter"))
        {
            currentInteraction = InteractionType.CharSelecter;
            pressEImage.SetActive(true);
            return;
        }
        if (other.CompareTag("Achievements"))
        {
            currentInteraction = InteractionType.Achiv;
            pressEImage.SetActive(true);
            return;
        }
        if (other.CompareTag("Unlocks"))
        {
            currentInteraction = InteractionType.Unlocks;
            pressEImage.SetActive(true);
            return;
        }

        // 🔹 TeleportPoint erkannt?
        TeleportPoint tp = other.GetComponent<TeleportPoint>();
        if (tp != null)
        {
            currentTeleport = tp;
            pressEImage.SetActive(true);
            return;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<MapName>() == currentMap)
        {
            currentMap = null;
            currentInteraction = InteractionType.None;
            pressEImage.SetActive(false);
        }

        if (other.CompareTag("Shop") || other.CompareTag("RogueLike") || other.CompareTag("CharSelecter") || other.CompareTag("Achievements"))
        {
            currentInteraction = InteractionType.None;
            pressEImage.SetActive(false);
        }

        // 🔹 Teleport verlassen
        if (other.GetComponent<TeleportPoint>() == currentTeleport)
        {
            currentTeleport = null;
            pressEImage.SetActive(false);
        }
    }

    public void rogueLikePanelClose()
    {
        rogueLikeCanvas.SetActive(false);
        toolTipCanvas.SetActive(false);
        Time.timeScale = 1f;
    }
    public void ShopPanelClose()
    {
        shopCanvas.SetActive(false);
        Time.timeScale = 1f;
    }

    public void CharSelecterPanelClose()
    {
        CharCaves.SetActive(false);
        Time.timeScale = 1f;
    }

    public void PauseCanvasOpen()
    {
        pauseCanvas.SetActive(true);
        Time.timeScale = 0f;
        // Slider-Refresh nur beim Öffnen statt jeden Frame in Update
        // (FindObjectsByType pro Frame war teuer)
        AudioSettingsManager.Instance?.UpdateAllVolumeSliders();
    }

    public void PauseCanvasClose()
    {
        pauseCanvas.SetActive(false);
        Time.timeScale = 1f;
    }
    public void AchivementCavesOpen()
    {
        AchivementCaves.SetActive(true);
        Time.timeScale = 0f;
    }
    public void SkilltreeCavesClose()
    {
        rogueLikeCanvas.SetActive(false);
        Time.timeScale = 1f;
    }

    public void AchivementCavesClose()
    {
        AchivementCaves.SetActive(false);
        Time.timeScale = 1f;
    }
    public void UnlocksCavesClose()
    {
        UnlocksCaves.SetActive(false);
        Time.timeScale = 1f;
    }

    public void CharCanvasClose()
    {
        CharCaves.SetActive(false);
        Time.timeScale = 1f;
    }

    // 🔹 Allgemeine Teleport-Funktion mit Standard-BlackScreen-Fallback
    private IEnumerator TeleportToPoint(TeleportPoint tp)
    {
        if (tp.targetPosition == null) yield break;

        // Verwende den individuellen BlackScreen, falls vorhanden – sonst den globalen
        GameObject activeBlackScreen = tp.blackScreen != null ? tp.blackScreen : blackScreen;

        if (activeBlackScreen == null)
        {
            // Kein BlackScreen vorhanden → direkt teleportieren
            transform.position = tp.targetPosition.position;
            yield break;
        }

        // CanvasGroup holen oder hinzufügen
        activeBlackScreen.SetActive(true);
        CanvasGroup cg = activeBlackScreen.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = activeBlackScreen.AddComponent<CanvasGroup>();

        float duration = 0.6f;
        float time = 0f;

        // Fade-In
        while (time < duration)
        {
            cg.alpha = Mathf.Lerp(0f, 1f, time / duration);
            time += Time.unscaledDeltaTime;
            yield return null;
        }
        cg.alpha = 1f;

        // kurze Pause
        yield return new WaitForSecondsRealtime(0.3f);

        // Teleportieren
        transform.position = tp.targetPosition.position;

        // kurze Pause nach Teleport
        yield return new WaitForSecondsRealtime(0.3f);

        // Fade-Out
        time = 0f;
        while (time < duration)
        {
            cg.alpha = Mathf.Lerp(1f, 0f, time / duration);
            time += Time.unscaledDeltaTime;
            yield return null;
        }
        cg.alpha = 0f;
        activeBlackScreen.SetActive(false);
    }
    private bool TryCloseOpenPanel()
    {
        foreach (var panel in closeablePanels)
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
                Time.timeScale = 1f;
                return true; // hat ein Panel geschlossen
            }
        }

        return false; // kein Panel offen
    }
}
