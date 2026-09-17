using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DER SKILLTREE-KATALOG. Hier - und nur hier - werden Bäume gebaut.
///
/// Ein Knoten hinzufügen: eine Zeile in den passenden Ast. Die Position auf dem
/// Bildschirm rechnet das Spiel aus den Vorbedingungen aus (siehe
/// <see cref="SkillTreeLayout"/>) - du musst keine Koordinaten angeben.
///
/// Einen ganzen Ast wegwerfen: den Block löschen. Einen neuen anlegen:
/// Branch(...) aufrufen und Knoten dranhängen.
///
/// EIN BAUM PRO CHARAKTER (später):
/// <code>
///   Tree("char_boba", "Boba")
///       .Branch("wind", "Wind")
///           .Root("move_speed_1", SkillType.IncreaseSpeed, 0.1f, 10)
///           ...
/// </code>
/// Die Schlüssel im Spielstand sind "baum.ast.knoten", also kollidiert nichts
/// zwischen zwei Charakteren - derselbe Knotenname darf in jedem Baum vorkommen.
/// Welcher Baum gerade gilt, bestimmt <see cref="Skills.ActiveTree"/>.
///
/// SCHLÜSSEL NIEMALS ÄNDERN: Baum-, Ast- und Knotennamen landen zusammen als
/// Schlüssel in skills.json.
/// </summary>
public static class SkillTrees
{
    // Muss textlich vor allen Tree(...)-Feldern stehen.
    private static readonly List<SkillTreeDef> Registry = new List<SkillTreeDef>();

    // ==================================================================
    //  Der Baum, den heute alle Charaktere teilen
    // ==================================================================

    public static readonly SkillTreeDef Default = BuildDefault();

