using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Schwerthieb - Startwaffe des Zwiebelritters. Schlaegt in Laufrichtung einen
/// breiten Bogen vor sich: alles in einem Rechteck von 2,5 Tiles Tiefe und
/// 5 Tiles Breite bekommt Schaden. Kein Projektil, der Treffer ist sofort da.
///
/// Mehrere Hiebe einer Stufe kommen direkt hintereinander und wechseln die
/// Seite (hin, zurueck, hin). Erst nach dem letzten Hieb laeuft der Cooldown.
/// Ab drei Hieben (Stufe 5+) ist der letzte ein Finisher: dieselbe Flaeche,
/// aber goldene Sichel mit Funkeln (carrot_slash_finisher).
///
/// cooldown = Pause zwischen zwei Hiebserien
/// damage   = Schaden pro Hieb
/// range    = Tiefe des Bereichs in Tiles (Breite steht in <see cref="Width"/>)
/// shots    = Hiebe pro Serie (+ playerShots)
///
/// Die Werte stehen im Code (<see cref="LevelStats"/>), nicht am Prefab - sie
/// ueberschreiben beim Start, was im Inspector steht.
/// </summary>
public class SwordSlash : Weapon
{
    /// <summary>Breite des Bereichs quer zur Laufrichtung, in Tiles.</summary>
    public const float Width = 5f;

    /// <summary>Abstand zwischen zwei Hieben derselben Serie.</summary>
    private const float SwingGap = 0.15f;

    /// <summary>
    /// Wie lange der Hieb sichtbar ist (alle Frames der Animation): bis der
    /// naechste Hieb der Serie kommt. WaitForSeconds wacht erst ein Frame nach
    /// Ablauf auf - ohne die 20 ms Zugabe blitzt dazwischen ein leeres Bild.
    /// Ueberlappt wird so nur das letzte, fast aufgeloeste Bild.
    /// </summary>
    private const float SlashVisibleTime = SwingGap + 0.02f;

    /// <summary>
    /// Der Finisher ist der letzte Hieb einer Serie - nach ihm kommt erst der
    /// Cooldown, er darf also etwas laenger stehen.
    /// </summary>
    private const float FinisherVisibleTime = 0.2f;

    /// <summary>Ab so vielen Hieben pro Serie wird der letzte zum Finisher (Stufe 5+).</summary>
    private const int FinisherFromSwings = 3;

    /// <summary>
    /// Radius des Bogens in den Hieb-Frames, in Tiles (80 px bei PPU 32).
    /// Darauf wird skaliert - der Rand der Bilder ist nur Platz fuer Spritzer
    /// und zaehlt nicht zur Trefferflaeche.
    /// </summary>
    private const float SlashArcRadius = 2.5f;

    /// <summary>Hieb-Animation aus Tools/karottenhieb.py (Resources).</summary>
    private const string SlashFramesPath = "Weapons/carrot_slash";

    /// <summary>Goldener Abschlusshieb, ebenfalls aus Tools/karottenhieb.py.</summary>
    private const string FinisherFramesPath = "Weapons/carrot_slash_finisher";

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Tiefe  Hiebe
        Level(2.6f, 8f, 2.5f, 1, "Schlägt vor dir zu"),
        Level(2.3f, 8f, 2.5f, 1, "Schneller"),
        Level(2.0f, 10f, 2.5f, 2, "Zweiter Hieb, Damage +2"),
        Level(1.7f, 10f, 2.5f, 2, "Schneller"),
        Level(1.5f, 12f, 2.5f, 3, "Dritter Hieb, Damage +2"),
        Level(1.3f, 13f, 2.5f, 3, "Schneller, Damage +1"),
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

    [Tooltip("Hieb-Animation, zeigt nach rechts (+x), Pivot links mittig, Bogenradius = " +
             "SlashArcRadius. Leer = Resources/Weapons/carrot_slash, fehlt das auch: einfacher Bogen aus dem Code.")]
    [SerializeField] private Sprite[] slashFrames;

    [Tooltip("Ausgangspunkt des Hiebs relativ zum Spieler-Pivot (der liegt unter den Fuessen).")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.4f);

    private readonly List<Enemy> hitBuffer = new List<Enemy>();
    private Vector2 moveDir = Vector2.right;
    private float spawnCounter;
    private bool swinging;

