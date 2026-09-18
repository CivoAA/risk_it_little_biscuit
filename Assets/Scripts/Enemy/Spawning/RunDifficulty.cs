using UnityEngine;

/// <summary>
/// Der Chaos-Regler: eine Zahl, aus der alles folgt, was einen Lauf schwerer
/// macht.
///
/// Chaos 1 = die normale Schwierigkeit. Darunter wird es leichter, darueber
/// kommen mehr und zaehere Gegner - und es gibt mehr dafuer. Die Belohnung
/// steigt mit, sonst dreht niemand freiwillig hoch.
///
/// Gespeist wird der Wert aus <see cref="GameSession.Chaos"/> (Auswahl im Hub)
/// plus der Rampe im Endless-Modus. Spaeter koennen Fluch-Items oder der Shop
/// einfach oben drauf addieren - die Formeln stehen alle hier und nirgends
/// sonst.
/// </summary>
public static class RunDifficulty
{
    /// <summary>Wie schnell Chaos im Endless-Modus pro Minute steigt.</summary>
    public const float EndlessRampPerMinute = 0.08f;

    private static float chaos = 1f;

    /// <summary>1 = normal. Wird beim Start eines Laufs gesetzt.</summary>
    public static float Chaos
    {
        get { return chaos; }
        set { chaos = Mathf.Max(0.25f, value); }
    }

    /// <summary>Mehr Gegner gleichzeitig auf dem Feld.</summary>
    public static float CountFactor => 1f + (chaos - 1f) * 0.8f;

    /// <summary>Zaeher. Waechst leicht ueberproportional, damit hohes Chaos beisst.</summary>
    public static float HealthFactor => Mathf.Pow(chaos, 1.4f);

    /// <summary>Mehr Schaden - vorsichtiger als Leben, sonst wird es unfair statt schwer.</summary>
    public static float DamageFactor => 1f + (chaos - 1f) * 0.5f;

    /// <summary>Wie oft aus einem normalen Gegner ein Elite-Gegner wird (0..1).</summary>
    public static float EliteChance => Mathf.Clamp01((chaos - 1f) * 0.15f);

    /// <summary>Erfahrung und Waehrung. Muss ueber 1 bleiben, sonst lohnt Chaos nicht.</summary>
    public static float RewardFactor => Mathf.Pow(chaos, 1.2f);

    /// <summary>
    /// Setzt den Regler auf den Stand, mit dem der Lauf startet. Ruft der
    /// <see cref="SpawnDirector"/> beim Betreten einer Map auf.
    /// </summary>
    public static void BeginRun()
    {
        Chaos = GameSession.Chaos;
        Debug.Log($"[RunDifficulty] Lauf startet mit Chaos {Chaos:0.00} " +
                  $"(Gegner x{CountFactor:0.00}, Leben x{HealthFactor:0.00}, Belohnung x{RewardFactor:0.00}).");
    }

    /// <summary>
    /// Endless: Chaos steigt mit der Laufzeit. Im Story-Modus passiert nichts -
    /// dort macht der Plan die Steigerung ueber den Druck.
    /// </summary>
    public static void UpdateEndlessRamp(float runTime)
    {
        if (!GameSession.IsEndless) return;

        Chaos = GameSession.Chaos + (runTime / 60f) * EndlessRampPerMinute;
    }

    /// <summary>Haengt die aktuellen Faktoren an einen frisch erzeugten Gegner.</summary>
    public static void Apply(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.ApplyRunScaling(HealthFactor, DamageFactor, RewardFactor);
    }
}
