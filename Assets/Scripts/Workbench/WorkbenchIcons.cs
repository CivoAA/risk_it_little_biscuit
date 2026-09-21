using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Findet das auf das Werkbank-Raster gebrachte Symbol zu einer Waffen-Id.
///
/// Ablageort:  Assets/Resources/Workbench/[id]_[groesse].png
///             z.B. Workbench/cookie_saw_14.png
///
/// Es gibt zwei Groessen, beide erzeugt aus genau dem Icon, das am
/// Weapon-Bauteil im Player-Prefab haengt:
///
///   _14   14x14 Leinwand, 12x12 Motiv   Verteiler- und Bibliothekskachel
///   _10   10x10 Leinwand,  8x8 Motiv    Evo-Chip und Evo-Zeile
///
/// Leinwand = Slot-Aussenmass, Motiv = Slot-Oeffnung. Damit laesst sich das
/// Symbol ohne Versatz auf dasselbe Rechteck legen wie der Rahmen.
///
/// Fehlt eine Datei, kommt das Symbol am Player-Prefab als Rueckfallebene -
/// das ist dann zwar 64x64 und wird von Unity heruntergerechnet, aber es
/// steht etwas da. Welche Dateien fehlen, listet
/// Tools > Werkbank > Icons pruefen.
/// </summary>
public static class WorkbenchIcons
{
    private const string Folder = "Workbench/";

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> warned = new HashSet<string>();

    public static Sprite Get(string id, int size)
    {
        if (string.IsNullOrEmpty(id)) return null;

        string key = id + "_" + size;

        // != null statt TryGetValue allein: raeumt Unity die Resources zwischen
        // zwei Szenen auf, steht im Cache eine zerstoerte Referenz.
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Sprite sprite = Resources.Load<Sprite>(Folder + key);

        if (sprite == null)
        {
            sprite = Fallback(id);
            if (warned.Add(key))
            {
                Debug.Log($"[Werkbank] Kein Symbol '{key}' - erwartet wird " +
                          $"Assets/Resources/{Folder}{key}.png. " +
                          (sprite != null ? "Es wird das Prefab-Icon genommen."
                                          : "Die Kachel bleibt leer."));
            }
        }

        cache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Das ungerasterte Original aus dem Shop-Ordner. Deckt die Faelle ab, in
    /// denen eine Waffe schon einen Shop-Eintrag, aber noch kein Werkbank-Icon
    /// hat - und haelt die Kachel damit nie leer.
    /// </summary>
    private static Sprite Fallback(string id)
    {
        Sprite direct = Resources.Load<Sprite>("Shop/" + id);
        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>("Shop/" + id);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static void ClearCache()
    {
        cache.Clear();
        warned.Clear();
    }
}
