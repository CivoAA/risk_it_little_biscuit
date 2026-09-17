using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Knoten im Skilltree. Unveränderlich und ohne jeden Bezug zur Szene -
/// wo er auf dem Bildschirm landet, rechnet <see cref="SkillTreeLayout"/> aus.
/// </summary>
public sealed class SkillNodeDef
{
    /// <summary>Kurze Id innerhalb des Astes, z.B. "move_speed_2".</summary>
    public readonly string LocalId;

    /// <summary>Vollständiger Schlüssel im Spielstand: "baum.ast.knoten".</summary>
    public string Key { get; internal set; }

    public readonly SkillType Effect;
    public readonly float Value;
    public readonly int Price;

    /// <summary>Optionaler eigener Text. Leer = Text aus dem Effekt bauen.</summary>
    public readonly string DescriptionOverride;

    /// <summary>Optionale feste Position statt Auto-Layout.</summary>
    public readonly Vector2? Position;

    public SkillBranchDef Branch { get; internal set; }

    /// <summary>Knoten, die vorher offen sein müssen. Leer = Wurzel des Astes.</summary>
    public readonly List<SkillNodeDef> Requires = new List<SkillNodeDef>();

    /// <summary>Knoten, die diesen als Voraussetzung haben - für die Linien.</summary>
    public readonly List<SkillNodeDef> Unlocks = new List<SkillNodeDef>();

    /// <summary>Ebene im Ast: Wurzel = 0. Von SkillTreeLayout berechnet.</summary>
    public int Depth { get; internal set; }

    /// <summary>Reihenfolge innerhalb der Ebene. Von SkillTreeLayout berechnet.</summary>
    public int Column { get; internal set; }

    internal SkillNodeDef(string localId, SkillType effect, float value, int price,
                          string description, Vector2? position)
    {
        LocalId             = localId;
        Effect              = effect;
        Value               = value;
        Price               = Mathf.Max(0, price);
        DescriptionOverride = description;
        Position            = position;
    }

    public bool IsRoot => Requires.Count == 0;

    /// <summary>Angezeigter Name. Übersetzbar über skill.[Key].name.</summary>
    public string Name => Loc.Get($"skill.{Key}.name", DefaultName);

    /// <summary>Angezeigte Beschreibung. Übersetzbar über skill.[Key].desc.</summary>
    public string Description =>
        Loc.Get($"skill.{Key}.desc", DescriptionOverride ?? SkillText.Describe(Effect, Value));

    /// <summary>
    /// Notnagel, wenn keine Übersetzung da ist: aus der Id einen lesbaren Namen
    /// bauen ("move_speed_2" -> "Move Speed 2").
    /// </summary>
    private string DefaultName
    {
        get
        {
            string[] parts = LocalId.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }
    }

    public override string ToString() => Key ?? LocalId;
}

/// <summary>Ein Ast des Baums - im Spiel ein eigenes Panel, auf das gezoomt wird.</summary>
public sealed class SkillBranchDef
{
    public readonly string Id;
    public readonly string NameEn;

    /// <summary>Dateiname in Assets/Resources/Skills/ für das Ast-Symbol. Leer = keins.</summary>
    public readonly string IconKey;

    public readonly List<SkillNodeDef> Nodes = new List<SkillNodeDef>();

    public SkillTreeDef Tree { get; internal set; }

    /// <summary>Wie viele Knoten je Ebene - von SkillTreeLayout gefüllt.</summary>
    public Dictionary<int, int> LevelWidths { get; internal set; }

    internal SkillBranchDef(string id, string nameEn, string iconKey)
    {
        Id      = id;
        NameEn  = nameEn;
        IconKey = iconKey;
    }

    public string Name => Loc.Get($"skill.branch.{Id}.name", NameEn);

    public SkillNodeDef Find(string localId) => Nodes.Find(n => n.LocalId == localId);

    public override string ToString() => Id;
}

/// <summary>
/// Ein kompletter Skilltree. Heute gibt es genau einen ("default"); sobald jeder
/// Charakter einen eigenen bekommt, wird pro Charakter einer angelegt und der
/// Spielstand führt sie getrennt.
/// </summary>
public sealed class SkillTreeDef
{
    public readonly string Id;
    public readonly string NameEn;
    public readonly List<SkillBranchDef> Branches = new List<SkillBranchDef>();

    internal SkillTreeDef(string id, string nameEn)
    {
        Id     = id;
        NameEn = nameEn;
    }

    public string Name => Loc.Get($"skill.tree.{Id}.name", NameEn);

    public IEnumerable<SkillNodeDef> AllNodes()
    {
        foreach (SkillBranchDef branch in Branches)
        {
            foreach (SkillNodeDef node in branch.Nodes) yield return node;
        }
    }

    public SkillNodeDef Find(string key)
    {
        foreach (SkillNodeDef node in AllNodes())
        {
            if (node.Key == key) return node;
        }
        return null;
    }

    public override string ToString() => Id;
}
