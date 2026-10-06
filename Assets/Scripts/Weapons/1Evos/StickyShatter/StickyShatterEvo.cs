using UnityEngine;

/// <summary>
/// Sticky Shatter Evo (Throwing Jam Jar + AOE Range).
///
/// Statt einer einzelnen Pfuetze zerspringt das Glas in mehrere Scherben, die
/// jeweils eine eigene kleinere Marmeladenlache hinterlassen. Die Lachen
/// verlangsamen Gegner zusaetzlich - das macht aus der Wurfwaffe echte
/// Zonenkontrolle.
///
/// Erbt Wurf, Flug und Zielwurf von <see cref="AreaWeaponJamJar"/>; nur der
/// Aufprall ist anders. Die Lachen (<see cref="StickyShatterEvoPrefab"/>) erben
/// Einkochen und Marmeladenbad.
///
/// cooldown    = Pause zwischen zwei Wuerfen
/// duration    = Lebensdauer einer Lache
/// damage      = Schaden pro Tick
/// range       = Groesse einer einzelnen Lache
/// AttackSpeed = Abstand zwischen zwei Ticks
/// shots       = Anzahl Scherben. Extra-Schuss zaehlt bewusst nicht.
/// </summary>
public class StickyShatterEvo : AreaWeaponJamJar
{
    [Tooltip("Wie weit die Scherben vom Aufschlagpunkt wegfliegen.")]
    [SerializeField] private float shardSpread = 2.5f;

    [Tooltip("Tempo der Gegner in der Lache (0.5 = halbe Geschwindigkeit).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMultiplier = 0.5f;

    public float SlowMultiplier { get { return slowMultiplier; } }

    protected override void UnlockAchievements()
    {
        // Die Evo hat nur eine Stufe: aktiv sein heisst, sie wurde erhalten.
        Achievements.Unlock(Ach.StickyShatterEvo);
    }

    /// <summary>Erste Lache auf den Aufschlagpunkt, die restlichen im Kreis darum.</summary>
    protected override void Landed(Vector2 center)
    {
        int shards = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots));

        for (int i = 0; i < shards; i++)
        {
            Vector2 pos = center;
            if (i > 0)
            {
                float angle = 360f / (shards - 1) * (i - 1);
                pos += new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)) * shardSpread;
            }

            SpawnPuddle(pos);
        }
    }
}
