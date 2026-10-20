using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Geisterstunde - die Evo des Zeitlasers. Rezept: Zeitlaser + Todesstoss.
///
/// Der Laser schiesst wie auf Hoechststufe (Gegner fast bis zum Stillstand
/// gebremst). Ueber dem Spieler schwebt ein Ziffernblatt: jeder Strahl rueckt
/// den Zeiger eine Stunde weiter, jeder getroffene Gegner bekommt eine
/// Uhr-Markierung. Bei zwoelf schlaegt es Mitternacht - auf bis zu
/// <see cref="Strikes"/> markierte Gegner faellt gleichzeitig ein Todesstoss
/// (Elites und Bosse zuerst). Normale Gegner, die danach unter
/// <see cref="ExecuteBelow"/> ihres Lebens stehen, sterben sofort. Elites und
/// Bosse bekommen nur den Schaden - jeder hoechstens einen Schlag je
/// Mitternacht, damit die Evo keine Bossmuehle wird.
///
/// cooldown = Pause zwischen zwei Salven (Cooldown-Buff zaehlt)
/// damage   = Schaden je Strahl; der Mitternachtsschlag ist fest (<see cref="StrikeDamage"/>)
/// duration = so lange haelt eine Markierung (Duration-Buff zaehlt)
/// shots    = halbe Strahlen je Salve (wie die Basiswaffe; Extra-Schuss 2:1)
/// </summary>
public class TimeLaserEvo : TimeLaser
{
    public const int Hours = 12;
    public const int Strikes = 12;
    public const float StrikeDamage = 24f;
    public const float ExecuteBelow = 0.25f;

