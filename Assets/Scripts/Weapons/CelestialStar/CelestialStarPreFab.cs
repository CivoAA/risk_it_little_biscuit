using UnityEngine;

public class CelestialStarPreFab : MonoBehaviour
{
    public CelestialStar weapon;

    void Start()
    {
        weapon = WeaponFinder.Find<CelestialStar>("Celestial Star");
        //AudioController.Instance.PalySound(AudioController.Instance.saege);
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

