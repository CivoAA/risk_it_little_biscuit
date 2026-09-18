using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine einzelne Belohnung, so wie sie im Asset liegt. Das Gegenstueck zur
/// Laufzeit ist <see cref="SkillReward"/>.
/// </summary>
[Serializable]
public class SkillRewardData
{
    [Tooltip("Wert = eine Zahl aus der Liste unten. Schalter = ein Eintrag aus SkillGrants.")]
    public SkillRewardKind kind = SkillRewardKind.Wert;

    [Tooltip("Nur bei 'Wert'. Der Startwert kommt aus SkillDefaults und darf ueberschrieben werden.")]
    public SkillType stat = SkillType.IncreaseMaxHealth;

    public float value = 10f;

    [Tooltip("Nur bei 'Schalter'. Die Id aus SkillGrants.")]
    public string grantId = "";

    [Tooltip("Optional. Leer = Text wird aus Effekt und Wert gebaut.")]
    public string descriptionOverride = "";

    public SkillRewardData Copy() => new SkillRewardData
    {
        kind                = kind,
        stat                = stat,
        value               = value,
        grantId             = grantId,
        descriptionOverride = descriptionOverride,
    };

    public SkillReward ToRuntime()
    {
        string desc = string.IsNullOrWhiteSpace(descriptionOverride) ? null : descriptionOverride;

        return kind == SkillRewardKind.Schalter
            ? new SkillReward(grantId, desc)
            : new SkillReward(stat, value, desc);
    }
}

/// <summary>
/// Ein Knoten im Baum, so wie er im Asset liegt.
///
/// Die Id ist der Schluessel im Spielstand (zusammen mit Baum und Kategorie) -
/// NIE nachtraeglich aendern, sonst gilt ein gekaufter Knoten als nicht gekauft.
/// Der Skilltree-Editor vergibt sie beim Anlegen und laesst sie danach in Ruhe.
/// </summary>
[Serializable]
public class SkillNodeData
{
    [Tooltip("Eindeutig innerhalb der Kategorie. Landet im Spielstand - nicht aendern.")]
    public string id = "";

    public SkillCategory category = SkillCategory.Kampf;

    [Tooltip("Oben, Mitte oder Unten - die drei Wege einer Kategorie.")]
    public SkillLane lane = SkillLane.Mitte;

    [Tooltip("Der wievielte Knoten von links. 0 ist der Startknoten der Bahn.")]
    public int step;

    public SkillShape shape = SkillShape.Kreis;

    [Tooltip("Preis in Skillpunkten.")]
    public int price = 10;

    [Tooltip("Steht im Beschreibungsfeld. Leer = aus der Id gebaut.")]
    public string displayName = "";

    [Tooltip("Leer = aus den Belohnungen unten zusammengesetzt.")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("Optionales eigenes Bild. Leer = die Form oben wird gezeichnet.")]
    public Sprite icon;

    [Tooltip("Was der Knoten gibt. Mehrere sind erlaubt.")]
    public List<SkillRewardData> rewards = new List<SkillRewardData>();

    [Tooltip("Ids aus DERSELBEN Kategorie, die vorher gekauft sein muessen. " +
             "Leer = Startknoten. Mehrere = alle davon werden gebraucht.")]
    public List<string> requires = new List<string>();

    public SkillNodeData Copy()
    {
        var copy = new SkillNodeData
        {
            id          = id,
            category    = category,
            lane        = lane,
            step        = step,
            shape       = shape,
            price       = price,
            displayName = displayName,
            description = description,
            icon        = icon,
            requires    = new List<string>(requires),
            rewards     = new List<SkillRewardData>(),
        };

        foreach (SkillRewardData r in rewards) copy.rewards.Add(r.Copy());
        return copy;
    }
}

/// <summary>Farbe und Texte einer Kategorie in genau diesem Baum.</summary>
[Serializable]
public class SkillCategoryData
{
    public SkillCategory category = SkillCategory.Kampf;

    [Tooltip("Steht auf dem Knopf links. Leer = Vorgabe aus SkillCategoryStyle.")]
    public string displayName = "";

    [Tooltip("Ueberschrift ueber dem Baum. Leer = Vorgabe.")]
    public string pathLabel = "";

    [TextArea(2, 4)]
    public string description = "";

    [TextArea(1, 3)]
    public string quote = "";

    public Color color = Color.white;

    [Tooltip("Optional. Leer = eine Scheibe in der Kategoriefarbe.")]
    public Sprite icon;

    /// <summary>Fuellt leer gelassene Felder mit den Vorgaben der Kategorie.</summary>
    public SkillCategoryData WithDefaults()
    {
        SkillCategoryStyle.Style s = SkillCategoryStyle.For(category);

        return new SkillCategoryData
        {
            category    = category,
            displayName = string.IsNullOrWhiteSpace(displayName) ? s.Name : displayName,
            pathLabel   = string.IsNullOrWhiteSpace(pathLabel) ? s.PathLabel : pathLabel,
            description = string.IsNullOrWhiteSpace(description) ? s.Description : description,
            quote       = string.IsNullOrWhiteSpace(quote) ? s.Quote : quote,
            color       = color.a <= 0.001f ? s.Color : color,
            icon        = icon,
        };
    }

