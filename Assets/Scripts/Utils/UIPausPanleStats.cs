using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class UIPausPanleStats : MonoBehaviour
{
    [Header("Alle Stats-Texte im Inspector zuweisen")]
    public List<TMP_Text> statTexts = new List<TMP_Text>();
     void Update()
    {
        if (PlayerController.Instance == null) return;

        // 0  Max HP
        statTexts[0].text = PlayerController.Instance.playerMaxHealth.ToString("F0");

        // 1  HP Regeneration
        statTexts[1].text = PlayerController.Instance.playerHealthReg.ToString("F1");

        // 2  Armor
        statTexts[2].text = PlayerController.Instance.playerArmor.ToString("F0");

        // 3  Dodge Chance
        statTexts[3].text = (PlayerController.Instance.dodgeChance * 100f).ToString("F0");

        // 4  Life Steal
        statTexts[4].text = PlayerController.Instance.lifeStealChance.ToString("F0");

        // 5  Damage
        statTexts[5].text = PlayerController.Instance.damageMultiplier.ToString("F1");

        // 6  Crit Chance
        statTexts[6].text = (PlayerController.Instance.critChance * 100f).ToString("F0");

        // 7  Crit Damage
        statTexts[7].text = PlayerController.Instance.critDamage.ToString("F1");

        // 8 Size
        statTexts[8].text = PlayerController.Instance.AOERange.ToString("F1");

        // 9  Move Speed
        statTexts[9].text = PlayerController.Instance.moveSpeed.ToString("F1");

        // 10 Luck
        statTexts[10].text = PlayerController.Instance.luck.ToString("F0");

        // 11 Pickup Range
        statTexts[11].text = PlayerController.Instance.pickupRange.ToString("F1");

        // 12 XP Gain
        statTexts[12].text = PlayerController.Instance.experienceMultiplier.ToString("F2");

        // 13 Gold Gain
        statTexts[13].text = GameManager.Instance.currencyGainMultiplire.ToString("F1");

        // 14 Extra Shots
        statTexts[14].text = PlayerController.Instance.playerShots.ToString("F1");
    }
}
