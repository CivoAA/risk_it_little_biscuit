using System.Collections;
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

    [Header("Lauf-Musik (audioSources[1])")]
    [Tooltip("Stille nach dem Laden des Laufs, bevor die Lauf-Musik einsetzt - so kommt erst das Bild, dann die Musik.")]
    public float runMusicDelay = 1.2f;
    [Tooltip("So lange blendet die Lauf-Musik ein.")]
    public float runMusicFadeIn = 2.5f;
    [Tooltip("So lange klingt die Lauf-Musik aus, wenn der Lauf ohne Blende endet.")]
    public float musicFadeOut = 0.8f;
    [Tooltip("So lange blendet die Hub-Musik nach einem Lauf ohne Blende wieder ein.")]
    public float returnMusicFadeIn = 1.5f;

    // Das Stueck aus der Szene ist die Rueckfallebene fuer Karten ohne eigene
    // Musik (MapDefinition.Music).
    private AudioClip defaultRunClip;
    private float runMusicVolume = 1f;
    private bool runMusicActive;
    private Coroutine runMusicRoutine;
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

        if (audioSources != null && audioSources.Length >= 2 && audioSources[1] != null)
        {
            defaultRunClip = audioSources[1].clip;
            runMusicVolume = audioSources[1].volume;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
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

    /// <summary>
    /// Sicherheitsnetz: Geht es aus dem Lauf zurueck in den schon geladenen
    /// Hub, wird nichts geladen - SwitchMusic kaeme nie, und die Lauf-Musik
    /// liefe neben der Hub-Musik weiter. Regulaer erledigt das schon
    /// <see cref="ReturnFromRun"/>.
    /// </summary>
    private void OnSceneUnloaded(Scene scene)
    {
        if (IsRunScene(scene.name)) StopRunMusic(true);
    }

    private static bool IsRunScene(string sceneName)
    {
        // Im neuen System heisst die Lauf-Szene GameCore; "Game" ist die alte.
        return sceneName == "Game" || sceneName == MapSceneSystem.CoreScene;
    }

    public void SwitchMusic(string sceneName)
    {
        // nicht jede Szene hat die beiden Musikquellen verdrahtet (z.B. Game/World Map)
        if (audioSources == null || audioSources.Length < 2) return;

        if (sceneName == "Main Menu" || sceneName == "World Map")
        {
            // 0 = MainMenu-Musik AN - und zwar von vorn. Die Quelle läuft per
            // PlayOnAwake durchgehend und ist sonst nur stumm; ohne Neustart
            // setzt die Musik beim Zurückkommen mitten im Stück ein.
            // (SwitchMusic kommt pro Wechsel zweimal - nur beim Umschalten neu starten.)
            if (audioSources[0].mute)
            {
                audioSources[0].mute = false;
                audioSources[0].Stop();
                audioSources[0].Play();
            }
            // 1 = Lauf-Musik AUS - ausblenden macht hier der SceneFader
            StopRunMusic(false);
        }
        // Lauf - die Map-Szene selbst bringt keine Musik mit; welches Stueck
        // laeuft, sagt ihre MapDefinition (siehe RunMusicIn).
        else if (IsRunScene(sceneName))
        {
            // 0 = MainMenu-Musik AUS
            audioSources[0].mute = true;
            // 1 = Lauf-Musik AN - nach kurzer Pause, eingeblendet
            StartRunMusic();
        }
        // Hub bringt seine eigene Musik mit (Hub_Music in der Szene) -> beide hier aus
        else if (sceneName == "hub")
        {
            audioSources[0].mute = true;
            StopRunMusic(true);
        }
    }

    // ------------------------------------------------------------ Lauf-Musik

    /// <summary>
    /// Startet die Lauf-Musik nicht sofort: erst eine kurze Stille, in der der
    /// Startsound und die ausklingende Hub-Musik Platz haben, dann blendet das
    /// Stueck der Karte von vorn ein. Kommt pro Wechsel mehrfach - nur der
    /// erste Aufruf zaehlt.
    /// </summary>
    private void StartRunMusic()
    {
        if (runMusicActive) return;
        runMusicActive = true;

        AudioSource src = audioSources[1];
        src.Stop();
        src.mute = false;
        src.volume = 0f;

        if (runMusicRoutine != null) StopCoroutine(runMusicRoutine);
        runMusicRoutine = StartCoroutine(RunMusicIn(src));
    }

    private IEnumerator RunMusicIn(AudioSource src)
    {
        // Echtzeit: der Lauf kann in der Zeit schon pausiert sein (timeScale 0).
        yield return new WaitForSecondsRealtime(runMusicDelay);

        // Erst jetzt nachsehen - bis hierher ist die Map-Szene sicher geladen
        // und hat sich als MapDefinition.Active eingetragen.
        AudioClip clip = defaultRunClip;
        MapDefinition map = MapDefinition.Active;
        if (map != null && map.Music != null) clip = map.Music;

        src.clip = clip;
        src.volume = 0f;
        src.Play();

        for (float t = 0f; t < runMusicFadeIn; t += Time.unscaledDeltaTime)
        {
            src.volume = runMusicVolume * Smooth(t / runMusicFadeIn);
            yield return null;
        }
        src.volume = runMusicVolume;
        runMusicRoutine = null;
    }

    /// <summary>Lauf-Musik aus - mit <paramref name="fade"/> klingt sie kurz aus statt abzureissen.</summary>
    private void StopRunMusic(bool fade)
    {
        if (audioSources == null || audioSources.Length < 2 || audioSources[1] == null) return;

        AudioSource src = audioSources[1];
        bool audible = runMusicActive && src.isPlaying && !src.mute && src.volume > 0f;

        runMusicActive = false;
        if (runMusicRoutine != null)
        {
            StopCoroutine(runMusicRoutine);
            runMusicRoutine = null;
        }

        if (fade && audible) FadeOutCopy(src);

        src.Stop();
        src.mute = true;
        src.volume = runMusicVolume;
    }

    /// <summary>
    /// Nach dem Lauf zurueck in eine schon geladene Szene (Hub): die Lauf-Musik
    /// klingt aus, die Musik der Szene - die beim Wiedereinschalten per
    /// PlayOnAwake von vorn anfaengt - blendet ein. Direkt nach dem
    /// Einschalten aufrufen, im selben Frame. Laeuft der Rueckweg hinter dem
    /// <see cref="SceneFader"/>, blendet der die Musik - dann hier nur abschalten.
    /// </summary>
    public void ReturnFromRun(Scene target)
    {
        StopRunMusic(true);
        if (SceneFader.IsFading) return;

        foreach (AudioSource src in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
        {
            if (src.gameObject.scene == target && src.isPlaying && src.loop && !src.mute)
                StartCoroutine(FadeIn(src, src.volume, returnMusicFadeIn));
        }
    }

    // Eine Kopie spielt an derselben Stelle weiter und klingt aus - so kann
    // die Quelle selbst sofort gestoppt und fuer das naechste Stueck frei sein.
    private void FadeOutCopy(AudioSource src)
    {
        if (src.clip == null) return;

        GameObject go = new GameObject("MusicFadeOut (" + src.clip.name + ")");
        go.transform.SetParent(transform, false);
        AudioSource copy = go.AddComponent<AudioSource>();
        copy.clip = src.clip;
        copy.outputAudioMixerGroup = src.outputAudioMixerGroup;
        copy.volume = src.volume;
        copy.pitch = src.pitch;
        copy.priority = src.priority;
        copy.spatialBlend = 0f;
        copy.loop = false;
        copy.time = src.time;
        copy.Play();
        StartCoroutine(FadeOutAndDestroy(copy, musicFadeOut));
    }

    private static IEnumerator FadeOutAndDestroy(AudioSource src, float duration)
    {
        float from = src.volume;
        for (float t = 0f; t < duration && src != null; t += Time.unscaledDeltaTime)
        {
            src.volume = from * (1f - Smooth(t / duration));
            yield return null;
        }
        if (src != null) Destroy(src.gameObject);
    }

    private static IEnumerator FadeIn(AudioSource src, float to, float duration)
    {
        src.volume = 0f;
        for (float t = 0f; t < duration && src != null; t += Time.unscaledDeltaTime)
        {
            src.volume = to * Smooth(t / duration);
            yield return null;
        }
        if (src != null) src.volume = to;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
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