    [Tooltip("Der Blitz des Todesstosses - nur das Bild, den Schaden macht die Evo selbst.")]
    [SerializeField] private GameObject strikePrefab;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 2.2f,
            damage = 4f,
            duration = 8f,
            shots = 10,
            description = "Jeder Strahl rückt die Uhr vor - um Mitternacht schlägt der Tod bei allen Markierten ein",
        },
    };

    /// <summary>Ziffernblatt ueber dem Kopf (relativ zum Waffen-Objekt).</summary>
    private static readonly Vector2 ClockOffset = new Vector2(0f, 0.75f);
    /// <summary>Uhr-Markierung ueber dem Pivot eines Gegners.</summary>
    private static readonly Vector2 MarkOffset = new Vector2(0f, 0.9f);

    private int hour;
    private readonly Dictionary<Enemy, float> marked = new Dictionary<Enemy, float>();
    private readonly Dictionary<Enemy, SpriteRenderer> markSprites = new Dictionary<Enemy, SpriteRenderer>();
    private readonly List<Enemy> scratch = new List<Enemy>();
    private SpriteRenderer clock;
    private float chimeAge = -1f;

    void Awake()
    {
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive)
        {
            ClearMarks();
            if (clock != null) clock.enabled = false;
            return;
        }
        if (!IsCopy) Achievements.Unlock(Ach.TimeLaserEvo);
        ExpireMarks();
    }

    void LateUpdate()
    {
        if (!IsActive) return;
        DrawClock();
        foreach (KeyValuePair<Enemy, SpriteRenderer> kv in markSprites)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Value.transform.position = SweetFx.Snap((Vector2)kv.Key.transform.position + MarkOffset);
        }
    }

    void OnDestroy()
    {
        ClearMarks();
    }

    protected override void OnBeamFired()
    {
        hour++;
        if (hour < Hours) return;
        hour = 0;
        Midnight();
    }

    public override void OnBeamHit(Enemy enemy)
    {
        if (!IsActive || enemy == null) return;
        marked[enemy] = Time.time + CurrentDuration;
        if (markSprites.ContainsKey(enemy)) return;

        SpriteRenderer sr = SweetFx.NewRenderer("GhostHourMark", (Vector2)enemy.transform.position + MarkOffset, 6);
        sr.sprite = MarkFrames[0];
        markSprites[enemy] = sr;
    }

    // ------------------------------------------------------------------
    //  Mitternacht
    // ------------------------------------------------------------------

    private void Midnight()
    {
        scratch.Clear();
        foreach (KeyValuePair<Enemy, float> kv in marked)
            if (SweetFx.Valid(kv.Key) && !kv.Key.Untouchable) scratch.Add(kv.Key);

        // Elites und Bosse zuerst, sonst zufaellig
        for (int i = 0; i < scratch.Count; i++)
        {
            int j = Random.Range(i, scratch.Count);
            (scratch[i], scratch[j]) = (scratch[j], scratch[i]);
        }
        scratch.Sort((a, b) => (b.IsNormal ? 0 : 1).CompareTo(a.IsNormal ? 0 : 1));
        if (scratch.Count > Strikes) scratch.RemoveRange(Strikes, scratch.Count - Strikes);

        List<Enemy> targets = new List<Enemy>(scratch);
        scratch.Clear();
        ClearMarks();

        chimeAge = 0f;
        WeaponSounds.Play(WeaponSounds.LaserShot, 1f, 0.5f);
        if (targets.Count == 0) return;
        StartCoroutine(Strike(targets));
    }

    private IEnumerator Strike(List<Enemy> targets)
    {
        Scene gameScene = RunScene.Current;
        ScreenShake.Kick(2f, 0.25f);
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy e = targets[i];
            if (!SweetFx.Valid(e) || !IsActive) continue;

            if (strikePrefab != null)
            {
                GameObject fx = Instantiate(strikePrefab, e.transform.position, Quaternion.identity);
                if (gameScene.IsValid() && gameScene.isLoaded) SceneManager.MoveGameObjectToScene(fx, gameScene);
            }
            if (i % 3 == 0) WeaponSounds.Play(WeaponSounds.Deathstrike);

            e.TakeDamage(StrikeDamage, 0.1f, 0f);
            if (e.IsNormal && SweetFx.Valid(e) && e.HealthFraction <= ExecuteBelow)
                e.TakeTrueDamage(e.MaxHealth * e.HealthFraction + 1f);

            yield return new WaitForSeconds(0.03f);
        }
    }

    // ------------------------------------------------------------------
    //  Markierungen
    // ------------------------------------------------------------------

    private void ExpireMarks()
    {
        if (marked.Count == 0) return;
        float now = Time.time;
        scratch.Clear();
        foreach (KeyValuePair<Enemy, float> kv in marked)
            if (kv.Value < now || !SweetFx.Valid(kv.Key)) scratch.Add(kv.Key);
        foreach (Enemy e in scratch) Unmark(e);
        scratch.Clear();
    }

    private void Unmark(Enemy e)
    {
        marked.Remove(e);
        if (markSprites.TryGetValue(e, out SpriteRenderer sr))
        {
            if (sr != null) Destroy(sr.gameObject);
            markSprites.Remove(e);
        }
    }

    private void ClearMarks()
    {
        foreach (SpriteRenderer sr in markSprites.Values)
            if (sr != null) Destroy(sr.gameObject);
        markSprites.Clear();
        marked.Clear();
    }

    // ------------------------------------------------------------------
    //  Ziffernblatt
    // ------------------------------------------------------------------

    private void DrawClock()
    {
        if (clock == null)
        {
            clock = SweetFx.NewRenderer("GhostHourClock", (Vector2)transform.position + ClockOffset, 6, transform);
        }
        clock.enabled = true;
        clock.transform.position = SweetFx.Snap((Vector2)transform.position + ClockOffset);

        // Bilder 0-11: Zeiger auf 12, 1, ... 11. Danach Mitternachts-Leuchten.
        Sprite[] frames = ClockFrames;
        if (chimeAge >= 0f)
        {
            chimeAge += Time.deltaTime;
            int glow = frames.Length - Hours;
            int k = (int)(chimeAge * 12f);
            if (glow > 0 && k < glow)
            {
                clock.sprite = frames[Hours + k];
                return;
            }
            chimeAge = -1f;
        }
        clock.sprite = frames[Mathf.Min(hour, frames.Length - 1)];
    }

    // ------------------------------------------------------------------
    //  Bilder
    // ------------------------------------------------------------------

    public static readonly Color32 Ghost = new Color32(170, 220, 255, 255);

    public static Sprite[] ClockFrames => SweetFx.Strip("ghost_hour_clock", Ghost, 16);
    public static Sprite[] MarkFrames => SweetFx.Strip("ghost_hour_mark", Ghost, 7);
}
