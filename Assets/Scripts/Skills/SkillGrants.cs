using System.Collections.Generic;

/// <summary>
/// DIE LISTE DER BESONDEREN BELOHNUNGEN.
///
/// Ein Skill-Knoten kann zweierlei geben:
///   1. Werte - "+10 Leben". Das sind die Eintraege aus <see cref="SkillType"/>.
///   2. Freischaltungen - "Shurikookie schiesst in alle vier Richtungen". Die stehen hier.
///
/// Eine Freischaltung ist nur eine Id. Das Spiel fragt an der Stelle, wo es darauf
/// ankommt, danach:
///
/// <code>
///   if (Skills.HasGrant(SkillGrants.ShurikookieVierRichtungen))
///   {
///       // vier statt einer Richtung werfen
///   }
/// </code>
///
/// EINE NEUE FREISCHALTUNG ANLEGEN - zwei Schritte, beide hier:
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

    // ------------------------------------------------------------ Wissen

    /// <summary>Bestiarium: +1 % Schaden je 1000 Kills einer Gegnerart (siehe <see cref="Bestiary"/>).</summary>
    public const string Bestiarium = "bestiarium";

    /// <summary>Kartograf: ein Pfeil am Bildrand zeigt zum naechsten unbenutzten Mixer.</summary>
    public const string Kartograf = "kartograf";

    /// <summary>Rage: faellt das Leben unter 30 %, halbieren sich alle Waffen-Cooldowns fuer 5 s (alle 2 min).</summary>
    public const string Rage = "rage";

    // ------------------------------------------------------------ Geist

    /// <summary>Schockwelle: ein Treffer stoesst alle Gegner in der Naehe weg (siehe <see cref="Shockwave"/>).</summary>
    public const string Schockwelle = "schockwelle";

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

        new Def(Bestiarium, "Bestiarium",
                "Zaehlt deine Kills je Gegnerart: +1% Schaden je 1000 Kills. Neuer Reiter im Erfolge-Buch."),

        new Def(Kartograf, "Kartograf",
                "Ein Pfeil am Bildrand zeigt zum naechsten Mixer, den du noch nicht benutzt hast."),

        new Def(Rage, "Rage",
                "Faellt dein Leben unter 30%, feuern alle Waffen 5 Sekunden lang doppelt so schnell (alle 2 Minuten)."),

        new Def(Schockwelle, "Schockwelle",
                "Wirst du getroffen, stoesst eine Welle alle Gegner in der Naehe weg (alle 10 Sekunden)."),
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
        return d != null ? d.Name : (string.IsNullOrEmpty(id) ? "(keine Freischaltung)" : id + " (unbekannt)");
    }

    public static string DescriptionOf(string id)
    {
        Def d = Find(id);
        return d != null ? d.Description : "";
    }
}
