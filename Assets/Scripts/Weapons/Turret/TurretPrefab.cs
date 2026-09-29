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

    [Header("Rohr")]
    [Tooltip("Renderer am Rohr - zeigt kurz das Muendungsfeuer.")]
    [SerializeField] private SpriteRenderer barrelRenderer;
    [SerializeField] private Sprite barrelIdle;
    [SerializeField] private Sprite barrelFire;
    [SerializeField] private float muzzleFlashTime = 0.07f;

    [Tooltip("Das Projektil startet so weit vor dem Turm - an der Muendung statt in der Kuppel.")]
    [SerializeField] private float muzzleDistance = 0.4f;

    private float lifeTimer;
    private float shotCounter;
    private float flashTimer;
    private SpriteFlipbook[] looks;

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
        looks = GetComponentsInChildren<SpriteFlipbook>();

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
        foreach (SpriteFlipbook look in looks) look.SetLifeLeft(lifeTimer);
        UpdateMuzzleFlash();

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

    private void UpdateMuzzleFlash()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f && barrelRenderer != null && barrelIdle != null)
            barrelRenderer.sprite = barrelIdle;
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

        if (barrelRenderer != null && barrelFire != null)
        {
            barrelRenderer.sprite = barrelFire;
            flashTimer = muzzleFlashTime;
        }

        Vector3 muzzle = rotatingPart != null ? rotatingPart.position : transform.position;
        muzzle += (Vector3)(direction.normalized * muzzleDistance);

        GameObject shot = Instantiate(projectilePrefab, muzzle, Quaternion.Euler(0f, 0f, angle));

        Scene gameScene = RunScene.Current;
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
