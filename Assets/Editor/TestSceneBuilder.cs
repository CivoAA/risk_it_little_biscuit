using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Erzeugt "Assets/Scenes/test_scene.unity" als Kopie der Game-Szene, entfernt
/// Map und Gegner und baut das Test-Rig ein.
///
/// Die Game-Szene selbst wird dabei nie verändert – es wird nur kopiert.
/// Der Builder ist beliebig oft wiederholbar: ein vorhandenes test_scene.unity
/// wird ersetzt, sodass die Test-Szene jederzeit auf den aktuellen Stand der
/// Game-Szene nachgezogen werden kann.
/// </summary>
public static class TestSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/Game.unity";
    private const string TargetScenePath = "Assets/Scenes/test_scene.unity";
    private const string DummySourcePrefab = "Assets/Prefabs/Enemy/fin_slime.prefab";

    /// <summary>Root-Objekte, die in der Test-Szene nichts zu suchen haben.</summary>
    private static readonly string[] RootsToDelete =
    {
        "Grid",                  // Tilemaps der drei Welten inkl. EnemySpawner/Waves
        "squiddy_funny_walk_0",  // übrig gebliebener Gegner
    };

    /// <summary>Pfade unterhalb von Root-Objekten, die entfernt werden.</summary>
    private static readonly string[] ChildrenToDelete =
    {
        "Managers/World Manager",     // WorldSelector: aktiviert Welten + EnemySpawner
        "Managers/TileMapGenerator1", // erzeugt die Map
    };

    [MenuItem("Tools/Szenen/Test-Szene neu bauen", false, 100)]
    public static void BuildMenu()
    {
        if (System.IO.File.Exists(TargetScenePath))
        {
            bool replace = EditorUtility.DisplayDialog(
                "Test-Szene neu bauen",
                "test_scene.unity existiert bereits und wird durch eine frische Kopie der " +
                "Game-Szene ersetzt.\n\nEigene Änderungen in der Test-Szene gehen dabei verloren.",
                "Ersetzen", "Abbrechen");

            if (!replace)
            {
                return;
            }
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Build();
        EditorUtility.DisplayDialog("Test-Szene", "test_scene.unity wurde neu gebaut.", "Ok");
    }

    /// <summary>Einstieg für den Batch-Mode (Unity -executeMethod).</summary>
    public static void BuildFromCommandLine()
    {
        Build();
    }

    public static void Build()
    {
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
        {
            AssetDatabase.DeleteAsset(TargetScenePath);
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath))
        {
            Debug.LogError($"[TestSceneBuilder] Konnte {SourceScenePath} nicht nach {TargetScenePath} kopieren.");
            return;
        }

        AssetDatabase.ImportAsset(TargetScenePath, ImportAssetOptions.ForceSynchronousImport);

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        StripMapAndEnemies(scene);
        ActivateEventSystem(scene);
        SetupCamera(scene);
        Transform player = FindRoot(scene, "Player");
        CreateDummy(scene, player);
        CreateTestRig(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScenePath);

        RegisterInBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log($"[TestSceneBuilder] {TargetScenePath} fertig gebaut.");
    }

    // ------------------------------------------------------------------

    private static void StripMapAndEnemies(Scene scene)
    {
        List<GameObject> roots = scene.GetRootGameObjects().ToList();

        foreach (GameObject root in roots)
        {
            // Alle Baum-Instanzen der Map (Tree1 / Tree2, auch "Tree1 (1)").
            bool isTree = root.name.StartsWith("Tree1") || root.name.StartsWith("Tree2");

            if (isTree || RootsToDelete.Contains(root.name))
            {
                Object.DestroyImmediate(root);
            }
        }

        foreach (string path in ChildrenToDelete)
        {
            Transform target = FindByPath(scene, path);
            if (target != null)
            {
                Object.DestroyImmediate(target.gameObject);
            }
            else
            {
                Debug.LogWarning($"[TestSceneBuilder] '{path}' nicht gefunden – übersprungen.");
            }
        }

        // Sicherheitsnetz: alles, was noch als Gegner getaggt ist, fliegt raus.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Enemy enemy in root.GetComponentsInChildren<Enemy>(true))
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }

    private static void ActivateEventSystem(Scene scene)
    {
        Transform eventSystem = FindRoot(scene, "EventSystem");
        if (eventSystem != null)
        {
            // In der Game-Szene deaktiviert, weil die World Map ein EventSystem
            // mitbringt. Die Test-Szene läuft allein und braucht ein eigenes.
            eventSystem.gameObject.SetActive(true);
        }
    }

    private static void SetupCamera(Scene scene)
    {
        Transform cameraTransform = FindByPath(scene, "Managers/Main Camera");
        if (cameraTransform == null)
        {
            return;
        }

        Camera camera = cameraTransform.GetComponent<Camera>();
        if (camera != null)
        {
            // Ohne Map würde sonst der Skybox-/Müllinhalt durchscheinen.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.15f, 0.19f, 1f);
        }

        // Im echten Spiel hört die World-Map-Kamera; die Test-Szene läuft allein
        // und bräuchte sonst gar keinen Listener – dann bleibt alles stumm.
        AudioListener listener = cameraTransform.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = true;
        }
    }

    private static void CreateDummy(Scene scene, Transform player)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(DummySourcePrefab);
        if (source == null)
        {
            Debug.LogError($"[TestSceneBuilder] Dummy-Vorlage {DummySourcePrefab} nicht gefunden.");
            return;
        }

        GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        PrefabUtility.UnpackPrefabInstance(dummy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        dummy.name = "Training Dummy";

        // Gegner-Logik durch die Dummy-Variante ersetzen.
        foreach (Enemy enemy in dummy.GetComponents<Enemy>())
        {
            Object.DestroyImmediate(enemy);
        }
        foreach (EnemyTeleport teleport in dummy.GetComponents<EnemyTeleport>())
        {
            Object.DestroyImmediate(teleport);
        }
        dummy.AddComponent<TrainingDummy>();

        Vector3 basePosition = player != null ? player.position : Vector3.zero;
        dummy.transform.position = basePosition + new Vector3(0f, 4f, 0f);
    }

    private static void CreateTestRig(Scene scene)
    {
        GameObject rig = new GameObject("Test Scene Rig");
        SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.SetAsFirstSibling();

        rig.AddComponent<TestSceneBootstrap>();
        rig.AddComponent<DamageMeter>();
        rig.AddComponent<DummyArena>();
        rig.AddComponent<TestSceneHUD>();
    }

    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == TargetScenePath))
        {
            return;
        }

        // Hinten anhängen, damit sich die Indizes der bestehenden Szenen nicht
        // verschieben. Ohne Eintrag könnte die Szene sich nicht selbst neu laden.
        scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root.transform;
            }
        }
        return null;
    }

    private static Transform FindByPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        Transform current = FindRoot(scene, parts[0]);

        for (int i = 1; i < parts.Length && current != null; i++)
        {
            current = current.Find(parts[i]);
        }

        return current;
    }
}
