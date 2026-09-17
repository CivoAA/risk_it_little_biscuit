using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Der Shop in der World Map. Die Knöpfe hängen im Inspector an
/// <see cref="SelectUpgrade"/> und <see cref="BuyButton"/>; Inhalt, Preise und
/// Werte kommen aus dem Katalog <see cref="Shop"/> - genau wie beim Hub-Shop.
///
/// Die Liste <see cref="buttons"/> ist reine Szenen-Verdrahtung: Position i
/// gehört zum i-ten Eintrag im Katalog. Namen, Beschreibungen, Preise und
/// Symbole stehen hier bewusst nicht mehr.
/// </summary>
public class WM_GameManager : MonoBehaviour
{
    public TMP_Text descriptionBuy;
    public Image IconBuy;
    public GameObject BuyPanle;

    [Tooltip("Ein Knopf je Katalogeintrag, in derselben Reihenfolge wie Shop.cs.")]
    public List<GameObject> buttons;

    private int selectedIndex = -1;

    private ShopItemDef Selected =>
        (selectedIndex >= 0 && selectedIndex < Shop.All.Count) ? Shop.All[selectedIndex] : null;

    private void OnEnable()
    {
        Shop.Changed += OnShopChanged;
    }

    private void OnDisable()
    {
        Shop.Changed -= OnShopChanged;
    }

    private void OnShopChanged()
    {
        WM_UIController.Instance?.RefreshButtonTexts();
        WM_UIController.Instance?.UpdateCurrencyText();
        if (Selected != null) ShowDetail(Selected);
    }

    // ----------------------------------------------------------- Kaufen

    public void BuyButton()
    {
        ShopItemDef def = Selected;
        if (def == null) return;

        if (Shop.TryBuy(def))
        {
            AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);
            // Anzeige zieht über Shop.Changed nach.
        }
        else
        {
            AudioController.Instance?.PalySound(AudioController.Instance.PlayerHit);
        }
    }

    /// <summary>Im Inspector an den Shop-Knöpfen. Der Index ist die Position im Katalog.</summary>
    public void SelectUpgrade(int index)
    {
        if (index < 0 || index >= Shop.All.Count) return;

        selectedIndex = index;

        if (BuyPanle != null && !BuyPanle.activeSelf) BuyPanle.SetActive(true);

        ShowDetail(Shop.All[index]);
    }

    private void ShowDetail(ShopItemDef def)
    {
        if (IconBuy != null)
        {
            Sprite icon = def.Icon;
            IconBuy.sprite = icon;
            IconBuy.enabled = icon != null;
        }

        if (descriptionBuy == null) return;

        string text = def.Description;
        float current = Shop.CurrentValue(def);

        if (Shop.IsMaxed(def))
        {
            descriptionBuy.text = $"{text} {Nice(current)} Max Level Reached";
            return;
        }

        descriptionBuy.text = $"{text} {Nice(current)} | {Nice(Shop.NextValue(def))}" +
                              $" Price: <color=#FFFFFF>{Shop.NextPrice(def)}</color>";
    }

    /// <summary>0,35 statt 0,3500001 - und ganze Zahlen ohne Komma.</summary>
    private static string Nice(float v) =>
        Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

    // ----------------------------------------------------------- Szenen

    public void ActivateScene(string sceneName)
    {
        MenuManager.Instance.ActivateScene(sceneName);
    }

    public void DeactivateScene(string sceneName)
    {
        MenuManager.Instance.DeactivateScene(sceneName);
    }

    // ----------------------------------------------------------- Charakter

    public void SelectChar()
    {
        WM_UIController.Instance?.SelectChar();
    }

    // ----------------------------------------------------------- Debug-Knöpfe

    public void OnResetButtonPressed()
    {
        Shop.ResetAllUpgrades();
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);
    }

    public void ResetCurrencyToZero()
    {
        Shop.SetCurrency(0);
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);
        Debug.Log("💸 Currency wurde auf 0 gesetzt.");
    }

    public void Give1000Currency()
    {
        Shop.AddCurrency(1000);
        AudioController.Instance?.PalySound(AudioController.Instance.MenuClick);
        Debug.Log("💰 1000 Currency hinzugefügt.");
    }

    // ----------------------------------------------------------- Sichtbarkeit

    /// <summary>
    /// Blendet die Knöpfe aus, deren Eintrag noch nicht freigeschaltet ist.
    /// Welcher Eintrag welche Unlock-ID braucht, steht am Katalogeintrag - hier
    /// stand früher eine handgepflegte switch-Liste mit festen Indizes.
    /// </summary>
    public void RefreshUnlockButtons()
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null) continue;

            bool visible = i < Shop.All.Count && Shop.IsVisible(Shop.All[i]);
            buttons[i].SetActive(visible);
        }
    }
}
