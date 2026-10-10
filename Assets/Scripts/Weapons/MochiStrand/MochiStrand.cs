using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mochi-Faden - Mochis Startwaffe (nur fuer sie). Sie wirft einen klebrigen
/// Erdbeer-Mochi-Klecks auf den naechsten Gegner; von dort zieht sich ein
/// Mochi-Faden zu den naechsten Gegnern und bindet sie zu einer Kette
/// (<see cref="MochiChain"/>). Solange die Kette haelt, sind alle darin
/// langsamer, reissen einander mit, wenn einer wegwill, und ein Teil jedes
/// Treffers geht auf alle anderen ueber. Am Ende schnappt der Faden zurueck:
/// alle werden zur Mitte gerissen und bekommen einen Treffer.
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Treffer des Kleckses; beim Zurueckschnappen x <see cref="SnapFactor"/>
/// range    = Zielsuche fuer den Wurf in Tiles
/// duration = wie lange die Kette haelt (Duration-Buff zaehlt)
/// shots    = Ketten pro Wurf (+ Extra-Schuss 1:1)
/// Kettenlaenge, geteilter Schaden und Bremse je Stufe stehen in den Tabellen unten.
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht. Bilder aus
/// Tools/mochi_faden.py (Resources/Weapons/mochi_*).
/// </summary>
public class MochiStrand : Weapon
{
    /// <summary>Fluggeschwindigkeit des Kleckses in Tiles pro Sekunde.</summary>
    public const float ThrowSpeed = 11f;

