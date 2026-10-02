using UnityEngine;

/// <summary>
/// DIE LEVELKURVE DER CHARAKTERE (ausserhalb des Laufs).
///
/// Jede XP, die ein Charakter im Lauf einsammelt (mit allen Multiplikatoren),
/// landet auch auf seinem Konto ausserhalb des Laufs. Jedes Charakter-Level
/// gibt einen Skillpunkt fuer seinen Skilltree.
///
/// Die Kurve:
///   Level 1 = so viel XP wie ein Lauf bis Ingame-Level 15   (1.750)
///   Level 2 = so viel XP wie ein Lauf bis Ingame-Level 30   (16.200)
///   ab Level 3 kostet jedes Level Level2Xp * (1 + StepGrowth * (n - 2)),
///   also jedes etwas mehr als das davor - linear, nicht exponentiell.
///
/// Zum Nachstellen reichen die drei Zahlen unten.
/// </summary>
public static class CharLevel
{
    public const double Level1Xp   = 1750;
    public const double Level2Xp   = 16200;
    public const double StepGrowth = 0.15;

    /// <summary>Obergrenze, nur damit keine Schleife endlos laeuft.</summary>
    public const int MaxLevel = 200;

    /// <summary>Gesamt-XP, ab der ein Charakter dieses Level hat.</summary>
    public static double XpForLevel(int level)
    {
        if (level <= 0) return 0;
        if (level == 1) return Level1Xp;

        double total = Level2Xp;
        for (int n = 3; n <= level; n++) total += StepXp(n);
        return total;
    }

    /// <summary>XP von Level n-1 auf Level n.</summary>
    public static double StepXp(int level)
    {
        if (level <= 0) return 0;
        if (level == 1) return Level1Xp;
        if (level == 2) return Level2Xp - Level1Xp;
        return System.Math.Round(Level2Xp * (1 + StepGrowth * (level - 2)));
    }

    /// <summary>Das Level bei so viel Gesamt-XP.</summary>
    public static int LevelFor(double xp)
    {
        int level = 0;
        double next = Level1Xp;

        while (level < MaxLevel && xp >= next)
        {
            level++;
            next += StepXp(level + 1);
        }

        return level;
    }

    /// <summary>Fortschritt zum naechsten Level, 0..1.</summary>
    public static float Progress(double xp)
    {
        int level = LevelFor(xp);
        if (level >= MaxLevel) return 1f;

        double from = XpForLevel(level);
        double to   = XpForLevel(level + 1);
        return Mathf.Clamp01((float)((xp - from) / (to - from)));
    }
}
