using UnityEngine;
using System.Collections;

public class TimeLaserPrefab : MonoBehaviour
{
    public TimeLaser weapon;

    void Start()
    {
        weapon = WeaponFinder.Find<TimeLaser>("Time Laser");
        StartCoroutine(DestroyAfterDelay(0.45f));
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (weapon == null || !weapon.IsActive) return;
        if (collider.CompareTag("Enemy"))
        {
            collider.GetComponent<Enemy>()?.TakeDamage(weapon.CurrentStats.damage, 0.1f);
        }
    }
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
            Destroy(gameObject);
    }
    
}

