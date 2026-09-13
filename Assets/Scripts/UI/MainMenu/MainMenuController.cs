using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Steuert das Hauptmenü. Die fünf Knöpfe hängen jeweils an einer der öffentlichen
/// Methoden hier drunter - im Inspector unter OnClick auswählen.
///
/// Spielen      -> lädt die Levelauswahl
/// Endless      -> lädt die World Map
/// Optionen     -> öffnet das Options-Panel (baut sich selbst, siehe OptionsPanel)
/// Achievements -> noch ohne Funktion
/// Beenden      -> beendet das Spiel
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Szenen")]
    [SerializeField, Tooltip("Szene, die \"Spielen\" lädt. Muss in den Build Settings stehen.")]
    private string levelSelectSceneName = "levelauswahl";

    [SerializeField, Tooltip("Szene, die \"Endless\" lädt. Muss in den Build Settings stehen.")]
    private string endlessSceneName = "World Map";

    private bool isLoading;

    // ---------- Spielen ----------

    public void PlayStory()
    {
        GameSession.SelectedMode = GameMode.Story;
        LoadScene(levelSelectSceneName);
    }

    public void PlayEndless()
    {
        GameSession.SelectedMode = GameMode.Endless;
        LoadScene(endlessSceneName);
    }

    // ---------- Overlays ----------

    public void OpenOptions()
    {
        PlayClick();
        OptionsPanel.Open();
    }

    /// <summary>Achievements - bewusst noch ohne Funktion.</summary>
    public void OpenAchievements()
    {
        PlayClick();
        Debug.Log("[MainMenu] Achievements sind noch nicht angebunden.");
    }

    // ---------- Beenden ----------

    public void QuitGame()
    {
        PlayClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Helpers ----------

    private void LoadScene(string sceneName)
    {
        if (isLoading) return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[MainMenu] Kein Szenenname gesetzt.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MainMenu] Szene \"{sceneName}\" ist nicht in den Build Settings.");
            return;
        }

        isLoading = true;
        PlayClick();

        // Single-Modus statt additiv: LoadScene ist erst am Frame-Ende fertig, das
        // Entladen der alten Szene im selben Frame lehnt Unity deshalb ab ("Unloading
        // the last loaded scene is not supported") - das Menue bliebe sichtbar liegen.
        // Single ersetzt alles auf einmal; die DontDestroyOnLoad-Manager
        // (AudioController, SteamManager, ...) ueberleben das unveraendert.
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void PlayClick()
    {
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();
    }
}
