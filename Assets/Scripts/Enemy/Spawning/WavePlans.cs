using UnityEngine;

/// <summary>
/// Die Wellenplaene - einer je Karte. Das ist der Ort fuers Balancing.
///
/// Gelesen wird ein Plan von oben nach unten: Phasen der Reihe nach, in jeder
/// Phase laeuft der Grunddruck von <c>Pressure(von, bis)</c> hoch, und an den
/// Beats passiert etwas Besonderes. "Druck" ist keine Stueckzahl, sondern die
/// Summe der Gewichte aus <see cref="EnemyCatalog.Threat"/>: Druck 60 sind 60
/// Marshmellos, 15 Elite-Marshmellos oder 7 Fettis. Der
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
    /// Karte 2). Die Plaene fuer World1 und World2 bleiben liegen, bis
    /// entschieden ist, ob die Welten wirklich verschwinden.
    /// </summary>
    public static RunPlan For(string planId)
    {
        switch ((planId ?? "").Trim().ToLowerInvariant())
        {
            case "world1": return World1();
            case "world2": return World2();
            case "world3": return World3();
            default: return World0();
        }
    }

    /// <summary>Alle Plaene, die es gibt - fuer die Werkstatt und den Vergleich.</summary>
    public static readonly string[] AllIds = { "World0", "World1", "World2", "World3" };

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================

    // ------------------------------------------------------------- World 0

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

    // ------------------------------------------------------------- World 1

    /// <summary>
    /// Nachtwald - offenes 3x3-Gelaende. Spielt mit Flanken: Kolonnen und
    /// Boegen in Laufrichtung statt gleichmaessigem Regen.
    /// </summary>
    public static RunPlan World1()
    {
        var plan = new RunPlan("World1");

        plan.Phase("Daemmerung", 300f)
            .Pool(EnemyId.Marshmello, 55f)
            .Pool(EnemyId.EvilSlime, 45f)
            .Pressure(15f, 65f)
            .Base(Patterns.Arc)
            .Burst(40f, EnemyId.Marshmello, 22f, Patterns.Column, 0f)
            .Burst(85f, EnemyId.MausMitMesser, 20f, Patterns.Ambush, 0f)
            .Calm(130f, 8f, 0.15f)
            .Encircle(160f, EnemyId.MiniBossMarshmello, EnemyId.EvilSlime, 20, 13f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(230f, EnemyId.EliteMarshmello, 35f, Patterns.Column, 0f);

        plan.Phase("Dickicht", 300f)
            .Pool(EnemyId.EliteMarshmello, 50f)
            .Pool(EnemyId.MausMitMesser, 30f)
            .Pool(EnemyId.MiniMilch, 20f)
            .Pressure(70f, 145f)
            .Base(Patterns.Column)
            .Burst(45f, EnemyId.SaureMilch, 35f, Patterns.Arc, 0f)
            .Encircle(110f, EnemyId.MesserMaus1, EnemyId.MausMitMesser, 24, 14f, true, "MESSERMAUS!", 0.35f, 25f, 1.5f)
            .Calm(175f, 10f, 0.15f)
            .Burst(205f, EnemyId.Muffin, 40f, Patterns.Cluster, 0f)
            .Burst(260f, EnemyId.EliteMarshmello, 50f, Patterns.Ambush, 0f);

        plan.Phase("Sturm", 290f)
            .Pool(EnemyId.Pancake, 40f)
            .Pool(EnemyId.Suppe, 30f)
            .Pool(EnemyId.Muffin, 30f)
            .Pressure(145f, 235f)
            .Base(Patterns.Scatter)
            .Burst(45f, EnemyId.Fetti, 45f, Patterns.Column, 0f)
            .Calm(105f, 8f, 0.15f)
            .Encircle(140f, EnemyId.MesserMaus2, EnemyId.Pancake, 26, 15f, true, "MESSERRATTE!", 0.35f, 25f, 1.5f)
            .Burst(215f, EnemyId.Fetti, 65f, Patterns.Arc, 0f);

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

    // ------------------------------------------------------------- World 2

    /// <summary>
    /// Dorf - enger gedacht. Hier kommt alles in Rudeln: weniger Dauerregen,
    /// dafuer Blocks, die man umlaufen muss.
    /// </summary>
    public static RunPlan World2()
    {
        var plan = new RunPlan("World2");

        plan.Phase("Gassen", 300f)
            .Pool(EnemyId.Marshmello, 50f)
            .Pool(EnemyId.MiniMilch, 50f)
            .Pressure(14f, 60f)
            .Base(Patterns.Cluster)
            .Burst(50f, EnemyId.EvilSlime, 20f, Patterns.Ring, 12f)
            .Calm(100f, 8f, 0.15f)
            .Encircle(135f, EnemyId.MiniBossMarshmello, EnemyId.MiniMilch, 20, 12f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(200f, EnemyId.MausMitMesser, 28f, Patterns.Ambush, 0f)
            .Burst(255f, EnemyId.Marshmello, 35f, Patterns.Cluster, 0f);

        plan.Phase("Marktplatz", 300f)
            .Pool(EnemyId.SaureMilch, 40f)
            .Pool(EnemyId.Muffin, 35f)
            .Pool(EnemyId.EliteMarshmello, 25f)
            .Pressure(65f, 140f)
            .Base(Patterns.Cluster)
            .Encircle(60f, EnemyId.MesserMaus1, EnemyId.SaureMilch, 22, 13f, true, "MESSERMAUS!", 0.35f, 25f, 1.5f)
            .Calm(120f, 12f, 0.15f)
            .Burst(155f, EnemyId.Pancake, 40f, Patterns.Column, 0f)
            .Encircle(215f, EnemyId.MiniBossMarshmello, EnemyId.Muffin, 24, 14f, false, "RING!", 0.35f, 25f, 1.5f)
            .Burst(270f, EnemyId.Suppe, 45f, Patterns.Ambush, 0f);

        plan.Phase("Backstube", 290f)
            .Pool(EnemyId.Suppe, 40f)
            .Pool(EnemyId.Fetti, 25f)
            .Pool(EnemyId.Pancake, 35f)
            .Pressure(140f, 215f)
            .Base(Patterns.Cluster)
            .Burst(55f, EnemyId.Fetti, 50f, Patterns.Ring, 13f)
            .Calm(115f, 8f, 0.15f)
            .Encircle(150f, EnemyId.MesserMaus2, EnemyId.Suppe, 26, 15f, true, "MESSERRATTE!", 0.35f, 25f, 1.5f)
            .Burst(230f, EnemyId.Fetti, 70f, Patterns.Cluster, 0f);

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

    // ------------------------------------------------------------- World 3

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
            .Encircle(150f, EnemyId.Pilzkoenig, EnemyId.Fliegenpilz, 20, 12.5f, false, "PILZKOENIG!", 0.35f, 25f, 1.5f)
            .Burst(215f, EnemyId.Fluegeldolch, 28f, Patterns.Ambush, 0f)
            .Burst(265f, EnemyId.Fliegenpilz, 32f, Patterns.Cluster, 0f);

        plan.Phase("Dickicht", 300f)
            .Pool(EnemyId.Eichel, 40f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.WeisseMessermaus, 30f)
            .Pressure(70f, 150f)
            .Base(Patterns.Column)
            .Burst(45f, EnemyId.WeisseMessermaus, 30f, Patterns.Cluster, 0f)
            .Burst(95f, EnemyId.Sturmeichel, 32f, Patterns.Arc, 0f)
            .Calm(140f, 10f, 0.15f)
            .Encircle(175f, EnemyId.Rattenkoenigin, EnemyId.WeisseMessermaus, 24, 14f, true, "RATTENKOENIGIN!", 0.35f, 25f, 1.5f)
            .Burst(240f, EnemyId.Kirschslime, 40f, Patterns.Ring, 13f)
            .Burst(280f, EnemyId.Dolchschwarm, 35f, Patterns.Ambush, 0f);

        plan.Phase("Sturm", 290f)
            .Pool(EnemyId.Milchpanzer, 35f)
            .Pool(EnemyId.Kirschslime, 30f)
            .Pool(EnemyId.Sturmeichel, 20f)
            .Pool(EnemyId.Dolchschwarm, 15f)
            .Pressure(165f, 290f)
            .Base(Patterns.Scatter)
            .Burst(40f, EnemyId.Milchpanzer, 55f, Patterns.Arc, 0f)
            .Calm(100f, 8f, 0.15f)
            .Encircle(135f, EnemyId.Milchkoloss, EnemyId.Milchpanzer, 24, 15f, true, "MILCHKOLOSS!", 0.35f, 25f, 1.5f)
            .Burst(200f, EnemyId.Dolchschwarm, 50f, Patterns.Column, 0f)
            .Burst(250f, EnemyId.Milchpanzer, 70f, Patterns.Cluster, 0f);

        plan.Phase("Keks-Koenig", 600f)
            .Pool(EnemyId.Kirschslime, 100f)
            .Pressure(100f, 135f)
            .Base(Patterns.Scatter)
            .Boss(2f, EnemyId.KeksKoenig, "KEKS-KOENIG", 0.4f);

        plan.EndlessPhase("Endlos")
            .Pool(EnemyId.Kirschslime, 60f)
            .Pool(EnemyId.Milchpanzer, 25f)
            .Pool(EnemyId.Sturmeichel, 15f)
            .Pressure(175f, 175f)
            .Base(Patterns.Scatter)
            .Encircle(60f, EnemyId.None, EnemyId.Kirschslime, 26, 14f, false, "RING!", 0.35f, 25f, 1.5f);

        return plan;
    }

    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
