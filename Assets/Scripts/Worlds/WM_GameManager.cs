using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class WM_GameManager : MonoBehaviour
{
    public TMP_Text descriptionBuy;
    public Image IconBuy;
    public GameObject BuyPanle;
    private String TextPrice;
    [SerializeField] private List<Image> Icons;
    public List<String> description;
    public List<GameObject> buttons;
    private int buttonIndex;
    public void BuyButton()
    {
        var data = SaveGame.Instance.currentData;
        if (data == null || data.buttons == null) return;
        if (buttonIndex < 0 || buttonIndex >= data.buttons.Count) return;

        var btn = data.buttons[buttonIndex];

        // ✅ MAX-Check (wichtig, damit cost[level] nie out of range geht)
        if (btn.cost == null || btn.level >= btn.cost.Count)
        {
            AudioController.Instance?.PalySound(AudioController.Instance.PlayerHit);
            return;
        }

        int price = btn.cost[btn.level];

        if (price <= data.currency)
        {
            SaveGame.Instance.RemoveCurrency(price);
            SaveGame.Instance.SaveUpgradeButton(buttonIndex);

            WM_UIController.Instance.RefreshButtonTexts();
            WM_UIController.Instance.UpdateCurrencyText();

            AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);

            // UI sofort aktualisieren
            SelectUpgrade(buttonIndex);

            // Werte fürs Spiel neu berechnen
            LevelPoint.Instance.UpdateExtraData();
        }
        else
        {
            AudioController.Instance?.PalySound(AudioController.Instance.PlayerHit);
        }
    }

    public void SelectUpgrade(int index)
    {
        var data = SaveGame.Instance.currentData;
        if (data == null || data.buttons == null) return;
        if (index < 0 || index >= data.buttons.Count) return;

        buttonIndex = index;

        var btn = data.buttons[index];
        bool isMax = (btn.cost == null) || (btn.level >= btn.cost.Count);

        // Panel an
        if (!BuyPanle.activeSelf)
            BuyPanle.SetActive(true);

        // Icon setzen (sicher)
        if (Icons != null && index >= 0 && index < Icons.Count && Icons[index] != null)
            IconBuy.sprite = Icons[index].sprite;

        // Normalfall: Werte aus LevelPoint.buttonValueTables
        if (LevelPoint.Instance == null || LevelPoint.Instance.buttonValueTables == null)
        {
            descriptionBuy.text = "No data available.";
            return;
        }

        if (!LevelPoint.Instance.buttonValueTables.TryGetValue(index, out var values) || values == null || values.Count == 0)
        {
            descriptionBuy.text = "No values defined for this upgrade.";
            return;
        }

        // currentValue immer sicher lesen (clamp auf letztes)
        float currentValue = (btn.level >= values.Count) ? values[values.Count - 1] : values[btn.level];

        if (isMax)
        {
            // Max erreicht -> kein price/next
            descriptionBuy.text = description[index] + currentValue + " Max Level Reached";
            return;
        }

        // nextValue nur, wenn vorhanden
        int nextLevel = btn.level + 1;
        float nextValue = (nextLevel >= values.Count) ? values[values.Count - 1] : values[nextLevel];

        int nextPrice = btn.cost[btn.level];

        string textPrice =
            currentValue
            + " | "
            + nextValue
            + " Price: <color=#FFFFFF>"
            + nextPrice
            + "</color>";

        descriptionBuy.text = description[index] + textPrice;
    }

    public void ActivateScene(string sceneName)
    {
        MenuManager.Instance.ActivateScene(sceneName);
    }
    public void DeactivateScene(string sceneName)
    {
        MenuManager.Instance.DeactivateScene(sceneName);
    }
    public void SelectChar()
    {
        LevelPoint.Instance.extraData[0] = WM_UIController.Instance.currentIndex;
        LevelPoint.Instance.Startweapon(WM_UIController.Instance.currentIndex);
        if (PlayerSkinSwitcher.Instance != null)
        {
            PlayerSkinSwitcher.Instance.skinIndex = WM_UIController.Instance.currentIndex;
        }
        if (WM_PlayerSkinSwitcher.Instance != null)
        {
            WM_PlayerSkinSwitcher.Instance.skinIndex = WM_UIController.Instance.currentIndex;
        }
        SaveGame.Instance.currentData.skinIndex = WM_UIController.Instance.currentIndex;
        SaveGame.Instance.SaveGameData();
    }
    public void OnResetButtonPressed()
    {
        SaveGame.Instance.ResetAllUpgrades();
        WM_UIController.Instance.RefreshButtonTexts();
        WM_UIController.Instance.UpdateCurrencyText();
        LevelPoint.Instance.UpdateExtraData();
        AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
    }
    public void ResetCurrencyToZero()
    {
        if (SaveGame.Instance == null || SaveGame.Instance.currentData == null)
            return;

        SaveGame.Instance.currentData.currency = 0;
        SaveGame.Instance.SaveGameData();

        WM_UIController.Instance?.UpdateCurrencyText();
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);

        Debug.Log("💸 Currency wurde auf 0 gesetzt.");
    }
    public void Give1000Currency()
    {
        if (SaveGame.Instance == null)
            return;

        SaveGame.Instance.AddCurrency(1000); // speichert & aktualisiert UI
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);

        Debug.Log("💰 1000 Currency hinzugefügt.");
    }

    public void RefreshUnlockButtons()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null)
                continue;

            // 🔓 Standardmäßig aktivieren
            bool isVisible = true;

            // 🔐 Sonderregeln für bestimmte Buttons
            switch (i)
            {
                case 5:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_buff_slot");
                    break;
                case 6:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_weapon_slot");
                    break;
                case 7:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_evo_slot");
                    break;
                case 8:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_boba_gun");
                    break;
                case 9:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_shurikookie");
                    break;
                case 10:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_spike_fork");
                    break;
                case 12:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_celestial_star");
                    break;
                case 13:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_blade_swarm");
                    break;
                case 14:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_candy_bomb");
                    break;
                case 15:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_time_laser");
                    break;
                case 20:
                    isVisible = UnlockManager.Instance.IsUnlocked("unlock_extra_shot");
                    break;
            }
            buttons[i].SetActive(isVisible);
        }
    }

}
