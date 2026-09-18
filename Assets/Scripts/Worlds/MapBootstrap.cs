using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holt GameCore dazu, falls es fehlt.
///
/// Damit ist eine Map-Szene fuer sich allein lauffaehig: aufmachen, Play
/// druecken, es laeuft - genau wie bei der Test-Szene. Und der Hub muss beim
/// Start eines Levels nur die Map-Szene laden, der Rest kommt von hier.
///
/// Laeuft frueh (DefaultExecutionOrder), damit das Nachladen angestossen ist,
/// bevor die Welt-Skripte ihr erstes Update sehen. Unity laedt additiv erst am
/// Ende des Frames - die Skripte in der Map kommen also einen Frame vor Player
/// und Managern dran. Sie halten das aus: WorldManager3x3 und die
/// RandomObjectSpawner pruefen PlayerController.Instance auf null.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class MapBootstrap : MonoBehaviour
{
    [Tooltip("Szene mit allem Gemeinsamen (Player, UI, Manager, Spawner).")]
    [SerializeField] private string coreScene = MapSceneSystem.CoreScene;

    void Awake()
    {
        string core = string.IsNullOrWhiteSpace(coreScene) ? MapSceneSystem.CoreScene : coreScene;

        Scene loaded = SceneManager.GetSceneByName(core);
        if (loaded.IsValid() && loaded.isLoaded) return;

        if (!Application.CanStreamedLevelBeLoaded(core))
        {
            Debug.LogError($"[MapBootstrap] Szene \"{core}\" steht nicht in den Build Settings - " +
                           "ohne sie bleibt die Map leer (kein Player, keine UI).");
            return;
        }

        SceneManager.LoadScene(core, LoadSceneMode.Additive);
    }
}
