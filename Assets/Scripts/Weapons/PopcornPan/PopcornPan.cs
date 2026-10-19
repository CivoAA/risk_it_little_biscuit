using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Popcorn-Pfanne - wirft Maiskoerner im Bogen zu den Gegnern. Die Koerner
/// liegen kurz am Boden, werden heiss und poppen: Schaden im Kreis. Ein Pop
/// zuendet liegende Koerner in der Naehe sofort mit (Kettenreaktion) - je
/// dichter die Gegner stehen, desto lauter knallt es.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   4  Nachhuepfen: das gepoppte Popcorn springt zum naechsten Gegner und poppt noch einmal
///   6  Popcorn-Gewitter: zweimal Nachhuepfen
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Schaden je Pop
/// range    = Wurfweite in Tiles
/// shots    = Koerner je Wurf (+ Extra-Schuss)
/// </summary>
public class PopcornPan : Weapon
{
    /// <summary>So lange liegt ein Korn, bevor es von selbst poppt (Sekunden, zufaellig dazwischen).</summary>
    public const float FuseMin = 0.8f;
    public const float FuseMax = 1.3f;

    /// <summary>Ein Pop zuendet Koerner bis zu so viel mal seinem Radius.</summary>
    public const float ChainReach = 1.35f;

    /// <summary>Nachhuepfen: Schaden und Radius des zweiten Pops.</summary>
    public const float HopDamage = 0.6f;
    public const float HopRadius = 0.8f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Weite Koerner
        Level(2.2f, 2f, 6.0f, 3, "Wirft Maiskörner zu den Gegnern, die kurz darauf poppen und sich gegenseitig anstecken"),
        Level(2.1f, 3f, 6.0f, 3, "Schaden +1"),
        Level(2.0f, 3f, 6.5f, 4, "Ein Korn mehr, größerer Pop"),
        Level(1.9f, 4f, 6.5f, 4, "Nachhüpfen: gepopptes Popcorn springt zum nächsten Gegner und poppt noch einmal, Schaden +1"),
        Level(1.8f, 5f, 7.0f, 5, "Ein Korn mehr, Schaden +1"),
        Level(1.6f, 6f, 7.0f, 6, "Popcorn-Gewitter: zweimal Nachhüpfen, ein Korn mehr, Schaden +1"),
    };

    /// <summary>Pop-Radius in Tiles vor dem AOE-Buff.</summary>
    private static readonly float[] RadiusPerLevel = { 0.85f, 0.85f, 1.0f, 1.0f, 1.05f, 1.1f };
    private static readonly int[] HopsPerLevel = { 0, 0, 0, 1, 1, 2 };

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

    /// <summary>Wurf relativ zum Spieler-Pivot.</summary>
    public static readonly Vector2 PanOffset = new Vector2(0f, 0.3f);

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

    public PopcornKernel.Pop CurrentPop()
    {
        int lvl = Mathf.Clamp(TableLevel, 0, RadiusPerLevel.Length - 1);
        return new PopcornKernel.Pop
        {
            damage = CurrentStats.damage,
            radius = RadiusPerLevel[lvl] * SweetFx.Aoe,
            hops = HopsPerLevel[lvl],
        };
    }

    private bool Fire()
    {
        Vector2 origin = (Vector2)transform.position + PanOffset;
        float range = CurrentStats.range;

        targets.Clear();
        float maxSqr = range * range;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            if (((Vector2)e.transform.position - origin).sqrMagnitude <= maxSqr) targets.Add(e);
        }
        if (targets.Count == 0) return false;
        targets.Sort((a, b) => ((Vector2)a.transform.position - origin).sqrMagnitude
            .CompareTo(((Vector2)b.transform.position - origin).sqrMagnitude));

        int count = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots)) + RollExtraShots();
        // aus den naechsten Gegnern zufaellig waehlen - sonst landet alles auf dem ersten
        int pool = Mathf.Min(targets.Count, count * 2);
        PopcornKernel.Pop pop = CurrentPop();
        for (int i = 0; i < count; i++)
        {
            Enemy e = targets[Random.Range(0, pool)];
            Vector2 land = (Vector2)e.transform.position + Random.insideUnitCircle * 0.45f;
            PopcornKernel.Throw(origin, land, pop, i * 0.06f);
        }

        SweetFx.Play(Spawn, origin, 14f, 2);
        WeaponSounds.Play(WeaponSounds.PopcornThrow);
        targets.Clear();
        return true;
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Butter = new Color32(255, 223, 138, 255);

    public static Sprite[] Kernel => SweetFx.Strip("popcorn_kernel", new Color32(247, 200, 67, 255), 6);
    public static Sprite[] Shadow => SweetFx.Strip("popcorn_shadow", new Color32(40, 24, 20, 90), 4);
    public static Sprite[] Piece => SweetFx.Strip("popcorn_piece", Butter, 10);
    public static Sprite[] Spawn => SweetFx.Strip("popcorn_spawn", Butter, 6);

    private static readonly int[] PopRadii = { 16, 22, 28, 36, 44 };

    public static Sprite[] PopFor(float radiusTiles)
    {
        return SweetFx.Sized("popcorn_pop", PopRadii, radiusTiles * SweetFx.PixelsPerUnit, Butter);
    }
}
