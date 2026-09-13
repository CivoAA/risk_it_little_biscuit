using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ein aufgebauter Turm des <see cref="Turret"/>. Sucht sich im Schusstakt den
/// naechsten Gegner in Reichweite und feuert ein Projektil mit Vorhalt auf ihn.
///
/// Die Zielerfassung laeuft ueber OverlapCircle statt ueber einen Trigger:
/// die Reichweite ist ein Stat und wuerde sonst bei jedem Level-Up eine
/// Collider-Groesse nachziehen muessen.
/// </summary>
public class TurretPrefab : MonoBehaviour
{
    public Turret weapon;

    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Optional: dreht sich zum Ziel, bevor geschossen wird.")]
    [SerializeField] private Transform rotatingPart;

    private float lifeTimer;
    private float shotCounter;

    void Start()
    {
        if (weapon == null)
        {
            weapon = WeaponFinder.Find<Turret>("Turret");
        }

        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        lifeTimer = weapon.CurrentDuration;

        // Erster Schuss ohne volle Wartezeit, sonst wirkt der Turm tot
        shotCounter = 0.2f;
    }

    void Update()
    {
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        shotCounter -= Time.deltaTime;
        if (shotCounter > 0f) return;

        Enemy target = Aim.FindClosestEnemy(transform.position, Range);
        if (target == null) return;

        Shoot(target);
        shotCounter = Mathf.Max(0.05f, weapon.CurrentStats.AttackSpeed);
    }

    private float Range
    {
        get
        {
            float aoe = PlayerController.Instance != null ? PlayerController.Instance.AOERange : 1f;
            return weapon.CurrentStats.range * aoe;
        }
    }

    private void Shoot(Enemy target)
    {
        if (projectilePrefab == null) return;

        // Die Fluggeschwindigkeit steht im Prefab und geht in den Vorhalt ein,
        // deshalb vor dem Spawn auslesen statt am fertigen Objekt.
        TurretProjectile blueprint = projectilePrefab.GetComponent<TurretProjectile>();
        if (blueprint == null) return;

        Vector2 direction = Aim.PredictDirection(transform.position, target, blueprint.MoveSpeed);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (rotatingPart != null)
        {
            rotatingPart.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        GameObject shot = Instantiate(projectilePrefab, transform.position, Quaternion.Euler(0f, 0f, angle));

        Scene gameScene = SceneManager.GetSceneByName("Game");
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(shot, gameScene);
        }

        TurretProjectile projectile = shot.GetComponent<TurretProjectile>();
        if (projectile != null)
        {
            projectile.Launch(weapon, direction, target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (weapon == null || !weapon.IsActive) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Range);
    }
}
