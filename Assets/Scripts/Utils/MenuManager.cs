using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;
    public GameObject pauseCanvas;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0; 
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        { 
            PauseCanvasClose();
        }
    }
    public void PauseCanvasClose()
    {
        // Nach einem Szenenwechsel zeigt die Referenz ins Leere - sonst knallt es hier jedes Mal bei Escape.
        if (pauseCanvas == null) return;
        pauseCanvas.SetActive(false);
        //Time.timeScale = 1f;
    }
    public void PauseCanvasOpen()
    {
        if (pauseCanvas == null) return;
        pauseCanvas.SetActive(true);
        AudioSettingsManager.Instance.UpdateAllVolumeSliders();
        //Time.timeScale = 0f;
    }

    public void NewGame()
    {
        // Start und Ziel eines Laufs ist der Hub. Die World Map wird nicht mehr
        // angesteuert - sie laeuft nur noch, wenn man sie direkt startet.
        SceneManager.LoadScene(GameSession.HubScene, LoadSceneMode.Additive);
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.UnloadSceneAsync(currentScene);
    }
    public void QuitGame()
    {
        Application.Quit();
    }
    public void DeactivateScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject go in roots)
            {
                go.SetActive(false);
            }
        }
    }
    public void UnloadScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }

    public void ActivateScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject go in roots)
            {
                go.SetActive(true);
            }
        }
        else
        {
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
    }
}
