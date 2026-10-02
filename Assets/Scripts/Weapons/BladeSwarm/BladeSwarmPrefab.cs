using UnityEngine;
using System.Collections.Generic;

public class BladeSwarmPrefab : MonoBehaviour
{
    public BladeSwarm weapon;
    public List<Enemy> enemiesInRange;
    private SpriteRenderer spriteRenderer;
    private Animator playerAnimator;

    // Skilltree "Schattenschwarm" (siehe ShadowSwoop)
    private bool swooping;
    private Enemy swoopTarget;
    private Vector2 swoopDirection;
    private float swoopTime;

    void Start()
    {
        weapon = WeaponFinder.Find<BladeSwarm>("Blade Swarm");
        GameObject hitbox = GameObject.FindWithTag("PlayerHitbox");
        if (hitbox != null) playerAnimator = hitbox.GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    void Update()
    {
        if (swooping)
        {
            StepSwoop();
            return;
        }

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

    /// <summary>
    /// Skilltree "Schattenschwarm": statt einmal zuzustechen fliegt der Kunai
    /// so lange durch <paramref name="target"/>, bis es tot ist. Ist das Ziel
    /// schon weg, nimmt er einen anderen Gegner aus <paramref name="pool"/>.
    /// </summary>
    public void Swoop(Enemy target, List<Enemy> pool)
    {
        swoopTarget = ShadowSwoop.IsAlive(target) ? target : ShadowSwoop.AnyAlive(pool);
        if (swoopTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        swooping = true;
        swoopTime = 0f;
        swoopDirection = ShadowSwoop.FirstDirection(transform.position, swoopTarget);
    }

    private void StepSwoop()
    {
        swoopTime += Time.deltaTime;

        if (weapon == null || !weapon.IsActive || !ShadowSwoop.IsAlive(swoopTarget)
            || ShadowSwoop.TimedOut(swoopTime))
        {
            Destroy(gameObject);
            return;
        }

        Vector2 position = transform.position;
        Vector2 exit = ShadowSwoop.ExitPoint(swoopTarget, swoopDirection);
        Vector2 toExit = exit - position;
        float step = ShadowSwoop.Speed * Time.deltaTime;

        if (toExit.sqrMagnitude <= step * step)
        {
            transform.position = exit;
            swoopDirection = ShadowSwoop.TurnDirection(swoopDirection);
        }
        else
        {
            Vector2 dir = toExit.normalized;
            transform.position = position + dir * step;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);
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

            // Im Schattenschwarm bleibt der Kunai heil und fliegt weiter durch.
            if (!swooping) Destroy(gameObject);
        }
    }


}
