using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Die Szene, in der alles landen soll, was waehrend eines Laufs entsteht:
/// Gegner, Loot, Effekte, Exp-Kugeln, die Objekte der Random-Spawner.
///
/// Warum das ueberhaupt jemanden kuemmert: Instantiate legt neue Objekte in der
/// AKTIVEN Szene ab, und die ist waehrend eines Laufs der Hub (er hat das Level
/// nur additiv dazugeladen). Wuerden die Gegner dort liegen bleiben, ueberleben
/// sie das Entladen des Levels und stehen beim naechsten Lauf noch herum.
/// Darum schieben die Spawner jedes erzeugte Objekt hierher.
///
/// Frueher stand an jeder dieser Stellen SceneManager.GetSceneByName("Game").
/// Mit den eigenen Map-Szenen heisst die Lauf-Szene aber GameCore - und in der
/// Test-Szene wieder anders. Deshalb die Frage andersherum stellen: die
/// Lauf-Szene ist die, in der der Spieler steht.
/// </summary>
public static class RunScene
{
    /// <summary>
    /// Die aktuelle Lauf-Szene, oder eine ungueltige Szene, wenn gerade keine
    /// laeuft. Die Aufrufer pruefen ohnehin IsValid() und isLoaded.
    /// </summary>
    public static Scene Current
    {
        get
        {
            // Der Spieler steht im alten System in Game, im neuen in GameCore
            // und in der Test-Szene in test_scene - immer in der richtigen.
            if (PlayerController.Instance != null)
            {
                return PlayerController.Instance.gameObject.scene;
            }

            Scene scene = SceneManager.GetSceneByName("Game");
            if (scene.IsValid() && scene.isLoaded) return scene;

            scene = SceneManager.GetSceneByName(MapSceneSystem.CoreScene);
            if (scene.IsValid() && scene.isLoaded) return scene;

            return default;
        }
    }

    // Sammelobjekte pro Name - werden beim naechsten Lauf neu angelegt,
    // weil das alte mit seiner Szene verschwunden ist.
    private static readonly Dictionary<string, Transform> containers = new Dictionary<string, Transform>();

    /// <summary>
    /// Legt ein frisch erzeugtes Objekt in die Lauf-Szene, unter ein
    /// Sammelobjekt namens <paramref name="container"/> (z. B. "Gegner"), damit
    /// es sich in der Hierarchy einklappen laesst. Ohne Lauf-Szene bleibt es,
    /// wo es ist.
    /// </summary>
    public static void Place(GameObject spawned, string container)
    {
        if (spawned == null) return;

        Scene scene = Current;
        if (!scene.IsValid() || !scene.isLoaded) return;

        if (!containers.TryGetValue(container, out Transform parent) || parent == null || parent.gameObject.scene != scene)
        {
            parent = new GameObject(container).transform;
            SceneManager.MoveGameObjectToScene(parent.gameObject, scene);
            containers[container] = parent;
        }

        // SetParent zieht das Objekt mit in die Szene des Sammelobjekts.
        spawned.transform.SetParent(parent, true);
    }
}
