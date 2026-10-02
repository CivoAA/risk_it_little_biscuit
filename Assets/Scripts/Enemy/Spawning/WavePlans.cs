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
    /// Gespielt werden zurzeit World1 (Kueche, Karte 1, Szene Map_World0) und
    /// World2 (Wald, Karte 2, Szene Map_World3 - planId dort fest eingetragen).
    /// </summary>
    public static RunPlan For(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world2": return World2();
            default: return World1();
        }
    }

    /// <summary>Alle Plaene, die es gibt - fuer die Werkstatt und den Vergleich.</summary>
    public static readonly string[] AllIds = { "World1", "World2" };

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================

    // ------------------------------------------------------------- World1

    /// <summary>
    /// Kueche - die Einstiegskarte (Karte 1 in der Levelauswahl). Dicht am
    /// alten Ablauf: erst Marshmellos, dann Milch und Muffins, zum Schluss der
    /// Keks-Koenig.
    /// </summary>
    public static RunPlan World1()
    {
        var plan = new RunPlan("World1");

        plan.Phase(300f)
            .Pool(EnemyId.Marshmello, 70f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pressure(12f, 80f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.EvilSlime, 18f, Patterns.Arc, 0f)
            .Burst(80f, EnemyId.Marshmello, 25f, Patterns.Column, 0f)
            .Encircle(105f, EnemyId.EliteFliegenpilz, EnemyId.Marshmello, 18, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.MausMitMesser, 24f, Patterns.Cluster, 0f)
            .Burst(250f, EnemyId.EliteFliegenpilz, 1f, Patterns.Ambush, 0f)
            .Burst(270f, EnemyId.EvilSlime, 30f, Patterns.Scatter, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Marshmello, 45f)
            .Pool(EnemyId.MiniMilch, 35f)
            .Pool(EnemyId.EvilSlime, 20f)
            .Pressure(100f, 200f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.SaureMilch, 30f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Ambush, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.MesserMaus1, EnemyId.Marshmello, 22, 14f, true, "MESSERMAUS!", 0.35f, 25f, 1.5f)
            .Burst(195f, EnemyId.Muffin, 35f, Patterns.Cluster, 0f)
            .Burst(250f, EnemyId.MiniMilch, 45f, Patterns.Column, 0f);

        plan.Phase(290f)
            .Pool(EnemyId.Muffin, 40f)
            .Pool(EnemyId.Pancake, 35f)
            .Pool(EnemyId.Suppe, 25f)
            .Pressure(240f, 350f)
            .Base(Patterns.Scatter)
            .Burst(50f, EnemyId.Fetti, 40f, Patterns.Arc, 0f)
            .Calm(110f, 8f, 0.15f)
            .Encircle(145f, EnemyId.MesserMaus2, EnemyId.Muffin, 26, 15f, true, "MESSERRATTE!", 0.35f, 25f, 1.5f)
            .Burst(225f, EnemyId.Fetti, 60f, Patterns.Cluster, 0f);

        plan.Phase(600f)
            .Pool(EnemyId.Slime, 100f)
            .Pressure(400f, 600f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f)
            .Burst(240f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Arc, 0f);

        plan.EndlessPhase()
            .Pool(EnemyId.Slime, 70f)
            .Pool(EnemyId.Fetti, 15f)
            .Pool(EnemyId.Suppe, 15f)
            .Pressure(140f, 140f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Slime, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f);

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
    /// </summary>
    public static RunPlan World2()
    {
        var plan = new RunPlan("World2");

        plan.Phase(120f)
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
            .Burst(135f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Ambush, 0f)
            .Burst(150f, EnemyId.Kirschslime, 30f, Patterns.Scatter, 0f);

        plan.Phase(300f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 25f)
            .Pool(EnemyId.Eichel, 30f)
            .Pool(EnemyId.Fluegeldolch, 15f)
            .Pressure(100f, 200f)
            .Base(Patterns.Scatter)
            .Burst(20f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Ambush, 0f)
            .Burst(40f, EnemyId.Eichel, 25f, Patterns.Ambush, 0f)
            .Burst(60f, EnemyId.Fluegeldolch, 20f, Patterns.Arc, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.EliteFliegenpilz, EnemyId.Kirschslime, 22, 14f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(125f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Ambush, 0f)
            .Burst(195f, EnemyId.WeisseMessermaus, 30f, Patterns.Cluster, 0f)
            .Burst(225f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Arc, 0f)
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
            .Burst(80f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Arc, 0f)
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
            .Burst(90f, EnemyId.EliteMarshmelloGross, 1f, Patterns.Arc, 0f);

        return plan;
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
