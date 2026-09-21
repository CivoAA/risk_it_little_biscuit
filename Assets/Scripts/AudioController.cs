using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance;
    public AudioMixer mainMixer;

    [Range(0f, 1f)] public float masterVolume = 1f;   // default full volume
    [Range(0f, 1f)] public float musicVolume = 1f;    // für Slider-Startwert
    [Range(0f, 1f)] public float effectsVolume = 1f;  // für Slider-Startwert
    public AudioSource[] musicSources;   // hier deine Musikquellen reinziehen
    public AudioSource[] effectSources;

    [Header("Audio Sources (optional - keep your existing refs)")]
    public AudioSource pause;
    public AudioSource unpause;
    public AudioSource enemyDeath;
    public AudioSource selectUpgrade;
    public AudioSource areaWeaponSpawn;
    //public AudioSource areaWeaponDespawn;
    public AudioSource GameOver;
    public AudioSource JarJamBreakingGlass;
    public AudioSource Werfen;
    public AudioSource PlayerHit;
    public AudioSource PlayerHit2;
    public AudioSource MenuClick;
    public AudioSource BOBA;
    public AudioSource Speen;
    public AudioSource LevelUpSound;
    public AudioSource WinSound;
    public AudioSource LoseSound;
    public AudioSource NewBoba;
    public AudioSource ForkHit;
    public AudioSource EarthHit;
    public AudioSource Laser;
    public AudioSource[] audioSources;
    private static float ToDb(float v) => Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f;

    // cached list of AudioSources in scene (so background music not referenced explicitly still gets affected)
    private AudioSource[] cachedSceneSources;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // destroy the whole game object so references don't point to a destroyed component
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (AudioSettingsManager.Instance != null)
        {
            AudioSettingsManager.Instance.ApplySettingsNow();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void Start()
    {
        // cache audio sources that exist at Start
        cachedSceneSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SwitchMusic(scene.name);
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {   
        SwitchMusic(newScene.name);
    }

    public void SwitchMusic(string sceneName)
    {
        // nicht jede Szene hat die beiden Musikquellen verdrahtet (z.B. Game/World Map)
        if (audioSources == null || audioSources.Length < 2) return;

        if (sceneName == "Main Menu" || sceneName == "World Map")
        {
            // 0 = MainMenu-Musik AN
            audioSources[0].mute = false;
            // 1 = Game-Musik AUS
            audioSources[1].mute = true;
        }
        // Game - im neuen System heisst die Lauf-Szene GameCore; die Map-Szene
        // selbst bringt keine Musik mit und taucht hier nie auf.
        else if (sceneName == "Game" || sceneName == MapSceneSystem.CoreScene)
        {
            // 0 = MainMenu-Musik AUS
            audioSources[0].mute = true;
            // 1 = Game-Musik AN
            audioSources[1].mute = false;
        }
        // Hub bringt seine eigene Musik mit (Hub_Music in der Szene) -> beide hier aus
        else if (sceneName == "hub")
        {
            audioSources[0].mute = true;
            audioSources[1].mute = true;
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        SetMixerVolume("MasterVolume", value);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
        SetMixerVolume("MusicVolume", value);
    }

    public void SetEffectsVolume(float value)
    {
        effectsVolume = value;
        SetMixerVolume("EffectsVolume", value);
    }

    /// <summary>
    /// Schreibt einen Lautstaerkewert in den Mixer. Faellt der Mixer aus - in
    /// manchen Szenen ist am AudioController keiner eingetragen -, bleibt der
    /// Wert trotzdem gesetzt und gespeichert; hier darf nichts fliegen, sonst
    /// bricht der Klick im Optionen-Fenster mitten im Listener ab und die
    /// Leiste bewegt sich nicht mehr.
    /// </summary>
    private void SetMixerVolume(string parameter, float value)
    {
        if (mainMixer == null)
        {
            if (!missingMixerLogged)
            {
                missingMixerLogged = true;
                Debug.LogWarning($"[Audio] Am AudioController in Szene '{gameObject.scene.name}' " +
                                 "ist kein AudioMixer eingetragen - die Lautstaerke wird gemerkt, " +
                                 "aber nicht hoerbar. Master.mixer im Inspector zuweisen.");
            }
            return;
        }

        if (!mainMixer.SetFloat(parameter, ToDb(value)))
            Debug.LogWarning($"AudioMixer-Parameter '{parameter}' nicht gefunden.");
    }

    private bool missingMixerLogged;

    // keep your original methods (names preserved)
    public void PalySound(AudioSource sound, float? volume = null)
    {
        if (sound == null) return;
        sound.Stop();
        sound.volume = volume ?? masterVolume;
        sound.Play();
    }
    public void PalySoundTime(AudioSource sound, float? startTime = null)
    {
        if (sound == null) return;

        sound.Stop();

        // Lautstärke setzen
        sound.volume = masterVolume;

        // Wenn ein Startzeitpunkt angegeben ist → im Clip springen
        if (startTime.HasValue)
        {
            // Sicherheit: Clampen, damit kein Fehler bei zu großem Wert entsteht
            float t = Mathf.Clamp(startTime.Value, 0f, sound.clip != null ? sound.clip.length : 0f);
            sound.time = t;
        }

        sound.Play();
    }

    public void PalyModifiedSound(AudioSource sound)
    {
        if (sound == null) return;
        sound.pitch = Random.Range(0.7f, 1.3f);
        sound.Stop();
        sound.volume = masterVolume;
        sound.Play();
    }

    public void PalySoundMenu(AudioSource sound)
    {
        if (sound == null) return;
        sound.Stop();
        sound.volume = masterVolume;
        sound.Play();
    }

    public void StopSound(AudioSource sound)
    {
        if (sound == null) return;
        sound.Stop();
    }
}