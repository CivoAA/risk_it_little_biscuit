using UnityEngine;
using System.IO;
using System.Collections;
using UnityEngine.SceneManagement;

[System.Serializable]
public class AudioSettingsData
{
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float effectsVolume = 1f;
}

public class AudioSettingsManager : MonoBehaviour
{
    private static AudioSettingsManager instance;
    private static bool quitting;

    /// <summary>
    /// Der Ton-Speicher. In der Szene liegt er nur im Hauptmenü (auf dem
    /// "Audio Controller"), darum legt er sich hier selbst an, wenn keiner da
    /// ist - sonst laufen alle Lautstärke-Aufrufe aus dem Hub, aus GameCore
    /// oder aus einer direkt gestarteten Szene still ins Leere, und das
    /// Optionen-Fenster reagiert auf keinen Klick.
    /// </summary>
    public static AudioSettingsManager Instance
    {
        get
        {
            if (instance != null) return instance;
            if (quitting) return null;

            instance = FindAnyObjectByType<AudioSettingsManager>();
            if (instance != null) return instance;

            GameObject go = new GameObject("AudioSettingsManager");
            instance = go.AddComponent<AudioSettingsManager>();
            return instance;
        }
    }

    public AudioSettingsData currentSettings = new AudioSettingsData();
    private string savePath;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // Nur die doppelte Komponente abräumen: das Objekt gehört dem
            // AudioController, der hängt hier nur mit drauf.
            Destroy(this);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Application.persistentDataPath + "/audioSettings.json";

        // JSON laden
        LoadSettings();

        // beim Start und bei jedem Szenenwechsel anwenden
        SceneManager.sceneLoaded += OnSceneLoaded;

        // ganz simpel: nach 2 Frames sicher in den Mixer schreiben
        StartCoroutine(ApplySettingsDelayed());
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnApplicationQuit() => quitting = true;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySettingsNow();
        UpdateAllVolumeSliders();
    }

    public void UpdateAllVolumeSliders()
    {
        foreach (var slider in FindObjectsByType<VolumeSlider>(FindObjectsSortMode.None))
        {
            slider.RefreshFromSettings(); // ruft gleich definierte Methode unten auf
        }
    }

    private IEnumerator ApplySettingsDelayed()
    {
        // 2 Frames warten, damit AudioController garantiert existiert
        yield return null;
        yield return null;

        if (AudioController.Instance != null)
        {
            AudioController.Instance.SetMasterVolume(currentSettings.masterVolume);
            AudioController.Instance.SetMusicVolume(currentSettings.musicVolume);
            AudioController.Instance.SetEffectsVolume(currentSettings.effectsVolume);
        }
        else
        {
            Debug.LogWarning("Kein AudioController gefunden – konnte AudioSettings nicht anwenden!");
        }
    }

    public void SaveSettings()
    {
        // Wer den Manager selbst angelegt hat, kommt hier vor Awake vorbei.
        if (string.IsNullOrEmpty(savePath))
            savePath = Application.persistentDataPath + "/audioSettings.json";

        try
        {
            string json = JsonUtility.ToJson(currentSettings, true);
            File.WriteAllText(savePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ AudioSettings-Speichern fehlgeschlagen: " + e.Message);
        }
    }

    public void LoadSettings()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                currentSettings = JsonUtility.FromJson<AudioSettingsData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("⚠️ AudioSettings konnten nicht gelesen werden: " + e.Message);
                currentSettings = null;
            }

            if (currentSettings == null)
            {
                currentSettings = new AudioSettingsData();
                SaveSettings();
            }
        }
        else
        {
            SaveSettings();
            Debug.Log("✨ Neue AudioSettings-Datei erstellt!");
        }
    }

    // diese Methoden rufst du aus deinem VolumeSlider auf:
    public void SetMasterVolume(float value)
    {
        currentSettings.masterVolume = value;
        if (AudioController.Instance != null)
            AudioController.Instance.SetMasterVolume(value);
        SaveSettings();
    }

    public void SetMusicVolume(float value)
    {
        currentSettings.musicVolume = value;
        if (AudioController.Instance != null)
            AudioController.Instance.SetMusicVolume(value);
        SaveSettings();
    }

    public void SetEffectsVolume(float value)
    {
        currentSettings.effectsVolume = value;
        if (AudioController.Instance != null)
            AudioController.Instance.SetEffectsVolume(value);
        SaveSettings();
    }
    public void ApplySettingsNow()
    {
        if (AudioController.Instance != null)
        {
            AudioController.Instance.SetMasterVolume(currentSettings.masterVolume);
            AudioController.Instance.SetMusicVolume(currentSettings.musicVolume);
            AudioController.Instance.SetEffectsVolume(currentSettings.effectsVolume);
        }
    }
}
