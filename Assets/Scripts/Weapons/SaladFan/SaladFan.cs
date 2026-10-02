using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Salatfaecher - schlaegt sichelfoermige Luftdruckwellen auf die naechsten
/// Gegner. Eine Welle ist etwa ein Tile gross, fliegt geradeaus und geht durch
/// 3 Gegner, alle zwei Stufen einen mehr (<see cref="HitsPerLevel"/>). Jeder
/// weitere Gegner bekommt weniger Schaden, Rueckstoss und Slow als der davor;
/// wie viel dabei verloren geht, sinkt mit der Stufe (<see cref="KeepPerHit"/>).
/// Der Rueckstoss ist staerker als bei anderen Waffen (<see cref="BaseKnockback"/>).
///
/// Alle Wellen eines Schlags gehen gleichzeitig raus, jede auf einen anderen
/// nahen Gegner. Gibt es weniger Gegner als Wellen, faechern die uebrigen um
/// den naechsten herum auf. Ohne Gegner in Reichweite wird nicht geschlagen.
///
/// cooldown = Pause zwischen zwei Schlaegen
/// damage   = Schaden der Welle am ersten Gegner
/// range    = Zielsuche und Flugweite in Tiles
/// shots    = Wellen pro Schlag (+ playerShots)
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht.
///
/// Skilltree (Toast):
///   Spaltwelle - der erste getroffene Gegner spaltet zwei kleinere Wellen ab
///                (<see cref="SaladFanWave"/>, Werte <see cref="SplitScale"/> ff.).
///   Sturmboe   - jeder <see cref="StormEvery"/>. Schlag geht wie ein Uhrzeiger
///                einmal rundherum, mit doppelt so vielen Wellen.
///
/// Die Evo <see cref="TyphoonFan"/> erbt von hier, damit beide Sterne dort
/// automatisch mitlaufen. Was sie anders macht, haengt an den virtuellen
/// Stellschrauben unten (Groesse, Tempo, Wellenzahl, Kreis, Abschuss).
/// </summary>
public class SaladFan : Weapon
{
    /// <summary>Groesse einer Welle in Tiles (wird mit AOERange skaliert).</summary>
    private const float WaveSize = 1f;

    /// <summary>Fluggeschwindigkeit in Tiles pro Sekunde.</summary>
    private const float WaveSpeed = 8f;

    /// <summary>Winkelabstand der Wellen, die mangels Gegner auffaechern.</summary>
    private const float FanStep = 18f;

    /// <summary>Tempo-Faktor am ersten getroffenen Gegner (0,6 = 40 % langsamer).</summary>
    public const float BaseSlow = 0.6f;

    /// <summary>
    /// Rueckstoss am ersten Gegner. Bewusst staerker als bei allen anderen
    /// Waffen (die haben 1) - der Faecher ist eine Druckwelle.
    /// </summary>
    public const float BaseKnockback = 2.5f;

    /// <summary>Weniger Schaden als das macht eine Welle nie, auch am letzten Gegner nicht.</summary>
    public const float MinDamage = 1f;

    // ---------------------------------------------------------- Spaltwelle

    /// <summary>Groesse, Rueckstoss einer Abspaltung relativ zur Mutterwelle.</summary>
    public const float SplitScale = 0.6f;

    /// <summary>Schaden einer Abspaltung relativ zur Mutterwelle an diesem Treffer.</summary>
    public const float SplitDamage = 0.5f;

    /// <summary>Flugweite einer Abspaltung in Tiles.</summary>
    public const float SplitRange = 3.5f;

    /// <summary>So viele Gegner trifft eine Abspaltung.</summary>
    public const int SplitHits = 2;

    // ------------------------------------------------------------ Sturmboe

    /// <summary>Jeder wievielte Schlag als Kreis rausgeht.</summary>
    public const int StormEvery = 5;

    /// <summary>So viel mal mehr Wellen als ein normaler Schlag.</summary>
    public const int StormWaveFactor = 2;

