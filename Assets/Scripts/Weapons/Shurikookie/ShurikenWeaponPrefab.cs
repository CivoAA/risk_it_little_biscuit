using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ein geworfener Shurikookie. Fliegt geradeaus und zerbricht am ersten
/// Gegner - mit dem Skilltree "Abpraller" (<see cref="Ricochet"/>) prallt er
/// stattdessen zum naechsten Gegner ab.
///
/// Den Flug macht er selbst (frueher eine Coroutine auf der Waffe), damit er
/// beim Abprallen die Richtung wechseln kann.
/// </summary>
public class ShurikenWeaponPrefab : MonoBehaviour
{
    public ShurikenWeapon weapon;
    public List<Enemy> enemiesInRange;

    /// <summary>Fluggeschwindigkeit (Einheiten pro Sekunde).</summary>
    public const float Speed = 10f;

    /// <summary>So weit fliegt er ab dem Abwurf bzw. ab dem letzten Abpraller.</summary>
    public const float FlightDistance = 10f;

    // Destroy wirkt erst am Ende des Frames. Stehen mehrere Gegner
    // uebereinander, kommen ihre Trigger alle im selben Physikschritt an -
    // ohne den Schalter trafe ein Shuriken jeden davon.
    private bool hasHit;

    private bool launched;
    private Vector2 direction;
    private float travelled;
    private int bouncesLeft;
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    void Start()
    {
        weapon = WeaponFinder.Find<ShurikenWeapon>("Shurikookie");
    }

    /// <summary>Wirft den Stern. Wird von der Waffe direkt nach dem Spawnen aufgerufen.</summary>
    public void Launch(Vector2 dir)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.left;
        bouncesLeft = Ricochet.BouncesForThrow();
        travelled = 0f;
        launched = true;
    }

    void Update()
    {
        if (!launched) return;

        float step = Speed * Time.deltaTime;
        transform.position += (Vector3)(direction * step);
        travelled += step;

        if (travelled >= FlightDistance) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (hasHit) return;
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null && !alreadyHit.Add(enemy)) return;   // gerade erst abgeprallt

        if (weapon != null && weapon.IsActive && enemy != null)
        {
            enemy.TakeDamage(weapon.CurrentStats.damage);
        }

        if (TryBounce()) return;

        hasHit = true;
        Destroy(gameObject);
    }

    /// <summary>Abpraller: neue Richtung zum naechsten Gegner, der noch nicht getroffen wurde.</summary>
    private bool TryBounce()
    {
        if (!launched || bouncesLeft <= 0) return false;

        Enemy next = Ricochet.Nearest(transform.position, alreadyHit);
        if (next == null) return false;

        Vector2 to = (Vector2)next.transform.position - (Vector2)transform.position;
        if (to.sqrMagnitude < 0.0001f) return false;

        bouncesLeft--;
        direction = to.normalized;
        travelled = 0f;
        return true;
    }
}
