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
            Destroy(gameObject);
            return;
        }
        instance = this;

        // nicht zerstören beim Szenenwechsel
        DontDestroyOnLoad(gameObject);
    }
}