using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class CandyBombPrefab : MonoBehaviour
{
    public CandyBomb weapon; // Referenz zur Waffe
    public List<Enemy> enemiesInRange = new List<Enemy>(); // Gegner im Explosionsradius
    [SerializeField] private GameObject explosionPrefab;   // Explosionseffekt (im Inspector zuweisen)

    void Start()
    {
        // Deine bevorzugte Methode:
        GameObject weaponObj = GameObject.Find("Candy Bomb");
        if (weaponObj != null)
        {
            weapon = weaponObj.GetComponent<CandyBomb>();
        }
        else
        {
            Debug.LogWarning("CandyBombPrefab: ⚠️ Kein GameObject namens 'Candy Bomb' gefunden!");
        }

        // Starte Explosion nach kurzer Verzögerung
        StartCoroutine(TriggerExplosionAfterDelay(1.8f));

        Destroy(gameObject, 2.1f);
    }

    private IEnumerator TriggerExplosionAfterDelay(float delay)
    {
        // ⏳ kurz warten
        yield return new WaitForSeconds(delay);

        // Wenn keine Waffe oder ungültiges Level → trotzdem zerstören
        if (weapon == null || weapon.weaponLevel < 0)
        {
            Destroy(gameObject);
            yield break;
        }

        // 💥 Explosion erzeugen
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);

            // 🔹 Explosion skaliert mit Waffen-Range + Spieler-AOE
            float totalScale = weapon.stats[weapon.weaponLevel].range + PlayerController.Instance.AOERange + 1.5f;
            explosion.transform.localScale = Vector3.one * totalScale;
        }

        // Kopie der Liste erstellen (damit Änderungen die Schleife nicht crashen)
        var enemiesCopy = new List<Enemy>(enemiesInRange);

        float damage = weapon.stats[weapon.weaponLevel].damage;
        foreach (var enemy in enemiesCopy)
        {
            if (enemy != null)
                enemy.TakeDamage(damage);
        }

        // 🧹 Bombe löschen (Explosion bleibt)
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            var enemy = collider.GetComponent<Enemy>();
            if (enemy != null && !enemiesInRange.Contains(enemy))
                enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            var enemy = collider.GetComponent<Enemy>();
            if (enemy != null)
                enemiesInRange.Remove(enemy);
        }
    }
}
