using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Milchpanzer - Zwischenboss der Kueche (Rolle Zwischenboss =
/// EnemyRole.MiniBoss). Ein grimmiger Milchkarton auf Panzerketten, mit
/// Stahlschuerze, Boxhandschuhen und einem Ringel-Strohhalm im Schraubdeckel.
/// Das Prefab baut Tools -> Gegner -> Milchpanzer bauen (MilchpanzerBuilder).
///
///   Fahren     rollt stur auf den Spieler zu (Katalog-Tempo), der Auspuff
///              pafft Milchdampf.
///
///   Strohhalm-Sog  alle paar Sekunden: er bleibt stehen, der Strohhalm
///              schnellt aus dem Deckel, um ihn liegt die rote Zone - und er
///              SAUGT. Der Spieler wird zu ihm hingezogen (langsamer, als er
///              laufen kann - wer dagegenhaelt, kommt weg), Milchtropfen
///              fliegen in den Halm. Normale Gegner in der Naehe werden
///              mitgezogen und weggeschluerft; jeder macht die Zone groesser.
///              Ist die Zone voll: RUELPS. Wer drinsteht, bekommt ein
///              Vielfaches seines Beruehrungsschadens, die Kamera wackelt,
///              ringsum spritzt Milch.
///
///   Ab der Haelfte seiner Leben saugt er staerker, kuerzer und oefter.
///
/// Drei Sachen sind Absicht:
///   1. Der Sog ist schwaecher als das Spielertempo. Er soll den Spieler
///      in die Zone ZIEHEN, wenn der nicht aufpasst - nicht festhalten.
///   2. Die Zone waechst mit jedem geschluerften Gegner. Wer die Horde um
///      ihn herum vorher wegraeumt, hat es leichter - und wer ihn von
///      weit weg beschiesst, laesst ihn fett werden.
///   3. Nach dem Ruelpser steht er kurz still - das Fenster zum Draufhauen.
///
/// Animation: kein Animator. Er spielt seine Streifen selbst ab (Bilder unter
/// Assets/Art/Gegner/new/miniboss/milchpanzer_*.png, PPU 32, Pivot Bodenmitte,
/// gezeichnet nach rechts).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemyMilchpanzer : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float PhaseTwoAt = 0.5f;

    /// <summary>So lange faehrt er zwischen zwei Saugern (min, max).</summary>
    private const float DriveMin = 5.5f, DriveMax = 7.5f;
    private const float DriveMinPhase2 = 3.5f, DriveMaxPhase2 = 5f;

    /// <summary>Beim Auftauchen faehrt er erst einmal ein Stueck heran.</summary>
    private const float FirstDrive = 4f;

    /// <summary>So lange saugt er, bis er ruelpst (Zonenzeit).</summary>
    private const float SuckTime = 2.8f;
    private const float SuckTimePhase2 = 2.3f;

    /// <summary>
    /// Zug auf den Spieler in Einheiten/s. Der Spieler laeuft 4 - so kommt
    /// er mit Gegenhalten noch mit gut der Haelfte seines Tempos weg.
    /// </summary>
    private const float PullStrength = 1.9f;
    private const float PullStrengthPhase2 = 2.5f;

    /// <summary>Bis hierhin reicht der Sog (Einheiten ab Zonenmitte). Etwa ein Bildschirm.</summary>
    private const float PullRange = 11f;

    /// <summary>Der Sog setzt weich ein, nicht mit einem Ruck.</summary>
    private const float PullRampTime = 0.5f;

    /// <summary>Zug auf normale Gegner (Einheiten/s) und ab wann sie geschluerft sind.</summary>
    private const float EnemyPullSpeed = 4.5f;
    private const float EnemyPullRange = 7f;

    /// <summary>
    /// Ab hier ist ein Gegner geschluerft (Abstand zur Kartonmitte). Muss
    /// ueber Colliderradius + Gegnerradius liegen, sonst drueckt der Koerper
    /// sie weg, bevor sie ankommen.
    /// </summary>
    private const float SlurpRadius = 2.4f;

    /// <summary>Zonenradius ohne Beute, Zuwachs je geschluerftem Gegner, Obergrenze.</summary>
    private const float ZoneRadius = 2.6f;
    private const float ZonePerSlurp = 0.1f;
    private const float ZoneMax = 4.2f;

    /// <summary>Spielerkoerper, der noch mit drinsteht (halber Spieler).</summary>
    private const float PlayerRadius = 0.25f;

    /// <summary>Ruelpsschaden als Vielfaches des Beruehrungsschadens.</summary>
    private const float BurpDamageFactor = 2.5f;

    /// <summary>Nach dem Ruelpser steht er still - das Fenster zum Draufhauen.</summary>
    private const float RestAfterBurp = 1.3f;

    /// <summary>Kamerawackeln beim Ruelpser: Pixel, Sekunden.</summary>
    private const float ShakePixels = 5f, ShakeSeconds = 0.4f;

    // ------------------------------------------------- Tabellen aus dem Bildskript

    private const float Fps = 12f;

    /// <summary>Im Ruelps-Streifen: in diesem Bild knallt es.</summary>
    private const int BurpFrame = 3;

    /// <summary>Mitte der Zone ueber dem Pivot (Ketten + Karton).</summary>
    private static readonly Vector2 ZoneCenter = new Vector2(0f, 0.5f);

    /// <summary>Wo die Gegner verschwinden (Kartonmitte).</summary>
    private static readonly Vector2 BodyCenter = new Vector2(0f, 1.3f);

    /// <summary>Spitze des ausgefahrenen Halms (Bild schaut nach rechts).</summary>
    private static readonly Vector2 StrawTip = new Vector2(0.05f, 3.6f);

    // -------------------------------------------------------------- Effekte

    /// <summary>So viele Milchtropfen fliegen pro Sekunde in den Halm.</summary>
    private const float DropletsPerSecond = 22f;
    private const float DropletSpeed = 7f;

    /// <summary>Spritzer im Kreis beim Ruelpser (pro Einheit Zonenumfang).</summary>
    private const float SplashesPerUnit = 0.75f;

    private const string EffectLayer = "Objects";

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D hitbox;

    [SerializeField] private Sprite[] drive;
    [SerializeField] private Sprite[] strawOut;
    [SerializeField] private Sprite[] suck;
    [SerializeField] private Sprite[] burp;
    [SerializeField] private Sprite[] droplet;
    [SerializeField] private Sprite[] splash;

    // ---------------------------------------------------------------- Zustand

    private Enemy enemy;
    private Rigidbody2D rb;

    private bool driving;
    private float driveClock;
    private bool facingLeft;

    private bool sucking;
    private float suckClock;
    private float pullStrength;
    private float zoneRadius;
    private BossTelegraphMarker marker;

    private float dropletDebt;
    private readonly List<Droplet> droplets = new List<Droplet>();
    private readonly List<Splash> splashes = new List<Splash>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private class Droplet
    {
        public SpriteRenderer r;
        public Vector2 pos;
        public float age;
        public bool live;
    }

    private class Splash
    {
        public SpriteRenderer r;
        public float age;
        public bool live;
    }

    public bool IsPhaseTwo => enemy != null && enemy.HealthFraction <= PhaseTwoAt;

    // ---------------------------------------------------------------- Ablauf

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        enemy.SelfSteered = true;

        // Haengt die Gegner-Werkstatt einen Animator dran: der wuerde die
        // Bilder ueberschreiben.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        if (body != null)
        {
            Animator bodyAnimator = body.GetComponent<Animator>();
            if (bodyAnimator != null) bodyAnimator.enabled = false;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearMarker();
        driving = false;
        sucking = false;
        foreach (Droplet d in droplets) if (d.r != null) Destroy(d.r.gameObject);
        foreach (Splash s in splashes) if (s.r != null) Destroy(s.r.gameObject);
        droplets.Clear();
        splashes.Clear();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        PlayerController player = PlayerController.Instance;
        bool hasPlayer = player != null && player.gameObject.activeSelf;

        // Im Stand ist er ein Panzer: weder Spieler noch Horde schieben ihn
        // weg - sonst wandert er aus seiner eigenen Zone.
        rb.constraints = driving ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.FreezeAll;

        if (driving && hasPlayer)
        {
            Vector2 to = (Vector2)player.transform.position - rb.position;
            rb.linearVelocity = to.sqrMagnitude > 0.04f ? to.normalized * enemy.CurrentSpeed : Vector2.zero;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (sucking) Pull(player, hasPlayer);
    }

    private void Update()
    {
        if (driving)
        {
            driveClock += Time.deltaTime;
            Show(drive, Mathf.FloorToInt(driveClock * Fps));
            FacePlayer();
        }
        UpdateDroplets();
        UpdateSplashes();
    }

    private IEnumerator Run()
    {
        yield return Drive(FirstDrive);

        while (true)
        {
            if (PlayerAvailable()) yield return StrawSuck();

            bool phaseTwo = IsPhaseTwo;
            yield return Drive(phaseTwo ? Random.Range(DriveMinPhase2, DriveMaxPhase2)
                                        : Random.Range(DriveMin, DriveMax));
        }
    }

    private IEnumerator Drive(float seconds)
    {
        driving = true;
        yield return new WaitForSeconds(seconds);
        driving = false;
    }

    // ---------------------------------------------------------- Strohhalm-Sog

    private IEnumerator StrawSuck()
    {
        bool phaseTwo = IsPhaseTwo;
        FacePlayer();

        // 1. Halm raus - die Zone liegt ab jetzt, die Uhr laeuft mit dem Sog
        zoneRadius = ZoneRadius;
        Vector2 center = (Vector2)transform.position + ZoneCenter;
        marker = BossTelegraph.Zone(center, zoneRadius);
        marker.SetProgress(0f);
        MilchpanzerSounds.StrawPop();
        yield return PlayOnce(strawOut);

        // 2. Saugen
        float suckTime = phaseTwo ? SuckTimePhase2 : SuckTime;
        pullStrength = phaseTwo ? PullStrengthPhase2 : PullStrength;
        suckClock = 0f;
        dropletDebt = 0f;
        sucking = true;
        MilchpanzerSounds.Slurp(suckTime);

        while (suckClock < suckTime)
        {
            suckClock += Time.deltaTime;
            if (marker != null) marker.SetProgress(suckClock / suckTime);
            Show(suck, Mathf.FloorToInt(suckClock * Fps));
            SpawnDroplets(center);
            yield return null;
        }
        sucking = false;

        // 3. Ruelps
        float frameTime = 1f / Fps;
        for (int i = 0; i < burp.Length; i++)
        {
            Show(burp, i);
            if (i == BurpFrame) Burp(center);
            yield return new WaitForSeconds(frameTime);
        }

        // 4. Halm wieder rein, kurz verschnaufen
        for (int i = strawOut.Length - 1; i >= 0; i--)
        {
            Show(strawOut, i);
            yield return new WaitForSeconds(frameTime * 0.7f);
        }
        yield return Idle(RestAfterBurp);
    }

    /// <summary>Zieht Spieler und normale Gegner zur Zonenmitte. Laeuft im FixedUpdate.</summary>
    private void Pull(PlayerController player, bool hasPlayer)
    {
        Vector2 center = (Vector2)transform.position + ZoneCenter;
        float ramp = Mathf.Clamp01(suckClock / PullRampTime);

        if (hasPlayer)
        {
            Vector2 to = center - (Vector2)player.transform.position;
            float d = to.magnitude;
            // In der Mitte nicht ueberschiessen, ganz aussen nachlassen
            if (d > 0.2f && d < PullRange)
            {
                float edge = 1f - Mathf.Clamp01((d - PullRange * 0.75f) / (PullRange * 0.25f));
                player.AddDrift(to / d * pullStrength * ramp * edge * Mathf.Clamp01(d / 0.6f));
            }
        }

        Vector2 mouth = (Vector2)transform.position + BodyCenter;
        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.Role != EnemyRole.Normal) continue;

            Vector2 to = mouth - (Vector2)e.transform.position;
            float d = to.magnitude;
            if (d > EnemyPullRange) continue;

            if (d <= SlurpRadius)
            {
                Slurp(e, center);
                continue;
            }
            e.ApplyPull(to / d * EnemyPullSpeed * ramp, 0.15f);
        }
    }

    /// <summary>Weggeschluerft: keine Beute, aber die Zone waechst.</summary>
    private void Slurp(Enemy e, Vector2 center)
    {
        Destroy(e.gameObject);
        MilchpanzerSounds.Gulp();
        zoneRadius = Mathf.Min(ZoneMax, zoneRadius + ZonePerSlurp);
        if (marker != null) marker.Spread(center, zoneRadius);
    }

    private void Burp(Vector2 center)
    {
        if (marker != null) marker.Impact();
        marker = null;

        MilchpanzerSounds.Burp();
        ScreenShake.Kick(ShakePixels, ShakeSeconds);

        PlayerController player = PlayerController.Instance;
        if (player != null && player.gameObject.activeSelf)
        {
            float reach = zoneRadius + PlayerRadius;
            if (((Vector2)player.transform.position - center).sqrMagnitude <= reach * reach)
            {
                player.TakeDamage(enemy.ContactDamage * BurpDamageFactor);
            }
        }

        // Milch spritzt am Zonenrand und auf halbem Weg
        int count = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * zoneRadius * SplashesPerUnit));
        float turn = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = turn + i * Mathf.PI * 2f / count;
            SpawnSplash(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * zoneRadius, 0f);
        }
        int inner = count / 2;
        for (int i = 0; i < inner; i++)
        {
            float a = turn + (i + 0.5f) * Mathf.PI * 2f / inner;
            SpawnSplash(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * zoneRadius * 0.55f, -0.06f);
        }
    }

    private IEnumerator Idle(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            Show(drive, 0);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- Effekte

    /// <summary>Milchtropfen vom Zonenrand, die in den Halm fliegen.</summary>
    private void SpawnDroplets(Vector2 center)
    {
        if (droplet == null || droplet.Length == 0) return;

        dropletDebt += DropletsPerSecond * Time.deltaTime;
        while (dropletDebt >= 1f)
        {
            dropletDebt -= 1f;
            float a = Random.value * Mathf.PI * 2f;
            float r = zoneRadius * Random.Range(0.9f, 1.6f);
            Droplet d = FreeDroplet();
            d.pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            d.age = 0f;
            d.live = true;
            d.r.enabled = true;
            d.r.sprite = droplet[0];
            d.r.transform.position = Snap(d.pos);
        }
    }

    private void UpdateDroplets()
    {
        if (droplets.Count == 0) return;

        Vector2 tip = StrawTipWorld();
        foreach (Droplet d in droplets)
        {
            if (!d.live) continue;

            d.age += Time.deltaTime;
            Vector2 to = tip - d.pos;
            float step = DropletSpeed * (0.6f + d.age * 1.6f) * Time.deltaTime;
            if (to.magnitude <= step || !sucking && d.age > 1.5f)
            {
                d.live = false;
                d.r.enabled = false;
                continue;
            }
            d.pos += to.normalized * step;
            d.r.transform.position = Snap(d.pos);
            d.r.sprite = droplet[Mathf.FloorToInt(d.age * 14f) % droplet.Length];
        }
    }

    private void SpawnSplash(Vector2 at, float delay)
    {
        if (splash == null || splash.Length == 0) return;

        Splash s = FreeSplash();
        s.age = delay;
        s.live = true;
        s.r.enabled = delay >= 0f;
        s.r.sprite = splash[0];
        s.r.transform.position = Snap(at);
    }

    private void UpdateSplashes()
    {
        foreach (Splash s in splashes)
        {
            if (!s.live) continue;

            s.age += Time.deltaTime;
            if (s.age < 0f) continue;

            int i = Mathf.FloorToInt(s.age * 14f);
            if (i >= splash.Length)
            {
                s.live = false;
                s.r.enabled = false;
                continue;
            }
            s.r.enabled = true;
            s.r.sprite = splash[i];
        }
    }

    private Droplet FreeDroplet()
    {
        foreach (Droplet d in droplets) if (!d.live && d.r != null) return d;
        var made = new Droplet { r = NewEffectRenderer("Milchtropfen", 3) };
        droplets.Add(made);
        return made;
    }

    private Splash FreeSplash()
    {
        foreach (Splash s in splashes) if (!s.live && s.r != null) return s;
        var made = new Splash { r = NewEffectRenderer("Milchspritzer", -1) };
        splashes.Add(made);
        return made;
    }

    private SpriteRenderer NewEffectRenderer(string name, int order)
    {
        var go = new GameObject(name);
        RunScene.Place(go, "Effekte");
        var r = go.AddComponent<SpriteRenderer>();
        if (body != null) r.sharedMaterial = body.sharedMaterial;
        r.sortingLayerName = EffectLayer;
        r.sortingOrder = order;
        r.enabled = false;
        return r;
    }

    // ---------------------------------------------------------------- Kleinkram

    private Vector2 StrawTipWorld()
    {
        Vector2 tip = StrawTip;
        if (facingLeft) tip.x = -tip.x;
        return (Vector2)transform.position + tip;
    }

    /// <summary>Auf ganze Pixel (PPU 32), damit die Tropfen nicht flimmern.</summary>
    private static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, 0f);
    }

    /// <summary>Bild schaut nach rechts - steht der Spieler links, spiegeln. 0.3 Totzone gegen Flackern.</summary>
    private void FacePlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || body == null) return;

        float dx = player.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.3f) return;
        facingLeft = dx < 0f;
        body.flipX = facingLeft;
    }

    private IEnumerator PlayOnce(Sprite[] strip)
    {
        if (strip == null) yield break;
        float frameTime = 1f / Fps;
        for (int i = 0; i < strip.Length; i++)
        {
            Show(strip, i);
            yield return new WaitForSeconds(frameTime);
        }
    }

    private void Show(Sprite[] strip, int i)
    {
        if (body == null || strip == null || strip.Length == 0) return;
        body.sprite = strip[((i % strip.Length) + strip.Length) % strip.Length];
    }

    private static bool PlayerAvailable()
    {
        PlayerController player = PlayerController.Instance;
        return player != null && player.gameObject.activeSelf;
    }

    private void ClearMarker()
    {
        if (marker != null) marker.Cancel();
        marker = null;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer MilchpanzerBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Collider2D collider,
                           Sprite[] driveStrip, Sprite[] strawStrip, Sprite[] suckStrip, Sprite[] burpStrip,
                           Sprite[] dropletStrip, Sprite[] splashStrip)
    {
        body = bodyRenderer;
        hitbox = collider;
        drive = driveStrip;
        strawOut = strawStrip;
        suck = suckStrip;
        burp = burpStrip;
        droplet = dropletStrip;
        splash = splashStrip;
    }
#endif
}
