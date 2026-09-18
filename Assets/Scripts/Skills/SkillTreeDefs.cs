using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Knoten im Skilltree, wie ihn das Spiel sieht. Unveraenderlich und ohne
/// jeden Bezug zur Szene - wo er auf dem Bildschirm landet, rechnet
/// <see cref="SkillTreeLayout"/> aus Bahn und Schritt aus.
///
/// Gebaut wird er aus einem <see cref="SkillNodeData"/> im Baum-Asset. Wer etwas
/// aendern will, aendert das Asset (Tools -> Skilltree -> Editor), nicht diese Klasse.
/// </summary>
public sealed class SkillNodeDef
{
    /// <summary>Kurze Id innerhalb der Kategorie, z.B. "kampf_oben_2".</summary>
    public readonly string LocalId;

    /// <summary>Vollstaendiger Schluessel im Spielstand: "baum.kategorie.knoten".</summary>
    public string Key { get; internal set; }

    /// <summary>Oben, Mitte oder Unten.</summary>
    public readonly SkillLane Lane;

    /// <summary>Der wievielte Knoten von links. 0 = Startknoten der Bahn.</summary>
    public readonly int Step;

    public readonly SkillShape Shape;

    public readonly int Price;

    /// <summary>Eigenes Bild statt der gezeichneten Form. Darf null sein.</summary>
    public readonly Sprite Icon;

    /// <summary>Was der Knoten gibt. Nie null, darf aber leer sein.</summary>
    public readonly IReadOnlyList<SkillReward> Rewards;

    private readonly string nameOverride;
    private readonly string descriptionOverride;

    public SkillBranchDef Branch { get; internal set; }

    /// <summary>Knoten, die vorher offen sein muessen. Leer = Startknoten.</summary>
    public readonly List<SkillNodeDef> Requires = new List<SkillNodeDef>();

    /// <summary>Knoten, die diesen als Voraussetzung haben - fuer die Linien.</summary>
    public readonly List<SkillNodeDef> Unlocks = new List<SkillNodeDef>();

    internal SkillNodeDef(string localId, SkillLane lane, int step, SkillShape shape, int price,
                          IReadOnlyList<SkillReward> rewards, string name, string description,
                          Sprite icon)
    {
        LocalId             = localId;
        Lane                = lane;
        Step                = Mathf.Max(0, step);
        Shape               = shape;
        Price               = Mathf.Max(0, price);
        Rewards             = rewards ?? new List<SkillReward>();
        nameOverride        = name;
        descriptionOverride = description;
        Icon                = icon;
    }

    public bool IsRoot => Requires.Count == 0;

    /// <summary>
    /// Der Anfang einer Kategorie: der eine Knoten ganz links, an dem die drei
    /// Bahnen haengen. Er ist immer offen und kostet nichts - gekauft wird erst
    /// das, was danach kommt.
    /// </summary>
    public bool IsStart => SkillTreeAsset.IsStartId(LocalId);

    /// <summary>Angezeigter Name. Uebersetzbar ueber skill.[Key].name.</summary>
    public string Name => Loc.Get($"skill.{Key}.name", DefaultName);

    /// <summary>
    /// Angezeigte Beschreibung: der eigene Text, sonst eine Zeile je Belohnung.
    /// Uebersetzbar ueber skill.[Key].desc.
    /// </summary>
    public string Description => Loc.Get($"skill.{Key}.desc", DefaultDescription);

    /// <summary>
    /// Der erste Werteffekt des Knotens. Nur noch fuer alte Stellen da, die genau
    /// einen Effekt erwarten (Symbolsuche, Katalogpruefung) - gerechnet wird
    /// ueber <see cref="Rewards"/>.
    /// </summary>
    public SkillType Effect
    {
        get
        {
            foreach (SkillReward r in Rewards)
            {
                if (r.IsStat) return r.Stat;
            }
            return SkillType.None;
        }
    }

    /// <summary>Der Wert zu <see cref="Effect"/>.</summary>
    public float Value
    {
        get
        {
            foreach (SkillReward r in Rewards)
            {
                if (r.IsStat) return r.Value;
            }
            return 0f;
        }
    }

    /// <summary>Gibt der Knoten irgendeinen Schalter aus <see cref="SkillGrants"/>?</summary>
    public bool HasGrants
    {
        get
        {
            foreach (SkillReward r in Rewards)
            {
                if (r.IsGrant) return true;
            }
            return false;
        }
    }

