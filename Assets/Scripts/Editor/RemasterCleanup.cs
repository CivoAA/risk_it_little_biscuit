using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Räumt die Szenen nach dem Umzug der Daten in die Code-Kataloge auf.
///
///   * entfernt die Manager-Objekte, die es nicht mehr braucht
///     (SaveGame, LevelPoint)
///   * entfernt die alten, von Hand platzierten Skill-Knoten
///   * hängt SkillTreeView ans SkillTreeCanvas, damit der Baum aus
///     SkillTrees.cs gebaut wird
///
/// Darf mehrfach laufen - beim zweiten Mal findet es nichts mehr und ändert
/// nichts. Wenn alles sitzt, kann diese Datei weg.
/// </summary>
public static class RemasterCleanup
{
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/World Map.unity",
        "Assets/Scenes/Main Menu.unity",
    };

    [MenuItem("Tools/Remaster/Szenen aufräumen")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[Remaster] Bitte nicht im Play-Modus.");
            return;
        }

        string previous = EditorSceneManager.GetActiveScene().path;
        int total = 0;

        foreach (string path in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changes = CleanScene(scene);
            total += changes;

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Remaster] '{scene.name}': {changes} Änderung(en) gespeichert.");
            }
            else
            {
                Debug.Log($"[Remaster] '{scene.name}': nichts zu tun.");
            }
        }

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        Debug.Log($"[Remaster] Fertig - {total} Änderung(en) insgesamt.");
    }

    private static int CleanScene(Scene scene)
    {
        int changes = 0;

        changes += RemoveObsolete(scene);
        changes += RemoveOldSkillNodes(scene);
        changes += EnsureSkillTreeView(scene);

        return changes;
    }

    /// <summary>Die Manager, deren Daten jetzt im Code stehen.</summary>
    private static int RemoveObsolete(Scene scene)
    {
        int changes = 0;

        // UnlockManager und AchievementManager stehen hier nicht mehr: ihre
        // Objekte sind aus allen Szenen verschwunden, die Platzhalter-Skripte
        // sind geloescht.
#pragma warning disable CS0618 // die Typen sind absichtlich als veraltet markiert
        changes += DestroyHolders<SaveGame>(scene, "SaveGame");
        changes += DestroyHolders<LevelPoint>(scene, "LevelPoint");
#pragma warning restore CS0618

        return changes;
    }

    /// <summary>Die 142 von Hand platzierten Skill-Knoten.</summary>
    private static int RemoveOldSkillNodes(Scene scene)
    {
#pragma warning disable CS0618
        int removed = DestroyHolders<SkillNode>(scene, "Skill-Knoten");
#pragma warning restore CS0618
        return removed;
    }

    private static int DestroyHolders<T>(Scene scene, string label) where T : Component
    {
        List<GameObject> victims = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component != null && !victims.Contains(component.gameObject))
                    victims.Add(component.gameObject);
            }
        }

        foreach (GameObject go in victims) Object.DestroyImmediate(go);

        if (victims.Count > 0)
            Debug.Log($"[Remaster] '{scene.name}': {victims.Count}x {label} entfernt.");

        return victims.Count;
    }

    /// <summary>Hängt SkillTreeView ans SkillTreeCanvas, falls es noch fehlt.</summary>
    private static int EnsureSkillTreeView(Scene scene)
    {
        GameObject canvas = FindByName(scene, "SkillTreeCanvas");
        if (canvas == null) return 0;

        if (canvas.GetComponent<SkillTreeView>() != null) return 0;

        canvas.AddComponent<SkillTreeView>();
        Debug.Log($"[Remaster] '{scene.name}': SkillTreeView an '{canvas.name}' gehängt.");
        return 1;
    }

    private static GameObject FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }
}
