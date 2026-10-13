using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Schoko-Salve - der Keks schiesst seine eigenen Schokostueckchen ab. Sie
/// ploppen kurz nacheinander aus seinem Bauch und fliegen auf den naechsten
/// Gegner (mit Vorhalt), leicht gestreut. Ist keiner in Reichweite, gehen sie
/// in Laufrichtung raus - die Waffe soll sich als Startwaffe nie "tot" anfuehlen.
///
/// cooldown = Pause zwischen zwei Salven
/// damage   = Schaden pro Stueck
/// range    = Zielsuche und Flugweite in Tiles
/// shots    = Stuecke pro Salve (+ Extra-Schuss 1:1, Milchkrone 1:2)
/// Durchschlag je Stufe steht in <see cref="PiercePerLevel"/>.
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht. Bild in
/// Resources/Weapons/choco_chip.
/// </summary>
public class ChocoChips : Weapon
{
    /// <summary>Fluggeschwindigkeit in Tiles pro Sekunde.</summary>
    public const float ChipSpeed = 12f;

    /// <summary>Abstand zwischen zwei Stuecken einer Salve (Sekunden).</summary>
    private const float ChipInterval = 0.07f;

    /// <summary>Zufaellige Streuung um die Zielrichtung, in Grad (+/-).</summary>
    private const float Spread = 7f;

    /// <summary>Trefferradius eines Stuecks in Tiles.</summary>
    public const float ChipRadius = 0.2f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Stuecke
        Level(1.6f, 3f, 7f,   3, "Drei Schokostückchen auf den nächsten Gegner"),
        Level(1.5f, 3f, 7f,   4, "Viertes Stückchen"),
        Level(1.5f, 4f, 7.5f, 4, "Damage +1, durchschlägt 2 Gegner"),
        Level(1.3f, 5f, 7.5f, 5, "Fünftes Stückchen, Damage +1, schneller"),
        Level(1.2f, 6f, 8f,   6, "Sechstes Stückchen, durchschlägt 3 Gegner"),
        Level(1.0f, 7f, 8f,   7, "Siebtes Stückchen, Damage +1, schneller"),
    };

    /// <summary>So viele Gegner trifft ein Stueck je Stufe.</summary>
    private static readonly int[] PiercePerLevel = { 1, 1, 2, 2, 3, 3 };

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

    [Tooltip("Ausgangspunkt relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] protected Vector2 originOffset = new Vector2(0f, 0.4f);

    protected float spawnCounter;
    protected bool firing;
    protected Vector2 lastMoveDir = Vector2.right;

    /// <summary>Gilt fuer das Stueck, das gerade gespawnt wird: erstes der Salve mit "Erster Bissen".</summary>
    protected bool firstBite;

    public int CurrentPierce
    {
        get { return PiercePerLevel[Mathf.Clamp(TableLevel, 0, PiercePerLevel.Length - 1)]; }
    }

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (PlayerController.Instance == null) return;

        Vector2 move = PlayerController.Instance.playerMoveDirection;
        if (move.sqrMagnitude > 0.0001f) lastMoveDir = move.normalized;

        if (!CanAttack || firing) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f) StartCoroutine(Volley());
    }

    /// <summary>Eine Salve. Setzt am Ende <see cref="spawnCounter"/> neu und gibt <see cref="firing"/> frei.</summary>
    protected virtual IEnumerator Volley()
    {
        firing = true;

        int chips = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + RollExtraShots()));
        for (int i = 0; i < chips; i++)
        {
            if (!IsActive || PlayerController.Instance == null) break;

            Vector2 origin = (Vector2)transform.position + originOffset;
            float range = CurrentStats.range;

            // Ziel pro Stueck neu suchen - stirbt der erste Gegner, geht der Rest zum naechsten.
            Enemy target = Nearest(origin, range);
            Vector2 dir = target != null ? Aim.PredictDirection(origin, target, ChipSpeed) : lastMoveDir;
            dir = Quaternion.Euler(0f, 0f, Random.Range(-Spread, Spread)) * dir;

            // Kommt aus dem Bauch, nicht exakt aus einem Punkt.
            origin += Random.insideUnitCircle * 0.12f;

            WeaponSounds.Play(WeaponSounds.ChocoShot);
            firstBite = i == 0 && Skills.HasGrant(SkillGrants.ErsterBissen);
            Spawn(origin, dir, range);
            firstBite = false;

            if (i < chips - 1) yield return new WaitForSeconds(ChipInterval);
        }

        spawnCounter = CurrentCooldown;
        firing = false;
    }

    protected virtual void Spawn(Vector2 origin, Vector2 dir, float range)
    {
        GameObject go = new GameObject("ChocoChip");
        go.transform.position = origin;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, 2);

        ChocoChipShot shot = go.AddComponent<ChocoChipShot>();
        shot.Launch(dir, ChipSpeed, range, ChipRadius, CurrentStats.damage, CurrentPierce, Frames);
        ApplySkills(shot);
    }

    /// <summary>
    /// Skilltree des Kekses aufs Stueck: "Abpraller" (alle Stuecke) und
    /// "Erster Bissen" (nur das Stueck, fuer das <see cref="firstBite"/> gerade steht).
    /// </summary>
    protected void ApplySkills(ChocoChipShot shot)
    {
        shot.Bounces = Skills.HasGrant(SkillGrants.SchokoAbpraller) ? Ricochet.Bounces : 0;
        shot.AlwaysCrit = firstBite;
    }

    /// <summary>Naechster lebender Gegner im Radius, oder null.</summary>
    protected static Enemy Nearest(Vector2 origin, float radius)
    {
        Enemy best = null;
        float bestSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null) continue;

            float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = enemy;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static Sprite[] frames;

    /// <summary>Dreh-Bilder des Stuecks (Resources/Weapons/choco_chip), Rueckfall ein brauner Klecks.</summary>
    public static Sprite[] Frames
    {
        get
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                frames = SpriteStrip.Load("Weapons/choco_chip");
                if (frames.Length == 0) frames = new[] { SpriteStrip.Blob(6, new Color32(74, 38, 22, 255)) };
            }
            return frames;
        }
    }
}
