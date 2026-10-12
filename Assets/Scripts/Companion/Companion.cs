using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Begleiter, der in der Overworld im Shop gekauft und in den Run mitgenommen
/// wird. Bewusst KEINE <see cref="Weapon"/>: er belegt keinen Waffen-Slot,
/// taucht nicht im Level-Up auf und wird nicht gelevelt - seine Staerke haengt
/// allein an der Shop-Stufe (siehe DamagePerTier / TargetsPerTier).
///
/// Er laeuft dem Spieler hinterher, sucht sich selbst Ziele in Reichweite und
/// schiesst im eigenen Takt - je Stufe auf einen Gegner mehr gleichzeitig. Der Schaden geht durch
/// <see cref="Enemy.TakeDamage"/> und profitiert damit automatisch von
/// damageMultiplier, Crit und Lifesteal.
/// </summary>
public class Companion : MonoBehaviour
{
    [Header("Bewegung")]
    [Tooltip("Abstand, den der Begleiter zum Spieler haelt.")]
    [SerializeField] private float followDistance = 1.6f;

    [Tooltip("Ab diesem Abstand holt er auf. Darunter bleibt er stehen.")]
    [SerializeField] private float catchUpDistance = 2.5f;

    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("Wie schnell er um den Spieler kreist, wenn dieser steht.")]
    [SerializeField] private float idleOrbitSpeed = 40f;

    [Header("Kampf")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackRange = 5f;
    [Tooltip("Schusstakt - gleich auf jeder Shop-Stufe.")]
    [SerializeField] private float attackInterval = 1.2f;

    [SerializeField] private SpriteRenderer spriteRenderer;

    // Shop-Stufe 1, 2, 3: Schaden je Schuss und wie viele Gegner pro Salve
    // beschossen werden. Neue Stufe = neue Zeile (und im Shop eine Stufe mehr).
    private static readonly float[] DamagePerTier  = { 5f, 10f, 15f };
    private static readonly int[]   TargetsPerTier = { 1, 2, 3 };

    private readonly List<Enemy> targets = new List<Enemy>();
    private int tier = 1;
    private float attackCounter;
    private float orbitAngle;
    private float lastShot = -10f;
    private CompanionVisual visual;

    private int TierIndex => Mathf.Clamp(tier - 1, 0, DamagePerTier.Length - 1);

    /// <summary>Schaden auf der aktuellen Shop-Stufe.</summary>
    public float Damage => DamagePerTier[TierIndex];

    /// <summary>So viele Gegner beschiesst er pro Salve gleichzeitig.</summary>
    public int Targets => TargetsPerTier[TierIndex];

    /// <summary>Wird vom <see cref="CompanionSpawner"/> direkt nach dem Spawn gesetzt.</summary>
    public void SetTier(int shopTier)
    {
        tier = Mathf.Max(1, shopTier);
        if (visual != null) visual.Init(tier, spriteRenderer);
    }

    /// <summary>Die Shop-Stufe bestimmt auch die Sorte (siehe <see cref="CompanionVisual"/>).</summary>
    public int Tier { get { return tier; } }

    public CompanionVisual Visual { get { return visual; } }

    void Start()
    {
        visual = GetComponent<CompanionVisual>();
        if (visual == null) visual = gameObject.AddComponent<CompanionVisual>();
        visual.Init(tier, spriteRenderer);
    }

    void Update()
    {
        if (PlayerController.Instance == null) return;

        Vector2 before = transform.position;
        Follow();
        Fight();

        // Ohne frisches Ziel schaut er dahin, wo er hinfliegt.
        Vector2 moved = (Vector2)transform.position - before;
        if (visual != null && Time.time - lastShot > 0.8f && moved.sqrMagnitude > 0.0004f)
        {
            visual.Face(moved);
        }
    }

    private void Follow()
    {
        Vector2 playerPos = PlayerController.Instance.transform.position;
        Vector2 myPos = transform.position;

        float distance = Vector2.Distance(myPos, playerPos);

        if (distance > catchUpDistance)
        {
            // Aufholen: direkt auf den Spieler zu, aber nur bis followDistance
            Vector2 target = playerPos + (myPos - playerPos).normalized * followDistance;
            transform.position = Vector2.MoveTowards(myPos, target, moveSpeed * Time.deltaTime);
        }
        else
        {
            // In Reichweite: langsam um den Spieler kreisen, damit er nicht
            // auf dem Spieler klebt und optisch lebendig bleibt.
            orbitAngle += idleOrbitSpeed * Time.deltaTime;
            Vector2 offset = new Vector2(
                Mathf.Cos(orbitAngle * Mathf.Deg2Rad),
                Mathf.Sin(orbitAngle * Mathf.Deg2Rad)) * followDistance;

            transform.position = Vector2.Lerp(myPos, playerPos + offset, moveSpeed * 0.5f * Time.deltaTime);
        }
    }

    private void Fight()
    {
        attackCounter -= Time.deltaTime;
        if (attackCounter > 0f) return;

        Aim.FindClosestEnemies(transform.position, attackRange, Targets, targets);
        if (targets.Count == 0) return;

        foreach (Enemy target in targets) Shoot(target);
        attackCounter = attackInterval;
    }

    private void Shoot(Enemy target)
    {
        if (projectilePrefab == null) return;

        // Die Fluggeschwindigkeit steht im Prefab und geht in den Vorhalt ein,
        // deshalb vor dem Spawn auslesen statt am fertigen Objekt.
        CompanionProjectile blueprint = projectilePrefab.GetComponent<CompanionProjectile>();
        if (blueprint == null) return;

        Vector3 muzzle = visual != null ? visual.Muzzle : transform.position;
        Vector2 direction = Aim.PredictDirection(muzzle, target, blueprint.MoveSpeed);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (visual != null)
        {
            visual.Face(direction);
            visual.PlayShoot();
        }

        lastShot = Time.time;

        GameObject shot = Instantiate(projectilePrefab, muzzle, Quaternion.Euler(0f, 0f, angle));

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(shot, gameScene);
        }

        CompanionProjectile projectile = shot.GetComponent<CompanionProjectile>();
        if (projectile != null)
        {
            projectile.Launch(this, direction, target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