    void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    void Update()
    {
        if (PlayerController.Instance == null) return;

        Vector2 input = PlayerController.Instance.playerMoveDirection;
        if (input != Vector2.zero) moveDir = input.normalized;

        if (!IsActive) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f && !swinging)
        {
            swinging = true;
            StartCoroutine(SwingSeries());
        }
    }

    private IEnumerator SwingSeries()
    {
        int swings = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots + PlayerController.Instance.ExtraShots));

        for (int i = 0; i < swings && IsActive; i++)
        {
            bool finisher = swings >= FinisherFromSwings && i == swings - 1;
            Swing(moveDir, i % 2 == 1, finisher);
            if (i < swings - 1) yield return new WaitForSeconds(SwingGap);
        }

        spawnCounter = CurrentCooldown;
        swinging = false;
    }

    private void Swing(Vector2 dir, bool backhand, bool finisher)
    {
        AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.1f);

        float aoe = PlayerController.Instance.AOERange;
        float depth = CurrentStats.range * aoe;
        float width = Width * aoe;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Vector2 origin = (Vector2)transform.position + originOffset;
        Vector2 center = origin + dir * (depth * 0.5f);

        OverlapDamage.FindEnemies(center, new Vector2(depth, width), angle, hitBuffer);
        float damage = CurrentStats.damage;
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            if (hitBuffer[i] != null) hitBuffer[i].TakeDamage(damage);
        }

        StartCoroutine(ShowSlash(origin, angle, depth, width, backhand, finisher));
    }

    // ------------------------------------------------------------------
    //  Anzeige
    // ------------------------------------------------------------------

    private IEnumerator ShowSlash(Vector2 origin, float angle, float depth, float width, bool backhand, bool finisher)
    {
        GameObject go = new GameObject("SwordSlashFx");
        go.transform.SetParent(transform, false);
        go.transform.position = origin;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.flipY = backhand;
        CopySorting(sr);

        Sprite[] frames = SlashFrames;
        float visible = SlashVisibleTime;
        if (finisher && FinisherFrames.Length > 0)
        {
            frames = FinisherFrames;
            visible = FinisherVisibleTime;
        }
        if (frames.Length == 0)
        {
            yield return ShowGeneratedSlash(go, sr, angle, depth, width, backhand);
            yield break;
        }

        // Bogenradius = Tiefe, Bogendurchmesser = Breite der Trefferflaeche.
        go.transform.localScale = new Vector3(depth / SlashArcRadius, width * 0.5f / SlashArcRadius, 1f);

        // Die Frames fegen selbst durch den Bogen und blenden aus.
        for (float t = 0f; t < visible; t += Time.deltaTime)
        {
            if (go == null) yield break;
            int f = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t / visible * frames.Length));
            sr.sprite = frames[f];
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private Sprite[] loadedFrames, loadedFinisher;

    private Sprite[] SlashFrames
    {
        get
        {
            if (slashFrames != null && slashFrames.Length > 0) return slashFrames;
            return loadedFrames ??= LoadFrames(SlashFramesPath);
        }
    }

    private Sprite[] FinisherFrames => loadedFinisher ??= LoadFrames(FinisherFramesPath);

    private static Sprite[] LoadFrames(string path)
    {
        // LoadAll liefert die Teilbilder ohne feste Reihenfolge.
        Sprite[] frames = Resources.LoadAll<Sprite>(path);
        System.Array.Sort(frames, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
        return frames;
    }

    private static int FrameIndex(Sprite s)
    {
        int cut = s.name.LastIndexOf('_');
        return cut >= 0 && int.TryParse(s.name.Substring(cut + 1), out int i) ? i : 0;
    }

    /// <summary>Alter Platzhalter, falls die Frames fehlen.</summary>
    private IEnumerator ShowGeneratedSlash(GameObject go, SpriteRenderer sr, float angle, float depth, float width, bool backhand)
    {
        sr.sprite = GeneratedSlash;

        // Sprite auf die Trefferflaeche strecken: x = Tiefe, y = Breite.
        Vector2 spriteSize = sr.sprite.bounds.size;
        go.transform.localScale = new Vector3(depth / spriteSize.x, width / spriteSize.y, 1f);

        // Kurzes Nachdrehen in Schlagrichtung, danach ausblenden.
        const float visible = 0.14f;
        float sweep = backhand ? 25f : -25f;
        Color c = sr.color;
        for (float t = 0f; t < visible; t += Time.deltaTime)
        {
            if (go == null) yield break;
            float k = t / visible;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle - sweep * (1f - k));
            c.a = 1f - k * k;
            sr.color = c;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private static void CopySorting(SpriteRenderer target)
    {
        SpriteRenderer player = PlayerController.Instance != null
            ? PlayerController.Instance.GetComponentInChildren<SpriteRenderer>()
            : null;
        if (player == null) return;

        target.sortingLayerID = player.sortingLayerID;
        target.sortingOrder = player.sortingOrder + 1;
    }

    private static Sprite generatedSlash;

    /// <summary>
    /// Platzhalter-Bogen, bis es ein gezeichnetes Sprite gibt: ein halber Ring,
    /// der nach rechts zeigt, 2 Tiles tief und 4 breit bei 32 PPU (wird beim
    /// Anzeigen auf die echte Flaeche gestreckt). Pivot am linken Rand, also am
    /// Spieler.
    /// </summary>
    private static Sprite GeneratedSlash
    {
        get
        {
            if (generatedSlash != null) return generatedSlash;

            const int w = 64, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color32 edge = new Color32(255, 255, 255, 255);
            Color32 inner = new Color32(220, 235, 255, 150);
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32[] px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Ellipse, damit der Bogen die volle Tiefe und Breite ausfuellt.
                    float nx = (x + 0.5f) / w;
                    float ny = ((y + 0.5f) - h * 0.5f) / (h * 0.5f);
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    Color32 col = clear;
                    if (r <= 1f && r >= 0.9f) col = edge;
                    else if (r < 0.9f && r >= 0.55f) col = inner;
                    px[y * w + x] = col;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();

            generatedSlash = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 32f);
            return generatedSlash;
        }
    }
}
