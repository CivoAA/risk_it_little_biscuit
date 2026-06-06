using Unity.VisualScripting;
using UnityEngine;

public class LifeSteal : Weapon
{   
    public static LifeSteal Instance;
    public float chance;
    public float damageMultiplire;
    public float cooldown = 1;
    private int lastAppliedLevel = -2;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.lifeStealChance       += stats[weaponLevel].range;
            PlayerController.Instance.lifeStealMultiplire   += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }
    
    public void StealLife(int damage)
    {   
        if(cooldown <= 0)
        {   
            if (PlayerController.Instance.lifeStealChance >= 0 && PlayerController.Instance.playerHealth < PlayerController.Instance.playerMaxHealth)
            {
                chance = PlayerController.Instance.lifeStealChance;
                if (Random.Range(0f, 100f) < chance)
                {
                    //PlayerController.Instance.playerHealth += damage * PlayerController.Instance.lifeStealMultiplire;
                    PlayerController.Instance.playerHealth += 1;
                    UIController.Instance.UpdateHealthSlider();
                    if (weaponLevel < 0)
                    {
                        cooldown = 1f; // Fallback-Cooldown
                    }
                    else
                    {
                        cooldown = stats[weaponLevel].cooldown;
                    }
                    Debug.Log("LIFESTEAL");
                }
            }
        }
    }
}
