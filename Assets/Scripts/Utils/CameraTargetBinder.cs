using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Haengt auf der CinemachineCamera der Spielszene und haelt ihr Tracking Target
/// auf dem Spieler.
///
/// Im Inspector steht dort bisher der Player als Szenen-Referenz. Die gilt nur,
/// solange beide in derselben Szene liegen - sobald die Maps in eigene Szenen
/// wandern und der Spieler aus der Kern-Szene kommt, speichert Unity so eine
/// szenenuebergreifende Referenz nicht und laesst das Feld leer. Dann traegt
/// dieses Skript den Spieler zur Laufzeit nach.
///
/// Es ueberschreibt nichts: ist ein Target gesetzt, haelt es sich raus.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[DisallowMultipleComponent]
public class CameraTargetBinder : MonoBehaviour
{
    [Tooltip("Optional: festes Ziel. Leer = der Spieler aus PlayerController.Instance.")]
    [SerializeField] private Transform target;

    private CinemachineCamera cam;

    void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
    }

    void OnEnable()
    {
        Bind();
    }

    // Der Spieler kann eine Szene spaeter fertig werden als die Kamera, darum
    // wird weiter geprueft, bis das Ziel steht. Danach kostet das nichts mehr:
    // sobald Follow gesetzt ist, faellt die Methode sofort wieder raus.
    void LateUpdate()
    {
        Bind();
    }

    private void Bind()
    {
        if (cam == null) return;
        if (cam.Follow != null) return;

        Transform t = target;
        if (t == null && PlayerController.Instance != null)
            t = PlayerController.Instance.transform;

        if (t == null) return;

        cam.Follow = t;
    }
}
