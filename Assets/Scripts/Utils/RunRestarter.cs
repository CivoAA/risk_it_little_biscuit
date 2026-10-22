using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "NEUSTART" im Abschlussbildschirm: derselbe Lauf noch einmal, ohne Umweg
/// ueber den Hub. Laeuft hinter der Blende (<see cref="GameManager.RestartRun"/>):
///
///   1. GameCore und Map-Szene stilllegen und entladen
///   2. warten, bis beide wirklich weg sind - die Map holt GameCore sonst
///      nicht neu (MapBootstrap sieht die alte noch geladen)
///   3. dieselbe Map additiv laden; MapBootstrap legt GameCore dazu
///
/// Hub bzw. World Map bleiben dabei stillgelegt, Shop-Stand (Shop.CaptureRun),
/// Charakter, Modus und Schwierigkeit bleiben, wie sie beim Start waren.
/// Haengt an einem eigenen Objekt, weil der GameManager mit GameCore entladen wird.
/// </summary>
public class RunRestarter : MonoBehaviour
{
    /// <summary>Die neue Map ist angestossen - die Blende darf warten, bis GameCore steht.</summary>
    public static bool Loading { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Loading = false;
    }

    /// <summary>Fuer <see cref="SceneFader.Switch"/>: fertig, sobald der neue Lauf geladen ist.</summary>
    public static bool Done =>
        !Loading && SceneManager.GetSceneByName(MapSceneSystem.CoreScene).isLoaded;

    public static void Begin(string coreScene, string mapScene)
    {
        Loading = true;
        var go = new GameObject("RunRestarter");
        DontDestroyOnLoad(go);
        go.AddComponent<RunRestarter>().StartCoroutine(Run(go, coreScene, mapScene));
    }

    private static IEnumerator Run(GameObject self, string coreScene, string mapScene)
    {
        Deactivate(coreScene);
        Deactivate(mapScene);

        AsyncOperation core = Unload(coreScene);
        AsyncOperation map = Unload(mapScene);
        MapSceneSystem.ClearRunMapScene(mapScene);
        while ((core != null && !core.isDone) || (map != null && !map.isDone)) yield return null;

        // Wie HubLevelSelectUI.Launch: Stand vor dem Lauf merken (Abschluss
        // zeigt nur Neues), Skilltree des Lauf-Charakters.
        SessionProgressTracker.Instance?.SnapshotBeforeGame();
        Skills.SetActiveTreeForCharacter(Shop.RunSkinIndex);

        AsyncOperation load = SceneManager.LoadSceneAsync(mapScene, LoadSceneMode.Additive);
        while (load != null && !load.isDone) yield return null;

        Loading = false;
        Destroy(self);
    }

    private static void Deactivate(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false);
    }

    private static AsyncOperation Unload(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded ? SceneManager.UnloadSceneAsync(scene) : null;
    }
}
