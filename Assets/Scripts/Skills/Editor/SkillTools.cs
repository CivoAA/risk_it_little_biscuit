using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um den Skilltree-Katalog. Alles unter Tools -> Skills.
/// Die Prüfung läuft ausserdem automatisch nach jedem Compile.
/// </summary>
public static class SkillTools
{
    private const string IconFolder = "Assets/Resources/Skills";

    [MenuItem("Tools/Skills/Katalog prüfen")]
    public static void Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateInto(errors, warnings);

        foreach (string w in warnings) Debug.LogWarning("[Skills] " + w);
        foreach (string e in errors) Debug.LogError("[Skills] " + e);

        int nodes = 0;
        foreach (SkillTreeDef t in SkillTrees.All)
        {
            foreach (SkillNodeDef unused in t.AllNodes()) nodes++;
        }

        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log($"[Skills] Katalog in Ordnung - {SkillTrees.All.Count} Baum/Bäume, {nodes} Knoten.");
        else
            Debug.Log($"[Skills] Prüfung fertig: {errors.Count} Fehler, {warnings.Count} Hinweise.");
    }

    [DidReloadScripts]
    private static void ValidateOnReload()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            ValidateInto(errors, warnings);
        }
        catch (Exception e)
        {
            Debug.LogError("[Skills] Prüfung abgebrochen: " + e.Message);
            return;
        }

        foreach (string err in errors) Debug.LogError("[Skills] " + err);
    }

    private static void ValidateInto(List<string> errors, List<string> warnings)
    {
        var keys = new HashSet<string>();
        var missingIcons = new HashSet<string>();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillBranchDef branch in tree.Branches)
            {
                if (branch.Nodes.Count == 0)
                {
                    warnings.Add($"Ast '{tree.Id}.{branch.Id}' hat keine Knoten.");
                    continue;
                }

                var localIds = new HashSet<string>();
                int roots = 0;

                foreach (SkillNodeDef node in branch.Nodes)
                {
                    if (!localIds.Add(node.LocalId))
                        errors.Add($"Doppelte Knoten-Id '{node.LocalId}' im Ast '{tree.Id}.{branch.Id}'.");

                    if (!keys.Add(node.Key))
                        errors.Add($"Doppelter Schlüssel '{node.Key}' - der Spielstand könnte die " +
                                   "beiden nicht auseinanderhalten.");

                    if (node.IsRoot) roots++;

                    if (node.Price < 0)
                        errors.Add($"'{node.Key}' hat einen negativen Preis.");

                    if (node.Effect == SkillType.None)
                        warnings.Add($"'{node.Key}' hat keinen Effekt (SkillType.None) - der Kauf bewirkt nichts.");

                    if (Mathf.Approximately(node.Value, 0f) && node.Effect != SkillType.UnlockEvoOrWeapon)
                        warnings.Add($"'{node.Key}' hat den Wert 0 - der Kauf bewirkt nichts.");

                    if (!File.Exists(Path.Combine(IconFolder, node.Effect + ".png")))
                        missingIcons.Add(node.Effect.ToString());
                }

                if (roots == 0)
                {
                    errors.Add($"Ast '{tree.Id}.{branch.Id}' hat keine Wurzel - nichts davon wäre " +
                               "je kaufbar. Der erste Knoten muss mit Root(...) angelegt werden.");
                }
            }
        }

        foreach (string effect in missingIcons)
        {
            warnings.Add($"Kein eigenes Symbol {IconFolder}/{effect}.png - für diesen Effekt wird " +
                         "der allgemeine Gesperrt-/Freigeschaltet-Look benutzt.");
        }

        foreach (string special in new[] { SkillIcons.LockedKey, SkillIcons.UnlockedKey })
        {
            if (!File.Exists(Path.Combine(IconFolder, special + ".png")))
                errors.Add($"Pflicht-Symbol {IconFolder}/{special}.png fehlt.");
        }
    }

    [MenuItem("Tools/Skills/Übersicht ausgeben")]
    public static void PrintOverview()
    {
        var sb = new StringBuilder();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            sb.AppendLine($"== Baum '{tree.Id}' ==");

            foreach (SkillBranchDef branch in tree.Branches)
            {
                sb.AppendLine($"  Ast '{branch.Id}' ({branch.Nodes.Count} Knoten)");

                foreach (SkillNodeDef node in branch.Nodes)
                {
                    var req = new List<string>();
                    foreach (SkillNodeDef p in node.Requires) req.Add(p.LocalId);

                    sb.AppendLine($"    [Ebene {node.Depth}] {node.LocalId,-18} {node.Effect,-22} " +
                                  $"{node.Value,-6} {node.Price,4}  <- " +
                                  $"{(req.Count == 0 ? "(Wurzel)" : string.Join(", ", req))}");
                }
            }
        }

        Debug.Log("[Skills]\n" + sb);
    }

    [MenuItem("Tools/Skills/Spielstand zurücksetzen")]
    public static void ResetSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "skills.json");

        bool ok = EditorUtility.DisplayDialog(
            "Skills zurücksetzen",
            "Freigeschaltete Skills und Skillpunkte werden gelöscht.\n\n" + path,
            "Zurücksetzen", "Abbrechen");

        if (!ok) return;

        if (Application.isPlaying) Skills.ResetEverything();
        else if (File.Exists(path)) File.Delete(path);

        Debug.Log("[Skills] Spielstand zurückgesetzt.");
    }
}
