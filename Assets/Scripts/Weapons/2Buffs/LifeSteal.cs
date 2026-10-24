using UnityEngine;

public class LifeSteal : Weapon
{   
    public static LifeSteal Instance;
    public float chance;
    public float damageMultiplire;
    public float cooldown = 1;
    private int lastAppliedLevel = -2;
    private float appliedChance, appliedMultiplier;   // Summen aller Stufen - fuer RemoveEffect

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.lifeStealChance       += stats[weaponLevel].range;
            PlayerController.Instance.lifeStealMultiplire   += stats[weaponLevel].damage;
            appliedChance     += stats[weaponLevel].range;
            appliedMultiplier += stats[weaponLevel].damage;
            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.lifeStealChance     -= appliedChance;
            PlayerController.Instance.lifeStealMultiplire -= appliedMultiplier;
        }
        appliedChance = appliedMultiplier = 0f;
    }
    
    public void StealLife(int damage)
    {   
        if(cooldown <= 0)
        {   
            if (PlayerController.Instance.lifeStealChance >= 0 && PlayerController.Instance.CanReceiveHealing)
            {
                chance = PlayerController.Instance.lifeStealChance;
                if (Random.Range(0f, 100f) < chance)
                {
                    //PlayerController.Instance.playerHealth += damage * PlayerController.Instance.lifeStealMultiplire;
                    PlayerController.Instance.Heal(1f);
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
