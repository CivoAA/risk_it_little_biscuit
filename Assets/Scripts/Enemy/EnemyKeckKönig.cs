using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Keks-Koenig - Endboss der Kueche (World0) und zurzeit auch das Finale
/// jeder anderen Karte, siehe <see cref="WavePlans"/>.
///
/// Er kaempft in zwei Phasen:
///
///   Phase 1 - ueber der Haelfte seiner Leben
///     Keks-Regen  : bleibt stehen, markiert mehrere Zonen rund um den Spieler
///                   und laesst dort Kekse einschlagen.
///     Keks-Charge : bleibt stehen, markiert eine Bahn und den Einschlagpunkt
///                   rot und sprintet dann einmal durch.
///     Welche der beiden kommt, wird gewuerfelt - aber hoechstens dreimal
///     dieselbe hintereinander, danach kommt zwingend die andere.
///
///   Phase 2 - ab der Haelfte
///     Der Regen bleibt unveraendert - der traegt sich allein.
///     Aus der einen Charge werden drei am Stueck, jede mit eigener, kuerzerer
///     Vorwarnung und neu gesetztem Ziel.
///
/// Drei Sachen sind Absicht und sollten beim Balancing nicht verloren gehen:
///
///   1. Das Ziel der Charge friert VOR dem Losrennen ein, siehe
///      <see cref="ChargeAimLock"/>. Wuerde er bis zum letzten Moment
///      nachzielen, waere der Angriff nicht ausweichbar, sondern nur noch
///      aussitzbar - und die rote Bahn waere eine Luege.
///   2. Die Zeit ab Aim-Lock muss laenger sein als der Weg aus der Bahn
///      heraus. Faustregel: halbe Bahnbreite geteilt durch Spielertempo (4).
///      Bei Breite 5.5 sind das rund 0.7s, deshalb steht der Aim-Lock auf
///      0.85s. Wer die Bahn breiter macht, muss ihn mitziehen.
///   3. Nach jeder Attacke steht er kurz still. Das ist das Fenster, in dem
///      der Spieler Schaden macht - ohne das ist der Kampf ein reiner
///      Ausweich-Marathon, bei dem man nie zum Schiessen kommt.
///
/// Die Zahlen stehen als Konstanten im Code statt im Inspector: sonst liegt am
/// Prefab eine zweite Wahrheit daneben, und nach jedem Prefab-Neubau waere das
/// Balancing wieder weg. Das ist dieselbe Linie wie bei den Wellenplaenen.
///
/// Animation: der Controller muss nichts koennen. Gibt es die Trigger
/// "Charge", "ChargeGo", "Throw" oder "PhaseTwo", werden sie gefeuert; gibt es
/// sie nicht, passiert einfach nichts (siehe <see cref="Signal"/>). Bis dahin
/// telegrafiert er ueber Zusammenziehen, Zittern und Rotfaerbung.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Enemy))]
public class EnemyKeckKönig : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    /// <summary>Ab wie viel Restleben Phase 2 laeuft.</summary>
    private const float PhaseTwoAt = 0.5f;

    private const float WalkSpeed = 3f;

    /// <summary>
    /// Knapp unter dem Spielertempo (4). Absichtlich nicht darueber: er soll
    /// draengen, aber Weglaufen darf nie voellig sinnlos werden.
    /// </summary>
    private const float WalkSpeedPhase2 = 3.8f;

    /// <summary>Laufen, bevor er ueberhaupt das erste Mal angreift.</summary>
    private const float EntryWalk = 2f;

    /// <summary>Pause zwischen zwei Attacken - das Fenster zum Draufhauen.</summary>
    private const float AttackPause = 4.5f;
    private const float AttackPausePhase2 = 3f;

    private const float PhaseTwoRoar = 1.1f;

    /// <summary>
    /// Wie oft dieselbe Attacke hoechstens hintereinander kommen darf.
    /// Danach kommt zwingend die andere.
    /// </summary>
    private const int MaxSameInARow = 3;

    // --- Charge
    private const float ChargeWindup = 1.3f;
    private const float ChargeWindupChain = 0.95f;
    private const float ChargeAimLock = 0.85f;
    private const float ChargeSpeed = 26f;

    /// <summary>So breit wie sein Koerper - die Bahn soll nicht luegen.</summary>
    private const float ChargeWidth = 5.5f;

    private const float ChargeOvershoot = 4f;
    private const float ChargeMinLength = 7f;
    private const float ChargeMaxLength = 18f;

    /// <summary>Notbremse, falls er unterwegs an etwas haengen bleibt.</summary>
    private const float ChargeMaxTime = 1.6f;

    private const float ChargeDamage = 5f;
    private const float ChargeImpactRadius = 3.2f;
    private const float ChargeImpactDamage = 3f;
    private const float ChargeRecover = 0.7f;
    private const float ChargeTrailFade = 0.35f;

    private const int ChargeChain = 3;
    private const float ChargeChainGap = 0.3f;

    // --- Keks-Regen
    private const int RainCount = 5;
    private const float RainWindup = 1.25f;
    private const float RainRadius = 2f;
    private const float RainSpread = 5.5f;

    /// <summary>
    /// Mindestabstand zweier Zonen. Ohne den wachsen sie zu einer Wand
    /// zusammen - und genau die Luecke dazwischen IST die Attacke.
    /// </summary>
    private const float RainMinGap = RainRadius * 1.7f;

    private const float RainStagger = 0.14f;
    private const float RainDamage = 4f;
    private const float RainRecover = 0.8f;

    /// <summary>Grober Spielerradius fuer die Treffertests.</summary>
    private const float PlayerRadius = 0.45f;

    private static readonly Color WindupColor = new Color(1f, 0.55f, 0.5f, 1f);
    private static readonly Color RageColor = new Color(1f, 0.35f, 0.3f, 1f);

    // ---------------------------------------------------------------- Zustand

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Enemy enemy;
    private Animator animator;
    private HashSet<string> triggers;

    private Vector3 baseScale;
    private Color baseColor;
    private bool phaseTwo;

    private readonly List<BossTelegraphMarker> live = new List<BossTelegraphMarker>();

    /// <summary>Laeuft schon die zweite Phase? Zeigt die Test-Szene an.</summary>
    public bool IsPhaseTwo
    {
        get { return phaseTwo; }
    }

    // ------------------------------------------------------------------ Start

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        enemy = GetComponent<Enemy>();
        animator = GetComponent<Animator>();

        baseScale = transform.localScale;
        baseColor = sprite.color;

        CollectTriggers();
        StartCoroutine(Brain());
    }

    private void OnDisable()
    {
        // Stirbt er mitten in einer Vorwarnung, ist die Coroutine weg - die
        // Markierungen sind aber eigene GameObjects und blieben sonst als rote
        // Flecken liegen, in denen nie etwas einschlaegt. Danach glaubt einem
        // der Spieler keine Warnung mehr.
        foreach (BossTelegraphMarker marker in live)
        {
            if (marker != null) marker.Cancel();
        }
        live.Clear();
    }

    // ------------------------------------------------------------------ Kopf

    private IEnumerator Brain()
    {
        // Erst ankommen: der Director setzt ihn ausserhalb des Bildes ab, und
        // wer sofort chargt, chargt aus dem Nichts.
        yield return Walk(EntryWalk);

        // Der Regen faengt an. Er bringt dem Spieler die Lesart
        // "Fuellung voll = Einschlag" bei, ohne ihn dafuer zu ueberfahren -
        // die Charge baut dann auf derselben Sprache auf.
        bool charge = false;
        int sameInARow = 0;

        while (true)
        {
            if (!phaseTwo && enemy.HealthFraction <= PhaseTwoAt)
            {
                yield return EnterPhaseTwo();
            }

            yield return charge ? ChargeAttack() : RainAttack();
            sameInARow++;

            yield return Walk(phaseTwo ? AttackPausePhase2 : AttackPause);

            // Gewuerfelt statt stur abgewechselt: wer den Rhythmus einmal raus
            // hat, laeuft den Kampf blind. Die Bremse danach verhindert die
            // andere Richtung - ohne sie kaeme irgendwann die fuenfte Charge am
            // Stueck, und das ist kein Kampf mehr, sondern Pech.
            bool next = sameInARow >= MaxSameInARow ? !charge : Random.value < 0.5f;

            if (next != charge) sameInARow = 0;
            charge = next;
        }
    }

    private IEnumerator EnterPhaseTwo()
    {
        phaseTwo = true;
        Signal("PhaseTwo");

        if (SpawnDirector.Active != null) SpawnDirector.Active.Say("DER KOENIG WIRD WUETEND!");
        if (DamageNumberController.Instance != null)
        {
            DamageNumberController.Instance.CreateText("PHASE 2!", transform.position);
        }

        // Kurz aufplustern statt zusammenziehen: das ist ausdruecklich kein
        // Angriff, und der Spieler soll den Wechsel sehen, bevor ihn die erste
        // Dreierkette trifft.
        float t = 0f;
        while (t < PhaseTwoRoar)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            float swell = Mathf.Sin(t / PhaseTwoRoar * Mathf.PI);
            transform.localScale = baseScale * (1f + 0.12f * swell);
            sprite.color = Color.Lerp(baseColor, RageColor, swell);
            yield return null;
        }
    }

    private IEnumerator Walk(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;

            if (PlayerAlive) MoveToward(PlayerPos, phaseTwo ? WalkSpeedPhase2 : WalkSpeed);
            else rb.linearVelocity = Vector2.zero;

            Relax();
            yield return null;
        }
    }

    // ---------------------------------------------------------- Keks-Charge

    private IEnumerator ChargeAttack()
    {
        int times = phaseTwo ? ChargeChain : 1;

        for (int i = 0; i < times; i++)
        {
            if (!PlayerAlive) yield break;

            // Die erste Charge bekommt immer die volle Vorwarnung, erst die
            // Wiederholungen ziehen an. So bleibt die Kette lesbar, statt aus
            // dem Nichts zu kommen.
            float windup = i == 0 ? ChargeWindup : ChargeWindupChain;
            bool last = i == times - 1;

            yield return Charge(windup, last ? ChargeRecover : ChargeChainGap);
        }
    }

    private IEnumerator Charge(float windup, float recover)
    {
        Signal("Charge");

        Vector2 origin = transform.position;
        Vector2 aim = ChargeTarget(origin);

        BossTelegraphMarker path = Track(BossTelegraph.Path(origin, aim, ChargeWidth));
        BossTelegraphMarker spot = Track(BossTelegraph.Zone(aim, ChargeImpactRadius));

        // --- ausholen
        float t = 0f;
        while (t < windup)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            // Solange mehr als der Aim-Lock uebrig ist, zielt er nach. Danach
            // steht die Bahn fest - das ist das Fenster, in dem der Spieler
            // seitlich rauslaufen kann.
            if (windup - t > ChargeAimLock && PlayerAlive)
            {
                origin = transform.position;
                aim = ChargeTarget(origin);
                path.Aim(origin, aim, ChargeWidth);
                spot.MoveTo(aim);
            }

            float progress = t / windup;
            path.SetProgress(progress);
            spot.SetProgress(progress);
            Anticipate(progress);
            yield return null;
        }

        // --- losrennen
        Signal("ChargeGo");

        // Die Bahn bleibt noch kurz als Schleifspur liegen, statt im selben
        // Frame zu verschwinden - sonst sieht es aus, als waere sie weg,
        // bevor er ueberhaupt losgelaufen ist.
        path.Impact(ChargeTrailFade);
        Untrack(path);

        Vector2 direction = aim - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        bool hitAlongPath = false;
        float run = 0f;
        Vector2 previous = transform.position;

        while (run < ChargeMaxTime)
        {
            run += Time.deltaTime;
            rb.linearVelocity = direction * ChargeSpeed;
            Stretch();

            yield return null;

            Vector2 now = transform.position;

            // Die Strecke pruefen, nicht nur die Position: bei 26 Einheiten
            // pro Sekunde liegen zwischen zwei Frames gut 0.4 Einheiten, und
            // ein reiner Abstandstest wuerde den Spieler durchrutschen lassen.
            if (!hitAlongPath && SegmentHitsPlayer(previous, now, ChargeWidth * 0.5f))
            {
                hitAlongPath = true;
                Hit(ChargeDamage);
            }
            previous = now;

            // Ziel erreicht oder daran vorbei
            if (Vector2.Dot(aim - now, direction) <= 0f) break;
        }

        rb.linearVelocity = Vector2.zero;

        // --- Einschlag am Ende der Bahn
        spot.Impact();
        Untrack(spot);

        if (PlayerAlive && Vector2.Distance(PlayerPos, transform.position) <= ChargeImpactRadius + PlayerRadius)
        {
            Hit(ChargeImpactDamage);
        }

        yield return Recover(recover);
    }

    private Vector2 ChargeTarget(Vector2 origin)
    {
        Vector2 direction = PlayerPos - origin;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        // Er rennt ueber den Spieler hinaus. Wer genau auf ihm stehen bleibt,
        // soll nicht damit belohnt werden, dass der Boss davor abbremst.
        float length = Mathf.Clamp(
            Vector2.Distance(PlayerPos, origin) + ChargeOvershoot,
            ChargeMinLength,
            ChargeMaxLength);

        return origin + direction * length;
    }

    // ----------------------------------------------------------- Keks-Regen

    private IEnumerator RainAttack()
    {
        Signal("Throw");
        rb.linearVelocity = Vector2.zero;

        List<Vector2> spots = PickRainSpots(PlayerPos);
        var markers = new List<BossTelegraphMarker>(spots.Count);
        foreach (Vector2 spot in spots)
        {
            markers.Add(Track(BossTelegraph.Zone(spot, RainRadius)));
        }

        int open = markers.Count;
        float t = 0f;

        while (open > 0)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.zero;

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] == null) continue;

                // Versetzt: die Zonen platzen nacheinander statt alle auf
                // einmal. Das gibt dem Spieler eine Reihenfolge zum Lesen,
                // und das Feld wird nie fuer einen Frame komplett toedlich.
                float progress = (t - i * RainStagger) / RainWindup;
                markers[i].SetProgress(progress);
                if (progress < 1f) continue;

                markers[i].Impact();
                Untrack(markers[i]);
                markers[i] = null;
                open--;

                if (PlayerAlive && Vector2.Distance(PlayerPos, spots[i]) <= RainRadius + PlayerRadius)
                {
                    Hit(RainDamage);
                }
            }

            // Erst aufbauen, dann loslassen
            float squash = t < RainWindup
                ? t / RainWindup
                : Mathf.Max(0f, 1f - (t - RainWindup) * 3f);
            Anticipate(squash);

            yield return null;
        }

        yield return Recover(RainRecover);
    }

    private static List<Vector2> PickRainSpots(Vector2 center)
    {
        var spots = new List<Vector2>(RainCount);

        // Die erste Zone liegt fast auf dem Spieler: Stehenbleiben soll immer
        // die falsche Antwort sein.
        spots.Add(center + Random.insideUnitCircle.normalized * (RainRadius * 0.3f));

        int guard = 0;
        while (spots.Count < RainCount && guard++ < 300)
        {
            Vector2 candidate = center + Random.insideUnitCircle * RainSpread;

            bool free = true;
            foreach (Vector2 other in spots)
            {
                if (Vector2.Distance(candidate, other) >= RainMinGap) continue;
                free = false;
                break;
            }

            if (free) spots.Add(candidate);
        }

        return spots;
    }

    // ------------------------------------------------------------- Werkzeug

    /// <summary>Stillstehen - das Fenster, in dem der Spieler Schaden macht.</summary>
    private IEnumerator Recover(float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            Relax();
            yield return null;
        }
    }

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 delta = target - (Vector2)transform.position;
        rb.linearVelocity = delta.sqrMagnitude > 0.04f ? delta.normalized * speed : Vector2.zero;
    }

    private static bool SegmentHitsPlayer(Vector2 from, Vector2 to, float radius)
    {
        if (!PlayerAlive) return false;

        Vector2 player = PlayerPos;
        Vector2 along = to - from;
        float lengthSquared = along.sqrMagnitude;

        float t = lengthSquared < 0.0001f
            ? 0f
            : Mathf.Clamp01(Vector2.Dot(player - from, along) / lengthSquared);

        return Vector2.Distance(player, from + along * t) <= radius + PlayerRadius;
    }

    private void Hit(float damage)
    {
        if (!PlayerAlive) return;

        // Wie bei den Gegnern haengt auch der Boss am Chaos-Regler, sonst
        // waere er in einem harten Lauf ploetzlich der harmloseste Teil.
        PlayerController.Instance.TakeDamage(damage * RunDifficulty.DamageFactor);
    }

    private static bool PlayerAlive
    {
        get
        {
            return PlayerController.Instance != null
                && PlayerController.Instance.gameObject.activeSelf;
        }
    }

    private static Vector2 PlayerPos
    {
        get
        {
            return PlayerController.Instance != null
                ? (Vector2)PlayerController.Instance.transform.position
                : Vector2.zero;
        }
    }

    private BossTelegraphMarker Track(BossTelegraphMarker marker)
    {
        if (marker != null) live.Add(marker);
        return marker;
    }

    private void Untrack(BossTelegraphMarker marker)
    {
        live.Remove(marker);
    }

    // ----------------------------------------------------------- Koerpersprache

    /// <summary>
    /// Ausholen: zusammenziehen und zittern. Auch ohne eigene Animation liest
    /// sich das als "gleich passiert etwas". Kommen spaeter echte Frames dazu,
    /// laeuft das hier einfach mit und faellt nicht auf.
    /// </summary>
    private void Anticipate(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float squash = 0.16f * progress;
        float shiver = Mathf.Sin(Time.time * 48f) * 0.025f * progress;

        transform.localScale = new Vector3(
            baseScale.x * (1f - squash + shiver),
            baseScale.y * (1f + squash * 0.7f),
            baseScale.z);

        sprite.color = Color.Lerp(baseColor, WindupColor, progress);
    }

    private void Stretch()
    {
        transform.localScale = new Vector3(baseScale.x * 1.14f, baseScale.y * 0.88f, baseScale.z);
        sprite.color = WindupColor;
    }

    private void Relax()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 9f);
        sprite.color = Color.Lerp(sprite.color, baseColor, Time.deltaTime * 9f);
    }

    // ------------------------------------------------------------- Animation

    private void CollectTriggers()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        triggers = new HashSet<string>();
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                triggers.Add(parameter.name);
            }
        }
    }

    /// <summary>
    /// Feuert einen Animator-Trigger, falls es ihn gibt. Der Controller hat
    /// heute nur einen State ohne Parameter - statt ihn dafuer umzubauen,
    /// feuern wir nur, was vorhanden ist. Wer spaeter Frames malt, legt die
    /// Trigger an und muss hier nichts aendern.
    /// </summary>
    private void Signal(string trigger)
    {
        if (animator == null || triggers == null || !triggers.Contains(trigger)) return;
        animator.SetTrigger(trigger);
    }
}
