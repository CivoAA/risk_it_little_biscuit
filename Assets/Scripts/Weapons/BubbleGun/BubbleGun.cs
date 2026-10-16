using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P. Bubble Gun - wirft eine Luftblase auf einen nahen Gegner. Die Blase
/// faengt ihn ein, traegt ihn zur dichtesten Gegnergruppe in der Naehe und
/// platzt dort (<see cref="BubbleShot"/>): der Gefangene bekommt den vollen
/// Schaden, alle anderen im Radius die Haelfte (<see cref="SplashFactor"/>).
///
/// Eingefangen werden nur gewoehnliche Gegner - Elites, Minibosse und Bosse
/// nie (<see cref="Enemy.CanBeBubbled"/>). Solange einer in der Blase steckt,
/// nimmt er Schaden, kann aber nicht sterben.
///
/// cooldown = Pause zwischen zwei Wuerfen
/// damage   = Schaden am Gefangenen (die anderen: die Haelfte)
/// range    = Zielsuche fuer den Wurf in Tiles
/// shots    = Blasen pro Wurf (+ eine je 5 Extra-Schuesse)
///
/// Skaliert mit Extra-Schuss (1:5), AOE (Explosionsradius) und Cooldown.
/// Wirkdauer wirkt nicht: eine Blase lebt immer <see cref="CarryTime"/>.
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht.
/// </summary>
public class BubbleGun : Weapon
{
    /// <summary>Fuenf Extra-Schuesse fuer eine Blase mehr.</summary>
    protected override int ExtraShotsPerShot => 5;

    /// <summary>Fluggeschwindigkeit der leeren Blase in Tiles pro Sekunde.</summary>
    public const float ThrowSpeed = 6f;

    /// <summary>Spaetestens dann platzt eine Blase, die keinen Gegner erwischt hat.</summary>
    public const float MaxThrowTime = 1.5f;

    /// <summary>
    /// Tempo der vollen Blase in Tiles pro Sekunde. Bewusst gemaechlich - sie
    /// muss nicht ankommen, nach <see cref="CarryTime"/> platzt sie, wo sie ist.
    /// </summary>
    public const float CarrySpeed = 2.5f;

    /// <summary>So lange (Sekunden) traegt die Blase ihren Gefangenen hoechstens. Fest - Wirkdauer wirkt nicht.</summary>
    public const float CarryTime = 2f;

    /// <summary>
    /// So weit (Tiles, ab der Blase) sucht sie nach einer Gruppe. Laeuft erst
    /// spaeter jemand in diesen Umkreis, macht sie sich noch auf den Weg.
    /// </summary>
    public const float GroupSearchRadius = 6f;

    /// <summary>Explosionsradius in Tiles, vor AOERange.</summary>
    public const float BlastRadius = 1.5f;

    /// <summary>Anteil des Schadens fuer alle ausser dem Gefangenen.</summary>
    public const float SplashFactor = 0.5f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Blasen
        Level(3.0f,  6f, 7f, 1, "Fängt einen Gegner in einer Blase und lässt ihn in einer Gruppe platzen"),
        Level(2.8f,  8f, 7f, 1, "Schaden +2, kürzerer Cooldown"),
        Level(2.6f, 10f, 7f, 1, "Schaden +2, kürzerer Cooldown"),
        Level(2.4f, 12f, 7f, 1, "Schaden +2, kürzerer Cooldown"),
        Level(2.2f, 14f, 7f, 1, "Schaden +2, kürzerer Cooldown"),
        Level(2.0f, 16f, 7f, 2, "Zwei Blasen auf einmal, Schaden +2"),
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

    [Tooltip("Ausgangspunkt der Blasen relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.4f);

    private readonly List<Enemy> targets = new List<Enemy>();
    private float spawnCounter;

    /// <summary>Explosionsradius der aktuellen Stufe in Tiles, mit AOERange.</summary>
    public float CurrentBlastRadius
    {
        get
        {
            float aoe = PlayerController.Instance != null ? PlayerController.Instance.AOERange : 1f;
            return BlastRadius * aoe;
        }
    }

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (!CanAttack || PlayerController.Instance == null) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        // Ohne einfangbaren Gegner bleibt die Waffe geladen und wirft, sobald einer da ist.
        if (Fire()) spawnCounter = CurrentCooldown;
    }

    private bool Fire()
    {
        Vector2 origin = (Vector2)transform.position + originOffset;
        CollectTargets(origin, CurrentStats.range);
        if (targets.Count == 0) return false;

        // Jede Blase braucht ihren eigenen Gegner - gibt es weniger, fallen die uebrigen weg.
        int bubbles = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + RollExtraShots()));
        bubbles = Mathf.Min(bubbles, targets.Count);

        WeaponSounds.Play(WeaponSounds.BubbleShot);

        for (int i = 0; i < bubbles; i++)
        {
            BubbleShot.Launch(origin, targets[i], CurrentStats.damage, CurrentBlastRadius, CurrentStats.range);
        }

        return true;
    }

    /// <summary>Alle einfangbaren Gegner im Radius, die noch keine Blase anfliegt, der naechste zuerst.</summary>
    private void CollectTargets(Vector2 origin, float radius)
    {
        targets.Clear();
        float maxSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (!BubbleShot.IsFreeTarget(enemy)) continue;
            if (((Vector2)enemy.transform.position - origin).sqrMagnitude <= maxSqr) targets.Add(enemy);
        }

        targets.Sort((a, b) =>
            ((Vector2)a.transform.position - origin).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - origin).sqrMagnitude));
    }

    /// <summary>Legt ein leeres Objekt in der Lauf-Szene an.</summary>
    public static GameObject CreateInRunScene(string name, Vector2 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }
        return go;
    }
}
