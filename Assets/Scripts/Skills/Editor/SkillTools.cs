using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um die Skilltrees. Alles unter Tools -> Skilltree.
/// Die Pruefung laeuft ausserdem automatisch nach jedem Compile.
///
/// Gebaut werden die Baeume im Fenster (Tools -> Skilltree -> Editor) oder als
/// Text (<see cref="SkillTreeTextIO"/>) - hier steht nur, was danach schiefgehen
/// kann.
/// </summary>
public static class SkillTools
{
    private const string IconFolder = "Assets/Resources/Skills";

    [MenuItem("Tools/Skilltree/Baeume pruefen")]
    public static void Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        SkillTrees.Reload();
        ValidateInto(errors, warnings);

        foreach (string w in warnings) Debug.LogWarning("[Skills] " + w);
        foreach (string e in errors) Debug.LogError("[Skills] " + e);

        int nodes = 0;
        foreach (SkillTreeDef t in SkillTrees.All)
        {
            foreach (SkillNodeDef unused in t.AllNodes()) nodes++;
        }

        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log($"[Skills] Alles in Ordnung - {SkillTrees.All.Count} Baum/Baeume, {nodes} Knoten.");
        else
            Debug.Log($"[Skills] Pruefung fertig: {errors.Count} Fehler, {warnings.Count} Hinweise.");
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
            Debug.LogError("[Skills] Pruefung abgebrochen: " + e.Message);
            return;
        }

        foreach (string err in errors) Debug.LogError("[Skills] " + err);
    }

    private static void ValidateInto(List<string> errors, List<string> warnings)
    {
        var keys = new HashSet<string>();
        var treeIds = new HashSet<string>();
        var characters = new Dictionary<int, string>();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            if (!treeIds.Add(tree.Id))
            {
                errors.Add($"Es gibt zwei Baeume mit der Id '{tree.Id}' - im Spielstand waeren " +
                           "ihre Knoten nicht auseinanderzuhalten.");
            }

            // Der Notbaum hat absichtlich keinen Charakter und keine Inhalte.
            bool isFallback = tree.Id == "default";

            if (!isFallback && tree.CharacterIndex >= 0)
            {
                if (characters.TryGetValue(tree.CharacterIndex, out string other))
                {
                    errors.Add($"Charakter {tree.CharacterIndex} hat zwei Baeume ('{other}' und " +
                               $"'{tree.Id}') - es gilt immer nur einer.");
                }
                else
                {
                    characters[tree.CharacterIndex] = tree.Id;
                }
            }

            foreach (SkillBranchDef branch in tree.Branches)
            {
                if (branch.Nodes.Count == 0)
                {
                    if (!isFallback)
                        warnings.Add($"'{tree.Id}.{branch.Id}' hat keine Knoten.");
                    continue;
                }

                ValidateBranch(tree, branch, keys, errors, warnings, isFallback);
            }
        }

        // Charaktere ohne eigenen Baum: kein Fehler, aber gut zu wissen.
        for (int i = 0; i < Characters.Count; i++)
        {
            if (characters.ContainsKey(i)) continue;

            warnings.Add($"Charakter {i} hat keinen eigenen Skilltree - er bekommt den Notbaum. " +
                         "Anlegen unter Tools -> Skilltree -> Editor.");
        }

        foreach (string special in new[] { SkillIcons.LockedKey, SkillIcons.UnlockedKey })
        {
            if (!File.Exists(Path.Combine(IconFolder, special + ".png")))
            {
                warnings.Add($"Symbol {IconFolder}/{special}.png fehlt. Der Hub zeichnet seine " +
                             "Formen selbst, gebraucht wird es nur von der alten World-Map-Ansicht.");
            }
        }
    }

    private static void ValidateBranch(SkillTreeDef tree, SkillBranchDef branch,
                                       HashSet<string> keys, List<string> errors,
                                       List<string> warnings, bool isFallback)
    {
        var localIds = new HashSet<string>();
        var occupied = new HashSet<string>();
        bool hasStart = false;

        foreach (SkillNodeDef node in branch.Nodes)
        {
            if (!localIds.Add(node.LocalId))
                errors.Add($"Doppelte Knoten-Id '{node.LocalId}' in '{tree.Id}.{branch.Id}'.");

            if (!keys.Add(node.Key))
                errors.Add($"Doppelter Schluessel '{node.Key}' - der Spielstand koennte die " +
                           "beiden nicht auseinanderhalten.");

            string slot = node.Lane + ":" + node.Step;

            if (!occupied.Add(slot))
                errors.Add($"'{node.Key}' liegt auf demselben Platz wie ein anderer Knoten " +
                           $"({node.Lane}, Spalte {node.Step}) - im Hub liegen sie uebereinander.");

            if (node.Price < 0)
                errors.Add($"'{node.Key}' hat einen negativen Preis.");

            if (node.IsStart) hasStart = true;

            // Jede Kategorie hat genau einen Anfang: ihren Startknoten. Wer sonst
            // ohne Voraussetzung dasteht, waere ein zweiter - im Hub sofort kaufbar
            // und ohne Linie zum Rest.
            if (node.IsRoot && !node.IsStart)
            {
                warnings.Add($"'{node.Key}' haengt an nichts. Er sollte '{SkillTreeAsset.StartIdOf(branch.Category)}' " +
                             "als Voraussetzung haben - oder einen Knoten, der daran haengt.");
            }

            ValidateRewards(node, errors, warnings, isFallback);
            ValidateRequirements(node, errors);
        }

        if (!hasStart && !isFallback)
        {
            errors.Add($"'{tree.Id}.{branch.Id}' hat keinen Startknoten "  +
                       $"('{SkillTreeAsset.StartIdOf(branch.Category)}') - nichts davon waere je kaufbar. " +
                       "Der Editor legt ihn beim Oeffnen des Baums an.");
        }
    }

    private static void ValidateRewards(SkillNodeDef node, List<string> errors,
                                        List<string> warnings, bool isFallback)
    {
        if (node.Rewards.Count == 0)
        {
            // Der Startknoten gibt absichtlich nichts - er wird ja auch nicht gekauft.
            if (!isFallback && !node.IsStart)
                warnings.Add($"'{node.Key}' gibt nichts - der Kauf bewirkt nichts.");
            return;
        }

        foreach (SkillReward reward in node.Rewards)
        {
            if (reward.IsGrant)
            {
                if (string.IsNullOrWhiteSpace(reward.GrantId))
                {
                    errors.Add($"'{node.Key}' hat einen Schalter ohne Id.");
                }
                else if (SkillGrants.Find(reward.GrantId) == null)
                {
                    warnings.Add($"'{node.Key}' gibt den Schalter '{reward.GrantId}', den es in " +
                                 "SkillGrants nicht gibt - im Spiel fragt ihn dann niemand ab.");
                }

                continue;
            }

            if (reward.Stat == SkillType.None)
            {
                warnings.Add($"'{node.Key}' hat einen Effekt ohne Wirkung (SkillType.None).");
                continue;
            }

            if (Mathf.Approximately(reward.Value, 0f) && reward.Stat != SkillType.UnlockEvoOrWeapon)
                warnings.Add($"'{node.Key}': {reward.Stat} steht auf 0 - das bewirkt nichts.");
        }
    }

    private static void ValidateRequirements(SkillNodeDef node, List<string> errors)
    {
        foreach (SkillNodeDef parent in node.Requires)
        {
            // Ein Vorgaenger, der weiter rechts steht, ergibt eine Linie, die
            // rueckwaerts laeuft - sichtbar krumm, also ein Fehler.
            if (parent.Step >= node.Step)
            {
                errors.Add($"'{node.Key}' braucht '{parent.LocalId}', der genauso weit rechts " +
                           "oder weiter rechts steht. Voraussetzungen muessen links stehen.");
            }
        }
    }

    [MenuItem("Tools/Skilltree/Uebersicht ausgeben")]
    public static void PrintOverview()
    {
        SkillTrees.Reload();

        var sb = new StringBuilder();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            sb.AppendLine($"== '{tree.Id}' ({tree.NameEn}), Charakter {tree.CharacterIndex} ==");

            foreach (SkillBranchDef branch in tree.Branches)
            {
                sb.AppendLine($"  {branch.Id} ({branch.Nodes.Count} Knoten, bis Spalte {branch.MaxStep})");

                foreach (SkillNodeDef node in branch.Nodes)
                {
                    var req = new List<string>();
                    foreach (SkillNodeDef p in node.Requires) req.Add(p.LocalId);

                    var gives = new List<string>();
                    foreach (SkillReward r in node.Rewards) gives.Add(r.ToString());

                    sb.AppendLine($"    [{node.Lane,-5} {node.Step,2}] {node.LocalId,-20} " +
                                  $"{node.Shape,-9} {node.Price,4} SP  " +
                                  $"{string.Join(" + ", gives),-40} <- " +
                                  $"{(req.Count == 0 ? "(Start)" : string.Join(", ", req))}");
                }
            }
        }

        Debug.Log("[Skills]\n" + sb);
    }

    [MenuItem("Tools/Skilltree/Spielstand zuruecksetzen")]
    public static void ResetSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "skills.json");

        bool ok = EditorUtility.DisplayDialog(
            "Skills zuruecksetzen",
            "Freigeschaltete Skills und Skillpunkte werden geloescht.\n\n" + path,
            "Zuruecksetzen", "Abbrechen");

        if (!ok) return;

        if (Application.isPlaying) Skills.ResetEverything();
        else if (File.Exists(path)) File.Delete(path);

        Debug.Log("[Skills] Spielstand zurueckgesetzt.");
    }
}
