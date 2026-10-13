using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Schleimstrudel - die Evo des Koenigsplumps (Schleimkoenig). Rezept:
/// Koenigsplumps + Vortex.
///
/// Statt eines Klopses, der von Gruppe zu Gruppe huepft, springt ein
/// Schleimklon des Koenigs (<see cref="SlimeClone"/>) mitten in die dichteste
/// Gruppe in Reichweite. Der Aufschlag macht Schaden (ohne Schubs), dann oeffnet
/// sich unter dem Klon ein Strudel, doppelt so gross wie der groesste des
/// Vortex, der alle Gegner in der Naehe zu sich zieht, bremst und im Takt
/// verletzt. Laeuft der Strudel aus, zerplatzt der Klon.
///
/// Die Sterne laufen mit: Klebgelee legt beim Aufschlag die Pfuetze, mit
/// Zerplatzen springen beim Ende drei Mini-Klopse vom Klon weg.
///
/// cooldown = Pause zwischen zwei Klonen
/// damage   = Schaden des Aufschlags; ein Strudel-Tick macht <see cref="TickFactor"/> davon
/// range    = Sprungweite in Tiles
/// duration = Strudel in Sekunden (Duration-Buff zaehlt)
/// shots    = Klone je Wurf (+ einer je 3 Extra-Schuesse)
///
/// Die Tabellen der Basiswaffe (Platscher-Groesse) stehen ueber
/// <see cref="Weapon.TableLevel"/> auf deren Hoechststufe.
/// </summary>
public class RoyalSplatEvo : RoyalSplat
{
    /// <summary>Strudel-Radius in Tiles vor dem AOE-Buff - doppelt so gross wie der Vortex auf Hoechststufe (Radius 1,5).</summary>
    public const float VortexRadius = 3f;

    /// <summary>Schaden eines Strudel-Ticks im Verhaeltnis zum Aufschlag.</summary>
    public const float TickFactor = 0.15f;

    /// <summary>Abstand zwischen zwei Strudel-Ticks (wie der Vortex auf Hoechststufe).</summary>
    public const float TickInterval = 0.4f;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 2.2f,
            damage = 26f,
            range = 8f,
            duration = 4f,
            shots = 1,
            description = "Ein Schleimklon springt in die dichteste Gruppe und öffnet einen riesigen Strudel",
        },
    };

    [Tooltip("Bilder des Strudels - dieselben wie beim Vortex (Art/Waffen/Fx/vortex).")]
    [SerializeField] private Sprite[] vortexFrames;

    protected override int TableLevel => int.MaxValue;

    private readonly List<Vector2> avoid = new List<Vector2>();

    protected override void Awake()
    {
        base.Awake();
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    /// <summary>Strudel-Radius mit AOE-Buff.</summary>
    private static float PullRadius()
    {
        PlayerController player = PlayerController.Instance;
        return VortexRadius * (player != null ? Mathf.Clamp(player.AOERange, 0.5f, 2.2f) : 1f);
    }

    protected override bool Throw()
    {
        float pullRadius = PullRadius();

        // Gesucht wird mit dem Strudel-Radius: der Klon landet dort, wo der
        // Strudel am meisten einsammelt, und nicht in einem, der schon steht.
        avoid.Clear();
        avoid.AddRange(claimed);
        SlimeClone.AddLiveCenters(avoid);

        Vector2 ground = transform.position;
        Vector2 spot;
        if (!FindCluster(ground, CurrentStats.range, pullRadius, null, avoid, 0f, out spot)) return false;

        claimed.Add(spot);

        PlayerController player = PlayerController.Instance;
        SlimeClone.Jump(ground, throwHeight, spot, CurrentHit(), new SlimeClone.Pull
        {
            radius = pullRadius,
            time = CurrentDuration,
            tickDamage = CurrentStats.damage * TickFactor,
            tickInterval = TickInterval,
        }, player != null ? player.BodyRenderer() : null, vortexFrames);
        WeaponSounds.Play(WeaponSounds.SplatThrow);
        return true;
    }
}
