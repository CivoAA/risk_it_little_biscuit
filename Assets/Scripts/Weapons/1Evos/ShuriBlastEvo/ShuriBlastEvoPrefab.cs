using UnityEngine;

public class ShuriBlastEvoPrefab : MonoBehaviour
{
    public ShuriBlastEvo weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = GameObject.Find("Shuri Blast Evo").GetComponent<ShuriBlastEvo>();
    }

private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
    
}
