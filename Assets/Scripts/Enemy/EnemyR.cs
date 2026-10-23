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

        // Koop: zum naechsten Spieler schauen.
        if (NetPlayers.TryNearest(transform.position, out NetPlayers.Target target))
        {
            //face the player
            if (target.Position.x > transform.position.x)
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
