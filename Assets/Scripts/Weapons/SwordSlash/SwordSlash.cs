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
/// Skilltree (Zwiebelritter) - gilt auch fuer die Evo <see cref="ShellBlade"/>:
///   Zwiebelring  - jede 3. Serie endet mit einer Drehung: der letzte Hieb
///                  trifft im Kreis um den Ritter (zwei goldene Sicheln).
///   Gegenschlag  - wird der Ritter getroffen (auch wenn eine Schale den
///                  Treffer abfaengt), ist sein naechster Hieb doppelt so stark
///                  und <see cref="CounterScale"/> mal so gross
///                  (carrot_slash_big / carrot_slash_finisher_big).
///
/// cooldown = Pause zwischen zwei Hiebserien
/// damage   = Schaden pro Hieb
/// range    = Tiefe des Bereichs in Tiles (Breite steht in <see cref="Width"/>)
/// shots    = Hiebe pro Serie. Extra-Schuss zaehlt bewusst nicht - schneller
///            wird der Ritter nur ueber Cooldown (auch zwischen den Hieben).
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

    /// <summary>Bogenradius der grossen Frames (112 px) - passt zu <see cref="CounterScale"/>.</summary>
    private const float BigArcRadius = 3.5f;

    /// <summary>Hieb-Animation (Resources).</summary>
    private const string SlashFramesPath = "Weapons/carrot_slash";

    /// <summary>Goldener Abschlusshieb (Resources).</summary>
    private const string FinisherFramesPath = "Weapons/carrot_slash_finisher";

    /// <summary>Beide in gross fuer den Gegenschlag (Bogen 1 Tile weiter aussen, gleiche Pixelgroesse).</summary>
    private const string BigSlashFramesPath = "Weapons/carrot_slash_big";
    private const string BigFinisherFramesPath = "Weapons/carrot_slash_finisher_big";

    // ------------------------------------------------------- Skilltree

    /// <summary>Zwiebelring: jede so-vielte Serie (Evo: jeder so-vielte Hieb) wird zur Drehung.</summary>
    public const int CircleEvery = 3;

    /// <summary>So lange dauert eine ganze Drehung (beide Haelften nacheinander).</summary>
    private const float CircleVisibleTime = 0.28f;

    /// <summary>Gegenschlag: Schaden des naechsten Hiebs nach einem Treffer.</summary>
    public const float CounterDamage = 2f;

    /// <summary>Gegenschlag: Tiefe und Breite des naechsten Hiebs (= BigArcRadius / SlashArcRadius).</summary>
    public const float CounterScale = 1.4f;

    protected enum SwingKind { Normal, Finisher, Circle }

    private static readonly WeaponStats[] LevelStats =
    {
        //    cooldown  damage  Tiefe  Hiebe
        Level(2.35f, 10f, 2.5f, 1, "Schlägt vor dir zu"),
        Level(2.05f, 10f, 2.5f, 1, "Schneller"),
        Level(1.8f, 12.5f, 2.5f, 2, "Zweiter Hieb, Damage +2.5"),
        Level(1.55f, 12.5f, 2.5f, 2, "Schneller"),
        Level(1.35f, 15f, 2.5f, 3, "Dritter Hieb, Damage +2.5"),
        Level(1.15f, 16.25f, 2.5f, 3, "Schneller, Damage +1.25"),
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
    private int seriesCount;
    private float counterUsedAt = -1f;

    /// <summary>Letzte Laufrichtung - auch im Stehen die, in die zuletzt gelaufen wurde.</summary>
    protected Vector2 MoveDir => moveDir;

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>(LevelStats);
        maxweaponLevel = LevelStats.Length - 1;
    }

    protected virtual void Update()
    {
        if (PlayerController.Instance == null) return;

        Vector2 input = PlayerController.Instance.playerMoveDirection;
        if (input != Vector2.zero) moveDir = input.normalized;

        if (!CanAttack) return;

        spawnCounter -= Time.deltaTime;
        if (spawnCounter <= 0f && !swinging)
        {
            swinging = true;
            StartCoroutine(RunAttack());
        }
    }

    private IEnumerator RunAttack()
    {
        yield return Attack();
        spawnCounter = NextAttackDelay();
        swinging = false;
    }

    /// <summary>Ein Angriff - beim Schwerthieb eine ganze Serie. Danach wartet die Waffe <see cref="NextAttackDelay"/>.</summary>
    protected virtual IEnumerator Attack()
    {
        int swings = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.shots));

        seriesCount++;
        bool circleSeries = Skills.HasGrant(SkillGrants.Zwiebelring) && seriesCount % CircleEvery == 0;

        for (int i = 0; i < swings && IsActive; i++)
        {
            bool last = i == swings - 1;
            SwingKind kind = SwingKind.Normal;
            if (last && circleSeries) kind = SwingKind.Circle;
            else if (last && swings >= FinisherFromSwings) kind = SwingKind.Finisher;

            Swing(moveDir, i % 2 == 1, kind);
            if (!last) yield return new WaitForSeconds(CurrentSwingGap);
        }
    }

    protected virtual float NextAttackDelay() => CurrentCooldown;

    /// <summary>
    /// Abstand zwischen zwei Hieben einer Serie. Cooldown (Buff, Skilltree)
    /// verkuerzt ihn mit - der Ritter schlaegt also auch innerhalb der Serie
    /// schneller, nicht nur die Pause danach wird kuerzer.
    /// </summary>
    private float CurrentSwingGap
    {
        get
        {
            float mult = PlayerController.Instance != null ? PlayerController.Instance.CooldownMultiplier : 1f;
            return SwingGap * mult;
        }
    }

    /// <summary>Zeit bis zum naechsten normalen Hieb - so lange darf einer hoechstens stehen.</summary>
    protected virtual float SwingInterval => CurrentSwingGap;

    /// <summary>
    /// Gegenschlag: ist der Ritter seit dem letzten Gegenschlag getroffen
    /// worden, verbraucht dieser Hieb ihn.
    /// </summary>
    private bool TakeCounter()
    {
        if (!Skills.HasGrant(SkillGrants.Gegenschlag)) return false;
        if (PlayerController.Instance.LastStruckTime <= counterUsedAt) return false;

        counterUsedAt = Time.time;
        return true;
    }

    protected void Swing(Vector2 dir, bool backhand, SwingKind kind)
    {
        WeaponSounds.Play(kind == SwingKind.Normal ? WeaponSounds.SlashSwing : WeaponSounds.SlashFinisher);

        bool counter = TakeCounter();
        float aoe = PlayerController.Instance.AOERange * (counter ? CounterScale : 1f);
        float depth = CurrentStats.range * aoe;
        float width = Width * aoe;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Vector2 origin = (Vector2)transform.position + originOffset;

        if (kind == SwingKind.Circle)
        {
            OverlapDamage.FindEnemies(origin, depth, hitBuffer);
        }
        else
        {
            Vector2 center = origin + dir * (depth * 0.5f);
            OverlapDamage.FindEnemies(center, new Vector2(depth, width), angle, hitBuffer);
        }

        float damage = CurrentStats.damage * (counter ? CounterDamage : 1f);
        for (int i = 0; i < hitBuffer.Count; i++)
        {
            if (hitBuffer[i] != null) hitBuffer[i].TakeDamage(damage);
        }

        if (kind == SwingKind.Circle)
        {
            StartCoroutine(ShowCircle(origin, angle, depth, backhand, counter));
        }
        else
        {
            StartCoroutine(ShowSlash(origin, angle, depth, width, backhand, kind == SwingKind.Finisher, counter));
        }
    }

    // ------------------------------------------------------------------
    //  Anzeige
    // ------------------------------------------------------------------

    private IEnumerator ShowSlash(Vector2 origin, float angle, float depth, float width, bool backhand, bool finisher, bool big)
    {
        Sprite[] frames = SlashFrames;
        float visible = SlashVisibleTime;
        if (!finisher) visible = Mathf.Min(visible, SwingInterval + 0.02f);
        if (finisher && FinisherFrames.Length > 0)
        {
            frames = FinisherFrames;
            visible = FinisherVisibleTime;
        }

        if (frames.Length == 0)
        {
            GameObject go = NewFx(origin, angle, backhand);
            yield return ShowGeneratedSlash(go, go.GetComponent<SpriteRenderer>(), angle, depth, width, backhand);
            yield break;
        }

        float arcRadius = SlashArcRadius;
        if (big)
        {
            Sprite[] bigFrames = finisher ? BigFinisherFrames : BigSlashFrames;
            if (bigFrames.Length > 0)
            {
                frames = bigFrames;
                arcRadius = BigArcRadius;
            }
        }

        // Bogenradius = Tiefe, Bogendurchmesser = Breite der Trefferflaeche.
        Vector2 scale = new Vector2(depth / arcRadius, width * 0.5f / arcRadius);
        yield return PlayFrames(origin, angle, scale, backhand, frames, visible);
    }

    /// <summary>
    /// Zwiebelring: zwei goldene Sicheln nacheinander - erst vorne, dann
    /// hinten. Die hintere ist um 180 Grad gedreht und laeuft damit in
    /// derselben Drehrichtung weiter, zusammen also einmal rundherum.
    /// </summary>
    private IEnumerator ShowCircle(Vector2 origin, float angle, float radius, bool backhand, bool big)
    {
        Sprite[] frames = FinisherFrames;
        float arcRadius = SlashArcRadius;
        if (big && BigFinisherFrames.Length > 0)
        {
            frames = BigFinisherFrames;
            arcRadius = BigArcRadius;
        }
        if (frames.Length == 0) yield break;

        Vector2 scale = new Vector2(radius / arcRadius, radius / arcRadius);
        float half = CircleVisibleTime * 0.5f;

        // Die vordere Haelfte darf etwas laenger stehen, damit sich beide
        // fuer ein, zwei Bilder ueberlappen und keine Luecke blitzt.
        StartCoroutine(PlayFrames(origin, angle, scale, backhand, frames, half + 0.04f));
        yield return new WaitForSeconds(half);
        yield return PlayFrames(origin, angle + 180f, scale, backhand, frames, half);
    }

    /// <summary>Spielt einen Hieb-Streifen einmal ab; die Frames fegen selbst durch den Bogen und blenden aus.</summary>
    private IEnumerator PlayFrames(Vector2 origin, float angle, Vector2 scale, bool flip, Sprite[] frames, float visible)
    {
        GameObject go = NewFx(origin, angle, flip);
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        for (float t = 0f; t < visible; t += Time.deltaTime)
        {
            if (go == null) yield break;
            int f = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t / visible * frames.Length));
            sr.sprite = frames[f];
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private GameObject NewFx(Vector2 origin, float angle, bool flip)
    {
        GameObject go = new GameObject("SwordSlashFx");
        go.transform.SetParent(transform, false);
        go.transform.position = origin;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.flipY = flip;
        CopySorting(sr);
        return go;
    }

    private Sprite[] loadedFrames, loadedFinisher, loadedBig, loadedBigFinisher;

    private Sprite[] SlashFrames
    {
        get
        {
            if (slashFrames != null && slashFrames.Length > 0) return slashFrames;
            return loadedFrames ??= SpriteStrip.Load(SlashFramesPath);
        }
    }

    private Sprite[] FinisherFrames => loadedFinisher ??= SpriteStrip.Load(FinisherFramesPath);
    private Sprite[] BigSlashFrames => loadedBig ??= SpriteStrip.Load(BigSlashFramesPath);
    private Sprite[] BigFinisherFrames => loadedBigFinisher ??= SpriteStrip.Load(BigFinisherFramesPath);

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
