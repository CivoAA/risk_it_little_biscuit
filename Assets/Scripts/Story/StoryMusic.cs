using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Musik der Intro-Cutscene (Resources/Music/story_intro, aus intro_musik.py
/// im Artwork-Ordner) und der Uebergang zurueck zur Hub-Musik:
///
///   Begin   Hub-Musik blendet in 1 s aus, das Intro-Stueck in 1,5 s ein.
///   End     Intro blendet in 2,5 s aus; nach der halben Zeit beginnt die
///           Hub-Musik von vorn und blendet in 3 s auf ihre alte Lautstaerke.
///   Hold    Kommt das Intro gleich beim ersten Hub-Besuch, schaltet der
///           StoryDirector die Hub-Musik schon beim Laden stumm (mute) - so
///           blendet der SceneFader sie gar nicht erst ein. End gibt sie frei.
///
/// Die Hub-Musik ist die AudioSource auf "Hub_Music" in der Hub-Szene. Die
/// Intro-Quelle haengt in derselben Mixer-Gruppe, damit der Musik-Regler gilt.
/// Laeuft auf unskalierter Zeit, die Coroutinen haengen am StoryDirector.
/// </summary>
public static class StoryMusic
{
    private const string HubMusicName = "Hub_Music";

    private static AudioSource intro, hub;
    private static float hubVolume = 1f;
    private static Coroutine routine;
    private static bool held;

    /// <summary>Hub-Musik stumm halten, bis <see cref="EndIntro"/> sie zurueckholt.</summary>
    public static void HoldHubMusic()
    {
        AudioSource src = FindHubMusic();
        if (src == null) return;
        hub = src;
        if (src.volume > 0.001f) hubVolume = src.volume;
        src.mute = true;
        held = true;
    }

    private static void ReleaseHubMusic()
    {
        if (!held) return;
        held = false;
        if (hub == null) return;
        hub.volume = hubVolume;
        hub.mute = false;
    }

    public static void BeginIntro()
    {
        MonoBehaviour host = StoryDirector.Instance;
        if (host == null) return;
        AudioClip clip = Resources.Load<AudioClip>("Music/story_intro");
        if (clip == null)
        {
            Debug.LogWarning("[Story] Resources/Music/story_intro fehlt - Intro ohne eigene Musik.");
            ReleaseHubMusic();
            return;
        }

        AudioSource found = FindHubMusic();
        if (found != null) hub = found;
        if (hub != null && !held && hub.volume > 0.001f) hubVolume = hub.volume;

        if (intro == null)
        {
            var go = new GameObject("StoryIntroMusic");
            Object.DontDestroyOnLoad(go);
            intro = go.AddComponent<AudioSource>();
            intro.playOnAwake = false;
            intro.loop = false;
            intro.spatialBlend = 0f;
            intro.ignoreListenerPause = true;
        }
        AudioMixerGroup group = hub != null ? hub.outputAudioMixerGroup : null;
        if (group == null && AudioController.Instance != null && AudioController.Instance.audioSources != null &&
            AudioController.Instance.audioSources.Length > 0 && AudioController.Instance.audioSources[0] != null)
            group = AudioController.Instance.audioSources[0].outputAudioMixerGroup;
        intro.outputAudioMixerGroup = group;
        intro.clip = clip;
        intro.volume = 0f;
        intro.Play();

        if (routine != null) host.StopCoroutine(routine);
        routine = host.StartCoroutine(FadeIn());
    }

    public static void EndIntro()
    {
        MonoBehaviour host = StoryDirector.Instance;
        if (host == null)
        {
            ReleaseHubMusic();
            return;
        }
        if (routine != null) host.StopCoroutine(routine);
        routine = host.StartCoroutine(FadeBack());
    }

    private static AudioSource FindHubMusic()
    {
        GameObject go = GameObject.Find(HubMusicName);
        return go != null ? go.GetComponent<AudioSource>() : null;
    }

    private static IEnumerator FadeIn()
    {
        float hubFrom = hub != null ? hub.volume : 0f;
        for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
        {
            if (hub != null) hub.volume = hubFrom * (1f - Mathf.Clamp01(t / 1f));
            intro.volume = Smooth(t / 1.5f);
            yield return null;
        }
        if (hub != null) hub.volume = 0f;
        intro.volume = 1f;
        routine = null;
    }

    private static IEnumerator FadeBack()
    {
        const float Out = 2.5f, In = 3f, HubStart = 1.25f;
        // Ist das Intro-Stueck schon zu Ende, kommt nur die Hub-Musik zurueck.
        float from = intro != null && intro.isPlaying ? intro.volume : 0f;
        bool hubStarted = false;
        for (float t = 0f; t < HubStart + In; t += Time.unscaledDeltaTime)
        {
            if (intro != null) intro.volume = from * (1f - Smooth(t / Out));
            if (t >= HubStart && hub != null)
            {
                if (!hubStarted)
                {
                    hubStarted = true;
                    held = false;
                    hub.mute = false;
                    hub.volume = 0f;
                    hub.Stop();
                    hub.Play();
                }
                hub.volume = hubVolume * Smooth((t - HubStart) / In);
            }
            yield return null;
        }
        if (intro != null) intro.Stop();
        if (hub != null) hub.volume = hubVolume;
        routine = null;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
