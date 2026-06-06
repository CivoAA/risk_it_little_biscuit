using UnityEngine;

[System.Serializable]
public class PowerUpStats
{
    public PowerUpRarity rarity;
    [Header("Stat-Bereich (Min / Max)")]
    public Vector2 statRange;
}

public enum PowerUpRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}
