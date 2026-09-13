using UnityEngine;

public class BombSawEvoExplosionPrefab : MonoBehaviour
{
    public BombSawEvo weapon;

    void Start()
    {
        weapon = WeaponFinder.Find<BombSawEvo>("Bomb Saw Evo");
    }
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null || !weapon.IsActive) return;
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.CurrentStats.damage);
        }
    }
}
