using UnityEngine;
using UnityEngine.SceneManagement;

public class PickupObject : MonoBehaviour
{
    public float detectRange = 5f;       // Ab wann das Objekt den Spieler anzieht
    public float moveSpeed = 5f;         // Geschwindigkeit beim Hinfliegen
    [SerializeField] private GameObject destroyEffect;

    private Transform player;

    void Start()
    {
        // Nimmt an, dass dein Player das Tag "Player" hat
        GameObject playerObj = GameObject.FindGameObjectWithTag("PlayerHitbox");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // Wenn Player in Reichweite -> Richtung Player bewegen
        if (distance <= detectRange)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox"))
        {
            PlayerController.Instance.RandomWeapon2();
            UIController.Instance.GambaPanelOpen();

            // Danach zerstören
            Destroy(gameObject);
            GameObject DestroyEffect =Instantiate(destroyEffect, transform.position, transform.rotation);
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(DestroyEffect, gameScene);
            }
        }
    }
}