    /// <summary>So lange (Sekunden) braucht der Kreis einmal rundherum.</summary>
    public const float StormSweepTime = 0.6f;

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Reichweite  Wellen
        Level(1.6f, 2f, 6f,   2, "Zwei Sichelwellen, jede trifft bis zu 3 Gegner"),
        Level(1.6f, 3f, 6f,   3, "Dritte Welle, Damage +1"),
        Level(1.5f, 3f, 6f,   4, "Vierte Welle, trifft 4 Gegner"),
        Level(1.5f, 4f, 6.5f, 5, "Fünfte Welle, Damage +1"),
        Level(1.4f, 4f, 6.5f, 6, "Sechste Welle, trifft 5 Gegner"),
        Level(1.3f, 5f, 7f,   7, "Siebte Welle, Damage +1"),
    };

    /// <summary>So viele Gegner trifft eine Welle je Stufe - alle zwei Stufen einer mehr.</summary>
    private static readonly int[] HitsPerLevel = { 3, 3, 4, 4, 5, 5 };

    /// <summary>
    /// Anteil, den eine Welle nach jedem getroffenen Gegner behaelt - fuer
    /// Schaden, Rueckstoss und Slow gleichermassen. Der Schaden faellt dabei
    /// nie unter <see cref="MinDamage"/>: Stufe 1 macht 2 / 1 / 1.
    /// </summary>
    private static readonly float[] KeepPerHit = { 0.5f, 0.6f, 0.66f, 0.72f, 0.8f, 0.88f };

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

    [Tooltip("Optionales Wellen-Sprite, zeigt nach rechts (+x). Leer = einfache Sichel aus dem Code.")]
    [SerializeField] private Sprite waveSprite;

    [Tooltip("Ausgangspunkt der Wellen relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.4f);

    private readonly List<Enemy> targets = new List<Enemy>();
    private float spawnCounter;
    private int attackCount;
    private bool storming;

    /// <summary>Anteil, den eine Welle der aktuellen Stufe pro Treffer behaelt.</summary>
    public virtual float CurrentKeep
    {
        get { return KeepPerHit[Mathf.Clamp(weaponLevel, 0, KeepPerHit.Length - 1)]; }
    }

    /// <summary>So viele Gegner trifft eine Welle der aktuellen Stufe.</summary>
    public virtual int CurrentMaxHits
    {
        get { return HitsPerLevel[Mathf.Clamp(weaponLevel, 0, HitsPerLevel.Length - 1)]; }
    }

    /// <summary>Werte der hoechsten Stufe - die Evo baut darauf auf.</summary>
    protected static WeaponStats MaxLevelStats
    {
        get { return LevelStats[LevelStats.Length - 1]; }
    }

    /// <summary>Ausgangspunkt der Wellen in Weltkoordinaten.</summary>
    protected Vector2 WaveOrigin
    {
        get { return (Vector2)transform.position + originOffset; }
    }

    /// <summary>Groesse einer Welle in Tiles, vor AOERange.</summary>
    protected virtual float CurrentWaveSize
    {
        get { return WaveSize; }
    }

    /// <summary>Fluggeschwindigkeit einer Welle in Tiles pro Sekunde.</summary>
    protected virtual float CurrentWaveSpeed
    {
        get { return WaveSpeed; }
    }

    /// <summary>Bild der Welle, wenn am Prefab keins eingetragen ist.</summary>
    protected virtual Sprite DefaultWaveSprite
    {
        get { return GeneratedWave; }
    }

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    /// <summary>Wellen pro Schlag: Stufe plus Extra-Schuss 1:1.</summary>
    protected virtual int WaveCount()
    {
        return Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + PlayerController.Instance.ExtraShots));
    }

    void Update()
    {
        if (!IsActive || PlayerController.Instance == null) return;
        if (storming) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        // Ohne Ziel bleibt der Faecher geladen und schlaegt, sobald einer da ist.
        if (Fire()) spawnCounter = CurrentCooldown;
    }

    private bool Fire()
    {
        Vector2 origin = WaveOrigin;
        float range = CurrentStats.range;

        CollectTargets(origin, range);
        if (targets.Count == 0) return false;

        AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.1f);

        int waves = WaveCount();
        float speed = CurrentWaveSpeed;
        Vector2 nearestDir = Aim.PredictDirection(origin, targets[0], speed);

        attackCount++;
        if (Skills.HasGrant(SkillGrants.Sturmboe) && attackCount % StormEvery == 0)
        {
            StartCoroutine(StormVolley(nearestDir, waves * StormWaveFactor, range));
            return true;
        }

        for (int i = 0; i < waves; i++)
        {
            Vector2 dir;
            if (i < targets.Count)
            {
                dir = Aim.PredictDirection(origin, targets[i], speed);
            }
            else
            {
                // Uebrige Wellen abwechselnd links und rechts um den naechsten Gegner.
                int k = i - targets.Count + 1;
                float angle = FanStep * ((k + 1) / 2) * (k % 2 == 1 ? 1f : -1f);
                dir = Quaternion.Euler(0f, 0f, angle) * nearestDir;
            }

            SpawnWave(origin, dir, range);
        }

        return true;
    }

    /// <summary>
    /// Sturmboe: Wellen reihum wie ein Uhrzeiger, beim naechsten Gegner
    /// beginnend, einmal ganz herum. Jede Welle startet dort, wo der Spieler
    /// gerade steht. Der Cooldown laeuft erst danach weiter.
    /// </summary>
    protected virtual IEnumerator StormVolley(Vector2 startDir, int count, float range)
    {
        storming = true;

        float step = 360f / count;
        float interval = StormSweepTime / count;

        for (int i = 0; i < count; i++)
        {
            if (!IsActive || PlayerController.Instance == null) break;

            Vector2 origin = WaveOrigin;
            Vector2 dir = Quaternion.Euler(0f, 0f, -step * i) * startDir;
            SpawnWave(origin, dir, range);

            if (i < count - 1) yield return new WaitForSeconds(interval);
        }

        storming = false;
    }

    /// <summary>Alle lebenden Gegner im Radius, der naechste zuerst.</summary>
    private void CollectTargets(Vector2 origin, float radius)
    {
        targets.Clear();
        float maxSqr = radius * radius;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null) continue;
            if (((Vector2)enemy.transform.position - origin).sqrMagnitude <= maxSqr) targets.Add(enemy);
        }

        targets.Sort((a, b) =>
            ((Vector2)a.transform.position - origin).sqrMagnitude
                .CompareTo(((Vector2)b.transform.position - origin).sqrMagnitude));
    }

    /// <summary>Eine Hauptwelle - Abspaltungen der Spaltwelle laufen nicht hier durch.</summary>
    protected void SpawnWave(Vector2 origin, Vector2 dir, float range)
    {
        float size = CurrentWaveSize * PlayerController.Instance.AOERange;
        SaladFanWave wave = CreateWave(origin, size, waveSprite != null ? waveSprite : DefaultWaveSprite);
        wave.Launch(dir, CurrentWaveSpeed, range, size * 0.5f,
                    CurrentStats.damage, MinDamage, CurrentKeep, CurrentMaxHits,
                    BaseSlow, BaseKnockback);

        if (Skills.HasGrant(SkillGrants.Spaltwelle)) wave.EnableSplit();

        OnWaveLaunched(wave, origin, size);
    }

    /// <summary>Nach jeder Hauptwelle, z.B. fuer die Windschneise der Evo.</summary>
    /// <param name="size">Groesse der Welle in Tiles (mit AOERange).</param>
    protected virtual void OnWaveLaunched(SaladFanWave wave, Vector2 origin, float size)
    {
    }

    /// <summary>
    /// Legt eine Welle an (noch ohne Flug - danach <see cref="SaladFanWave.Launch"/>).
    /// Auch fuer die Abspaltungen der Spaltwelle.
    /// </summary>
    /// <param name="size">Groesse in Tiles.</param>
    public static SaladFanWave CreateWave(Vector2 origin, float size, Sprite sprite)
    {
        GameObject go = new GameObject("SaladFanWave");
        go.transform.position = origin;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        CopySorting(sr);

        Vector2 spriteSize = sr.sprite.bounds.size;
        go.transform.localScale = new Vector3(size / spriteSize.x, size / spriteSize.y, 1f);

        return go.AddComponent<SaladFanWave>();
    }

    // ------------------------------------------------------------------
    //  Anzeige
    // ------------------------------------------------------------------

    /// <summary>Gleiche Ebene wie der Spieler, <paramref name="orderOffset"/> davor (+) oder dahinter (-).</summary>
    public static void CopySorting(SpriteRenderer target, int orderOffset = 1)
    {
        SpriteRenderer player = PlayerController.Instance != null
            ? PlayerController.Instance.GetComponentInChildren<SpriteRenderer>()
            : null;
        if (player == null) return;

        target.sortingLayerID = player.sortingLayerID;
        target.sortingOrder = player.sortingOrder + orderOffset;
    }

    private static Sprite generatedWave;

    /// <summary>
    /// Platzhalter-Sichel, bis es ein gezeichnetes Sprite gibt: 32x32 bei
    /// 32 PPU, also genau ein Tile. Die Woelbung zeigt nach rechts (+x),
    /// vorne eine helle Kante, nach hinten ausblassendes Salatgruen.
    /// </summary>
    public static Sprite GeneratedWave
    {
        get
        {
            if (generatedWave == null)
            {
                generatedWave = BuildWaveSprite(new Color32(245, 255, 235, 255), new Color32(150, 225, 110, 220));
            }
            return generatedWave;
        }
    }

    /// <summary>
    /// Sichel in beliebigen Farben (z.B. die blaeuliche des
    /// <see cref="TyphoonFan"/>). <paramref name="leaf"/>.a ist die Deckkraft
    /// an der dicksten Stelle.
    /// </summary>
    public static Sprite BuildWaveSprite(Color32 edge, Color32 leaf)
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        const float outerR = 15.5f;
        const float innerR = 14f;
        const float innerShift = 7f;   // Mitte des ausgeschnittenen Kreises liegt weiter hinten
        const float c = n * 0.5f;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f - c;
                float fy = y + 0.5f - c;
                float rOuter = Mathf.Sqrt(fx * fx + fy * fy);
                float rInner = Mathf.Sqrt((fx + innerShift) * (fx + innerShift) + fy * fy);

                Color32 col = clear;
                if (rOuter <= outerR && rInner > innerR)
                {
                    if (outerR - rOuter < 2f)
                    {
                        col = edge;
                    }
                    else
                    {
                        // Nach hinten (zur Innenkante) durchsichtiger.
                        float t = Mathf.Clamp01((rInner - innerR) / 6f);
                        col = leaf;
                        col.a = (byte)Mathf.RoundToInt(Mathf.Lerp(70f, leaf.a, t));
                    }
                }

                px[y * n + x] = col;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
