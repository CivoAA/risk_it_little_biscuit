using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gemeinsame Basis fuer die Projektile von Begleiter und Turret.
///
/// Zwei Dinge, die das alte "geradeaus fliegen und auf OnTriggerEnter2D
/// warten" nicht konnte und die zusammen die Fehlschuesse verursacht haben:
///
/// 1. Nachfuehrung: der Vorhalt beim Abschuss (siehe
///    <see cref="Aim.PredictDirection"/>) trifft nur, solange der Gegner
///    geradeaus laeuft. Er laeuft aber dem Spieler hinterher und dreht damit
///    staendig. Deshalb korrigiert das Projektil seinen Kurs unterwegs - mit
///    begrenzter Drehrate, sonst kreist es um den Gegner herum statt ihn zu
///    durchschlagen.
///
/// 2. Durchgehende Trefferpruefung: statt auf einen Trigger zu warten wird
///    die Strecke eines Frames abgetastet. Bei 12 Einheiten/s und einem
///    Collider-Radius von 0.12 rutscht das Projektil sonst bei jedem Ruckler
///    komplett durch den Gegner hindurch.
/// </summary>
public abstract class TrackingProjectile : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float lifeTime = 2.5f;

    [Tooltip("Grad pro Sekunde, die das Projektil nachdrehen darf. 0 = stur geradeaus.")]
    [SerializeField] private float turnSpeed = 260f;

    [Tooltip("Trefferradius der Abtastung. Darf grosszuegiger sein als der Collider.")]
    [SerializeField] private float hitRadius = 0.25f;

    private Enemy target;
    private Vector2 direction = Vector2.right;

    private static readonly List<RaycastHit2D> castBuffer = new List<RaycastHit2D>();
    private static ContactFilter2D filter;
    private static bool filterReady;

    /// <summary>Damit der Schuetze den Vorhalt rechnen kann, bevor er spawnt.</summary>
    public float MoveSpeed { get { return moveSpeed; } }

    private static ContactFilter2D Filter
    {
        get
        {
            if (!filterReady)
            {
                filter = ContactFilter2D.noFilter;
                filter.useTriggers = true;
                filterReady = true;
            }

            return filter;
        }
    }

    /// <summary>
    /// Vom Schuetzen direkt nach dem Spawn aufgerufen.
    /// <paramref name="lockedTarget"/> darf null sein - dann fliegt das
    /// Projektil ohne Nachfuehrung geradeaus.
    /// </summary>
    protected void Launch(Vector2 flyDirection, Enemy lockedTarget)
    {
        direction = flyDirection.sqrMagnitude > 0.0001f ? flyDirection.normalized : Vector2.right;
        target = lockedTarget;
        FaceDirection();
    }

    void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Steer();

        float step = moveSpeed * Time.deltaTime;
        if (TryHitAlongPath(step)) return;

        transform.position += (Vector3)(direction * step);
    }

    private void Steer()
    {
        // Ziel tot oder nie gesetzt: aktuellen Kurs beibehalten.
        if (target == null || turnSpeed <= 0f) return;

        Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        direction = Vector3.RotateTowards(direction, toTarget.normalized,
            turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        FaceDirection();
    }

    private bool TryHitAlongPath(float step)
    {
        castBuffer.Clear();
        Physics2D.CircleCast(transform.position, hitRadius, direction, Filter, castBuffer, step);

        foreach (RaycastHit2D hit in castBuffer)
        {
            // Das eigene Projektil ist Untagged und faellt hier mit heraus,
            // obwohl Physics2D "Queries Start In Colliders" aktiv hat.
            if (hit.collider == null || !hit.collider.CompareTag("Enemy")) continue;

            Enemy enemy = hit.collider.GetComponent<Enemy>();
            if (enemy == null) continue;

            ApplyDamage(enemy);
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private void FaceDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// Schaden des jeweiligen Schuetzen. Wird nur bei einem Treffer gerufen;
    /// ob der Schuetze noch existiert, prueft der Erbe selbst.
    /// </summary>
    protected abstract void ApplyDamage(Enemy enemy);
}
