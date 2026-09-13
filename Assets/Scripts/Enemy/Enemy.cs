using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Enemy : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    private Vector3 direction;
    [SerializeField] private float moveSpeed;
    private float baseMoveSpeed; 
    private bool isSlowed = false;
    [SerializeField] private float damage;
    [SerializeField] private float health;
    [SerializeField] private int experienceToGive;
    [SerializeField] private GameObject destroyEffect;
    [SerializeField] private float pushTime;
    [SerializeField] private bool rightlooking = true;
    [SerializeField] private bool MiniBoss = false;
    [SerializeField] private bool BossBoss = false;
    [SerializeField] private bool Death_Boss = false;
    private float pushCounter;

    void Start()
    {
        baseMoveSpeed = moveSpeed;
    }

    void FixedUpdate()
    {
        if (PlayerController.Instance.gameObject.activeSelf)
        {
            //face the player
            if (rightlooking)
            {
                float xDiff = PlayerController.Instance.transform.position.x - transform.position.x;

                // Nur flippen, wenn der Unterschied groß genug ist
                if (Mathf.Abs(xDiff) > 0.2f)
                {
                    spriteRenderer.flipX = xDiff > 0;
                }
            }
            // push back
            if (pushCounter > 0)
            {
                pushCounter -= Time.deltaTime;
                if (moveSpeed > 0)
                {
                    moveSpeed = -moveSpeed;
                }
                if (pushCounter <= 0)
                {
                    moveSpeed = Mathf.Abs(moveSpeed);
                }
            }
            //move towards palyer
            if (!BossBoss)
            {
                direction = (PlayerController.Instance.transform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(direction.x * moveSpeed, direction.y * moveSpeed);
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController.Instance.TakeDamage(damage);
            
        }

    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox"))
        {
            PlayerController.Instance.TakeDamage(damage);
        }
    }

    public void TakeDamage(float damage, float? slowMultiplier = null)
    {
        float finalDamage = damage * PlayerController.Instance.damageMultiplier;
        float critChance = PlayerController.Instance.critChance;
        float critDamage = PlayerController.Instance.critDamage;

        if (UnityEngine.Random.value < critChance)
        {
            finalDamage *= critDamage;
            DamageNumberController.Instance.CreateNumberCrit(finalDamage, transform.position);
        }
        else
        {
            DamageNumberController.Instance.CreateNumber(finalDamage, transform.position);
        }
        health -= finalDamage;
        if (LifeSteal.Instance != null)
        {
            LifeSteal.Instance.StealLife((int)damage);
        }
        pushCounter = pushTime;
        if (health <= 0)
        {
            TrySpawnPickup();
            SpawnExp.Instance.SpawnEP(transform.position, experienceToGive);
            if (Death_Boss)
            {
                PlayerController.Instance.attractAllXP = true;
                AchievementManager.Instance.UnlockAchievement("Death");
                // 💰 Belohnung: +50 SkillCurrency
                if (SkillSaveManager.Instance != null)
                {
                    SkillSaveManager.Instance.AddSkillCurrency(50);
                    WM_UIController.Instance?.UpdateSkillCurrencyText();
                    Debug.Log($"💰 Gain 50 SkillCurrency !");
                }
                else
                {
                    Debug.LogWarning("⚠️ SkillSaveManager.Instance ist NULL – keine Belohnung vergeben!");
                }
                SpawnChest.Instance.Spawn(transform.position);
            }
            else if (MiniBoss)
            {
                SpawnChest.Instance.Spawn(transform.position);
                UnlockManager.Instance.Unlock("unlock_weapon_evo");
                AchievementManager.Instance.UpdateAchievementValue("Kill_10_Miniboss", 1f);
                AchievementManager.Instance.UpdateAchievementValue("Kill_100_Miniboss", 1f);
                int blockerLayer = LayerMask.NameToLayer("Enemy_barrier");
                foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                {
                    if (enemy.gameObject.layer == blockerLayer)
                    {
                        Destroy(enemy.gameObject);
                    }
                }
                // 💰 Belohnung: +1 SkillCurrency
                if (SkillSaveManager.Instance != null)
                {
                    SkillSaveManager.Instance.AddSkillCurrency(1);
                    WM_UIController.Instance?.UpdateSkillCurrencyText();
                    Debug.Log($"💰 Gain 1 SkillCurrency !");
                }
                else
                {
                    Debug.LogWarning("⚠️ SkillSaveManager.Instance ist NULL – keine Belohnung vergeben!");
                }
            }
            else
            {
                AchievementManager.Instance.UpdateAchievementValue("Kill_100", 1f);
                AchievementManager.Instance.UpdateAchievementValue("Kill_1000", 1f);
                AchievementManager.Instance.UpdateAchievementValue("Kill_10000", 1f);
            }
            if (BossBoss)
            {
                PlayerController.Instance.attractAllXP = true;
                Vector3 spawnPos1 = transform.position;
                spawnPos1.x += 50;
                GameManager.Instance.bossSpawned = true;
                SpawnDeath.Instance.Spawn(spawnPos1);
                // 💰 Belohnung: +10 SkillCurrency
                if (SkillSaveManager.Instance != null)
                {
                    SkillSaveManager.Instance.AddSkillCurrency(10);
                    WM_UIController.Instance?.UpdateSkillCurrencyText();
                    Debug.Log($"💰 Gain 10 SkillCurrency !");
                }
                else
                {
                    Debug.LogWarning("⚠️ SkillSaveManager.Instance ist NULL – keine Belohnung vergeben!");
                }
            }
            Destroy(gameObject);
            GameObject DestroyEffect = Instantiate(destroyEffect, transform.position, transform.rotation);
            Scene gameScene = SceneManager.GetSceneByName("Game");
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(DestroyEffect, gameScene);
            }

            //PlayerController.Instance.GetExperience(experienceToGive);
            AudioController.Instance.PalyModifiedSound(AudioController.Instance.enemyDeath);
        }
        // Slow nur anwenden, wenn der Gegner noch lebt
        // (sonst Coroutine auf einem bereits zerstörten Objekt)
        if (health > 0 && slowMultiplier.HasValue && !isSlowed)
        {
            StartCoroutine(ApplySlowOnce(slowMultiplier.Value, 1.5f));
        }
    }
    
    private IEnumerator ApplySlowOnce(float multiplier, float duration)
    {
        isSlowed = true;

        // aktuelle Geschwindigkeit basierend auf Basisgeschwindigkeit
        moveSpeed = baseMoveSpeed * multiplier;

        yield return new WaitForSeconds(duration);

        // zurücksetzen auf Originalwert
        moveSpeed = baseMoveSpeed;
        isSlowed = false;
    }
    private void TrySpawnPickup()
    {
        Scene gameScene = SceneManager.GetSceneByName("Game");
        // Magnet: 1 zu 500 (0.2%)
        if (UnityEngine.Random.Range(1, 501) == 1)
        {
            GameObject magnet = Instantiate(PickUpManager.Instance.magnet_PickUP, transform.position, Quaternion.identity);
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(magnet, gameScene);
            }
        }
        // Herz: 1 zu 50 (2%)
        if (UnityEngine.Random.Range(1, 101) == 1)
        {
            GameObject heart = Instantiate(PickUpManager.Instance.heart_PickUP, transform.position, Quaternion.identity);
            if (gameScene.IsValid() && gameScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(heart, gameScene);
            }
            return;
        }

    }

}
