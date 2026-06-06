using UnityEngine;

public class ArrowPointer : MonoBehaviour
{
    public Transform target;   // die Kiste, die angezeigt werden soll
    private Transform player;  // der Spieler

    void Start()
    {
        player = PlayerController.Instance.transform;
    }

    void Update()
    {
        if (target == null || player == null) return;

        // Richtung von Spieler → Kiste
        Vector3 dir = target.position - player.position;

        // Nur auf der XZ-Ebene (falls du ein Top-Down Spiel hast)
        dir.y = 0;

        if (dir.sqrMagnitude > 0.01f)
        {
            // Pfeil in Richtung drehen
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
