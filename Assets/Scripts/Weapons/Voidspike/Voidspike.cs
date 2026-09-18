using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class Voidspike : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange;
    public Vector2 target;
    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxVoidSpike);
        }
        if (weaponLevel >= 0)
        {
            //UpdateTarget();
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = CurrentCooldown;
                StartCoroutine(SpawnVoidSpike());
            }
                //Vector2 myPos = transform.position;
                //float randomX = Random.Range(-5f, 5f); //                     Random spawns  um den player 
                //float randomY = Random.Range(-5f, 5f);
                //target = myPos + new Vector2(randomX, randomY);
        }
    }
    IEnumerator SpawnVoidSpike()
    {
        int count = Mathf.RoundToInt(stats[weaponLevel].shots + PlayerController.Instance.playerShots); // Wie oft spawnen?
        // Bewusst ohne CurrentDuration: das ist der Abstand zwischen zwei Spikes,
        // ein laengerer Wert waere hier eine Verschlechterung.
        float spawnDelay = stats[weaponLevel].duration;   // Zeit zwischen Spawns
        float lifeTime = 0.6f;      // Lebensdauer pro Spike
        Scene gameScene = RunScene.Current;
        for (int i = 0; i < count; i++)
        {
            UpdateTarget();
            Vector2 spawnPos = new Vector2(
            target.x + Random.Range(-0.1f, 0.1f), // kleine Abweichung auf X
            target.y - 0.5f                       // immer etwas tiefer
            );
            if (target == Vector2.zero) yield break;
            AudioController.Instance.PalySound(AudioController.Instance.PlayerHit);
            GameObject voidSpike = Instantiate(prefab, spawnPos, transform.rotation);
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(voidSpike, gameScene);
            }
            StartCoroutine(DestroyAfterSeconds(voidSpike, lifeTime));
            yield return new WaitForSeconds(spawnDelay);
        }
        target = Vector2.zero;
    }
    IEnumerator DestroyAfterSeconds(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj != null)
        {
            Destroy(obj);
        }
    }

    private void UpdateTarget()
    {
        if (enemiesInRange == null || enemiesInRange.Count == 0)
            return;

        // Entferne evtl. Null-Einträge, damit kein Fehler kommt
        enemiesInRange.RemoveAll(e => e == null);

        if (enemiesInRange.Count == 0)
            return;

        // Zufälligen Index wählen
        int randomIndex = Random.Range(0, enemiesInRange.Count);

        // Ziel setzen
        target = enemiesInRange[randomIndex].transform.position;
    }
    private void OnTriggerEnter2D(Collider2D collider)
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
