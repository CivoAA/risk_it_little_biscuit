using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Symbole für Skill-Knoten und Äste, gefunden allein über den Namen.
///
///   Assets/Resources/Skills/[SkillType].png   z.B. IncreaseDamage.png
///   Assets/Resources/Skills/_locked.png       allgemein gesperrt
///   Assets/Resources/Skills/_unlocked.png     allgemein freigeschaltet
///
/// Gibt es für einen Effekt noch kein eigenes Bild, greift automatisch der
/// allgemeine Gesperrt-/Freigeschaltet-Look - genau so sah der Baum bisher aus,
/// da waren für alle 21 Effekte dieselben zwei Bilder eingetragen.
/// </summary>
public static class SkillIcons
{
    private const string Folder = "Skills/";

    public const string LockedKey = "_locked";
    public const string UnlockedKey = "_unlocked";

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Bild für einen Knoten, je nach Zustand.</summary>
    public static Sprite For(SkillNodeDef node, bool unlocked)
    {
        if (node == null) return Get(LockedKey);

        Sprite own = Get(node.Effect.ToString(), warn: false);
        if (own != null) return own;

        return Get(unlocked ? UnlockedKey : LockedKey);
    }

    /// <summary>Bild eines Astes. Leerer Schlüssel = keins.</summary>
    public static Sprite ForBranch(SkillBranchDef branch)
    {
        if (branch == null || string.IsNullOrEmpty(branch.IconKey)) return null;
        return Get(branch.IconKey, warn: false);
    }

    public static Sprite Locked => Get(LockedKey);
    public static Sprite Unlocked => Get(UnlockedKey);

    public static Sprite Get(string key, bool warn = true)
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Sprite sprite = LoadRaw(Folder + key);

        if (sprite == null && warn)
            Debug.Log($"[Skills] Kein Symbol Assets/Resources/{Folder}{key}.png.");

        cache[key] = sprite;
        return sprite;
    }

    private static Sprite LoadRaw(string path)
    {
        Sprite direct = Resources.Load<Sprite>(path);
        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>(path);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static void ClearCache() => cache.Clear();
}
