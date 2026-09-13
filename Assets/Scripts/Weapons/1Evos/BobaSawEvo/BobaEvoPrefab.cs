using UnityEngine;

public class BobaEvoPrefab : MonoBehaviour
{
     public BobaEvo weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = WeaponFinder.Find<BobaEvo>("Boba Saw Evo");
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