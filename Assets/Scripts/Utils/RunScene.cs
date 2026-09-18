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
}
