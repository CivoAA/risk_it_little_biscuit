using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taifunfaecher - Evo aus Salatfaecher + Move Speed.
///
/// Zielt und schlaegt wie der <see cref="SaladFan"/> (erbt von ihm), aber mit
/// wenigen, schweren Wellen: Schaden x<see cref="DamageFactor"/> der hoechsten
/// Faecherstufe, groesser, schneller, durchschlagender. Dafuer zaehlen
/// Extra-Schuesse nur halb (<see cref="ExtraShotsPerWave"/>): ein Faecher mit
/// 8 Wellen wird zum Taifunfaecher mit 4.
///
/// Jede Hauptwelle zieht eine Windschneise (<see cref="TyphoonTrail"/>) hinter
/// sich her, die Gegner bremst. Steht Toast selbst darin, hat er Rueckenwind
/// (<see cref="TailwindBonus"/>) - das ist alles, was vom Move Speed in der
/// Evo noch uebrig ist. Der Tempo-Bonus des Buffs selbst bleibt beim Spieler.
///
/// Skilltree (Toast) laeuft ueber den Faecher mit:
///   Spaltwelle - wie beim Faecher; die Abspaltungen ziehen KEINE Windschneise.
///   Sturmboe   - jeder 5. Schlag als Kreis mit doppelt so vielen Wellen. Hier
///                gehen alle gleichzeitig, gleichmaessig verteilt, vom selben
///                Punkt aus - sonst saehe man an den Windschneisen, dass der
///                Kreis schief ist.
/// </summary>
public class TyphoonFan : SaladFan
{
    /// <summary>Schaden einer Welle relativ zur hoechsten Faecherstufe.</summary>
    public const float DamageFactor = 2.2f;

    /// <summary>So viele Extra-Schuesse ergeben eine Welle mehr.</summary>
    public const int ExtraShotsPerWave = 2;

    /// <summary>Groesse einer Welle in Tiles (Faecher: 1).</summary>
    private const float EvoWaveSize = 1.5f;

    /// <summary>Fluggeschwindigkeit in Tiles pro Sekunde (Faecher: 8).</summary>
    private const float EvoWaveSpeed = 10f;

    /// <summary>So viele Gegner trifft eine Welle (Faecher Stufe 6: 5).</summary>
    private const int EvoHits = 6;

    /// <summary>Anteil, den eine Welle pro Treffer behaelt (Faecher Stufe 6: 0,88).</summary>
    private const float EvoKeep = 0.9f;

    // ------------------------------------------------------- Windschneise

    /// <summary>So lange bleibt die Schneise, nachdem ihre Welle weg ist (Sekunden).</summary>
    public const float TrailLinger = 1.5f;

    /// <summary>Breite der Schneise relativ zur Wellengroesse.</summary>
    public const float TrailWidth = 0.7f;

    /// <summary>Tempo-Faktor der Gegner in der Schneise (0,7 = 30 % langsamer).</summary>
    public const float TrailSlow = 0.7f;

    /// <summary>Rueckenwind: so viel schneller laeuft Toast in einer Schneise (0,1 = +10 %).</summary>
    public const float TailwindBonus = 0.1f;

    /// <summary>Bis zu diesem Zeitpunkt (Time.time) hat der Spieler Rueckenwind.</summary>
    public static float TailwindUntil;

    /// <summary>Steht der Spieler gerade in einer Windschneise?</summary>
    public static bool HasTailwind
    {
        get { return Time.time < TailwindUntil; }
    }

    public override float CurrentKeep
    {
        get { return EvoKeep; }
    }

    public override int CurrentMaxHits
    {
        get { return EvoHits; }
    }

    protected override float CurrentWaveSize
    {
        get { return EvoWaveSize; }
    }

    protected override float CurrentWaveSpeed
    {
        get { return EvoWaveSpeed; }
    }

    protected override void Awake()
    {
        WeaponStats max = MaxLevelStats;
        stats = new List<WeaponStats>
        {
            new WeaponStats
            {
                cooldown = max.cooldown,
                damage = max.damage * DamageFactor,
                range = max.range + 0.5f,
                shots = 4,
                description = "Vier schwere Taifunwellen mit Windschneise",
            },
        };
        maxweaponLevel = 0;
    }

    void LateUpdate()
    {
        // Die Evo hat nur eine Stufe: aktiv sein heisst, sie wurde erhalten.
        if (IsActive) Achievements.Unlock(Ach.TyphoonFanEvo);
    }

    /// <summary>Grundwellen plus eine je <see cref="ExtraShotsPerWave"/> Extra-Schuesse.</summary>
    protected override int WaveCount()
    {
        int extra = Mathf.Max(0, PlayerController.Instance.ExtraShots) / ExtraShotsPerWave;
        return Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots) + extra);
    }

    /// <summary>
    /// Sturmboe: alle Wellen auf einmal, gleichmaessig verteilt und vom selben
    /// Punkt aus. Die Anzahl ist immer gerade (Wellen x2), der Kreis also auch
    /// spiegelgleich.
    /// </summary>
    protected override IEnumerator StormVolley(Vector2 startDir, int count, float range)
    {
        Vector2 origin = WaveOrigin;
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, -step * i) * startDir;
            SpawnWave(origin, dir, range);
        }

        yield break;
    }

    protected override void OnWaveLaunched(SaladFanWave wave, Vector2 origin, float size)
    {
        TyphoonTrail.Create(wave.transform, origin, size * TrailWidth);
    }

    private static Sprite typhoonWave;

    /// <summary>Dieselbe Sichel wie beim Faecher, nur blaeulich.</summary>
    protected override Sprite DefaultWaveSprite
    {
        get
        {
            if (typhoonWave == null)
            {
                typhoonWave = BuildWaveSprite(new Color32(235, 250, 255, 255), new Color32(120, 200, 235, 220));
            }
            return typhoonWave;
        }
    }
}
