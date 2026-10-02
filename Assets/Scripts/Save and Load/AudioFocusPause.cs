using UnityEngine;

/// <summary>
/// Haelt Musik und alle Sounds an, solange das Spiel nicht im Vordergrund ist
/// (Alt+Tab, anderes Fenster angeklickt), und laesst sie beim Zurueckkommen
/// an derselben Stelle weiterlaufen. Haengt an keinem Objekt, meldet sich beim
/// Start selbst an.
///
/// Nur im fertigen Spiel: im Editor wuerde schon ein Klick in den Inspector
/// den Ton anhalten.
/// </summary>
public static class AudioFocusPause
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if !UNITY_EDITOR
        Application.focusChanged -= OnFocusChanged;
        Application.focusChanged += OnFocusChanged;
#endif
    }

    private static void OnFocusChanged(bool hasFocus)
    {
        AudioListener.pause = !hasFocus;
    }
}
