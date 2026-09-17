using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rechnet aus, wo die Knoten eines Astes hängen - damit im Katalog keine
/// Koordinaten stehen müssen.
///
/// Das Verfahren ist absichtlich einfach und vorhersagbar: Die Ebene eines
/// Knotens ist der längste Weg von einer Wurzel zu ihm. Alle Knoten einer Ebene
/// werden nebeneinander gelegt und zentriert. Der Ast wächst also von oben nach
/// unten, Abzweigungen laufen auseinander und wieder zusammen.
///
/// Wer einen Knoten doch woanders haben will, schreibt im Katalog .At(x, y)
/// dahinter - das gewinnt immer.
/// </summary>
public static class SkillTreeLayout
{
    /// <summary>Waagerechter Abstand zweier Knoten derselben Ebene.</summary>
    public static float SpacingX = 110f;

    /// <summary>Senkrechter Abstand zweier Ebenen.</summary>
    public static float SpacingY = 95f;

    public static void Compute(SkillTreeDef tree)
    {
        foreach (SkillBranchDef branch in tree.Branches) Compute(branch);
    }

    public static void Compute(SkillBranchDef branch)
    {
        // Ebene = längster Weg von einer Wurzel. Weil ein Knoten im Katalog immer
        // nach seinen Vorbedingungen steht, reicht ein Durchlauf in Reihenfolge.
        foreach (SkillNodeDef node in branch.Nodes)
        {
            int depth = 0;

            foreach (SkillNodeDef parent in node.Requires)
            {
                depth = Mathf.Max(depth, parent.Depth + 1);
            }

            node.Depth = depth;
        }

        // Spalte = Platz innerhalb der Ebene, in Reihenfolge des Katalogs.
        var perDepth = new Dictionary<int, int>();

        foreach (SkillNodeDef node in branch.Nodes)
        {
            perDepth.TryGetValue(node.Depth, out int used);
            node.Column = used;
            perDepth[node.Depth] = used + 1;
        }

        // Zum Zentrieren wird gleich noch die Breite jeder Ebene gebraucht.
        branch.LevelWidths = perDepth;
    }

    /// <summary>Position eines Knotens im Ast-Panel. Eine feste Position gewinnt.</summary>
    public static Vector2 PositionOf(SkillNodeDef node)
    {
        if (node.Position.HasValue) return node.Position.Value;

        int count = 1;
        if (node.Branch != null && node.Branch.LevelWidths != null)
            node.Branch.LevelWidths.TryGetValue(node.Depth, out count);

        if (count < 1) count = 1;

        float offset = (node.Column - (count - 1) * 0.5f) * SpacingX;
        return new Vector2(offset, -node.Depth * SpacingY);
    }

    /// <summary>Grösse, die der Ast insgesamt einnimmt - für Scrollbereich und Zoom.</summary>
    public static Vector2 SizeOf(SkillBranchDef branch)
    {
        int levels = 1;
        int widest = 1;

        foreach (SkillNodeDef node in branch.Nodes)
        {
            levels = Mathf.Max(levels, node.Depth + 1);
        }

        if (branch.LevelWidths != null)
        {
            foreach (KeyValuePair<int, int> pair in branch.LevelWidths)
                widest = Mathf.Max(widest, pair.Value);
        }

        return new Vector2(widest * SpacingX, levels * SpacingY);
    }
}
