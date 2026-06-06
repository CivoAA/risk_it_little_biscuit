using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillManager))]
public class SkillManagerEditor : Editor
{
    private SerializedProperty skillsProp;

    private void OnEnable()
    {
        skillsProp = serializedObject.FindProperty("skills");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("⚙️ Skill Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox("Definiert Icons und Beschreibungen für jeden Skill. 'X' kann als Platzhalter für den Wert verwendet werden.", MessageType.Info);

        for (int i = 0; i < skillsProp.arraySize; i++)
        {
            SerializedProperty element = skillsProp.GetArrayElementAtIndex(i);
            SerializedProperty typeProp = element.FindPropertyRelative("type");
            SerializedProperty lockedIconProp = element.FindPropertyRelative("lockedIcon");
            SerializedProperty unlockedIconProp = element.FindPropertyRelative("unlockedIcon");
            SerializedProperty descriptionProp = element.FindPropertyRelative("description");

            string displayName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"🧩 {displayName}", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(typeProp, new GUIContent("Skill Type"));
            EditorGUILayout.PropertyField(lockedIconProp, new GUIContent("Locked Icon"));
            EditorGUILayout.PropertyField(unlockedIconProp, new GUIContent("Unlocked Icon"));
            EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description"));

            EditorGUILayout.Space(4);
            if (GUILayout.Button("🗑 Remove Skill Entry", GUILayout.Height(22)))
            {
                skillsProp.DeleteArrayElementAtIndex(i);
                break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        if (GUILayout.Button("➕ Add New Skill", GUILayout.Height(24)))
        {
            skillsProp.InsertArrayElementAtIndex(skillsProp.arraySize);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
