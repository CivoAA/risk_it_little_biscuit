using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BoomerangEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel >= 0)
        {
            Achievements.Unlock(Ach.BoomerangEvo);
            spawnCounter -= Time.deltaTime;

            if (spawnCounter <= 0 && !shooting)
            {
                shooting = true;
                StartCoroutine(SpawnBoomerangRing());
                Achievements.Unlock(Ach.BoomerangEvo);
            }
        }
    }

    IEnumerator SpawnBoomerangRing()
    {
        int totalBoomerangs = Mathf.RoundToInt((stats[weaponLevel].shots + PlayerController.Instance.playerShots) * 2);
        float angleStep = 360f / totalBoomerangs;

        for (int i = 0; i < totalBoomerangs; i++)
        {
            float angle = i * angleStep;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject boomerang = Instantiate(prefab, transform.position, Quaternion.identity);
            boomerang.transform.localScale *= (PlayerController.Instance.AOERange * 0.7f);

            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
                SceneManager.MoveGameObjectToScene(boomerang, gameScene);

            StartCoroutine(MoveAndDestroy(boomerang, direction));
        }

        AudioController.Instance?.PalySound(AudioController.Instance.Werfen);

        spawnCounter = CurrentCooldown;
        shooting = false;

        yield return null;
    }

    IEnumerator MoveAndDestroy(GameObject boomerang, Vector2 direction)
    {
        float moveSpeed = 12f;
        float maxDistance = 12f; // Wie weit die Boomerangs fliegen sollen
        Vector2 startPos = transform.position;

        // Zielpunkt berechnen
        Vector2 targetPos = startPos + direction * maxDistance;

        while (boomerang != null && Vector2.Distance(boomerang.transform.position, targetPos) > 0.05f)
        {
            boomerang.transform.position = Vector2.MoveTowards(
                boomerang.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            // Optional: Boomerang leicht rotieren lassen
            boomerang.transform.Rotate(0f, 0f, 720f * Time.deltaTime);

            yield return null;
        }

        if (boomerang != null)
            Destroy(boomerang);
    }
}
