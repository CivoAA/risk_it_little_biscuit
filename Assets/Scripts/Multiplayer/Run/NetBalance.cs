using UnityEngine;

/// <summary>
/// Alle Stellschrauben des Koop-Modus an einem Ort.
///
/// SCHWIERIGKEIT
///
/// Der Lauf soll mit n Spielern (2n - 1)-mal so schwer sein: zu zweit 3x, zu
/// dritt 5x, zu viert 7x - so war es gewuenscht. Jeder Spieler hat seinen
/// eigenen Wellenplan um sich herum (eigener <see cref="SpawnDirector"/>),
/// also gibt es schon n-mal so viele Gegner wie allein. Den Rest machen die
/// Lebenspunkte:
///
///   Normale Gegner und Elites   Leben x (2n - 1) / n   (2: 1,5  3: 1,67  4: 1,75)
///   Bosse und Zwischenbosse     Leben x (2n - 1)       - es gibt nur einen fuer alle
///
/// Obendrauf kommt wie allein die Schwierigkeit aus der Lobby (Chili-Regal).
///
/// ERFAHRUNG
///
/// Die XP sind gemeinsam: jedes Bonbon zaehlt fuer das Team, alle leveln
/// zusammen und waehlen gleichzeitig. Weil n Spieler n-mal so viele Gegner
/// erlegen, wird jedes Bonbon durch n geteilt - sonst levelt man zu viert
/// viermal so schnell wie allein. Ein kleiner Teambonus bleibt uebrig.
/// </summary>
public static class NetBalance
{
    /// <summary>Teambonus auf die XP je weiterem Spieler (0.1 = +10 %).</summary>
    public const float TeamXpBonus = 0.1f;

    /// <summary>So lange kann man niedergegangen auf Rettung warten.</summary>
    public const float BleedOutSeconds = 45f;

    /// <summary>So lange muss ein Mitspieler danebenstehen.</summary>
    public const float ReviveSeconds = 3f;

    /// <summary>So nah muss er dafuer sein.</summary>
    public const float ReviveRadius = 1.7f;

    /// <summary>Mit diesem Anteil der Max-HP steht man wieder auf.</summary>
    public const float ReviveHealth = 0.3f;

    /// <summary>Danach kurz unverwundbar.</summary>
    public const float ReviveImmunity = 2.5f;

    /// <summary>Tempo als Geist.</summary>
    public const float GhostSpeed = 0.55f;

    /// <summary>Nach dem Boss-Sieg (Story) so lange bis zum Sieg-Bildschirm - die Beute fliegt noch ein.</summary>
    public const float WinDelay = 3f;

    /// <summary>Gesamtschwierigkeit fuer n Spieler: 1, 3, 5, 7.</summary>
    public static float TeamFactor(int players) => Mathf.Max(1, 2 * players - 1);

    /// <summary>Lebens-Faktor fuer einen Gegner dieser Rolle.</summary>
    public static float HealthFactor(EnemyRole role, int players)
    {
        if (players <= 1) return 1f;
        bool boss = role == EnemyRole.Boss || role == EnemyRole.MiniBoss || role == EnemyRole.DeathBoss;
        return boss ? TeamFactor(players) : TeamFactor(players) / players;
    }

    /// <summary>Anteil eines Bonbons, der beim Team ankommt.</summary>
    public static float XpShare(int players)
    {
        if (players <= 1) return 1f;
        return (1f + TeamXpBonus * (players - 1)) / players;
    }
}
