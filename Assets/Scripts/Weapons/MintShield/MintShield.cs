using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pfefferminz-Schild - eine Minzblase mit rot-weissen Pfefferminzstreifen
/// legt sich um die Figur und faengt den naechsten Treffer komplett ab
/// (<see cref="PlayerController.TakeDamage"/> fragt <see cref="TryBlockAny"/>).
/// Dabei platzt sie in einer eisigen Druckwelle: Schaden, kraeftiger Schubs,
/// Gegner frieren fast fest (Slow). Nach <c>cooldown</c> Sekunden waechst eine
/// neue Blase.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   3  Ueberdruck: draengen sich genug Gegner an die volle Blase, platzt sie von selbst
///   4  Frische Brise: solange eine Blase steht, pustet sie regelmaessig kuehle Stoesse
///   5  zweite Schicht (zwei Ladungen), groessere Welle
///   6  Minzsturm: jede Welle schleudert Pfefferminzbonbons nach aussen
///
/// cooldown = so lange waechst eine neue Blase (Cooldown-Buff zaehlt)
/// damage   = Schaden der Welle; Brise und Bonbons machen einen Teil davon
/// range    = Radius der Welle in Tiles (mit AOE)
/// duration = so lange bleiben getroffene Gegner gebremst (Duration-Buff zaehlt)
/// shots    = Bonbons je Welle (ab Stufe 6). Extra-Schuss wirkt bewusst nicht - waere zu stark.
/// </summary>
public class MintShield : Weapon
{
    public const float WaveSlow = 0.45f;
    public const float WaveKnockback = 3f;

    /// <summary>So lange ist der Spieler nach einem abgefangenen Treffer unverwundbar.</summary>
    public const float BlockImmunity = 0.8f;

    /// <summary>Ueberdruck: so viele Gegner so nah an der Blase lassen sie platzen.</summary>
    public const int OverpressureCount = 5;
    public const float OverpressureRadius = 1.35f;

    /// <summary>Mindestabstand zweier Ueberdruck-Platzer - sonst gehen im Gedraenge alle Schichten auf einmal.</summary>
    public const float OverpressureGap = 1.5f;

    public const float BreezeInterval = 2.2f;
    public const float BreezeRadius = 1.6f;
    public const float BreezeFactor = 0.3f;

