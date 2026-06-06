using System.Collections.Generic;
using UnityEngine;

public class PowerUpDatabase : MonoBehaviour
{
    [Header("Alle PowerUps im Spiel")]
    public List<PowerUpEntry> powerUps = new List<PowerUpEntry>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (powerUps == null) return;

        foreach (var p in powerUps)
        {
            if (p == null) continue;

            if (p.rarityStats == null)
                p.rarityStats = new List<PowerUpStats>();

            var allRarities = System.Enum.GetValues(typeof(PowerUpRarity));

            foreach (PowerUpRarity rarity in allRarities)
            {
                if (!p.rarityStats.Exists(r => r.rarity == rarity))
                {
                    p.rarityStats.Add(new PowerUpStats
                    {
                        rarity = rarity,
                        statRange = new Vector2(0, 1)
                    });
                }
            }

            // sortiert die Rarities schön im Inspector
            p.rarityStats.Sort((a, b) => a.rarity.CompareTo(b.rarity));
        }
    }
#endif
}
