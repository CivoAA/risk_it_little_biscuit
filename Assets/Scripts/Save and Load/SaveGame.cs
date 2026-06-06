using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class ButtonData
{
    public string Name;
    public int index;
    public int level;          // level = Anzahl Käufe (0..cost.Count)
    public List<int> cost;
}

[System.Serializable]
public class SaveData
{
    public int currency;
    public int skinIndex;
    public List<ButtonData> buttons;
}

public class SaveGame : MonoBehaviour
{
    public static SaveGame Instance;
    private string savePath;

    public SaveData currentData;

    [Header("Default Buttons")]
    [SerializeField] public List<ButtonData> defaultButtons = new List<ButtonData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            savePath = Application.persistentDataPath + "/save.json";
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ---------------- SPEICHERN ----------------
    public void SaveGameData()
    {
        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(savePath, json);
        Debug.Log("✅ Daten gespeichert unter: " + savePath);
    }

    // ---------------- LADEN + MIGRATION ----------------
    public void LoadGame()
    {
        if (!File.Exists(savePath))
        {
            CreateNewFromDefaults();
            SaveGameData();
            return;
        }

        string json = File.ReadAllText(savePath);
        var loaded = JsonUtility.FromJson<SaveData>(json);

        if (loaded == null)
        {
            Debug.LogWarning("⚠️ Save konnte nicht gelesen werden. Erzeuge neu aus Defaults.");
            CreateNewFromDefaults();
            SaveGameData();
            return;
        }

        int keptCurrency = loaded.currency;
        int keptSkinIndex = loaded.skinIndex;

        if (loaded.buttons == null)
            loaded.buttons = new List<ButtonData>();

        // Placeholder raus
        loaded.buttons.RemoveAll(b =>
            !string.IsNullOrEmpty(b.Name) &&
            b.Name.Trim().Equals("PLACE HOLDER", StringComparison.OrdinalIgnoreCase));

        // alte Buttons nach Index
        var oldByIndex = new Dictionary<int, ButtonData>();
        foreach (var b in loaded.buttons)
        {
            if (!oldByIndex.ContainsKey(b.index))
                oldByIndex.Add(b.index, b);
        }

        // Migration: komplett neu aus defaults bauen
        var migratedButtons = new List<ButtonData>();
        int totalRefund = 0;

        foreach (var def in defaultButtons)
        {
            // Deep copy vom Default
            var newBtn = new ButtonData
            {
                Name = def.Name,
                index = def.index,
                level = 0,
                cost = def.cost != null ? new List<int>(def.cost) : new List<int>()
            };

            if (oldByIndex.TryGetValue(def.index, out var oldBtn))
            {
                // Wenn Kosten gleich -> Level übernehmen (aber sicher clampen)
                if (SameCosts(oldBtn.cost, def.cost))
                {
                    newBtn.level = ClampPurchases(oldBtn.level, newBtn.cost);
                }
                else
                {
                    // Kosten geändert -> Refund nach alten Kosten + reset
                    totalRefund += CalcRefund(oldBtn);
                    newBtn.level = 0;
                }
            }

            migratedButtons.Add(newBtn);
        }

        keptCurrency += totalRefund;

        currentData = new SaveData
        {
            currency = keptCurrency,
            skinIndex = keptSkinIndex,
            buttons = migratedButtons
        };

        SaveGameData();
        Debug.Log($"✅ Migration done. Refund={totalRefund}, Currency={currentData.currency}");
    }

    private void CreateNewFromDefaults()
    {
        currentData = new SaveData
        {
            currency = 0,
            skinIndex = 0,
            buttons = new List<ButtonData>()
        };

        foreach (var def in defaultButtons)
        {
            currentData.buttons.Add(new ButtonData
            {
                Name = def.Name,
                index = def.index,
                level = 0,
                cost = def.cost != null ? new List<int>(def.cost) : new List<int>()
            });
        }
    }

    // ---------------- HELPERS ----------------

    // Kostenvergleich (Name ignorieren!)
    private static bool SameCosts(List<int> a, List<int> b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    // level = Käufe (0..cost.Count)
    private static int ClampPurchases(int purchases, List<int> cost)
    {
        if (cost == null) return 0;
        return Mathf.Clamp(purchases, 0, cost.Count);
    }

    // Refund = Summe der bereits bezahlten Stufen anhand alter Kosten
    private static int CalcRefund(ButtonData oldBtn)
    {
        if (oldBtn == null || oldBtn.cost == null) return 0;

        int purchases = ClampPurchases(oldBtn.level, oldBtn.cost);
        int refund = 0;

        for (int i = 0; i < purchases; i++)
            refund += oldBtn.cost[i];

        return refund;
    }

    // ---------------- WÄHRUNG ----------------
    public void AddCurrency(int amount)
    {
        currentData.currency += amount;
        SaveGameData();
        WM_UIController.Instance?.UpdateCurrencyText();
    }

    public void RemoveCurrency(int amount)
    {
        currentData.currency -= amount;
        SaveGameData();
    }

    public int GetCurrency() => currentData.currency;

    // ---------------- BUTTON UPGRADE ----------------
    public void SaveUpgradeButton(int index)
    {
        ButtonData btn = currentData.buttons.Find(b => b.index == index);
        if (btn == null)
        {
            Debug.LogError("❌ Kein Button mit Index " + index + " gefunden!");
            return;
        }

        // Sicherheit: nicht über MAX hinaus
        int max = btn.cost != null ? btn.cost.Count : 0;
        if (btn.level >= max)
        {
            Debug.Log($"⚠️ Button {index} ist bereits MAX (level={btn.level}, max={max})");
            return;
        }

        btn.level++;
        SaveGameData();
        Debug.Log($"🔼 Button {index} auf Level {btn.level} erhöht!");
    }

    public int GetButtonLevel(int index)
    {
        ButtonData btn = currentData.buttons.Find(b => b.index == index);
        return btn != null ? btn.level : 0;
    }

    public void ResetAllUpgrades()
    {
        if (currentData == null || currentData.buttons == null) return;

        int refund = 0;

        foreach (var btn in currentData.buttons)
        {
            if (btn.cost == null) continue;

            int purchases = ClampPurchases(btn.level, btn.cost);

            for (int i = 0; i < purchases; i++)
                refund += btn.cost[i];

            btn.level = 0;
        }

        currentData.currency += refund;
        SaveGameData();

        Debug.Log($"🔄 Alle Upgrades zurückgesetzt. {refund} erstattet. Neue Currency: {currentData.currency}");
    }
}
