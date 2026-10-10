using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Koenigsplumps - die Startwaffe des Schleimkoenigs (nur fuer ihn). Er
/// schleudert einen Klops Traubengelee (mit ganzer Traube drin) im hohen
/// Bogen auf die DICHTESTE Gegnergruppe in Reichweite. Der Klops klatscht
/// auf, schubst alles im Kreis nach aussen und huepft sofort zur naechsten
/// Gruppe weiter (<see cref="SplatBlob"/>). Ein gestrichelter Ring am Boden
/// zeigt vorher genau, wo und wie gross er landet.
///
/// Stufen: mehr Abpraller, ab Stufe 4 bleibt beim letzten Aufschlag eine
/// klebrige Pfuetze (<see cref="SplatPuddle"/>), auf Stufe 6 zerplatzt der
/// Klops zum Schluss in drei Mini-Klopse.
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Schaden je Aufschlag
/// range    = Wurfweite in Tiles (die Abpraller springen <see cref="BounceRange"/>)
/// duration = Pfuetze in Sekunden (Duration-Buff zaehlt), 0 = keine
/// shots    = Klopse je Wurf (+ Extra-Schuss 1:1)
///
/// Werte im Code (<see cref="LevelStats"/>), nicht am Prefab. Bilder aus
/// Tools/koenigsplumps.py (Resources/Weapons/splat_*).
/// </summary>
public class RoyalSplat : Weapon
{
    /// <summary>Trefferradius eines Aufschlags in Tiles (vor AOE-Buff und Stufen-Faktor).</summary>
    public const float HitRadius = 1.2f;

    /// <summary>So weit springt ein Abpraller hoechstens.</summary>
    public const float BounceRange = 4.5f;

    /// <summary>Schubs nach aussen (Tiles/s) - kurz, damit die Gruppe auseinanderspritzt, aber nicht wegfliegt.</summary>
    public const float Shove = 7f;

    /// <summary>Tempo-Faktor in der Pfuetze.</summary>
    public const float PuddleSlow = 0.45f;

    /// <summary>Mini-Klopse der letzten Stufe: Schaden und Radius im Verhaeltnis zum grossen.</summary>
    public const float MiniFactor = 0.45f;

