using System.Collections.Generic;
using TMPro;
using Unity.Hierarchy;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering.Universal;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;
    [SerializeField] private Rigidbody2D rb;
    public float moveSpeed;
    [SerializeField] private Animator animator;
    public Vector3 playerMoveDirection;
    public float playerMaxHealth;
    public float playerHealth;
    public float playerHealthReg;
    public float playerArmor;
    public float experience;
    public float experienceMultiplier = 1;
    public float AOERange = 1;
    public GameObject PickupRange;
    public float pickupRange = 1;
    private float pickupRangeOLD = -1;
    public float playerShots;

    /// <summary>
    /// Extra-Schuesse, die Waffen wirklich abfeuern: nur ganze. 1,5 oder 1,999
    /// gibt einen, erst ab 2 gibt es zwei. Waffen lesen das hier, nie playerShots.
    /// </summary>
    public int ExtraShots => Mathf.FloorToInt(playerShots + 0.0001f);

    public float critChance;
    public float critDamage;
    public float dodgeChance;
    public float luck;
    public float damageMultiplier = 1;
    public float lifeStealChance;
    public float lifeStealMultiplire = 0.1f;
    public float powerUpShrinkSpeed = 1f;

    [Header("Globale Waffen-Multiplikatoren")]
    [Tooltip("1 = unveraendert. Der Cooldown-Buff zieht hier ab, 0.8 bedeutet -20% Cooldown.")]
    public float cooldownMultiplier = 1f;
    [Tooltip("Untergrenze, damit der Cooldown-Buff die Waffen nicht auf quasi 0 druecken kann.")]
    public float minCooldownMultiplier = 0.35f;
    [Tooltip("1 = unveraendert. Der Duration-Buff addiert hier drauf, 1.3 bedeutet +30% Wirkdauer.")]
    public float durationMultiplier = 1f;

    [Header("Zweite Chance")]
    [Tooltip("Verbleibende Wiederbelebungen. Wird vom Buff 'Zweite Chance' gesetzt.")]
    public int secondChanceCharges;
    [Tooltip("Anteil der Max-HP, mit dem der Spieler zurueckkommt.")]
    public float secondChanceHealthPercent = 0.3f;
    [Tooltip("Unverwundbarkeit direkt nach der Wiederbelebung.")]
    public float secondChanceImmunity = 2f;

    // Glücks-Ast im Skilltree. Kommen nur aus dem Skilltree und werden im
    // Spiel nicht als Stat angezeigt - Luck selbst wirkt nur auf den Mixer.
    [Header("Glück (Skilltree)")]
    [Tooltip("Faktor auf Magnet- und Herz-Chance. 1 = unveraendert.")]
    public float dropChanceMultiplier = 1f;
    [Tooltip("Chance, dass ein Gegner doppelte XP fallen laesst.")]
    public float luckyXpChance;
    [Tooltip("Chance, dass ein gedropptes Herz golden ist.")]
    public float goldenHeartChance;
    [Tooltip("Chance auf eine Wiederbelebung. Wird beim ersten Tod einmal gewuerfelt, vor dem Buff 'Zweite Chance'.")]
    public float luckyReviveChance;
    public float luckyReviveHealthPercent = 0.3f;
    public float luckyReviveImmunity = 2f;
    private bool luckyReviveRolled;

    // Mixer-Knoten im Skilltree. Stehen in Prozent (1 = 1 %), so wie sie im
    // Skilltree eingetragen werden.
    [Tooltip("+x % Mixer auf der Map (25 = ein Viertel mehr).")]
    public float moreMixersPercent;
    [Tooltip("Chance in %, dass ein Mixer alle drei Stats gibt.")]
    public float mixerAllThreeChance;
    [Tooltip("Chance in %, dass alle drei Stats eines Mixers legendaer sind.")]
    public float mixerAllLegendaryChance;

    // Wissens-Ast, ebenfalls in Prozent.
    [Header("Wissen (Skilltree)")]
    [Tooltip("Chance in %, dass ein Reroll nichts kostet.")]
    public float freeRerollChance;
    [Tooltip("Heilung in % der Max-HP bei jedem Level-Up.")]
    public float levelUpHealPercent;
    [Tooltip("+x % Charakter-XP (ausserhalb des Laufs) auf alles, was eingesammelt wird.")]
    public float charXpBonusPercent;
    [Tooltip("Bosse und Minibosse spawnen mit x % weniger Leben.")]
    public float bossHealthReduction;

    // Geist-Ast, in Prozent.
    [Header("Geist (Skilltree)")]
    [Tooltip("Ueberheilung wird zu Schild, hoechstens x % der Max-HP.")]
    public float overhealShieldPercent;
    [Tooltip("Letzter Atem: unter 30 % Leben +x % Tempo und doppelte Regeneration.")]
    public float lastBreathSpeedPercent;
    [Tooltip("Aktueller Schild aus Ueberheilung. Faengt Schaden vor dem Leben ab.")]
    public float shield;

    /// <summary>
    /// Cooldown-Faktor inklusive Untergrenze - Waffen lesen nur diesen Wert.
    /// Rage halbiert erst NACH der Untergrenze, sonst ginge sie am Maximum verloren.
    /// </summary>
    public float CooldownMultiplier
    {
        get
        {
            float mult = Mathf.Max(minCooldownMultiplier, cooldownMultiplier);
            return IsRaging ? mult * RageCooldownFactor : mult;
        }
    }

    /// <summary>Duration-Faktor. Nach unten abgesichert, damit Flaechen nie 0s leben.</summary>
    public float DurationMultiplier
    {
        get { return Mathf.Max(0.1f, durationMultiplier); }
    }
    public int WeaponSlots = 5;
    public int BuffSlots = 3;
    public int EvoSlots = 0;
    public int rerollAmount;
    public int banishAmount;
    public int currentLevel;
    public int maxLevel;
    public bool attractAllXP = false;
    public bool LevelUpSelectet = true;
    public List<int> playerLevels;
    private bool isImmune;
    private Vector2 lastMoveDir = Vector2.down;

    public Weapon[] activeWeapon;
    public Weapon[] activeBuffs;
    public Weapon[] activeEvos;
    public Weapon[] maxLevelStuff;

    [Header("Evo-Kombinationen")]
    public List<EvoRecipe> EvoCombinations = new List<EvoRecipe>();

    [SerializeField] private float immunityDuration;
    [SerializeField] private float immunityTimer;
    private int hitsoundinterval = 10;
    private PlayerHitFeedback hitFeedback;
    private static readonly System.Random rng = new System.Random();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        hitFeedback = GetComponent<PlayerHitFeedback>();
    }

    void Start()
    {
        BuildLevelCurve();

        // Im Lauf gibt es nur, was in der Werkbank liegt - leere Gruppen
        // werden dabei zufaellig gefuellt (siehe Loadout.PrepareForRun).
        Loadout.PrepareForRun();

        StartStats();
        UIController.Instance.UpdateHealthSlider();
        UIController.Instance.UpdateExperienceSlider();
    }

    // Levelkurve einmalig aufbauen (vorher jeden Frame in Update geprüft)
    private void BuildLevelCurve()
    {
        if (playerLevels == null)
        {
            playerLevels = new List<int>();
        }
        if (playerLevels.Count == 0)
        {
            playerLevels.Add(10); // Fallback, falls im Inspector nichts eingetragen ist
            Debug.LogWarning("PlayerController: playerLevels war leer – Fallback-Startwert gesetzt.");
        }

        int targetLevel = 30;
        int level30Value = 10000;

        for (int i = playerLevels.Count; i < maxLevel; i++)
        {
            int lastValue = playerLevels[playerLevels.Count - 1];

            if (i == targetLevel)
            {
                playerLevels.Add(level30Value); // Set level 30 explicitly
            }
            else if (i < targetLevel)
            {
                playerLevels.Add(Mathf.CeilToInt(lastValue * 1.1f + 15f));
            }
            else
            {
                // From level 31 onward, grow based on the previous level (starting from 5000)
                playerLevels.Add(Mathf.CeilToInt(lastValue * 1.1f + 20f));
            }
        }
    }

    void Update()
    {
        if (pickupRange > pickupRangeOLD)
        {
            PickupRange.transform.localScale = Vector3.one * pickupRange;
            pickupRangeOLD = pickupRange;
        }

        // Index absichern: currentLevel muss >= 1 sein und in die Levelkurve passen
        if (currentLevel >= 1 && currentLevel - 1 < playerLevels.Count
            && experience >= playerLevels[currentLevel - 1])
        {
            LevelUp();
        }
        // Bei pausiertem Spiel (Level-Up-Panel, Pause-Menue, Mixer, ...) laufen
        // die Updates weiter, obwohl Time.timeScale 0 ist. Eingaben duerfen dann
        // nicht ausgewertet werden: sonst dreht sich der Spieler im Menue mit
        // A/D mit - und mit ihm jede Waffe, die sich an LastMoveX orientiert.
        if (Time.timeScale > 0f)
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

        if (immunityTimer > 0)
        {
            immunityTimer -= Time.deltaTime;
        }
        else
        {
            isImmune = false;
        }
    }

    void LateUpdate()
    {
        // Nach den Coroutines, sonst setzt der Treffer-Flash die Deckkraft fuer einen Frame zurueck.
        UpdateKawarimiFade();
    }

    void FixedUpdate()
    {
        // Skilltree "Letzter Atem": unter 30 % Leben schneller.
        float speed = IsLastBreath ? moveSpeed * (1f + lastBreathSpeedPercent / 100f) : moveSpeed;
        // Skilltree "Kawarimi": waehrend der Wirkung deutlich schneller, um wegzurennen.
        if (IsKawarimi) speed *= 1f + KawarimiSpeedBonus;
        rb.linearVelocity = new Vector3(playerMoveDirection.x * speed, playerMoveDirection.y * speed);

        DecayShield(Time.fixedDeltaTime);
    }

    // ------------------------------------------------------------------
    // Geist: Ueberheilung, Letzter Atem, Schockwelle
    // ------------------------------------------------------------------

    /// <summary>Ab diesem Lebensanteil greift "Letzter Atem".</summary>
    public const float LastBreathThreshold = 0.3f;

    /// <summary>So lange haelt der Schild, bevor er abbaut, wenn nichts nachkommt.</summary>
    private const float ShieldHoldTime = 3f;

    /// <summary>So viel vom maximalen Schild baut er pro Sekunde ab.</summary>
    private const float ShieldDecayPerSecond = 0.2f;

    private float shieldGainedAt = -999f;

    /// <summary>Wie gross der Schild hoechstens werden kann (Skilltree "OverhealShield").</summary>
    public float MaxShield => playerMaxHealth * overhealShieldPercent / 100f;

    /// <summary>Lohnt sich Heilung gerade? Auch bei vollem Leben, solange der Schild noch Platz hat.</summary>
    public bool CanReceiveHealing => playerHealth < playerMaxHealth || shield < MaxShield;

    public bool IsLastBreath =>
        lastBreathSpeedPercent > 0f && playerHealth > 0f && playerHealth <= playerMaxHealth * LastBreathThreshold;

    /// <summary>
    /// Heilt den Spieler. Was ueber die Max-HP hinausgeht, wird mit dem Skill
    /// "OverhealShield" zu Schild statt zu verfallen. Alle Heilungen sollen hier
    /// durch - sonst geht die Ueberheilung an ihnen vorbei.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        float room = Mathf.Max(0f, playerMaxHealth - playerHealth);
        float toHealth = Mathf.Min(room, amount);
        playerHealth += toHealth;

        float over = amount - toHealth;
        if (over > 0f && MaxShield > 0f)
        {
            shield = Mathf.Min(MaxShield, shield + over);
            shieldGainedAt = Time.time;
        }

        UIController.Instance?.UpdateHealthSlider();
    }

    private void DecayShield(float dt)
    {
        if (shield <= 0f) return;
        if (shield > MaxShield) shield = MaxShield;   // Max-HP gesunken
        if (Time.time - shieldGainedAt < ShieldHoldTime) return;

        shield = Mathf.Max(0f, shield - MaxShield * ShieldDecayPerSecond * dt);
    }

    private float shockwaveReadyAt;

    /// <summary>Skilltree "Schockwelle": bei einem Treffer alle Gegner in der Naehe wegstossen.</summary>
    private void TryShockwave()
    {
        if (!Skills.HasGrant(SkillGrants.Schockwelle) || Time.time < shockwaveReadyAt) return;

        shockwaveReadyAt = Time.time + Shockwave.Cooldown;
        Shockwave.Fire(transform.position);
    }

    // ------------------------------------------------------------------
    // Wissen: Rage
    // ------------------------------------------------------------------

    /// <summary>Ab diesem Lebensanteil springt Rage an.</summary>
    public const float RageThreshold = 0.3f;

    /// <summary>So lange haelt Rage an (Sekunden).</summary>
    public const float RageDuration = 5f;

    /// <summary>So lange dauert es, bis Rage wieder anspringen kann (ab Start gerechnet).</summary>
    public const float RageCooldown = 120f;

    /// <summary>Faktor auf alle Waffen-Cooldowns waehrend Rage - 0.5 = halb so lang.</summary>
    public const float RageCooldownFactor = 0.5f;

    private float rageUntil;
    private float rageReadyAt;

    public bool IsRaging => Time.time < rageUntil;

    /// <summary>Skilltree "Rage": nach einem Treffer unter 30 % Leben feuern alle Waffen doppelt so schnell.</summary>
    private void TryRage()
    {
        if (!Skills.HasGrant(SkillGrants.Rage) || Time.time < rageReadyAt) return;
        if (playerHealth <= 0f || playerHealth > playerMaxHealth * RageThreshold) return;

        rageUntil = Time.time + RageDuration;
        rageReadyAt = Time.time + RageCooldown;
        DamageNumberController.Instance?.CreateText("RAGE!", transform.position);
    }

    // ------------------------------------------------------------------
    // Geist: Kawarimi
    // ------------------------------------------------------------------

    /// <summary>Ab diesem Lebensanteil springt Kawarimi an.</summary>
    public const float KawarimiThreshold = 0.5f;

    /// <summary>So lange weicht der Spieler allem aus (Sekunden).</summary>
    public const float KawarimiDuration = 4f;

    /// <summary>Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float KawarimiCooldown = 35f;

    /// <summary>Deckkraft des Spielers waehrend Kawarimi.</summary>
    public const float KawarimiAlpha = 0.4f;

    /// <summary>Tempo-Bonus waehrend Kawarimi - 0.5 = 50 % schneller. Rechnet auf Letzter Atem drauf.</summary>
    public const float KawarimiSpeedBonus = 0.5f;

    private float kawarimiUntil;
    private float kawarimiReadyAt;
    private bool kawarimiFaded;

    public bool IsKawarimi => Time.time < kawarimiUntil;

    /// <summary>Skilltree "Kawarimi": faellt das Leben unter 50 %, 4 s lang 100 % Ausweichen und schneller.</summary>
    private void TryKawarimi()
    {
        if (!Skills.HasGrant(SkillGrants.Kawarimi) || Time.time < kawarimiReadyAt) return;
        if (playerHealth <= 0f || playerHealth > playerMaxHealth * KawarimiThreshold) return;

        kawarimiUntil = Time.time + KawarimiDuration;
        kawarimiReadyAt = kawarimiUntil + KawarimiCooldown;
        DamageNumberController.Instance?.CreateText("KAWARIMI!", transform.position);
    }

    /// <summary>
    /// Durchscheinend, solange Kawarimi laeuft. Jeden Frame gesetzt, weil der
    /// Treffer-Flash (<see cref="PlayerHitFeedback"/>) die Farbe danach
    /// zuruecksetzt - und mit ihr die Deckkraft.
    /// </summary>
    private void UpdateKawarimiFade()
    {
        bool active = IsKawarimi;
        if (!active && !kawarimiFaded) return;

        SpriteRenderer body = hitFeedback != null && hitFeedback.playerSpriteRenderer != null
            ? hitFeedback.playerSpriteRenderer
            : (animator != null ? animator.GetComponent<SpriteRenderer>() : null);
        if (body == null) return;

        Color c = body.color;
        c.a = active ? KawarimiAlpha : 1f;
        body.color = c;
        kawarimiFaded = active;
    }

    // ------------------------------------------------------------------
    // Geist: Klebreis
    // ------------------------------------------------------------------

    private float stickyRiceReadyAt;

    /// <summary>Skilltree "Klebreis": faellt das Leben unter 35 %, kleben alle Gegner im Bild fest.</summary>
    private void TryStickyRice()
    {
        if (!Skills.HasGrant(SkillGrants.Klebreis) || Time.time < stickyRiceReadyAt) return;
        if (playerHealth <= 0f || playerHealth > playerMaxHealth * StickyRice.Threshold) return;

        stickyRiceReadyAt = Time.time + StickyRice.Duration + StickyRice.Cooldown;
        StickyRice.Fire();
        DamageNumberController.Instance?.CreateText("KLEBREIS!", transform.position);
    }

    // ------------------------------------------------------------------
    // Geist: Wirbelsturm
    // ------------------------------------------------------------------

    private float whirlwindReadyAt;

    /// <summary>Skilltree "Wirbelsturm": faellt das Leben unter 40 %, alle Gegner in der Naehe wegdruecken.</summary>
    private void TryWhirlwind()
    {
        if (!Skills.HasGrant(SkillGrants.Wirbelsturm) || Time.time < whirlwindReadyAt) return;
        if (playerHealth <= 0f || playerHealth > playerMaxHealth * Whirlwind.Threshold) return;

        whirlwindReadyAt = Time.time + Whirlwind.Duration + Whirlwind.Cooldown;
        Whirlwind.Fire(transform);
        DamageNumberController.Instance?.CreateText("WIRBELSTURM!", transform.position);
    }

    public void StartStats()
    {
        rageUntil = 0f;
        rageReadyAt = 0f;
        kawarimiUntil = 0f;
        kawarimiReadyAt = 0f;
        stickyRiceReadyAt = 0f;
        whirlwindReadyAt = 0f;

        if (activeWeapon != null && activeWeapon.Length > 0)
        {
            int startWeaponIndex = Mathf.Clamp(Shop.RunStartWeapon, 0, activeWeapon.Length - 1);
            Weapon startWeapon = activeWeapon[startWeaponIndex];
            // Skilltree "StartWeaponLevel": +1 heisst Start auf Stufe 2.
            startWeapon.weaponLevel = Mathf.Clamp(Skills.BonusInt(SkillType.StartWeaponLevel), 0,
                                                  Mathf.Max(0, startWeapon.maxweaponLevel));
            startWeapon.posssibleEvo = true;

            foreach (var recipe in EvoCombinations)
            {
                // wenn Startwaffe Weapon1 ist
                if (recipe.RequiredWeapon1 == startWeapon && recipe.RequiredWeapon2 != null)
                {
                    recipe.RequiredWeapon2.posssibleEvo = true;
                }
                // oder wenn Startwaffe Weapon2 ist
                else if (recipe.RequiredWeapon2 == startWeapon && recipe.RequiredWeapon1 != null)
                {
                    recipe.RequiredWeapon1.posssibleEvo = true;
                }
            }
        }

        // Shop-Extras. Welcher Eintrag welchen Wert liefert, steht in Shop.cs -
        // hier stand früher ein Zugriff auf feste Plätze in einem float[30].
        if (GameManager.Instance != null)
            GameManager.Instance.currencyGainMultiplire += Shop.Get(Shop.CurrencyGain);

        rerollAmount       += Shop.GetInt(Shop.Rerolls);
        banishAmount       += Shop.GetInt(Shop.Banish);
        experience         += Shop.Get(Shop.StartXp);
        powerUpShrinkSpeed += Shop.Get(Shop.ShrinkSpeed);
        BuffSlots          += Shop.GetInt(Shop.BuffSlot);
        WeaponSlots        += Shop.GetInt(Shop.WeaponSlot);
        EvoSlots           += Shop.GetInt(Shop.EvoSlot);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.charLevelBeforeGame = Skills.Level;
            GameManager.Instance.charXpGained = 0f;
        }
        Achievements.Unlock(Ach.FirstGame);

        // Skilltree-Boni. Die Summen kommen aus dem gerade aktiven Baum
        // (später: dem Baum des gewählten Charakters).
        playerMaxHealth      += Skills.Bonus(SkillType.IncreaseMaxHealth);
        playerHealth          = playerMaxHealth;
        moveSpeed            += Skills.Bonus(SkillType.IncreaseSpeed);
        experienceMultiplier += Skills.Bonus(SkillType.xpMultiplier);
        playerHealthReg      += Skills.Bonus(SkillType.IncreaseHealthReg);
        playerShots          += Skills.Bonus(SkillType.IncreaseExtraShot);
        playerArmor          += Skills.Bonus(SkillType.IncreaseArmor);
        AOERange             += Skills.Bonus(SkillType.IncreaseAOERange);
        lifeStealChance      += Skills.Bonus(SkillType.IncreaseLifeSteal);
        damageMultiplier     += Skills.Bonus(SkillType.IncreaseDamage);
        critDamage           += Skills.Bonus(SkillType.IncreaseCritDamage);
        critChance           += Skills.Bonus(SkillType.IncreaseCritChance);
        dodgeChance          += Skills.Bonus(SkillType.IncreaseDodgeChance);
        pickupRange          += Skills.Bonus(SkillType.IncreasePickupRange);
        luck                 += Skills.Bonus(SkillType.IncreaseLuck);
        banishAmount         += Skills.BonusInt(SkillType.BanishAmount);
        rerollAmount         += Skills.BonusInt(SkillType.RerollAmount);
        experience           += Skills.Bonus(SkillType.StartXPAmount);
        powerUpShrinkSpeed   += Skills.Bonus(SkillType.IncreaseShrinkSpeed);
        dropChanceMultiplier += Skills.Bonus(SkillType.IncreaseDropChance);
        luckyXpChance        += Skills.Bonus(SkillType.LuckyXpChance);
        goldenHeartChance    += Skills.Bonus(SkillType.GoldenHeartChance);
        luckyReviveChance    += Skills.Bonus(SkillType.LuckyReviveChance);
        moreMixersPercent       += Skills.Bonus(SkillType.MoreMixers);
        mixerAllThreeChance     += Skills.Bonus(SkillType.MixerAllThreeChance);
        mixerAllLegendaryChance += Skills.Bonus(SkillType.MixerAllLegendaryChance);
        freeRerollChance        += Skills.Bonus(SkillType.FreeRerollChance);
        levelUpHealPercent      += Skills.Bonus(SkillType.LevelUpHealPercent);
        charXpBonusPercent      += Skills.Bonus(SkillType.CharXpBonusPercent);
        bossHealthReduction     += Skills.Bonus(SkillType.BossHealthReduction);
        overhealShieldPercent   += Skills.Bonus(SkillType.OverhealShield);
        lastBreathSpeedPercent  += Skills.Bonus(SkillType.LastBreath);

        if (Skills.HasGrant(SkillGrants.Kartograf)) MixerCompass.Ensure();
    }

    public void PlayerHealthReg()
    {
        // Letzter Atem verdoppelt die Regeneration. Ueber Heal, damit volle
        // HP mit Ueberheilung in den Schild gehen.
        Heal(IsLastBreath ? playerHealthReg * 2f : playerHealthReg);
    }

    public void TakeDamage(float damage)
    {
        if (isImmune) return;

        // Kawarimi: waehrend der Wirkung weicht der Spieler allem aus.
        if (IsKawarimi || UnityEngine.Random.value < dodgeChance)
        {
            isImmune = true;
            immunityTimer = immunityDuration;
            DamageNumberController.Instance.CreateDodgeText(transform.position);
            return;
        }

        float finalDamage = damage * Mathf.Pow(0.9f, playerArmor);
        if (finalDamage <= 0f) return;

        TryShockwave();

        // Der Schild aus der Ueberheilung faengt zuerst ab.
        if (shield > 0f)
        {
            float absorbed = Mathf.Min(shield, finalDamage);
            shield -= absorbed;
            finalDamage -= absorbed;

            if (finalDamage <= 0f)
            {
                isImmune = true;
                immunityTimer = immunityDuration;
                UIController.Instance.UpdateHealthSlider();
                return;
            }
        }

        if (finalDamage > 0f)
        {
            hitsoundinterval++;
            isImmune = true;
            immunityTimer = immunityDuration;
            playerHealth -= finalDamage;
            UIController.Instance.UpdateHealthSlider();
            AudioController.Instance.PalyModifiedSound(AudioController.Instance.PlayerHit);
            if (hitFeedback != null) hitFeedback.OnPlayerHit();

            if (hitsoundinterval >= UnityEngine.Random.Range(2, 5))
            {
                AudioController.Instance.PalyModifiedSound(AudioController.Instance.PlayerHit2);
                hitsoundinterval = 0;
            }

            // Erst das Glück aus dem Skilltree, dann der Buff - so bleibt der
            // Buff erhalten, wenn das Glück schon rettet.
            if (playerHealth <= 0 && !TryLuckyRevive() && !TryUseSecondChance())
            {
                gameObject.SetActive(false);
                GameManager.Instance.GameOver();
                WM_UIController.Instance?.UpdateCurrencyText();
                return;
            }

            TryRage();
            TryKawarimi();
            TryStickyRice();
            TryWhirlwind();
        }
    }


    /// <summary>
    /// Faengt einen toedlichen Treffer ab, solange noch eine Ladung des Buffs
    /// "Zweite Chance" uebrig ist. Gibt true zurueck, wenn der Tod verhindert
    /// wurde - dann laeuft TakeDamage ohne GameOver weiter.
    /// </summary>
    private bool TryUseSecondChance()
    {
        if (secondChanceCharges <= 0) return false;

        secondChanceCharges--;
        Revive(secondChanceHealthPercent, secondChanceImmunity, "Second Chance!");

        // Aufgebraucht: der Buff verschwindet fuer den Rest des Laufs und gibt
        // seinen Platz frei, damit ein neuer Buff reinkann.
        if (secondChanceCharges <= 0) RemoveSecondChanceBuff();

        return true;
    }

    /// <summary>
    /// Skilltree "LuckyReviveChance": beim ersten toedlichen Treffer des Laufs
    /// wird einmal gewuerfelt. Egal ob Treffer oder nicht - danach nie wieder.
    /// </summary>
    private bool TryLuckyRevive()
    {
        if (luckyReviveRolled || luckyReviveChance <= 0f) return false;

        luckyReviveRolled = true;
        if (UnityEngine.Random.value >= luckyReviveChance) return false;

        Revive(luckyReviveHealthPercent, luckyReviveImmunity, "Lucky!");
        return true;
    }

    private void Revive(float healthPercent, float immunity, string text)
    {
        playerHealth = Mathf.Max(1f, playerMaxHealth * Mathf.Clamp01(healthPercent));

        // Kurze Unverwundbarkeit, sonst toetet der naechste Kontaktschaden im
        // selben Gegnerpulk sofort wieder.
        isImmune = true;
        immunityTimer = Mathf.Max(immunityDuration, immunity);

        UIController.Instance.UpdateHealthSlider();
        DamageNumberController.Instance?.CreateText(text, transform.position);
        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.LevelUpSound);
        }
    }

    private void RemoveSecondChanceBuff()
    {
        if (activeBuffs == null) return;

        foreach (Weapon buff in activeBuffs)
        {
            if (buff is SecondChance && buff.weaponLevel >= 0)
            {
                // Wie eine von einer Evo ersetzte Waffe: zaehlt nicht mehr als
                // belegter Platz und wird im Lauf nicht wieder angeboten.
                buff.weaponLevel = Weapon.RemovedLevel;
                buff.hasBeenRemoved = true;
            }
        }
    }
    public void GetExperience(int experienceToGet)
    {
        float gained = experienceToGet * experienceMultiplier;
        experience += gained;

        // Dieselben XP gehen auch auf das Konto des Charakters (Charakter-Level
        // -> Skillpunkte). Der Skill "Charakter-XP" legt noch Prozente drauf.
        float charXp = gained * (1f + charXpBonusPercent / 100f);
        Skills.AddXp(charXp);
        if (GameManager.Instance != null) GameManager.Instance.charXpGained += charXp;

        UIController.Instance.UpdateExperienceSlider();
        attractAllXP = false;
    }

    public void LevelUp()
    {
        while (currentLevel >= 1 && currentLevel - 1 < playerLevels.Count
            && experience >= playerLevels[currentLevel - 1] && LevelUpSelectet == true)
        {
            LevelUpSelectet = false;
            experience -= playerLevels[currentLevel - 1];
            playerMaxHealth += 1f;

            // Skilltree "Wissen ist Heilung".
            if (levelUpHealPercent > 0f)
                Heal(playerMaxHealth * levelUpHealPercent / 100f);

            UIController.Instance.UpdateHealthSlider();
            currentLevel++;

            if (currentLevel >= maxLevel)
            {
                currentLevel = maxLevel;
                experience = 0;
                break;
            }

            UIController.Instance.UpdateExperienceSlider();
            RandomWeapon();
            AudioController.Instance.PalySound(AudioController.Instance.LevelUpSound);
            UIController.Instance.LevelUpPanelOpen();
        }

        if(currentLevel == 30)
        {
            Unlocks.Grant(Unlocks.WeaponSlot);
        }
        if(currentLevel == 10)
        {
            Unlocks.Grant(Unlocks.BuffSlot);
        }

        // Wer im Lauf auf zwei Schuss kommt, schaltet den Extra-Shot-Kauf frei.
        // Die Bedingung stand frueher in UnlocksChecker, einem Skript, das an
        // keinem Objekt haengt - der Unlock war deshalb nie erreichbar.
        if (playerShots >= 2)
        {
            Unlocks.Grant(Unlocks.ExtraShot);
        }
    }

    public void RandomWeapon()
    {
        int maxWeaponSlots = WeaponSlots;
        int maxBuffSlots = BuffSlots;

        List<Weapon> availableWeapons = new List<Weapon>();
        List<Weapon> evoRewards = new List<Weapon>();

        foreach (var recipe in EvoCombinations)
        {
            if (recipe.EvoWeapon == null) continue;

            bool w1Maxed = recipe.RequiredWeapon1 != null && recipe.RequiredWeapon1.weaponLevel >= recipe.RequiredWeapon1.maxweaponLevel;
            bool w2Maxed = recipe.RequiredWeapon2 != null && recipe.RequiredWeapon2.weaponLevel >= recipe.RequiredWeapon2.maxweaponLevel;

            if (w1Maxed && w2Maxed)
            {
                evoRewards.Add(recipe.EvoWeapon);
            }
        }

        int currentEvoCount = activeEvos.Count(e => e.weaponLevel >= 0);
        int remainingEvoSlots = EvoSlots - currentEvoCount;

        if (evoRewards.Count > 0 && remainingEvoSlots > 0)
        {
            availableWeapons = evoRewards;
        }
        else
        {
            availableWeapons = activeWeapon
                .Where(w =>
                    w.weaponLevel >= -2 &&
                    w.weaponLevel < w.maxweaponLevel &&
                    IsWeaponUnlocked(w) &&
                    !w.hasBeenRemoved)
                .Concat(activeBuffs.Where(b =>
                    b.weaponLevel < b.maxweaponLevel &&
                    IsBuffUnlocked(b) &&
                    !b.hasBeenRemoved))
                .ToList();
            var activeWeaponsOnly = activeWeapon
                .Where(w => w.weaponLevel >= 0 && !w.hasBeenRemoved)
                .Concat(activeEvos.Where(e => e.weaponLevel >= 0 && !e.hasBeenRemoved))
                .ToList();

            var activeBuffsOnly = activeBuffs
                .Where(b => b.weaponLevel >= 0 && !b.hasBeenRemoved)
                .ToList();

            if (activeWeaponsOnly.Count >= maxWeaponSlots && activeBuffsOnly.Count <= maxBuffSlots - 1)
            {
                availableWeapons = activeWeapon
                    .Where(w =>
                        w.weaponLevel >= 0 &&
                        w.weaponLevel < w.maxweaponLevel &&
                        IsWeaponUnlocked(w) &&
                        !w.hasBeenRemoved)
                    .Concat(activeBuffs.Where(b =>
                        b.weaponLevel < b.maxweaponLevel &&
                        IsBuffUnlocked(b)&&
                        !b.hasBeenRemoved))
                    .ToList();
            }
            else if (activeBuffsOnly.Count >= maxBuffSlots && activeWeaponsOnly.Count <= maxWeaponSlots - 1)
            {
                availableWeapons = activeWeapon
                    .Where(w =>
                        w.weaponLevel < w.maxweaponLevel &&
                        IsWeaponUnlocked(w)&&
                        !w.hasBeenRemoved)
                    .Concat(activeBuffs.Where(b =>
                        b.weaponLevel >= 0 &&
                        b.weaponLevel < b.maxweaponLevel &&
                        IsBuffUnlocked(b)&&
                        !b.hasBeenRemoved))
                    .ToList();
            }
            else if (activeBuffsOnly.Count >= maxBuffSlots && activeWeaponsOnly.Count >= maxWeaponSlots)
            {
                availableWeapons = activeWeapon
                    .Where(w =>
                        w.weaponLevel >= 0 &&
                        w.weaponLevel < w.maxweaponLevel &&
                        IsWeaponUnlocked(w)&&
                        !w.hasBeenRemoved)
                    .Concat(activeBuffs.Where(b =>
                        b.weaponLevel >= 0 &&
                        b.weaponLevel < b.maxweaponLevel &&
                        IsBuffUnlocked(b) &&
                        !b.hasBeenRemoved))
                    .ToList();
            }
            if (availableWeapons.Count == 0)
            {
                availableWeapons = maxLevelStuff.ToList();
            }
        }

        availableWeapons = availableWeapons.OrderBy(x => rng.Next()).ToList();

        UIController.Instance.currentLevelUpWeapons = availableWeapons.ToList();

        for (int i = 0; i < UIController.Instance.levelUpButtons.Length; i++)
        {
            if (i < availableWeapons.Count)
            {
                var btn = UIController.Instance.levelUpButtons[i];
                btn.gameObject.SetActive(true);
                btn.ActivateButton(availableWeapons[i]);
            }
            else
            {
                UIController.Instance.levelUpButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public List<Sprite> GetEvoPartnerIcons(Weapon weapon)
    {
        var partnerIcons = new List<Sprite>();
        string wName = weapon.name;

        foreach (var recipe in EvoCombinations)
        {
            bool w1Active = recipe.RequiredWeapon1 != null && recipe.RequiredWeapon1.weaponLevel >= 0;
            bool w2Active = recipe.RequiredWeapon2 != null && recipe.RequiredWeapon2.weaponLevel >= 0;

            if (recipe.RequiredWeapon1 != null && recipe.RequiredWeapon1.name == wName && (w1Active || w2Active))
            {
                if (recipe.RequiredWeapon2 != null && recipe.RequiredWeapon2.weaponIcon != null)
                    partnerIcons.Add(recipe.RequiredWeapon2.weaponIcon);
            }
            else if (recipe.RequiredWeapon2 != null && recipe.RequiredWeapon2.name == wName && (w1Active || w2Active))
            {
                if (recipe.RequiredWeapon1 != null && recipe.RequiredWeapon1.weaponIcon != null)
                    partnerIcons.Add(recipe.RequiredWeapon1.weaponIcon);
            }
        }

        return partnerIcons.Distinct().ToList();
    }

    private bool IsWeaponUnlocked(Weapon weapon)
    {
        if (weapon == null) return false;

        // 1. Startwaffe ist immer freigeschaltet
        if (activeWeapon != null && activeWeapon.Length > 0)
        {
            int startIdx = Mathf.Clamp(Shop.RunStartWeapon, 0, activeWeapon.Length - 1);
            string startWeaponID = activeWeapon[startIdx]?.weaponID;
            if (startWeaponID == weapon.weaponID)
                return true;
        }

        // 2. Der Verteiler von der Werkbank. Solange der Spieler dort nichts
        //    uebernommen hat, sagt Loadout zu allem ja - ein alter Spielstand
        //    verhaelt sich also genau wie vorher. Die Startwaffe oben kommt
        //    bewusst vor dieser Schranke: die bekommt man sowieso, und ohne
        //    Aufstiegsmoeglichkeit waere sie ein toter Slot.
        if (!Loadout.AllowsInRun(weapon.weaponID))
            return false;

        // 3. Standardwaffen → immer freigeschaltet
        HashSet<string> defaultUnlockedWeapons = new HashSet<string>
        {
            "coffee_pool",
            "cookie_saw",
            "butterblast",
            "jam_jar",
            "void_spike",
            "fire_ball",
            "boomerang",
            // Neue Waffen: bewusst ohne Shop-Eintrag sofort verfuegbar.
            "crumb_trail",
            "vortex",
            "turret",
            "sword_slash",
            "salad_fan"
        };

        if (defaultUnlockedWeapons.Contains(weapon.weaponID))
            return true;

        // 4. Alles Weitere muss im Shop gekauft sein. Welche Waffe zu welchem
        //    Shop-Eintrag gehört, steht am Eintrag selbst (unlocksWeapon in Shop.cs) -
        //    hier stand früher eine zweite, handgepflegte Tabelle.
        return Shop.IsWeaponUnlocked(weapon.weaponID);
    }

    private bool IsBuffUnlocked(Weapon buff)
    {
        if (buff == null) return false;

        // Der Verteiler von der Werkbank, dieselbe Regel wie bei den Waffen.
        if (!Loadout.AllowsInRun(buff.weaponID))
            return false;

        // Standardmäßig immer freigeschaltete Buffs
        HashSet<string> defaultUnlockedBuffs = new HashSet<string>
        {
            "buff_max_hp",
            "buff_regeneration",
            "buff_move_speed",
            "buff_pickup_range",
            "buff_armor",
            "buff_dodge",
            // Neue Buffs: ebenfalls sofort verfuegbar.
            "buff_cooldown",
            "buff_duration",
            "buff_glass_cannon",
            "buff_second_chance"
        };

        if (defaultUnlockedBuffs.Contains(buff.weaponID))
            return true;

        // Freischaltbare Buffs: dieselbe Regel wie bei den Waffen.
        return Shop.IsWeaponUnlocked(buff.weaponID);
    }



}
