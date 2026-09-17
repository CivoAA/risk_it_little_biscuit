using UnityEngine;

// Kamera-Follow ausschliesslich fuer die Hub-Szene.
// Hartes Folgen ohne Damping: die Kamera sitzt in jedem Frame exakt auf dem Target
// und bleibt in dem Moment stehen, in dem das Target stehen bleibt - kein Nachziehen.
// Ersetzt in der Hub-Szene bewusst Cinemachine. Nicht in andere Szenen uebernehmen,
// dort laeuft weiterhin der CinemachineBrain.
public class HubCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;          // Hub_Player
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    void Awake()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning($"{name}: Kein Target gesetzt und kein Objekt mit Tag 'Player' in der Szene!");
            }
        }
    }

    void Start()
    {
        SnapToTarget();
    }

    // LateUpdate, damit die Kamera erst nach der Spielerbewegung nachzieht.
    void LateUpdate()
    {
        SnapToTarget();
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }
}
