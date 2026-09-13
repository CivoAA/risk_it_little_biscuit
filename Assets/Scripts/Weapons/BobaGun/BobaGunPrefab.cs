using UnityEngine;

public class BobaGunPrefab : MonoBehaviour
{
     public BobaGun weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = WeaponFinder.Find<BobaGun>("Boba Gun");
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
