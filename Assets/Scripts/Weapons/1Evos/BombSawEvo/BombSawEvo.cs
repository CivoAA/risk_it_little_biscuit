using UnityEngine;
using UnityEngine.SceneManagement;

public class BombSawEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    private float spawnCounter;

    // Der laufende Saegenkreis. Lebt er beim naechsten Cooldown noch (Wirkdauer
    // >= Cooldown), wird er nur verlaengert statt einen zweiten daneben zu legen.
    private GameObject orbit;
    private float orbitEnd;
    private float orbitAoe;

    void Update()
    {
        // Ablauf selbst pruefen statt Destroy(orbit, t) - so laesst er sich verlaengern.
        if (orbit != null && Time.time >= orbitEnd)
        {
            Destroy(orbit);
            orbit = null;
        }

        if (weaponLevel >= 0)
        {
            spawnCounter -= Time.deltaTime;
            if (spawnCounter <= 0)
            {
                // Die Evo feuert nur, wenn man sie besitzt - also ist das hier der
                // Moment, in dem das Achievement aufgeht (wie bei den anderen Evos).
                Achievements.Unlock(Ach.BombSawEvo);

                spawnCounter = CurrentCooldown;

                // Kreis laeuft noch: nur verlaengern. Hat sich AOE geaendert,
                // neu bauen, damit die Saegen die neue Groesse bekommen.
                if (orbit != null && Mathf.Approximately(orbitAoe, PlayerController.Instance.AOERange))
                {
                    orbitEnd = Time.time + CurrentDuration;
                    return;
                }
                if (orbit != null) Destroy(orbit);

                WeaponSounds.Play(WeaponSounds.SawSpawn, 1f, 0.85f);

                // Orbit-Pivot erstellen
                orbit = new GameObject("OrbitPivotEvo");
                Scene gameScene = RunScene.Current;
                if (gameScene.IsValid() && gameScene.isLoaded)
                {
                    SceneManager.MoveGameObjectToScene(orbit, gameScene);
                }
                orbit.transform.position = transform.position;

                // nach Ablauf der Duration das ganze Pivot (mit Kindern) zerstören
                orbitEnd = Time.time + CurrentDuration;
                orbitAoe = PlayerController.Instance.AOERange;

                
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
                    // AOE vergroessert nur die Trefferflaeche - das Bild bleibt pixelgenau.
                    CircleCollider2D hitArea = obj.GetComponent<CircleCollider2D>();
                    if (hitArea != null) hitArea.radius *= PlayerController.Instance.AOERange;
                    else obj.transform.localScale *= PlayerController.Instance.AOERange;
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
