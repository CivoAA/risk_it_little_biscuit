using UnityEngine;

public class ShuriBlastEvoPrefab : MonoBehaviour
{
    public ShuriBlastEvo weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = WeaponFinder.Find<ShuriBlastEvo>("Shuri Blast Evo");
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
