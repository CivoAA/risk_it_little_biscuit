using UnityEngine;

/// <summary>
/// Die vier Unterkategorien, die jeder Charakter in seinem Skilltree hat.
///
/// DIE REIHENFOLGE HIER IST DIE REIHENFOLGE IM SPIEL - die Knoepfe links im
/// Skilltree-Fenster stehen genau so untereinander. Wer umsortiert, sortiert
/// damit auch die Oberflaeche um.
///
/// Eine fuenfte Kategorie anhaengen: hier einen Wert ergaenzen und in
/// <see cref="SkillCategoryStyle"/> Farbe und Texte dazuschreiben. Alles andere -
/// Knoepfe, Bahnen, Editor-Reiter - richtet sich danach von selbst.
///
/// Die Namen landen NICHT im Spielstand (dort stehen die Ast-Ids aus dem Asset),
/// umbenennen ist also gefahrlos.
/// </summary>
public enum SkillCategory
{
    Kampf,
    Geist,
    Wissen,
    Glueck,
}

/// <summary>
/// Die drei Bahnen, in denen eine Kategorie nach rechts waechst - genau wie im
/// Konzeptbild: ein Weg nach oben, einer geradeaus, einer nach unten.
/// </summary>
public enum SkillLane
{
    Oben,
    Mitte,
    Unten,
}

/// <summary>
/// Die Form eines Knotens. Reine Optik - sie sagt nichts darueber aus, was der
/// Knoten gibt, und darf jederzeit geaendert werden.
///
/// Solange es keine gemalten Symbole gibt, zeichnet
/// <see cref="SkillShapeSprites"/> jede Form pixelgenau zur Laufzeit. Wer spaeter
/// echte Assets hat, traegt sie im Knoten als eigenes Symbol ein - das gewinnt
/// dann ueber die Form.
/// </summary>
public enum SkillShape
{
    Kreis,
    Rechteck,
    Raute,
    Stern,
    Sechseck,
    Dreieck,
    Kreuz,
}

/// <summary>
/// Vorgaben je Kategorie: Farbe, Ueberschrift, Beschreibung, Spruch.
///
/// Das sind nur die Startwerte. Sobald ein Baum angelegt ist, stehen seine
/// eigenen Texte im Asset und gewinnen - so kann der Kampfpfad bei jedem
/// Charakter anders heissen und anders beschrieben sein.
/// </summary>
public static class SkillCategoryStyle
{
    public struct Style
    {
        public string Name;
        public string PathLabel;
        public string Description;
        public string Quote;
        public Color Color;
    }

    public static Style For(SkillCategory category)
    {
        switch (category)
        {
            case SkillCategory.Kampf:
                return new Style
                {
                    Name        = "KAMPF",
                    PathLabel   = "KAMPFPFAD",
                    Description = "Schaerfe deine Waffen und triff haerter, wo es weh tut.",
                    Quote       = "\"Angriff ist die beste Verteidigung.\"",
                    Color       = new Color32(0xB2, 0x41, 0x41, 0xFF),
                };

            case SkillCategory.Geist:
                return new Style
                {
                    Name        = "GEIST",
                    PathLabel   = "GEISTPFAD",
                    Description = "Meistere die Kraefte in dir und halte laenger durch als alle anderen.",
                    Quote       = "\"Der Geist ueberdauert.\"",
                    Color       = new Color32(0x3C, 0x6F, 0xC0, 0xFF),
                };

            case SkillCategory.Wissen:
                return new Style
                {
                    Name        = "WISSEN",
                    PathLabel   = "WISSENSPFAD",
                    Description = "Lerne schneller, waehle klueger und hole aus jedem Lauf mehr heraus.",
                    Quote       = "\"Wissen veraendert die Welt.\"",
                    Color       = new Color32(0x3E, 0x8F, 0x4F, 0xFF),
                };

            case SkillCategory.Glueck:
                return new Style
                {
                    Name        = "GLÜCK",
                    PathLabel   = "GLÜCKSPFAD",
                    Description = "Bessere Funde, seltenere Beute und der eine Wurf, der alles dreht.",
                    Quote       = "\"Glueck ist kein Zufall.\"",
                    Color       = new Color32(0xC8, 0x9A, 0x2C, 0xFF),
                };

            default:
                return new Style
                {
                    Name        = category.ToString().ToUpperInvariant(),
                    PathLabel   = category.ToString().ToUpperInvariant() + "PFAD",
                    Description = "",
                    Quote       = "",
                    Color       = Color.white,
                };
        }
    }

    /// <summary>Kleinbuchstaben-Id, wie sie als Ast-Id im Spielstand landet.</summary>
    public static string IdOf(SkillCategory category) => category.ToString().ToLowerInvariant();
}
