using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Macht aus dem Player-Objekt der Game-Szene ein Prefab und haengt den
/// <see cref="CameraTargetBinder"/> an die CinemachineCamera.
///
/// Warum das geht, ohne Inspector-Daten zu verlieren: unter "Player" haengt
/// alles, was der Lauf braucht - Weapons, Buffs, EvoWeapons, MaxLevelStuff,
/// PlayerExpCollector, UnlockChecker. Und jede Referenz im PlayerController
/// (rb, animator, PickupRange, die Waffenlisten, die EvoCombinations) zeigt in
/// genau diesen Teilbaum hinein. Ein Prefab nimmt den Teilbaum am Stueck mit,
/// Unity setzt die Verweise dabei selbst um. Verloren gehen koennte nur, was
/// von aussen hineinzeigt - das sind vier Felder, und die holen sich den Spieler
/// inzwischen zur Laufzeit ueber PlayerController.Instance:
///
///   * RandomObjectSpawner.player       (Welt 0)
///   * RandomObjectSpawner3x3.player    (Welt 1 und 2)
///   * CinemachineCamera.Tracking Target
///
/// Die Szenen-Referenzen bleiben erst mal stehen und funktionieren weiter. Erst
/// wenn die Maps in eigene Szenen wandern, laesst Unity sie fallen (szenen-
/// uebergreifende Referenzen speichert es nicht) - dann greift die Laufzeit-
/// Bindung.
///
/// Der Aufruf ist wiederholbar: ist der Player schon ein Prefab, passiert nichts.
/// </summary>
public static class PlayerPrefabBuilder
{
    private const string ScenePath  = "Assets/Scenes/Game.unity";
    private const string PrefabPath = "Assets/Prefabs/Player.prefab";
    private const string CameraName = "CinemachineCamera";

    [MenuItem("Tools/Remaster/Player-Prefab erzeugen", false, 10)]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string result = Build();
        EditorUtility.DisplayDialog("Player-Prefab", result, "Ok");
    }

    /// <summary>Einstieg fuer den Batch-Mode (Unity -executeMethod).</summary>
    public static void BuildFromCommandLine()
    {
        Debug.Log("[PlayerPrefabBuilder] " + Build());
    }

    public static string Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Player");
        if (player == null)
        {
            return $"Kein Root-Objekt \"Player\" in {ScenePath} gefunden - nichts geaendert.";
        }

        bool changed = false;
        string report = "";

        // 1. Player -> Prefab. Connect, damit die Szene danach eine Instanz des
        //    Prefabs enthaelt und beide Seiten synchron bleiben.
        if (PrefabUtility.IsPartOfAnyPrefab(player))
        {
            report += "Player ist bereits ein Prefab - uebersprungen.\n";
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                player, PrefabPath, InteractionMode.AutomatedAction, out bool ok);

            if (!ok || prefab == null)
            {
                return $"Prefab konnte nicht unter {PrefabPath} angelegt werden - nichts geaendert.";
            }

            int children = player.GetComponentsInChildren<Transform>(true).Length - 1;
            report += $"{PrefabPath} angelegt ({children} Objekte im Teilbaum).\n";
            changed = true;
        }

        // 2. Kamera-Binder. Das Tracking Target der CinemachineCamera zeigt heute
        //    als Szenen-Referenz auf den Player; nach dem Szenen-Split waere es
        //    leer. Der Binder traegt den Spieler dann zur Laufzeit nach.
        GameObject camera = FindByName(scene, CameraName);
        if (camera == null)
        {
            report += $"Achtung: kein Objekt \"{CameraName}\" gefunden - Binder nicht gesetzt.\n";
        }
        else if (camera.GetComponent<CameraTargetBinder>() != null)
        {
            report += "CameraTargetBinder haengt schon an der Kamera - uebersprungen.\n";
        }
        else
        {
            Undo.AddComponent<CameraTargetBinder>(camera);
            report += $"CameraTargetBinder an \"{CameraName}\" gehaengt.\n";
            changed = true;
        }

        if (!changed)
        {
            return report + "\nNichts zu tun.";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        return report + "\nGame.unity gespeichert. Danach bitte Tools -> Test Scene neu bauen.";
    }

    /// <summary>Sucht ein Objekt ueberall in der Szene, auch unter Kindern.</summary>
    private static GameObject FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform hit = root.GetComponentsInChildren<Transform>(true)
                                .FirstOrDefault(t => t.name == name);
            if (hit != null) return hit.gameObject;
        }
        return null;
    }
}
