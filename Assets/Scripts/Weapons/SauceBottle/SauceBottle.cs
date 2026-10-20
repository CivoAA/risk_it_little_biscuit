using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ketchup &amp; Senf - eine Quetschflasche mit Gesicht taucht neben dem
/// Spieler auf und spritzt einen Kegel Sauce auf den naechsten Gegner. Der
/// Spritzer selbst tut kaum weh, aber jeder bekleckerte Gegner traegt einen
/// Fleck (<see cref="SauceMark"/>): Ketchup laesst ihn von ALLEN Waffen mehr
/// Schaden nehmen, Senf bremst ihn. Die erste Waffe, die den anderen hilft.
///
/// Stufen (Tabellen neben <see cref="Weapon.stats"/>):
///   3  Senf: jeder zweite Spritzer ist Senf
///   5  Doppelt gewuerzt: Ketchup + Senf zusammen geben noch etwas mehr
///   6  Saucen-Explosion: Bekleckerte platzen beim Tod und bekleckern die Nachbarn
///
/// cooldown = Pause zwischen zwei Spritzern
/// damage   = Schaden je Spritzer
/// range    = Laenge des Strahls in Tiles
/// duration = so lange haelt ein Fleck (Duration-Buff zaehlt)
/// shots    = Spritzer je Druecken (+ Extra-Schuss 2:1)
/// </summary>
public class SauceBottle : Weapon
{
    public const float MustardSlow = 0.65f;
    public const float ComboBonus = 0.15f;
    public const float ExplodeRadius = 1.5f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown damage Strahl Fleck Spritzer
        Level(1.6f, 1f, 3.0f, 4f, 1, "Ketchup-Spritzer: bekleckerte Gegner nehmen 15 % mehr Schaden von allem"),
        Level(1.5f, 2f, 3.0f, 4f, 1, "Schaden +1, Ketchup 20 %"),
        Level(1.5f, 2f, 3.2f, 4f, 1, "Senf: jeder zweite Spritzer ist Senf und bremst die Gegner"),
        Level(1.4f, 3f, 3.6f, 5f, 1, "Breiterer, längerer Strahl, Ketchup 25 %, Schaden +1"),
        Level(1.3f, 3f, 3.6f, 5f, 2, "Doppelt gewürzt: Ketchup und Senf zusammen geben 15 % extra, ein Spritzer mehr"),
        Level(1.2f, 4f, 4.0f, 6f, 2, "Saucen-Explosion: Bekleckerte platzen beim Tod und bekleckern ihre Nachbarn, Ketchup 30 %"),
    };

    private static readonly float[] BonusPerLevel = { 0.15f, 0.20f, 0.20f, 0.25f, 0.25f, 0.30f };
    /// <summary>Halber Oeffnungswinkel des Kegels in Grad.</summary>
    private static readonly float[] HalfAnglePerLevel = { 26f, 26f, 26f, 34f, 34f, 36f };
    private const int MustardFrom = 2;
    private const int ComboFrom = 4;
    private const int ExplodeFrom = 5;

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

    protected override int ExtraShotsPerShot => 2;

    /// <summary>Wo die Flasche neben dem Spieler steht (x wird zur Zielseite gespiegelt).</summary>
    private static readonly Vector2 BottleOffset = new Vector2(0.5f, 0.05f);

    private float timer;
    private int squirts;
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

    /// <summary>Was ein Fleck dieser Stufe kann.</summary>
    public SauceMark.Recipe CurrentRecipe()
    {
        int lvl = Mathf.Clamp(TableLevel, 0, BonusPerLevel.Length - 1);
        return new SauceMark.Recipe
        {
            duration = CurrentDuration,
            bonus = BonusPerLevel[lvl],
            combo = lvl >= ComboFrom ? ComboBonus : 0f,
            explodeDamage = lvl >= ExplodeFrom ? CurrentStats.damage : 0f,
        };
    }

    private bool Fire()
    {
        Vector2 player = PlayerController.Instance.transform.position;
        float range = CurrentStats.range * Mathf.Sqrt(SweetFx.Aoe);

        targets.Clear();
        float maxSqr = (range + 0.5f) * (range + 0.5f);
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            if ((SweetFx.Body(e) - player).sqrMagnitude <= maxSqr) targets.Add(e);
        }
        if (targets.Count == 0) return false;
        targets.Sort((a, b) => (SweetFx.Body(a) - player).sqrMagnitude.CompareTo((SweetFx.Body(b) - player).sqrMagnitude));

        int lvl = Mathf.Clamp(TableLevel, 0, HalfAnglePerLevel.Length - 1);
        float half = HalfAnglePerLevel[lvl] * Mathf.Lerp(1f, SweetFx.Aoe, 0.5f);
        int count = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots)) + RollExtraShots();
        SauceMark.Recipe recipe = CurrentRecipe();

        // Jeder Spritzer nimmt eine andere Richtung: der naechste Gegner, der
        // nicht schon im Kegel eines frueheren liegt.
        List<Vector2> used = new List<Vector2>(count);
        for (int s = 0; s < count; s++)
        {
            Vector2 dir = Vector2.zero;
            for (int i = 0; i < targets.Count && dir == Vector2.zero; i++)
            {
                Vector2 d = (SweetFx.Body(targets[i]) - player).normalized;
                bool covered = false;
                foreach (Vector2 u in used)
                    if (Vector2.Angle(u, d) < half * 1.5f) covered = true;
                if (!covered) dir = d;
            }
            if (dir == Vector2.zero)
            {
                // Alles schon abgedeckt: gegenueber vom ersten spritzen
                dir = used.Count > 0 ? -used[0] : Vector2.right;
            }
            used.Add(dir);

            bool mustard = lvl >= MustardFrom && squirts % 2 == 1;
            squirts++;
            Squirt(player, dir, range, half, mustard, recipe, s * 0.12f);
        }
        targets.Clear();
        return true;
    }

    private void Squirt(Vector2 player, Vector2 dir, float range, float half, bool mustard, SauceMark.Recipe recipe, float delay)
    {
        float side = dir.x >= 0f ? 1f : -1f;
        Vector2 bottleAt = player + new Vector2(BottleOffset.x * side, BottleOffset.y);
        SauceSpray.Spawn(bottleAt, dir, range, half, mustard, CurrentStats.damage, recipe, delay);
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Ketchup = new Color32(232, 51, 47, 255);
    public static readonly Color32 Mustard = new Color32(246, 200, 43, 255);

    public static Sprite[] Bottle(bool mustard) => SweetFx.Strip(mustard ? "sauce_bottle_m" : "sauce_bottle_k", mustard ? Mustard : Ketchup, 12);
    public static Sprite[] Drop(bool mustard) => SweetFx.Strip(mustard ? "sauce_drop_m" : "sauce_drop_k", mustard ? Mustard : Ketchup, 3);
    public static Sprite[] Splat(bool mustard) => SweetFx.Strip(mustard ? "sauce_splat_m" : "sauce_splat_k", mustard ? Mustard : Ketchup, 10);}
