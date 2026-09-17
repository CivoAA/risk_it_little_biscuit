using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Findet das Symbol zu einem Achievement allein über seinen Namen - ohne
/// Inspector, ohne Prefab, ohne dass irgendwo etwas verdrahtet werden muss.
///
/// Ablageort:  Assets/Resources/Achievements/[IconKey].png
/// IconKey:    standardmässig die Achievement-Id in Kleinbuchstaben,
///             also z.B. Assets/Resources/Achievements/first_win.png
///
/// Fehlt eine Datei, kommt der Platzhalter zurück und in der Konsole steht eine
/// Notiz. Ein Achievement ohne fertige Grafik ist damit kein Fehler, sondern nur
/// eine offene Aufgabe - das Spiel läuft weiter.
/// </summary>
public static class AchievementIcons
{
    private const string Folder = "Achievements/";

    /// <summary>Platzhalter für ein Achievement, dessen Grafik noch fehlt.</summary>
    public const string MissingKey = "_locked";

    /// <summary>Grafik für versteckte Achievements, solange sie zu sind.</summary>
    public const string HiddenKey = "_hidden";

    /// <summary>Haken-Overlay für freigeschaltete Einträge.</summary>
    public const string BadgeKey = "_badge_unlocked";

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
                Debug.Log($"[Achievements] Kein Symbol für '{key}' - erwartet wird " +
                          $"Assets/Resources/{Folder}{key}.png. Es wird der Platzhalter angezeigt.");
            }
            sprite = Get(MissingKey);
        }

        cache[key] = sprite;
        return sprite;
    }

    public static Sprite Missing => Get(MissingKey);
    public static Sprite Badge => Get(BadgeKey);

    /// <summary>
    /// Lädt sowohl PNGs im Sprite-Modus "Single" als auch solche im Modus
    /// "Multiple" mit einem einzelnen Teilbild - die alten Achievement-Grafiken
    /// sind nämlich als Multiple importiert.
    /// </summary>
    private static Sprite LoadRaw(string path)
    {
        Sprite direct = Resources.Load<Sprite>(path);
        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>(path);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    /// <summary>Nach dem Nachlegen einer Grafik im Editor aufrufen, damit sie sofort erscheint.</summary>
    public static void ClearCache()
    {
        cache.Clear();
        warned.Clear();
    }
}
