using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpButton : MonoBehaviour
{
    [Header("UI Elemente")]
    public TMP_Text powerUpRarity;
    public TMP_Text powerUpDescription;
    public Image powerUpRarityPanel;
    public PowerUpEntry assignedPowerUp;
    private PowerUpRarity selectedRarity;
    private float selectedValue;
    public void ActivateButton()
    {
        if (assignedPowerUp == null)
        {
            Debug.LogWarning("Kein PowerUp zugewiesen!");
            return;
        }

        // Rarity + Wert ermitteln
        RaritySelector();

        // UI aktualisieren
        UpdateUI();
    }

    // --------------------------------------------------
    // 🔹 Rarity-Berechnung inkl. Luck
    // --------------------------------------------------
    private void RaritySelector()
    {
        float luck = PlayerController.Instance != null ? PlayerController.Instance.luck : 0f;

        // 1️⃣ Rarity bestimmen
        selectedRarity = PowerUpRaritySelector.GetRandomRarity(luck);

        // 2️⃣ Passenden Stat finden
        PowerUpStats rarityStats = assignedPowerUp.rarityStats.Find(r => r.rarity == selectedRarity);
        if (rarityStats == null)
        {
            Debug.LogWarning($"Keine Stats für Rarity '{selectedRarity}' gefunden!");
            selectedValue = 0f;
            return;
        }

        // 3️⃣ Wert zufällig innerhalb der Range auswählen
        selectedValue = Random.Range(rarityStats.statRange.x, rarityStats.statRange.y);

        Debug.Log($"PowerUp '{assignedPowerUp.powerUpName}' -> {selectedRarity} ({selectedValue:F2})");
    }

    // --------------------------------------------------
    // 🔹 Aktualisiert die UI-Anzeige
    // --------------------------------------------------
    private void UpdateUI()
    {
        if (powerUpRarity != null)
        {
            powerUpRarity.text = selectedRarity.ToString();
            powerUpRarityPanel.color = GetRarityColor(selectedRarity);
        }

        if (powerUpDescription != null)
        {
            powerUpDescription.text =
                $"{assignedPowerUp.descriptionBefore} {selectedValue:F2} {assignedPowerUp.descriptionAfter}";
        }
    }

    public void SelectUpgrade()
    {

        // 🔹 Spieler holen
        var player = PlayerController.Instance;
        if (player == null)
        {
            Debug.LogError("❌ Kein PlayerController gefunden!");
            return;
        }

        // 🔹 Versuch, das passende Feld anhand des Namens zu finden
        var field = typeof(PlayerController).GetField(assignedPowerUp.powerUpName);

        if (field == null)
        {
            Debug.LogWarning($"⚠ Kein Feld mit dem Namen '{assignedPowerUp.powerUpName}' im PlayerController gefunden!");
            return;
        }

        // 🔹 Aktuellen Wert auslesen
        object currentValue = field.GetValue(player);

        // 🔹 Nur unterstützen, wenn es float oder int ist
        if (currentValue is float currentFloat)
        {
            field.SetValue(player, currentFloat + selectedValue);
            //Debug.Log($"✅ PowerUp '{assignedPowerUp.powerUpName}' erhöht: {currentFloat} → {currentFloat + selectedValue}");
        }
        else if (currentValue is int currentInt)
        {
            field.SetValue(player, currentInt + Mathf.RoundToInt(selectedValue));
            //Debug.Log($"✅ PowerUp '{assignedPowerUp.powerUpName}' erhöht: {currentInt} → {currentInt + Mathf.RoundToInt(selectedValue)}");
        }
        else
        {
            //Debug.LogWarning($"⚠ Feld '{assignedPowerUp.powerUpName}' ist kein int oder float!");
        }
        // 🔹 Panel schließen & Sound
        UIController.Instance.PowerUpPanelClose();
        UIController.Instance.UpdateHealthSlider();
        AudioController.Instance.PalySound(AudioController.Instance.MenuClick);
    }


    // --------------------------------------------------
    // 🔹 Gibt passende Farben je nach Rarity zurück
    // --------------------------------------------------
    private Color GetRarityColor(PowerUpRarity rarity)
    {
        switch (rarity)
        {
            case PowerUpRarity.Common: return Color.gray;
            case PowerUpRarity.Uncommon: return new Color(0.4f, 1f, 0.4f);
            case PowerUpRarity.Rare: return new Color(0.4f, 0.6f, 1f);
            case PowerUpRarity.Epic: return new Color(0.7f, 0.4f, 1f);
            case PowerUpRarity.Legendary: return new Color(1f, 0.85f, 0.3f);
            default: return Color.gray;
        }
    }
}
