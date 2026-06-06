using UnityEngine;

public class TeleportPoint : MonoBehaviour
{
    [Tooltip("Wohin der Spieler teleportiert werden soll")]
    public Transform targetPosition;

    [Tooltip("Optional: BlackScreen Canvas")]
    public GameObject blackScreen;
}
