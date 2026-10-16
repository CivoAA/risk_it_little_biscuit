using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Candy-Frisbee - wirft eine flache Pfefferminzscheibe auf den naechsten
/// Gegner. Von dort springt sie von Gegner zu Gegner (<see cref="FrisbeeDisc"/>)
/// und segelt danach zum Spieler zurueck. Jede Stufe fliegt sie einen Gegner
/// weiter (<see cref="TargetsPerLevel"/>). Das Pfefferminz-Rad des
/// Pfefferminz-Bollwerks ist derselbe Gedanke im Grossen - darum ist der
/// Frisbee die zweite Zutat der Evo.
///
/// cooldown = Pause zwischen zwei Wuerfen (ab dem Wurf, nicht ab der Rueckkehr)
/// damage   = Schaden je getroffenem Gegner
/// range    = so weit sucht der Wurf sein erstes Ziel, in Tiles
/// shots    = Scheiben pro Wurf (+ eine je 3 Extra-Schuesse)
///
/// Skaliert mit Extra-Schuss (1:3), AOE (Trefferkreis und Sprungweite) und
/// Cooldown. Wirkdauer wirkt nicht.
/// </summary>
public class CandyFrisbee : Weapon
{
    /// <summary>Drei Extra-Schuesse fuer eine Scheibe mehr - jede Scheibe trifft mehrere Gegner.</summary>
    protected override int ExtraShotsPerShot => 3;

    /// <summary>So viele verschiedene Gegner fliegt eine Scheibe je Stufe an.</summary>
    private static readonly int[] TargetsPerLevel = { 2, 3, 4, 5, 6, 7 };

    /// <summary>So weit (Tiles, vor AOE) sucht die Scheibe ihr naechstes Ziel.</summary>
    public const float JumpRange = 4.5f;

    /// <summary>Trefferkreis der Scheibe in Tiles, vor AOE.</summary>
    public const float HitRadius = 0.4f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Scheiben
        Level(2.6f,  5f, 6f, 1, "Wirft eine Bonbonscheibe, die zu zwei Gegnern springt"),
        Level(2.5f,  6f, 6f, 1, "Springt zu einem Gegner mehr, Schaden +1"),
        Level(2.4f,  7f, 6f, 1, "Springt zu einem Gegner mehr, Schaden +1"),
        Level(2.3f,  8f, 6f, 1, "Springt zu einem Gegner mehr, Schaden +1"),
        Level(2.2f, 10f, 6f, 1, "Springt zu einem Gegner mehr, Schaden +2"),
        Level(2.0f, 12f, 6f, 1, "Springt zu einem Gegner mehr, Schaden +2"),
    };

    private static WeaponStats Level(float cooldown, float damage, float range, int shots, string description)
    {
        return new WeaponStats
        {
            cooldown = cooldown,
            damage = damage,
            range = range,
            shots = shots,
            description = description,
        };
    }

    /// <summary>Ausgangspunkt der Scheibe relativ zum Spieler-Pivot (der liegt unter den Fuessen).</summary>
    public static readonly Vector2 Origin = new Vector2(0f, 0.4f);

    private readonly List<Enemy> targets = new List<Enemy>();
    private float throwTimer;

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    /// <summary>Wie viele Gegner eine Scheibe auf der aktuellen Stufe anfliegt.</summary>
    public int Targets => TargetsPerLevel[Mathf.Clamp(TableLevel, 0, TargetsPerLevel.Length - 1)];

    void Update()
    {
        if (!CanAttack || PlayerController.Instance == null) return;

        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f) return;

        // Ohne Gegner in Reichweite bleibt die Scheibe in der Hand.
        if (Throw()) throwTimer = CurrentCooldown;
    }

    private bool Throw()
    {
        Vector2 origin = (Vector2)transform.position + Origin;
        SweetFx.EnemiesWithin(origin, CurrentStats.range, targets);
        if (targets.Count == 0) return false;

        targets.Sort((a, b) =>
            (SweetFx.Body(a) - origin).sqrMagnitude.CompareTo((SweetFx.Body(b) - origin).sqrMagnitude));

        int discs = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots) + RollExtraShots());
        float aoe = SweetFx.Aoe;

        WeaponSounds.Play(WeaponSounds.FrisbeeThrow);
        for (int i = 0; i < discs; i++)
        {
            // Mehr Scheiben als Gegner: die uebrigen nehmen wieder vorne die Liste
            Enemy first = targets[i % targets.Count];
            FrisbeeDisc.Launch(this, origin, first, new FrisbeeDisc.Flight
            {
                damage = CurrentStats.damage,
                targets = Targets,
                hitRadius = HitRadius * aoe,
                jumpRange = JumpRange * aoe,
            });
        }
        targets.Clear();
        return true;
    }

    /// <summary>Wo die Scheiben zurueckgefangen werden.</summary>
    public Vector2 CatchPoint => (Vector2)transform.position + Origin;

    public static Sprite[] DiscFrames => SweetFx.Strip("candy_frisbee", new Color32(232, 57, 79, 255), 14);
}
