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

    /// <summary>Shurikookie prallt von einem getroffenen Gegner zum naechsten ab (siehe <see cref="Ricochet"/>).</summary>
    public const string ShurikookieAbpraller = "shurikookie_abpraller";

    /// <summary>
    /// Blade Swarm / Blade Storm: jeder Kunai sucht sich ein eigenes Ziel und
    /// fliegt so lange hindurch und wieder hinein, bis es tot ist (siehe
    /// <see cref="BladeSwarmPrefab"/> und <see cref="BladeStormEvoPrefab"/>).
    /// </summary>
    public const string Schattenschwarm = "onigiri_schattenschwarm";

    /// <summary>Salatfaecher: der erste Gegner, den eine Welle trifft, spaltet zwei kleinere Wellen ab (siehe <see cref="SaladFanWave"/>).</summary>
    public const string Spaltwelle = "toast_spaltwelle";

    /// <summary>Salatfaecher: jeder 5. Schlag geht als Kreis rundherum, mit doppelt so vielen Wellen (siehe <see cref="SaladFan"/>).</summary>
    public const string Sturmboe = "toast_sturmboe";

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

    /// <summary>Kawarimi: unter 50 % Leben 4 s lang 100 % Ausweichen und +50 % Tempo, durchscheinend (alle 35 s).</summary>
    public const string Kawarimi = "kawarimi";

    /// <summary>Klebreis: unter 35 % Leben kleben alle sichtbaren Gegner 8 s fest (alle 35 s, siehe <see cref="StickyRice"/>).</summary>
    public const string Klebreis = "klebreis";

    /// <summary>Wirbelsturm: unter 40 % Leben 5 s lang alle Gegner in der Naehe wegdruecken (alle 35 s, siehe <see cref="Whirlwind"/>).</summary>
    public const string Wirbelsturm = "wirbelsturm";

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
                "Shurikookie fliegt in alle vier Richtungen gleichzeitig. Gilt auch fuer Shuri Blast."),

        new Def(ShurikookieAbpraller, "Shurikookie: Abpraller",
                "Shurikookie zerbricht nicht am ersten Gegner, sondern prallt bis zu zweimal zum naechsten ab. Gilt auch fuer Shuri Blast."),

        new Def(Schattenschwarm, "Schattenschwarm",
                "Jeder Kunai sucht sich ein eigenes Ziel und fliegt immer wieder hindurch, bis es tot ist. Gilt auch fuer Blade Storm."),

        new Def(Spaltwelle, "Spaltwelle",
                "Der erste Gegner, den eine Salatwelle trifft, spaltet zwei kleinere Wellen in entgegengesetzte Richtungen ab."),

        new Def(Sturmboe, "Sturmboe",
                "Jeder 5. Schlag des Salatfaechers geht einmal im Kreis um dich herum - mit doppelt so vielen Wellen."),

        new Def(Bestiarium, "Bestiarium",
                "Zaehlt deine Kills je Gegnerart: +1% Schaden je 1000 Kills. Neuer Reiter im Erfolge-Buch."),

        new Def(Kartograf, "Kartograf",
                "Ein Pfeil am Bildrand zeigt zum naechsten Mixer, den du noch nicht benutzt hast."),

        new Def(Rage, "Rage",
                "Faellt dein Leben unter 30%, feuern alle Waffen 5 Sekunden lang doppelt so schnell (alle 2 Minuten)."),

        new Def(Schockwelle, "Schockwelle",
                "Wirst du getroffen, stoesst eine Welle alle Gegner in der Naehe weg (alle 10 Sekunden)."),

        new Def(Kawarimi, "Kawarimi",
                "Faellt dein Leben unter 50%, wirst du durchscheinend und weichst 4 Sekunden lang allem aus und bist 50% schneller (alle 35 Sekunden)."),

        new Def(Klebreis, "Klebreis",
                "Faellt dein Leben unter 35%, kleben alle Gegner im Bild 8 Sekunden lang fest (alle 35 Sekunden). Bosse nicht."),

        new Def(Wirbelsturm, "Wirbelsturm",
                "Faellt dein Leben unter 40%, wirbelst du 5 Sekunden lang alle Gegner in deiner Naehe weg (alle 35 Sekunden). Bosse nicht."),
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
        return d != null ? Loc.Get($"skill.grant.{d.Id}.name", d.Name) : (string.IsNullOrEmpty(id) ? "(keine Freischaltung)" : id + " (unbekannt)");
    }

    public static string DescriptionOf(string id)
    {
        Def d = Find(id);
        return d != null ? Loc.Get($"skill.grant.{d.Id}.desc", d.Description) : "";
    }
}
