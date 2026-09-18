using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setzt die Levelauswahl im Hub auf die Welten, die es wirklich gibt.
///
/// Stand der Dinge: gespielt werden World0 (Kueche) und World3 (Wald). World1
/// und World2 fliegen spaeter raus - ihre Szenen bleiben vorerst liegen, aber
/// in der Auswahl haben sie nichts mehr verloren. Die restlichen Karten stehen
/// als "BALD" ohne Namen da, bis sie eine Welt bekommen.
///
/// Warum ein Werkzeug und nicht von Hand: die Liste haengt als serialisierte
/// Liste in hub.unity. Von aussen im YAML herumzuschneiden waere riskant,
/// solange der Editor die Szene offen hat - so laeuft es ueber Unity selbst.
/// Ein einmaliger Aufruf; danach kann die Liste wie immer im Inspector am
/// Objekt "LevelSelectUI" gepflegt werden (Vorschaubilder, Texte, Unlocks).
/// </summary>
public static class HubLevelTools
{
    private const string HubScenePath = "Assets/Scenes/hub.unity";

    /// <summary>Was in der Auswahl stehen soll - Reihenfolge = Reihenfolge der Karten.</summary>
    private static readonly (string Name, string Description, int MapId)[] Levels =
    {
        ("Kueche", "Mehl, Zucker und Aerger. Hier faengt alles an.", 0),
        ("Wald",   "Zwischen den Baeumen wird es voll.",             3),
    };

    [MenuItem("Tools/Hub/Levelauswahl auf die neuen Welten setzen", false, 0)]
    public static void ApplyMenu()
    {
        bool go = EditorUtility.DisplayDialog(
            "Levelauswahl setzen",
            "Karte 1 wird \"Kueche\" (World0), Karte 2 wird \"Wald\" (World3).\n\n" +
            "Alle weiteren Karten verlieren Name und Welt und stehen als \"BALD\" da.\n\n" +
            "Eigene Texte und Vorschaubilder der ersten beiden Karten werden dabei ueberschrieben.",
            "Setzen", "Abbrechen");

        if (!go) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
        string report = Apply(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, HubScenePath);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Levelauswahl", report, "Ok");
    }

    public static string Apply(Scene scene)
    {
        HubLevelSelectUI ui = FindInScene<HubLevelSelectUI>(scene);
        if (ui == null)
        {
            return "Kein HubLevelSelectUI in " + HubScenePath + " gefunden - nichts geaendert.";
        }

        var serialized = new SerializedObject(ui);
        SerializedProperty list = serialized.FindProperty("levels");
        if (list == null || !list.isArray)
        {
            return "Die Liste \"levels\" wurde nicht gefunden - nichts geaendert.";
        }

        // Mindestens so viele Karten, wie wir fuellen wollen.
        if (list.arraySize < Levels.Length) list.arraySize = Levels.Length;

        var report = new StringBuilder();

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);

            if (i < Levels.Length)
            {
                (string name, string description, int mapId) = Levels[i];

                Set(entry, "displayName", name);
                Set(entry, "description", description);
                SetInt(entry, "mapId", mapId);

                // Die Map-Szene ergibt sich aus der Map-ID (Map_World<mapId>).
                // Der Eintrag hier ist nur noch die Rueckfallebene und bleibt
                // leer, damit niemand aus Versehen wieder in Game.unity landet.
                Set(entry, "sceneToLoad", "");

                report.AppendLine($"Karte {i + 1}: {name} (World{mapId})");
            }
            else
            {
                // Noch keine Welt: ohne gueltige Map-ID findet MapSceneSystem
                // keine Szene, und die Karte zeigt "BALD".
                Set(entry, "displayName", "");
                Set(entry, "description", "");
                SetInt(entry, "mapId", -1);
                Set(entry, "sceneToLoad", "");

                report.AppendLine($"Karte {i + 1}: noch ohne Welt (BALD)");
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);

        return report.ToString();
    }

    // ------------------------------------------------------------------ Teile

    private static void Set(SerializedProperty entry, string field, string value)
    {
        SerializedProperty property = entry.FindPropertyRelative(field);
        if (property != null) property.stringValue = value;
    }

    private static void SetInt(SerializedProperty entry, string field, int value)
    {
        SerializedProperty property = entry.FindPropertyRelative(field);
        if (property != null) property.intValue = value;
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
}
