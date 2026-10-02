using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skilltree "Abpraller" (<see cref="SkillGrants.ShurikookieAbpraller"/>):
/// trifft der Shurikookie einen Gegner, zerbricht er nicht, sondern prallt zum
/// naechsten Gegner in der Naehe ab. Gemeinsam fuer Shurikookie
/// (<see cref="ShurikenWeaponPrefab"/>) und die Evo Shuri Blast
/// (<see cref="ShuriBlastEvo"/>).
/// </summary>
public static class Ricochet
{
    /// <summary>So oft prallt ein Wurfstern hoechstens ab.</summary>
    public const int Bounces = 2;

    /// <summary>So weit (Einheiten) sucht er nach dem naechsten Gegner.</summary>
    public const float Range = 6f;

    /// <summary>Wie viele Abpraller ein frisch geworfener Stern hat - 0 ohne den Skill.</summary>
    public static int BouncesForThrow()
    {
        return Skills.HasGrant(SkillGrants.ShurikookieAbpraller) ? Bounces : 0;
    }

    /// <summary>
    /// Der naechste lebende Gegner um <paramref name="from"/> innerhalb von
    /// <see cref="Range"/>, der nicht in <paramref name="exclude"/> steht.
    /// Null, wenn keiner da ist.
    /// </summary>
    public static Enemy Nearest(Vector2 from, ICollection<Enemy> exclude)
    {
        Enemy best = null;
        float bestSqr = Range * Range;
        var enemies = Enemy.Alive;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || (exclude != null && exclude.Contains(e))) continue;

            float d = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = e;
            }
        }

        return best;
    }
}
