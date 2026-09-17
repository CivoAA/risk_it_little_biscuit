using UnityEngine;

/// <summary>
/// Das einzige Objekt, das das Achievement-System in der Szene braucht - und das
/// legt es sich selbst an. Es steht in keiner Szene, taucht nicht in der Hierarchie
/// auf und muss nirgends eingetragen werden.
///
/// Aufgaben: gesammelte Fortschritts-Änderungen in Abständen wegschreiben, beim
/// Beenden und beim Wegklicken des Fensters sicher speichern, und den Abgleich
/// mit Steam nachholen, sobald Steam bereit ist (der SteamManager startet erst
/// mit der ersten Szene, also nach diesem Objekt).
///
/// Die Skillpunkte hängen mit dran: sie fallen im Spiel an denselben Stellen an
/// (jeder Miniboss-Kill, jedes Achievement) und werden deshalb im selben Takt
/// gebündelt geschrieben statt einzeln.
/// </summary>
[DisallowMultipleComponent]
public class AchievementRuntime : MonoBehaviour
{
    private const float SaveInterval = 5f;

    private static AchievementRuntime instance;

    private float saveTimer;
    private bool steamSynced;

    internal static void Ensure()
    {
        if (instance != null) return;

        GameObject go = new GameObject("~AchievementRuntime")
        {
            hideFlags = HideFlags.HideInHierarchy
        };

        instance = go.AddComponent<AchievementRuntime>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Update()
    {
        if (!steamSynced && SteamManager.Initialized)
        {
            steamSynced = true;
            AchievementSteamSync.Reconcile();
        }

        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer < SaveInterval) return;

        saveTimer = 0f;
        FlushAll();
        AchievementSteamSync.StoreIfPending();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) FlushAll();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) FlushAll();
    }

    private void OnApplicationQuit()
    {
        FlushAll();
        AchievementSteamSync.StoreIfPending();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        FlushAll();
    }

    private static void FlushAll()
    {
        Achievements.Flush();
        Skills.Flush();
    }
}