    /// <summary>Beim Zurueckschnappen gibt es so viel mal den Klecks-Schaden.</summary>
    public const float SnapFactor = 1.5f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Dauer  Ketten
        Level(3.2f,  6f, 6f,   2.5f, 1, "Ein Mochi-Faden bindet 3 Gegner aneinander"),
        Level(3.0f,  6f, 6f,   2.6f, 1, "Der Faden bindet 4 Gegner"),
        Level(3.0f,  8f, 6.5f, 2.8f, 1, "Schaden +2, Gefesselte sind deutlich langsamer"),
        Level(2.8f,  8f, 6.5f, 2.8f, 1, "30 % jedes Treffers gehen an die ganze Kette"),
        Level(2.8f, 10f, 7f,   3.0f, 2, "Zwei Fäden auf einmal, je 5 Gegner"),
        Level(2.5f, 12f, 7f,   3.0f, 2, "Schaden +2, Zurückschnappen gibt einen Schockring"),
    };

    /// <summary>So viele Gegner haengen je Stufe an einer Kette (inklusive dem ersten).</summary>
    private static readonly int[] LinksPerLevel = { 3, 4, 4, 4, 5, 5 };

    /// <summary>Anteil jedes Treffers, der an die anderen Gegner der Kette geht.</summary>
    private static readonly float[] SharePerLevel = { 0.2f, 0.2f, 0.2f, 0.3f, 0.3f, 0.3f };

    /// <summary>Tempo-Faktor der Gefesselten (0,7 = 30 % langsamer).</summary>
    private static readonly float[] SlowPerLevel = { 0.7f, 0.7f, 0.5f, 0.5f, 0.5f, 0.5f };

    /// <summary>Ab dieser Stufe (Index) gibt das Zurueckschnappen einen Schockring.</summary>
    private const int ShockLevel = 5;

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

    [Tooltip("Ausgangspunkt relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.4f);

    private float spawnCounter;
    private readonly List<Enemy> targets = new List<Enemy>();

    private int LevelIndex(int length) { return Mathf.Clamp(weaponLevel, 0, length - 1); }

    public int CurrentLinks => LinksPerLevel[LevelIndex(LinksPerLevel.Length)];
    public float CurrentShare => SharePerLevel[LevelIndex(SharePerLevel.Length)];
    public float CurrentSlow => SlowPerLevel[LevelIndex(SlowPerLevel.Length)];
    public bool HasShockRing => weaponLevel >= ShockLevel;

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (PlayerController.Instance == null || !IsActive) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        // Ohne Gegner in Reichweite wartet die Waffe geladen - ein Faden ins
        // Leere hat nichts, woran er kleben koennte.
        Vector2 origin = (Vector2)transform.position + originOffset;
        int chains = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + PlayerController.Instance.ExtraShots));
        FindTargets(origin, CurrentStats.range, chains);
        if (targets.Count == 0) return;

        AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.06f);
        for (int i = 0; i < targets.Count; i++) Throw(origin, targets[i]);

        spawnCounter = CurrentCooldown;
    }

    private void Throw(Vector2 origin, Enemy target)
    {
        GameObject go = new GameObject("MochiChain");
        go.transform.position = origin;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        go.AddComponent<MochiChain>().Launch(origin, target, new MochiChain.Settings
        {
            hitDamage = CurrentStats.damage,
            snapDamage = CurrentStats.damage * SnapFactor,
            holdTime = CurrentDuration,
            maxLinks = CurrentLinks,
            share = CurrentShare,
            slow = CurrentSlow,
            shockRadius = HasShockRing ? 1.8f * AoeFactor() : 0f,
        });
    }

    private static float AoeFactor()
    {
        PlayerController player = PlayerController.Instance;
        return player != null ? Mathf.Max(0.5f, player.AOERange) : 1f;
    }

    /// <summary>
    /// Die <paramref name="count"/> naechsten Gegner im Radius, die noch an
    /// keiner Kette haengen - jede Kette soll ihren eigenen Anfang haben.
    /// </summary>
    private void FindTargets(Vector2 origin, float radius, int count)
    {
        targets.Clear();
        float maxSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int n = 0; n < count; n++)
        {
            Enemy best = null;
            float bestSqr = maxSqr;
            for (int i = 0; i < alive.Count; i++)
            {
                Enemy enemy = alive[i];
                if (enemy == null || enemy.Untouchable || MochiChain.IsBound(enemy) || targets.Contains(enemy)) continue;

                float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = enemy;
                }
            }

            if (best == null) break;
            targets.Add(best);
        }
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static Sprite[] blobFrames;
    private static Sprite[] beadFrames;
    private static Sprite[] ploppFrames;

    /// <summary>Wabbel-Bilder des Kleckses (Resources/Weapons/mochi_klecks), Rueckfall ein rosa Klecks.</summary>
    public static Sprite[] BlobFrames
    {
        get
        {
            if (blobFrames == null || blobFrames.Length == 0 || blobFrames[0] == null)
            {
                blobFrames = SpriteStrip.Load("Weapons/mochi_klecks");
                if (blobFrames.Length == 0) blobFrames = new[] { SpriteStrip.Blob(10, new Color32(255, 225, 232, 255)) };
            }
            return blobFrames;
        }
    }

    /// <summary>
    /// Faden-Perlen: 0-2 Fuellung (dick bis duenn), 3-5 der passende Rand.
    /// Rueckfall: zwei Kleckse.
    /// </summary>
    public static Sprite[] BeadFrames
    {
        get
        {
            if (beadFrames == null || beadFrames.Length < 6 || beadFrames[0] == null)
            {
                beadFrames = SpriteStrip.Load("Weapons/mochi_faden");
                if (beadFrames.Length < 6)
                {
                    Sprite fill = SpriteStrip.Blob(3, new Color32(255, 225, 232, 255));
                    Sprite rim = SpriteStrip.Blob(5, new Color32(161, 74, 112, 255));
                    beadFrames = new[] { fill, fill, fill, rim, rim, rim };
                }
            }
            return beadFrames;
        }
    }

    /// <summary>Plopp beim Zurueckschnappen (Resources/Weapons/mochi_plopp), Rueckfall leer.</summary>
    public static Sprite[] PloppFrames
    {
        get
        {
            if (ploppFrames == null || (ploppFrames.Length > 0 && ploppFrames[0] == null))
            {
                ploppFrames = SpriteStrip.Load("Weapons/mochi_plopp");
            }
            return ploppFrames;
        }
    }
}
