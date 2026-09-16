using UnityEditor;
using UnityEngine;

/// <summary>
/// Blendet im Inspector das Feld aus, das zur gewaehlten Zonenform nicht passt,
/// damit nicht unklar ist, welcher Wert gerade wirkt.
/// Gilt auch fuer alle abgeleiteten Klassen (BookHint, TeleportToShop, ...).
/// </summary>
[CustomEditor(typeof(HubInteractable), true)]
public class HubInteractableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty shape = serializedObject.FindProperty("shape");
        int form = shape != null ? shape.enumValueIndex : 0;

        SerializedProperty outline = serializedObject.FindProperty("showOutline");
        bool hasOutline = outline != null && outline.boolValue;

        SerializedProperty p = serializedObject.GetIterator();
        bool enterChildren = true;
        while (p.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (p.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(p);
                continue;
            }

            // 0 = Kreis, 1 = Rechteck
            if (p.name == "interactRadius" && form != 0) continue;
            if (p.name == "interactSize"   && form != 1) continue;

            // Umrandungs-Einstellungen nur zeigen, wenn die Umrandung an ist
            if (!hasOutline &&
                (p.name == "outlineMode" || p.name == "outlineColor" || p.name == "outlineTargets"))
                continue;

            EditorGUILayout.PropertyField(p, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
