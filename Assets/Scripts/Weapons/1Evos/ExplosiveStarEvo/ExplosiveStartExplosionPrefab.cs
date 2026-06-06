using UnityEngine;

public class ExplosiveStartExplosionPrefab : MonoBehaviour
{
    public ExplosiveStarEvo weapon;

    void Start()
    {
        weapon = GameObject.Find("Explosive Star Evo").GetComponent<ExplosiveStarEvo>();
    }
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
