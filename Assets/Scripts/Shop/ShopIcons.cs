using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Findet das Symbol zu einem Shop-Eintrag allein über seinen Namen.
///
/// Ablageort:  Assets/Resources/Shop/[IconKey].png
/// IconKey:    standardmässig die Item-Id, also z.B.
///             Assets/Resources/Shop/buff_slot.png
///
/// Fehlt eine Datei, kommt der Platzhalter und in der Konsole steht einmal,
/// welcher Dateiname erwartet wird. Ein Eintrag ohne fertige Grafik ist damit
/// kein Fehler, sondern eine offene Aufgabe.
///
/// Welche Dateien noch fehlen, listet Tools -> Shop -> Katalog prüfen.
/// </summary>
public static class ShopIcons
{
    private const string Folder = "Shop/";

    /// <summary>Platzhalter, wenn die Grafik fehlt.</summary>
    public const string MissingKey = "_missing";

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> warned = new HashSet<string>();

    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return Get(MissingKey);

        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Sprite sprite = LoadRaw(Folder + key);

        if (sprite == null && key != MissingKey)
        {
            if (warned.Add(key))
            {
                Debug.Log($"[Shop] Kein Symbol für '{key}' - erwartet wird " +
                          $"Assets/Resources/{Folder}{key}.png. Es wird der Platzhalter angezeigt.");
            }
            sprite = Get(MissingKey);
        }

        cache[key] = sprite;
        return sprite;
    }

    public static Sprite Missing => Get(MissingKey);

    /// <summary>Lädt PNGs im Sprite-Modus "Single" und solche im Modus "Multiple".</summary>
    private static Sprite LoadRaw(string path)
    {
        Sprite direct = Resources.Load<Sprite>(path);
        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>(path);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static void ClearCache()
    {
        cache.Clear();
        warned.Clear();
    }
}
