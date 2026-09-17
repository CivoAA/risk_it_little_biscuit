using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShurikenWeapon : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;
    public Vector2 moveDir;
    public Vector2 inputDir;
    private bool shooting = false;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxShurikookie);
            Unlocks.Grant(Unlocks.Shurikookie);
        }
        // Input speichern (letzte Bewegungsrichtung merken)
        inputDir = PlayerController.Instance.playerMoveDirection;
        if (inputDir != Vector2.zero)
        {
            moveDir = inputDir;
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
            AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.1f);

            GameObject shuriken = Instantiate(prefab, transform.position, transform.rotation);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(shuriken, gameScene);
            }

            StartCoroutine(MoveAndDestroy(shuriken));

            yield return new WaitForSeconds(0.2f);
        }

        // Cooldown korrekt setzen
        spawnCounter = CurrentCooldown;
        shooting = false;
    }

    IEnumerator MoveAndDestroy(GameObject shuriken)
    {
        float moveSpeed = 10f;

        // Richtung normalisieren (falls Spieler stillsteht → Standard nach links)
        if (moveDir == Vector2.zero) moveDir = Vector2.left;
        moveDir.Normalize();

        // Zielposition berechnen (+10f in Bewegungsrichtung)
        Vector2 targetPos = (Vector2)transform.position + moveDir * 10f;

        while (shuriken != null && Vector3.Distance(shuriken.transform.position, targetPos) > 0.001f)
        {
            shuriken.transform.position = Vector3.MoveTowards(
                shuriken.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (shuriken != null)
        {
            Destroy(shuriken);
        }
    }
}
