using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sticky Shatter Evo (Throwing Jam Jar + AOE Range).
///
/// Das Glas huepft vom Spieler weg: es schlaegt <see cref="bounces"/> Mal auf,
/// jeder Hopser etwas nach links oder rechts, aber immer weiter weg. Jeder
/// Aufschlag laesst eine kleine Marmeladenlache liegen, die Gegner bremst, und
/// verschiesst Glassplitter (<see cref="GlassShard"/>), die den ersten Gegner
/// treffen und dann weg sind.
///
/// Erbt Wurf, Flug und Zielwurf von <see cref="AreaWeaponJamJar"/> - Zielwurf
/// legt den ersten Aufschlag in die Gruppe, von dort huepft es weiter. Die
/// Lachen (<see cref="StickyShatterEvoPrefab"/>) erben Einkochen und Marmeladenbad.
///
/// cooldown    = Pause zwischen zwei Wuerfen
/// duration    = Lebensdauer einer Lache
/// damage      = Schaden pro Tick; ein Splitter macht <see cref="ShardDamageFactor"/> davon
/// range       = Groesse einer einzelnen Lache
/// AttackSpeed = Abstand zwischen zwei Ticks
/// shots       = Splitter je Aufschlag. Extra-Schuss zaehlt bewusst nicht.
/// </summary>
public class StickyShatterEvo : AreaWeaponJamJar
{
    /// <summary>Ein Splitter trifft so stark wie zwei Lachen-Ticks.</summary>
    public const float ShardDamageFactor = 2f;

    [Tooltip("So oft schlaegt das Glas auf - der letzte Aufschlag zerbricht es.")]
    [Min(1)]
    [SerializeField] private int bounces = 3;

    [Tooltip("Abstand zwischen zwei Aufschlaegen.")]
    [SerializeField] private float hopDistance = 2.5f;

    [Tooltip("So weit (Grad) weicht ein Hopser hoechstens von der Wurfrichtung ab, abwechselnd links und rechts.")]
    [Range(0f, 80f)]
    [SerializeField] private float hopAngle = 30f;

    [Tooltip("Tempo der Glassplitter.")]
    [SerializeField] private float shardSpeed = 9f;

    [Tooltip("So weit fliegt ein Splitter, bevor er verschwindet.")]
    [SerializeField] private float shardRange = 4f;

    [Tooltip("Tempo der Gegner in der Lache (0.5 = halbe Geschwindigkeit).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMultiplier = 0.5f;

    public float SlowMultiplier { get { return slowMultiplier; } }

    protected override void UnlockAchievements()
    {
        // Die Evo hat nur eine Stufe: aktiv sein heisst, sie wurde erhalten.
        Achievements.Unlock(Ach.StickyShatterEvo);
    }

    /// <summary>
    /// Erster Aufschlag am Ziel, danach Hopser in Wurfrichtung - abwechselnd
    /// leicht nach links und rechts gedreht, aber nie zurueck zum Spieler.
    /// </summary>
    protected override IReadOnlyList<Vector2> FlightPath(Vector2 target)
    {
        Vector2 dir = target - (Vector2)transform.position;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        var path = new Vector2[Mathf.Max(1, bounces)];
        path[0] = target;

        float side = Random.value < 0.5f ? 1f : -1f;
        for (int i = 1; i < path.Length; i++)
        {
            side = -side;
            float angle = side * Random.Range(0.4f, 1f) * hopAngle;
            Vector2 hop = Quaternion.Euler(0f, 0f, angle) * dir;
            path[i] = path[i - 1] + hop * hopDistance;
        }

        return path;
    }

    /// <summary>Jeder Aufschlag: eine kleine Lache und ein Kranz Glassplitter.</summary>
    protected override void Landed(Vector2 position)
    {
        SpawnPuddle(position);

        int shards = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots));
        float damage = CurrentStats.damage * ShardDamageFactor;
        SpriteRenderer sortLike = jarPrefab != null ? jarPrefab.GetComponent<SpriteRenderer>() : null;
        float offset = Random.Range(0f, 360f);

        for (int i = 0; i < shards; i++)
        {
            float angle = (offset + 360f / shards * i) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            MoveToRunScene(GlassShard.Fire(position, dir, shardSpeed, shardRange, damage, sortLike));
        }
    }
}