    private static SkillTreeDef BuildDefault()
    {
        SkillTreeBuilder tree = Tree("default", "Skill Tree");

        // ---------------------------------------------------------- Wind
        tree.Branch("wind", "Wind", icon: nameof(SkillType.IncreaseSpeed))
            .Root("move_speed_1", SkillType.IncreaseSpeed, 0.1f, 10)
            .Node("move_speed_2", SkillType.IncreaseSpeed, 0.1f, 10, "move_speed_1")
            .Node("move_speed_3", SkillType.IncreaseSpeed, 0.1f, 10, "move_speed_2")

            .Node("dodge_1", SkillType.IncreaseDodgeChance, 0.03f, 10, "move_speed_3")
            .Node("dodge_2", SkillType.IncreaseDodgeChance, 0.03f, 10, "dodge_1")
            .Node("dodge_3", SkillType.IncreaseDodgeChance, 0.03f, 10, "dodge_2")

            .Node("move_speed_4", SkillType.IncreaseSpeed, 0.1f, 10, "move_speed_3")
            .Node("move_speed_5", SkillType.IncreaseSpeed, 0.1f, 10, "move_speed_4")
            .Node("armor_1", SkillType.IncreaseArmor, 0.25f, 10, "move_speed_5")
            .Node("armor_2", SkillType.IncreaseArmor, 0.25f, 10, "armor_1")
            .Node("armor_3", SkillType.IncreaseArmor, 0.25f, 10, "armor_2")
            .Node("armor_4", SkillType.IncreaseArmor, 0.25f, 10, "armor_3");

        // --------------------------------------------------------- Sword
        tree.Branch("sword", "Sword", icon: nameof(SkillType.IncreaseDamage))
            .Root("damage_1", SkillType.IncreaseDamage, 0.25f, 10)
            .Node("damage_2", SkillType.IncreaseDamage, 0.25f, 10, "damage_1")

            .Node("crit_chance", SkillType.IncreaseCritChance, 0.05f, 50, "damage_2")
            .Node("crit_damage", SkillType.IncreaseCritDamage, 0.5f, 50, "damage_2")

            .Node("damage_3", SkillType.IncreaseDamage, 0.1f, 10, "damage_2")
            .Node("damage_4", SkillType.IncreaseDamage, 0.1f, 10, "damage_3")
            .Node("damage_5", SkillType.IncreaseDamage, 0.1f, 10, "damage_4")
            .Node("damage_6", SkillType.IncreaseDamage, 0.1f, 10, "damage_5")
            .Node("damage_7", SkillType.IncreaseDamage, 0.1f, 10, "damage_6");

        // --------------------------------------------------------- Heart
        tree.Branch("heart", "Heart", icon: nameof(SkillType.IncreaseMaxHealth))
            .Root("max_hp_1", SkillType.IncreaseMaxHealth, 5f, 10)

            .Node("max_hp_2", SkillType.IncreaseMaxHealth, 5f, 10, "max_hp_1")
            .Node("life_steal_1", SkillType.IncreaseLifeSteal, 0.2f, 10, "max_hp_2")
            .Node("life_steal_2", SkillType.IncreaseLifeSteal, 0.2f, 10, "life_steal_1")
            .Node("life_steal_3", SkillType.IncreaseLifeSteal, 0.2f, 10, "life_steal_2")

            .Node("max_hp_3", SkillType.IncreaseMaxHealth, 5f, 10, "max_hp_1")
            .Node("health_reg_1", SkillType.IncreaseHealthReg, 1f, 10, "max_hp_3")
            .Node("health_reg_2", SkillType.IncreaseHealthReg, 1f, 10, "health_reg_1")
            .Node("health_reg_3", SkillType.IncreaseHealthReg, 1f, 10, "health_reg_2")

            .Node("max_hp_4", SkillType.IncreaseMaxHealth, 10f, 20, "life_steal_3", "health_reg_3");

        // --------------------------------------------------------- Clock
        tree.Branch("clock", "Clock", icon: nameof(SkillType.xpMultiplier))
            .Root("xp_mult_1", SkillType.xpMultiplier, 0.3f, 10)

            .Node("luck_1", SkillType.IncreaseLuck, 5f, 10, "xp_mult_1")
            .Node("luck_2", SkillType.IncreaseLuck, 5f, 10, "luck_1")

            .Node("pickup_range_1", SkillType.IncreasePickupRange, 0.2f, 10, "xp_mult_1")
            .Node("pickup_range_2", SkillType.IncreasePickupRange, 0.2f, 10, "pickup_range_1")

            .Node("xp_mult_2", SkillType.xpMultiplier, 0.3f, 10, "luck_2", "pickup_range_2")

            .Node("luck_3", SkillType.IncreaseLuck, 5f, 10, "xp_mult_2")
            .Node("luck_4", SkillType.IncreaseLuck, 5f, 10, "luck_3")

            .Node("pickup_range_3", SkillType.IncreasePickupRange, 0.2f, 10, "xp_mult_2")
            .Node("pickup_range_4", SkillType.IncreasePickupRange, 0.2f, 10, "pickup_range_3")

            .Node("xp_mult_3", SkillType.xpMultiplier, 0.3f, 10, "luck_4", "pickup_range_4");

        return tree.Done();
    }

    // ==================================================================
    //  Ab hier nur noch Mechanik.
    // ==================================================================

    public static IReadOnlyList<SkillTreeDef> All => Registry;

    public static SkillTreeDef Find(string id)
    {
        foreach (SkillTreeDef t in Registry)
        {
            if (t.Id == id) return t;
        }
        return null;
    }

    /// <summary>
    /// Der Baum für einen Charakter. Solange es nur einen gibt, bekommen alle
    /// denselben; sobald "char_[index]" im Katalog steht, gewinnt der.
    /// </summary>
    public static SkillTreeDef ForCharacter(int skinIndex)
    {
        return Find($"char_{skinIndex}") ?? Default;
    }

    private static SkillTreeBuilder Tree(string id, string nameEn)
    {
        SkillTreeDef def = new SkillTreeDef(id, nameEn);
        Registry.Add(def);
        return new SkillTreeBuilder(def);
    }
}

// ======================================================================
//  Baukasten - nur dafür da, dass der Katalog oben lesbar bleibt.
// ======================================================================

public sealed class SkillTreeBuilder
{
    private readonly SkillTreeDef tree;

