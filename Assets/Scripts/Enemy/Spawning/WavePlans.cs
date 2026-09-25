using UnityEngine;

/// <summary>
/// Die Wellenplaene - einer je Karte. Das ist der Ort fuers Balancing.
///
/// Gelesen wird ein Plan von oben nach unten: Phasen der Reihe nach, in jeder
/// Phase laeuft der Grunddruck von <c>Pressure(von, bis)</c> hoch, und an den
/// Beats passiert etwas Besonderes. "Druck" ist keine Stueckzahl, sondern die
/// Summe der Gewichte aus <see cref="EnemyCatalog.Threat"/>: Druck 60 sind 60
/// Marshmellos, gut 20 Elite-Marshmellos oder 8 Fettis. Der
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
    /// Holt den Plan zur Karte. Unbekannt = der Plan von World0.
    ///
    /// Gespielt werden zurzeit World0 (Kueche, Karte 1) und World3 (Wald,
    /// Karte 2). World1 und World2 sind rausgeflogen.
    /// </summary>
    public static RunPlan For(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world3": return World3();
            default: return World0();
        }
    }

    /// <summary>Alle Plaene, die es gibt - fuer die Werkstatt und den Vergleich.</summary>
    public static readonly string[] AllIds = { "World0", "World3" };

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================

    // ------------------------------------------------------------- World0

    /// <summary>
    /// Kueche - die Einstiegskarte (Karte 1 in der Levelauswahl). Dicht am
    /// alten Ablauf: erst Marshmellos, dann Milch und Muffins, zum Schluss der
    /// Keks-Koenig.
    /// </summary>
    public static RunPlan World0()
    {
        var plan = new RunPlan("World0");

        plan.Phase("Ankunft", 300f)
            .Pool(EnemyId.Marshmello, 70f)
            .Pool(EnemyId.EvilSlime, 30f)
            .Pressure(12f, 55f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.EvilSlime, 18f, Patterns.Arc, 0f)
            .Burst(90f, EnemyId.Marshmello, 25f, Patterns.Column, 0f)
            .Encircle(150f, EnemyId.MiniBossMarshmello, EnemyId.Marshmello, 18, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Calm(195f, 10f, 0.15f)
            .Burst(225f, EnemyId.MausMitMesser, 24f, Patterns.Cluster, 0f)
            .Burst(270f, EnemyId.EvilSlime, 30f, Patterns.Scatter, 0f);

        plan.Phase("Zuckerguss", 300f)
            .Pool(EnemyId.EliteMarshmello, 45f)
            .Pool(EnemyId.MiniMilch, 35f)
            .Pool(EnemyId.EvilSlime, 20f)
            .Pressure(60f, 130f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.SaureMilch, 30f, Patterns.Ambush, 0f)
            .Calm(95f, 10f, 0.15f)
            .Encircle(125f, EnemyId.MesserMaus1, EnemyId.EliteMarshmello, 22, 14f, true, "MESSERMAUS!", 0.35f, 25f, 1.5f)
            .Burst(195f, EnemyId.Muffin, 35f, Patterns.Cluster, 0f)
            .Burst(250f, EnemyId.MiniMilch, 45f, Patterns.Column, 0f);

        plan.Phase("Sturm", 290f)
            .Pool(EnemyId.Muffin, 40f)
            .Pool(EnemyId.Pancake, 35f)
            .Pool(EnemyId.Suppe, 25f)
            .Pressure(130f, 220f)
            .Base(Patterns.Scatter)
            .Burst(50f, EnemyId.Fetti, 40f, Patterns.Arc, 0f)
            .Calm(110f, 8f, 0.15f)
            .Encircle(145f, EnemyId.MesserMaus2, EnemyId.Muffin, 26, 15f, true, "MESSERRATTE!", 0.35f, 25f, 1.5f)
            .Burst(225f, EnemyId.Fetti, 60f, Patterns.Cluster, 0f);

        plan.Phase("Keks-Koenig", 600f)
            .Pool(EnemyId.Slime, 100f)
            .Pressure(90f, 120f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f);

        plan.EndlessPhase("Endlos")
            .Pool(EnemyId.Slime, 70f)
            .Pool(EnemyId.Fetti, 15f)
            .Pool(EnemyId.Suppe, 15f)
            .Pressure(140f, 140f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Slime, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f);

        return plan;
    }

    // ------------------------------------------------------------- World3

    /// <summary>
    /// Wald - Karte 2 in der Levelauswahl. Eigener Gegnersatz: hier laeuft
    /// kein einziger Gegner aus der Kueche herum, nur der Keks-Koenig am Ende
    /// ist derselbe.
    ///
    /// Der Anstieg ist bewusst anders als in der Kueche: der Anfang liegt
    /// gleichauf (Druck 14 gegen 12), das Ende deutlich darueber (290 gegen
    /// 220). Wer hier ankommt, hat die Kueche hinter sich und einen Skilltree -
    /// die Karte darf ihn also hinten fordern, ohne ihn vorne zu erschlagen.
    ///
    /// Die Reihenfolge der Gegner folgt der Ansage: Pilz, Fluegeldolch,
    /// Eichel, Messermaus, Milchpanzer. Der Kirschslime war in der Liste nicht
    /// dabei - er sitzt hier als Fuellgegner der mittleren Phasen und im
    /// Finale. Passt das nicht, ist er in der Werkstatt ein Klick.
    /// </summary>
    public static RunPlan World3()
    {
        var plan = new RunPlan("World3");

        plan.Phase("Waldrand", 300f)
            .Pool(EnemyId.Fliegenpilz, 60f)
            .Pool(EnemyId.Fluegeldolch, 40f)
            .Pressure(14f, 58f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Fluegeldolch, 20f, Patterns.Arc, 0f)
            .Burst(85f, EnemyId.Fliegenpilz, 24f, Patterns.Column, 0f)
            .Calm(120f, 8f, 0.15f)
            .Burst(215f, EnemyId.Fluegeldolch, 28f, Patterns.Ambush, 0f)
            .Burst(265f, EnemyId.Fliegenpilz, 32f, Patterns.Cluster, 0f);

        plan.Phase("Dickicht", 300f)
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pressure(70f, 150f)
            .Base(Patterns.Column)
            .Burst(45f, EnemyId.WeisseMessermaus, 30f, Patterns.Cluster, 0f)
            .Burst(95f, EnemyId.Marshmello, 32f, Patterns.Arc, 0f)
            .Calm(140f, 10f, 0.15f)
            .Burst(240f, EnemyId.Kirschslime, 40f, Patterns.Ring, 13f)
            .Burst(280f, EnemyId.Marshmello, 35f, Patterns.Ambush, 0f);

        plan.Phase("Sturm", 290f)
            .Pool(EnemyId.Milchpanzer, 35f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pressure(165f, 290f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Milchpanzer, 55f, Patterns.Arc, 0f)
            .Calm(100f, 8f, 0.15f)
            .Burst(250f, EnemyId.Milchpanzer, 70f, Patterns.Cluster, 0f);

        plan.Phase("Keks-Koenig", 600f)
            .Pool(EnemyId.Kirschslime, 100f)
            .Pressure(100f, 135f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f);

        plan.EndlessPhase("Endlos")
            .Pool(EnemyId.Kirschslime, 60f)
            .Pool(EnemyId.Milchpanzer, 25f)
            .Pressure(175f, 175f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Kirschslime, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f);

        return plan;
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
