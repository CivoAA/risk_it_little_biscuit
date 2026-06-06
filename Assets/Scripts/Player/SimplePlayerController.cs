using UnityEngine;

public class SimplePlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    public float moveSpeed;
    [SerializeField] private Animator animator;
    public Vector3 playerMoveDirection;
        private Vector2 lastMoveDir = Vector2.down;


    void Update()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        playerMoveDirection = new Vector3(inputX, inputY).normalized;

        animator.SetFloat("MoveX", inputX);
        animator.SetFloat("MoveY", inputY);

        if (playerMoveDirection != Vector3.zero)
        {
            lastMoveDir = playerMoveDirection;
            animator.SetBool("moving", true);
        }
        else
        {
            animator.SetBool("moving", false);
        }
        animator.SetFloat("LastMoveX", lastMoveDir.x);
        animator.SetFloat("LastMoveY", lastMoveDir.y);
    }
    
    void FixedUpdate()
    {
        rb.linearVelocity = new Vector3(playerMoveDirection.x * moveSpeed, playerMoveDirection.y * moveSpeed);

    }
}
