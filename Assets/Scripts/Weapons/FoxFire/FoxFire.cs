using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fuchsfeuer (Kitsunebi) - Inaris Startwaffe (nur fuer ihn). Hinter ihm
/// schweben blaue Irrlichter mit Fuchsohren im Faecher, wie zusaetzliche
/// Schwaenze (<see cref="FoxWisp"/>). Ist eines bereit, schiesst es in einem
/// Bogen auf einen Gegner, verpufft dort in einem Flammenring und setzt alle
/// darin in blaues Fuchsfeuer (<see cref="FoxBurn"/>), danach kehrt es an
/// seinen Platz zurueck. Ab Stufe 4 "Lauffeuer": stirbt ein brennender
/// Gegner, springt sein Feuer als kleines Irrlicht auf den naechsten ueber -
/// das kann sich durch eine ganze Horde fressen.
///
/// cooldown = Pause je Irrlicht zwischen zwei Angriffen
/// damage   = Treffer im Flammenring; Brand tickt mit <see cref="BurnTickFactor"/> davon
/// range    = Jagdradius in Tiles
/// duration = Brenndauer (Duration-Buff zaehlt)
/// shots    = Irrlichter (+ Extra-Schuss 1:1)
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht. Bilder aus
/// Tools/fuchsfeuer.py (Resources/Weapons/foxfire_*).
/// </summary>
public class FoxFire : Weapon
{
    /// <summary>Fluggeschwindigkeit der Irrlichter in Tiles pro Sekunde.</summary>
    public const float FlySpeed = 12f;

    /// <summary>Radius des Flammenrings in Tiles (vor AOE-Buff und Stufen-Faktor).</summary>
    public const float BurstRadius = 1.25f;

    /// <summary>Anteil des Treffer-Schadens, den der Brand je Tick macht.</summary>
    public const float BurnTickFactor = 0.3f;

    /// <summary>Lauffeuer: so weit springt das Feuer von einem toten Gegner.</summary>
    public const float LeapRange = 3.5f;

