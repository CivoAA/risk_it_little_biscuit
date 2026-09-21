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

    // Zug von aussen (Wirbel). Wird als Geschwindigkeit auf die normale
    // Laufbewegung addiert und laeuft nach kurzer Zeit von selbst aus, damit
    // ein zerstoerter Wirbel keinen Gegner dauerhaft mitzieht.
    private Vector2 externalVelocity;
    private float externalVelocityTimer;

    /// <summary>
    /// Aktuelle Laufgeschwindigkeit. Grundlage fuer den Vorhalt der Schuetzen,
    /// siehe <see cref="Aim.PredictDirection"/>.
    /// </summary>
    public Vector2 Velocity
    {
        get { return rb != null ? rb.linearVelocity : Vector2.zero; }
    }

    /// <summary>Bosse und Minibosse lassen sich nicht ziehen oder wegschieben.</summary>
    public bool IsBoss
    {
        get { return MiniBoss || BossBoss || Death_Boss; }
    }

    /// <summary>
    /// Leben beim Spawn, nach der Lauf-Skalierung. Bezugspunkt fuer
    /// <see cref="HealthFraction"/>.
    /// </summary>
    private float maxHealth;

    /// <summary>
    /// Wie viel Leben noch steht, 1 = voll. Der Keks-Koenig haengt daran
    /// seinen Phasenwechsel auf. Ohne das muesste jeder Boss sein Leben ein
    /// zweites Mal selbst mitzaehlen, und die beiden Staende liefen
    /// auseinander, sobald irgendwo anders Schaden dazukommt.
    /// </summary>
    public float HealthFraction
    {
        get
        {
            // Fragt jemand schon vor Start (Start-Reihenfolge ist nicht
            // garantiert), gilt der aktuelle Stand als voll.
            if (maxHealth <= 0f) maxHealth = health;
            return maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 1f;
        }
    }

    /// <summary>
    /// Setzt das Leben auf einen Anteil des Startwerts. Nur fuer die
    /// Test-Szene: damit laesst sich die zweite Phase eines Bosses anspringen,
    /// ohne ihn vorher von Hand halb totzuschlagen. Toetet nie - das soll der
    /// Spieler schon selbst machen.
    /// </summary>
    public void DebugSetHealthFraction(float fraction)
    {
        if (maxHealth <= 0f) maxHealth = health;
        health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(fraction));
    }

    // ---------------------------------------------------------------- Register

    private static readonly System.Collections.Generic.List<Enemy> alive =
        new System.Collections.Generic.List<Enemy>();

    /// <summary>
    /// Alle Gegner, die gerade leben. Der <see cref="SpawnDirector"/> rechnet
    /// daraus den Druck auf dem Feld aus - und zwar auch ueber Gegner, die er
    /// nicht selbst gesetzt hat (Mini-Muffins aus einem Muffin zum Beispiel).
    /// Ohne die wuerde er nachlegen, obwohl es schon voll ist.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<Enemy> Alive => alive;

    /// <summary>
    /// Was dieser Gegner im Druck-Budget wiegt. Setzt der Director beim
    /// Spawnen; wer anders erzeugt wird, zaehlt als 1.
    /// </summary>
    [System.NonSerialized] public float RunThreat = 1f;

    /// <summary>
    /// Darf der Director diesen Gegner nachziehen, wenn der Spieler wegrennt?
    /// Fuer Kaefig und Ring-Gegner eines Encirclements: nein - die gehoeren an
    /// ihren Platz.
    /// </summary>
    [System.NonSerialized] public bool CanRecycle;

    protected virtual void OnEnable()
    {
        alive.Add(this);
    }

    protected virtual void OnDisable()
    {
        alive.Remove(this);
    }

    /// <summary>
    /// Haengt die Lauf-Schwierigkeit an: mehr Leben, mehr Schaden, mehr
    /// Belohnung. Ruft <see cref="RunDifficulty"/> direkt nach dem Spawnen auf,
    /// also bevor der Gegner das erste Mal laeuft.
    /// </summary>
    public void ApplyRunScaling(float healthFactor, float damageFactor, float rewardFactor)
    {
        health = Mathf.Max(1f, health * healthFactor);
        damage *= damageFactor;
        experienceToGive = Mathf.Max(0, Mathf.RoundToInt(experienceToGive * rewardFactor));

        // Der Bezugspunkt muss NACH der Skalierung stehen, sonst waere ein
        // Boss in einem harten Lauf sofort unter 50% und die zweite Phase
        // liefe von Anfang an.
        maxHealth = health;
    }

    /// <summary>
    /// Zieht den Gegner fuer kurze Zeit in eine Richtung. Der Aufrufer muss das
    /// jeden Frame erneuern (der Wirbel tut das), sonst laeuft der Zug aus.
    /// </summary>
    public void ApplyPull(Vector2 velocity, float holdTime = 0.2f)
    {
        if (IsBoss) return;

        externalVelocity = velocity;
        externalVelocityTimer = holdTime;
    }

    protected virtual void Start()
    {
        baseMoveSpeed = moveSpeed;

        // Wer ohne Director gesetzt wird (Test-Szene, alte Aufbauten), laeuft
        // nie durch ApplyRunScaling - dann gilt der Prefab-Wert.
        if (maxHealth <= 0f) maxHealth = health;
    }

    protected virtual void FixedUpdate()
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
            // Zug von aussen auslaufen lassen
            if (externalVelocityTimer > 0f)
            {
                externalVelocityTimer -= Time.fixedDeltaTime;
                if (externalVelocityTimer <= 0f)
                {
                    externalVelocity = Vector2.zero;
                }
            }

            //move towards palyer
            if (!BossBoss)
            {
                direction = (PlayerController.Instance.transform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(direction.x * moveSpeed, direction.y * moveSpeed) + externalVelocity;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController.Instance.TakeDamage(damage);
            
        }

    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerHitbox"))
        {
            PlayerController.Instance.TakeDamage(damage);
        }
    }

    public virtual void TakeDamage(float damage, float? slowMultiplier = null)
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

            // Ohne Null-Pruefung reisst ein fehlender Spawner den ganzen
            // Todesfall mit: die NullReference fliegt, das Destroy unten laeuft
            // nie, und der Gegner steht mit negativem Leben weiter herum.
            if (SpawnExp.Instance != null)
            {
                SpawnExp.Instance.SpawnEP(transform.position, experienceToGive);
            }
            if (Death_Boss)
            {
                PlayerController.Instance.attractAllXP = true;
                Achievements.Unlock(Ach.Death);
                // 💰 Belohnung: +50 SkillCurrency
                Skills.AddCurrency(50);
                WM_UIController.Instance?.UpdateSkillCurrencyText();
                if (SpawnChest.Instance != null) SpawnChest.Instance.Spawn(transform.position);
            }
            else if (MiniBoss)
            {
                if (SpawnChest.Instance != null) SpawnChest.Instance.Spawn(transform.position);
                // Evo-Slot ist jetzt von Anfang an im Shop sichtbar - hier gibt es nichts mehr freizuschalten.
                Achievements.Progress(Ach.Kill10Miniboss, 1f);
                Achievements.Progress(Ach.Kill100Miniboss, 1f);
                int blockerLayer = LayerMask.NameToLayer("Enemy_barrier");
                foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                {
                    if (enemy.gameObject.layer == blockerLayer)
                    {
                        Destroy(enemy.gameObject);
                    }
                }
                // 💰 Belohnung: +1 SkillCurrency
                Skills.AddCurrency(1);
                WM_UIController.Instance?.UpdateSkillCurrencyText();
            }
            else
            {
                Achievements.Progress(Ach.Kill100, 1f);
                Achievements.Progress(Ach.Kill1000, 1f);
                Achievements.Progress(Ach.Kill10000, 1f);
            }
            if (BossBoss)
            {
                PlayerController.Instance.attractAllXP = true;
                Vector3 spawnPos1 = transform.position;
                spawnPos1.x += 50;
                GameManager.Instance.bossSpawned = true;
                if (SpawnDeath.Instance != null) SpawnDeath.Instance.Spawn(spawnPos1);
                // 💰 Belohnung: +10 SkillCurrency
                Skills.AddCurrency(10);
                WM_UIController.Instance?.UpdateSkillCurrencyText();
            }
            Destroy(gameObject);
            GameObject DestroyEffect = Instantiate(destroyEffect, transform.position, transform.rotation);
            Scene gameScene = RunScene.Current;
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
        Scene gameScene = RunScene.Current;
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
