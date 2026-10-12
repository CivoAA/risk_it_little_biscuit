using UnityEngine;

/// <summary>
/// Skilltree "Bass-Sog" (<see cref="SkillGrants.BassSog"/>), nur fuer die Evo
/// der Mochi-Melodie: trifft eine Musikbox einen Gegner (Note oder Akkord),
/// laeuft der mit <see cref="Chance"/> eine Weile auf diese Box zu statt auf
/// Mochi. Treffen ihn mehrere Boxen, gilt die letzte (<see cref="Enemy.Lure"/>).
///
/// Die Box ruft bei jedem Treffer <see cref="OnBoxHit"/> auf - mehr braucht es nicht.
/// </summary>
public static class BassSog
{
    /// <summary>Chance je Treffer einer Box.</summary>
    public const float Chance = 0.2f;

    /// <summary>So lange laeuft der Gegner auf die Box zu (Sekunden).</summary>
    public const float Duration = 3f;

    /// <summary>Ein Treffer der Box <paramref name="box"/> auf <paramref name="enemy"/>.</summary>
    public static void OnBoxHit(Enemy enemy, Transform box)
    {
        if (enemy == null || box == null || !enemy.isActiveAndEnabled) return;
        if (!Skills.HasGrant(SkillGrants.BassSog)) return;
        if (Random.value >= Chance) return;

        enemy.Lure(box, Duration);
    }
}
