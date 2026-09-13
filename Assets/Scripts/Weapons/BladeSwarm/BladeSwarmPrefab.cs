using UnityEngine;
using System.Collections.Generic;

public class BladeSwarmPrefab : MonoBehaviour
{
    public BladeSwarm weapon;
    public List<Enemy> enemiesInRange;
    private SpriteRenderer spriteRenderer;
    private Animator playerAnimator;

    void Start()
    {
        weapon = WeaponFinder.Find<BladeSwarm>("Blade Swarm");
        GameObject hitbox = GameObject.FindWithTag("PlayerHitbox");
        if (hitbox != null) playerAnimator = hitbox.GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    void Update()
    {
        if (playerAnimator == null || spriteRenderer == null) return;

        // Wert aus Animator auslesen
        float lastMoveX = playerAnimator.GetFloat("LastMoveX");

        // Rotation nur ändern, wenn sich die Richtung ändert
        if (lastMoveX > 0.1f)
        {
            // Nach rechts → 135 Grad
            transform.rotation = Quaternion.Euler(0f, 0f, -45f);
        }
        else if (lastMoveX < -0.1f)
        {
            // Nach links → -45 Grad
            transform.rotation = Quaternion.Euler(0f, 0f, 135f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            if (weapon != null && weapon.IsActive)
            {
                collider.GetComponent<Enemy>()?.TakeDamage(weapon.CurrentStats.damage);
            }
            Destroy(gameObject);
        }
    }
    
    
}
