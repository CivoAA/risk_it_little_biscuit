using UnityEngine;
using UnityEngine.EventSystems;

public class GlobalEventSystem : MonoBehaviour
{
    private static GlobalEventSystem instance;

    void Awake()
    {
        // sicherstellen, dass nur eins existiert
        if (instance != null && instance != this)
        {
            // Sofort aus, nicht erst am Frame-Ende weg: sonst sieht der
            // EventSystemGuard beim sceneLoaded noch zwei aktive, behaelt das
            // dieser Szene (also dieses hier) und schaltet das bleibende aus -
            // zurueck im Hauptmenue ginge dann kein Knopf mehr.
            gameObject.SetActive(false);
            Destroy(gameObject);
            instance.Revive();
            return;
        }
        instance = this;

        // nicht zerstören beim Szenenwechsel
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Falls das bleibende EventSystem unterwegs abgeschaltet wurde: wieder an.</summary>
    private void Revive()
    {
        gameObject.SetActive(true);
        foreach (BaseInputModule module in GetComponents<BaseInputModule>()) module.enabled = true;
        EventSystem es = GetComponent<EventSystem>();
        if (es != null) es.enabled = true;
    }
}
