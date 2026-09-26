using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Weicher Szenenwechsel: Bild blendet ab und die laufende Musik aus, die neue
/// Szene lädt hinter der Blende, dann kommt das Bild zurück und die Musik der
/// neuen Szene blendet ein.
///
///   SceneFader.Load("hub");
///   SceneFader.Load("Main Menu", () => Time.timeScale = 1f);   // läuft direkt vor dem Laden
///
/// Für Wechsel, die keine Szene mit Single laden (Levelstart aus dem Hub,
/// Rückweg in den noch geladenen Hub), gibt es <see cref="Switch"/>: der
/// Wechsel selbst läuft bei voller Blende, die Blende wartet, bis er fertig ist.
///
/// Baut sich selbst (eigenes Overlay-Canvas über allem), überlebt den
/// Szenenwechsel und räumt sich danach wieder weg. Während der Blende fängt das
/// Overlay alle Klicks ab.
///
/// "Musik" heißt hier: jede AudioSource, die gerade loopt und nicht stumm ist.
/// Vor dem Laden werden alle davon ausgeblendet, nach dem Laden alle, die dann
/// hörbar sind, eingeblendet - egal ob sie aus der neuen Szene kommen (Hub_Music)
/// oder vom AudioController, der beim Laden per SwitchMusic umschaltet
/// (Menümusik auf dem Rückweg ins Hauptmenü).
/// </summary>
public class SceneFader : MonoBehaviour
{
    private const float FadeOutTime = 0.6f;
    private const float FadeInTime = 0.6f;
    private const float MusicInTime = 1.5f;
    private static readonly Color BlendColor = new Color32(0x10, 0x0b, 0x0a, 0xff);

    private static SceneFader instance;

    public static bool IsFading => instance != null;

    private Image blend;

    /// <summary>Wie lange die Blende höchstens auf einen Wechsel wartet, bevor sie trotzdem aufgeht.</summary>
    private const float SwitchTimeout = 5f;

    public static void Load(string sceneName, System.Action beforeLoad = null)
    {
        AsyncOperation load = null;
        Switch(() =>
               {
                   beforeLoad?.Invoke();
                   load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
               },
               () => load == null || load.isDone);
    }

    /// <summary>
    /// Beliebiger Wechsel hinter der Blende. <paramref name="doSwitch"/> läuft,
    /// sobald das Bild ganz zu ist; <paramref name="isDone"/> sagt, wann der
    /// Wechsel steht (null = sofort). Gibt false zurück, wenn schon eine Blende
    /// läuft - dann passiert nichts.
    /// </summary>
    public static bool Switch(System.Action doSwitch, System.Func<bool> isDone = null)
    {
        if (instance != null) return false;

        GameObject go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneFader>();
        instance.Build();
        instance.StartCoroutine(instance.Run(doSwitch, isDone));
        return true;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject img = new GameObject("Blend", typeof(RectTransform));
        img.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        blend = img.AddComponent<Image>();
        blend.color = new Color(BlendColor.r, BlendColor.g, BlendColor.b, 0f);
        blend.raycastTarget = true;
    }

    private IEnumerator Run(System.Action doSwitch, System.Func<bool> isDone)
    {
        // ---------- Abblenden + Musik aus ----------
        List<(AudioSource source, float volume)> oldMusic = FindMusic();

        for (float t = 0f; t < FadeOutTime; t += Time.unscaledDeltaTime)
        {
            float k = Smooth(t / FadeOutTime);
            SetBlend(k);
            foreach (var m in oldMusic)
                if (m.source != null) m.source.volume = m.volume * (1f - k);
            yield return null;
        }
        SetBlend(1f);

        // Während des Ladens stumm: PlayOnAwake der neuen Szene würde sonst einen
        // Augenblick in voller Lautstärke durchrutschen, bevor sie runtergeregelt ist.
        float listenerVolume = AudioListener.volume;
        AudioListener.volume = 0f;

        doSwitch?.Invoke();
        float waited = 0f;
        while (isDone != null && !isDone() && waited < SwitchTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        // Ein Frame Luft: additiv geladene Szenen sind erst jetzt ganz wach,
        // ihre PlayOnAwake-Musik läuft und wird unten mit eingeblendet.
        yield return null;

        // Alte Quellen, die den Wechsel überlebt haben (AudioController), bekommen
        // ihre Lautstärke zurück; ob sie hörbar sind, entscheidet ab hier SwitchMusic.
        foreach (var m in oldMusic)
            if (m.source != null) m.source.volume = m.volume;

        // Alles, was jetzt hörbar ist, startet bei null und blendet ein.
        List<(AudioSource source, float volume)> newMusic = FindMusic();
        foreach (var m in newMusic) m.source.volume = 0f;

        AudioListener.volume = listenerVolume;
        yield return null;

        // ---------- Aufblenden + neue Musik rein ----------
        float total = Mathf.Max(FadeInTime, MusicInTime);
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            SetBlend(1f - Smooth(Mathf.Clamp01(t / FadeInTime)));
            float k = Smooth(Mathf.Clamp01(t / MusicInTime));
            foreach (var m in newMusic)
                if (m.source != null) m.source.volume = m.volume * k;
            yield return null;
        }
        foreach (var m in newMusic)
            if (m.source != null) m.source.volume = m.volume;

        instance = null;
        Destroy(gameObject);
    }

    private static List<(AudioSource, float)> FindMusic()
    {
        var list = new List<(AudioSource, float)>();
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            if (source.isPlaying && source.loop && !source.mute)
                list.Add((source, source.volume));
        return list;
    }

    private void SetBlend(float alpha)
    {
        Color c = blend.color;
        c.a = alpha;
        blend.color = c;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
