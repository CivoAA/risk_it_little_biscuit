using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ItemMenuBuffs : MonoBehaviour
{
    public UnityEngine.UI.Image[] weaponSlots;
    public List<TMP_Text> levelTexts = new List<TMP_Text>();
    public List<GameObject> weaponSlot = new List<GameObject>();
    public Sprite defaultSprite;
    public Armor armor;
    public Buffs buffs;
    public ExperienceGain experienceGain;
    public HPReg hPReg;
    public MoveSpeedBuff moveSpeedBuff;
    public CurrencyGain currencyGain;
    public AOERange AOERange;
    public PickupRange pickupRange;
    public LifeSteal lifeSteal;
    public ExtraShot extraShot;
    public CritChance critChance;
    public CritDamage critDamage;
    public DodgeChance dodgeChance;
    public Damage damage;
    public Luck luck;

    void Start()
    {
        armor           = GameObject.Find("Armor").GetComponent<Armor>();
        buffs           = GameObject.Find("Max HP").GetComponent<Buffs>();
        experienceGain  = GameObject.Find("Experience Gain").GetComponent<ExperienceGain>();
        hPReg           = GameObject.Find("HP Regeneration").GetComponent<HPReg>();
        moveSpeedBuff   = GameObject.Find("Move Speed").GetComponent<MoveSpeedBuff>();
        currencyGain    = GameObject.Find("Currency Gain").GetComponent<CurrencyGain>();
        AOERange        = GameObject.Find("AOE Range").GetComponent<AOERange>();
        pickupRange     = GameObject.Find("Pickup Range").GetComponent<PickupRange>();
        lifeSteal       = GameObject.Find("Life Steal").GetComponent<LifeSteal>();
        extraShot       = GameObject.Find("Extra Shot").GetComponent<ExtraShot>();
        critChance      = GameObject.Find("Crit Chance").GetComponent<CritChance>();
        critDamage      = GameObject.Find("Crit Damage").GetComponent<CritDamage>();
        dodgeChance     = GameObject.Find("Dodge Chance").GetComponent<DodgeChance>();
        damage          = GameObject.Find("Damage").GetComponent<Damage>();
        luck            = GameObject.Find("Luck").GetComponent<Luck>();
    }

    void Update()
    {
        int activeSlots = PlayerController.Instance.BuffSlots;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            bool shouldBeActive = i < activeSlots;
            if (weaponSlot[i].activeSelf != shouldBeActive)
                weaponSlot[i].SetActive(shouldBeActive);
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        var weapons = new (int level, int maxLevel, Sprite sprite)[]
        {
            (armor.weaponLevel,            armor.maxweaponLevel,           armor.weaponIcon),
            (buffs.weaponLevel,            buffs.maxweaponLevel,           buffs.weaponIcon),
            (experienceGain.weaponLevel,   experienceGain.maxweaponLevel,  experienceGain.weaponIcon),
            (hPReg.weaponLevel,            hPReg.maxweaponLevel,           hPReg.weaponIcon),
            (moveSpeedBuff.weaponLevel,    moveSpeedBuff.maxweaponLevel,   moveSpeedBuff.weaponIcon),
            (AOERange.weaponLevel,         AOERange.maxweaponLevel,        AOERange.weaponIcon),
            (pickupRange.weaponLevel,      pickupRange.maxweaponLevel,     pickupRange.weaponIcon),
            (lifeSteal.weaponLevel,        lifeSteal.maxweaponLevel,       lifeSteal.weaponIcon),
            (critChance.weaponLevel,       critChance.maxweaponLevel,      critChance.weaponIcon),
            (critDamage.weaponLevel,       critDamage.maxweaponLevel,      critDamage.weaponIcon),
            (extraShot.weaponLevel,        extraShot.maxweaponLevel,       extraShot.weaponIcon),
            (dodgeChance.weaponLevel,      dodgeChance.maxweaponLevel,     dodgeChance.weaponIcon),
            (damage.weaponLevel,           damage.maxweaponLevel,          damage.weaponIcon),
            (luck.weaponLevel,             luck.maxweaponLevel,            luck.weaponIcon),
            (currencyGain.weaponLevel,     currencyGain.maxweaponLevel,    currencyGain.weaponIcon)
        };

        List<Sprite> currentWeapons = new List<Sprite>();

        // aktive Buffs prüfen
        foreach (var slot in weaponSlots)
        {
            if (slot.sprite != defaultSprite)
            {
                bool stillActive = false;
                foreach (var weapon in weapons)
                {
                    if (weapon.sprite == slot.sprite && weapon.level >= 0)
                    {
                        stillActive = true;
                        break;
                    }
                }

                if (stillActive)
                    currentWeapons.Add(slot.sprite);
            }
        }

        // neue hinzufügen
        foreach (var weapon in weapons)
        {
            if (weapon.level >= 0 && !currentWeapons.Contains(weapon.sprite))
            {
                currentWeapons.Add(weapon.sprite);
            }
        }

        // Slots und Leveltexte aktualisieren
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i < currentWeapons.Count)
            {
                var sprite = currentWeapons[i];
                weaponSlots[i].sprite = sprite;

                // Level- & Max-Level finden
                int level = -1;
                int maxLevel = -1;

                foreach (var w in weapons)
                {
                    if (w.sprite == sprite)
                    {
                        level = w.level;
                        maxLevel = w.maxLevel;
                        break;
                    }
                }

                // 🔹 Text aktualisieren
                if (i < levelTexts.Count)
                {
                    if (level >= 0)
                    {
                        // Wenn maxLevel == 0 -> automatisch "M"
                        // oder Level == Max-Level -> auch "M"
                        if (maxLevel == 0 || level >= maxLevel)
                            levelTexts[i].text = "M";
                        else
                            levelTexts[i].text = (level + 1).ToString();
                    }
                    else
                    {
                        levelTexts[i].text = "";
                    }
                }
            }
            else
            {
                weaponSlots[i].sprite = defaultSprite;

                if (i < levelTexts.Count)
                    levelTexts[i].text = "";
            }
        }
    }
}
