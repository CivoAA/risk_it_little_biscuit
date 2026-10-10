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
/// </summary>
public static class WavePlans
{
    /// <summary>
    /// Holt den Plan zur Karte. Unbekannt = der Plan von World1.
    ///
    /// Gespielt werden zurzeit World1 (Kueche, Karte 1, Szene Map_World0),
    /// World2 (Wald, Karte 2, Szene Map_World3 - planId dort fest eingetragen)
    /// World5 (Eisgletscher, Karte 3, Szene Map_World5 - kommt ueber den
    /// mapName der Karte, die planId dort ist leer) und World6 (Geisterwald,
    /// Karte 4, Szene Map_World6 - ebenso ueber den mapName).
    /// </summary>
    public static RunPlan For(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world2": return World2();
            case "world2demo": return World2Demo();
            case "world5": return World5();
            case "world6": return World6();
            default: return World1();
        }
    }

    /// <summary>
    /// Welcher Plan im Wald wirklich laeuft: "World2" (voller Wald) oder
    /// "World2Demo" (leichter, fuer die Demo). Die Karte selbst traegt weiter
    /// "World2" als planId - umgeschaltet wird nur hier. Die Wellenplan-Werkstatt
    /// schreibt diese Zeile beim Umschalten neu.
    /// </summary>
    public const string WaldPlan = "World2Demo";

    /// <summary>
    /// Wie <see cref="For"/>, aber mit dem Wald-Schalter: die planId einer
    /// Karte ("World2") wird auf den gerade aktiven Waldplan umgelenkt. Das
    /// nimmt der Director beim Laufstart.
    /// </summary>
    public static RunPlan ForMap(string planId)
    {
        string id = (planId ?? "").Trim();
        if (string.Equals(id, "World2", System.StringComparison.OrdinalIgnoreCase)) id = WaldPlan;
        return For(id);
    }

    /// <summary>Alle Plaene, die es gibt - fuer die Werkstatt und den Vergleich.</summary>
    public static readonly string[] AllIds = { "World1", "World2", "World2Demo", "World5", "World6" };

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================

    // ------------------------------------------------------------- World1

    /// <summary>
    /// Kueche - die Einstiegskarte (Karte 1 in der Levelauswahl). Laeuft mit
    /// der Skalierung der Wald-Demo (World2Demo): dieselben Druckwerte, nur auf
    /// 20 Minuten gestreckt, Boss bei 20:00. Nur aktive Kuechengegner:
    ///
    ///   Marshmello, Boeser Slime, Muffin, Milchpanzer,
    ///   Elite-Marshmello (Elite, immer einzeln) und der Keks-Koenig (Boss)
    ///
    /// Ablauf:
    ///
    ///   0:00  Anlauf, ~3 Marshmellos (5 s)
    ///   0:05  Marshmellos, erster Ring mit Elite-Marshmello
    ///   3:00  + Boeser Slime
    ///   9:00  + Muffin
    ///  14:00  + Milchpanzer
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
            .Pool(EnemyId.Marshmello, 100f)
            .Pressure(10f, 28f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Marshmello, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.EvilSlime, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteMarshmello, EnemyId.Marshmello, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(150f, EnemyId.Marshmello, 12f, Patterns.Column, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Marshmello, 60f)
            .Pool(EnemyId.EvilSlime, 40f)
            .Pressure(28f, 56f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.EvilSlime, 10f, Patterns.Arc, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.EvilSlime, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteMarshmello, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Marshmello, 18f, Patterns.Scatter, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.EvilSlime, 18f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Marshmello, 20f, Patterns.Arc, 0f)
            .Burst(300f, EnemyId.EliteMarshmello, 1f, Patterns.Ambush, 0f)
            .Burst(330f, EnemyId.EvilSlime, 20f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Marshmello, 25f)
            .Pool(EnemyId.EvilSlime, 40f)
            .Pool(EnemyId.Muffin, 35f)
            .Pressure(70f, 140f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Muffin, 16f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.EvilSlime, 12f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteMarshmello, EnemyId.EvilSlime, 18, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(195f, EnemyId.EvilSlime, 20f, Patterns.Cluster, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.EliteMarshmello, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Muffin, 26f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Marshmello, 10f)
            .Pool(EnemyId.EvilSlime, 35f)
            .Pool(EnemyId.Muffin, 45f)
            .Pool(EnemyId.Milchpanzer, 10f)
            .Pressure(140f, 170f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.Milchpanzer, 16f, Patterns.Ambush, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.EliteMarshmello, EnemyId.Muffin, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(220f, EnemyId.Muffin, 26f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.EliteMarshmello, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.EvilSlime, 24f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Marshmello, 15f)
            .Pool(EnemyId.EvilSlime, 25f)
            .Pool(EnemyId.Muffin, 45f)
            .Pool(EnemyId.Milchpanzer, 15f)
            .Pressure(280f, 420f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Marshmello, 10f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pool(EnemyId.Muffin, 40f)
            .Pool(EnemyId.Milchpanzer, 20f)
            .Pressure(100f, 100f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Muffin, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteMarshmello, 1f, Patterns.Arc, 0f);

        return plan;
    }

    // ------------------------------------------------------------- World2

    /// <summary>
    /// Wald - Karte 2 in der Levelauswahl. Laeuft im Takt der Kueche: dieselben
    /// Druckwerte, dieselben Zeitpunkte fuer Ring und Atempausen - nur mit
    /// Waldgegnern. Die Kueche war gut balanciert, also wird hier nicht neu
    /// erfunden, sondern uebersetzt:
    ///
    ///   Marshmello       -> Fliegenpilz   (5 Leben, sonst gleich)
    ///   Boeser Slime     -> Kirschslime   (etwas zaeher)
    ///   Elite-Marshmello -> Eichel        (stand frueher im Pool der Kueche)
    ///   Messermaus       -> Elite-Fliegenpilz (Elite im Ring, ohne Kaefig -
    ///                                     die Kaefig-Wand ist archiviert)
    ///   Muffin & Co.     -> Honey (und spaeter der verstaerkte Milchpanzer)
    ///
    /// Die Gegner kommen von leicht nach schwer dazu, jede Phase bringt einen
    /// neuen mit:
    ///
    ///   Phase 1  Fliegenpilz (am Ende ein erster Kirschslime-Schwall)
    ///   Phase 2  + Kirschslime, + Weisse Messermaus
    ///   Phase 3  Fliegenpilz raus, + Eichel, + Fluegeldolch
    ///   Phase 4  Kirschslime raus, + Honey
    ///   Boss     Eichel, Honey, Fluegeldolch, + Milchpanzer - und die Glutwurz,
    ///            der Baumriese des Waldes (speit Feuer, siehe EnemyGlutwurz)
    ///   Endlos   Eichel, Honey, Milchpanzer, Fluegeldolch
    ///
    /// Der Fluegeldolch (Fledermaus) kommt nur als Beimischung - in Rudeln ist
    /// er zu viel.
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

        plan.Phase(115f)
            .Pool(EnemyId.Fliegenpilz, 100f)
            .Pressure(12f, 39f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Fliegenpilz, 14f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Kirschslime, 16f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteFliegenpilz, EnemyId.Fliegenpilz, 18, 13f, false, "RING!", 0.35f, 25f, 1.5f);

        plan.Phase(180f)
            .Pool(EnemyId.Fliegenpilz, 55f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 15f)
            .Pressure(39f, 80f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.Kirschslime, 16f, Patterns.Arc, 0f)
            .Burst(60f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.WeisseMessermaus, 24f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Kirschslime, 30f, Patterns.Scatter, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pool(EnemyId.Eichel, 30f)
            .Pool(EnemyId.Fluegeldolch, 15f)
            .Pressure(100f, 200f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(40f, EnemyId.Eichel, 25f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.Fluegeldolch, 20f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteFliegenpilz, EnemyId.Kirschslime, 22, 14f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(125f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(195f, EnemyId.WeisseMessermaus, 30f, Patterns.Cluster, 0f)
            .Burst(225f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f)
            .Burst(250f, EnemyId.Eichel, 40f, Patterns.Column, 0f);

        plan.Phase(290f)
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.WeisseMessermaus, 20f)
            .Pool(EnemyId.Honey, 25f)
            .Pool(EnemyId.Fluegeldolch, 15f)
            .Pressure(240f, 350f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Burst(50f, EnemyId.Honey, 40f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f)
            .Calm(110f, 8f, 0.15f)
            .Encircle(145f, EnemyId.EliteFliegenpilz, EnemyId.Eichel, 26, 15f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(225f, EnemyId.Honey, 60f, Patterns.Cluster, 0f)
            .Burst(265f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Eichel, 45f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Milchpanzer, 10f)
            .Pool(EnemyId.Fluegeldolch, 30f)
            .Pressure(400f, 600f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Glutwurz, "GLUTWURZ", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Milchpanzer, 20f)
            .Pool(EnemyId.Fluegeldolch, 25f)
            .Pressure(140f, 140f)
            .Base(Patterns.Scatter)
            .Burst(30f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Encircle(60f, EnemyId.None, EnemyId.Eichel, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f);

        return plan;
    }

    // ------------------------------------------------------------- World2Demo

    /// <summary>
    /// Welt2 Wald Demo - der Wald fuer die Demo. Dieselbe Karte, dieselben
    /// Gegner in derselben Reihenfolge, derselbe Boss wie World2 - nur leichter,
    /// weil in der Demo nicht alle Chars, Skills und Features offen sind.
    ///
    /// Gegenueber World2:
    ///
    ///   Druck            etwa 70 % in jeder Phase
    ///   Schwalle         kleiner (etwa zwei Drittel)
    ///   Elites           eine pro Phase statt zwei bis drei
    ///   Ringe            weniger Gegner im Ring
    ///   Atempausen       je eine zusaetzliche in Phase 3 und 4
    ///   Zeitplan         die harten Stellen 2 min spaeter, Boss schon bei 13:00
    ///   Ende             Boss tot -> der Todes-Ramen kommt und beendet den
    ///                    Lauf (zaehlt als Sieg)
    ///
    /// Ablauf:
    ///
    ///   0:00  Anlauf, ~3 Fliegenpilze (5 s)
    ///   0:05  Fliegenpilze
    ///   2:00  + Kirschslime, + Weisse Messermaus - 5 statt 3 min lang, damit
    ///         die erste harte Stelle (Eichel, Fluegeldolch) erst bei ~8:00
    ///         kommt statt bei ~6:00
    ///   7:00  Eichel, Fluegeldolch
    ///  12:00  kurzer Uebergang ohne neuen Druck-Sprung - der Sprung von
    ///         ~11:00 (Honey) faellt damit auf 13:00 und geht im Boss auf
    ///  13:00  Glutwurz
    ///
    /// Anlauf (5 s, ~3 Fliegenpilze) wie im vollen Wald. Welcher der beiden
    /// Plaene laeuft, steht in WavePlans.WaldPlan (Umschalter in der Werkstatt).
    /// </summary>
    public static RunPlan World2Demo()
    {
        var plan = new RunPlan("World2Demo");

        plan.Phase(5f)
            .Pool(EnemyId.Fliegenpilz, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(115f)
            .Pool(EnemyId.Fliegenpilz, 100f)
            .Pressure(10f, 28f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Fliegenpilz, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Kirschslime, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteFliegenpilz, EnemyId.Fliegenpilz, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f);

        plan.Phase(300f)
            .Pool(EnemyId.Fliegenpilz, 55f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 15f)
            .Pressure(28f, 56f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.Kirschslime, 10f, Patterns.Arc, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.WeisseMessermaus, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Kirschslime, 18f, Patterns.Scatter, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.WeisseMessermaus, 18f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Kirschslime, 20f, Patterns.Arc, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pool(EnemyId.Eichel, 30f)
            .Pool(EnemyId.Fluegeldolch, 15f)
            .Pressure(70f, 140f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Eichel, 16f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.Fluegeldolch, 12f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteFliegenpilz, EnemyId.Kirschslime, 18, 14f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(195f, EnemyId.WeisseMessermaus, 20f, Patterns.Cluster, 0f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Eichel, 26f, Patterns.Column, 0f);

        plan.Phase(58f)
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pool(EnemyId.Kirschslime, 15f)
            .Pool(EnemyId.Fluegeldolch, 20f)
            .Pressure(140f, 155f)
            .Base(Patterns.Scatter);

        plan.Phase(600f)
            .Pool(EnemyId.Eichel, 45f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Milchpanzer, 10f)
            .Pool(EnemyId.Fluegeldolch, 30f)
            .Pressure(280f, 420f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Glutwurz, "GLUTWURZ", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.Honey, 15f)
            .Pool(EnemyId.Milchpanzer, 20f)
            .Pool(EnemyId.Fluegeldolch, 25f)
            .Pressure(100f, 100f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Eichel, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f);

        plan.EndWith(EnemyId.VerkohlterTod);

        return plan;
    }

    // ------------------------------------------------------------- World5

    /// <summary>
    /// Eisgletscher - Karte 3 in der Levelauswahl. Im Takt der Kueche (gleiche
    /// Druckwerte, Boss bei 20:00), mit den Gegnern der Eiswelt:
    ///
    ///   Minzkugel    Grundgegner, schlittert auf der Schmelzpfuetze
    ///   Raketeneis   springt auf dem Stiel, schnell und etwas zaeher
    ///   Softi        Elite, Waffeltuete mit wirbelnder Softeis-Frisur
    ///
    /// Dazu ab der Mitte die Weisse Messermaus (faellt im Schnee kaum auf) und
    /// spaeter der Milchpanzer als schwerer Brocken. Der Schleimkoenig kommt
    /// bei 11:30 als Zwischenboss, am Ende wartet der Eiskaiser
    /// (Bauchrutscher-Billard + Frostring, siehe EnemyEiskaiser).
    ///
    /// Ablauf:
    ///
    ///   0:00  Anlauf, ~3 Minzkugeln (5 s)
    ///   0:05  Minzkugeln, erster Raketen-Schwall bei 1:25, Ring mit Softi
    ///   3:00  + Raketeneis im Grundnachschub
    ///   9:00  + Weisse Messermaus, 11:30 Schleimkoenig (Zwischenboss)
    ///  14:00  + Milchpanzer, Minzkugeln nur noch Beimischung
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
            .Pressure(10f, 28f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Minzkugel, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Raketeneis, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.Softi, EnemyId.Minzkugel, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(150f, EnemyId.Minzkugel, 12f, Patterns.Column, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Minzkugel, 60f)
            .Pool(EnemyId.Raketeneis, 40f)
            .Pressure(28f, 56f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.Raketeneis, 10f, Patterns.Arc, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.Raketeneis, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.Softi, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Minzkugel, 18f, Patterns.Scatter, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.Raketeneis, 18f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Minzkugel, 20f, Patterns.Arc, 0f)
            .Burst(300f, EnemyId.Softi, 1f, Patterns.Ambush, 0f)
            .Burst(330f, EnemyId.Raketeneis, 20f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Minzkugel, 30f)
            .Pool(EnemyId.Raketeneis, 45f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pressure(70f, 140f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.WeisseMessermaus, 16f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.Raketeneis, 12f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Boss(150f, EnemyId.Schleimkoenig, "SCHLEIMKOENIG!", 0.4f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.Softi, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.Raketeneis, 26f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Minzkugel, 15f)
            .Pool(EnemyId.Raketeneis, 45f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Milchpanzer, 10f)
            .Pressure(140f, 170f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.Milchpanzer, 16f, Patterns.Ambush, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.Softi, EnemyId.Raketeneis, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(220f, EnemyId.WeisseMessermaus, 26f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.Softi, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Minzkugel, 24f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Minzkugel, 15f)
            .Pool(EnemyId.Raketeneis, 40f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Milchpanzer, 15f)
            .Pressure(280f, 420f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Eiskaiser, "EISKAISER!", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Minzkugel, 10f)
            .Pool(EnemyId.Raketeneis, 40f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Milchpanzer, 20f)
            .Pressure(100f, 100f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Raketeneis, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.Softi, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ------------------------------------------------------------- World6

    /// <summary>
    /// Lebkuchen-Geisterwald - Karte 4 in der Levelauswahl. Im Takt der Kueche
    /// (gleiche Druckwerte, Boss bei 20:00). Eigene Gegner hat der Wald noch
    /// nicht, also die, die nachts am besten passen:
    ///
    ///   Marshmello         Grundgegner (Zuckerwesen wie die Zuckergeister)
    ///   Boeser Slime       ab 3:00
    ///   Fluegeldolch       Fledermaus, nur Beimischung
    ///   Elite-Fluegeldolch Elite (Fledermaus mit Schwertfluegeln)
    ///   Weisse Messermaus  ab 9:00 - leuchtet im Dunkeln fast wie ein Geist
    ///   Milchpanzer        ab 14:00
    ///
    /// Der Schleimkoenig kommt bei 11:30 als Zwischenboss, am Ende das
    /// Gespenst - und was unter dem Laken steckt (siehe EnemySquiddy).
    ///
    ///   0:00  Anlauf, ~3 Marshmellos (5 s)
    ///   0:05  Marshmellos, Ring mit Elite-Fluegeldolch
    ///   3:00  + Boeser Slime, + Fluegeldolch
    ///   9:00  + Weisse Messermaus, 11:30 Schleimkoenig (Zwischenboss)
    ///  14:00  + Milchpanzer
    ///  20:00  Das Gespenst
    /// </summary>
    public static RunPlan World6()
    {
        var plan = new RunPlan("World6");

        plan.Phase(5f)
            .Pool(EnemyId.Marshmello, 100f)
            .Pressure(3.3f, 3.3f)
            .Base(Patterns.Scatter);

        plan.Phase(175f)
            .Pool(EnemyId.Marshmello, 100f)
            .Pressure(10f, 28f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Marshmello, 10f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.EvilSlime, 10f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteFluegdolch, EnemyId.Marshmello, 14, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(150f, EnemyId.Marshmello, 12f, Patterns.Column, 0f);

        plan.Phase(360f)
            .Pool(EnemyId.Marshmello, 55f)
            .Pool(EnemyId.EvilSlime, 35f)
            .Pool(EnemyId.Fluegeldolch, 10f)
            .Pressure(28f, 56f)
            .Base(Patterns.Scatter)
            .Burst(5f, EnemyId.EvilSlime, 10f, Patterns.Arc, 0f)
            .Calm(75f, 10f, 0.15f)
            .Burst(105f, EnemyId.EvilSlime, 16f, Patterns.Cluster, 0f)
            .Burst(135f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Fluegeldolch, 12f, Patterns.Arc, 0f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.EvilSlime, 18f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.Marshmello, 20f, Patterns.Arc, 0f)
            .Burst(300f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(330f, EnemyId.EvilSlime, 20f, Patterns.Column, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Marshmello, 25f)
            .Pool(EnemyId.EvilSlime, 40f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pool(EnemyId.Fluegeldolch, 10f)
            .Pressure(70f, 140f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.WeisseMessermaus, 16f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.Fluegeldolch, 12f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Boss(150f, EnemyId.Schleimkoenig, "SCHLEIMKOENIG!", 0.4f)
            .Calm(215f, 10f, 0.15f)
            .Burst(235f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Burst(250f, EnemyId.EvilSlime, 26f, Patterns.Column, 0f);

        plan.Phase(358f)
            .Pool(EnemyId.Marshmello, 15f)
            .Pool(EnemyId.EvilSlime, 35f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Fluegeldolch, 10f)
            .Pool(EnemyId.Milchpanzer, 10f)
            .Pressure(140f, 170f)
            .Base(Patterns.Scatter)
            .Burst(60f, EnemyId.Milchpanzer, 16f, Patterns.Ambush, 0f)
            .Calm(120f, 10f, 0.15f)
            .Encircle(150f, EnemyId.EliteFluegdolch, EnemyId.EvilSlime, 18, 15f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(220f, EnemyId.WeisseMessermaus, 26f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.EliteFluegdolch, 1f, Patterns.Ambush, 0f)
            .Calm(290f, 10f, 0.15f)
            .Burst(320f, EnemyId.Marshmello, 24f, Patterns.Arc, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Marshmello, 15f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Fluegeldolch, 10f)
            .Pool(EnemyId.Milchpanzer, 15f)
            .Pressure(280f, 420f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.Squiddy, "DAS GESPENST!", 0.4f);

        plan.EndlessPhase()
            .Pool(EnemyId.Marshmello, 10f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pool(EnemyId.Milchpanzer, 20f)
            .Pool(EnemyId.Fluegeldolch, 10f)
            .Pressure(100f, 100f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.EvilSlime, 20, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(90f, EnemyId.EliteFluegdolch, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
