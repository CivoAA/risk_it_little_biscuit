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
        // Absicherung: MapsManager und extraData müssen vorhanden sein
        if (MapsManager.Instance == null || MapsManager.Instance.extraData == null || MapsManager.Instance.extraData.Length < 10)
        {
            Debug.LogWarning("PlayerController.StartStats: MapsManager/extraData fehlt oder ist zu kurz – Start-Extras werden übersprungen.");
        }
        else
        {
            int startWeaponIndex = Mathf.Clamp((int)MapsManager.Instance.extraData[1], 0, activeWeapon.Length - 1); // in LevelPoint hinterlegt
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

            var ex = MapsManager.Instance.extraData;

            // ✅ neue Shop-Extras (2..9)
            GameManager.Instance.currencyGainMultiplire += ex[2]; // Currency Gain
            rerollAmount += Mathf.RoundToInt(ex[3]); // Reroll
            banishAmount += Mathf.RoundToInt(ex[4]); // Banish
            experience += ex[5]; // Start XP
            powerUpShrinkSpeed += ex[6]; // Shrink Speed
            BuffSlots += Mathf.RoundToInt(ex[7]); // Buff Slot
            WeaponSlots += Mathf.RoundToInt(ex[8]); // Weapon Slot
            EvoSlots += Mathf.RoundToInt(ex[9]); // Evo Slot
        }

        if (GameManager.Instance != null && SkillSaveManager.Instance != null)
        {
            GameManager.Instance.skillCurrencyBeforeGame = SkillSaveManager.Instance.currentData.skillCurrency;
        }
        AchievementManager.Instance?.UnlockAchievement("First_Game");

        if (SkillStatsManager.Instance != null)
        {
            playerMaxHealth += SkillStatsManager.Instance.bonusMaxHealth;
            playerHealth = playerMaxHealth;
            moveSpeed += SkillStatsManager.Instance.bonusSpeed;
            experienceMultiplier += SkillStatsManager.Instance.bonusXpMultiplier;
            playerHealthReg += SkillStatsManager.Instance.bonusHealthReg;
            playerShots += SkillStatsManager.Instance.bonusExtraShots;
            playerArmor += SkillStatsManager.Instance.bonusArmor;
            AOERange += SkillStatsManager.Instance.bonusAOERange;
            lifeStealChance += SkillStatsManager.Instance.bonusLifeSteal;
            damageMultiplier += SkillStatsManager.Instance.bonusDamage;
            critDamage += SkillStatsManager.Instance.bonusCritDamage;
            critChance += SkillStatsManager.Instance.bonusCritChance;
            dodgeChance += SkillStatsManager.Instance.bonusDodgeChance;
            pickupRange += SkillStatsManager.Instance.bonusPickupRange;
            luck += SkillStatsManager.Instance.bonusLuck;
            banishAmount += Mathf.RoundToInt(SkillStatsManager.Instance.bonusBanish);
            rerollAmount += Mathf.RoundToInt(SkillStatsManager.Instance.bonusReroll);
            experience += SkillStatsManager.Instance.startXPAmount;
            powerUpShrinkSpeed += SkillStatsManager.Instance.shrinkSpeed;
            WeaponSlots += Mathf.RoundToInt(SkillStatsManager.Instance.weaponSlots);
            BuffSlots += Mathf.RoundToInt(SkillStatsManager.Instance.buffSlots);
            EvoSlots += Mathf.RoundToInt(SkillStatsManager.Instance.evoSlots);
        }
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

            if (playerHealth <= 0)
            {
                gameObject.SetActive(false);
                GameManager.Instance.GameOver();
                WM_UIController.Instance.UpdateCurrencyText();
            }
        }
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
            UnlockManager.Instance.Unlock("unlock_weapon_slot");
        }
        if(currentLevel == 10)
        {
            UnlockManager.Instance.Unlock("unlock_buff_slot");
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

        // 1. Startwaffe ist immer freigeschaltet (mit Absicherung gegen fehlende Daten)
        if (MapsManager.Instance != null && MapsManager.Instance.extraData != null && MapsManager.Instance.extraData.Length > 1)
        {
            int startIdx = Mathf.Clamp(Mathf.RoundToInt(MapsManager.Instance.extraData[1]), 0, activeWeapon.Length - 1);
            string startWeaponID = activeWeapon[startIdx]?.weaponID;
            if (startWeaponID == weapon.weaponID)
                return true;
        }

        // 2. Standardwaffen → immer freigeschaltet
        HashSet<string> defaultUnlockedWeapons = new HashSet<string>
        {
            "coffee_pool",
            "cookie_saw",
            "butterblast",
            "jam_jar",
            "void_spike",
            "fire_ball",
            "boomerang"
        };

        if (defaultUnlockedWeapons.Contains(weapon.weaponID))
            return true;

        // 3. Mapping WeaponID → extraData Index (nur für Upgrades)
        Dictionary<string, int> weaponToIndex = new Dictionary<string, int>
        {
            { "boba_gun",        10 },
            { "shurikookie",     11 },
            { "spike_fork",      12 },
            { "deathstrike",     13 },
            { "celestial_star",  14 },
            { "blade_swarm",     15 },
            { "candy_bomb",      16 },
            { "time_laser",      17 },
        };

        if (!weaponToIndex.TryGetValue(weapon.weaponID, out int index))
            return false;

        return MapsManager.Instance.extraData.Length > index && MapsManager.Instance.extraData[index] >= 1f;
    }

    private bool IsBuffUnlocked(Weapon buff)
    {
        if (buff == null) return false;

        // Standardmäßig immer freigeschaltete Buffs
        HashSet<string> defaultUnlockedBuffs = new HashSet<string>
        {
            "buff_max_hp",
            "buff_regeneration",
            "buff_move_speed",
            "buff_pickup_range",
            "buff_armor",
            "buff_dodge"
        };

        if (defaultUnlockedBuffs.Contains(buff.weaponID))
            return true;

        // Freischaltbare Buffs → Mapping
        Dictionary<string, int> buffToIndex = new Dictionary<string, int>
        {
            { "buff_xp_gain",       18 },
            { "buff_currency",      19 },
            { "buff_life_steal",    20 },
            { "buff_luck",          21 },
            { "buff_extra_shot",    22 },
            { "buff_aoe_range",     23 },
            { "buff_damage",        24 },
            { "buff_crit_chance",   25 },
            { "buff_crit_damage",   26 },
        };

        if (!buffToIndex.TryGetValue(buff.weaponID, out int index)) return false;

        return MapsManager.Instance.extraData.Length > index && MapsManager.Instance.extraData[index] >= 1f;
    }



}
