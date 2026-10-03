using System.Collections;
using UnityEngine;

/// <summary>
/// Der Schleimkoenig - Zwischenboss (Rolle Zwischenboss = EnemyRole.MiniBoss).
/// Ein grosser lila Schleim mit Krone und Zepter. Bilder aus
/// Tools/schleimkoenig.py, das Prefab baut Tools -> Gegner -> Schleimkoenig
/// bauen (SchleimkoenigBuilder).
///
///   Hopsen     wie der Kirschslime: ducken, abspringen, landen. Nur in der
///              Luft kommt er vom Fleck - im Schnitt so schnell wie das Tempo
///              im Katalog. Die Krone federt nach, das Zepter wippt mit.
///
///   Riesensprung  alle paar Hopser: er duckt sich tief und schwingt das
///              Zepter (das Funkeln ist die Ansage), schnellt aus dem Bild,
///              und auf dem Spieler erscheint die rote Zone - mit seinem
///              Schatten darin, der waechst, waehrend er herunterfaellt. Wer
///              beim Aufprall drinsteht, bekommt ein Vielfaches seines
///              Beruehrungsschadens. Die Kamera wackelt, Schleim spritzt.
///
///   Ab der Haelfte seiner Leben springt er oefter und die Zone ist kuerzer.
///
/// Drei Sachen sind Absicht:
///   1. Die Zone liegt FEST dort, wo der Spieler beim Absprung-Ende stand.
///      Sie zielt nicht nach - wer rausgeht, ist sicher.
///   2. In der Luft ist er nicht zu treffen und hat keinen Koerper
///      (<see cref="Enemy.Untouchable"/>, Collider aus). Sonst wuerden
///      Waffen in einen unsichtbaren Gegner ueber dem Bildrand feuern.
///   3. Nach der Landung wabbelt er eine Sekunde am Platz - das Fenster zum
///      Draufhauen.
///
/// Animation: kein Animator. Die Bildstreifen enthalten nur die Verformung,
/// die Hoehe ueber dem Boden setzt dieses Skript (ganze Pixel) - so bleibt
/// der Schatten am Boden und der Sprung kann nahtlos aus dem Bild fliegen.
/// Die Hoehentabellen unten kommen aus dem Bildskript und muessen zu den
/// Bildern passen.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Enemy))]
public class EnemySchleimkoenig : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float PhaseTwoAt = 0.5f;

    /// <summary>So viele Hopser liegen zwischen zwei Riesenspruengen (min, max inklusive).</summary>
    private const int JumpEveryMin = 3, JumpEveryMax = 4;
    private const int JumpEveryMinPhase2 = 2, JumpEveryMaxPhase2 = 3;

    /// <summary>Ducken vor dem Riesensprung. Der Streifen wird auf diese Zeit gestreckt.</summary>
    private const float DuckTime = 0.9f;
    private const float DuckTimePhase2 = 0.7f;

    /// <summary>So lange liegt die rote Zone, bis er einschlaegt.</summary>
    private const float ZoneTime = 1.2f;
    private const float ZoneTimePhase2 = 1.0f;

    /// <summary>Am Ende der Zone faellt er sichtbar herunter (Teil von ZoneTime).</summary>
    private const float FallTime = 0.35f;

    /// <summary>Radius der Zone (Einheiten). Er selbst ist gut 1.3 breit (halb).</summary>
    private const float ZoneRadius = 2.0f;

    /// <summary>Spielerkoerper, der noch mit drinsteht (halber Spieler).</summary>
    private const float PlayerRadius = 0.25f;

    /// <summary>Aufprallschaden als Vielfaches des Beruehrungsschadens.</summary>
    private const float LandingDamageFactor = 2.5f;

    /// <summary>So hoch fliegt er (Einheiten) - sicher ueber jedem Bildrand.</summary>
    private const float FlyHeight = 12f;

    /// <summary>
    /// Von hier faellt er herunter: knapp ueber dem oberen Bildrand (halbe
    /// Bildhoehe gut 4 + seine eigene Hoehe). Aus FlyHeight waere er nur ein
    /// einziges Bild lang zu sehen.
    /// </summary>
    private const float FallFrom = 8f;

    /// <summary>Wie schnell er nach dem Absprung weiter beschleunigt (Einheiten/s²).</summary>
    private const float LaunchAcceleration = 70f;

    /// <summary>Kamerawackeln bei der Landung: Pixel, Sekunden.</summary>
    private const float ShakePixels = 5f, ShakeSeconds = 0.45f;

    /// <summary>Jeder dritte Hopser blinzelt.</summary>
    private const int BlinkEvery = 3;

    // ------------------------------------------------- Tabellen aus dem Bildskript

    private const float Fps = 12f;
    private const float PixelsPerUnit = 32f;

    /// <summary>Hoehe (Pixel) je Bild des Hopsers.</summary>
    private static readonly int[] HopLift = { 0, 0, 0, 0, 3, 11, 16, 18, 16, 10, 3, 0, 0, 0 };

    /// <summary>In diesen Bildern ist er in der Luft und kommt vom Fleck.</summary>
    private const int HopAirFirst = 4, HopAirLast = 10;

    /// <summary>Hoehe (Pixel) je Bild des Absprungs. Danach fliegt der Code weiter.</summary>
    private static readonly int[] LaunchLift = { 4, 16, 36, 62 };

    /// <summary>Ab diesem Absprungbild ist er weg vom Boden (unverwundbar, kein Koerper).</summary>
    private const int LaunchAirborneFrame = 1;

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private SpriteRenderer splashRenderer;
    [SerializeField] private Collider2D hitbox;

    [SerializeField] private Sprite[] hop;
    [SerializeField] private Sprite[] hopBlink;
    [SerializeField] private Sprite[] duck;
    [SerializeField] private Sprite[] launch;
    [SerializeField] private Sprite[] fall;
    [SerializeField] private Sprite[] land;
    [SerializeField] private Sprite[] splash;

    /// <summary>Schatten gross -> klein.</summary>
    [SerializeField] private Sprite[] shadow;

    // ---------------------------------------------------------------- Zustand

    private Enemy enemy;
    private Rigidbody2D rb;

    private bool moving;
    private Vector2 hopVelocity;

    private BossTelegraphMarker marker;

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

        if (splashRenderer != null) splashRenderer.enabled = false;
        SetLift(0f);
    }

    private void OnEnable()
    {
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearMarker();
        moving = false;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        rb.linearVelocity = moving ? hopVelocity : Vector2.zero;
    }

    private IEnumerator Run()
    {
        // Erst ein Hopser zum Ankommen, dann die Zaehlung.
        int untilJump = Random.Range(JumpEveryMin, JumpEveryMax + 1);
        int hops = 0;

        while (true)
        {
            if (untilJump <= 0 && PlayerAvailable())
            {
                yield return BigJump();
                untilJump = IsPhaseTwo
                    ? Random.Range(JumpEveryMinPhase2, JumpEveryMaxPhase2 + 1)
                    : Random.Range(JumpEveryMin, JumpEveryMax + 1);
                continue;
            }

            hops++;
            yield return Hop(hops % BlinkEvery == 0);
            untilJump--;
        }
    }

    // ---------------------------------------------------------------- Hopser

    private IEnumerator Hop(bool blink)
    {
        Sprite[] strip = blink && hopBlink != null && hopBlink.Length == hop.Length ? hopBlink : hop;
        float frameTime = 1f / Fps;

        for (int i = 0; i < strip.Length; i++)
        {
            if (i == HopAirFirst) BeginHopMove(strip.Length);
            if (i == HopAirLast + 1) moving = false;

            Show(strip, i);
            SetLift(i < HopLift.Length ? HopLift[i] / PixelsPerUnit : 0f);
            yield return new WaitForSeconds(frameTime);
        }
        moving = false;
    }

    /// <summary>
    /// Richtung beim Abheben festlegen. Er springt dorthin, wo der Spieler
    /// JETZT steht - in der Luft lenkt er nicht nach. Die Luftgeschwindigkeit
    /// ist so gewaehlt, dass er im Schnitt das Katalog-Tempo laeuft.
    /// </summary>
    private void BeginHopMove(int frameCount)
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf)
        {
            moving = false;
            return;
        }

        Vector2 to = (Vector2)player.transform.position - rb.position;
        float airFrames = HopAirLast - HopAirFirst + 1;
        float airSpeed = enemy.CurrentSpeed * frameCount / airFrames;

        // Nicht ueber den Spieler hinausspringen.
        float maxDistance = to.magnitude;
        float jump = airSpeed * airFrames / Fps;
        if (jump > maxDistance) airSpeed *= maxDistance / Mathf.Max(jump, 0.0001f);

        hopVelocity = to.sqrMagnitude > 0.0001f ? to.normalized * airSpeed : Vector2.zero;
        moving = true;
    }

    // ----------------------------------------------------------- Riesensprung

    private IEnumerator BigJump()
    {
        moving = false;
        bool phaseTwo = IsPhaseTwo;

        // 1. Ducken - auf DuckTime gestreckt
        yield return PlayStretched(duck, phaseTwo ? DuckTimePhase2 : DuckTime);

        // 2. Absprung
        float frameTime = 1f / Fps;
        for (int i = 0; i < launch.Length; i++)
        {
            if (i == LaunchAirborneFrame) SetAirborne(true);
            Show(launch, i);
            SetLift(LaunchLift[Mathf.Min(i, LaunchLift.Length - 1)] / PixelsPerUnit);
            yield return new WaitForSeconds(frameTime);
        }

        // 3. Weiter hoch, immer schneller, bis weit ueber den Bildrand
        int n = LaunchLift.Length;
        float lift = LaunchLift[n - 1] / PixelsPerUnit;
        float speed = (LaunchLift[n - 1] - LaunchLift[n - 2]) * Fps / PixelsPerUnit;
        while (lift < FlyHeight)
        {
            speed += LaunchAcceleration * Time.deltaTime;
            lift += speed * Time.deltaTime;
            SetLift(lift);
            yield return null;
        }

        // 4. Oben: rote Zone auf den Spieler, er zieht mit dahin
        Vector2 target = PlayerAvailable()
            ? (Vector2)PlayerController.Instance.transform.position
            : rb.position;
        rb.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        body.enabled = false;

        float zoneTime = phaseTwo ? ZoneTimePhase2 : ZoneTime;
        marker = BossTelegraph.Zone(target, ZoneRadius);

        float t = 0f;
        float fallStart = zoneTime - FallTime;
        while (t < zoneTime)
        {
            t += Time.deltaTime;
            if (marker != null) marker.SetProgress(Mathf.Clamp01(t / zoneTime));

            if (t >= fallStart)
            {
                // Faellt mit zunehmendem Tempo (ease-in) genau zum Ende der Zone
                float k = Mathf.Clamp01((t - fallStart) / FallTime);
                body.enabled = true;
                Show(fall, Mathf.FloorToInt(t * Fps) % Mathf.Max(1, fall.Length));
                SetLift(FallFrom * (1f - Mathf.Pow(k, 1.6f)));
            }
            else
            {
                SetLift(FlyHeight);
            }

            // Der Schatten waechst ueber die ganze Warnzeit - "da kommt was".
            // Nicht nach der Hoehe: oben ueber dem Bildrand waere er sonst
            // die ganze Zeit winzig.
            SetShadow(Mathf.Clamp01(t / zoneTime));
            yield return null;
        }

        // 5. Aufprall
        SetLift(0f);
        body.enabled = true;
        if (marker != null) marker.Impact();
        marker = null;
        SetAirborne(false);

        HitPlayer(target);
        ScreenShake.Kick(ShakePixels, ShakeSeconds);
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.EarthHit);
        }

        if (splashRenderer != null && splash != null && splash.Length > 0) StartCoroutine(PlaySplash());

        // 6. Aufschlagen und auswabbeln - steht dabei still
        for (int i = 0; i < land.Length; i++)
        {
            Show(land, i);
            yield return new WaitForSeconds(frameTime);
        }
    }

    private void HitPlayer(Vector2 center)
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf) return;

        float reach = ZoneRadius + PlayerRadius;
        if (((Vector2)player.transform.position - center).sqrMagnitude > reach * reach) return;

        player.TakeDamage(enemy.ContactDamage * LandingDamageFactor);
    }

    private IEnumerator PlaySplash()
    {
        splashRenderer.enabled = true;
        float frameTime = 1f / Fps;
        for (int i = 0; i < splash.Length; i++)
        {
            splashRenderer.sprite = splash[i];
            yield return new WaitForSeconds(frameTime);
        }
        splashRenderer.enabled = false;
    }

    /// <summary>Spielt einen Streifen in genau <paramref name="seconds"/> ab.</summary>
    private IEnumerator PlayStretched(Sprite[] strip, float seconds)
    {
        if (strip == null || strip.Length == 0) yield break;

        SetLift(0f);
        float t = 0f;
        while (t < seconds)
        {
            int i = Mathf.Min(strip.Length - 1, Mathf.FloorToInt(t / seconds * strip.Length));
            Show(strip, i);
            t += Time.deltaTime;
            yield return null;
        }
        Show(strip, strip.Length - 1);
    }

    // ---------------------------------------------------------------- Kleinkram

    private void Show(Sprite[] strip, int i)
    {
        if (body == null || strip == null || strip.Length == 0) return;
        body.sprite = strip[Mathf.Clamp(i, 0, strip.Length - 1)];
    }

    /// <summary>
    /// Hoehe ueber dem Boden (Einheiten), auf ganze Pixel gerundet. Der
    /// Schatten bleibt am Boden und wird mit der Hoehe kleiner.
    /// </summary>
    private void SetLift(float units)
    {
        float px = Mathf.Max(0f, Mathf.Floor(units * PixelsPerUnit + 0.5f));
        if (body != null) body.transform.localPosition = new Vector3(0f, px / PixelsPerUnit, 0f);

        if (shadowRenderer != null && shadow != null && shadow.Length > 0)
        {
            // Schnell kleiner in Bodennaehe, oben kaum noch: 1 - e^(-h/90)
            float k = 1f - Mathf.Exp(-px / 90f);
            int i = Mathf.Clamp(Mathf.FloorToInt(k * shadow.Length), 0, shadow.Length - 1);
            shadowRenderer.sprite = shadow[i];
        }
    }

    /// <summary>Schatten nach Anteil: 0 = kleinster, 1 = voller Schatten.</summary>
    private void SetShadow(float fraction)
    {
        if (shadowRenderer == null || shadow == null || shadow.Length == 0) return;
        int i = Mathf.Clamp(Mathf.FloorToInt((1f - fraction) * shadow.Length), 0, shadow.Length - 1);
        shadowRenderer.sprite = shadow[i];
    }

    private void SetAirborne(bool air)
    {
        enemy.Untouchable = air;
        if (hitbox != null) hitbox.enabled = !air;
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
    /// <summary>Nur fuer SchleimkoenigBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, SpriteRenderer shadowR, SpriteRenderer splashR,
                           Collider2D collider,
                           Sprite[] hopStrip, Sprite[] hopBlinkStrip, Sprite[] duckStrip, Sprite[] launchStrip,
                           Sprite[] fallStrip, Sprite[] landStrip, Sprite[] splashStrip, Sprite[] shadowStrip)
    {
        body = bodyRenderer;
        shadowRenderer = shadowR;
        splashRenderer = splashR;
        hitbox = collider;
        hop = hopStrip;
        hopBlink = hopBlinkStrip;
        duck = duckStrip;
        launch = launchStrip;
        fall = fallStrip;
        land = landStrip;
        splash = splashStrip;
        shadow = shadowStrip;
    }
#endif
}
