using UnityEngine;

public class BobaGunPrefab : MonoBehaviour
{
     public BobaGun weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = GameObject.Find("Boba Gun").GetComponent<BobaGun>();
    }

private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}
