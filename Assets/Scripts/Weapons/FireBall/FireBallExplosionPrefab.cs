using UnityEngine;

public class FireBallExplosionPrefab : MonoBehaviour
{
    public FireBall weapon;

    void Start()
    {
        weapon = GameObject.Find("Fire Ball").GetComponent<FireBall>();
    }
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
