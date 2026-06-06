using UnityEngine;

public class CelestialStarPreFab : MonoBehaviour
{
    public CelestialStar weapon;

    void Start()
    {
        weapon = GameObject.Find("Celestial Star").GetComponent<CelestialStar>();
        //AudioController.Instance.PalySound(AudioController.Instance.saege);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.stats[weapon.weaponLevel].damage);
        }
    }
}

