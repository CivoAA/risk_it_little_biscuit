using UnityEngine;
using System.Collections.Generic;

public class LevelPoint : MonoBehaviour
{
    public float[] extraData;

    // Tabellen für die Werte je Button (pro Level)
    public Dictionary<int, List<float>> buttonValueTables = new Dictionary<int, List<float>>();

    public static LevelPoint Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        buttonValueTables.Clear();

        // 0 - Currency Gain
        buttonValueTables[0] = new List<float> { 0f, 0.1f, 0.2f, 0.35f, 0.5f };

        // 1 - Reroll
        buttonValueTables[1] = new List<float> { 0f, 1f, 2f, 3f, 4f, 5f };

        // 2 - Banish
        buttonValueTables[2] = new List<float> { 0f, 1f, 2f, 3f, 4f, 5f };

        // 3 - Start XP
        buttonValueTables[3] = new List<float> { 0f, 3f, 13f, 33f, 63f };

        // 4 - Shrink Speed
        buttonValueTables[4] = new List<float> { 0f, 0.1f, 0.2f, 0.3f, 0.4f };

        // 5 - Buff Slot
        buttonValueTables[5] = new List<float> { 0f, 1f, 2f , 3f };

        // 6 - Weapon Slot
        buttonValueTables[6] = new List<float> { 0f, 1f, 2f, 3f };

        // 7 - Evo Slot
        buttonValueTables[7] = new List<float> { 0f, 1f, 2f, 3f, 4f, 5f };

        // Boba Gun
        buttonValueTables[8] = new List<float> { 0f, 1f};

        // Shurikookie
        buttonValueTables[9] = new List<float> { 0f, 1f};

        // Spikefork
        buttonValueTables[10] = new List<float> { 0f, 1f};

        // Deathstrike
        buttonValueTables[11] = new List<float> { 0f, 1f};

        // Celestial Star
        buttonValueTables[12] = new List<float> { 0f, 1f};

        // Blade Swarm
        buttonValueTables[13] = new List<float> { 0f, 1f};

        // Candy Bomb
        buttonValueTables[14] = new List<float> { 0f, 1f};

        // Time Laser
        buttonValueTables[15] = new List<float> { 0f, 1f};

        buttonValueTables[16] = new List<float> { 0f, 1f }; // XP Gain
        buttonValueTables[17] = new List<float> { 0f, 1f }; // Currency
        buttonValueTables[18] = new List<float> { 0f, 1f }; // Life Steal
        buttonValueTables[19] = new List<float> { 0f, 1f }; // Luck
        buttonValueTables[20] = new List<float> { 0f, 1f }; // Extra Shot
        buttonValueTables[21] = new List<float> { 0f, 1f }; // AOE Range
        buttonValueTables[22] = new List<float> { 0f, 1f }; // Damage
        buttonValueTables[23] = new List<float> { 0f, 1f }; // Crit Chance
        buttonValueTables[24] = new List<float> { 0f, 1f }; // Crit Damage

        UpdateExtraData();
    }

    // Mapping: ButtonIndex -> extraDataIndex
    private static readonly Dictionary<int, int> ButtonToExtra = new Dictionary<int, int>
    {
        { 0, 2 }, // Currency Gain
        { 1, 3 }, // Reroll
        { 2, 4 }, // Banish
        { 3, 5 }, // Start XP
        { 4, 6 }, // Shrink Speed
        { 5, 7 }, // Buff Slot
        { 6, 8 }, // Weapon Slot
        { 7, 9 }, // Evo Slot
        { 8, 10 }, // Boba Gun
        { 9, 11 }, // Shurikookie
        { 10, 12 }, // Spikefork
        { 11, 13 }, // Deathstrike
        { 12, 14 }, // Celestial Star
        { 13, 15 }, // Blade Swarm
        { 14, 16 }, // Candy Bomb
        { 15, 17 }, // Time Laser
        { 16, 18 }, // XP Gain
        { 17, 19 }, // Currency Gain
        { 18, 20 }, // Life Steal
        { 19, 21 }, // Luck
        { 20, 22 }, // Extra Shot
        { 21, 23 }, // AOE Range
        { 22, 24 }, // Damage
        { 23, 25 }, // Crit Chance
        { 24, 26 }, // Crit Damage
    };

    public void UpdateExtraData()
    {
        extraData = new float[30];

        // Char / weapon
        extraData[0] = SaveGame.Instance.currentData.skinIndex;
        if (WM_PlayerSkinSwitcher.Instance != null)
        {
            WM_PlayerSkinSwitcher.Instance.skinIndex = (int)extraData[0];
        }
        Startweapon((int)extraData[0]);

        // Nur gemappte Buttons verarbeiten
        foreach (var btn in SaveGame.Instance.currentData.buttons)
        {
            if (!ButtonToExtra.TryGetValue(btn.index, out int extraIndex))
                continue;

            if (!buttonValueTables.TryGetValue(btn.index, out var values))
                continue;

            int level = btn.level;

            // wichtig: level ist "Käufe" (0..cost.Count), values sind pro Level.
            // Wenn values nur bis maxIndex gehen: clampen.
            float v = (level >= values.Count) ? values[values.Count - 1] : values[level];
            extraData[extraIndex] = v;
        }
    }

    public void Startweapon(int index)
    {
        switch (index)
        {
            case 0: extraData[1] = 2f; break;
            case 1: extraData[1] = 6f; break;
            case 2: extraData[1] = 11f; break;
            case 3: extraData[1] = 1f; break;
        }
    }
}
