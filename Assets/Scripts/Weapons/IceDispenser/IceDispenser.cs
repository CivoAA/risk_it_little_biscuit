using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eiswuerfel-Spender - kleine Eiswuerfel mit Gesicht ploppen aus dem Spender
/// und rutschen ueber den Boden auf die naechsten Gegner zu. Sie gleiten durch
/// alles hindurch (jeder Treffer bremst sie etwas) und bleiben am Ende liegen,
/// bis sie schmelzen.
///
/// Jeder Treffer gibt einen Kaeltestapel (<see cref="IceChill"/>). Beim
/// dritten friert der Gegner in einen Eisblock. Trifft ein Wuerfel einen
/// gefrorenen Gegner, zerschmettert er ihn: viel Schaden, Splitter treffen die
/// Nachbarn und geben ihnen Kaelte. Bosse frieren nicht ein, bekommen beim
/// dritten Stapel aber einen Frostbiss.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   4  Karambolage: nach dem Zerschmettern prallt der Wuerfel zum naechsten Gegner
///   6  Eislawine: Zerschmettern wirft drei Splitter-Wuerfel (zweite Karambolage)
///
/// cooldown = Pause zwischen zwei Ladungen
/// damage   = Schaden je Treffer
/// range    = Rutschweite in Tiles
/// duration = so lange bleibt ein Gegner eingefroren (Duration-Buff zaehlt)
/// shots    = Wuerfel je Ladung (+ Extra-Schuss)
/// </summary>
public class IceDispenser : Weapon
{
    public const int ChillToFreeze = 3;

    /// <summary>Kaeltestapel verfallen nach so vielen Sekunden ohne Treffer.</summary>
    public const float ChillDecay = 3f;

    public const float ShatterFactor = 2.5f;
    public const float ShardFactor = 0.5f;
    public const float BossBiteFactor = 1.5f;

    /// <summary>Splitter-Radius in Tiles vor dem AOE-Buff.</summary>
    public const float ShardRadius = 1.1f;

    /// <summary>Trefferradius des Wuerfels (Tiles, mit AOE).</summary>
    public const float CubeRadius = 0.36f;

    /// <summary>Starttempo in Tiles/s - abgebremst wird so, dass er nach range liegen bleibt.</summary>
    public const float SlideSpeed = 10f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Weite Wuerfel Einfrieren
        Level(1.9f, 1f, 5.5f, 2, 1.2f, "Eiswürfel rutschen durch Gegner, drei Treffer frieren sie ein"),
        Level(1.8f, 2f, 5.5f, 2, 1.2f, "Schaden +1"),
        Level(1.8f, 3f, 6.0f, 3, 1.2f, "Ein Eiswürfel mehr, Schaden +1"),
        Level(1.7f, 4f, 6.0f, 3, 1.3f, "Karambolage: Würfel prallen nach dem Zerschmettern weiter, Schaden +1"),
        Level(1.6f, 5f, 6.5f, 4, 1.7f, "Ein Eiswürfel mehr, friert länger ein, Schaden +1"),
        Level(1.5f, 6f, 6.5f, 4, 1.7f, "Eislawine: Zerschmettern wirft Splitter-Würfel, Schaden +1"),
    };

    private static readonly int[] RicochetsPerLevel = { 0, 0, 0, 1, 1, 2 };
    private const int AvalancheFrom = 5;
    public const int AvalancheCubes = 3;

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

    /// <summary>Ausgabe relativ zum Spieler-Pivot.</summary>
    public static readonly Vector2 SpoutOffset = new Vector2(0f, 0.3f);

    private float timer;
    private static readonly List<Enemy> targets = new List<Enemy>();

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (!CanAttack || PlayerController.Instance == null) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = Fire() ? CurrentCooldown : 0.25f;
    }

    public IceCube.Slide CurrentSlide()
    {
        int lvl = Mathf.Clamp(TableLevel, 0, RicochetsPerLevel.Length - 1);
        float aoe = SweetFx.Aoe;
        return new IceCube.Slide
        {
            damage = CurrentStats.damage,
            distance = CurrentStats.range,
            hitRadius = CubeRadius * aoe,
            shardRadius = ShardRadius * aoe,
            freezeTime = CurrentStats.duration * SweetFx.DurationFactor,
            ricochets = RicochetsPerLevel[lvl],
            avalanche = lvl >= AvalancheFrom,
        };
    }

    private bool Fire()
    {
        Vector2 origin = (Vector2)transform.position + SpoutOffset;
        IceCube.Slide slide = CurrentSlide();

        // Naechste Gegner in Reichweite, jeder Wuerfel nimmt einen anderen
        targets.Clear();
        float maxSqr = (slide.distance + 1f) * (slide.distance + 1f);
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
                // Mehr Wuerfel als Ziele: Faecher um den ersten
                float spread = ((i - targets.Count) % 2 == 0 ? 1f : -1f) * (14f + 10f * ((i - targets.Count) / 2));
                dir = Quaternion.Euler(0f, 0f, spread) * first;
            }
            IceCube.Launch(origin + dir * 0.2f, dir, slide, false);
        }

        SweetFx.Play(Spawn, origin, 16f, 2);
        WeaponSounds.Play(WeaponSounds.IceLaunch);
        targets.Clear();
        return true;
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Ice = new Color32(181, 230, 251, 255);

    public static Sprite[] Cube => SweetFx.Strip("ice_cube", Ice, 12);
    public static Sprite[] Mini => SweetFx.Strip("ice_mini", Ice, 7);
    public static Sprite[] Trail => SweetFx.Strip("ice_trail", Ice, 3);
    public static Sprite[] Mark => SweetFx.Strip("ice_mark", Ice, 5);
    public static Sprite[] Spawn => SweetFx.Strip("ice_spawn", Ice, 6);

    private static readonly int[] BlockWidths = { 20, 28, 36, 48, 64 };
    private static readonly int[] ShatterRadii = { 20, 28, 36 };

    public static Sprite[] BlockFor(float widthTiles)
    {
        return SweetFx.Sized("ice_block", BlockWidths, widthTiles * SweetFx.PixelsPerUnit, new Color32(181, 230, 251, 120));
    }

    public static Sprite[] ShatterFor(float radiusTiles)
    {
        return SweetFx.Sized("ice_shatter", ShatterRadii, radiusTiles * SweetFx.PixelsPerUnit, Ice);
    }
}
