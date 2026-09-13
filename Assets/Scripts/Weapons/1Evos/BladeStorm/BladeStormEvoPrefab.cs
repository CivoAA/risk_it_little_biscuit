using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Eine einzelne Klinge der Blade Storm Evo.
///
/// Der Flug läuft bewusst als Zustandsautomat in FixedUpdate und NICHT in
/// Coroutines auf der Waffe:
///
/// * Coroutines sterben still, sobald das Waffen-Objekt deaktiviert wird. Die
///   Klingen blieben dann für immer im Flugzustand hängen – und da die Waffe
///   nur feuert, wenn alle Klingen im Orbit sind, hat sie danach nie wieder
///   geschossen.
/// * In FixedUpdate passt die Bewegung zu den Physikschritten, und der
///   zurückgelegte Weg kann per BoxCast geprüft werden. Sonst tunneln die
///   Klingen bei Tempo 20 durch Gegner hindurch, ohne je einen Trigger auszulösen.
/// </summary>
public class BladeStormEvoPrefab : MonoBehaviour
{
    public enum BladeState { Orbit, Outbound, Inbound }

    public BladeStormEvo weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private const float OutboundSpeed = 20f;
    private const float InboundDuration = 0.5f;  // entspricht dem alten returnSpeed 2
    private const float OutboundTimeout = 2f;
    private const float InboundTimeout = 2.5f;
    private const float MaxRange = 12f;          // ab Abschussort, nicht ab Spieler
    private const float ArriveDistance = 0.15f;

    public BladeState State { get; private set; }
    public bool InOrbit { get { return State == BladeState.Orbit; } }

    private BoxCollider2D hitBox;
    private Animator playerAnimator;
    private Transform owner;

    private Vector2 aimDirection;     // Richtung beim Abschuss, bleibt stabil
    private Vector2 launchOrigin;
    private Vector2 bezierStart;
    private Vector2 bezierControl;
    private float stateTimer;
    private float inboundT;
    private Vector2 lastCenter;

    // Pro Flugabschnitt darf jeder Gegner einmal getroffen werden. Hin- und
    // Rückflug sind getrennte Abschnitte – wie vorher über Enter/Exit/Enter.
    private readonly HashSet<Enemy> hitThisPass = new HashSet<Enemy>();

    void Awake()
    {
        State = BladeState.Orbit;
        hitBox = GetComponent<BoxCollider2D>();
        owner = transform.parent;
        if (enemiesInRange == null) enemiesInRange = new List<Enemy>();
    }

    void Start()
    {
        // Die Waffe setzt das beim Spawn direkt; der Find ist nur der Fallback.
        if (weapon == null) weapon = WeaponFinder.Find<BladeStormEvo>("Blade Storm Evo");

        GameObject hitbox = GameObject.FindWithTag("PlayerHitbox");
        if (hitbox != null) playerAnimator = hitbox.GetComponent<Animator>();
    }

    /// <summary>Schickt die Klinge auf den Weg. Wird von der Waffe aufgerufen.</summary>
    public void Launch(Transform bladeOwner, Vector2 direction)
    {
        if (!InOrbit) return;

        owner = bladeOwner;
        aimDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        launchOrigin = transform.position;

        transform.SetParent(null);
        EnterState(BladeState.Outbound);
    }

    void Update()
    {
        // Im Orbit zeigt die Klinge in Laufrichtung des Spielers. Im Flug
        // bestimmt FixedUpdate die Rotation - deshalb hier nur der Orbit-Fall.
        // (Vorher hat zusaetzlich PositionBlades() der Waffe jeden Frame eine
        // feste Rotation gesetzt, was sich mit dieser hier gebissen hat.)
        if (InOrbit) ApplyOrbitRotation();
    }

    void FixedUpdate()
    {
        if (InOrbit) return;

        stateTimer += Time.fixedDeltaTime;

        if (State == BladeState.Outbound) StepOutbound();
        else StepInbound();

        DamageSweptEnemies();
    }

