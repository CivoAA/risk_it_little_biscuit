using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hilfsfunktionen, um in der Test-Szene Waffen, Buffs, Evos und das
/// Spieler-Level direkt zu setzen.
///
/// Wichtig: Buffs addieren ihren Wert in ihrem eigenen Update, sobald sich
/// <c>weaponLevel</c> ändert – und zwar immer nur den Wert der aktuellen Stufe.
/// Wer im Spiel von Stufe 0 auf 3 kommt, hat die Werte von 0, 1, 2 und 3
/// aufaddiert. Deshalb wird hier nie direkt auf eine Zielstufe gesprungen,
/// sondern Stufe für Stufe (ein Schritt pro Frame) hochgezählt.
/// </summary>
public static class TestSceneLoadout
{
    /// <summary>Alle Waffen, Buffs und Evos des Spielers.</summary>
    public static IEnumerable<Weapon> All(PlayerController player)
    {
        foreach (Weapon w in Group(player, Category.Weapons)) yield return w;
        foreach (Weapon w in Group(player, Category.Buffs)) yield return w;
        foreach (Weapon w in Group(player, Category.Evos)) yield return w;
    }

    public enum Category
    {
        Weapons,
        Buffs,
        Evos
    }

    public static Weapon[] Group(PlayerController player, Category category)
    {
        if (player == null)
        {
            return new Weapon[0];
        }

        switch (category)
        {
            case Category.Weapons: return player.activeWeapon ?? new Weapon[0];
            case Category.Buffs: return player.activeBuffs ?? new Weapon[0];
            case Category.Evos: return player.activeEvos ?? new Weapon[0];
            default: return new Weapon[0];
        }
    }

    /// <summary>Höchste erreichbare Stufe – begrenzt durch maxweaponLevel und die Stats-Liste.</summary>
    public static int MaxLevelOf(Weapon weapon)
    {
        if (weapon == null)
        {
            return 0;
        }

        int statsMax = weapon.stats != null ? weapon.stats.Count - 1 : 0;
        return Mathf.Max(0, Mathf.Min(weapon.maxweaponLevel, statsMax));
    }

    public static bool IsOwned(Weapon weapon)
    {
        return weapon != null && weapon.weaponLevel >= 0;
    }

    // ------------------------------------------------------------------
    // Einzelne Schritte
    // ------------------------------------------------------------------

    /// <summary>Eine Stufe höher. Aus "nicht besessen" wird Stufe 0.</summary>
    public static void StepUp(PlayerController player, Weapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.hasBeenRemoved = false;

        if (weapon.weaponLevel < 0)
        {
            weapon.weaponLevel = 0;
        }
        else if (weapon.weaponLevel < MaxLevelOf(weapon))
        {
            weapon.weaponLevel++;
        }
        else
        {
            return;
        }

        MarkEvoPartners(player, weapon);
    }

    /// <summary>
    /// Eine Stufe tiefer. Von Stufe 0 aus wird die Waffe abgelegt.
    /// Achtung: Buff-Boni, die bereits auf den Spieler addiert wurden,
    /// verschwinden dadurch nicht – dafür die Szene neu starten.
    /// </summary>
    public static void StepDown(Weapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.weaponLevel <= 0)
        {
            Remove(weapon);
        }
        else
        {
            weapon.weaponLevel--;
        }
    }

    public static void Remove(Weapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.weaponLevel = -1;
        weapon.hasBeenRemoved = false;
        weapon.posssibleEvo = false;
    }

    public static void ClearAll(PlayerController player)
    {
        foreach (Weapon weapon in All(player))
        {
            Remove(weapon);
        }
    }

    /// <summary>
    /// Zählt Stufe für Stufe (ein Schritt pro Frame) bis zur Zielstufe hoch,
    /// damit Buffs ihre Werte genauso aufaddieren wie im echten Spiel.
    /// </summary>
    public static IEnumerator RaiseTo(PlayerController player, Weapon weapon, int targetLevel)
    {
        if (weapon == null)
        {
            yield break;
        }

        targetLevel = Mathf.Clamp(targetLevel, 0, MaxLevelOf(weapon));

        while (weapon.weaponLevel < targetLevel)
        {
            StepUp(player, weapon);
            yield return null;
        }
    }

    /// <summary>Setzt alle Waffen auf einmal auf Maximalstufe (stufenweise).</summary>
    public static IEnumerator MaxOut(PlayerController player, Weapon weapon)
    {
        return RaiseTo(player, weapon, MaxLevelOf(weapon));
    }

    private static void MarkEvoPartners(PlayerController player, Weapon weapon)
    {
        if (player == null || player.EvoCombinations == null)
        {
            return;
        }

        weapon.posssibleEvo = true;

        foreach (EvoRecipe recipe in player.EvoCombinations)
        {
            if (recipe == null)
            {
                continue;
            }

            if (recipe.RequiredWeapon1 == weapon && recipe.RequiredWeapon2 != null)
            {
                recipe.RequiredWeapon2.posssibleEvo = true;
            }
            else if (recipe.RequiredWeapon2 == weapon && recipe.RequiredWeapon1 != null)
            {
                recipe.RequiredWeapon1.posssibleEvo = true;
            }
        }
    }

    // ------------------------------------------------------------------
    // Spieler-Level
    // ------------------------------------------------------------------

    /// <summary>
    /// Schenkt genau so viel Erfahrung, dass das nächste Level erreicht wird.
    /// Der Level-Up läuft danach ganz normal über PlayerController.Update,
    /// inklusive Level-Up-Panel und Waffenauswahl.
    /// </summary>
    public static void GrantLevelUp(PlayerController player)
    {
        if (player == null || player.playerLevels == null || player.playerLevels.Count == 0)
        {
            return;
        }

        if (player.currentLevel >= player.maxLevel)
        {
            return;
        }

        int index = Mathf.Clamp(player.currentLevel - 1, 0, player.playerLevels.Count - 1);
        player.experience = player.playerLevels[index];
        UIController.Instance?.UpdateExperienceSlider();
    }

    /// <summary>
    /// Setzt das Spieler-Level direkt, ohne Level-Up-Panels zu öffnen.
    /// Gedacht zum schnellen Testen von level-abhängigen Effekten.
    /// </summary>
    public static void SetPlayerLevel(PlayerController player, int level)
    {
        if (player == null || player.playerLevels == null || player.playerLevels.Count == 0)
        {
            return;
        }

        int max = Mathf.Max(1, Mathf.Min(player.maxLevel, player.playerLevels.Count));
        player.currentLevel = Mathf.Clamp(level, 1, max);
        player.experience = 0f;
        player.LevelUpSelectet = true;
        UIController.Instance?.UpdateExperienceSlider();
    }
}