    /// <summary>Der Gegner sitzt mit dem Pivot unten - seine Mitte liegt etwas hoeher.</summary>
    public static readonly Vector2 BodyOffset = new Vector2(0f, 0.3f);

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Wurfweite  Pfuetze  Klopse
        Level(2.4f, 12f, 6.5f, 0f,   1, "Ein Gelee-Klops plumpst auf die dichteste Gegnergruppe und hüpft einmal weiter"),
        Level(2.4f, 16f, 6.5f, 0f,   1, "Schaden +4"),
        Level(2.2f, 16f, 7f,   0f,   1, "Der Klops hüpft zweimal weiter"),
        Level(2.2f, 18f, 7f,   2.5f, 1, "Klebrige Pfütze: Wo er zuletzt landet, bleiben Gegner kleben"),
        Level(2.0f, 20f, 7.5f, 2.5f, 1, "Hüpft dreimal weiter, größerer Platscher"),
        Level(1.8f, 26f, 8f,   3f,   1, "Schaden +6, zum Schluss zerplatzt er in drei Mini-Klopse"),
    };

    private static readonly int[] BouncesPerLevel = { 1, 1, 2, 2, 3, 3 };
    private static readonly float[] RadiusPerLevel = { 1f, 1f, 1f, 1f, 1.25f, 1.25f };
    private static readonly bool[] SplitPerLevel = { false, false, false, false, false, true };

    /// <summary>Abstand zwischen zwei Klopsen desselben Wurfs (Extra-Schuss).</summary>
    private const float VolleyGap = 0.18f;

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

    /// <summary>Alles, was ein Klops braucht - als Kopie, damit er die Waffe ueberleben kann.</summary>
    public struct Hit
    {
        public float damage;
        public float radius;
        public int bounces;
        public float puddleTime;
        public bool split;
    }

    [Tooltip("Hoehe der Zepterspitze ueber dem Spieler-Pivot (der liegt unter den Fuessen) - von dort fliegt der Klops los.")]
    [SerializeField] private float throwHeight = 0.75f;

    private float nextThrow;
    private int volleyLeft;
    private float nextVolley;
    private readonly List<Vector2> claimed = new List<Vector2>();

    private int LevelIndex(int length) { return Mathf.Clamp(weaponLevel, 0, length - 1); }

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !IsActive) return;

        // laufende Salve (Extra-Schuss): Klopse kurz nacheinander, je auf eine andere Gruppe
        if (volleyLeft > 0)
        {
            if (Time.time < nextVolley) return;
            if (!Throw()) volleyLeft = 0;
            else volleyLeft--;
            nextVolley = Time.time + VolleyGap;
            return;
        }

        if (Time.time < nextThrow) return;

        claimed.Clear();
        if (!Throw()) return;    // nichts in Reichweite - geladen warten

        int count = Mathf.Clamp(Mathf.RoundToInt(CurrentStats.shots + player.ExtraShots), 1, 8);
        volleyLeft = count - 1;
        nextVolley = Time.time + VolleyGap;
        nextThrow = Time.time + CurrentCooldown;
    }

    private bool Throw()
    {
        Hit hit = CurrentHit();
        Vector2 ground = transform.position;
        Vector2 spot;
        if (!FindCluster(ground, CurrentStats.range, hit.radius, null, claimed, 0f, out spot)) return false;

        claimed.Add(spot);
        SplatBlob.Throw(ground, throwHeight, spot, hit);
        if (AudioController.Instance != null) AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.05f);
        return true;
    }

    private Hit CurrentHit()
    {
        return new Hit
        {
            damage = CurrentStats.damage,
            radius = HitRadius * RadiusPerLevel[LevelIndex(RadiusPerLevel.Length)] * AoeFactor(),
            bounces = BouncesPerLevel[LevelIndex(BouncesPerLevel.Length)],
            puddleTime = CurrentStats.duration > 0f ? CurrentDuration : 0f,
            split = SplitPerLevel[LevelIndex(SplitPerLevel.Length)],
        };
    }

    private static float AoeFactor()
    {
        PlayerController player = PlayerController.Instance;
        return player != null ? Mathf.Clamp(player.AOERange, 0.5f, 2.2f) : 1f;
    }

    // ------------------------------------------------------------------
    //  Zielwahl
    // ------------------------------------------------------------------

    private static readonly List<Enemy> near = new List<Enemy>();

    /// <summary>
    /// Sucht die dichteste Gruppe: jeder Gegner in Reichweite ist ein Kandidat,
    /// gezaehlt wird, wie viele im Trefferradius um ihn stehen. Wer von diesem
    /// Klops schon getroffen wurde (<paramref name="seen"/>), zaehlt nur ein
    /// Drittel; Stellen, die ein anderer Klops schon anfliegt
    /// (<paramref name="claimed"/>), und Stellen naeher als
    /// <paramref name="minDist"/> (Abpraller sollen weiterziehen) sind
    /// unattraktiv. Gelandet wird im Schwerpunkt der Gruppe - so trifft der
    /// Platscher sie mittig statt am Rand.
    /// </summary>
    public static bool FindCluster(Vector2 origin, float range, float radius, HashSet<Enemy> seen,
                                   List<Vector2> claimed, float minDist, out Vector2 spot)
    {
        spot = origin;
        near.Clear();
        float maxSqr = range * range;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position + BodyOffset - origin).sqrMagnitude <= maxSqr) near.Add(enemy);
        }
        if (near.Count == 0) return false;

        float rSqr = radius * radius;
        float bestScore = float.MinValue;
        Vector2 best = origin;
        for (int i = 0; i < near.Count; i++)
        {
            Vector2 c = (Vector2)near[i].transform.position + BodyOffset;
            float score = 0f;
            Vector2 sum = Vector2.zero;
            float weight = 0f;
            for (int j = 0; j < near.Count; j++)
            {
                Vector2 p = (Vector2)near[j].transform.position + BodyOffset;
                if ((p - c).sqrMagnitude > rSqr) continue;
                float w = seen != null && seen.Contains(near[j]) ? 0.33f : 1f;
                score += w;
                sum += p * w;
                weight += w;
            }

            if (claimed != null)
            {
                for (int k = 0; k < claimed.Count; k++)
                    if ((claimed[k] - c).sqrMagnitude < rSqr * 1.5f) { score *= 0.3f; break; }
            }

            float dist = (c - origin).magnitude;
            if (dist < minDist) score *= 0.4f;
            score -= dist * 0.02f;   // bei Gleichstand die naehere Gruppe

            if (score > bestScore)
            {
                bestScore = score;
                best = weight > 0f ? sum / weight : c;
            }
        }
        near.Clear();
        spot = best;
        return true;
    }

    // ------------------------------------------------------------------
    //  Aufschlag
    // ------------------------------------------------------------------

    private static readonly List<Enemy> hitBuffer = new List<Enemy>();

    /// <summary>Platscher bei <paramref name="at"/>: Schaden fuer alle im Kreis, alle werden nach aussen geschubst.</summary>
    public static void Splash(Vector2 at, float damage, float radius, HashSet<Enemy> seen, float shake)
    {
        FoxFx.Play(HitFor(radius), at, 22f, 1);
        if (shake > 0f) ScreenShake.Kick(shake, 0.12f);
        if (AudioController.Instance != null) AudioController.Instance.PalySound(AudioController.Instance.EarthHit, 0.12f);

        // erst sammeln, dann treffen - TakeDamage kann Gegner aus Alive nehmen
        hitBuffer.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        float reach = radius + 0.25f;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position + BodyOffset - at).sqrMagnitude <= reach * reach) hitBuffer.Add(enemy);
        }

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy enemy = hitBuffer[i];
            if (enemy == null) continue;
            if (seen != null) seen.Add(enemy);

            Vector2 away = (Vector2)enemy.transform.position + BodyOffset - at;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitCircle.normalized;
            enemy.TakeDamage(damage, null, 0f);
            if (enemy != null) enemy.ApplyPull(away * Shove, 0.12f);
        }
        hitBuffer.Clear();
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    /// <summary>Radien der gemalten Platscher/Ringe in Pixeln (Tools/koenigsplumps.py, HIT_RADII).</summary>
    private static readonly int[] HitRadii = { 32, 40, 48, 60, 72 };

    /// <summary>Radien der gemalten Pfuetzen (PUDDLE_RADII).</summary>
    private static readonly int[] PuddleRadii = { 28, 36, 46 };

    private static Sprite[] blobFrames, miniFrames, shadowFrames;
    private static readonly Sprite[][] hitFrames = new Sprite[HitRadii.Length][];
    private static readonly Sprite[][] markFrames = new Sprite[HitRadii.Length][];
    private static readonly Sprite[][] puddleFrames = new Sprite[PuddleRadii.Length][];

    /// <summary>Klops: 0 rund, 1 gestreckt, 2 gequetscht, 3 wabbelnd. Pivot unten.</summary>
    public static Sprite[] BlobFrames => Cached(ref blobFrames, "Weapons/splat_blob", 20);

    /// <summary>Mini-Klops: 0 rund, 1 gestreckt, 2 gequetscht.</summary>
    public static Sprite[] MiniFrames => Cached(ref miniFrames, "Weapons/splat_mini", 10);

    /// <summary>Schatten: 0 = am Boden (gross) ... 3 = hoch oben (klein).</summary>
    public static Sprite[] ShadowFrames => Cached(ref shadowFrames, "Weapons/splat_shadow", 0);

    public static Sprite[] HitFor(float radius) { return Pick(hitFrames, HitRadii, radius, "Weapons/splat_hit_"); }

    public static Sprite[] MarkFor(float radius) { return Pick(markFrames, HitRadii, radius, "Weapons/splat_mark_"); }

    public static Sprite[] PuddleFor(float radius) { return Pick(puddleFrames, PuddleRadii, radius, "Weapons/splat_puddle_"); }

    private static Sprite[] Cached(ref Sprite[] cache, string path, int fallbackSize)
    {
        if (cache == null || cache.Length == 0 || cache[0] == null)
        {
            cache = SpriteStrip.Load(path);
            if (cache.Length == 0 && fallbackSize > 0)
                cache = new[] { SpriteStrip.Blob(fallbackSize, new Color32(173, 120, 230, 255)) };
        }
        return cache;
    }

    /// <summary>
    /// Der gemalte Streifen, dessen Radius am besten passt. Die Lauf-Kamera ist
    /// pixelgenau - skaliert wird darum nie.
    /// </summary>
    private static Sprite[] Pick(Sprite[][] cache, int[] radii, float radius, string prefix)
    {
        float px = radius * 32f;
        int best = 0;
        for (int i = 1; i < radii.Length; i++)
        {
            if (Mathf.Abs(radii[i] - px) < Mathf.Abs(radii[best] - px)) best = i;
        }

        Sprite[] frames = cache[best];
        if (frames == null || (frames.Length > 0 && frames[0] == null))
        {
            frames = SpriteStrip.Load(prefix + radii[best]);
            cache[best] = frames;
        }
        return frames;
    }
}
