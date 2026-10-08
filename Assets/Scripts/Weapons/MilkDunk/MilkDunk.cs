using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Milch-Tunker - der Keks tunkt sich ein und schickt einen Milchring um sich
/// herum. Der Ring waechst in einer halben Sekunde auf seine volle Groesse,
/// trifft dabei jeden Gegner einmal, schiebt ihn kraeftig weg und macht ihn
/// kurz klebrig-langsam. Ab Stufe 3 kommen Nachwellen dazu.
///
/// Geschlagen wird nur, wenn ein Gegner in Reichweite ist - sonst bleibt die
/// Waffe geladen und tunkt, sobald einer nah genug kommt.
///
/// cooldown = Pause zwischen zwei Tunkern
/// damage   = Schaden pro Ring
/// range    = Radius des Rings in Tiles (wird mit AOERange skaliert)
/// shots    = Ringe pro Tunker (+ Extra-Schuss 1:1), kurz hintereinander
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht. Bild aus
/// Tools/schoko_milch.py (Resources/Weapons/milk_wave).
/// </summary>
public class MilkDunk : Weapon
{
    /// <summary>So lange braucht ein Ring von innen bis ganz aussen (Sekunden).</summary>
    public const float WaveTime = 0.5f;

    /// <summary>Abstand zwischen zwei Ringen eines Tunkers.</summary>
    private const float WaveInterval = 0.28f;

    /// <summary>Tempo-Faktor fuer getroffene Gegner (0,7 = 30 % langsamer).</summary>
    public const float Slow = 0.7f;

    /// <summary>Rueckstoss - staerker als der Standard (1), der Ring ist eine Druckwelle.</summary>
    public const float Knockback = 2.5f;

    /// <summary>Nachwellen machen so viel Schaden wie der erste Ring.</summary>
    public const float EchoDamage = 0.6f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Radius  Ringe
        Level(2.6f, 6f,  2.2f, 1, "Ein Milchring stößt alle Gegner um dich weg"),
        Level(2.5f, 8f,  2.4f, 1, "Damage +2, größerer Ring"),
        Level(2.4f, 8f,  2.6f, 2, "Eine Nachwelle"),
        Level(2.2f, 10f, 2.9f, 2, "Damage +2, größerer Ring"),
        Level(2.0f, 12f, 3.2f, 2, "Damage +2, größerer Ring, schneller"),
        Level(1.8f, 14f, 3.6f, 3, "Zweite Nachwelle, Damage +2"),
    };

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

    /// <summary>
    /// Radius je Bild des Milchrings, als Anteil vom vollen Radius. Dieselbe
    /// Kurve wie RING_R in Tools/schoko_milch.py - sonst passt der Treffer
    /// nicht mehr zum Bild.
    /// </summary>
    public static readonly float[] RingCurve =
    {
        7f / 61f, 17f / 61f, 27f / 61f, 36f / 61f, 44f / 61f,
        50f / 61f, 55f / 61f, 58f / 61f, 60f / 61f, 1f,
    };

    /// <summary>Radius des letzten Bildes in Tiles bei Scale 1 (61 px bei 32 PPU).</summary>
    public const float SpriteRadius = 61f / 32f;

    [Tooltip("Mitte des Rings relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.3f);

    private float spawnCounter;
    private bool dunking;

    /// <summary>Radius der aktuellen Stufe mit AOE-Buff.</summary>
    public float CurrentRadius
    {
        get { return CurrentStats.range * PlayerController.Instance.AOERange; }
    }

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (!IsActive || PlayerController.Instance == null || dunking) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        if (AnyEnemyWithin((Vector2)transform.position + originOffset, CurrentRadius))
        {
            StartCoroutine(Dunk());
        }
    }

    private IEnumerator Dunk()
    {
        dunking = true;

        int waves = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + PlayerController.Instance.ExtraShots));
        for (int i = 0; i < waves; i++)
        {
            if (!IsActive || PlayerController.Instance == null) break;

            float damage = CurrentStats.damage * (i == 0 ? 1f : EchoDamage);
            AudioController.Instance.PalySound(AudioController.Instance.areaWeaponSpawn, i == 0 ? 0.25f : 0.15f);
            Spawn(damage);

            if (i < waves - 1) yield return new WaitForSeconds(WaveInterval);
        }

        spawnCounter = CurrentCooldown;
        dunking = false;
    }

    private void Spawn(float damage)
    {
        GameObject go = new GameObject("MilkWave");
        go.transform.position = (Vector2)transform.position + originOffset;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        // Unter dem Spieler, damit der Keks "in" seiner Milch steht.
        SaladFan.CopySorting(sr, -1);

        go.AddComponent<MilkWave>().Launch(transform, originOffset, CurrentRadius, damage, Frames);
    }

    private static bool AnyEnemyWithin(Vector2 origin, float radius)
    {
        float maxSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy != null && ((Vector2)enemy.transform.position - origin).sqrMagnitude <= maxSqr) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    private static Sprite[] frames;

    /// <summary>Bilder des Rings (Resources/Weapons/milk_wave), Rueckfall ein weisser Klecks.</summary>
    public static Sprite[] Frames
    {
        get
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                frames = SpriteStrip.Load("Weapons/milk_wave");
                if (frames.Length == 0) frames = new[] { SpriteStrip.Blob(122, new Color32(251, 246, 236, 110)) };
            }
            return frames;
        }
    }
}
