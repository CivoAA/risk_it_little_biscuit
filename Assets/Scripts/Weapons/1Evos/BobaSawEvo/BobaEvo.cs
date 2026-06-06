using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class BobaEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    public Vector2 target;
    private float spawnCounter;

    void Update()
    {
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                // Achievment Unlocken
                AchievementManager.Instance.UnlockAchievement("Boba_Saw_Evo");
                spawnCounter = stats[weaponLevel].cooldown;
                StartCoroutine(SpawnShuriken());
            }
        }
    }

    IEnumerator SpawnShuriken()
    {
        // Achievment Unlocken
        AchievementManager.Instance.UnlockAchievement("Boba_Saw_Evo");
        float duration = stats[weaponLevel].duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            AudioController.Instance.PalyModifiedSound(AudioController.Instance.NewBoba);

            int projectileCount = 4;   // wie viele Bobas gleichzeitig
            float radius = 5f;         // Abstand vom Spieler (Kreisradius)

            for (int i = 0; i < projectileCount; i++)
            {
                // Winkel berechnen für jedes Projektil
                float angle = (360f / projectileCount) * i + (elapsed * 200f); 
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * radius;

                // Projektil am Spieler spawnen
                GameObject boba = Instantiate(prefab, PlayerController.Instance.transform.position, Quaternion.identity);
                Scene gameScene = SceneManager.GetSceneByName("Game");
                if (gameScene.IsValid() && gameScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(boba, gameScene);
                }

                // Zielpunkt = Position um den Spieler herum
                StartCoroutine(MoveAndDestroy(boba, PlayerController.Instance.transform.position + offset));
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.2f;
        }
    }

    IEnumerator MoveAndDestroy(GameObject BOBA, Vector2 targetPos)
    {
        float moveSpeed = 10f;

        // Richtung bestimmen
        Vector2 shooterPos = PlayerController.Instance.transform.position;
        Vector2 direction = (targetPos - shooterPos).normalized;
        Vector2 finalTargetPos = (Vector2)PlayerController.Instance.transform.position + direction * 10f;

        // Rotation vom Projektil anpassen
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        BOBA.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

        // Bewegung
        while (BOBA != null && Vector3.Distance(BOBA.transform.position, finalTargetPos) > 0.001f)
        {
            BOBA.transform.position = Vector3.MoveTowards(BOBA.transform.position, finalTargetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }

        // Zerstören
        if (BOBA != null)
        {
            Destroy(BOBA);
            target = Vector2.zero;
        }
    }
}