using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ItemMenuBuffs : MonoBehaviour
{
    /// <summary>Die Buff-Slots in Anzeigereihenfolge, nach jedem UpdateUI neu - fuer das GameHud.</summary>
    public readonly List<ItemSlotInfo> CurrentSlots = new List<ItemSlotInfo>();

    private readonly List<Sprite> shownBuffs = new List<Sprite>();

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
    public CooldownReduction cooldownReduction;
    public DurationBuff durationBuff;
    public GlassCannon glassCannon;
    public SecondChance secondChance;

    /// <summary>
    /// Wie GameObject.Find(...).GetComponent&lt;T&gt;(), aber ohne
    /// NullReferenceException, wenn das Objekt (noch) nicht in der Szene liegt.
    /// </summary>
    private static T FindInScene<T>(string objectName) where T : Component
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("ItemMenuBuffs: " + objectName + " liegt nicht in der Szene – Eintrag wird übersprungen.");
            return null;
        }

        return go.GetComponent<T>();
    }

    void Start()
    {
        armor             = FindInScene<Armor>("Armor");
        buffs             = FindInScene<Buffs>("Max HP");
        experienceGain    = FindInScene<ExperienceGain>("Experience Gain");
        hPReg             = FindInScene<HPReg>("HP Regeneration");
        moveSpeedBuff     = FindInScene<MoveSpeedBuff>("Move Speed");
        currencyGain      = FindInScene<CurrencyGain>("Currency Gain");
        AOERange          = FindInScene<AOERange>("AOE Range");
        pickupRange       = FindInScene<PickupRange>("Pickup Range");
        lifeSteal         = FindInScene<LifeSteal>("Life Steal");
        extraShot         = FindInScene<ExtraShot>("Extra Shot");
        critChance        = FindInScene<CritChance>("Crit Chance");
        critDamage        = FindInScene<CritDamage>("Crit Damage");
        dodgeChance       = FindInScene<DodgeChance>("Dodge Chance");
        damage            = FindInScene<Damage>("Damage");
        luck              = FindInScene<Luck>("Luck");
        cooldownReduction = FindInScene<CooldownReduction>("Cooldown");
        durationBuff      = FindInScene<DurationBuff>("Duration");
        glassCannon       = FindInScene<GlassCannon>("Glass Cannon");
        secondChance      = FindInScene<SecondChance>("Second Chance");
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
        // Fehlende Buffs fallen hier raus, statt die ganze Liste zu sprengen.
        var weapons = new List<(int level, int maxLevel, Sprite sprite)>();
        var collected = new HashSet<Weapon>();

        void Collect(Weapon buff)
        {
            if (buff == null || !collected.Add(buff)) return;
            weapons.Add((buff.weaponLevel, buff.maxweaponLevel, buff.weaponIcon));
        }

        Collect(armor);
        Collect(buffs);
        Collect(experienceGain);
        Collect(hPReg);
        Collect(moveSpeedBuff);
        Collect(AOERange);
        Collect(pickupRange);
        Collect(lifeSteal);
        Collect(critChance);
        Collect(critDamage);
        Collect(extraShot);
        Collect(dodgeChance);
        Collect(damage);
        Collect(luck);
        Collect(currencyGain);
        Collect(cooldownReduction);
        Collect(durationBuff);
        Collect(glassCannon);
        Collect(secondChance);

        // Alles, was oben (noch) nicht namentlich steht - z.B. Cursed Sugar.
        // Neue Buffs tauchen damit von selbst oben links auf.
        PlayerController player = PlayerController.Instance;
        if (player != null && player.activeBuffs != null)
            foreach (Weapon b in player.activeBuffs) Collect(b);

        // aktive Buffs prüfen - die Reihenfolge merkt sich shownBuffs, nicht
        // die alten Slot-Bilder: davon liegen nur 4 in der Szene, im Lauf
        // gibt es aber bis zu 6 Buffslots (Shop + Tofu).
        shownBuffs.RemoveAll(sprite => !weapons.Exists(w => w.sprite == sprite && w.level >= 0));

        // neue hinzufügen
        foreach (var weapon in weapons)
        {
            if (weapon.level >= 0 && !shownBuffs.Contains(weapon.sprite))
            {
                shownBuffs.Add(weapon.sprite);
            }
        }

        // Level- & Max-Level je Slot fuers GameHud
        CurrentSlots.Clear();
        foreach (var sprite in shownBuffs)
        {
            var w = weapons.Find(x => x.sprite == sprite);
            CurrentSlots.Add(new ItemSlotInfo { Icon = sprite, Level = w.level, MaxLevel = w.maxLevel });
        }

        // alte Slots und Leveltexte aktualisieren
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i < CurrentSlots.Count)
            {
                var sprite = CurrentSlots[i].Icon;
                int level = CurrentSlots[i].Level;
                int maxLevel = CurrentSlots[i].MaxLevel;
                weaponSlots[i].sprite = sprite;

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
