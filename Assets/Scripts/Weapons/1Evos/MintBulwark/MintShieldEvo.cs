using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pfefferminz-Bollwerk - die Evo des Pfefferminz-Schilds. Rezept:
/// Pfefferminz-Schild + HP-Regeneration (Minze = frisch = heilt).
///
/// Die Blase kann alles, was die Basiswaffe auf Hoechststufe kann (zwei
/// Schichten, Ueberdruck, Frische Brise, Bonbons), waechst schneller nach und
/// heilt bei jedem Platzen. Alle <see cref="WheelInterval"/> Sekunden zieht sie
/// sich zu einem grossen Pfefferminz-Rad zusammen (<see cref="MintWheel"/>), das
/// von Gruppe zu Gruppe rollt, bei jedem Abprall klirrt und am Ende als
/// heilende Polarlicht-Nova zur Blase zurueckkehrt. Solange das Rad unterwegs
/// ist, gibt es keine Blase - das ist der Preis.
///
/// cooldown = so lange waechst eine Schicht nach
/// damage   = Schaden der Welle (Brise/Bonbons rechnen davon); Rad, Abprall und Nova sind feste Werte
/// range    = Radius der Welle in Tiles (mit AOE)
/// duration = Bremse nach Treffern (Duration-Buff zaehlt)
/// shots    = Bonbons je Welle. Extra-Schuss wirkt nicht (wie bei der Basiswaffe).
/// </summary>
public class MintShieldEvo : MintShield
{
    /// <summary>So oft rollt das Rad los (Cooldown-Buff zaehlt).</summary>
    public const float WheelInterval = 5f;

    public const int WheelBounces = 4;
    /// <summary>Schaden des Rads je ueberfahrenem Gegner (fest, nicht vom Wellenschaden abgeleitet).</summary>
    public const float WheelDamage = 10f;
    /// <summary>Feste Werte: Klirr-Stoss beim Abprall und Nova bei der Rueckkehr.</summary>
    public const float ClinkDamage = 11.2f;
    public const float NovaDamage = 12.8f;

    /// <summary>Heilung je geplatzter Schicht und beim Zurueckkehren des Rads.</summary>
    public const float PopHeal = 3f;
    public const float NovaHeal = 6f;

    /// <summary>Das Rad sucht Gegner in diesem Umkreis (Tiles).</summary>
    public const float WheelSearch = 7f;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 3f,
            damage = 12f,
            range = 3.2f,
            duration = 2f,
            shots = 10,
            description = "Ein Pfefferminz-Rad rollt durch die Gegner und kehrt als heilende Nova zurück",
        },
    };

    protected override int TableLevel => int.MaxValue;

    private float wheelTimer = 1.5f;
    private bool wheelOut;
    private static readonly List<Enemy> near = new List<Enemy>();

    protected override void Awake()
    {
        base.Awake();
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    protected override bool HideBubble => wheelOut;
    protected override Sprite[] IdleBubble => EvoBubbleFrames;

    protected override void Update()
    {
        base.Update();
        if (!IsActive || PlayerController.Instance == null)
        {
            wheelOut = false;
            return;
        }
        if (!IsCopy) Achievements.Unlock(Ach.MintShieldEvo);

        if (wheelOut || !CanAttack) return;
        wheelTimer -= Time.deltaTime;
        if (wheelTimer > 0f) return;

        SweetFx.EnemiesWithin(BubbleCenter, WheelSearch, near);
        bool any = near.Count > 0;
        near.Clear();
        if (!any)
        {
            wheelTimer = 0.4f;
            return;
        }

        float aoe = SweetFx.Aoe;
        MintWheel.Launch(this, new MintWheel.Roll
        {
            damage = WheelDamage,
            hitRadius = 0.55f * aoe,
            bounces = WheelBounces,
            clinkDamage = ClinkDamage,
            clinkRadius = 1.4f * aoe,
            slowTime = CurrentStats.duration * SweetFx.DurationFactor,
            search = WheelSearch,
        });
        wheelOut = true;
        float mult = PlayerController.Instance.CooldownMultiplier;
        wheelTimer = WheelInterval * mult;
    }

    protected override void OnPopped(Vector2 center)
    {
        PlayerController.Instance?.Heal(PopHeal);
    }

    /// <summary>Vom Rad, wenn es wieder beim Spieler ist: Nova, Heilung, alle Schichten wieder da.</summary>
    public void WheelReturned()
    {
        wheelOut = false;
        if (!IsActive || PlayerController.Instance == null) return;

        Vector2 c = BubbleCenter;
        float radius = CurrentStats.range * 1.3f * SweetFx.Aoe;
        SweetFx.Play(NovaFor(radius), c, 18f, 3);
        WeaponSounds.Play(WeaponSounds.MintNova);
        ScreenShake.Kick(2.5f, 0.3f);

        SweetFx.EnemiesWithin(c, radius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;
            e.TakeDamage(NovaDamage, WaveSlow, WaveKnockback);
            if (SweetFx.Valid(e)) e.ApplySlow(WaveSlow, CurrentStats.duration * SweetFx.DurationFactor);
        }
        near.Clear();

        PlayerController.Instance.Heal(NovaHeal);
        // Kommt als volle Blase zurueck
        while (Charges < MaxCharges) Grow();
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static Sprite[] EvoBubbleFrames => SweetFx.Strip("mint_bubble_evo", Mint, 44);
    public static Sprite[] WheelFrames => SweetFx.Strip("mint_wheel", new Color32(255, 90, 110, 255), 30);
    public static Sprite[] MorphFrames => SweetFx.Strip("mint_morph", Mint, 40);
    public static Sprite[] TrailFrames => SweetFx.Strip("mint_trail", Mint, 4);

    private static Sprite[] morphBack;

    /// <summary>Rad -> Blase (die Verwandlung rueckwaerts).</summary>
    public static Sprite[] MorphBackFrames
    {
        get
        {
            Sprite[] f = MorphFrames;
            if (morphBack == null || morphBack.Length != f.Length || morphBack[0] != f[f.Length - 1])
            {
                morphBack = (Sprite[])f.Clone();
                System.Array.Reverse(morphBack);
            }
            return morphBack;
        }
    }

    private static readonly int[] ClinkRadii = { 36, 44, 52 };
    private static readonly int[] NovaRadii = { 80, 104, 128 };

    public static Sprite[] ClinkFor(float radiusTiles)
    {
        return SweetFx.Sized("mint_clink", ClinkRadii, radiusTiles * SweetFx.PixelsPerUnit, Mint);
    }

    public static Sprite[] NovaFor(float radiusTiles)
    {
        return SweetFx.Sized("mint_nova", NovaRadii, radiusTiles * SweetFx.PixelsPerUnit, Mint);
    }
}
