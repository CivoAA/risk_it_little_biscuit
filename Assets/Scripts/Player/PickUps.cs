using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickUps : MonoBehaviour
{
    /// <summary>0 = Herz, 1 = Magnet, 2 = goldenes Herz (heilt wie ein Herz und gibt Max-Leben).</summary>
    public const int GoldenHeartId = 2;

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
                PlayerController.Instance.Heal(PlayerController.Instance.playerMaxHealth * 0.1f);

            }
            else if (PickUp_id == 1)
            {
                PlayerController.Instance.attractAllXP = true;
            }
            else if (PickUp_id == GoldenHeartId)
            {
                PlayerController p = PlayerController.Instance;
                float bonus = EnemyCatalog.GoldenHeartMaxHealth;
                p.playerMaxHealth += bonus;
                p.Heal(p.playerMaxHealth * 0.1f + bonus);
                DamageNumberController.Instance?.CreateText($"+{bonus:0} Max HP", transform.position);
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
            Scene gameScene = RunScene.Current;
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(DestroyEffect, gameScene);
            }
        }
    }
}