    public static SkillCategoryData Default(SkillCategory category)
    {
        SkillCategoryStyle.Style s = SkillCategoryStyle.For(category);

        return new SkillCategoryData
        {
            category    = category,
            displayName = s.Name,
            pathLabel   = s.PathLabel,
            description = s.Description,
            quote       = s.Quote,
            color       = s.Color,
        };
    }
}

/// <summary>
/// DER SKILLTREE EINES CHARAKTERS.
///
/// Ein Asset je Charakter, alle unter Assets/Resources/SkillTrees/. Das Spiel
/// liest sie beim Start ueber <see cref="SkillTrees"/> ein und sucht sich ueber
/// den Charakter-Index den passenden heraus.
///
/// Gebaut wird der Inhalt NICHT hier im Inspector, sondern im Fenster
/// Tools -> Skilltree -> Editor. Dort klickt man auf einen Knoten und haengt den
/// naechsten daran; die Verbindungslinien rechnet das Spiel selbst aus.
///
/// Der Inspector darunter bleibt trotzdem offen - fuer den schnellen Blick in
/// die Rohdaten oder wenn mal etwas geradegerueckt werden muss.
/// </summary>
[CreateAssetMenu(fileName = "SkillTree", menuName = "Risk it/Skilltree", order = 0)]
public class SkillTreeAsset : ScriptableObject
{
    [Tooltip("Schluessel im Spielstand - zum Beispiel char_0. NIE nachtraeglich aendern.")]
    public string treeId = "char_0";

    [Tooltip("Nur fuer den Editor und die Konsole.")]
    public string displayName = "Skilltree";

    [Tooltip("Welcher Charakter diesen Baum bekommt - der Skin-Index aus dem Shop. " +
             "-1 = keinem zugeordnet (Vorlage).")]
    public int characterIndex;

    [Tooltip("Farbe und Texte der vier Kategorien in diesem Baum.")]
    public List<SkillCategoryData> categories = new List<SkillCategoryData>();

    [Tooltip("Alle Knoten aller Kategorien. Der Editor haelt die Liste in Ordnung.")]
    public List<SkillNodeData> nodes = new List<SkillNodeData>();

    /// <summary>Die Einstellungen einer Kategorie, mit Vorgaben aufgefuellt.</summary>
    public SkillCategoryData CategoryOf(SkillCategory category)
    {
        foreach (SkillCategoryData c in categories)
        {
            if (c != null && c.category == category) return c.WithDefaults();
        }
        return SkillCategoryData.Default(category);
    }

