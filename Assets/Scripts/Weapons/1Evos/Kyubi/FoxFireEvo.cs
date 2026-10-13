using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kyuubi - die Evo des Fuchsfeuers (Inari). Rezept: Fuchsfeuer + Duration.
///
/// Kaempft wie das Fuchsfeuer auf Hoechststufe (Irrlichter, Brand, Lauffeuer),
/// aber: stirbt ein Gegner, waehrend er brennt, steigt seine Seele auf
/// (<see cref="KyubiSoul"/>) und fliegt zu Inari. Alle
/// <see cref="SoulsPerTail"/> Seelen waechst ihm ein weiteres Irrlicht - ein
/// weiterer Schwanz im Faecher. Bei neun Schwaenzen gluehen sie golden, und
/// sobald eine Horde in Reichweite ist, erwacht der Neunschwanz
/// (<see cref="KyubiSpirit"/>): die Irrlichter fahren in Inari zurueck, ein
/// riesiger Geisterfuchs steigt aus einer Feuersaeule, springt in die
/// dichteste Gruppe, schlaegt mit neun Feuersaeulen ein, heult und zerfaellt
/// in neun Funken, die je einen Gegner jagen. Danach faengt es wieder mit den
/// Grund-Schwaenzen an.
///
/// Werte stehen im Code (wie beim Fuchsfeuer). Die Tabellen der Basiswaffe
/// (Lauffeuer-Spruenge, Ringgroesse) stehen ueber <see cref="TableLevel"/>
/// auf deren Hoechststufe.
/// </summary>
public class FoxFireEvo : FoxFire
{
    /// <summary>Mehr Schwaenze hat kein Fuchs.</summary>
    public const int MaxTails = 9;

    /// <summary>So viele Seelen braucht ein neuer Schwanz.</summary>
    public const int SoulsPerTail = 3;

    /// <summary>Mindestens so viele Seelen bis zum Erwachen (auch wenn Extra-Schuss schon neun Schwaenze gibt).</summary>
    public const int MinSouls = 12;

    /// <summary>Ruhe nach einem Erwachen, bevor das naechste darf.</summary>
    public const float AwakenPause = 3f;

    /// <summary>Radius des Einschlags in Tiles (vor AOE-Buff) - passt zum gemalten Ring kyubi_slam_80.</summary>
    public const float SlamRadius = 2.5f;

    /// <summary>Einschlag-Schaden im Verhaeltnis zum Irrlicht-Treffer.</summary>
    public const float SlamDamageFactor = 6f;

    /// <summary>Brand nach dem Einschlag: so viel staerker und laenger.</summary>
    public const float SlamBurnFactor = 2f;

    /// <summary>Schaden der neun Schwanz-Funken im Verhaeltnis zum Irrlicht-Treffer.</summary>
    public const float SparkDamageFactor = 1.2f;

