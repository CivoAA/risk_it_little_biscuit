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

    /// <summary>Cooldown-Faktor inklusive Untergrenze - Waffen lesen nur diesen Wert.</summary>
    public float CooldownMultiplier
    {
        get { return Mathf.Max(minCooldownMultiplier, cooldownMultiplier); }
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
        // Bei pausiertem Spiel (Level-Up-Panel, Pause-Menue, Gamba, ...) laufen
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

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector3(playerMoveDirection.x * moveSpeed, playerMoveDirection.y * moveSpeed);
    }

    public void StartStats()
    {
        if (activeWeapon != null && activeWeapon.Length > 0)
        {
            int startWeaponIndex = Mathf.Clamp(Shop.RunStartWeapon, 0, activeWeapon.Length - 1);
            Weapon startWeapon = activeWeapon[startWeaponIndex];
            startWeapon.weaponLevel = 0;
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
            GameManager.Instance.skillCurrencyBeforeGame = Skills.Currency;
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
        WeaponSlots          += Skills.BonusInt(SkillType.IncreaseWeaponSlots);
        BuffSlots            += Skills.BonusInt(SkillType.IncreaseBuffSlots);
        EvoSlots             += Skills.BonusInt(SkillType.IncreaseEvoSlots);
    }

    public void PlayerHealthReg()
    {
        if (playerHealth >= playerMaxHealth)
        {
            playerHealth = playerMaxHealth;
        }
        else
        {
            playerHealth += playerHealthReg;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isImmune) return;

        if (UnityEngine.Random.value < dodgeChance)
        {
            isImmune = true;
            immunityTimer = immunityDuration;
            DamageNumberController.Instance.CreateDodgeText(transform.position);
            return;
        }

        float finalDamage = damage * Mathf.Pow(0.9f, playerArmor);

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

            if (playerHealth <= 0 && !TryUseSecondChance())
            {
                gameObject.SetActive(false);
                GameManager.Instance.GameOver();
                WM_UIController.Instance?.UpdateCurrencyText();
            }
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
        playerHealth = Mathf.Max(1f, playerMaxHealth * Mathf.Clamp01(secondChanceHealthPercent));

        // Kurze Unverwundbarkeit, sonst toetet der naechste Kontaktschaden im
        // selben Gegnerpulk sofort wieder.
        isImmune = true;
        immunityTimer = Mathf.Max(immunityDuration, secondChanceImmunity);

        UIController.Instance.UpdateHealthSlider();
        DamageNumberController.Instance?.CreateText("Second Chance!", transform.position);
        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.LevelUpSound);
        }

        return true;
    }
    public void GetExperience(int experienceToGet)
    {
        experience += experienceToGet * experienceMultiplier;
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

    public void RandomWeapon2()
    {
        int maxWeaponSlots = WeaponSlots;

        var lowLevelActiveWeapons = activeWeapon
            .Where(w => w.weaponLevel >= 0 && w.weaponLevel <= 3 && w.weaponLevel < w.maxweaponLevel && IsWeaponUnlocked(w))
            .ToList();

        var activeWeaponsOnly = activeWeapon
            .Where(w => w.weaponLevel >= 0 && w.weaponLevel < w.maxweaponLevel && IsWeaponUnlocked(w))
            .ToList();

        var inactiveWeapons = activeWeapon
            .Where(w => w.weaponLevel < 0 && w.weaponLevel < w.maxweaponLevel && IsWeaponUnlocked(w))
            .ToList();

        var activeWeaponsAndEvos = activeWeapon
            .Where(w => w.weaponLevel >= 0)
            .Concat(activeEvos.Where(e => e.weaponLevel >= 0))
            .ToList();

        List<Weapon> availableWeapons;

        if (lowLevelActiveWeapons.Count > 0)
        {
            availableWeapons = lowLevelActiveWeapons;
        }
        else if (activeWeaponsOnly.Count > 0)
        {
            availableWeapons = activeWeaponsOnly;
        }
        else if (activeWeaponsAndEvos.Count < maxWeaponSlots && inactiveWeapons.Count > 0)
        {
            availableWeapons = inactiveWeapons;
        }
        else if (activeWeaponsAndEvos.Count >= maxWeaponSlots)
        {
            availableWeapons = maxLevelStuff.ToList();
        }
        else
        {
            Debug.Log("Kritischer Fehler beim Waffenzusortieren der Lootchest im PlayerController Script");
            availableWeapons = new List<Weapon>();
        }

        // 🔀 Shuffle
        availableWeapons = availableWeapons.OrderBy(x => rng.Next()).ToList();

        // 📦 1 Button aktivieren
        for (int i = 0; i < 1; i++)
        {
            if (i < availableWeapons.Count)
            {
                UIController.Instance.GambaButtons.ActivateButton(availableWeapons[i]);
            }
            else
            {
                UIController.Instance.GambaButtons.gameObject.SetActive(false);
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
            "turret"
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