    internal SkillTreeBuilder(SkillTreeDef tree) => this.tree = tree;

    public SkillBranchBuilder Branch(string id, string nameEn, string icon = null)
    {
        SkillBranchDef def = new SkillBranchDef(id, nameEn, icon) { Tree = tree };
        tree.Branches.Add(def);
        return new SkillBranchBuilder(this, def);
    }

    public SkillTreeDef Done()
    {
        SkillTreeLayout.Compute(tree);
        return tree;
    }
}

public sealed class SkillBranchBuilder
{
    private readonly SkillTreeBuilder owner;
    private readonly SkillBranchDef branch;
    private SkillNodeDef last;

    internal SkillBranchBuilder(SkillTreeBuilder owner, SkillBranchDef branch)
    {
        this.owner = owner;
        this.branch = branch;
    }

    /// <summary>Anfang des Astes - ohne Vorbedingung, also von Beginn an kaufbar.</summary>
    public SkillBranchBuilder Root(string id, SkillType effect, float value, int price)
    {
        return Add(id, effect, value, price, new string[0]);
    }

    /// <summary>
    /// Ein Knoten. <paramref name="after"/> zählt die Knoten auf, die vorher offen
    /// sein müssen - meist einer, bei zusammenlaufenden Pfaden mehrere.
    /// </summary>
    public SkillBranchBuilder Node(string id, SkillType effect, float value, int price,
                                   params string[] after)
    {
        return Add(id, effect, value, price, after);
    }

    /// <summary>Eigener Beschreibungstext für den zuletzt angelegten Knoten.</summary>
    public SkillBranchBuilder Describe(string text)
    {
        if (last != null) LastDescription = text;
        return this;
    }

    /// <summary>Feste Position statt Auto-Layout, für den zuletzt angelegten Knoten.</summary>
    public SkillBranchBuilder At(float x, float y)
    {
        if (last != null) LastPosition = new Vector2(x, y);
        return this;
    }

    /// <summary>Nächsten Ast anlegen.</summary>
    public SkillBranchBuilder Branch(string id, string nameEn, string icon = null)
        => owner.Branch(id, nameEn, icon);

    public SkillTreeDef Done() => owner.Done();

    // Describe/At müssen den fertigen Knoten nachträglich ändern können; die Felder
    // sind readonly, deshalb wird der Knoten ersetzt statt verändert.
    private string LastDescription
    {
        set => Replace(value, last.Position);
    }

    private Vector2? LastPosition
    {
        set => Replace(last.DescriptionOverride, value);
    }

    private void Replace(string description, Vector2? position)
    {
        SkillNodeDef old = last;
        SkillNodeDef fresh = new SkillNodeDef(old.LocalId, old.Effect, old.Value, old.Price,
                                              description, position)
        {
            Key = old.Key,
            Branch = branch,
        };

        fresh.Requires.AddRange(old.Requires);

        foreach (SkillNodeDef parent in fresh.Requires)
        {
            parent.Unlocks.Remove(old);
            parent.Unlocks.Add(fresh);
        }

        int index = branch.Nodes.IndexOf(old);
        if (index >= 0) branch.Nodes[index] = fresh;

        last = fresh;
    }

    private SkillBranchBuilder Add(string id, SkillType effect, float value, int price,
                                   string[] after)
    {
        SkillNodeDef node = new SkillNodeDef(id, effect, value, price, null, null)
        {
            Branch = branch,
            Key = $"{branch.Tree.Id}.{branch.Id}.{id}",
        };

        foreach (string parentId in after)
        {
            SkillNodeDef parent = branch.Find(parentId);

            if (parent == null)
            {
                Debug.LogError($"[Skills] '{branch.Id}.{id}': Vorbedingung '{parentId}' gibt es " +
                               "im Ast nicht (oder sie steht weiter unten - Knoten müssen nach " +
                               "ihren Vorbedingungen kommen).");
                continue;
            }

            node.Requires.Add(parent);
            parent.Unlocks.Add(node);
        }

        branch.Nodes.Add(node);
        last = node;
        return this;
    }
}