    private void StepOutbound()
    {
        Vector2 position = transform.position;

        // Ziel lebt → nachführen. Ziel tot → stur in Abschussrichtung weiter.
        Vector2 aimPoint = (weapon != null && weapon.HasTarget)
            ? weapon.TargetPosition + aimDirection * 1.5f
            : position + aimDirection * 5f;

        Vector2 dir = aimPoint - position;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : aimDirection;

        // Nicht am Ziel vorbeischiessen: bei Tempo 20 sind das 0.4 Einheiten pro
        // Physikschritt. Mit einer festen Ankunftsschwelle von 0.15 hat die Klinge
        // das Ziel nie "erreicht", sondern ist davor hin und her gezappelt, bis
        // der 2s-Timeout griff - sichtbar als Haengenbleiben am Gegner.
        float step = OutboundSpeed * Time.fixedDeltaTime;
        float remaining = Vector2.Distance(position, aimPoint);
        bool arrived = remaining <= Mathf.Max(step, ArriveDistance);

        position = arrived ? aimPoint : position + dir * step;
        transform.position = position;
        FaceDirection(dir);

        // Reichweite ab dem Abschussort messen, nicht ab dem Spieler. Sonst
        // beendet Weglaufen den Hinflug vorzeitig und verkürzt die Reichweite.
        bool outOfRange = Vector2.Distance(position, launchOrigin) > MaxRange;

        if (arrived || outOfRange || stateTimer > OutboundTimeout)
        {
            bezierStart = position;
            bezierControl = position + aimDirection * 8f;
            EnterState(BladeState.Inbound);
        }
    }

    private void StepInbound()
    {
        // Endpunkt jeden Schritt neu vom Spieler holen. Vorher war er beim
        // Start des Rückflugs eingefroren – bei schneller Bewegung endete die
        // Kurve im Leeren und die Klinge sprang danach sichtbar in den Orbit.
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 end = owner.position;
        inboundT += Time.fixedDeltaTime / InboundDuration;

        Vector2 position = Bezier(bezierStart, bezierControl, end, Mathf.Min(inboundT, 1f));
        transform.position = position;
        FaceDirection(end - position);

        if (inboundT >= 1f || stateTimer > InboundTimeout)
        {
            ReturnToOrbit();
        }
    }

    private void ReturnToOrbit()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.SetParent(owner, true);
        EnterState(BladeState.Orbit);
        ApplyOrbitRotation();

        if (weapon != null) weapon.PositionOrbitBlades();
    }

    /// <summary>
    /// Ausrichtung im Orbit: nach links, sobald der Spieler zuletzt nach links
    /// gelaufen ist, sonst nach rechts. Ohne den Standardfall haetten frisch
    /// gespawnte Klingen ihre Instantiate-Rotation behalten und mit der Spitze
    /// nach oben gezeigt, bis der Spieler das erste Mal seitwaerts laeuft.
    /// </summary>
    private void ApplyOrbitRotation()
    {
        float lastMoveX = playerAnimator != null ? playerAnimator.GetFloat("LastMoveX") : 0f;
        float angle = lastMoveX > 0.1f ? -90f : 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void EnterState(BladeState next)
    {
        State = next;
        stateTimer = 0f;
        inboundT = 0f;
        hitThisPass.Clear();
        if (hitBox != null) lastCenter = OverlapDamage.WorldCenter(hitBox);
    }

    private void FaceDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void DamageSweptEnemies()
    {
        if (hitBox == null) return;

        OverlapDamage.SweepEnemies(hitBox, lastCenter, enemiesInRange);
        lastCenter = OverlapDamage.WorldCenter(hitBox);

        for (int i = 0; i < enemiesInRange.Count; i++)
        {
            DealDamage(enemiesInRange[i]);
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;
        DealDamage(collider.GetComponent<Enemy>());
    }

    private void DealDamage(Enemy enemy)
    {
        if (enemy == null) return;
        if (weapon == null || !weapon.IsActive) return;

        // Im Orbit wie bisher bei jedem Eintritt; im Flug einmal pro Abschnitt.
        if (!InOrbit && !hitThisPass.Add(enemy)) return;

        enemy.TakeDamage(weapon.CurrentStats.damage);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        Vector2 ab = Vector2.Lerp(a, b, t);
        Vector2 bc = Vector2.Lerp(b, c, t);
        return Vector2.Lerp(ab, bc, t);
    }
}
