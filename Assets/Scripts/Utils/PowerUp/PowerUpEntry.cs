using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PowerUpEntry
{
    [Header("Allgemeine Infos")]
    public string powerUpName;
    [TextArea(2, 4)] public string descriptionBefore;
    [TextArea(2, 4)] public string descriptionAfter;

    [Header("Rarity-Werte")]
    public List<PowerUpStats> rarityStats = new List<PowerUpStats>();
}