    /// <summary>Mehr Seelen fliegen nie gleichzeitig sichtbar - der Rest zaehlt direkt.</summary>
    private const int MaxVisibleSouls = 20;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 1.6f,
            damage = 20f,
            range = 8f,
            duration = 4f,
            shots = 4,
            description = "Seelen lassen Schwaenze wachsen - bei neun erwacht der Kyubi",
        },
    };

    protected override int TableLevel => int.MaxValue;

    private int souls;
    private KyubiSpirit spirit;
    private float nextAwaken;

    protected override void Awake()
    {
        base.Awake();
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        FoxBurn.DiedBurning -= OnDiedBurning;
        FoxBurn.DiedBurning += OnDiedBurning;
    }

    protected override void OnDisable()
    {
        FoxBurn.DiedBurning -= OnDiedBurning;
        base.OnDisable();
    }

    // ------------------------------------------------------------------
    //  Schwaenze
    // ------------------------------------------------------------------

    private int BaseTails(PlayerController player)
    {
        return Mathf.Clamp(Mathf.RoundToInt(CurrentStats.shots + WholeExtraShots), 1, MaxTails);
    }

    private static int SoulsNeeded(int baseTails)
    {
        return Mathf.Max(MinSouls, (MaxTails - baseTails) * SoulsPerTail);
    }

    private bool Ready(PlayerController player)
    {
        return souls >= SoulsNeeded(BaseTails(player));
    }

    protected override int WantedWisps(PlayerController player)
    {
        return Mathf.Min(MaxTails, BaseTails(player) + souls / SoulsPerTail);
    }

    /// <summary>Viele Schwaenze: weiter auseinander, aber nie mehr als ein Halbkreis.</summary>
    protected override float SlotRadiusFor(int count)
    {
        return base.SlotRadiusFor(count) + 0.07f * Mathf.Max(0, count - 4);
    }

    protected override float SlotSpreadFor(int count)
    {
        return count > 1 ? Mathf.Min(base.SlotSpreadFor(count), 190f / (count - 1)) : 0f;
    }

    protected override FoxWisp NewWisp(Vector2 at)
    {
        FoxWisp wisp = FoxWisp.Create(at);
        PlayerController player = PlayerController.Instance;
        wisp.Frames = player != null && Ready(player) ? WispLitFrames : WispFrames;
        return wisp;
    }

    protected override bool BeforeWisps(PlayerController player)
    {
        // Der Neunschwanz ist unterwegs - Inari hat gerade keine Irrlichter.
        if (spirit != null) return false;

        bool ready = Ready(player);
        if (ready && Time.time >= nextAwaken && BestSlamPoint(Center, SlamRadius * AoeFactor()) != null)
        {
            Awaken();
            return false;
        }

        // neun Schwaenze: sie gluehen golden
        Sprite[] frames = ready ? WispLitFrames : WispFrames;
        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] != null) wisps[i].Frames = frames;
        }
        return true;
    }

    // ------------------------------------------------------------------
    //  Seelen
    // ------------------------------------------------------------------

    private void OnDiedBurning(Enemy enemy)
    {
        if (enemy == null || !CanAttack) return;

        if (KyubiSoul.Alive >= MaxVisibleSouls)
        {
            AddSoul();
            return;
        }
        KyubiSoul.Spawn(this, (Vector2)enemy.transform.position + new Vector2(0f, 0.4f));
    }

    /// <summary>Eine Seele ist bei Inari angekommen.</summary>
    public void AddSoul()
    {
        PlayerController player = PlayerController.Instance;
        int cap = player != null && IsActive ? SoulsNeeded(BaseTails(player)) : MinSouls;
        souls = Mathf.Min(cap, souls + 1);
    }

    /// <summary>Testtaste (Strg+F5): sofort neun Schwaenze.</summary>
    public void DebugFillSouls()
    {
        PlayerController player = PlayerController.Instance;
        souls = player != null && IsActive ? SoulsNeeded(BaseTails(player)) : MinSouls;
        nextAwaken = 0f;
    }

    // ------------------------------------------------------------------
    //  Erwachen
    // ------------------------------------------------------------------

    private void Awaken()
    {
        // Die Schwaenze fahren in Inari zurueck - aus ihm steigt gleich die Saeule.
        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] != null) wisps[i].Absorb(transform, new Vector2(0f, 0.45f));
        }
        wisps.Clear();

        souls = 0;
        spirit = KyubiSpirit.Spawn(this, transform, SlamHit(), SparkHit(), SlamRadius * AoeFactor());
    }

    private Hit SlamHit()
    {
        Hit hit = CurrentHit();
        float baseDamage = hit.damage;
        hit.damage = baseDamage * SlamDamageFactor;
        hit.radius = SlamRadius * AoeFactor();
        hit.burnTime = CurrentDuration * SlamBurnFactor;
        hit.tickDamage = baseDamage * BurnTickFactor * SlamBurnFactor;
        return hit;
    }

    private Hit SparkHit()
    {
        Hit hit = CurrentHit();
        hit.damage *= SparkDamageFactor;
        return hit;
    }

    /// <summary>
    /// Der Fuchs zerfaellt: die Grund-Schwaenze fliegen von dort zu Inari zurueck.
    /// Aufgerufen von <see cref="KyubiSpirit"/>.
    /// </summary>
    public void SpiritDone(Vector2 at)
    {
        spirit = null;
        nextAwaken = Time.time + AwakenPause;

        PlayerController player = PlayerController.Instance;
        if (player == null || !IsActive || Resting) return;

        int count = WantedWisps(player);
        for (int i = 0; i < count; i++)
        {
            FoxWisp wisp = NewWisp(at);
            wisp.Slot = Center;
            wisp.ReturnFrom(at + Random.insideUnitCircle * 0.4f, 0.3f + i * 0.12f);
            wisps.Add(wisp);
        }
    }

    /// <summary>
    /// Mitte der dichtesten Gruppe in Reichweite: der Gegner, um den im
    /// Einschlag-Radius die meisten anderen stehen. Bei Gleichstand der naehere.
    /// Null, wenn niemand in Reichweite ist.
    /// </summary>
    public Vector2? BestSlamPoint(Vector2 from, float radius)
    {
        if (!IsActive) return null;
        float range = CurrentStats.range * 1.2f;
        float rangeSqr = range * range;
        float rSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        candidates.Clear();
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            if (((Vector2)e.transform.position - from).sqrMagnitude <= rangeSqr) candidates.Add(e);
        }
        if (candidates.Count == 0) return null;

        // bei riesigen Horden nur jeden n-ten pruefen
        int step = Mathf.Max(1, candidates.Count / 60);
        Vector2 best = candidates[0].transform.position;
        int bestCount = -1;
        float bestDist = float.MaxValue;
        for (int c = 0; c < candidates.Count; c += step)
        {
            Vector2 p = candidates[c].transform.position;
            int n = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                Enemy e = alive[i];
                if (e != null && !e.Untouchable && ((Vector2)e.transform.position - p).sqrMagnitude <= rSqr) n++;
            }
            float d = (p - from).sqrMagnitude;
            if (n > bestCount || (n == bestCount && d < bestDist))
            {
                bestCount = n;
                bestDist = d;
                best = p;
            }
        }
        return best;
    }

    private static readonly List<Enemy> candidates = new List<Enemy>();

    // ------------------------------------------------------------------
    //  Bilder (Resources/Weapons/kyubi_*)
    // ------------------------------------------------------------------

    private static Sprite[] rise, leap, howl, fade, soul, absorb, wisp, wispLit, slam80, slam112;
    private static Sprite shadow;

    private static Sprite[] Load(ref Sprite[] cache, string name, Sprite[] fallback)
    {
        if (cache == null || cache.Length == 0 || cache[0] == null)
        {
            cache = SpriteStrip.Load("Weapons/" + name);
            if (cache.Length == 0) cache = fallback;
        }
        return cache;
    }

    public static Sprite[] RiseFrames => Load(ref rise, "kyubi_rise", WispFrames);
    public static Sprite[] LeapFrames => Load(ref leap, "kyubi_leap", WispFrames);
    public static Sprite[] HowlFrames => Load(ref howl, "kyubi_howl", WispFrames);
    public static Sprite[] FadeFrames => Load(ref fade, "kyubi_fade", WispFrames);
    public static Sprite[] SoulFrames => Load(ref soul, "kyubi_soul", SparkFrames);
    public static Sprite[] AbsorbFrames => Load(ref absorb, "kyubi_absorb", BurstFor(0.75f));
    public static new Sprite[] WispFrames => Load(ref wisp, "kyubi_wisp", FoxFire.WispFrames);
    public static Sprite[] WispLitFrames => Load(ref wispLit, "kyubi_wisp_lit", WispFrames);

    public static Sprite ShadowSprite
    {
        get
        {
            if (shadow == null)
            {
                Sprite[] s = SpriteStrip.Load("Weapons/kyubi_shadow");
                shadow = s.Length > 0 ? s[0] : null;
            }
            return shadow;
        }
    }

    /// <summary>Gemalter Einschlag, der am besten zum Radius (Tiles) passt: 80 oder 112 Pixel.</summary>
    public static Sprite[] SlamFor(float radius)
    {
        return radius * 32f < 96f
            ? Load(ref slam80, "kyubi_slam_80", BurstFor(radius))
            : Load(ref slam112, "kyubi_slam_112", BurstFor(radius));
    }
}
