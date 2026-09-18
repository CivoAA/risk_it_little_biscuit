using UnityEngine;

public class EnemyTeleport : MonoBehaviour
{
    [Header("Einstellungen")]
    public float maxDistance = 23f; // maximale Entfernung in jede Richtung
    public float offsetX = 15f;     // X-Versatz beim Teleport
    public float offsetY = 10f;     // Y-Versatz beim Teleport

    void Awake()
    {
        // Laeuft ein SpawnDirector, macht der das Nachziehen gebuendelt und
        // ueber das Spawn-Muster der Phase - dann waere das hier ein zweiter,
        // widersprechender Mechanismus. Das Skript bleibt am Prefab, damit die
        // Test-Szene und alte Aufbauten weiter funktionieren.
        if (SpawnDirector.Active != null) enabled = false;
    }

    void Update()
    {
        if (PlayerController.Instance == null)
            return;

        Transform player = PlayerController.Instance.transform;
        Vector2 playerPos = player.position;
        Vector2 myPos = transform.position;

        float dx = myPos.x - playerPos.x;
        float dy = myPos.y - playerPos.y;

        // Prüfen, ob Gegner zu weit weg ist
        if (Mathf.Abs(dx) > maxDistance || Mathf.Abs(dy) > maxDistance)
        {
            float randomXOffset = Random.Range(-offsetX, offsetX);
            float randomYOffset = Random.Range(-offsetY, offsetY);

            Vector2 newPos = playerPos;

            // Wenn weiter rechts als erlaubt → nach links teleportieren
            if (dx > maxDistance)
                newPos.x = playerPos.x - offsetX;
            // Wenn weiter links als erlaubt → nach rechts teleportieren
            else if (dx < -maxDistance)
                newPos.x = playerPos.x + offsetX;
            else
                newPos.x = playerPos.x + randomXOffset;

            // Wenn zu weit oben → unten teleportieren
            if (dy > maxDistance)
                newPos.y = playerPos.y - offsetY;
            // Wenn zu weit unten → oben teleportieren
            else if (dy < -maxDistance)
                newPos.y = playerPos.y + offsetY;
            else
                newPos.y = playerPos.y + randomYOffset;

            transform.position = newPos;
        }
    }
}
