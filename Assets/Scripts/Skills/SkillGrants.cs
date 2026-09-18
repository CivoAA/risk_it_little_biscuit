using System.Collections.Generic;

/// <summary>
/// DIE LISTE DER BESONDEREN BELOHNUNGEN.
///
/// Ein Skill-Knoten kann zweierlei geben:
///   1. Werte - "+10 Leben". Das sind die Eintraege aus <see cref="SkillType"/>.
///   2. Schalter - "Shurikookie schiesst in alle vier Richtungen". Die stehen hier.
///
/// Ein Schalter ist nur eine Id. Das Spiel fragt an der Stelle, wo es darauf
/// ankommt, danach:
///
/// <code>
///   if (Skills.HasGrant(SkillGrants.ShurikookieVierRichtungen))
///   {
///       // vier statt einer Richtung werfen
///   }
/// </code>
///
/// EINEN NEUEN SCHALTER ANLEGEN - zwei Schritte, beide hier:
///   1. Eine Konstante ergaenzen (der Text darin ist der Schluessel; er landet
///      im Baum-Asset, also NIE nachtraeglich aendern).
///   2. Eine Zeile in <see cref="All"/> dazu - damit steht sie im Skilltree-Editor
///      im Aufklappmenue und hat dort einen lesbaren Namen.
///
/// Danach die Abfrage an der passenden Stelle im Spiel einbauen. Mehr ist es nicht.
/// </summary>
public static class SkillGrants
{
    // ------------------------------------------------------------ Waffen

    /// <summary>Shurikookie wirft in alle vier Richtungen statt nur nach vorn.</summary>
    public const string ShurikookieVierRichtungen = "shurikookie_vier_richtungen";

    /// <summary>Shurikookie prallt an Waenden ab.</summary>
    public const string ShurikookieAbpraller = "shurikookie_abpraller";

    // ------------------------------------------------------------ Beispiele
    // Die hier sind nur Platzhalter, damit im Editor nicht nur eine Zeile steht.
    // Loeschen, sobald du eigene hast.

    /// <summary>Ein zweites Leben je Lauf.</summary>
    public const string ZweitesLeben = "zweites_leben";

    /// <summary>Die erste Truhe in jedem Lauf ist umsonst.</summary>
    public const string ErsteTruheGratis = "erste_truhe_gratis";

    // ==================================================================
    //  Der Katalog - was hier steht, steht im Editor zur Auswahl.
    // ==================================================================

    public sealed class Def
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;

        public Def(string id, string name, string description)
        {
            Id          = id;
            Name        = name;
            Description = description;
        }
    }

    public static readonly IReadOnlyList<Def> All = new List<Def>
    {
        new Def(ShurikookieVierRichtungen, "Shurikookie: Vier Richtungen",
                "Shurikookie fliegt in alle vier Richtungen gleichzeitig."),

        new Def(ShurikookieAbpraller, "Shurikookie: Abpraller",
                "Shurikookie prallt an Waenden ab, statt zu zerbrechen."),

        new Def(ZweitesLeben, "Zweites Leben",
                "Einmal je Lauf stehst du mit halbem Leben wieder auf."),

        new Def(ErsteTruheGratis, "Erste Truhe gratis",
                "Die erste Truhe in jedem Lauf kostet nichts."),
    };

    /// <summary>Eintrag zu einer Id. Null, wenn die Id nicht (mehr) im Katalog steht.</summary>
    public static Def Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (Def d in All)
        {
            if (d.Id == id) return d;
        }
        return null;
    }

    /// <summary>
    /// Lesbarer Name fuer eine Id. Steht sie nicht mehr im Katalog, kommt die Id
    /// selbst zurueck - dann sieht man im Editor sofort, dass etwas fehlt, statt
    /// eine leere Zeile zu haben.
    /// </summary>
    public static string NameOf(string id)
    {
        Def d = Find(id);
        return d != null ? d.Name : (string.IsNullOrEmpty(id) ? "(kein Schalter)" : id + " (unbekannt)");
    }

    public static string DescriptionOf(string id)
    {
        Def d = Find(id);
        return d != null ? d.Description : "";
    }
}
