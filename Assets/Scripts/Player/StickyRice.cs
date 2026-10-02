using UnityEngine;

/// <summary>
/// Skilltree "Klebreis" (<see cref="SkillGrants.Klebreis"/>): faellt das Leben
/// unter 35 %, kleben alle Gegner im Bild 8 Sekunden lang fest - sie laufen
/// nicht, drehen sich nicht um, und ihre Animation steht
/// (<see cref="Enemy.Freeze"/>). Bosse und Minibosse bleiben frei.
///
/// Rennt der Spieler weg, bleiben die festgeklebten Gegner zurueck, statt vom
/// <see cref="SpawnDirector"/> sofort wieder nach vorn geholt zu werden. Erst
/// nach dem Aufloesen darf er sie umsetzen - jeden mit eigenem Nachlauf, damit
/// nicht alle im selben Moment vor dem Spieler auftauchen.
///
/// Ausgeloest wird er von <see cref="PlayerController.TakeDamage"/>, der auch
/// die Abklingzeit haelt. Hier stehen nur die Werte und das Festkleben.
/// </summary>
public static class StickyRice
{
    /// <summary>Ab diesem Lebensanteil springt Klebreis an.</summary>
    public const float Threshold = 0.35f;

    /// <summary>So lange kleben die Gegner fest (Sekunden).</summary>
    public const float Duration = 8f;

    /// <summary>Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float Cooldown = 35f;

    /// <summary>
    /// Nach dem Aufloesen wartet jeder Gegner zufaellig zwischen diesen beiden
    /// Werten, bevor der Director ihn umsetzen darf (Sekunden).
    /// </summary>
    public const float RecycleDelayMin = 0.5f;
    public const float RecycleDelayMax = 5f;

    /// <summary>Klebt alle Gegner im Bild fest. Gibt zurueck, wie viele es waren.</summary>
    public static int Fire()
    {
        if (!ViewBounds.TryGetWorldRect(out Rect view)) return 0;

        var enemies = Enemy.Alive;
        int count = 0;

        // Rueckwaerts, falls ein Gegner dabei aus der Liste faellt.
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsBoss) continue;
            if (!view.Contains(e.transform.position)) continue;

            e.Freeze(Duration, Random.Range(RecycleDelayMin, RecycleDelayMax));
            count++;
        }

        return count;
    }
}
