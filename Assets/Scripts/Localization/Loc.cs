using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sehr kleines Übersetzungssystem: eine JSON-Datei pro Sprache unter
/// Assets/Resources/Localization/[sprache].json.
///
/// Aufbau einer Datei (bewusst als Liste, weil Unitys JsonUtility keine
/// Wörterbücher kann - für Übersetzer trotzdem gut lesbar):
///
///   {
///     "language": "de",
///     "entries": [
///       { "key": "ach.First_Win.name", "value": "Glückwunsch!" }
///     ]
///   }
///
/// Fällt ein Schlüssel durch, greift der Reihe nach: gewählte Sprache ->
/// Englisch -> der im Code hinterlegte Originaltext. Eine fehlende Übersetzung
/// kann also nie zu einem leeren Feld im Spiel führen.
/// </summary>
public static class Loc
{
    public const string DefaultLanguage = "en";
    private const string Folder = "Localization/";
    private const string PrefsKey = "loc_language";

    [Serializable]
    private class LocEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class LocFile
    {
        public string language;
        public List<LocEntry> entries = new List<LocEntry>();
    }

    private static Dictionary<string, string> current;
    private static Dictionary<string, string> fallback;
    private static string language;

    /// <summary>Feuert, wenn die Sprache gewechselt wurde - UI kann sich daran neu aufbauen.</summary>
    public static event Action LanguageChanged;

    /// <summary>Sprachen, die als Datei vorliegen.</summary>
    public static readonly string[] Available = { "en", "de" };

    public static string Language
    {
        get
        {
            if (language == null) Init();
            return language;
        }
        set => SetLanguage(value);
    }

    private static void Init()
    {
        string saved = PlayerPrefs.GetString(PrefsKey, null);
        if (string.IsNullOrEmpty(saved)) saved = SystemDefault();
        Apply(saved);
    }

    /// <summary>Die Sprache, die ohne eigene Wahl gelten würde.</summary>
    public static string SystemDefault()
    {
        return Application.systemLanguage == SystemLanguage.German ? "de" : DefaultLanguage;
    }

    public static void SetLanguage(string lang)
    {
        if (string.IsNullOrEmpty(lang)) lang = DefaultLanguage;
        if (language == lang) return;

        Apply(lang);
        PlayerPrefs.SetString(PrefsKey, language);
        PlayerPrefs.Save();

        try
        {
            LanguageChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Loc] Fehler im LanguageChanged-Handler: {e}");
        }
    }

    private static void Apply(string lang)
    {
        language = lang;
        current = LoadFile(lang);
        fallback = lang == DefaultLanguage ? null : LoadFile(DefaultLanguage);
    }

    /// <summary>Übersetzung zu einem Schlüssel, sonst der mitgegebene Originaltext.</summary>
    public static string Get(string key, string fallbackText)
    {
        if (string.IsNullOrEmpty(key)) return fallbackText;
        if (language == null) Init();

        if (current != null && current.TryGetValue(key, out string hit) && !string.IsNullOrEmpty(hit))
            return hit;

        if (fallback != null && fallback.TryGetValue(key, out string en) && !string.IsNullOrEmpty(en))
            return en;

        return fallbackText;
    }

    /// <summary>Wie <see cref="Get"/>, aber ohne Originaltext im Code - liefert notfalls den Schlüssel.</summary>
    public static string T(string key) => Get(key, key);

    public static bool Has(string key)
    {
        if (language == null) Init();
        return current != null && current.ContainsKey(key);
    }

    private static Dictionary<string, string> LoadFile(string lang)
    {
        var map = new Dictionary<string, string>();

        TextAsset asset = Resources.Load<TextAsset>(Folder + lang);
        if (asset == null)
        {
            Debug.LogWarning($"[Loc] Keine Sprachdatei Assets/Resources/{Folder}{lang}.json gefunden.");
            return map;
        }

        LocFile file;
        try
        {
            file = JsonUtility.FromJson<LocFile>(asset.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Loc] Sprachdatei '{lang}' ist fehlerhaft: {e.Message}");
            return map;
        }

        if (file?.entries == null) return map;

        foreach (LocEntry e in file.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key)) continue;
            map[e.key] = e.value;
        }

        return map;
    }

    /// <summary>Lädt die Sprachdateien neu - nach einer Änderung im Editor.</summary>
    public static void Reload()
    {
        if (language == null) Init();
        else Apply(language);
    }
}
