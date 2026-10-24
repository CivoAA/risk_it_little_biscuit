using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Schneeballeffekt - die exklusive Startwaffe des Yetis (Charakter 9). Der
/// Yeti rollt Schneebaelle mit Streuseln auf die naechsten Gegner. Ein Ball
/// rollt durch bis zu <see cref="HitsPerLevel"/> Gegner, trifft jeden einmal
/// und waechst mit jedem Treffer eine Groessenstufe (<see cref="MaxSize"/>):
/// mehr Schaden je Treffer, groesserer Platzer. Am Ende seiner Strecke, nach
/// dem letzten Treffer oder an einem Boss zerstiebt er in Pulverschnee
/// (<see cref="Snowball"/>).
///
/// Balancing 24.10.2026 (Nick: "zu stark, vor allem wenn man die Gegner
/// reiht"): weniger Grundschaden, +15 % statt +25 % je Groesse, und der Ball
/// zerstiebt nach wenigen Treffern - eine lange Reihe zahlt sich nicht mehr
/// endlos aus. Der zweite Ball kommt erst auf Stufe 4.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   3  Frostfeld: das Zerstieben laesst einen Frostfleck liegen, der bremst
///   4  zweiter Ball
///   5  Baelle starten eine Groesse groesser
///   6  Schneesplitter: das Zerstieben wirft drei kleine Schneebaelle
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Schaden je Treffer in Startgroesse (+<see cref="GrowDamage"/> je Stufe)
/// range    = Rollweite in Tiles
/// duration = Frostfleck in Sekunden (Duration-Buff zaehlt)
/// shots    = Baelle je Wurf (+ eins je 2 Extra-Schuesse)
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab.
/// </summary>
public class SnowballEffect : Weapon
{
    /// <summary>Hoechste Groessenstufe (Bilder snowball_12 ... snowball_32).</summary>
    public const int MaxSize = 4;

    /// <summary>Schaden je Treffer waechst um so viel je Groessenstufe.</summary>
    public const float GrowDamage = 0.15f;

    /// <summary>Platzer: damage * (BurstBase + BurstPerSize * Groesse).</summary>
    public const float BurstBase = 1f;
    public const float BurstPerSize = 0.3f;

    /// <summary>Platzer-Radius in Tiles vor AOE: Basis + je Groessenstufe.</summary>
    public const float BurstRadius = 0.9f;
    public const float BurstRadiusPerSize = 0.22f;

    /// <summary>So stark bremst der Platzer kurz alle Getroffenen.</summary>
    public const float BurstSlow = 0.6f;

    /// <summary>Frostfleck: Bremse fuer alle, die darin stehen.</summary>
    public const float FrostSlow = 0.5f;

    /// <summary>Rolltempo in Tiles/s - grosse Baelle werden etwas traeger.</summary>
    public const float RollSpeed = 5.5f;

    /// <summary>Schneesplitter: so viele Mini-Baelle, mit diesem Anteil an Schaden und Strecke.</summary>
    public const int AvalancheBalls = 3;
    public const float AvalancheDamage = 0.5f;
    public const float AvalancheDistance = 0.45f;