    public const float CandyFactor = 0.45f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Radius Bonbons Bremse
        Level(9f,   1f,  2.2f, 0, 1.5f, "Eine Minzblase fängt einen Treffer ab und platzt in einer eisigen Welle"),
        Level(8f,   2f,  2.2f, 0, 1.5f, "Schaden +1, wächst schneller nach"),
        Level(8f,   4f,  2.4f, 0, 1.6f, "Überdruck: platzt von selbst, wenn sich Gegner drängen, Schaden +2"),
        Level(7.5f, 6f,  2.4f, 0, 1.6f, "Frische Brise: kühle Stöße, solange die Blase steht, Schaden +2"),
        Level(7f,   8f,  2.7f, 0, 1.8f, "Zweite Schicht, größere Welle, Schaden +2"),
        Level(6f,   10f, 2.8f, 8, 1.8f, "Minzsturm: die Welle schleudert Pfefferminzbonbons, Schaden +2"),
    };

    protected override int ExtraShotsPerShot => 0;

    private static readonly int[] ChargesPerLevel = { 1, 1, 1, 1, 2, 2 };
    private const int OverpressureFrom = 2;
    private const int BreezeFrom = 3;

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

    /// <summary>Mitte der Blase relativ zum Spieler-Pivot (der liegt unter den Fuessen).</summary>
    public static readonly Vector2 Center = new Vector2(0f, 0.45f);

    private static readonly List<MintShield> shields = new List<MintShield>();

    public int Charges { get; private set; }

    protected float recharge;
    private float breezeTimer = BreezeInterval;
    private float pressureTimer;
    private float lastPressurePop = -10f;
    private bool wasActive;
    private float formSince = -10f;
    private SpriteRenderer bubble;
    private SpriteRenderer outer;

    private static readonly List<Enemy> near = new List<Enemy>();

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void OnEnable() => shields.Add(this);
    void OnDisable() => shields.Remove(this);

    private int Lvl => Mathf.Clamp(TableLevel, 0, ChargesPerLevel.Length - 1);
    protected int MaxCharges => ChargesPerLevel[Lvl];
    public Vector2 BubbleCenter => (Vector2)transform.position + Center;

    /// <summary>Von <see cref="PlayerController.TakeDamage"/>: faengt eine Blase den Treffer?</summary>
    public static bool TryBlockAny()
    {
        for (int i = 0; i < shields.Count; i++)
        {
            MintShield s = shields[i];
            if (s != null && s.IsActive && s.Charges > 0 && !s.HideBubble)
            {
                WeaponSounds.Play(WeaponSounds.MintBlock);
                s.Pop();
                return true;
            }
        }
        return false;
    }

    protected virtual void Update()
    {
        if (!IsActive || PlayerController.Instance == null)
        {
            if (wasActive)
            {
                Charges = 0;
                wasActive = false;
            }
            DrawBubble();
            return;
        }

        // Neu bekommen: gleich mit einer Blase starten
        if (!wasActive)
        {
            wasActive = true;
            Grow();
        }

        if (Charges < MaxCharges && !Resting)
        {
            recharge += Time.deltaTime;
            if (recharge >= CurrentCooldown)
            {
                recharge = 0f;
                Grow();
            }
        }
        else
        {
            recharge = 0f;
        }

        if (Charges > 0 && CanAttack && !HideBubble)
        {
            if (Lvl >= OverpressureFrom) CheckOverpressure();
            if (Lvl >= BreezeFrom) UpdateBreeze();
        }

        DrawBubble();
    }

    /// <summary>Eine Schicht mehr (bis zum Maximum), mit Aufwachs-Bildern.</summary>
    public void Grow()
    {
        Charges = Mathf.Min(MaxCharges, Charges + 1);
        formSince = Time.time;
        WeaponSounds.Play(WeaponSounds.MintForm);
    }

    private void CheckOverpressure()
    {
        pressureTimer -= Time.deltaTime;
        if (pressureTimer > 0f) return;
        pressureTimer = 0.25f;

        if (Charges < MaxCharges || Time.time - lastPressurePop < OverpressureGap) return;
        SweetFx.EnemiesWithin(BubbleCenter, OverpressureRadius * SweetFx.Aoe, near);
        int count = near.Count;
        near.Clear();
        if (count >= OverpressureCount)
        {
            lastPressurePop = Time.time;
            Pop();
        }
    }

    private void UpdateBreeze()
    {
        breezeTimer -= Time.deltaTime;
        if (breezeTimer > 0f) return;
        breezeTimer = BreezeInterval;

        float radius = BreezeRadius * SweetFx.Aoe;
        Vector2 c = BubbleCenter;
        SweetFx.EnemiesWithin(c, radius, near);
        if (near.Count == 0)
        {
            breezeTimer = 0.4f;
            return;
        }
        SweetFx.Play(PulseFor(radius), c, 16f, 1);
        WeaponSounds.Play(WeaponSounds.MintPulse);
        for (int i = 0; i < near.Count; i++)
            if (near[i] != null) near[i].TakeDamage(CurrentStats.damage * BreezeFactor, 0.7f, 0.8f);
        near.Clear();
    }

    /// <summary>Nach jeder geplatzten Schicht (die Evo heilt hier).</summary>
    protected virtual void OnPopped(Vector2 center) { }

    /// <summary>Eine Schicht platzt: Welle, ggf. Bonbons.</summary>
    protected void Pop()
    {
        if (Charges <= 0) return;
        Charges--;
        recharge = 0f;

        Vector2 c = BubbleCenter;
        float radius = CurrentStats.range * SweetFx.Aoe;

        SweetFx.Play(PopFrames, c, 18f, 3);
        SweetFx.Play(BurstFor(radius), c, 16f, 2);
        WeaponSounds.Play(WeaponSounds.MintBurst);
        ScreenShake.Kick(1.5f, 0.15f);

        float slowTime = CurrentStats.duration * SweetFx.DurationFactor;
        SweetFx.EnemiesWithin(c, radius, near);
        for (int i = 0; i < near.Count; i++)
        {
            Enemy e = near[i];
            if (e == null) continue;
            e.TakeDamage(CurrentStats.damage, WaveSlow, WaveKnockback);
            if (SweetFx.Valid(e)) e.ApplySlow(WaveSlow, slowTime);
        }
        near.Clear();

        int candies = Mathf.RoundToInt(CurrentStats.shots);
        if (candies > 0)
        {
            float start = Random.value * 360f;
            for (int k = 0; k < candies; k++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, start + k * 360f / candies) * Vector2.right;
                MintCandy.Launch(c + d * 0.6f, d, CurrentStats.damage * CandyFactor, radius * 2.2f);
            }
        }
        OnPopped(c);
    }

    // ------------------------------------------------------------------
    //  Blase zeichnen
    // ------------------------------------------------------------------

    private const float FormFps = 14f;
    private const float BubbleFps = 8f;

    /// <summary>Blase gerade unsichtbar (die Evo rollt sie als Rad durch die Gegend).</summary>
    protected virtual bool HideBubble => false;

    protected virtual Sprite[] IdleBubble => BubbleFrames;

    private void DrawBubble()
    {
        bool show = IsActive && Charges > 0 && PlayerController.Instance != null && !HideBubble;
        if (!show)
        {
            if (bubble != null) bubble.enabled = false;
            if (outer != null) outer.enabled = false;
            return;
        }

        if (bubble == null)
        {
            bubble = SweetFx.NewRenderer("MintBubble", BubbleCenter, 2, transform);
            outer = SweetFx.NewRenderer("MintOuter", BubbleCenter, 2, transform);
        }

        Vector3 at = SweetFx.Snap(BubbleCenter);
        float t = Time.time - formSince;
        Sprite[] form = FormFrames;
        int fi = (int)(t * FormFps);
        bubble.enabled = true;
        bubble.sprite = fi < form.Length && Charges == 1 ? form[fi] : IdleBubble[(int)(Time.time * BubbleFps) % IdleBubble.Length];
        bubble.transform.position = at;

        outer.enabled = Charges >= 2;
        if (outer.enabled)
        {
            Sprite[] ring = OuterFrames;
            outer.sprite = ring[(int)(Time.time * BubbleFps) % ring.Length];
            outer.transform.position = at;
        }
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Mint = new Color32(143, 230, 192, 110);

    public static Sprite[] BubbleFrames => SweetFx.Strip("mint_bubble", Mint, 42);
    public static Sprite[] FormFrames => SweetFx.Strip("mint_form", Mint, 42);
    public static Sprite[] OuterFrames => SweetFx.Strip("mint_outer", Mint, 50);
    public static Sprite[] PopFrames => SweetFx.Strip("mint_pop", Mint, 44);
    public static Sprite[] CandyFrames => SweetFx.Strip("mint_candy", new Color32(255, 90, 110, 255), 10);

    private static readonly int[] BurstRadii = { 56, 72, 88, 104, 128 };
    private static readonly int[] PulseRadii = { 40, 48, 56, 64 };

    public static Sprite[] BurstFor(float radiusTiles)
    {
        return SweetFx.Sized("mint_burst", BurstRadii, radiusTiles * SweetFx.PixelsPerUnit, Mint);
    }

    public static Sprite[] PulseFor(float radiusTiles)
    {
        return SweetFx.Sized("mint_pulse", PulseRadii, radiusTiles * SweetFx.PixelsPerUnit, Mint);
    }
}
