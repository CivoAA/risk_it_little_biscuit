using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Baut den <see cref="SpawnDirector"/> in GameCore ein und legt die alten
/// Spawner still.
///
/// Die Gegner-Prefabs holt der Installer aus dem alten TimeWaveManager - das
/// sind garantiert dieselben, die bisher benutzt wurden, und niemand muss 16
/// Felder von Hand ziehen. Der TimeWaveManager bleibt deshalb als deaktiviertes
/// Objekt in der Szene stehen: er ist ab jetzt nur noch die Prefab-Ablage.
/// Erst wenn der Katalog aus eigener Kraft steht, kann er ganz weg.
///
/// Der Aufruf ist wiederholbar und wird auch vom <see cref="MapSceneSplitter"/>
/// automatisch mitgemacht, wenn GameCore neu gebaut wird.
/// </summary>
public static class SpawnDirectorInstaller
{
    private const string CoreScenePath = "Assets/Scenes/Core/GameCore.unity";
    private const string DirectorName = "Spawn Director";

    /// <summary>Alte Spawner - werden abgeschaltet, nicht geloescht.</summary>
    private static readonly string[] LegacySpawners =
    {
        "Managers/CinemachineCamera/TimeWaveManager",
        "Managers/CinemachineCamera/Enemy Spawner",
        "Managers/CinemachineCamera/Enemy Spawner 2",
    };

    [MenuItem("Tools/Spawns/Spawn-Director in GameCore einbauen", false, 0)]
    public static void InstallMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);

        string report = Install(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, CoreScenePath);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Spawn-Director", report, "Ok");
    }

    /// <summary>
    /// Baut den Director in die uebergebene (bereits geoeffnete) Szene ein.
    /// Speichern macht der Aufrufer.
    /// </summary>
    public static string Install(Scene scene)
    {
        var report = new StringBuilder();

        // 1. Objekt anlegen oder wiederverwenden.
        GameObject host = FindRoot(scene, DirectorName);
        if (host == null)
        {
            host = new GameObject(DirectorName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.SetAsFirstSibling();
            report.AppendLine($"Objekt \"{DirectorName}\" angelegt.");
        }
        else
        {
            report.AppendLine($"Objekt \"{DirectorName}\" war schon da - aktualisiert.");
        }

        SpawnCatalog catalog = host.GetComponent<SpawnCatalog>();
        if (catalog == null) catalog = host.AddComponent<SpawnCatalog>();

        SpawnDirector director = host.GetComponent<SpawnDirector>();
        if (director == null) director = host.AddComponent<SpawnDirector>();

        // 2. Prefabs aus dem alten TimeWaveManager uebernehmen.
        TimeWaveManager source = FindInScene<TimeWaveManager>(scene);
        if (source == null)
        {
            report.AppendLine("Kein TimeWaveManager gefunden - der Katalog bleibt leer!");
        }
        else
        {
            CopyPrefabs(source, catalog);
            report.AppendLine("Gegner-Prefabs aus dem TimeWaveManager uebernommen.");
        }

        EditorUtility.SetDirty(catalog);

        // 3. Verdrahtung am Director: Katalog und das alte Wave-Textfeld.
        var serialized = new SerializedObject(director);
        serialized.FindProperty("catalog").objectReferenceValue = catalog;

        Object waveText = FindWaveText(scene);
        if (waveText != null)
        {
            serialized.FindProperty("phaseText").objectReferenceValue = waveText;
            report.AppendLine("Wave-Textfeld uebernommen - es zeigt jetzt die Phase an.");
        }
        else
        {
            report.AppendLine("Kein Wave-Textfeld gefunden - der Director laeuft ohne Anzeige.");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);

        // 4. SpawnExp retten. Der haengt als zweite Komponente an den alten
        //    Spawner-Objekten - schaltet man die ab, laeuft sein Awake nie,
        //    SpawnExp.Instance bleibt null, und der Gegner fliegt beim Sterben
        //    in eine NullReference: er verliert Leben, wird aber nie zerstoert.
        if (host.GetComponent<SpawnExp>() == null)
        {
            SpawnExp expSource = FindInScene<SpawnExp>(scene);
            if (expSource != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(expSource);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(host);
                report.AppendLine("SpawnExp (Erfahrungskugeln) auf den Director uebernommen.");
            }
            else
            {
                report.AppendLine("ACHTUNG: kein SpawnExp in der Szene - Gegner wuerden beim Sterben haengen.");
            }
        }

        // 5. Alte Spawner stilllegen.
        foreach (string path in LegacySpawners)
        {
            Transform target = FindByPath(scene, path);
            if (target == null) continue;

            if (target.gameObject.activeSelf)
            {
                target.gameObject.SetActive(false);
                report.AppendLine($"\"{target.name}\" abgeschaltet.");
            }
        }

        // 6. Was im Katalog fehlt, jetzt sagen und nicht erst im Spiel.
        List<EnemyId> missing = catalog.EditorMissing();
        if (missing.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Ohne Prefab (werden im Plan uebersprungen):");
            foreach (EnemyId id in missing) report.AppendLine("  " + id);
        }

        return report.ToString();
    }

    // ------------------------------------------------------------------ Teile

    private static void CopyPrefabs(TimeWaveManager source, SpawnCatalog catalog)
    {
        catalog.EditorSet(EnemyId.Marshmello, source.marshmello);
        catalog.EditorSet(EnemyId.EliteMarshmello, source.eliteMarshmello);
        catalog.EditorSet(EnemyId.MausMitMesser, source.mausMitMesser);
        catalog.EditorSet(EnemyId.EvilSlime, source.evilSlime);
        catalog.EditorSet(EnemyId.SaureMilch, source.saureMilch);
        catalog.EditorSet(EnemyId.MiniMilch, source.miniMilch);
        catalog.EditorSet(EnemyId.Muffin, source.muffin);
        catalog.EditorSet(EnemyId.Suppe, source.suppe);
        catalog.EditorSet(EnemyId.Pancake, source.pancake);
        catalog.EditorSet(EnemyId.Fetti, source.fetti);
        catalog.EditorSet(EnemyId.MesserMaus1, source.messerMaus1);
        catalog.EditorSet(EnemyId.MesserMaus2, source.messerMaus2);
        catalog.EditorSet(EnemyId.MiniBossMarshmello, source.miniBoss_marshmello);
        catalog.EditorSet(EnemyId.Blocker, source.blocker);
        catalog.EditorSet(EnemyId.KeksKoenig, source.keksKoenig);

        if (source.slimeVariants != null) catalog.EditorSetSlimes(source.slimeVariants);
    }

    /// <summary>Das "Wave: N"-Feld haengt als private Referenz am alten EnemySpawner.</summary>
    private static Object FindWaveText(Scene scene)
    {
        EnemySpawner spawner = FindInScene<EnemySpawner>(scene);
        if (spawner == null) return null;

        SerializedProperty property = new SerializedObject(spawner).FindProperty("WaveText");
        return property != null ? property.objectReferenceValue : null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }
        return null;
    }

    private static Transform FindByPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = FindRoot(scene, parts[0]);
        Transform current = root != null ? root.transform : null;

        for (int i = 1; i < parts.Length && current != null; i++)
        {
            current = current.Find(parts[i]);
        }

        return current;
    }
}
