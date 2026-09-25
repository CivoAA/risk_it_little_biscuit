using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Zwei Aufgaben, beide beim Laden des Editors:
///
///   1. Gibt es noch gar keine Baeume, wird einmalig der Beispielbaum fuer
///      Charakter 0 aus der Textvorlage gebaut - sonst staenden im Hub vier leere
///      Kategorien. Sobald der Ordner Assets/Resources/SkillTrees existiert,
///      passiert das nie wieder. Wer den Beispielbaum zurueck will, nimmt den
///      Menuepunkt unten; der ueberschreibt char_0.
///
///   2. Jeder vorhandene Baum bekommt seinen Startknoten je Kategorie, falls er
///      noch von vorher stammt. Das ist ein einmaliger Umzug, der sich danach
///      selbst still verhaelt.
///
/// Gebaut wird aus der Textvorlage neben den Skripten, also durch denselben
/// Leser, den auch Tools -> Skilltree -> Baum aus Textdatei bauen benutzt. So
/// gibt es keine zweite Stelle, an der ein Baum entstehen kann.
/// </summary>
[InitializeOnLoad]
public static class SkillTreeBootstrap
{
    const string TemplatePath = "Assets/Scripts/Skills/Vorlagen/char_0_standard.txt";

    static SkillTreeBootstrap()
    {
        // Nicht mitten im Import loslegen - erst wenn Unity fertig geladen hat.
        EditorApplication.delayCall += RunOnce;
    }

    static void RunOnce()
    {
        if (!AssetDatabase.IsValidFolder(SkillTreeTextIO.AssetFolder))
        {
            if (!File.Exists(TemplatePath)) return;

            Debug.Log("[Skills] Noch keine Skilltrees im Projekt - der Beispielbaum fuer Charakter 0 " +
                      "wird aus " + TemplatePath + " angelegt. Weiter geht es unter " +
                      "Tools -> Skilltree -> Editor.");

            SkillTreeTextIO.ImportFile(TemplatePath);
            return;
        }

        EnsureStarts();
    }

    /// <summary>
    /// Der nachtraegliche Umzug auf den Startknoten: jede Kategorie faengt mit
    /// genau einem Knoten an, alles ohne Vorbedingung haengt sich daran, und was
    /// in Spalte 0 lag, rueckt eine nach rechts.
    ///
    /// Laeuft bei jedem Neuladen der Skripte mit, tut aber nur beim ersten Mal
    /// etwas - danach haben alle Baeume ihren Start und nichts aendert sich mehr.
    /// </summary>
    static void EnsureStarts()
    {
        bool changed = false;

        foreach (SkillTreeAsset tree in SkillTreeTextIO.LoadAll())
        {
            if (tree == null || !tree.EnsureStartNodes()) continue;

            EditorUtility.SetDirty(tree);
            changed = true;

            Debug.Log($"[Skills] '{tree.treeId}': Startknoten ergaenzt. Jede Kategorie faengt jetzt " +
                      "mit einem Knoten an, an dem die drei Bahnen haengen.");
        }

        if (!changed) return;

        AssetDatabase.SaveAssets();
        SkillTrees.Reload();
    }

    [MenuItem("Tools/Skilltree/Beispielbaum (Charakter 0) neu anlegen", false, 226)]
    static void Rebuild()
    {
        if (!File.Exists(TemplatePath))
        {
            EditorUtility.DisplayDialog("Skilltree", "Die Vorlage " + TemplatePath +
                                        " gibt es nicht mehr.", "Gut");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "Beispielbaum anlegen",
            "Der Baum 'char_0' wird aus der Vorlage neu gebaut. Aenderungen, die du im Editor " +
            "daran gemacht hast, gehen dabei verloren.",
            "Neu bauen", "Abbrechen");

        if (!ok) return;

        SkillTreeAsset asset = SkillTreeTextIO.ImportFile(TemplatePath);

        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
