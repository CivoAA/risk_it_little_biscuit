using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class ItemMenu : MonoBehaviour
{
    public UnityEngine.UI.Image[] weaponSlots;
    public List<TMP_Text> levelTexts = new List<TMP_Text>();
    public List<GameObject> weaponSlot = new List<GameObject>();

    [Header("Slot-Hintergründe")]
    public Sprite normalSlotBackground;
    public Sprite evoSlotBackground;

    [Header("Waffen-und-Evo-Icons")]
    public Sprite defaultSprite;
    // Waffen
    public BobaGun bobaGun;
    public AreaWeapon areaWeapon;
    public AreaWeaponJamJar areaWeaponJamJar;
    public BobaWeapon bobaWeapon;
    public ShurikenWeapon shurikenWeapon;
    public KeckssaegeWeapon keckssaegeWeapon;
    public Spikefork SpikeFork;
    public Deathstrike deathstrike;
    public RandomVoidSpike RandomVoidSpike;
    public CelestialStar CelestialStar;
    public FireBall FireBall;
    public BladeSwarm BladeSwarm;
    public CandyBomb candyBomb;
    public Boomerang boomerang;
    public TimeLaser timeLaser;
    public CrumbTrail crumbTrail;
    public Vortex vortex;
    public Turret turret;

    // Evos
    public BobaEvo BobaSawEvo;
    public ShuriBlastEvo ShuriBlastEvo;
    public ExplosiveStarEvo ExplosiveStarEvo;
    public BloodyFork BloodyFork;
    public BladeStormEvo BladeStormEvo;
    public BoomerangEvo boomerangEvo;
    public BombSawEvo bombSawEvo;
    public StickyShatterEvo stickyShatterEvo;

    /// <summary>
    /// Wie GameObject.Find(...).GetComponent&lt;T&gt;(), aber ohne
    /// NullReferenceException, wenn das Objekt (noch) nicht in der Szene liegt.
    /// Sonst legt eine einzige fehlende Waffe das komplette Item-Menue lahm,
    /// bevor sie überhaupt in der Szene angelegt ist.
    /// </summary>
    private static T FindInScene<T>(string objectName) where T : Component
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("ItemMenu: " + objectName + " liegt nicht in der Szene – Eintrag wird übersprungen.");
            return null;
        }

        return go.GetComponent<T>();
    }

    void Start()
    {
        // Waffen finden
        bobaGun = FindInScene<BobaGun>("Boba Gun");
        areaWeaponJamJar = FindInScene<AreaWeaponJamJar>("Throwing Jam Jar");
        shurikenWeapon = FindInScene<ShurikenWeapon>("Shurikookie");
        keckssaegeWeapon = FindInScene<KeckssaegeWeapon>("CookieSaw");
        bobaWeapon = FindInScene<BobaWeapon>("Butterblast");
        areaWeapon = FindInScene<AreaWeapon>("Coffe Pool");
        SpikeFork = FindInScene<Spikefork>("Spike Fork");
        deathstrike = FindInScene<Deathstrike>("Deathstrike");
        RandomVoidSpike = FindInScene<RandomVoidSpike>("Void Spike");
        CelestialStar = FindInScene<CelestialStar>("Celestial Star");
        FireBall = FindInScene<FireBall>("Fire Ball");
        BladeSwarm = FindInScene<BladeSwarm>("Blade Swarm");
        candyBomb = FindInScene<CandyBomb>("Candy Bomb");
        boomerang = FindInScene<Boomerang>("Boomerang");
        timeLaser = FindInScene<TimeLaser>("Time Laser");
        crumbTrail = FindInScene<CrumbTrail>("Crumb Trail");
        vortex = FindInScene<Vortex>("Vortex");
        turret = FindInScene<Turret>("Turret");

        // Evos
        ShuriBlastEvo = FindInScene<ShuriBlastEvo>("Shuri Blast Evo");
        BobaSawEvo = FindInScene<BobaEvo>("Boba Saw Evo");
        ExplosiveStarEvo = FindInScene<ExplosiveStarEvo>("Explosive Star Evo");
        BloodyFork = FindInScene<BloodyFork>("Bloody Fork Evo");
        BladeStormEvo = FindInScene<BladeStormEvo>("Blade Storm Evo");
        boomerangEvo = FindInScene<BoomerangEvo>("Boomerang Evo");
        bombSawEvo = FindInScene<BombSawEvo>("Bomb Saw Evo");
        stickyShatterEvo = FindInScene<StickyShatterEvo>("Sticky Shatter Evo");

        UpdateUI();
    }

    void Update()
    {
        int activeSlots = PlayerController.Instance.WeaponSlots;
        int evoSlots = PlayerController.Instance.EvoSlots;

        for (int i = 0; i < weaponSlot.Count; i++)
        {
            bool shouldBeActive = i < activeSlots;
            weaponSlot[i].SetActive(shouldBeActive);

            // Versuche das Image am Slot zu holen
            Image backgroundImage = weaponSlot[i].GetComponent<Image>();
            if (backgroundImage == null) continue;

            // Wähle Evo- oder Normal-Sprite
            if (i < evoSlots)
                backgroundImage.sprite = evoSlotBackground;
            else
                backgroundImage.sprite = normalSlotBackground;
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        // Schritt 1: Waffen + Evos sammeln (mit isEvo). Fehlende Objekte fallen
        // dabei einfach raus, statt die ganze Liste zu sprengen.
        var allWeapons = new List<(int level, int maxLevel, Sprite sprite, bool isEvo)>();

        void Collect(Weapon weapon, bool isEvo)
        {
            if (weapon == null) return;
            allWeapons.Add((weapon.weaponLevel, weapon.maxweaponLevel, weapon.weaponIcon, isEvo));
        }

        Collect(shurikenWeapon, false);
        Collect(areaWeaponJamJar, false);
        Collect(bobaWeapon, false);
        Collect(bobaGun, false);
        Collect(areaWeapon, false);
        Collect(keckssaegeWeapon, false);
        Collect(SpikeFork, false);
        Collect(deathstrike, false);
        Collect(RandomVoidSpike, false);
        Collect(CelestialStar, false);
        Collect(FireBall, false);
        Collect(BladeSwarm, false);
        Collect(candyBomb, false);
        Collect(boomerang, false);
        Collect(timeLaser, false);
        Collect(crumbTrail, false);
        Collect(vortex, false);
        Collect(turret, false);

        // 🔥 Evo-Waffen
        Collect(ShuriBlastEvo, true);
        Collect(ExplosiveStarEvo, true);
        Collect(BloodyFork, true);
        Collect(BobaSawEvo, true);
        Collect(BladeStormEvo, true);
        Collect(boomerangEvo, true);
        Collect(bombSawEvo, true);
        Collect(stickyShatterEvo, true);

        // Schritt 2: Bereits angezeigte Waffen
        List<Sprite> currentSprites = new List<Sprite>();
        foreach (var slot in weaponSlots)
        {
            if (slot.sprite != defaultSprite)
            {
                currentSprites.Add(slot.sprite);
            }
        }

        // Schritt 3: Neu aktive Waffen (level >= 0)
        List<Sprite> activeWeapons = new List<Sprite>();
        List<(Sprite sprite, int level, int maxLevel)> weaponData = new List<(Sprite, int, int)>();

        foreach (var weapon in allWeapons)
        {
            if (weapon.level >= 0)
            {
                activeWeapons.Add(weapon.sprite);
                weaponData.Add((weapon.sprite, weapon.level, weapon.maxLevel));
            }
        }

        // Schritt 4: Neue Waffen erkennen (nicht in currentSprites)
        List<Sprite> newWeapons = new List<Sprite>();
        foreach (var sprite in activeWeapons)
        {
            if (!currentSprites.Contains(sprite))
                newWeapons.Add(sprite);
        }

        // Schritt 5: Entfernte Waffen erkennen (nicht mehr vorhanden)
        currentSprites.RemoveAll(sprite => !activeWeapons.Contains(sprite));

        // Schritt 6: Neue Waffen hinzufügen
        foreach (var newSprite in newWeapons)
        {
            var weaponInfo = allWeapons.Find(w => w.sprite == newSprite);
            bool isEvo = weaponInfo.isEvo;

            if (isEvo)
            {
                // 🔥 Evo kommt NACH bereits vorhandenen Evos
                int evoCount = 0;

                foreach (var sprite in currentSprites)
                {
                    var info = allWeapons.Find(w => w.sprite == sprite);
                    if (info.isEvo)
                        evoCount++;
                    else
                        break; // Evos sind immer links → danach abbrechen
                }

                currentSprites.Insert(evoCount, newSprite);
            }
            else
            {
                // ➕ Normale Waffen immer rechts
                currentSprites.Add(newSprite);
            }
        }

        // Schritt 7: Slots setzen
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i < currentSprites.Count)
            {
                var sprite = currentSprites[i];
                weaponSlots[i].sprite = sprite;

                int level = -1;
                int maxLevel = -1;

                foreach (var w in weaponData)
                {
                    if (w.sprite == sprite)
                    {
                        level = w.level;
                        maxLevel = w.maxLevel;
                        break;
                    }
                }

                if (i < levelTexts.Count)
                {
                    if (level >= 0)
                    {
                        levelTexts[i].text = (level >= maxLevel) ? "M" : (level + 1).ToString();
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
