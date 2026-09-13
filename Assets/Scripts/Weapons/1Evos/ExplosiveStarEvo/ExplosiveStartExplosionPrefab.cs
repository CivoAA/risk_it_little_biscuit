using UnityEngine;

public class ExplosiveStartExplosionPrefab : MonoBehaviour
{
    public ExplosiveStarEvo weapon;

    void Start()
    {
        weapon = WeaponFinder.Find<ExplosiveStarEvo>("Explosive Star Evo");
    }
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null) return;
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
