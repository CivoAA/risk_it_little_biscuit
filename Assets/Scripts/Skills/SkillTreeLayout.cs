using UnityEngine;

/// <summary>
/// Rechnet aus, wo ein Knoten haengt - damit im Baum-Asset keine Koordinaten
/// stehen muessen.
///
/// Das Verfahren ist absichtlich stumpf: die Bahn bestimmt die Hoehe, der
/// Schritt die Position von links. Der Baum waechst also nach RECHTS, genau wie
/// im Konzeptbild - oben, Mitte und unten laufen parallel, Verbindungen duerfen
/// zwischen den Bahnen springen.
///
/// Nullpunkt ist der Startknoten der mittleren Bahn (Schritt 0, Bahn Mitte).
/// Y zaehlt wie in Unity nach oben.
///
/// Die Abstaende stehen hier und nicht in der UI, weil der Editor mit denselben
/// rechnet - so sieht der Baum im Fenster aus wie im Spiel.
/// </summary>
public static class SkillTreeLayout
{
    /// <summary>Waagerechter Abstand zweier Schritte.</summary>
    public static float SpacingX = 34f;

    /// <summary>Senkrechter Abstand zweier Bahnen.</summary>
    public static float SpacingY = 30f;

    /// <summary>Wie hoch eine Bahn ueber der Mitte liegt: oben +1, Mitte 0, unten -1.</summary>
    public static int LaneOffset(SkillLane lane)
    {
        switch (lane)
        {
            case SkillLane.Oben:  return 1;
            case SkillLane.Unten: return -1;
            default:              return 0;
        }
    }

    public static void Compute(SkillTreeDef tree)
    {
        if (tree == null) return;
        foreach (SkillBranchDef branch in tree.Branches) Compute(branch);
    }

    public static void Compute(SkillBranchDef branch)
    {
        if (branch == null) return;

        int max = 0;
        foreach (SkillNodeDef node in branch.Nodes) max = Mathf.Max(max, node.Step);
        branch.MaxStep = max;
    }

    /// <summary>Position eines Knotens, relativ zum Startknoten der mittleren Bahn.</summary>
    public static Vector2 PositionOf(SkillNodeDef node)
    {
        if (node == null) return Vector2.zero;
        return PositionOf(node.Lane, node.Step);
    }

    public static Vector2 PositionOf(SkillLane lane, int step)
    {
        return new Vector2(step * SpacingX, LaneOffset(lane) * SpacingY);
    }

    /// <summary>
    /// Wie breit eine Kategorie insgesamt ist - daraus ergibt sich, wie weit das
    /// Feld im Hub nach rechts gescrollt werden kann.
    /// </summary>
    public static float WidthOf(SkillBranchDef branch)
    {
        if (branch == null) return 0f;
        return branch.MaxStep * SpacingX;
    }

    /// <summary>Hoehe ueber alle drei Bahnen.</summary>
    public static float HeightOf() => 2f * SpacingY;
}