    private string DefaultDescription
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(descriptionOverride)) return descriptionOverride;
            if (Rewards.Count == 0) return "";

            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < Rewards.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Rewards[i].Describe());
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Notnagel, wenn kein Name eingetragen und keine Uebersetzung da ist: aus der
    /// Id einen lesbaren Namen bauen ("kampf_oben_2" -> "Kampf Oben 2").
    /// </summary>
    private string DefaultName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(nameOverride)) return nameOverride;

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

/// <summary>
/// Eine Kategorie eines Baums - im Spiel einer der vier Knoepfe links und das
/// Feld daneben. Frueher hiess das "Ast"; die Ast-Id ist weiter der mittlere
/// Teil des Spielstand-Schluessels.
/// </summary>
public sealed class SkillBranchDef
{
    public readonly string Id;
    public readonly SkillCategory Category;

    /// <summary>Name auf dem Knopf links.</summary>
    public readonly string NameEn;

    /// <summary>Ueberschrift ueber dem Baum, z.B. "KAMPFPFAD".</summary>
    public readonly string PathLabel;

    public readonly string DescriptionEn;
    public readonly string QuoteEn;
    public readonly Color Color;

    /// <summary>Symbol auf dem Knopf. Darf null sein - dann eine Scheibe in Astfarbe.</summary>
    public readonly Sprite Icon;

    /// <summary>Dateiname in Assets/Resources/Skills/ fuer das Ast-Symbol. Leer = keins.</summary>
    public readonly string IconKey;

    public readonly List<SkillNodeDef> Nodes = new List<SkillNodeDef>();

    public SkillTreeDef Tree { get; internal set; }

    /// <summary>Der groesste Schritt in dieser Kategorie - wie weit man scrollen kann.</summary>
    public int MaxStep { get; internal set; }

    internal SkillBranchDef(SkillCategory category, string nameEn, string pathLabel,
                            string description, string quote, Color color, Sprite icon,
                            string iconKey = null)
    {
        Category      = category;
        Id            = SkillCategoryStyle.IdOf(category);
        NameEn        = nameEn;
        PathLabel     = pathLabel;
        DescriptionEn = description;
        QuoteEn       = quote;
        Color         = color;
        Icon          = icon;
        IconKey       = iconKey;
    }

    public string Name => Loc.Get($"skill.branch.{Id}.name", NameEn);
    public string Path => Loc.Get($"skill.branch.{Id}.path", PathLabel);
    public string Description => Loc.Get($"skill.branch.{Id}.desc", DescriptionEn);
    public string Quote => Loc.Get($"skill.branch.{Id}.quote", QuoteEn);

    public SkillNodeDef Find(string localId) => Nodes.Find(n => n.LocalId == localId);

    /// <summary>Die Knoten einer Bahn, von links nach rechts.</summary>
    public List<SkillNodeDef> InLane(SkillLane lane)
    {
        var result = new List<SkillNodeDef>();

        foreach (SkillNodeDef n in Nodes)
        {
            if (n.Lane == lane) result.Add(n);
        }

        result.Sort((a, b) => a.Step.CompareTo(b.Step));
        return result;
    }

    public override string ToString() => Id;
}

/// <summary>
/// Ein kompletter Skilltree - einer je Charakter. Welcher gerade gilt, bestimmt
/// <see cref="Skills.ActiveTree"/>.
/// </summary>
public sealed class SkillTreeDef
{
    public readonly string Id;
    public readonly string NameEn;

    /// <summary>Skin-Index des Charakters. -1 = keinem zugeordnet.</summary>
    public readonly int CharacterIndex;

    public readonly List<SkillBranchDef> Branches = new List<SkillBranchDef>();

    internal SkillTreeDef(string id, string nameEn, int characterIndex)
    {
        Id             = id;
        NameEn         = nameEn;
        CharacterIndex = characterIndex;
    }

    public string Name => Loc.Get($"skill.tree.{Id}.name", NameEn);

    public IEnumerable<SkillNodeDef> AllNodes()
    {
        foreach (SkillBranchDef branch in Branches)
        {
            foreach (SkillNodeDef node in branch.Nodes) yield return node;
        }
    }

    public SkillBranchDef Branch(SkillCategory category)
    {
        foreach (SkillBranchDef b in Branches)
        {
            if (b.Category == category) return b;
        }
        return null;
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
