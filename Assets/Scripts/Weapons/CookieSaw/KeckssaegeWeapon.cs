using UnityEngine;
using UnityEngine.SceneManagement;

public class KeckssaegeWeapon : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;

    void Update()
    {
        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance.UnlockAchievement("Max_Level_Cookiesaw");
            UnlockManager.Instance.Unlock("unlock_boba_gun");
            UnlockManager.Instance.Unlock("unlock_candy_bomb");
        }
        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                spawnCounter = CurrentCooldown;

                // Orbit-Pivot erstellen
                GameObject orbit = new GameObject("OrbitPivot");
                Scene gameScene = SceneManager.GetSceneByName("Game");
                if (gameScene.IsValid() && gameScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(orbit, gameScene);
                }
                orbit.transform.position = transform.position;

                // nach Ablauf der Duration das ganze Pivot (mit Kindern) zerstören
                Destroy(orbit, CurrentDuration);
            
                
                // berechne diagonale Distanz so, dass sie "mittig" zwischen den Kanten liegt
                float diag = stats[weaponLevel].range * 0.7071f; // ca. 1/√2

                Vector3[] offsets = new Vector3[]
                {
                    new Vector3(0f, stats[weaponLevel].range, 0f),    // oben
                    new Vector3(0f, -stats[weaponLevel].range, 0f),   // unten
                    new Vector3(stats[weaponLevel].range, 0f, 0f),    // rechts
                    new Vector3(-stats[weaponLevel].range, 0f, 0f),   // links
                    new Vector3(diag, diag, 0f),                      // oben rechts
                    new Vector3(-diag, diag, 0f),                     // oben links
                    new Vector3(diag, -diag, 0f),                     // unten rechts
                    new Vector3(-diag, -diag, 0f),                    // unten links
                };
                int count = Mathf.Clamp(Mathf.FloorToInt(stats[weaponLevel].shots), 1, offsets.Length);
                for (int i = 0; i < count; i++)
                {
                    GameObject obj = Instantiate(prefab, transform.position + offsets[i], Quaternion.identity);
                    obj.transform.SetParent(orbit.transform, true);
                    obj.transform.localScale *= PlayerController.Instance.AOERange;
                }

                // Orbit-Rotation aktivieren
                orbit.AddComponent<OrbitRotate>().Init(transform);
            }
        }
    }

    public class OrbitRotate : MonoBehaviour
    {
        private Transform player;
        public float rotationSpeed = 90f; // Grad pro Sekunde

        public void Init(Transform playerTransform)
        {
            player = playerTransform;
        }

        void Update()
        {
            if (player == null) return;

            // Orbit-Pivot auf Spielerposition setzen
            transform.position = player.position;

            // Orbit-Pivot um Z-Achse drehen
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}
