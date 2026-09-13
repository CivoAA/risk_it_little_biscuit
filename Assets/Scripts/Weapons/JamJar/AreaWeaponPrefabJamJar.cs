using UnityEngine;
using System.Collections.Generic;

public class AreaWeaponPrefabJamJar : MonoBehaviour
{
    public AreaWeaponJamJar weapon;
    private Vector3 targetSize;
    private float timer;
    public List<Enemy> enemiesInRange;
    private float counter;

    void Start()
    {
        weapon = WeaponFinder.Find<AreaWeaponJamJar>("Throwing Jam Jar");
        if (weapon == null)
        {
            Destroy(gameObject);
            return;
        }
        //Destroy(gameObject, weapon.duration);
        targetSize = Vector3.one * weapon.stats[weapon.weaponLevel].range * PlayerController.Instance.AOERange;
        transform.localScale = Vector3.zero;
        timer = weapon.stats[weapon.weaponLevel].duration;
        AudioController.Instance.PalySound(AudioController.Instance.JarJamBreakingGlass, 0.1f);
        AudioController.Instance.PalySound(AudioController.Instance.areaWeaponSpawn, 0.5f);
    }

    void Update()
    {
        if (weapon == null) return;

        //grow and shrink towards targetSize
        transform.localScale = Vector3.MoveTowards(transform.localScale, targetSize, Time.deltaTime * 3);
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            Destroy(gameObject);
        }
        // periodic damage
        counter -= Time.deltaTime;
        if (counter <= 0)
        {
            counter = weapon.stats[weapon.weaponLevel].AttackSpeed;
            for (int i = enemiesInRange.Count - 1; i >= 0; i--)
            {
                // zerstörte Gegner aus der Liste entfernen statt Exception
                if (enemiesInRange[i] == null)
                {
                    enemiesInRange.RemoveAt(i);
                    continue;
                }
                enemiesInRange[i].TakeDamage(weapon.stats[weapon.weaponLevel].damage);
            }
        }
    }
    
    private  void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Add(collider.GetComponent<Enemy>());
        }
    }
    
    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Remove(collider.GetComponent<Enemy>());
        }
    }
}
