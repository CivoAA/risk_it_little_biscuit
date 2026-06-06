using UnityEngine;

public static class PowerUpRaritySelector
{
    // Basis-Chancen in %: Legendary → Epic → Rare → Uncommon (Common ist Fallback)
    private static readonly float[] baseChances = { 2f, 5f, 10f, 25f };

    private static readonly PowerUpRarity[] order =
    {
        PowerUpRarity.Legendary,
        PowerUpRarity.Epic,
        PowerUpRarity.Rare,
        PowerUpRarity.Uncommon,
        PowerUpRarity.Common // Fallback
    };

    /// <summary>
    /// Gibt eine Rarity zurück, skaliert mit Luck (100 Luck = x2, 200 = x3, ...).
    /// Überschreitungen >100% "drücken" die nachfolgenden Stufen weg.
    /// </summary>
    public static PowerUpRarity GetRandomRarity(float luck)
    {
        float mult = 1f + (luck / 100f);

        // Luck-angepasste Chancen
        float[] modified = new float[baseChances.Length];
        for (int i = 0; i < baseChances.Length; i++)
            modified[i] = baseChances[i] * mult;

        // Wenn kumuliert >= 100%, nachfolgende Stufen entfernen
        float total = 0f;
        for (int i = 0; i < modified.Length; i++)
        {
            total += modified[i];
            if (total >= 100f)
            {
                for (int j = i + 1; j < modified.Length; j++)
                    modified[j] = 0f;
                break;
            }
        }

        // Ziehen
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;
        for (int i = 0; i < modified.Length; i++)
        {
            cumulative += modified[i];
            if (roll <= cumulative)
                return order[i];
        }

        // Fallback
        return PowerUpRarity.Common;
    }
}