    /// <summary>Lauffeuer: Treffer und Ring des uebergesprungenen Feuers im Verhaeltnis zum normalen.</summary>
    public const float LeapFactor = 0.6f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Brand  Irrlichter
        Level(2.4f,  7f, 6f,   2.0f, 2, "Zwei Irrlichter jagen Gegner und setzen sie in blaues Fuchsfeuer"),
        Level(2.4f,  7f, 6f,   2.0f, 3, "Ein drittes Irrlicht schließt sich an"),
        Level(2.2f, 10f, 6.5f, 3.0f, 3, "Schaden +3, das Feuer brennt länger"),
        Level(2.2f, 10f, 6.5f, 3.0f, 3, "Lauffeuer: Stirbt ein brennender Gegner, springt sein Feuer weiter"),
        Level(2.0f, 12f, 7f,   3.0f, 4, "Vier Irrlichter, größerer Flammenring"),
        Level(1.8f, 15f, 7.5f, 3.5f, 4, "Schaden +3, das Lauffeuer springt auf zwei Gegner"),
    };

    /// <summary>Auf so viele Gegner springt das Feuer eines toten Brennenden (0 = gar nicht).</summary>
    private static readonly int[] LeapsPerLevel = { 0, 0, 0, 1, 1, 2 };

    /// <summary>Faktor auf den Flammenring je Stufe.</summary>
    private static readonly float[] RingPerLevel = { 1f, 1f, 1f, 1f, 1.35f, 1.35f };

    private static WeaponStats Level(float cooldown, float damage, float range, float duration,
                                     int shots, string description)
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

    /// <summary>Alles, was ein Treffer braucht - als Kopie, damit Lauffeuer die Waffe nicht kennen muss.</summary>
    public struct Hit
    {
        public float damage;
        public float radius;
        public float burnTime;
        public float tickDamage;
        public int leaps;
    }

    [Tooltip("Mitte des Fuchses relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.45f);

    /// <summary>Abstand der Irrlichter zur Mitte, in Tiles.</summary>
    private const float SlotRadius = 0.95f;

    /// <summary>Winkel zwischen zwei Irrlichtern im Faecher.</summary>
    private const float SlotSpread = 28f;

    /// <summary>Mindestabstand zwischen zwei Abschuessen, damit sie nacheinander losziehen.</summary>
    private const float LaunchGap = 0.12f;

    private readonly List<FoxWisp> wisps = new List<FoxWisp>();
    private Vector2 facing = Vector2.right;
    private Vector2 lastPos;
    private float nextLaunch;

    private int LevelIndex(int length) { return Mathf.Clamp(weaponLevel, 0, length - 1); }

    public Vector2 Center => (Vector2)transform.position + originOffset;

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void OnDisable() { ClearWisps(); }

    void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !IsActive)
        {
            ClearWisps();
            return;
        }

        int want = Mathf.Clamp(Mathf.RoundToInt(CurrentStats.shots + player.ExtraShots), 1, 9);
        while (wisps.Count < want)
        {
            FoxWisp wisp = FoxWisp.Create(Center);
            // die ersten Angriffe gestaffelt, nicht alle im selben Frame
            wisp.ReadyAt = Time.time + 0.4f + wisps.Count * 0.25f;
            wisps.Add(wisp);
        }
        while (wisps.Count > want)
        {
            FoxWisp last = wisps[wisps.Count - 1];
            wisps.RemoveAt(wisps.Count - 1);
            if (last != null) last.Vanish();
        }

        // Blickrichtung aus der Bewegung - die Irrlichter haengen hinter ihm
        Vector2 pos = transform.position;
        Vector2 delta = pos - lastPos;
        lastPos = pos;
        if (delta.sqrMagnitude > 1e-6f)
        {
            Vector2 goal = delta.normalized;
            facing = Vector2.Lerp(facing, goal, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (facing.sqrMagnitude < 1e-4f) facing = goal;
            facing.Normalize();
        }

        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] == null)
            {
                wisps[i] = FoxWisp.Create(Center);
                wisps[i].ReadyAt = Time.time + CurrentCooldown;
            }
            wisps[i].Slot = SlotPosition(i, wisps.Count);
        }

        if (Time.time < nextLaunch) return;

        for (int i = 0; i < wisps.Count; i++)
        {
            FoxWisp wisp = wisps[i];
            if (!wisp.IsIdle || Time.time < wisp.ReadyAt) continue;

            Enemy target = FindTarget(wisp.Position, CurrentStats.range);
            if (target == null) return;   // nichts in Reichweite - geladen warten

            wisp.Launch(target, CurrentHit(), CurrentCooldown);
            AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.05f);
            nextLaunch = Time.time + LaunchGap;
            return;
        }
    }

    private Hit CurrentHit()
    {
        return new Hit
        {
            damage = CurrentStats.damage,
            radius = BurstRadius * RingPerLevel[LevelIndex(RingPerLevel.Length)] * AoeFactor(),
            burnTime = CurrentDuration,
            tickDamage = CurrentStats.damage * BurnTickFactor,
            leaps = LeapsPerLevel[LevelIndex(LeapsPerLevel.Length)],
        };
    }

    /// <summary>
    /// Faecher hinter dem Fuchs, leicht nach oben gebogen, mit sanftem Schweben.
    /// </summary>
    private Vector2 SlotPosition(int index, int count)
    {
        Vector2 back = -facing;
        float angle = (index - (count - 1) * 0.5f) * SlotSpread;
        Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * back);
        dir = (dir + Vector2.up * 0.7f).normalized;
        float bob = Mathf.Sin(Time.time * 3f + index * 1.7f) * 0.08f;
        return Center + dir * SlotRadius + new Vector2(0f, bob);
    }

    private void ClearWisps()
    {
        for (int i = 0; i < wisps.Count; i++)
        {
            if (wisps[i] != null) wisps[i].Vanish();
        }
        wisps.Clear();
    }

    private static float AoeFactor()
    {
        PlayerController player = PlayerController.Instance;
        return player != null ? Mathf.Max(0.5f, player.AOERange) : 1f;
    }

    /// <summary>
    /// Naechster Gegner im Radius. Wer schon brennt oder schon von einem
    /// anderen Irrlicht angeflogen wird, zaehlt als weiter weg - so verteilen
    /// sich die Irrlichter, statt alle denselben zu jagen.
    /// </summary>
    public static Enemy FindTarget(Vector2 origin, float radius, Enemy except = null)
    {
        Enemy best = null;
        float bestScore = float.MaxValue;
        float maxSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy == except || enemy.Untouchable) continue;

            float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqr > maxSqr) continue;

            float score = sqr;
            if (FoxBurn.IsBurning(enemy)) score *= 2.5f;
            if (FoxWisp.IsTargeted(enemy)) score *= 4f;
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        return best;
    }

    // ------------------------------------------------------------------
    //  Treffer
    // ------------------------------------------------------------------

    private static readonly List<Enemy> hitBuffer = new List<Enemy>();

    /// <summary>Flammenring an <paramref name="at"/>: Schaden fuer alle darin, alle fangen Feuer.</summary>
    public static void Explode(Vector2 at, Hit hit)
    {
        FoxFx.Play(BurstFor(hit.radius), at, 18f, 3);

        // erst sammeln, dann treffen - TakeDamage kann Gegner aus Alive nehmen
        hitBuffer.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        float reach = hit.radius + 0.3f;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position - at).sqrMagnitude <= reach * reach) hitBuffer.Add(enemy);
        }

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null) continue;
            // erst anzuenden: stirbt er am Treffer, springt das Feuer schon weiter
            FoxBurn.Apply(enemy, hit);
            enemy.TakeDamage(hit.damage, null, 0.4f);
        }
        hitBuffer.Clear();
    }

    /// <summary>
    /// Lauffeuer: das Feuer eines toten Gegners springt als kleines Irrlicht
    /// auf bis zu <c>hit.leaps</c> Gegner in der Naehe.
    /// </summary>
    public static void Leap(Enemy from, Hit hit)
    {
        if (from == null || hit.leaps <= 0) return;

        Vector2 origin = from.transform.position;
        Hit next = hit;
        next.damage = hit.damage * LeapFactor;
        next.radius = hit.radius * LeapFactor;

        var chosen = new List<Enemy>();
        for (int n = 0; n < hit.leaps; n++)
        {
            Enemy target = null;
            float bestScore = float.MaxValue;
            IReadOnlyList<Enemy> alive = Enemy.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                Enemy enemy = alive[i];
                if (enemy == null || enemy == from || enemy.Untouchable || chosen.Contains(enemy)) continue;
                float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sqr > LeapRange * LeapRange) continue;
                float score = FoxBurn.IsBurning(enemy) ? sqr * 3f : sqr;
                if (score < bestScore)
                {
                    bestScore = score;
                    target = enemy;
                }
            }
            if (target == null) break;
            chosen.Add(target);
            FoxWisp.CreateLeap(origin + new Vector2(0f, 0.3f), target, next);
        }
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    /// <summary>
    /// Ringradien der gemalten Flammenringe in Pixeln (Tools/fuchsfeuer.py,
    /// BURST_RADII). Die Lauf-Kamera ist pixelgenau - skaliert wird darum
    /// nie, sondern der naechstpassende Ring genommen.
    /// </summary>
    private static readonly int[] BurstRadii = { 24, 40, 54, 70 };

    private static Sprite[] wispFrames;
    private static Sprite[] sparkFrames;
    private static Sprite[] trailFrames;
    private static Sprite[] markFrames;
    private static readonly Sprite[][] burstFrames = new Sprite[BurstRadii.Length][];

    /// <summary>Irrlicht (Resources/Weapons/foxfire_wisp), Rueckfall ein blauer Klecks.</summary>
    public static Sprite[] WispFrames
    {
        get
        {
            if (wispFrames == null || wispFrames.Length == 0 || wispFrames[0] == null)
            {
                wispFrames = SpriteStrip.Load("Weapons/foxfire_wisp");
                if (wispFrames.Length == 0) wispFrames = new[] { SpriteStrip.Blob(12, new Color32(143, 220, 255, 255)) };
            }
            return wispFrames;
        }
    }

    /// <summary>Lauffeuer-Funke, ein kleineres Irrlicht (Resources/Weapons/foxfire_spark).</summary>
    public static Sprite[] SparkFrames
    {
        get
        {
            if (sparkFrames == null || sparkFrames.Length == 0 || sparkFrames[0] == null)
            {
                sparkFrames = SpriteStrip.Load("Weapons/foxfire_spark");
                if (sparkFrames.Length == 0) sparkFrames = WispFrames;
            }
            return sparkFrames;
        }
    }

    /// <summary>Nachleuchten, 0 = gross ... 3 = klein (Resources/Weapons/foxfire_trail).</summary>
    public static Sprite[] TrailFrames
    {
        get
        {
            if (trailFrames == null || trailFrames.Length == 0 || trailFrames[0] == null)
            {
                trailFrames = SpriteStrip.Load("Weapons/foxfire_trail");
                if (trailFrames.Length == 0) trailFrames = new[] { SpriteStrip.Blob(8, new Color32(74, 166, 255, 255)) };
            }
            return trailFrames;
        }
    }

    /// <summary>Der gemalte Flammenring, dessen Radius am besten zu <paramref name="radius"/> (Tiles) passt.</summary>
    public static Sprite[] BurstFor(float radius)
    {
        float px = radius * 32f;
        int best = 0;
        for (int i = 1; i < BurstRadii.Length; i++)
        {
            if (Mathf.Abs(BurstRadii[i] - px) < Mathf.Abs(BurstRadii[best] - px)) best = i;
        }

        Sprite[] frames = burstFrames[best];
        if (frames == null || (frames.Length > 0 && frames[0] == null))
        {
            frames = SpriteStrip.Load("Weapons/foxfire_burst_" + BurstRadii[best]);
            burstFrames[best] = frames;
        }
        return frames;
    }

    /// <summary>Flaemmchen auf brennenden Gegnern (Resources/Weapons/foxfire_mark), Rueckfall klein + blau.</summary>
    public static Sprite[] MarkFrames
    {
        get
        {
            if (markFrames == null || markFrames.Length == 0 || markFrames[0] == null)
            {
                markFrames = SpriteStrip.Load("Weapons/foxfire_mark");
                if (markFrames.Length == 0) markFrames = new[] { SpriteStrip.Blob(6, new Color32(74, 166, 255, 255)) };
            }
            return markFrames;
        }
    }
}
