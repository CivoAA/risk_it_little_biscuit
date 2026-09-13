using UnityEngine;
using System.Collections.Generic;

public class BloodyForkPrefab : MonoBehaviour
{
    public BloodyFork weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private BoxCollider2D hitBox;
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();

    void Awake()
    {
        hitBox = GetComponent<BoxCollider2D>();
        if (enemiesInRange == null) enemiesInRange = new List<Enemy>();
    }

    void Start()
    {
        weapon = WeaponFinder.Find<BloodyFork>("Bloody Fork Evo");

        // Gleich beim Spawn treffen: Gegner, die schon in der Gabel stehen,
        // lösen kein OnTriggerEnter2D aus (siehe OverlapDamage).
        DamageOverlappingEnemies();
    }

    void FixedUpdate()
    {
        DamageOverlappingEnemies();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;
        DealDamage(collider.GetComponent<Enemy>());
    }

    private void DamageOverlappingEnemies()
    {
        OverlapDamage.FindEnemies(hitBox, enemiesInRange);

        for (int i = 0; i < enemiesInRange.Count; i++)
        {
            DealDamage(enemiesInRange[i]);
        }
    }

    private void DealDamage(Enemy enemy)
    {
        if (weapon == null || enemy == null) return;
        if (!weapon.IsActive) return;

        // Eine Gabel trifft denselben Gegner nur einmal
        if (!alreadyHit.Add(enemy)) return;

        enemy.TakeDamage(weapon.CurrentStats.damage);
    }
}
