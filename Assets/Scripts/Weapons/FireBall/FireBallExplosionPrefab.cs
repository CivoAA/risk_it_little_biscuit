using UnityEngine;

public class FireBallExplosionPrefab : MonoBehaviour
{
    public FireBall weapon;

    void Start()
    {
        weapon = WeaponFinder.Find<FireBall>("Fire Ball");
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
