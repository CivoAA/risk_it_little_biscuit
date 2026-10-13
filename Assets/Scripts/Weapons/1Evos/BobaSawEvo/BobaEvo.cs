using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Boba Saw Evo (Cookie Saw + Boba Gun). Feuert ohne Pause: alle
/// <see cref="VolleyInterval"/> Sekunden <see cref="ProjectileCount"/> Bobas
/// im Kreis, der Kreis dreht sich dabei weiter.
///
/// Bewusst ohne Duration: die Salve hoert nie auf, ein Duration-Buff haette
/// nichts zu verlaengern. Der Cooldown-Buff macht das Feuer dichter.
/// </summary>
public class BobaEvo : Weapon
{
    /// <summary>Abstand zwischen zwei Salven vor dem Cooldown-Buff.</summary>
    private const float VolleyInterval = 0.1f;

    /// <summary>So weit dreht sich der Kreis pro Salve (Grad).</summary>
    private const float SpinPerVolley = 40f;

    /// <summary>Bobas pro Salve.</summary>
    private const int ProjectileCount = 4;

    /// <summary>Abstand des Zielpunkts vom Spieler - gibt nur die Richtung vor.</summary>
    private const float Radius = 5f;

    [SerializeField] private GameObject prefab;
    public Vector2 target;
    private float spawnCounter;
    private float spin;

    void Update()
    {
        if (!CanAttack) return;

        // Achievment Unlocken
        Achievements.Unlock(Ach.BobaSawEvo);

        spawnCounter -= Time.deltaTime;
        if (spawnCounter > 0f) return;

        float mult = PlayerController.Instance != null ? PlayerController.Instance.CooldownMultiplier : 1f;
        spawnCounter += VolleyInterval * mult;
        if (spawnCounter <= 0f) spawnCounter = VolleyInterval * mult;

        FireVolley();
        spin = (spin + SpinPerVolley) % 360f;
    }

    private void FireVolley()
    {
        WeaponSounds.Play(WeaponSounds.BobaEvoShot);

        for (int i = 0; i < ProjectileCount; i++)
        {
            // Winkel berechnen für jedes Projektil
            float angle = (360f / ProjectileCount) * i + spin;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * Radius;

            // Projektil am Spieler spawnen
            GameObject boba = Instantiate(prefab, PlayerController.Instance.transform.position, Quaternion.identity);
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(boba, gameScene);
            }

            // Zielpunkt = Position um den Spieler herum
            StartCoroutine(MoveAndDestroy(boba, PlayerController.Instance.transform.position + offset));
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