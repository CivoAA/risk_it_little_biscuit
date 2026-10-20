using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Karamell-Kino - die Evo der Popcorn-Pfanne. Rezept: Popcorn-Pfanne +
/// Krit-Chance.
///
/// Die Pfanne kann alles, was die Basiswaffe auf Hoechststufe kann (zweimal
/// Nachhuepfen). Dazu:
///
///   Karamell    Jedes Korn wird mit <see cref="CaramelChance"/> zu Karamell:
///               groesserer, staerkerer Pop, der klebt (Gegner kurz gebremst)
///               und weiter entfernte Koerner ansteckt. Die Chance waechst mit
///               der Krit-Chance des Spielers, ist aber gedeckelt.
///   Kino-Eimer  Alle <see cref="BucketInterval"/> Sekunden (Cooldown-Buff
///               zaehlt) faellt ein Popcorn-Eimer auf die dichteste Gruppe,
///               kippt um und verschuettet <see cref="BucketKernels"/> Koerner.
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Schaden je Pop (Karamell x <see cref="CaramelDamage"/>)
/// range    = Wurfweite in Tiles
/// shots    = Koerner je Wurf (+ Extra-Schuss; auch der Eimer bekommt sie)
/// </summary>
public class PopcornPanEvo : PopcornPan
{
    /// <summary>Karamell-Chance = Grundwert + Anteil der Krit-Chance, hoechstens <see cref="CaramelMax"/>.</summary>
    public const float CaramelBase = 0.15f;
    public const float CaramelPerCrit = 0.4f;
    public const float CaramelMax = 0.6f;

    public const float CaramelDamage = 1.5f;
    public const float CaramelRadius = 1.3f;
    public const float ChainReach = 1.7f;
    public const float StickySlow = 0.5f;
    public const float StickyTime = 1.2f;

    public const float BucketInterval = 9f;
    public const int BucketKernels = 10;
    public const float BucketSpill = 2.2f;
    public const float BucketImpact = 8f;
    public const float BucketImpactRadius = 1.2f;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 1.5f,
            damage = 6f,
            range = 7.5f,
            shots = 7,
            description = "Karamell-Popcorn klebt und knallt größer, ein Kino-Eimer kippt auf die Gegner",
        },
    };

    protected override int TableLevel => int.MaxValue;

    private float bucketTimer = 3f;
    private static readonly List<Enemy> near = new List<Enemy>();

    public static float CaramelChance
    {
        get
        {
            float crit = PlayerController.Instance != null ? PlayerController.Instance.critChance : 0f;
            return Mathf.Clamp(CaramelBase + CaramelPerCrit * Mathf.Max(0f, crit), CaramelBase, CaramelMax);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive || PlayerController.Instance == null) return;
        if (!IsCopy) Achievements.Unlock(Ach.PopcornPanEvo);
        if (!CanAttack) return;

        bucketTimer -= Time.deltaTime;
        if (bucketTimer > 0f) return;
        bucketTimer = DropBucket() ? BucketInterval * PlayerController.Instance.CooldownMultiplier : 0.5f;
    }

    protected override PopcornKernel.Pop KernelPop(PopcornKernel.Pop pop)
    {
        if (Random.value >= CaramelChance) return pop;
        pop.caramel = true;
        pop.damage *= CaramelDamage;
        pop.radius *= CaramelRadius;
        return pop;
    }

    /// <summary>Eimer auf den Gegner mit den meisten Nachbarn in Wurfweite.</summary>
    private bool DropBucket()
    {
        Vector2 player = PlayerController.Instance.transform.position;
        float maxSqr = CurrentStats.range * CurrentStats.range;
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        Enemy best = null;
        int bestCount = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            Vector2 p = e.transform.position;
            if ((p - player).sqrMagnitude > maxSqr) continue;
            SweetFx.EnemiesWithin(SweetFx.Body(e), BucketSpill, near);
            if (near.Count > bestCount)
            {
                bestCount = near.Count;
                best = e;
            }
        }
        near.Clear();
        if (best == null) return false;

        int kernels = BucketKernels + RollExtraShots();
        PopcornBucket.Drop(this, (Vector2)best.transform.position, kernels);
        return true;
    }

    /// <summary>Vom Eimer beim Umkippen: Koerner rundherum verschuetten.</summary>
    public void Spill(Vector2 at, int kernels)
    {
        if (!IsActive) return;
        float aoe = SweetFx.Aoe;

        SweetFx.EnemiesWithin(at + new Vector2(0f, 0.2f), BucketImpactRadius * aoe, near);
        for (int i = 0; i < near.Count; i++)
            if (near[i] != null) near[i].TakeDamage(BucketImpact, null, 1f);
        near.Clear();

        PopcornKernel.Pop pop = CurrentPop();
        for (int i = 0; i < kernels; i++)
        {
            float a = (i + Random.value * 0.6f) / kernels * Mathf.PI * 2f;
            float d = BucketSpill * aoe * Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(Random.value));
            Vector2 land = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * d;
            PopcornKernel.Throw(at + new Vector2(0f, 0.3f), land, KernelPop(pop), i * 0.03f);
        }
        WeaponSounds.Play(WeaponSounds.PopcornThrow, 1f, 0.8f);
        ScreenShake.Kick(1.5f, 0.2f);
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Caramel = new Color32(214, 140, 58, 255);

    public static Sprite[] Kernel => SweetFx.Strip("popcorn_caramel_kernel", Caramel, 6);
    public static Sprite[] Piece => SweetFx.Strip("popcorn_caramel_piece", Caramel, 10);
    public static Sprite[] BucketFrames => SweetFx.Strip("popcorn_bucket", new Color32(220, 40, 50, 255), 24);

    private static readonly int[] PopRadii = { 22, 28, 36, 44, 56 };

    public static Sprite[] PopFor(float radiusTiles)
    {
        return SweetFx.Sized("popcorn_caramel_pop", PopRadii, radiusTiles * SweetFx.PixelsPerUnit, Caramel);
    }
}
