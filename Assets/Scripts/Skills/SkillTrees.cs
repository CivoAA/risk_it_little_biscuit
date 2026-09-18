using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DIE SKILLTREES DES SPIELS.
///
/// Gebaut werden sie nicht mehr hier, sondern in den Assets unter
/// Assets/Resources/SkillTrees/ - ein <see cref="SkillTreeAsset"/> je Charakter.
/// Diese Klasse liest sie beim ersten Zugriff ein und macht daraus die
/// Laufzeitform (<see cref="SkillTreeDef"/>).
///
/// EINEN BAUM ANLEGEN ODER AENDERN:
///   Tools -> Skilltree -> Editor. Dort Baum waehlen oder neu anlegen, Kategorie
///   oben umschalten, auf einen Knoten klicken und mit dem Plus den naechsten
///   dranhaengen.
///
/// EINEN BAUM PER TEXT ANLEGEN (auch fuer Claude):
///   Tools -> Skilltree -> Baum aus Textdatei bauen. Das Format steht in
///   <see cref="SkillTreeTextIO"/>; exportieren geht ueber denselben Weg, damit
///   man einen bestehenden Baum als Vorlage nehmen kann.
///
/// WELCHER BAUM GILT:
///   <see cref="ForCharacter"/> sucht das Asset, dessen characterIndex passt.
///   Findet es keins, bekommt der Charakter den Notbaum von hier - damit steht
///   das Fenster im Hub nie leer da.
///
/// SCHLUESSEL NIEMALS AENDERN: treeId, Kategorie und Knoten-Id landen zusammen
/// als Schluessel in skills.json.
/// </summary>
public static class SkillTrees
{
    /// <summary>Ordner unter Resources, in dem die Baum-Assets liegen.</summary>
    public const string ResourceFolder = "SkillTrees";

    private static readonly List<SkillTreeDef> registry = new List<SkillTreeDef>();
    private static bool loaded;

    public static IReadOnlyList<SkillTreeDef> All
    {
        get
        {
            EnsureLoaded();
            return registry;
        }
    }

    /// <summary>
    /// Der Baum, den ein Charakter ohne eigenes Asset bekommt. Enthaelt nur die
    /// drei Startknoten je Kategorie - genug, damit man sieht, dass etwas fehlt.
    /// </summary>
    public static SkillTreeDef Default
    {
        get
        {
            EnsureLoaded();

            SkillTreeDef found = Find("default");
            return found ?? registry[registry.Count - 1];
        }
    }

    public static SkillTreeDef Find(string id)
    {
        EnsureLoaded();

        foreach (SkillTreeDef t in registry)
        {
            if (t.Id == id) return t;
        }
        return null;
    }

    /// <summary>
    /// Der Baum eines Charakters. Zuerst wird ueber den Charakter-Index gesucht,
    /// danach ueber die Id "char_[index]" - so funktioniert beides.
    /// </summary>
    public static SkillTreeDef ForCharacter(int skinIndex)
    {
        EnsureLoaded();

        foreach (SkillTreeDef t in registry)
        {
            if (t.CharacterIndex == skinIndex) return t;
        }

        return Find($"char_{skinIndex}") ?? Default;
    }

    /// <summary>
    /// Wirft den eingelesenen Stand weg. Ruft der Editor, wenn ein Asset
    /// gespeichert wurde - im Spiel wird das nie gebraucht.
    /// </summary>
    public static void Reload()
    {
        registry.Clear();
        loaded = false;
        EnsureLoaded();
    }

    // ==================================================================
    //  Einlesen
    // ==================================================================

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        SkillTreeAsset[] assets = Resources.LoadAll<SkillTreeAsset>(ResourceFolder);

        if (assets != null)
        {
            // Nach Charakter sortieren, damit die Reihenfolge nicht davon abhaengt,
            // in welcher Reihenfolge Unity die Dateien ausliefert.
            System.Array.Sort(assets, (a, b) =>
            {
                int byIndex = a.characterIndex.CompareTo(b.characterIndex);
                return byIndex != 0 ? byIndex : string.CompareOrdinal(a.treeId, b.treeId);
            });

            foreach (SkillTreeAsset asset in assets)
            {
                SkillTreeDef def = Build(asset);
                if (def != null) registry.Add(def);
            }
        }

