using UnityEditor;
using UnityEngine;

/// <summary>
/// Sammelknopf für die vier Kataloge (Achievements, Shop, Skills, Unlocks).
/// Praktisch vor einem Build - und von aussen aufrufbar über
/// <c>Unity.exe -batchmode -executeMethod CatalogTools.ValidateAll</c>.
/// </summary>
public static class CatalogTools
{
    [MenuItem("Tools/Alle Kataloge prüfen", priority = -100)]
    public static void ValidateAll()
    {
        Debug.Log("===== Katalog-Prüfung =====");

        AchievementTools.Validate();
        ShopTools.Validate();
        SkillTools.Validate();
        UnlockTools.Validate();

        Debug.Log($"===== Fertig: {Ach.All.Count} Achievements, {Shop.All.Count} Shop-Einträge, " +
                  $"{CountSkills()} Skills, {Unlocks.All.Count} Unlocks =====");
    }

    private static int CountSkills()
    {
        int n = 0;
        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillNodeDef unused in tree.AllNodes()) n++;
        }
        return n;
    }
}
