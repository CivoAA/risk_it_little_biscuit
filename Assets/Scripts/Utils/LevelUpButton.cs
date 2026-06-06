using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class LevelUpButton : MonoBehaviour
{
    public TMP_Text weaponName;
    public TMP_Text weaponDescription;
    public Image weaponIcon;
    public Image weaponIconEvo;
    public Image backgroundImage;
    public Sprite Weapon_Frame_Sprite;
    public Sprite Buff_Frame_Sprite;
    public Sprite Evo_Frame_Sprite;

    private Weapon assingedWeapon;
    private List<Sprite> partnerIcons = new List<Sprite>();
    private Coroutine rotateCoroutine;

    private void OnDisable()
    {
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }

        if (weaponIconEvo != null)
        {
            weaponIconEvo.enabled = false;
            weaponIconEvo.sprite = null;
        }
    }

    public void ActivateButton(Weapon weapon)
    {
        assingedWeapon = weapon;

        // Text & Basis-Icon
        weaponName.text = weapon.name;
        if (weapon.weaponLevel + 2 == weapon.maxweaponLevel + 1)
            weaponDescription.text = "Level to Max Level\nMax Level: " + (weapon.maxweaponLevel + 1);
        else
            weaponDescription.text = $"Level: {weapon.weaponLevel + 1} -> {weapon.weaponLevel + 2}\nMax Level: {weapon.maxweaponLevel + 1}";

        weaponIcon.sprite = weapon.weaponIcon;

        if (backgroundImage != null)
        {
            if (PlayerController.Instance.activeWeapon.Contains(weapon))
            {
                backgroundImage.sprite = Weapon_Frame_Sprite;
            }
            else if (PlayerController.Instance.activeBuffs.Contains(weapon))
            {
                backgroundImage.sprite = Buff_Frame_Sprite;
            }
            else if (PlayerController.Instance.activeEvos.Contains(weapon))
            {
                backgroundImage.sprite = Evo_Frame_Sprite;
            }
            else
            {
                backgroundImage.sprite = null;
            }
        }


        // Partner-Icons holen
        partnerIcons = PlayerController.Instance.GetEvoPartnerIcons(weapon);

        if (weaponIconEvo == null) return;

        if (partnerIcons == null || partnerIcons.Count == 0)
        {
            weaponIconEvo.enabled = false;
            weaponIconEvo.sprite = null;

            if (rotateCoroutine != null)
            {
                StopCoroutine(rotateCoroutine);
                rotateCoroutine = null;
            }

            return;
        }

        // Mindestens ein Partner vorhanden
        weaponIconEvo.enabled = true;
        weaponIconEvo.sprite = partnerIcons[0];

        if (rotateCoroutine != null)
            StopCoroutine(rotateCoroutine);

        rotateCoroutine = StartCoroutine(RotatePartnerIcons());
    }

    private void OnEnable()
    {
        if (partnerIcons != null && partnerIcons.Count > 1 && weaponIconEvo != null)
        {
            if (rotateCoroutine != null)
                StopCoroutine(rotateCoroutine);

            rotateCoroutine = StartCoroutine(RotatePartnerIcons());
        }
    }

    private IEnumerator RotatePartnerIcons()
    {
        if (partnerIcons == null || partnerIcons.Count <= 1)
            yield break;

        int i = 0;
        while (true)
        {
            if (weaponIconEvo != null && weaponIconEvo.enabled)
            {
                weaponIconEvo.sprite = partnerIcons[i];
                i = (i + 1) % partnerIcons.Count;
            }

            yield return new WaitForSecondsRealtime(2f); 
        }
    }

    public void SelectUpgrade()
    {
        // 🔹 1) BANISH-FALL
        if (UIController.Instance.banish)
        {
            var player = PlayerController.Instance;

            // aktive Waffen dürfen nicht gebannt werden
            if (assingedWeapon.weaponLevel >= 0)
            {
                DamageNumberController.Instance.CreateText(
                    "Can't banish active weapons!",
                    player.transform.position
                );
                AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
                return;
            }

            // Evos dürfen nicht gebannt werden
            if (player.activeEvos.Contains(assingedWeapon))
            {
                DamageNumberController.Instance.CreateText(
                    "Can't banish Evos!",
                    player.transform.position
                );
                AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
                return;
            }

            // ✅ HIER PASSIERT DER BANISH
            assingedWeapon.hasBeenRemoved = true;

            player.banishAmount--;

            UIController.Instance.Panelback();
            UIController.Instance.LevelUpPanelClose();
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
            PlayerController.Instance.LevelUpSelectet = true;
            return;
        }

        // 🔹 2) NORMALES LEVEL-UP / EVO-UPGRADE
        else
        {
            // Wenn es sich um eine Evo handelt, lösche die Basiswaffen
            var evoRecipe = PlayerController.Instance.EvoCombinations
                .FirstOrDefault(r => r.EvoWeapon == assingedWeapon);

            if (evoRecipe != null)
            {
                // 🔸 Entferne die Basiswaffen dieser Evo
                void RemoveWeapon(Weapon w)
                {
                    if (w == null) return;

                    if (PlayerController.Instance.activeWeapon.Contains(w))
                    {
                        w.weaponLevel = -99;
                        w.hasBeenRemoved = true;
                    }

                    if (PlayerController.Instance.activeBuffs.Contains(w))
                    {
                        w.weaponLevel = -99;
                        w.hasBeenRemoved = true;
                    }
                }

                RemoveWeapon(evoRecipe.RequiredWeapon1);
                RemoveWeapon(evoRecipe.RequiredWeapon2);

                // Markiere Evo als aktiv
                assingedWeapon.weaponLevel = 0;
                assingedWeapon.posssibleEvo = false;
            }
            else
            {
                // 🔸 Normale Waffe leveln
                assingedWeapon.LevelUP();
                assingedWeapon.posssibleEvo = true;

                // Markiere potentielle Evo-Partner als "möglich"
                foreach (var recipe in PlayerController.Instance.EvoCombinations)
                {
                    if (recipe.RequiredWeapon1 == assingedWeapon && recipe.RequiredWeapon2 != null)
                        recipe.RequiredWeapon2.posssibleEvo = true;
                    else if (recipe.RequiredWeapon2 == assingedWeapon && recipe.RequiredWeapon1 != null)
                        recipe.RequiredWeapon1.posssibleEvo = true;
                }
            }

            // 🔹 UI und Sound-Handling
            UIController.Instance.LevelUpPanelClose();
            AudioController.Instance.PalySound(AudioController.Instance.selectUpgrade);
            PlayerController.Instance.LevelUpSelectet = true;
            AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
        }
    }


    public void SelectUpgradeGamba()
    {
        for (int i = 0; i < Gamba.Instance.wins; i++)
            assingedWeapon.LevelUP();

        UIController.Instance.GambaPanelClose();
        AudioController.Instance.PalySound(AudioController.Instance.selectUpgrade);
        AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
    }
}
