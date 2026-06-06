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

    // Evos
    public BobaEvo BobaSawEvo;
    public ShuriBlastEvo ShuriBlastEvo;
    public ExplosiveStarEvo ExplosiveStarEvo;
    public BloodyFork BloodyFork;
    public BladeStormEvo BladeStormEvo;
    public BoomerangEvo boomerangEvo;
    public BombSawEvo bombSawEvo;

    void Start()
    {
        // Waffen finden
        bobaGun = GameObject.Find("Boba Gun").GetComponent<BobaGun>();
        areaWeaponJamJar = GameObject.Find("Throwing Jam Jar").GetComponent<AreaWeaponJamJar>();
        shurikenWeapon = GameObject.Find("Shurikookie").GetComponent<ShurikenWeapon>();
        keckssaegeWeapon = GameObject.Find("CookieSaw").GetComponent<KeckssaegeWeapon>();
        bobaWeapon = GameObject.Find("Butterblast").GetComponent<BobaWeapon>();
        areaWeapon = GameObject.Find("Coffe Pool").GetComponent<AreaWeapon>();
        SpikeFork = GameObject.Find("Spike Fork").GetComponent<Spikefork>();
        deathstrike = GameObject.Find("Deathstrike").GetComponent<Deathstrike>();
        RandomVoidSpike = GameObject.Find("Void Spike").GetComponent<RandomVoidSpike>();
        CelestialStar = GameObject.Find("Celestial Star").GetComponent<CelestialStar>();
        FireBall = GameObject.Find("Fire Ball").GetComponent<FireBall>();
        BladeSwarm = GameObject.Find("Blade Swarm").GetComponent<BladeSwarm>();
        candyBomb = GameObject.Find("Candy Bomb").GetComponent<CandyBomb>();
        boomerang = GameObject.Find("Boomerang").GetComponent<Boomerang>();
        timeLaser = GameObject.Find("Time Laser").GetComponent<TimeLaser>();

        // Evos
        ShuriBlastEvo = GameObject.Find("Shuri Blast Evo").GetComponent<ShuriBlastEvo>();
        BobaSawEvo = GameObject.Find("Boba Saw Evo").GetComponent<BobaEvo>();
        ExplosiveStarEvo = GameObject.Find("Explosive Star Evo").GetComponent<ExplosiveStarEvo>();
        BloodyFork = GameObject.Find("Bloody Fork Evo").GetComponent<BloodyFork>();
        BladeStormEvo = GameObject.Find("Blade Storm Evo").GetComponent<BladeStormEvo>();
        boomerangEvo = GameObject.Find("Boomerang Evo").GetComponent<BoomerangEvo>();
        bombSawEvo = GameObject.Find("Bomb Saw Evo").GetComponent<BombSawEvo>(); 

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
        // Schritt 1: Waffen + Evos definieren (mit isEvo)
        var allWeapons = new (int level, int maxLevel, Sprite sprite, bool isEvo)[]
        {
            (shurikenWeapon.weaponLevel,   shurikenWeapon.maxweaponLevel,   shurikenWeapon.weaponIcon,   false),
            (areaWeaponJamJar.weaponLevel, areaWeaponJamJar.maxweaponLevel, areaWeaponJamJar.weaponIcon, false),
            (bobaWeapon.weaponLevel,       bobaWeapon.maxweaponLevel,       bobaWeapon.weaponIcon,       false),
            (bobaGun.weaponLevel,          bobaGun.maxweaponLevel,          bobaGun.weaponIcon,          false),
            (areaWeapon.weaponLevel,       areaWeapon.maxweaponLevel,       areaWeapon.weaponIcon,       false),
            (keckssaegeWeapon.weaponLevel, keckssaegeWeapon.maxweaponLevel, keckssaegeWeapon.weaponIcon, false),
            (SpikeFork.weaponLevel,        SpikeFork.maxweaponLevel,        SpikeFork.weaponIcon,        false),
            (deathstrike.weaponLevel,      deathstrike.maxweaponLevel,      deathstrike.weaponIcon,      false),
            (RandomVoidSpike.weaponLevel,  RandomVoidSpike.maxweaponLevel,  RandomVoidSpike.weaponIcon,  false),
            (CelestialStar.weaponLevel,    CelestialStar.maxweaponLevel,    CelestialStar.weaponIcon,    false),
            (FireBall.weaponLevel,         FireBall.maxweaponLevel,         FireBall.weaponIcon,         false),
            (BladeSwarm.weaponLevel,       BladeSwarm.maxweaponLevel,       BladeSwarm.weaponIcon,       false),
            (candyBomb.weaponLevel,        candyBomb.maxweaponLevel,        candyBomb.weaponIcon,        false),
            (boomerang.weaponLevel,        boomerang.maxweaponLevel,        boomerang.weaponIcon,        false),
            (timeLaser.weaponLevel,        timeLaser.maxweaponLevel,        timeLaser.weaponIcon,        false),

            // 🔥 Evo-Waffen
            (ShuriBlastEvo.weaponLevel,    ShuriBlastEvo.maxweaponLevel,    ShuriBlastEvo.weaponIcon,    true),
            (ExplosiveStarEvo.weaponLevel, ExplosiveStarEvo.maxweaponLevel, ExplosiveStarEvo.weaponIcon, true),
            (BloodyFork.weaponLevel,       BloodyFork.maxweaponLevel,       BloodyFork.weaponIcon,       true),
            (BobaSawEvo.weaponLevel,       BobaSawEvo.maxweaponLevel,       BobaSawEvo.weaponIcon,       true),
            (BladeStormEvo.weaponLevel,    BladeStormEvo.maxweaponLevel,    BladeStormEvo.weaponIcon,    true),
            (boomerangEvo.weaponLevel,     boomerangEvo.maxweaponLevel,     boomerangEvo.weaponIcon,     true),
            (bombSawEvo.weaponLevel,       bombSawEvo.maxweaponLevel,       bombSawEvo.weaponIcon,       true)
        };

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
            var weaponInfo = System.Array.Find(allWeapons, w => w.sprite == newSprite);
            bool isEvo = weaponInfo.isEvo;

            if (isEvo)
            {
                // 🔥 Evo kommt NACH bereits vorhandenen Evos
                int evoCount = 0;

                foreach (var sprite in currentSprites)
                {
                    var info = System.Array.Find(allWeapons, w => w.sprite == sprite);
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
