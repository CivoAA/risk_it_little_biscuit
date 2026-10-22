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
///   ab Level 3 kostet Level n  Level2Xp * (1 + StepGrowth * k + EndGrowth * k²)
///   mit k = n - 2. Der lineare Teil traegt die ersten Level, der quadratische
///   macht die letzten Level deutlich teurer.
///
/// EndGrowth wird nicht eingestellt, sondern so gerechnet, dass Level
/// FullTreeLevel (ein voller Baum, ohne Boss-Level) genau FullTreeXp kostet.
///
/// Zum Nachstellen reichen die Zahlen unten.
/// </summary>
public static class CharLevel
{
    public const double Level1Xp   = 1750;
    public const double Level2Xp   = 16200;
    public const double StepGrowth = 0.15;

    /// <summary>Level, ab dem ein voller Baum (41 Punkte) bezahlt ist.</summary>
    public const int    FullTreeLevel = 41;

    /// <summary>Gesamt-XP fuer FullTreeLevel. Vorher 2.543.400.</summary>
    public const double FullTreeXp    = 7_500_000;

    /// <summary>Quadratischer Anteil, so gerechnet, dass FullTreeLevel genau FullTreeXp kostet.</summary>
    public static readonly double EndGrowth =
        (FullTreeXp / Level2Xp - (FullTreeLevel - 1) - StepGrowth * SumK(FullTreeLevel)) / SumK2(FullTreeLevel);

    /// <summary>
    /// So viel von den XP der ANDEREN Charaktere zaehlt bei jedem Charakter mit.
    /// Geteilt wird nur, was ein Charakter selbst gesammelt hat - die geteilten
    /// XP werden nicht noch einmal weitergereicht.
    /// </summary>
    public const double SharedXpFactor = 1.0 / 3.0;

    /// <summary>
    /// Auf die ganze Kurve (0.9 = jedes Level kostet 10 % weniger). Die Zahlen
    /// oben bleiben der Bezug, der Faktor kommt erst in <see cref="XpForLevel"/> drauf.
    /// Seit 22.10.2026 das vorherige 0.9 durch 2,5 - dafuer gilt <see cref="MaxLevelsPerRun"/>.
    /// </summary>
    public const double CostFactor = 0.9 / 2.5;

    /// <summary>
    /// So viele Charakter-Level (= Skillpunkte) bringen die XP eines Laufs hoechstens.
    /// Was darueber hinaus gesammelt wird, verfaellt. Boss-Erstsiege zaehlen nicht mit.
    /// </summary>
    public const int MaxLevelsPerRun = 3;

    /// <summary>Obergrenze, nur damit keine Schleife endlos laeuft.</summary>
    public const int MaxLevel = 200;

    // ------------------------------------------------------------ Nachhol-Bonus
    //
    //  Jede Map hat ein Ziel-Level (RunDifficulty.CatchUpTarget). Liegt der
    //  Charakter darunter, zaehlen seine Charakter-XP mehr - je weiter drunter,
    //  desto mehr, bis CatchUpMax. Das Level wird bei jeder XP neu gelesen,
    //  der Bonus schrumpft also schon im Lauf und ist ab dem Ziel-Level weg.
    //  Nur Charakter-XP, die XP im Lauf (Level-Ups) bleiben wie sie sind.

    /// <summary>Bonus je Level unter dem Ziel (0.2 = +20 %).</summary>
    public const float CatchUpPerLevel = 0.2f;

    /// <summary>Hoechster Faktor, egal wie weit unter dem Ziel.</summary>
    public const float CatchUpMax = 3f;

    /// <summary>Faktor auf die Charakter-XP bei diesem Level und diesem Ziel-Level.</summary>
    public static float CatchUpFactor(int level, int target)
    {
        int gap = target - level;
        if (gap <= 0) return 1f;
        return Mathf.Min(CatchUpMax, 1f + gap * CatchUpPerLevel);
    }

    /// <summary>Gesamt-XP, ab der ein Charakter dieses Level hat.</summary>
    public static double XpForLevel(int level)
    {
        if (level <= 0) return 0;
        if (level == 1) return System.Math.Round(Level1Xp * CostFactor);

        return System.Math.Round(Level2Xp * CostFactor * ((level - 1) + StepGrowth * SumK(level) + EndGrowth * SumK2(level)));
    }

    /// <summary>XP von Level n-1 auf Level n.</summary>
    public static double StepXp(int level) => XpForLevel(level) - XpForLevel(level - 1);

    /// <summary>Das Level bei so viel Gesamt-XP.</summary>
    public static int LevelFor(double xp)
    {
        int level = 0;
        while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
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

    // Summe k und k² fuer k = 1 .. level-2 (die Schritte ab Level 3).
    static double SumK(int level)  { double m = level - 2; return m * (m + 1) / 2; }
    static double SumK2(int level) { double m = level - 2; return m * (m + 1) * (2 * m + 1) / 6; }
}
