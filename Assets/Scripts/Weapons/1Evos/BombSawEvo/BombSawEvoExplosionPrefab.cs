using UnityEngine;

public class BombSawEvoExplosionPrefab : MonoBehaviour
{
    public BombSawEvo weapon;

    void Start()
    {
        weapon = GameObject.Find("Bomb Saw Evo").GetComponent<BombSawEvo>();
    }
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
