using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ExplosiveStarEvo : Weapon
{   
    [SerializeField] private GameObject prefab;
     [SerializeField] private Transform bottomLimit;
    private float spawnCounter;
    public Camera cam;
    void Update()
    {   
        if (weaponLevel >= 0)
        {
            // Achievment Unlocken
            AchievementManager.Instance.UnlockAchievement("Explosive_Star_Evo");
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = CurrentCooldown;

                // Anzahl der Sterne aus AttackSpeed holen
                int spawnCount = Mathf.Max(1, Mathf.RoundToInt(stats[weaponLevel].AttackSpeed));

                // Coroutine starten, die alle Sterne spawnt
                StartCoroutine(SpawnMultipleStars(spawnCount));
                // Achievment Unlocken
                AchievementManager.Instance.UnlockAchievement("Explosive_Star_Evo");
            }
        }
    }
    private IEnumerator SpawnMultipleStars(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // zufälligen Punkt berechnen
            Vector2 randomPoint = RandomSpawnPoint();

            // Prefab spawnen
            GameObject star = Instantiate(prefab, (Vector3)randomPoint, transform.rotation);

            // in Game-Szene verschieben
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(star, gameScene);
            }

            // Bewegung starten
            StartCoroutine(MoveAndDestroy(star));

            // 1 Sekunde warten, bevor nächster Star gespawnt wird
            if (i < count - 1)
                yield return new WaitForSeconds(1f);
        }
    }
    IEnumerator MoveAndDestroy(GameObject star)
    {
        float moveSpeed = 9f;
        float duration = CurrentDuration;
        float elapsed = 0f;

        float angleDeg;
        do
        {
            angleDeg = Random.Range(0f, 360f);
        }
        while (Mathf.Abs(Mathf.Cos(angleDeg * Mathf.Deg2Rad)) < 0.6f   // X zu klein
            || Mathf.Abs(Mathf.Sin(angleDeg * Mathf.Deg2Rad)) < 0.6f); // Y zu klein

        Vector2 velocity = new Vector2(
            Mathf.Cos(angleDeg * Mathf.Deg2Rad),
            Mathf.Sin(angleDeg * Mathf.Deg2Rad)
        ) * moveSpeed;

        Camera cam = Camera.main;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // aktuelle Position
            Vector3 pos = star.transform.position;

            // Position updaten
            pos += (Vector3)(velocity * Time.deltaTime);

            // Kamera-Ränder berechnen (Z=0 bei 2D)
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));
            float bottomY = bottomLimit != null ? bottomLimit.position.y : bottomLeft.y;

            if (pos.x < bottomLeft.x)
            {
                pos.x = bottomLeft.x;     // sofort auf die Grenze setzen
                velocity.x *= -1;         // Richtung umdrehen
            }
            else if (pos.x > topRight.x)
            {
                pos.x = topRight.x;
                velocity.x *= -1;
            }

            // --------------------- Bounce prüfen Y ---------------------
            if (pos.y < bottomY)
            {
                pos.y = bottomY;          // sofort auf Grenze setzen
                velocity.y *= -1;
            }
            else if (pos.y > topRight.y)
            {
                pos.y = topRight.y;
                velocity.y *= -1;
            }

            star.transform.position = pos;

            yield return null;
        }

        // Nach Ablauf zerstören oder Effekt spawnen
        Destroy(star);
    }

    private Vector2 RandomSpawnPoint()
    {
        Vector2 spawnPoint;
        if (Random.Range(0f, 1f) > 0.5)
        {
            spawnPoint.x = Random.Range(transform.position.x - 5f, transform.position.x + 5f);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.y = transform.position.y;
            }
            else
            {
                spawnPoint.y = transform.position.y;
            }
        }
        else
        {
            spawnPoint.y = Random.Range(transform.position.y - 5f, transform.position.y + 5f);
            if (Random.Range(0f, 1f) > 0.5)
            {
                spawnPoint.x = transform.position.x;
            }
            else
            {
                spawnPoint.x = transform.position.x;
            }
        }

        return spawnPoint;
    }
}
