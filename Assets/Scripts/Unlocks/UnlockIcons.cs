using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Symbol zu einem Unlock, gefunden allein ueber den Namen.
///
/// Gesucht wird der Reihe nach:
///   Assets/Resources/Unlocks/[IconKey].png   eigenes Bild fuer den Unlock
///   Assets/Resources/Shop/[IconKey].png      das Bild des Shop-Eintrags
///   Assets/Resources/Shop/_missing.png       Platzhalter
///
/// Die meisten Unlocks zeigen genau das, was sie im Shop freischalten - deshalb
/// die zweite Stufe: der Katalog traegt dort einfach die Shop-Id als icon ein und
/// die Grafik liegt nur einmal im Projekt.
/// </summary>
public static class UnlockIcons
{
    private static readonly string[] Folders = { "Unlocks/", "Shop/" };

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> warned = new HashSet<string>();

    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return Missing;

        if (cache.TryGetValue(key, out Sprite cached)) return cached;

        Sprite sprite = null;

        foreach (string folder in Folders)
        {
            sprite = LoadRaw(folder + key);
            if (sprite != null) break;
        }

        if (sprite == null)
        {
            if (warned.Add(key))
            {
                Debug.Log($"[Unlocks] Kein Symbol fuer '{key}' - erwartet wird " +
                          $"Assets/Resources/Unlocks/{key}.png (oder ein Shop-Bild gleichen Namens). " +
                          "Es wird der Platzhalter angezeigt.");
            }
            sprite = Missing;
        }

        cache[key] = sprite;
        return sprite;
    }

    public static Sprite Missing => ShopIcons.Missing;

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
