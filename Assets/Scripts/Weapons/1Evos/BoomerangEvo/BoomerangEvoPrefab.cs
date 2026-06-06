using UnityEngine;


public class BoomerangEvoPrefab : MonoBehaviour
{
    public BoomerangEvo weapon;

    private float counter;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = GameObject.Find("Boomerang Evo").GetComponent<BoomerangEvo>();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
    
}

