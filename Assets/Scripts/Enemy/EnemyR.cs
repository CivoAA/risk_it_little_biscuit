using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyR : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    void FixedUpdate()
    {
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
