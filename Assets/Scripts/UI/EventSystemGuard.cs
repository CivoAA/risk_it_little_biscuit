using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Sorgt dafür, dass immer genau ein EventSystem läuft. Panels, die per Code
/// gebaut werden (Optionen, Unlocks, Hub-Shop ...), rufen <see cref="Ensure"/>;
/// nach jedem Szenenwechsel werden doppelte EventSystems wieder entfernt -
/// das der neu geladenen Szene gewinnt, weil es dort bewusst eingestellt ist.
/// </summary>
public static class EventSystemGuard
{
    /// <summary>Legt eins an (oder weckt ein deaktiviertes), falls gerade keins aktiv ist.</summary>
    public static void Ensure()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;

        EventSystem[] all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EventSystem es in all)
        {
            if (es.isActiveAndEnabled) return;
        }

        if (all.Length > 0)
        {
            all[0].enabled = true;
            all[0].gameObject.SetActive(true);
            return;
        }

        // Bewusst ohne DontDestroyOnLoad: es lebt mit der aktiven Szene und
        // kollidiert so nicht mit dem EventSystem der nächsten Szene.
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EventSystem[] all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (all.Length <= 1) return;

        EventSystem keep = null;
        foreach (EventSystem es in all)
        {
            if (es.gameObject.scene == scene) { keep = es; break; }
        }
        if (keep == null) keep = EventSystem.current != null ? EventSystem.current : all[0];

        foreach (EventSystem es in all)
        {
            if (es == keep) continue;
            Remove(es);
        }
    }

    private static void Remove(EventSystem es)
    {
        GameObject go = es.gameObject;
        if (HoldsOnlyEventSystem(go))
        {
            // Erst deaktivieren: Destroy greift erst am Frame-Ende, und bis dahin
            // würde EventSystem.Update sonst noch einmal warnen.
            go.SetActive(false);
            Object.Destroy(go);
        }
        else
        {
            foreach (BaseInputModule module in go.GetComponents<BaseInputModule>()) module.enabled = false;
            es.enabled = false;
        }
    }

    private static bool HoldsOnlyEventSystem(GameObject go)
    {
        if (go.transform.childCount > 0) return false;
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c is Transform || c is EventSystem || c is BaseInputModule) continue;
            return false;
        }
        return true;
    }
}
