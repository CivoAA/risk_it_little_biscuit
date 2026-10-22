using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fruchtgummi-Bande - kleine, durchscheinende Fruchtgummi-Baerchen hopsen vom
/// Spieler los, rennen zum naechsten Gegner, beissen sich fest und knabbern,
/// bis sie nach ein paar Bissen zerplatzen (<see cref="GummyBear"/>). Stirbt
/// ihr Gegner vorher, springen sie ab und suchen sich den naechsten. Faellt
/// einer weg, waechst nach der Abklingzeit ein neuer nach.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   4  Zuckerschock: platzende Baerchen treffen alles in der Naehe
///   6  Glitzerbaer: jedes vierte Baerchen glitzert (doppelter Biss, grosser Schock)
///
/// cooldown = so oft kommt ein neues Baerchen, solange welche fehlen
/// damage   = Schaden je Biss
/// range    = so weit sucht ein Baerchen nach Gegnern (Tiles)
/// duration = Bisse bis zum Platzen (Duration-Buff zaehlt)
/// shots    = Baerchen gleichzeitig (+ Extra-Schuss, nur ganze)
/// </summary>
public class GummyGang : Weapon
{
    public const float RunSpeed = 6.5f;
    public const float ShockFactor = 1.5f;
    public const float ShockRadius = 1.0f;
    public const float GoldFactor = 2f;
    public const float GoldShockRadius = 1.5f;
    public const int GoldEvery = 4;

    /// <summary>Nie mehr als so viele Baerchen am selben Gegner, solange andere frei sind.</summary>
    public const int BearsPerEnemy = 2;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Suche Bisse Baerchen
        Level(1.4f, 1.5f, 8f, 4f, 2, "Fruchtgummi-Bärchen rennen zu den Gegnern, beißen sich fest und platzen nach vier Bissen"),
        Level(1.3f, 2.5f, 8f, 4f, 2, "Biss +1"),
        Level(1.2f, 2.5f, 8f, 4f, 3, "Ein Bärchen mehr, beißen schneller"),
        Level(1.1f, 3.5f, 8f, 4f, 3, "Zuckerschock: platzende Bärchen treffen alles in der Nähe, Biss +1"),
        Level(1.0f, 3.5f, 9f, 5f, 4, "Ein Bärchen mehr, ein Biss mehr"),
        Level(0.9f, 4f, 9f, 5f, 5, "Glitzerbär: jedes vierte Bärchen glitzert, beißt doppelt und platzt groß, ein Bärchen mehr"),
    };

    private static readonly float[] BiteIntervalPerLevel = { 0.5f, 0.5f, 0.42f, 0.42f, 0.38f, 0.35f };
    private const int ShockFrom = 3;
    private const int GoldFrom = 5;

    private static WeaponStats Level(float cooldown, float damage, float range, float duration, int shots, string description)
    {
        return new WeaponStats
        {
            cooldown = cooldown,
            damage = damage,
            range = range,
            duration = duration,
            shots = shots,
            description = description,
        };
    }

    public static readonly string[] Flavors = { "red", "orange", "yellow", "green", "white" };
    public const string GoldFlavor = "gold";

    private float timer;
    private int spawned;
    private readonly List<GummyBear> bears = new List<GummyBear>();

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        bears.RemoveAll(b => b == null);
        if (!CanAttack || PlayerController.Instance == null) return;

        int max = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots)) + WholeExtraShots;
        if (bears.Count >= max)
        {
            // volle Bande: faellt einer weg, kommt der naechste nach einer halben Abklingzeit
            timer = Mathf.Max(timer, CurrentCooldown * 0.5f);
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (!AnyEnemyNear()) return;

        timer = CurrentCooldown;
        Spawn();
    }

    private bool AnyEnemyNear()
    {
        Vector2 p = transform.position;
        float maxSqr = CurrentStats.range * CurrentStats.range;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e != null && !e.Untouchable && ((Vector2)e.transform.position - p).sqrMagnitude <= maxSqr) return true;
        }
        return false;
    }

    public GummyBear.Bite CurrentBite(bool gold)
    {
        int lvl = Mathf.Clamp(TableLevel, 0, BiteIntervalPerLevel.Length - 1);
        float aoe = SweetFx.Aoe;
        float dmg = CurrentStats.damage * (gold ? GoldFactor : 1f);
        bool shock = lvl >= ShockFrom || gold;
        return new GummyBear.Bite
        {
            damage = dmg,
            interval = BiteIntervalPerLevel[lvl],
            bites = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.duration * SweetFx.DurationFactor)),
            seekRange = CurrentStats.range,
            shockDamage = shock ? dmg * ShockFactor : 0f,
            shockRadius = (gold ? GoldShockRadius : ShockRadius) * aoe,
        };
    }

    private void Spawn()
    {
        int lvl = Mathf.Clamp(TableLevel, 0, BiteIntervalPerLevel.Length - 1);
        spawned++;
        bool gold = lvl >= GoldFrom && spawned % GoldEvery == 0;
        string flavor = gold ? GoldFlavor : Flavors[Random.Range(0, Flavors.Length)];

        Vector2 at = (Vector2)transform.position + Random.insideUnitCircle * 0.3f;
        GummyBear bear = GummyBear.Spawn(at, flavor, CurrentBite(gold));
        bears.Add(bear);
        WeaponSounds.Play(WeaponSounds.GummySpawn, 1f, gold ? 0.85f : 1f);
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static readonly Dictionary<string, Color32> FlavorColors = new Dictionary<string, Color32>
    {
        { "red", new Color32(236, 59, 76, 255) },
        { "orange", new Color32(255, 138, 42, 255) },
        { "yellow", new Color32(255, 216, 58, 255) },
        { "green", new Color32(92, 201, 58, 255) },
        { "white", new Color32(223, 227, 240, 255) },
        { "gold", new Color32(255, 204, 58, 255) },
    };

    public static Color32 ColorOf(string flavor)
    {
        return FlavorColors.TryGetValue(flavor, out Color32 c) ? c : FlavorColors["red"];
    }

    public static Sprite[] Bear(string flavor) => SweetFx.Strip("gummy_" + flavor, ColorOf(flavor), 10);
    public static Sprite[] Pop(string flavor) => SweetFx.Strip("gummy_pop_" + flavor, ColorOf(flavor), 12);

    private static readonly int[] ShockRadii = { 20, 28, 36 };

    public static Sprite[] ShockFor(float radiusTiles)
    {
        return SweetFx.Sized("gummy_shock", ShockRadii, radiusTiles * SweetFx.PixelsPerUnit, new Color32(255, 228, 240, 255));
    }
}
