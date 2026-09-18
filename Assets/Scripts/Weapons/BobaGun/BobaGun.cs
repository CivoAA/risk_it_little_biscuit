using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class BobaGun : Weapon
{
    [SerializeField] private GameObject prefab;
    //public List<Enemy> enemiesInRange;
    public Vector2 target;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxBobaGun);
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0 && shooting == false)
            {
                shooting = true;
                StartCoroutine(SpawnShuriken());
            }
        }
    }

    IEnumerator SpawnShuriken()
    {
        int shots = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots);

        for (int i = 0; i < shots; i++)
        {
            AudioController.Instance.PalyModifiedSound(AudioController.Instance.NewBoba);
            Vector3 topLeft = new Vector3(-5, 5, 0);
            Vector3 topRight = new Vector3(5, 5, 0);
            Vector3 bottomLeft = new Vector3(-5, -5, 0);
            Vector3 bottomRight = new Vector3(5, -5, 0);

            GameObject boba1 = Instantiate(prefab, PlayerController.Instance.transform.position, transform.rotation);
            StartCoroutine(MoveAndDestroy(boba1, PlayerController.Instance.transform.position + topLeft));

            GameObject boba2 = Instantiate(prefab, PlayerController.Instance.transform.position, transform.rotation);
            StartCoroutine(MoveAndDestroy(boba2, PlayerController.Instance.transform.position + topRight));

            GameObject boba3 = Instantiate(prefab, PlayerController.Instance.transform.position, transform.rotation);
            StartCoroutine(MoveAndDestroy(boba3, PlayerController.Instance.transform.position + bottomLeft));

            GameObject boba4 = Instantiate(prefab, PlayerController.Instance.transform.position, transform.rotation);
            StartCoroutine(MoveAndDestroy(boba4, PlayerController.Instance.transform.position + bottomRight));
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(boba1, gameScene);
                SceneManager.MoveGameObjectToScene(boba2, gameScene);
                SceneManager.MoveGameObjectToScene(boba3, gameScene);
                SceneManager.MoveGameObjectToScene(boba4, gameScene);
            }

            yield return new WaitForSeconds(0.2f);
        }
        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator MoveAndDestroy(GameObject BOBA, Vector2 Target)
    {
        float moveSpeed = 10f;
        // Zielposition berechnen (+10f in Bewegungsrichtung)
        Vector2 shooterPos = transform.position; 
        Vector2 enemyPos = Target;               
        Vector2 direction = (enemyPos - shooterPos).normalized; 
        Vector2 targetPos = enemyPos + direction * 10f;    
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        BOBA.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);     

        while (BOBA != null && Vector3.Distance(BOBA.transform.position, targetPos) > 0.001f)
        {
            BOBA.transform.position = Vector3.MoveTowards(BOBA.transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null; // 1 Frame warten
        }

        // Am Ziel: Effekt spawnen + Jar zerstören
        if (BOBA != null)
        {
            Destroy(BOBA);
            target = Vector2.zero;
        }
    }
   /* private void OnTriggerEnter2D(Collider2D collider)
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
    }*/
}
