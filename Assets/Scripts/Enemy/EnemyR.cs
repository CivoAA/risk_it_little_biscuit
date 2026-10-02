using Unity.Mathematics;
using UnityEngine;

public class EnemyR : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Enemy enemy;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void FixedUpdate()
    {
        // Festgeklebt (Klebreis): nicht umdrehen.
        if (enemy != null && enemy.IsFrozen) return;

        if (PlayerController.Instance.gameObject.activeSelf)
        {
            //face the player
            if (PlayerController.Instance.transform.position.x > transform.position.x)
            {
                spriteRenderer.flipX = false;
            }
            else
            {
                spriteRenderer.flipX = true;
            }
        }
    }

}
