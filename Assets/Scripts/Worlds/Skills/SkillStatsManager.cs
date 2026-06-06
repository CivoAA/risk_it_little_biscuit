using UnityEngine;
using System.Collections.Generic;

public class SkillStatsManager : MonoBehaviour
{
    public static SkillStatsManager Instance;

    public float bonusMaxHealth;
    public float bonusSpeed;
    public float bonusXpMultiplier;
    public float bonusHealthReg;
    public float bonusExtraShots;
    public float bonusArmor;
    public float bonusAOERange;
    public float bonusLifeSteal;
    public float bonusDamage;
    public float bonusCritDamage;
    public float bonusCritChance;
    public float bonusDodgeChance;
    public float bonusPickupRange;
    public float bonusLuck;
    public float bonusBanish;
    public float bonusReroll;
    public float startXPAmount;
    public float shrinkSpeed;
    public float weaponSlots;
    public float buffSlots;
    public float evoSlots;
    public float unlockEvoOrWeapon;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UpdateFromSkills();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateFromSkills()
    {
        ResetBonuses();

        foreach (var id in SkillSaveManager.Instance.currentData.unlockedSkillIDs)
        {
            SkillNode node = SkillNode.allSkills.Find(n => n.skillID == id);
            if (node == null) continue;

            switch (node.skillEffect)
            {
                case SkillNode.SkillType.IncreaseMaxHealth:
                    bonusMaxHealth += node.value;
                    break;

                case SkillNode.SkillType.IncreaseSpeed:
                    bonusSpeed += node.value;
                    break;

                case SkillNode.SkillType.xpMultiplier:
                    bonusXpMultiplier += node.value;
                    break;

                case SkillNode.SkillType.IncreaseHealthReg:
                    bonusHealthReg += node.value;
                    break;

                case SkillNode.SkillType.IncreaseExtraShot:
                    bonusExtraShots += node.value;
                    break;

                case SkillNode.SkillType.IncreaseArmor:
                    bonusArmor += node.value;
                    break;

                case SkillNode.SkillType.IncreaseAOERange:
                    bonusAOERange += node.value;
                    break;

                case SkillNode.SkillType.IncreaseLifeSteal:
                    bonusLifeSteal += node.value;
                    break;

                case SkillNode.SkillType.IncreaseDamage:
                    bonusDamage += node.value;
                    break;

                case SkillNode.SkillType.IncreaseCritDamage:
                    bonusCritDamage += node.value;
                    break;

                case SkillNode.SkillType.IncreaseCritChance:
                    bonusCritChance += node.value;
                    break;

                case SkillNode.SkillType.IncreaseDodgeChance:
                    bonusDodgeChance += node.value;
                    break;

                case SkillNode.SkillType.IncreasePickupRange:
                    bonusPickupRange += node.value;
                    break;

                case SkillNode.SkillType.IncreaseLuck:
                    bonusLuck += node.value;
                    break;

                case SkillNode.SkillType.BanishAmount:
                    bonusBanish += node.value;
                    break;

                case SkillNode.SkillType.RerollAmount:
                    bonusReroll += node.value;
                    break;
                    
                case SkillNode.SkillType.StartXPAmount:
                    startXPAmount += node.value;
                    break;
                    
                case SkillNode.SkillType.IncreaseShrinkSpeed:
                    shrinkSpeed += node.value;
                    break;
                    
                case SkillNode.SkillType.IncreaseWeaponSlots:
                    weaponSlots += node.value;
                    break;

                case SkillNode.SkillType.IncreaseBuffSlots:
                    buffSlots += node.value;
                    break;

                case SkillNode.SkillType.IncreaseEvoSlots:
                    evoSlots += node.value;
                    break;

                case SkillNode.SkillType.UnlockEvoOrWeapon:
                    unlockEvoOrWeapon += node.value;
                    break;
            }

        }

        Debug.Log("🎯 SkillStats aktualisiert");
    }

    private void ResetBonuses()
    {
        bonusMaxHealth = 0;
        bonusSpeed = 0;
        bonusXpMultiplier = 0;
        bonusHealthReg = 0;
        bonusExtraShots = 0;
        bonusArmor = 0;
        bonusAOERange = 0;
        bonusLifeSteal = 0;
        bonusDamage = 0;
        bonusCritDamage = 0;
        bonusCritChance = 0;
        bonusDodgeChance = 0;
        bonusPickupRange = 0;
        bonusLuck = 0;
        bonusBanish = 0;
        bonusReroll = 0;
        startXPAmount = 0;
        shrinkSpeed = 0;
        weaponSlots = 0;
        buffSlots = 0;
        evoSlots = 0;
        unlockEvoOrWeapon = 0;
    }

}