    protected override int ExtraShotsPerShot => 2;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Weite Baelle Frost
        Level(2.4f, 2.0f, 5.5f, 1, 1.5f, "Rollt einen Schneeball durch drei Gegner, er wächst mit jedem Treffer"),
        Level(2.3f, 2.5f, 5.5f, 1, 1.5f, "Rollt durch einen Gegner mehr, Schaden +0,5"),
        Level(2.2f, 3.0f, 6.0f, 1, 1.5f, "Frostfeld: Zerstiebende Bälle lassen bremsenden Frost liegen, Schaden +0,5"),
        Level(2.1f, 3.5f, 6.0f, 2, 2.0f, "Ein zweiter Schneeball, Schaden +0,5"),
        Level(2.0f, 4.5f, 6.5f, 2, 2.0f, "Die Bälle starten eine Nummer größer und rollen durch einen Gegner mehr, Schaden +1"),
        Level(1.8f, 5.5f, 7.0f, 2, 2.5f, "Schneesplitter: Jeder Ball zerstiebt in drei kleine, Schaden +1"),
    };

    /// <summary>So viele Gegner trifft ein Ball, dann zerstiebt er.</summary>
    private static readonly int[] HitsPerLevel = { 3, 4, 4, 4, 5, 6 };
    private static readonly int[] StartSizePerLevel = { 0, 0, 0, 0, 1, 1 };
    private const int FrostFrom = 2;
    private const int AvalancheFrom = 5;

    private static WeaponStats Level(float cooldown, float damage, float range, int shots, float duration, string description)
    {
        return new WeaponStats
        {
            cooldown = cooldown,
            damage = damage,
            range = range,
            shots = shots,
            duration = duration,
            description = description,
        };
    }

    /// <summary>Abwurf relativ zum Spieler-Pivot (der liegt unter den Fuessen).</summary>
    public static readonly Vector2 ThrowOffset = new Vector2(0f, 0.25f);

    private float timer;
    private static readonly List<Enemy> targets = new List<Enemy>();

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    protected virtual void Update()
    {
        if (!CanAttack || PlayerController.Instance == null) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = Fire() ? CurrentCooldown : 0.25f;
    }

    /// <summary>Alles, was ein Ball beim Abwurf mitnimmt (Kopie - die Waffe darf danach weg sein).</summary>
    public virtual Snowball.Roll CurrentRoll()
    {
        int lvl = Mathf.Clamp(TableLevel, 0, StartSizePerLevel.Length - 1);
        return new Snowball.Roll
        {
            damage = CurrentStats.damage,
            distance = CurrentStats.range,
            startSize = StartSizePerLevel[lvl],
            maxHits = HitsPerLevel[lvl],
            aoe = SweetFx.Aoe,
            frostTime = lvl >= FrostFrom ? CurrentStats.duration * SweetFx.DurationFactor : 0f,
            avalanche = lvl >= AvalancheFrom,
        };
    }

    private bool Fire()
    {
        Vector2 origin = (Vector2)transform.position + ThrowOffset;
        Snowball.Roll roll = CurrentRoll();

        targets.Clear();
        float maxSqr = (roll.distance + 1f) * (roll.distance + 1f);
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            if ((SweetFx.Body(e) - origin).sqrMagnitude <= maxSqr) targets.Add(e);
        }
        if (targets.Count == 0) return false;
        targets.Sort((a, b) => (SweetFx.Body(a) - origin).sqrMagnitude.CompareTo((SweetFx.Body(b) - origin).sqrMagnitude));

        int count = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots)) + RollExtraShots();
        Vector2 first = (SweetFx.Body(targets[0]) - origin).normalized;
        for (int i = 0; i < count; i++)
        {
            Vector2 dir;
            if (i < targets.Count)
            {
                dir = SweetFx.Body(targets[i]) - origin;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : first;
            }
            else
            {
                // Mehr Baelle als Ziele: Faecher um den ersten
                float spread = ((i - targets.Count) % 2 == 0 ? 1f : -1f) * (16f + 12f * ((i - targets.Count) / 2));
                dir = Quaternion.Euler(0f, 0f, spread) * first;
            }
            Snowball.Launch(origin + dir * 0.2f, dir, roll);
        }

        SweetFx.Play(Trail, origin, 12f, 2);
        WeaponSounds.Play(WeaponSounds.SnowThrow);
        targets.Clear();
        return true;
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Snow = new Color32(238, 244, 255, 255);

    private static readonly int[] BallSizes = { 12, 16, 20, 26, 32 };
    private static readonly int[] BurstRadii = { 20, 28, 36, 48 };
    private static readonly int[] FrostWidths = { 32, 44, 56 };

    /// <summary>Durchmesser des Balls in Pixeln fuer eine Groessenstufe.</summary>
    public static int BallPixels(int size) => BallSizes[Mathf.Clamp(size, 0, BallSizes.Length - 1)];

    public static Sprite[] BallFor(int size) => SweetFx.Strip("snowball_" + BallPixels(size), Snow, BallPixels(size));

    public static Sprite[] Trail => SweetFx.Strip("snowball_trail", Snow, 5);

    public static Sprite[] BurstFor(float radiusTiles) =>
        SweetFx.Sized("snowball_burst", BurstRadii, radiusTiles * SweetFx.PixelsPerUnit, Snow);

    // Yeti-Lawine (Evo): Riesenball, Schneeklumpen um Gefangene, Lawinen-Explosion
    private static readonly int[] BigSizes = { 40, 48, 56, 64 };
    private static readonly int[] AvalancheRadii = { 56, 72, 88 };

    public static int BigPixels(int index) => BigSizes[Mathf.Clamp(index, 0, BigSizes.Length - 1)];

    public static Sprite[] BigFor(int index) => SweetFx.Strip("snowball_big_" + BigPixels(index), Snow, BigPixels(index));

    public static Sprite[] Clump => SweetFx.Strip("snow_clump", Snow, 12);

    public static Sprite[] AvalancheFor(float radiusTiles) =>
        SweetFx.Sized("avalanche_burst", AvalancheRadii, radiusTiles * SweetFx.PixelsPerUnit, Snow);

    public static Sprite[] FrostFor(float radiusTiles) =>
        SweetFx.Sized("snowball_frost", FrostWidths, radiusTiles * 2f * SweetFx.PixelsPerUnit, new Color32(210, 225, 250, 120));
}
