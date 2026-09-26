using UnityEngine;

/// <summary>
/// Steuert das Hauptmenü. Die fünf Knöpfe hängen jeweils an einer der öffentlichen
/// Methoden hier drunter - im Inspector unter OnClick auswählen.
///
/// Spielen      -> lädt den Hub (dort steht die Levelauswahl)
/// Endless      -> lädt denselben Hub, nur mit vorgewähltem Endless-Haken
/// Optionen     -> öffnet das Options-Panel (baut sich selbst, siehe OptionsPanel)
/// Achievements -> öffnet die Achievement-Liste (baut sich selbst, siehe AchievementPanel)
/// Beenden      -> beendet das Spiel
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Szenen")]
    [SerializeField, Tooltip("Szene, die \"Spielen\" lädt. Muss in den Build Settings stehen.")]
    private string levelSelectSceneName = "hub";

    [SerializeField, Tooltip("Szene, die \"Endless\" lädt. Muss in den Build Settings stehen. " +
                             "Derselbe Hub wie bei \"Spielen\" - der Unterschied steckt nur in GameSession.SelectedMode.")]
    private string endlessSceneName = "hub";

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

    /// <summary>
    /// Achievements. Das Panel baut sich selbst - der gleiche Aufruf funktioniert
    /// aus jeder Szene heraus (Hub, World Map, im Spiel).
    /// </summary>
    public void OpenAchievements()
    {
        PlayClick();
        AchievementPanel.Open();
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

        // Blende statt hartem Schnitt: Bild und Menümusik blenden aus, die Szene lädt
        // dahinter (Single-Modus, siehe SceneFader), dann kommt die neue Musik weich rein.
        // Die DontDestroyOnLoad-Manager (AudioController, SteamManager, ...) überleben das.
        SceneFader.Load(sceneName);
    }

    private void PlayClick()
    {
        if (AudioController.Instance != null && AudioController.Instance.MenuClick != null)
            AudioController.Instance.MenuClick.Play();
    }
}
