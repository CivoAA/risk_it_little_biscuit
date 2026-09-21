using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Setzt die gelbe Kapsel mit dem Kochbuch in die offene Szene - das Objekt, an
/// dem [E] das Erfolge-/Unlocks-Buch aufmacht.
///
/// Bewusst ein Werkzeug und kein fertiges Szenen-Objekt: laut Remaster-Regel
/// gehoeren Inhalte in Code, nicht ins Szenen-YAML. Wer die Kapsel woanders
/// braucht, setzt sie hier neu oder zieht das Prefab rein.
/// </summary>
public static class AchievementsBookSceneTools
{
    private const string SpritePath = "Assets/Art/World-Objects/kapsel_erfolgsbuch.png";
    private const string PrefabPath = "Assets/Prefabs/MapObjects/KapselErfolgsbuch.prefab";

    [MenuItem("Tools/Achievements/Buch-Objekt in Szene setzen")]
    private static void PlaceInScene()
    {
        GameObject go = Create();
        if (go == null) return;

        Undo.RegisterCreatedObjectUndo(go, "Erfolge-Kapsel setzen");
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) view.FrameSelected();

        Debug.Log($"[Buch] '{go.name}' gesetzt bei {go.transform.position}. " +
                  "Spielen, hinlaufen, [E] druecken. Zone und Hinweistext stehen im Inspector.");
    }

    [MenuItem("Tools/Achievements/Buch-Prefab erzeugen")]
    private static void CreatePrefab()
    {
        GameObject go = Create();
        if (go == null) return;

        go.transform.position = Vector3.zero;

        // AssetDatabase rechnet immer mit Schraegstrichen, GetDirectoryName liefert
        // unter Windows aber Backslashes.
        string dir = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Debug.LogError($"[Buch] Ordner '{dir}' gibt es nicht.");
            Object.DestroyImmediate(go);
            return;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        if (prefab == null)
        {
            Debug.LogError("[Buch] Prefab konnte nicht gespeichert werden.");
            return;
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[Buch] Prefab liegt unter {PrefabPath}.");
    }

    [MenuItem("Tools/Achievements/Buch oeffnen (nur im Play Mode)")]
    private static void OpenPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Buch] Geht nur im Play Mode - das Fenster baut sich zur Laufzeit auf.");
            return;
        }
        AchievementsBookPanel.Toggle();
    }

    // ------------------------------------------------------------------

    private static GameObject Create()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[Buch] Sprite fehlt: {SpritePath}");
            return null;
        }

        GameObject go = new GameObject("Kapsel_Erfolgsbuch");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;

        AchievementsBookTrigger trigger = go.AddComponent<AchievementsBookTrigger>();

        // Die Zonen-Felder liegen geschuetzt in HubInteractable - ueber
        // SerializedObject lassen sie sich trotzdem sauber vorbelegen.
        SerializedObject so = new SerializedObject(trigger);
        Set(so, "shape", p => p.enumValueIndex = 1);                                   // Rechteck
        Set(so, "interactSize", p => p.vector2Value = new Vector2(2f, 2.5f));
        Set(so, "interactOffset", p => p.vector2Value = new Vector2(0f, -1.2f));
        Set(so, "promptText", p => p.stringValue = "[E] Erfolge");
        Set(so, "showPrompt", p => p.boolValue = true);
        Set(so, "showOutline", p => p.boolValue = true);
        Set(so, "outlineMode", p => p.enumValueIndex = 1);                             // NurInReichweite
        so.ApplyModifiedPropertiesWithoutUndo();

        go.transform.position = SuggestPosition();
        return go;
    }

    private static void Set(SerializedObject so, string field, System.Action<SerializedProperty> apply)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) apply(p);
        else Debug.LogWarning($"[Buch] Feld '{field}' gibt es in HubInteractable nicht mehr.");
    }

    /// <summary>Neben den Spieler, sonst in die Mitte der Szenenansicht.</summary>
    private static Vector3 SuggestPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) return player.transform.position + new Vector3(2f, 0f, 0f);

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) return new Vector3(view.pivot.x, view.pivot.y, 0f);

        return Vector3.zero;
    }
}