        // Immer einen Notbaum anhaengen, damit Default nie null ist.
        registry.Add(BuildFallback());
    }

    /// <summary>Macht aus einem Asset die Laufzeitform.</summary>
    public static SkillTreeDef Build(SkillTreeAsset asset)
    {
        if (asset == null) return null;

        if (string.IsNullOrWhiteSpace(asset.treeId))
        {
            Debug.LogError($"[Skills] Baum-Asset '{asset.name}' hat keine treeId - wird uebersprungen.");
            return null;
        }

        string treeName = string.IsNullOrWhiteSpace(asset.displayName) ? asset.treeId : asset.displayName;
        var tree = new SkillTreeDef(asset.treeId, treeName, asset.characterIndex);

        foreach (SkillCategory category in (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory)))
        {
            SkillCategoryData style = asset.CategoryOf(category);

            var branch = new SkillBranchDef(category, style.displayName, style.pathLabel,
                                            style.description, style.quote, style.color, style.icon)
            {
                Tree = tree,
            };

            tree.Branches.Add(branch);

            // Erst alle Knoten anlegen, dann die Vorbedingungen verdrahten - so
            // darf ein Knoten auch auf einen zeigen, der weiter vorn in der Liste
            // steht (etwa quer von der Bahn darunter).
            var byId = new Dictionary<string, SkillNodeDef>();

            foreach (SkillNodeData data in asset.NodesOf(category))
            {
                if (data == null || string.IsNullOrWhiteSpace(data.id)) continue;

                if (byId.ContainsKey(data.id))
                {
                    Debug.LogError($"[Skills] '{asset.treeId}.{branch.Id}': Knoten-Id '{data.id}' " +
                                   "kommt doppelt vor - der zweite wird verworfen.");
                    continue;
                }

                var rewards = new List<SkillReward>();
                foreach (SkillRewardData r in data.rewards)
                {
                    if (r != null) rewards.Add(r.ToRuntime());
                }

                var node = new SkillNodeDef(data.id, data.lane, data.step, data.shape, data.price,
                                            rewards, data.displayName, data.description, data.icon)
                {
                    Branch = branch,
                    Key    = $"{tree.Id}.{branch.Id}.{data.id}",
                };

                branch.Nodes.Add(node);
                byId[data.id] = node;
            }

            foreach (SkillNodeData data in asset.NodesOf(category))
            {
                if (data == null || !byId.TryGetValue(data.id, out SkillNodeDef node)) continue;

                foreach (string parentId in data.requires)
                {
                    if (string.IsNullOrWhiteSpace(parentId)) continue;

                    if (!byId.TryGetValue(parentId, out SkillNodeDef parent))
                    {
                        Debug.LogError($"[Skills] '{asset.treeId}.{branch.Id}.{data.id}': " +
                                       $"Vorbedingung '{parentId}' gibt es in dieser Kategorie nicht.");
                        continue;
                    }

                    if (parent == node) continue;

                    node.Requires.Add(parent);
                    parent.Unlocks.Add(node);
                }
            }
        }

        SkillTreeLayout.Compute(tree);
        return tree;
    }

    /// <summary>
    /// Der Notbaum: je Kategorie der Startknoten und die drei leeren Bahnen
    /// daran. Er sorgt dafuer, dass das Fenster im Hub etwas anzeigt, solange
    /// noch kein Asset existiert - und dass nichts abstuerzt, wenn eins fehlt.
    /// </summary>
    private static SkillTreeDef BuildFallback()
    {
        var tree = new SkillTreeDef("default", "Skilltree", -1);

        foreach (SkillCategory category in (SkillCategory[])System.Enum.GetValues(typeof(SkillCategory)))
        {
            SkillCategoryStyle.Style s = SkillCategoryStyle.For(category);

            var branch = new SkillBranchDef(category, s.Name, s.PathLabel, s.Description,
                                            s.Quote, s.Color, null)
            {
                Tree = tree,
            };

            tree.Branches.Add(branch);

            SkillNodeDef start = FallbackNode(tree, branch, SkillTreeAsset.StartIdOf(category),
                                              SkillLane.Mitte, 0, 0, "Hier beginnt der Pfad.");

            // Drei leere Bahnen am Start - genau die Form, die ein richtiger Baum
            // auch hat, nur ohne Inhalt.
            foreach (SkillLane lane in (SkillLane[])System.Enum.GetValues(typeof(SkillLane)))
            {
                string id = $"{branch.Id}_{lane.ToString().ToLowerInvariant()}_1";

                SkillNodeDef node = FallbackNode(tree, branch, id, lane, 1, 10,
                                                 "Noch kein Baum fuer diesen Charakter.");

                node.Requires.Add(start);
                start.Unlocks.Add(node);
            }
        }

        SkillTreeLayout.Compute(tree);
        return tree;
    }

    private static SkillNodeDef FallbackNode(SkillTreeDef tree, SkillBranchDef branch, string id,
                                             SkillLane lane, int step, int price, string description)
    {
        var node = new SkillNodeDef(id, lane, step, SkillShape.Kreis, price,
                                    new List<SkillReward>(), null, description, null)
        {
            Branch = branch,
            Key    = $"{tree.Id}.{branch.Id}.{id}",
        };

        branch.Nodes.Add(node);
        return node;
    }
}
