using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Milchkrone - Evo aus Schoko-Salve + Milch-Tunker (die Evo des Kekses).
///
/// Schiesst Salven wie die <see cref="ChocoChips"/> (erbt von ihr), aber die
/// Stuecke sind getunkt: groesser, durchschlagen <see cref="DunkedPierce"/>
/// Gegner, und wo eins seinen letzten Gegner trifft, zerlaeuft es zu einer
/// <see cref="ChocoPuddle"/>, die bremst und leicht nachbrennt.
///
/// Jede <see cref="CrownEvery"/>. Salve ist keine Salve, sondern ein Tunker:
/// Der Keks taucht ab, eine Milchkrone (<see cref="MilkCrown"/>) schiesst um
/// ihn hoch, ein Milchring (<see cref="MilkWave"/>) stoesst alles weg - und
/// wenn die Zacken oben sind, fliegen aus ihnen <see cref="CrownChips"/>
/// getunkte Stuecke in alle Richtungen (+2 je Extra-Schuss).
///
/// Die Werte stehen im Code (wie bei der Salve), nicht am Prefab.
/// Bilder in Resources/Weapons: milk_crown_back/front, choco_chip_dunked,
/// choco_puddle.
/// </summary>
public class ChocoChipsEvo : ChocoChips
{
    /// <summary>Jede wievielte Salve ein Kronen-Tunker ist.</summary>
    public const int CrownEvery = 3;

    /// <summary>Stuecke aus der Krone ohne Extra-Schuesse.</summary>
    public const int CrownChips = 16;

    /// <summary>So viele Stuecke mehr bringt ein Extra-Schuss in der Krone.</summary>
    public const int CrownChipsPerExtraShot = 2;

    /// <summary>Radius des Milchrings beim Tunker in Tiles (mit AOE-Buff).</summary>
    public const float RingRadius = 2.8f;

    /// <summary>Schaden des Milchrings relativ zum Schaden eines Stuecks.</summary>
    public const float RingDamageFactor = 1.5f;

    /// <summary>So viele Gegner trifft ein getunktes Stueck (Salve Stufe 6: 3).</summary>
    public const int DunkedPierce = 4;

    /// <summary>Trefferradius eines getunkten Stuecks in Tiles (Salve: 0,2).</summary>
    public const float DunkedRadius = 0.27f;

    /// <summary>
    /// Bild der Krone, in dem die Zacken oben sind - dann fliegen die Stuecke.
    /// Muss zum Bild passen (CROWN_PEAK im Zeichenskript).
    /// </summary>
    public const int CrownLaunchFrame = 6;

    /// <summary>Grundradius der Krone in diesem Bild (Pixel bei 32 PPU).</summary>
    private const float CrownRimPx = 34f;

    /// <summary>Hoehe der Zackenspitzen ueber dem Boden in diesem Bild (Pixel).</summary>
    private const float CrownTopPx = 28f;

    /// <summary>Die Krone ist flach gemalt: Ellipse mit halber Hoehe.</summary>
    private const float CrownPerspective = 0.5f;

    /// <summary>Jedes zweite Kronen-Stueck fliegt so viel langsamer - zwei Ringe statt einem.</summary>
    private const float SlowRingSpeed = 0.82f;

    private int volleys;

    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        stats = new List<WeaponStats>
        {
            new WeaponStats
            {
                cooldown = 0.9f,
                damage = 11f,
                range = 8.5f,
                shots = 8,
                description = "Getunkte Stücke, jede dritte Salve eine Milchkrone",
            },
        };
        maxweaponLevel = 0;
    }

    protected override IEnumerator Volley()
    {
        volleys++;
        return volleys % CrownEvery == 0 ? Crown() : base.Volley();
    }

    private IEnumerator Crown()
    {
        firing = true;

        WeaponSounds.Play(WeaponSounds.MilkCrown);
        MilkCrown.Create(transform, originOffset);
        SpawnRing(CurrentStats.damage * RingDamageFactor);
        ScreenShake.Kick(1.5f, 0.18f);

        yield return new WaitForSeconds(CrownLaunchFrame / MilkCrown.Fps);

        if (IsActive && PlayerController.Instance != null)
        {
            int count = CrownChips + CrownChipsPerExtraShot * Mathf.Max(0, PlayerController.Instance.ExtraShots);
            float step = 360f / count;
            float start = Random.Range(0f, step);
            Vector2 center = (Vector2)transform.position + originOffset;
            float rim = CrownRimPx / 32f;
            float top = CrownTopPx / 32f;

            WeaponSounds.Play(WeaponSounds.ChocoShot, 1.3f, 0.85f);
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, start + step * i) * Vector2.right;
                // Aus der Zackenspitze: auf der flachen Ellipse, dann hochgezogen
                Vector2 from = center + new Vector2(dir.x * rim, dir.y * rim * CrownPerspective + top);
                float speed = ChipSpeed * (i % 2 == 0 ? 1f : SlowRingSpeed);
                SpawnDunked(from, dir, speed, CurrentStats.range);
            }
        }

        spawnCounter = CurrentCooldown;
        firing = false;
    }

    protected override void Spawn(Vector2 origin, Vector2 dir, float range)
    {
        SpawnDunked(origin, dir, ChipSpeed, range);
    }

    private void SpawnDunked(Vector2 origin, Vector2 dir, float speed, float range)
    {
        GameObject go = new GameObject("DunkedChocoChip");
        go.transform.position = origin;
        MoveToRunScene(go);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, 2);

        float damage = CurrentStats.damage;
        ChocoChipShot shot = go.AddComponent<ChocoChipShot>();
        shot.Launch(dir, speed, range, DunkedRadius, damage, DunkedPierce, DunkedFrames);
        ApplySkills(shot);
        shot.Spent = at => ChocoPuddle.Create(at, damage);
    }

    private void SpawnRing(float damage)
    {
        GameObject go = new GameObject("MilkCrownWave");
        go.transform.position = (Vector2)transform.position + originOffset;
        MoveToRunScene(go);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, -2);

        float radius = RingRadius * PlayerController.Instance.AOERange;
        go.AddComponent<MilkWave>().Launch(transform, originOffset, radius, damage, MilkDunk.Frames);
    }

    public static void MoveToRunScene(GameObject go)
    {
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static Sprite[] dunkedFrames;

    /// <summary>Getunktes Stueck (Resources/Weapons/choco_chip_dunked), Rueckfall das normale.</summary>
    public static Sprite[] DunkedFrames
    {
        get
        {
            if (dunkedFrames == null || dunkedFrames.Length == 0 || dunkedFrames[0] == null)
            {
                dunkedFrames = SpriteStrip.Load("Weapons/choco_chip_dunked");
                if (dunkedFrames.Length == 0) dunkedFrames = Frames;
            }
            return dunkedFrames;
        }
    }
}
