using UnityEngine;

/// <summary>
/// Die Wellenplaene - einer je Karte. Das ist der Ort fuers Balancing.
///
/// Gelesen wird ein Plan von oben nach unten: Phasen der Reihe nach, in jeder
/// Phase laeuft der Grunddruck von <c>Pressure(von, bis)</c> hoch, und an den
/// Beats passiert etwas Besonderes. "Druck" ist keine Stueckzahl, sondern die
/// Summe der Gewichte aus <see cref="EnemyCatalog.Threat"/>: Druck 60 sind 60
/// Marshmellos, gut 20 Eicheln oder 8 Fettis. Der
/// <see cref="SpawnDirector"/> fuellt auf diesen Wert auf und haelt ihn.
///
/// Warum nicht mehr wie frueher "spawne 150 Stueck alle 0,7s": weil jede
/// Aenderung an der Spielerstaerke damit die ganze Zeitleiste verschoben hat.
/// Ueber den Druck reguliert sich das von selbst - wer schneller raeumt,
/// bekommt schneller Nachschub, statt in einer leeren Karte zu stehen.
///
/// Zum Anpassen gibt es die Wellenplan-Werkstatt (Tools -> Gegner ->
/// Wellenplaene). Sie schreibt den Block zwischen den beiden Markern neu;
/// alles ausserhalb bleibt stehen.
///
///   - zu leicht/zu schwer insgesamt  -> Chaos (RunDifficulty) oder Pressure
///   - zu voll/zu leer auf dem Bild   -> Pressure
///   - langweilig                     -> Beats: mehr Burst/Encircle, mehr Calm
///   - falsche Gegner                 -> Pool(...)
///
/// Jede Welt der Route ist schwerer als die davor - der Spieler wird ja ueber
/// Skilltree und Shop staerker. Alle Plaene haben dieselben Phasenlaengen
/// (Miniboss um 10:00, Boss um 20:00), der Druck ist die Wald-Kurve mal
/// einem Faktor:
///
///   Welt 1  Wald         World2   x 1.0   (Massstab)
///   Welt 2  Kueche       World1   x 1.25
///   Welt 3  Eisgletscher World5   x 1.55
///   Welt 4  Geisterwald  World6   x 1.9
///   Welt 5  Vulkan       World4   x 2.3
///
/// Dazu werden die Gegner selbst zaeher und schlagen haerter
/// (<see cref="RunDifficulty.WorldHealth"/> / <see cref="RunDifficulty.WorldDamage"/>).
/// Welt 4 und 5 sollen ohne Skilltree und Shop nicht zu schaffen sein.
/// </summary>
public static class WavePlans
{
    /// <summary>
    /// Holt den Plan zur Karte. Unbekannt = der Plan von World1.
    ///
    /// Gespielt werden zurzeit World1 (Kueche, Karte 1, Szene Map_World0),
    /// World2 (Wald, Karte 2, Szene Map_World3 - planId dort fest eingetragen)
    /// World5 (Eisgletscher, Karte 3, Szene Map_World5 - kommt ueber den
    /// mapName der Karte, die planId dort ist leer), World6 (Geisterwald,
    /// Karte 4, Szene Map_World6 - ebenso ueber den mapName) und World4
    /// (Vulkanwelt, Karte 5, Szene Map_World4 - ebenso ueber den mapName).
    /// </summary>
    public static RunPlan For(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world2": return World2();
            case "world5": return World5();
            case "world6": return World6();
            case "world4": return World4();
            default: return World1();
        }
    }

    /// <summary>Alle Plaene, die es gibt - fuer die Werkstatt und den Vergleich.</summary>
    public static readonly string[] AllIds = { "World1", "World2", "World5", "World6", "World4" };

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================

    // ------------------------------------------------------------- World1

    /// <summary>
    /// Kueche - Welt 2 der Route (nach dem Wald). 20 Minuten, Boss bei 20:00,
    /// Druck = Wald x 1.25 (siehe Kopf von WavePlans). Nur aktive Kuechengegner:
    ///
    ///   Marshmello, Boeser Slime, Muffin, Pfannkuchen,
    ///   Popcorn (Schwarm, ab 0:05), Wurst-Dackel + Spiegelei (ab 3:00),
    ///   Erbsenschote (ab 9:00), Kaesekeil + Spaghetti-Monster (ab 14:00),
    ///   Elite-Marshmello (Elite, immer einzeln), ab 8:00 die Kerzentorte
    ///   (zweite Elite, zaeher, "TORTENALARM!"), um 10:00 der Milchpanzer
    ///   (Zwischenboss) und der Keks-Koenig (Boss)
    ///
    /// Ablauf:
    ///
    ///   0:00  Anlauf, ~3 Marshmellos (5 s)
    ///   0:05  Marshmellos, erster Ring mit Elite-Marshmello
    ///   3:00  + Boeser Slime, 8:00 Ring mit der ersten Kerzentorte
    ///   9:00  + Muffin, 10:00 Milchpanzer (Zwischenboss)
    ///  14:00  + Pfannkuchen
    ///  20:00  Keks-Koenig
    /// </summary>
    public static RunPlan World1()
    {
        var plan = new RunPlan("World1");

        plan.Phase(5f)
            .Pool(EnemyId.Marshmello, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Marshmello, 80f)
            .Pool(EnemyId.Popcorn, 20f)
            .Pressure(15f, 49f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Marshmello, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.EvilSlime, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteMarshmello, EnemyId.Marshmello, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(125f, EnemyId.Popcorn, 12f, Patterns.Cluster, 0f)
            .Burst(150f, EnemyId.Marshmello, 12f, Patterns.Column, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Marshmello, 40f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pool(EnemyId.Wurstdackel, 15f)
            .Pool(EnemyId.Spiegelei, 15f)
            .Pressure(49f, 100f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.EvilSlime, 10f, Patterns.Arc, 0f)
            .Burst(45f, EnemyId.Wurstdackel, 12f, Patterns.Column, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.EvilSlime, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteMarshmello, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Marshmello, 18f, Patterns.Scatter, 0f)
            .Burst(175f, EnemyId.Spiegelei, 12f, Patterns.Cluster, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.EvilSlime, 18f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Marshmello, 20f, Patterns.Arc, 0f)
            .Encircle(300f, EnemyId.Kerzentorte, EnemyId.Wurstdackel, 16, 14f, false, "TORTENALARM!", 0.35f, 25f, 1.5f)
            .Burst(330f, EnemyId.EvilSlime, 20f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Marshmello, 10f)
            .Pool(EnemyId.EvilSlime, 25f)
            .Pool(EnemyId.Muffin, 25f)
            .Pool(EnemyId.Erbsenschote, 20f)
            .Pool(EnemyId.Wurstdackel, 10f)
            .Pool(EnemyId.Popcorn, 10f)
            .Pressure(125f, 250f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Muffin, 16f, Patterns.Ambush, 0f)
            .Boss(60f, EnemyId.Milchpanzer, "MILCHPANZER!", 0.4f)
            .Burst(80f, EnemyId.Popcorn, 24f, Patterns.Cluster, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteMarshmello, EnemyId.EvilSlime, 18, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(165f, EnemyId.Erbsenschote, 14f, Patterns.Arc, 0f)
            .Burst(195f, EnemyId.EvilSlime, 20f, Patterns.Cluster, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.Kerzentorte, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Muffin, 26f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.EvilSlime, 20f)
            .Pool(EnemyId.Muffin, 25f)
            .Pool(EnemyId.Pancake, 10f)
            .Pool(EnemyId.Kaesekeil, 20f)
            .Pool(EnemyId.Fleischball, 15f)
            .Pool(EnemyId.Erbsenschote, 10f)
            .Pressure(300f, 440f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Kaesekeil, 14f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.EvilSlime, 12f, Patterns.Arc, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.EliteMarshmello, EnemyId.Muffin, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(220f, EnemyId.Muffin, 26f, Patterns.Cluster, 0f)
            .Burst(240f, EnemyId.Fleischball, 14f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.Kerzentorte, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.EvilSlime, 24f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Marshmello, 10f)
            .Pool(EnemyId.EvilSlime, 15f)
            .Pool(EnemyId.Muffin, 25f)
            .Pool(EnemyId.Pancake, 15f)
            .Pool(EnemyId.Kaesekeil, 15f)
            .Pool(EnemyId.Fleischball, 10f)
            .Pool(EnemyId.Popcorn, 10f)
            .Pressure(500f, 750f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.EvilSlime, 20f)
            .Pool(EnemyId.Muffin, 25f)
            .Pool(EnemyId.Pancake, 15f)
            .Pool(EnemyId.Kaesekeil, 15f)
            .Pool(EnemyId.Fleischball, 15f)
            .Pool(EnemyId.Wurstdackel, 10f)
            .Pressure(175f, 175f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Kerzentorte, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Muffin, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteMarshmello, 1f, Patterns.Arc, 0f);

        return plan;
    }

    // ------------------------------------------------------------- World2

    /// <summary>
    /// Wald - Welt 1 der Route und der Massstab fuer den Druck aller Welten.
    /// Urspruenglich im Takt der Kueche (Zeitpunkte fuer Ring und Atempausen) - nur mit
    /// Waldgegnern. Die Kueche war gut balanciert, also wird hier nicht neu
    /// erfunden, sondern uebersetzt:
    ///
    ///   Marshmello       -> Fliegenpilz   (5 Leben, sonst gleich)
    ///   Boeser Slime     -> Kirschslime   (etwas zaeher)
    ///   Elite-Marshmello -> Eichel        (stand frueher im Pool der Kueche)
    ///   Messermaus       -> Elite-Fliegenpilz (Elite im Ring, ohne Kaefig -
    ///                                     die Kaefig-Wand ist archiviert)
    ///   Muffin & Co.     -> Honey (und spaeter der Pfannkuchen)
    ///
    /// Die Gegner kommen von leicht nach schwer dazu, jede Phase bringt einen
    /// neuen mit:
    ///
    ///   Phase 1  Fliegenpilz (am Ende ein erster Kirschslime-Schwall)
    ///   Phase 2  + Kirschslime, + Weisse Messermaus
    ///   Phase 3  Fliegenpilz raus, + Eichel
    ///   Phase 4  Kirschslime raus, + Honey
    ///   Boss     Eichel, Honey, + Pfannkuchen - und die Glutwurz,
    ///            der Baumriese des Waldes (speit Feuer, siehe EnemyGlutwurz)
    ///   Endlos   Eichel, Honey, Pfannkuchen
    ///
    /// Seit Oktober 2026 im 20-Minuten-Takt der Kueche (gleiche Phasenlaengen):
    ///
    ///   0:00  Anlauf, ~3 Fliegenpilze (5 s)
    ///   0:05  Fliegenpilze, 1:50 Ring mit Elite-Fliegenpilz
    ///   3:00  + Kirschslime, Messermaus, Beerenspinne, 8:00 Ring "PILZKOENIG!"
    ///   9:00  + Eichel, Maronen-Igel, Honigbaerchen, 10:00 Schleimkoenig (Zwischenboss)
    ///  14:00  + Honey, Walnuss-Krabbe
    ///  20:00  Glutwurz
    ///
    /// Der Fluegeldolch (Fledermaus) ist seit Oktober 2026 in den Geisterwald
    /// (World6) umgezogen.
    ///
    /// Seit Welle 4 dazu: Beerenspinne (Phase 2), Maronen-Igel + Honigbaerchen
    /// (Phase 3), Walnuss-Krabbe (Phase 4); dieselben auch im Demo-Plan.
    ///
    /// Elites kommen nicht nur im Ring, sondern immer wieder zwischendurch -
    /// aber immer genau EINER pro Burst. Der Director setzt bei Elites nie
    /// mehr als einen, egal welcher Druck dasteht, und nimmt sie nie aus dem Pool.
    ///
    /// Die ersten 5 Sekunden sind ein Anlauf mit nur etwa 3 Fliegenpilzen -
    /// damit schwache Chars (z.B. Shorikookie) Level 2 erreichen und man kurz
    /// ins Spiel findet. Phase 1 ist dafuer 5 s kuerzer, alles danach liegt
    /// zeitlich wie vorher.
    /// </summary>
    public static RunPlan World2()
    {
        var plan = new RunPlan("World2");

        plan.Phase(5f)
            .Pool(EnemyId.Fliegenpilz, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Fliegenpilz, 100f)
            .Pressure(12f, 39f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Fliegenpilz, 14f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Kirschslime, 16f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteFliegenpilz, EnemyId.Fliegenpilz, 18, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(130f, EnemyId.Fliegenpilz, 16f, Patterns.Cluster, 0f)
            .Burst(155f, EnemyId.Kirschslime, 16f, Patterns.Arc, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Fliegenpilz, 45f)
            .Pool(EnemyId.Kirschslime, 25f)
            .Pool(EnemyId.WeisseMessermaus, 15f)
            .Pool(EnemyId.Beerenspinne, 15f)
            .Pressure(39f, 80f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.Kirschslime, 16f, Patterns.Arc, 0f)
            .Burst(60f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(90f, EnemyId.Beerenspinne, 16f, Patterns.Arc, 0f)
            .Burst(105f, EnemyId.WeisseMessermaus, 24f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Kirschslime, 30f, Patterns.Scatter, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.Beerenspinne, 20f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Kirschslime, 30f, Patterns.Arc, 0f)
            .Encircle(300f, EnemyId.EliteFliegenpilz, EnemyId.WeisseMessermaus, 20, 14f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(330f, EnemyId.WeisseMessermaus, 24f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Kirschslime, 20f)
            .Pool(EnemyId.WeisseMessermaus, 20f)
            .Pool(EnemyId.Eichel, 25f)
            .Pool(EnemyId.Maronenigel, 15f)
            .Pool(EnemyId.Honigbaer, 10f)
            .Pressure(100f, 200f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(40f, EnemyId.Eichel, 25f, Patterns.Ambush, 0f)
            .Boss(60f, EnemyId.Schleimkoenig, "SCHLEIMKOENIG!", 0.4f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteFliegenpilz, EnemyId.Kirschslime, 22, 14f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(125f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(165f, EnemyId.Maronenigel, 20f, Patterns.Column, 0f)
            .Burst(195f, EnemyId.WeisseMessermaus, 30f, Patterns.Cluster, 0f)
            .Burst(225f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f)
            .Burst(250f, EnemyId.Eichel, 40f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Eichel, 30f)
            .Pool(EnemyId.WeisseMessermaus, 15f)
            .Pool(EnemyId.Honey, 20f)
            .Pool(EnemyId.Honigbaer, 15f)
            .Pool(EnemyId.Walnusskrabbe, 10f)
            .Pressure(240f, 350f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Burst(50f, EnemyId.Honey, 40f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f)
            .Burst(95f, EnemyId.Honigbaer, 24f, Patterns.Arc, 0f)
            .Calm(110f, 8f, 0.15f)
            .Encircle(145f, EnemyId.EliteFliegenpilz, EnemyId.Eichel, 26, 15f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(190f, EnemyId.Walnusskrabbe, 20f, Patterns.Ambush, 0f)
            .Burst(225f, EnemyId.Honey, 60f, Patterns.Cluster, 0f)
            .Burst(265f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Honey, 40f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Eichel, 35f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Pancake, 10f)
            .Pool(EnemyId.Walnusskrabbe, 10f)
            .Pool(EnemyId.Maronenigel, 10f)
            .Pressure(400f, 600f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Glutwurz, "GLUTWURZ", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Eichel, 30f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Pancake, 15f)
            .Pool(EnemyId.Walnusskrabbe, 10f)
            .Pool(EnemyId.Honigbaer, 10f)
            .Pressure(140f, 140f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Eichel, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f);

        return plan;
    }

    // ------------------------------------------------------------- World5

    /// <summary>
    /// Eisgletscher - Karte 3 in der Levelauswahl. Im Takt der Kueche (gleiche
    /// Phasen, Boss bei 20:00, Druck = Wald x 1.55), nur mit Gegnern der Eiswelt:
    ///
    ///   Minzkugel        Grundgegner, schlittert auf der Schmelzpfuetze
    ///   Raketeneis       springt auf dem Stiel, schnell und etwas zaeher
    ///   Zuckerwoelkchen  schwebt schnell heran, kommt gern im Schwarm
    ///   Eiskeks-Sandwich huepft und schnappt zu, zaeher als das Raketeneis
    ///   Kokos-Yeti       der schwere Brocken (Werte wie der Milchpanzer)
    ///   Sternplaetzchen  rollt wie ein Rad, schnell und schwach, kommt in Reihen
    ///   Kakaotasse       watschelt, zaeh fuer ihre Groesse
    ///   Puddingrobbe     robbt und rutscht, schneller als die Tasse
    ///   Pfefferminz-Schnecke  gleitet mit Frostspur, ab 3:00
    ///   Eiswuerfel-Baer  eingefrorener Gummibaer, zaeh, ab 9:00
    ///   Softi            Elite, Waffeltuete mit wirbelnder Softeis-Frisur
    ///   Fuerst Pueckler  zweite Elite ab 8:00, Eisbombe mit Krone, zaeher ("SEINE HOHEIT!")
    ///
    /// Stracciatella kommt bei 10:00 als Zwischenboss, am Ende wartet der
    /// Eiskaiser (Kaiser-Kick + Frostring, siehe EnemyEiskaiser).
    ///
    /// Ablauf:
    ///
    ///   0:00  Anlauf, ~3 Minzkugeln (5 s)
    ///   0:05  Minzkugeln, erster Raketen-Schwall bei 1:25, Ring mit Softi,
    ///         erster Woelkchen-Schwarm bei 2:05, erste Plaetzchen-Reihe bei 2:20
    ///   3:00  + Raketeneis, Zuckerwoelkchen und Sternplaetzchen im Grundnachschub,
    ///         erste Kakaotassen bei 7:00, 8:00 Ring mit Fuerst Pueckler
    ///   9:00  + Eiskeks-Sandwich und Kakaotasse, 10:00 Stracciatella (Zwischenboss),
    ///         erste Puddingrobben bei 13:15
    ///  14:00  + Kokos-Yeti und Puddingrobbe, Minzkugeln nur noch Beimischung
    ///  20:00  Eiskaiser
    /// </summary>
    public static RunPlan World5()
    {
        var plan = new RunPlan("World5");

        plan.Phase(5f)
            .Pool(EnemyId.Minzkugel, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Minzkugel, 100f)
            .Pressure(19f, 60f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Minzkugel, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Raketeneis, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.Softi, EnemyId.Minzkugel, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(120f, EnemyId.Zuckerwolke, 12f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.Plaetzchen, 10f, Patterns.Arc, 0f)
            .Burst(150f, EnemyId.Minzkugel, 12f, Patterns.Column, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Minzkugel, 25f)
            .Pool(EnemyId.Raketeneis, 30f)
            .Pool(EnemyId.Minzschnecke, 10f)
            .Pool(EnemyId.Zuckerwolke, 15f)
            .Pool(EnemyId.Plaetzchen, 20f)
            .Pressure(60f, 124f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.Raketeneis, 10f, Patterns.Arc, 0f)
            .Burst(50f, EnemyId.Zuckerwolke, 16f, Patterns.Cluster, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.Raketeneis, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.Softi, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Minzkugel, 18f, Patterns.Scatter, 0f)
            .Burst(170f, EnemyId.Plaetzchen, 16f, Patterns.Column, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(210f, EnemyId.Zuckerwolke, 20f, Patterns.Arc, 0f)
            .Burst(225f, EnemyId.Raketeneis, 18f, Patterns.Cluster, 0f)
            .Burst(240f, EnemyId.Kakaotasse, 8f, Patterns.Cluster, 0f)
            .Burst(255f, EnemyId.Minzschnecke, 14f, Patterns.Column, 0f)
            .Burst(270f, EnemyId.Minzkugel, 20f, Patterns.Arc, 0f)
            .Encircle(300f, EnemyId.FuerstPueckler, EnemyId.Plaetzchen, 16, 14f, false, "SEINE HOHEIT!", 0.35f, 25f, 1.5f)
            .Burst(330f, EnemyId.Raketeneis, 20f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Minzkugel, 5f)
            .Pool(EnemyId.Raketeneis, 20f)
            .Pool(EnemyId.Minzschnecke, 10f)
            .Pool(EnemyId.Eiswuerfelbaer, 15f)
            .Pool(EnemyId.Zuckerwolke, 10f)
            .Pool(EnemyId.EisSandwich, 20f)
            .Pool(EnemyId.Plaetzchen, 10f)
            .Pool(EnemyId.Kakaotasse, 20f)
            .Pressure(155f, 310f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.EisSandwich, 16f, Patterns.Ambush, 0f)
            .Boss(60f, EnemyId.Stracciatella, "STRACCIATELLA!", 0.4f)
            .Calm(95f, 10f, 0.15f)
            .Burst(115f, EnemyId.Eiswuerfelbaer, 10f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Raketeneis, 12f, Patterns.Arc, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.FuerstPueckler, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Zuckerwolke, 26f, Patterns.Cluster, 0f)
            .Burst(255f, EnemyId.Puddingrobbe, 12f, Patterns.Ambush, 0f)
            .Burst(270f, EnemyId.EisSandwich, 20f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Minzkugel, 5f)
            .Pool(EnemyId.Raketeneis, 15f)
            .Pool(EnemyId.Zuckerwolke, 10f)
            .Pool(EnemyId.EisSandwich, 15f)
            .Pool(EnemyId.KokosYeti, 10f)
            .Pool(EnemyId.Eiswuerfelbaer, 10f)
            .Pool(EnemyId.Minzschnecke, 5f)
            .Pool(EnemyId.Kakaotasse, 15f)
            .Pool(EnemyId.Puddingrobbe, 15f)
            .Pool(EnemyId.Plaetzchen, 5f)
            .Pressure(370f, 545f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.KokosYeti, 16f, Patterns.Ambush, 0f)
            .Burst(90f, EnemyId.Puddingrobbe, 18f, Patterns.Column, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.Softi, EnemyId.EisSandwich, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(200f, EnemyId.Plaetzchen, 24f, Patterns.Arc, 0f)
            .Burst(220f, EnemyId.Zuckerwolke, 30f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.FuerstPueckler, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Minzkugel, 24f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Minzkugel, 5f)
            .Pool(EnemyId.Raketeneis, 25f)
            .Pool(EnemyId.Zuckerwolke, 10f)
            .Pool(EnemyId.EisSandwich, 20f)
            .Pool(EnemyId.KokosYeti, 15f)
            .Pool(EnemyId.Kakaotasse, 10f)
            .Pool(EnemyId.Puddingrobbe, 15f)
            .Pool(EnemyId.Eiswuerfelbaer, 10f)
            .Pressure(620f, 930f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Eiskaiser, "EISKAISER!", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Minzkugel, 5f)
            .Pool(EnemyId.Raketeneis, 20f)
            .Pool(EnemyId.Zuckerwolke, 10f)
            .Pool(EnemyId.EisSandwich, 20f)
            .Pool(EnemyId.KokosYeti, 15f)
            .Pool(EnemyId.Kakaotasse, 10f)
            .Pool(EnemyId.Puddingrobbe, 15f)
            .Pool(EnemyId.Plaetzchen, 5f)
            .Pool(EnemyId.Eiswuerfelbaer, 10f)
            .Pool(EnemyId.Minzschnecke, 5f)
            .Pressure(215f, 215f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.FuerstPueckler, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Raketeneis, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.Softi, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ------------------------------------------------------------- World6

    /// <summary>
    /// Lebkuchen-Geisterwald - Karte 4 in der Levelauswahl. Im Takt der Kueche
    /// (gleiche Phasen, Boss bei 20:00, Druck = Wald x 1.9).
    ///
    /// Seit Oktober 2026 mit eigenen Gegnern, die alle selbst leuchten (Leuchtebene
    /// EnemyGlowLayer, im dunklen Wald sieht man vor allem ihr Licht). Jede Phase
    /// stellt neue vor, meist mit einem eigenen Burst. Dazu weiter der
    /// Fluegeldolch (Fledermaus) und am Ende das Gespenst (siehe EnemySquiddy).
    /// Elites wie in der Kueche: die Spuksuppe (Hexenkessel) ab 1:45 im Ring,
    /// der Wackelgeist (Goetterspeise, zaeher) ab 8:00 mit "BUH!".
    ///
    ///   0:00  Anlauf (5 s), saure Gluehwuermer
    ///   0:05  + Flambierkirschen (Schwarm), Fluegeldolche, erste Kuerbiswichte, 1:45 Spuksuppe-Ring
    ///   3:00  + Gluehbirnen, Lakritzkater, Baisergeister, 8:00 Ring mit dem ersten Wackelgeist
    ///   9:00  + Leuchtpilze, Windbeutel-Eulen, 10:00 Knusperhaus (Zwischenboss)
    ///  14:00  + Lebkuchen-Zombies (Brocken)
    ///  20:00  Das Gespenst
    /// </summary>
    public static RunPlan World6()
    {
        var plan = new RunPlan("World6");

        plan.Phase(5f)
            .Pool(EnemyId.Gluehwurm, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Gluehwurm, 40f)
            .Pool(EnemyId.Flambierkirsche, 30f)
            .Pool(EnemyId.Fluegeldolch, 30f)
            .Pressure(23f, 74f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.Flambierkirsche, 12f, Patterns.Arc, 0f)
            .Encircle(105f, EnemyId.Spuksuppe, EnemyId.Gluehwurm, 14, 13f, false, "ES BRODELT!", 0.35f, 25f, 1.5f)
            .Burst(130f, EnemyId.Kuerbiswicht, 8f, Patterns.Cluster, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Kuerbiswicht, 25f)
            .Pool(EnemyId.Gluehbirne, 20f)
            .Pool(EnemyId.Flambierkirsche, 20f)
            .Pool(EnemyId.Gluehwurm, 15f)
            .Pool(EnemyId.Fluegeldolch, 20f)
            .Pressure(74f, 152f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Gluehbirne, 12f, Patterns.Column, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(110f, EnemyId.Lakritzkater, 10f, Patterns.Ambush, 0f)
            .Burst(135f, EnemyId.Spuksuppe, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Fluegeldolch, 12f, Patterns.Arc, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(240f, EnemyId.Baisergeist, 14f, Patterns.Cluster, 0f)
            .Encircle(300f, EnemyId.Wackelgeist, EnemyId.Flambierkirsche, 16, 14f, false, "BUH!", 0.35f, 25f, 1.5f)
            .Burst(330f, EnemyId.Lakritzkater, 12f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Kuerbiswicht, 15f)
            .Pool(EnemyId.Gluehbirne, 15f)
            .Pool(EnemyId.Lakritzkater, 20f)
            .Pool(EnemyId.Baisergeist, 20f)
            .Pool(EnemyId.Leuchtpilz, 15f)
            .Pool(EnemyId.Fluegeldolch, 15f)
            .Pressure(190f, 380f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.Leuchtpilz, 10f, Patterns.Column, 0f)
            .Boss(60f, EnemyId.Knusperhaus, "KNUSPERHAUS!", 0.4f)
            .Calm(95f, 10f, 0.15f)
            .Burst(110f, EnemyId.Spuksuppe, 1f, Patterns.Ambush, 0f)
            .Burst(130f, EnemyId.Lakritzkater, 16f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Fluegeldolch, 12f, Patterns.Arc, 0f)
            .Burst(180f, EnemyId.Windbeuteleule, 8f, Patterns.Cluster, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.Wackelgeist, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Baisergeist, 20f, Patterns.Arc, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Lakritzkater, 15f)
            .Pool(EnemyId.Baisergeist, 15f)
            .Pool(EnemyId.Leuchtpilz, 15f)
            .Pool(EnemyId.Windbeuteleule, 15f)
            .Pool(EnemyId.Lebkuchenzombie, 15f)
            .Pool(EnemyId.Gluehbirne, 10f)
            .Pool(EnemyId.Flambierkirsche, 15f)
            .Pressure(455f, 665f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Lebkuchenzombie, 14f, Patterns.Ambush, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.Spuksuppe, EnemyId.Baisergeist, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(180f, EnemyId.Flambierkirsche, 30f, Patterns.Arc, 0f)
            .Burst(220f, EnemyId.Lebkuchenzombie, 20f, Patterns.Column, 0f)
            .Burst(260f, EnemyId.Wackelgeist, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Windbeuteleule, 16f, Patterns.Cluster, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Lebkuchenzombie, 20f)
            .Pool(EnemyId.Windbeuteleule, 15f)
            .Pool(EnemyId.Leuchtpilz, 15f)
            .Pool(EnemyId.Lakritzkater, 20f)
            .Pool(EnemyId.Baisergeist, 15f)
            .Pool(EnemyId.Flambierkirsche, 15f)
            .Pressure(760f, 1140f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Squiddy, "DAS GESPENST!", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Lebkuchenzombie, 15f)
            .Pool(EnemyId.Windbeuteleule, 10f)
            .Pool(EnemyId.Leuchtpilz, 10f)
            .Pool(EnemyId.Lakritzkater, 15f)
            .Pool(EnemyId.Baisergeist, 10f)
            .Pool(EnemyId.Kuerbiswicht, 10f)
            .Pool(EnemyId.Gluehbirne, 10f)
            .Pool(EnemyId.Flambierkirsche, 10f)
            .Pool(EnemyId.Gluehwurm, 5f)
            .Pool(EnemyId.Fluegeldolch, 5f)
            .Pressure(265f, 265f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Wackelgeist, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Lakritzkater, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.Spuksuppe, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ------------------------------------------------------------- World4

    /// <summary>
    /// Vulkanwelt - Karte 5 in der Levelauswahl, die letzte Welt. Im Takt der
    /// Kueche (gleiche Phasen), Druck = Wald x 2.3 - die schwerste Welt.
    ///
    /// Seit Oktober 2026 mit eigenen Gegnern (lauter heisses, scharfes und
    /// gebackenes Essen). Jede Phase stellt neue vor, meist mit einem eigenen
    /// Burst. Bei 20:00 kommt der Verkohlte (Endboss der Story, Phase 3 in
    /// seiner Herzkammer) - danach geht es mit voller Mischung weiter, dann Endlos. Elites: der Feuertaco ab 1:45 im Ring,
    /// die Creme Brulee (zaeher) ab 8:00 mit "FLAMBIERT!".
    ///
    ///   0:00  Anlauf (5 s), Knisterbrause
    ///   0:05  + Feuerschoten, Takoyaki, 1:45 Feuertaco-Ring
    ///   3:00  + Lavakuchen, Pizzastuecke, Schaschliks (Huepfer), 8:00 Ring mit der ersten Creme Brulee
    ///   9:00  + Kaesefondue (zaeh), Wasabi-Kleckse, Drachenfruechte, 10:00 Ofenkartoffel (Zwischenboss)
    ///  14:00  + Ofenbrot-Golems (Brocken)
    ///  20:00  alles, hoher Druck - und der Verkohlte
    /// </summary>
    public static RunPlan World4()
    {
        var plan = new RunPlan("World4");

        plan.Phase(5f)
            .Pool(EnemyId.Knisterbrause, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Knisterbrause, 35f)
            .Pool(EnemyId.Feuerschote, 35f)
            .Pool(EnemyId.Takoyaki, 30f)
            .Pressure(28f, 90f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.Knisterbrause, 14f, Patterns.Arc, 0f)
            .Encircle(105f, EnemyId.Feuertaco, EnemyId.Knisterbrause, 14, 13f, false, "ZU SCHARF!", 0.35f, 25f, 1.5f)
            .Burst(130f, EnemyId.Feuerschote, 8f, Patterns.Cluster, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Feuerschote, 20f)
            .Pool(EnemyId.Takoyaki, 20f)
            .Pool(EnemyId.Lavakuchen, 20f)
            .Pool(EnemyId.Pizzastueck, 15f)
            .Pool(EnemyId.Schaschlik, 10f)
            .Pool(EnemyId.Knisterbrause, 15f)
            .Pressure(90f, 185f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Lavakuchen, 10f, Patterns.Column, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(110f, EnemyId.Takoyaki, 12f, Patterns.Ambush, 0f)
            .Burst(135f, EnemyId.Feuertaco, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Schaschlik, 10f, Patterns.Arc, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(240f, EnemyId.Pizzastueck, 12f, Patterns.Cluster, 0f)
            .Encircle(300f, EnemyId.CremeBrulee, EnemyId.Takoyaki, 16, 14f, false, "FLAMBIERT!", 0.35f, 25f, 1.5f)
            .Burst(330f, EnemyId.Knisterbrause, 24f, Patterns.Arc, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Lavakuchen, 15f)
            .Pool(EnemyId.Pizzastueck, 15f)
            .Pool(EnemyId.Schaschlik, 10f)
            .Pool(EnemyId.Kaesefondue, 15f)
            .Pool(EnemyId.Wasabiklecks, 15f)
            .Pool(EnemyId.Drachenfrucht, 15f)
            .Pool(EnemyId.Takoyaki, 15f)
            .Pressure(230f, 460f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.Kaesefondue, 10f, Patterns.Column, 0f)
            .Boss(60f, EnemyId.Ofenkartoffel, "OFENKARTOFFEL!", 0.4f)
            .Calm(95f, 10f, 0.15f)
            .Burst(110f, EnemyId.Feuertaco, 1f, Patterns.Ambush, 0f)
            .Burst(130f, EnemyId.Wasabiklecks, 14f, Patterns.Cluster, 0f)
            .Burst(150f, EnemyId.Drachenfrucht, 12f, Patterns.Arc, 0f)
            .Burst(180f, EnemyId.Takoyaki, 16f, Patterns.Ambush, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.CremeBrulee, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Knisterbrause, 30f, Patterns.Arc, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Brotgolem, 15f)
            .Pool(EnemyId.Kaesefondue, 15f)
            .Pool(EnemyId.Wasabiklecks, 10f)
            .Pool(EnemyId.Drachenfrucht, 15f)
            .Pool(EnemyId.Pizzastueck, 10f)
            .Pool(EnemyId.Feuerschote, 15f)
            .Pool(EnemyId.Knisterbrause, 20f)
            .Pressure(550f, 805f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.Brotgolem, 10f, Patterns.Ambush, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.Feuertaco, EnemyId.Pizzastueck, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(180f, EnemyId.Knisterbrause, 34f, Patterns.Arc, 0f)
            .Burst(220f, EnemyId.Brotgolem, 14f, Patterns.Column, 0f)
            .Burst(260f, EnemyId.CremeBrulee, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Schaschlik, 16f, Patterns.Cluster, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Brotgolem, 20f)
            .Pool(EnemyId.Kaesefondue, 15f)
            .Pool(EnemyId.Drachenfrucht, 15f)
            .Pool(EnemyId.Wasabiklecks, 10f)
            .Pool(EnemyId.Lavakuchen, 10f)
            .Pool(EnemyId.Takoyaki, 15f)
            .Pool(EnemyId.Knisterbrause, 15f)
            .Pressure(920f, 1380f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Verkohlter, "DER VERKOHLTE!", 0.4f)
            .Burst(60f, EnemyId.Feuertaco, 1f, Patterns.Ambush, 0f)
            .Burst(240f, EnemyId.CremeBrulee, 1f, Patterns.Ambush, 0f)
            .Burst(420f, EnemyId.Feuertaco, 1f, Patterns.Arc, 0f);

        plan.EndlessPhase()
            .Pool(EnemyId.Brotgolem, 15f)
            .Pool(EnemyId.Kaesefondue, 10f)
            .Pool(EnemyId.Drachenfrucht, 10f)
            .Pool(EnemyId.Wasabiklecks, 10f)
            .Pool(EnemyId.Lavakuchen, 10f)
            .Pool(EnemyId.Pizzastueck, 10f)
            .Pool(EnemyId.Schaschlik, 5f)
            .Pool(EnemyId.Takoyaki, 10f)
            .Pool(EnemyId.Feuerschote, 10f)
            .Pool(EnemyId.Knisterbrause, 10f)
            .Pressure(320f, 320f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.CremeBrulee, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Takoyaki, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.Feuertaco, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
