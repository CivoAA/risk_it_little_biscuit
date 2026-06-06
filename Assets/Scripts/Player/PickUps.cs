using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickUps : MonoBehaviour
{
    public float detectRange = 1f;       // Ab wann das Objekt den Spieler anzieht
    public float moveSpeed = 5f;         // Geschwindigkeit beim Hinfliegen
    public int PickUp_id = 99;
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
            if(PickUp_id == 0)
            {
                PlayerController.Instance.playerHealth += PlayerController.Instance.playerMaxHealth* 0.1f;
                UIController.Instance.UpdateHealthSlider();
                if(PlayerController.Instance.playerHealth > PlayerController.Instance.playerMaxHealth)
                {
                    PlayerController.Instance.playerHealth = PlayerController.Instance.playerMaxHealth;
                }

            }
            else if (PickUp_id == 1)
            {
                PlayerController.Instance.attractAllXP = true;
            }
            else if(PickUp_id == 99)
            {
                return;
            }
            else
            {
                return;
            }
            
            // Danach zerstören
            Destroy(gameObject);
            GameObject DestroyEffect =Instantiate(destroyEffect, transform.position, transform.rotation);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(DestroyEffect, gameScene);
            }
        }
    }
}