    public SkillNodeData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (SkillNodeData n in nodes)
        {
            if (n != null && n.id == id) return n;
        }
        return null;
    }

    /// <summary>Alle Knoten einer Kategorie, von links nach rechts.</summary>
    public List<SkillNodeData> NodesOf(SkillCategory category)
    {
        var result = new List<SkillNodeData>();

        foreach (SkillNodeData n in nodes)
        {
            if (n != null && n.category == category) result.Add(n);
        }

        result.Sort((a, b) => a.step != b.step ? a.step.CompareTo(b.step)
                                               : a.lane.CompareTo(b.lane));
        return result;
    }

    /// <summary>Alle Knoten einer Bahn, von links nach rechts.</summary>
    public List<SkillNodeData> NodesOf(SkillCategory category, SkillLane lane)
    {
        var result = new List<SkillNodeData>();

        foreach (SkillNodeData n in nodes)
        {
            if (n != null && n.category == category && n.lane == lane) result.Add(n);
        }

        result.Sort((a, b) => a.step.CompareTo(b.step));
        return result;
    }

    /// <summary>
    /// Sorgt dafuer, dass alle vier Kategorien eingetragen sind - in der
    /// Reihenfolge aus <see cref="SkillCategory"/>. Ruft der Editor beim Oeffnen.
    /// </summary>
    public bool EnsureCategories()
    {
        var wanted = (SkillCategory[])Enum.GetValues(typeof(SkillCategory));
        bool changed = false;

        var rebuilt = new List<SkillCategoryData>();

        foreach (SkillCategory c in wanted)
        {
            SkillCategoryData existing = null;

            foreach (SkillCategoryData d in categories)
            {
                if (d != null && d.category == c) { existing = d; break; }
            }

            if (existing == null)
            {
                existing = SkillCategoryData.Default(c);
                changed = true;
            }

            rebuilt.Add(existing);
        }

        if (rebuilt.Count != categories.Count) changed = true;

        categories = rebuilt;
        return changed;
    }

    /// <summary>Eine freie Id in der Form kampf_oben_3.</summary>
    public string NextId(SkillCategory category, SkillLane lane)
    {
        string prefix = SkillCategoryStyle.IdOf(category) + "_" +
                        lane.ToString().ToLowerInvariant() + "_";

        for (int i = 1; i < 1000; i++)
        {
            string candidate = prefix + i;
            if (Find(candidate) == null) return candidate;
        }

        return prefix + Guid.NewGuid().ToString("N").Substring(0, 6);
    }

    // ------------------------------------------------------------ Startknoten

    /// <summary>
    /// Die Endung der Start-Id. Jede Kategorie faengt mit genau EINEM Knoten an -
    /// Bahn Mitte, Spalte 0 - und von dem gehen die drei Bahnen weiter. An dieser
    /// Endung erkennen Editor und Spiel ihn wieder.
    /// </summary>
    public const string StartSuffix = "_start";

    public static string StartIdOf(SkillCategory category) =>
        SkillCategoryStyle.IdOf(category) + StartSuffix;

    /// <summary>
    /// Genau eine der vier Start-Ids - nicht einfach "endet auf _start". Sonst
    /// waere ein selbst vergebener Name wie "glueck_oben_start" auf einmal ein
    /// Startknoten und liesse sich weder kaufen noch loeschen.
    /// </summary>
    public static bool IsStartId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        foreach (SkillCategory c in (SkillCategory[])Enum.GetValues(typeof(SkillCategory)))
        {
            if (id == StartIdOf(c)) return true;
        }

        return false;
    }

    /// <summary>Der Startknoten einer Kategorie, oder null solange es keinen gibt.</summary>
    public SkillNodeData StartOf(SkillCategory category) => Find(StartIdOf(category));

    /// <summary>
    /// Sorgt dafuer, dass jede Kategorie ihren Startknoten hat und alles andere
    /// daran haengt. Laeuft beim Oeffnen im Editor, beim Anlegen eines Baums und
    /// nach dem Einlesen einer Textdatei.
    ///
    /// Fuer aeltere Baeume ist das gleichzeitig der Umzug: lag schon etwas in
    /// Spalte 0, rueckt die ganze Kategorie eine Spalte nach rechts, und jeder
    /// Knoten ohne Vorbedingung haengt sich an den neuen Startknoten. Ids und
    /// damit der Spielstand bleiben dabei unangetastet.
    /// </summary>
    public bool EnsureStartNodes()
    {
        bool changed = false;

        foreach (SkillCategory category in (SkillCategory[])Enum.GetValues(typeof(SkillCategory)))
        {
            // Kein |= mit Kurzschluss: jede Kategorie muss wirklich laufen.
            if (EnsureStartNode(category)) changed = true;
        }

        return changed;
    }

    public bool EnsureStartNode(SkillCategory category)
    {
        bool changed = false;
        string startId = StartIdOf(category);
        List<SkillNodeData> inCategory = NodesOf(category);

        // Spalte 0 gehoert dem Startknoten. Steht da etwas anderes, rueckt die
        // ganze Kategorie einen Schritt nach rechts - so bleiben die Abstaende.
        bool clash = false;

        foreach (SkillNodeData n in inCategory)
        {
            if (n.id != startId && n.step == 0) { clash = true; break; }
        }

        if (clash)
        {
            foreach (SkillNodeData n in inCategory)
            {
                if (n.id == startId) continue;
                n.step += 1;
                changed = true;
            }
        }

        SkillNodeData start = Find(startId);

        if (start == null)
        {
            start = NewStart(category);
            nodes.Add(start);
            inCategory.Add(start);
            changed = true;
        }

        // Der Startknoten steht fest: Mitte, Spalte 0, umsonst, ohne Vorbedingung.
        if (start.lane != SkillLane.Mitte) { start.lane = SkillLane.Mitte; changed = true; }
        if (start.step != 0)               { start.step = 0;               changed = true; }
        if (start.price != 0)              { start.price = 0;              changed = true; }
        if (start.requires.Count > 0)      { start.requires.Clear();       changed = true; }
        if (start.rewards.Count > 0)       { start.rewards.Clear();        changed = true; }

        // Alles andere haengt am Start - sonst gaebe es einen zweiten Anfang,
        // der ohne Vorbedingung sofort kaufbar waere.
        foreach (SkillNodeData n in inCategory)
        {
            if (n == start || n.requires.Count > 0) continue;
            n.requires.Add(startId);
            changed = true;
        }

        return changed;
    }

    /// <summary>Ein frischer Startknoten. Er gibt nichts und kostet nichts.</summary>
    public static SkillNodeData NewStart(SkillCategory category)
    {
        return new SkillNodeData
        {
            id          = StartIdOf(category),
            category    = category,
            lane        = SkillLane.Mitte,
            step        = 0,
            shape       = SkillShape.Kreis,
            price       = 0,
            displayName = "Start",
            description = "Hier beginnt der Pfad.",
        };
    }
}
