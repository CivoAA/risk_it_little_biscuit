using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um den Achievement-Katalog. Menue unter Tools -> Erfolge,
/// der Spielstand unter Tools -> Spielstand.
///
///   Katalog prüfen        - findet doppelte Ids, fehlende Grafiken, fehlende
///                           Übersetzungen und Achievements, die im Code nie
///                           ausgelöst werden.
///   Steamworks-Liste      - die Tabelle, die im Steamworks-Backend eingetragen
///                           werden muss, damit Steam die Achievements kennt.
///   Sprachdatei-Vorlage   - erzeugt eine json mit allen Schlüsseln zum Übersetzen.
///
/// Die Prüfung läuft ausserdem automatisch nach jedem Compile.
/// </summary>
public static class AchievementTools
{
    private const string IconFolder = "Assets/Resources/Achievements";
    private const string LocFolder = "Assets/Resources/Localization";

    // ==================================================================
    //  Prüfung
    // ==================================================================

    [MenuItem("Tools/Erfolge/Katalog prüfen", false, 201)]
    public static void Validate()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        ValidateInto(errors, warnings);

        foreach (string w in warnings) Debug.LogWarning("[Achievements] " + w);
        foreach (string e in errors) Debug.LogError("[Achievements] " + e);

        if (errors.Count == 0 && warnings.Count == 0)
        {
            Debug.Log($"[Achievements] Katalog in Ordnung - {Ach.All.Count} Achievements, nichts zu meckern.");
        }
        else
        {
            Debug.Log($"[Achievements] Prüfung fertig: {errors.Count} Fehler, {warnings.Count} Hinweise.");
        }
    }

    [DidReloadScripts]
    private static void ValidateOnReload()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        try
        {
            ValidateInto(errors, warnings);
        }
        catch (Exception e)
        {
            Debug.LogError("[Achievements] Prüfung abgebrochen: " + e.Message);
            return;
        }

        // Nach dem Compile nur echte Fehler zeigen, damit die Konsole nicht zuläuft.
        foreach (string err in errors) Debug.LogError("[Achievements] " + err);
    }

    private static void ValidateInto(List<string> errors, List<string> warnings)
    {
        var seen = new HashSet<string>();
        var idRegex = new Regex("^[A-Za-z0-9_]+$");

        Dictionary<string, string> symbolById = SymbolsById();
        HashSet<string> referenced = ReferencedSymbols();
        HashSet<string> enKeys = KeysOf("en");
        HashSet<string> deKeys = KeysOf("de");

        foreach (AchievementDef def in Ach.All)
        {
            if (!seen.Add(def.Id))
                errors.Add($"Doppelte Id '{def.Id}' im Katalog.");

            if (!idRegex.IsMatch(def.Id))
                errors.Add($"Id '{def.Id}' enthält Zeichen, die Steam nicht erlaubt (nur A-Z, a-z, 0-9, _).");

            if (def.Goal <= 0f)
                errors.Add($"'{def.Id}' hat ein Ziel von {def.Goal} - muss grösser als 0 sein.");

            if (string.IsNullOrWhiteSpace(def.NameEn))
                errors.Add($"'{def.Id}' hat keinen englischen Namen.");

            if (!File.Exists(Path.Combine(IconFolder, def.IconKey + ".png")))
                warnings.Add($"'{def.Id}': keine Grafik {IconFolder}/{def.IconKey}.png - es wird der Platzhalter angezeigt.");

            if (!enKeys.Contains($"ach.{def.Id}.name") || !enKeys.Contains($"ach.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in en.json.");

            if (!deKeys.Contains($"ach.{def.Id}.name") || !deKeys.Contains($"ach.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in de.json.");

            if (symbolById.TryGetValue(def.Id, out string symbol) && !referenced.Contains(symbol))
            {
                warnings.Add($"'{def.Id}' (Ach.{symbol}) wird im Code nirgends ausgelöst - " +
                             "es kann so nie freigeschaltet werden.");
            }
        }

        foreach (string key in new[] { "ach.hidden.name", "ach.hidden.desc" })
        {
            if (!enKeys.Contains(key)) warnings.Add($"Schlüssel '{key}' fehlt in en.json.");
        }

        foreach (string special in new[] { AchievementIcons.MissingKey, AchievementIcons.HiddenKey })
        {
            if (!File.Exists(Path.Combine(IconFolder, special + ".png")))
                warnings.Add($"Platzhalter {IconFolder}/{special}.png fehlt.");
        }
    }

    /// <summary>Feldname im Katalog je Achievement-Id, z.B. "First_Win" -> "FirstWin".</summary>
    private static Dictionary<string, string> SymbolsById()
    {
        var map = new Dictionary<string, string>();

        foreach (FieldInfo field in typeof(Ach).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(AchievementDef)) continue;
            if (field.GetValue(null) is AchievementDef def && !map.ContainsKey(def.Id))
            {
                map.Add(def.Id, field.Name);
            }
        }

        return map;
    }

    /// <summary>Alle Ach.Xyz, die irgendwo im Projektcode vorkommen - ohne den Katalog selbst.</summary>
    private static HashSet<string> ReferencedSymbols()
    {
        var found = new HashSet<string>();
        var regex = new Regex(@"\bAch\.([A-Za-z_][A-Za-z0-9_]*)");

        string scriptRoot = Path.Combine(Application.dataPath, "Scripts");
        foreach (string path in Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            if (name == "Ach.cs" || name == "AchievementTools.cs") continue;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            foreach (Match m in regex.Matches(text)) found.Add(m.Groups[1].Value);
        }

        return found;
    }

    private static HashSet<string> KeysOf(string language)
    {
        var keys = new HashSet<string>();
        string path = Path.Combine(LocFolder, language + ".json");
        if (!File.Exists(path)) return keys;

        foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"key\"\\s*:\\s*\"([^\"]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }

        return keys;
    }

    // ==================================================================
    //  Steamworks
    // ==================================================================

    [MenuItem("Tools/Erfolge/Steamworks-Liste ausgeben", false, 212)]
    public static void PrintSteamList()
    {
        var sb = new StringBuilder();
        sb.AppendLine("API Name\tDisplay Name\tDescription\tHidden");

        foreach (AchievementDef def in Ach.All)
        {
            sb.AppendLine($"{def.SteamApiName}\t{Strip(def.NameEn)}\t{Strip(def.DescEn)}\t{(def.Hidden ? "Yes" : "No")}");
        }

        string outPath = Path.Combine(Application.dataPath, "..", "achievements_steamworks.tsv");
        outPath = Path.GetFullPath(outPath);

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"[Achievements] {Ach.All.Count} Einträge geschrieben nach:\n{outPath}\n\n{sb}");
        EditorUtility.RevealInFinder(outPath);
    }

    /// <summary>TMP-Farbtags raus - Steam zeigt sonst den Auszeichnungstext mit an.</summary>
    private static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return Regex.Replace(text, "<[^>]+>", "").Replace("\t", " ").Trim();
    }

    // ==================================================================
    //  Übersetzung
    // ==================================================================

    [MenuItem("Tools/Erfolge/Sprachdatei-Vorlage erzeugen", false, 213)]
    public static void ExportLocTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"language\": \"xx\",");
        sb.AppendLine("  \"entries\": [");

        var lines = new List<string>();

        foreach (AchievementDef def in Ach.All)
        {
            lines.Add($"    {{ \"key\": \"ach.{def.Id}.name\", \"value\": {Quote(def.NameEn)} }}");
            lines.Add($"    {{ \"key\": \"ach.{def.Id}.desc\", \"value\": {Quote(def.DescEn)} }}");
        }

        sb.AppendLine(string.Join(",\n", lines));
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "achievements_loc_template.json"));
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"[Achievements] Vorlage mit {Ach.All.Count * 2} Schlüsseln geschrieben nach:\n{outPath}");
        EditorUtility.RevealInFinder(outPath);
    }

    private static string Quote(string text)
    {
        if (text == null) return "\"\"";
        return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }

    // ==================================================================
    //  Play Mode
    // ==================================================================

    [MenuItem("Tools/Erfolge/Buch öffnen (nur im Play Mode)", false, 224)]
    private static void OpenBook()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Buch] Geht nur im Play Mode - das Fenster baut sich zur Laufzeit auf.");
            return;
        }
        AchievementsBookPanel.Toggle();
    }

    // ==================================================================
    //  Fortschritt
    // ==================================================================

    [MenuItem("Tools/Spielstand/Erfolge anzeigen", false, 311)]
    public static void ShowSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "achievements.json");

        if (!File.Exists(path))
        {
            Debug.Log($"[Achievements] Noch keine Speicherdatei unter {path}.");
            return;
        }

        Debug.Log($"[Achievements] {path}\n\n{File.ReadAllText(path)}");
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/Spielstand/Erfolge zurücksetzen", false, 321)]
    public static void ResetSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "achievements.json");

        bool ok = EditorUtility.DisplayDialog(
            "Achievements zurücksetzen",
            "Der lokale Achievement-Fortschritt wird gelöscht.\n\n" +
            "Steam bleibt unberührt - beim nächsten Start holt sich das Spiel den " +
            "Stand von dort zurück, falls Steam läuft.\n\n" + path,
            "Zurücksetzen", "Abbrechen");

        if (!ok) return;

        if (Application.isPlaying)
        {
            Achievements.ResetAll();
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }

        Debug.Log("[Achievements] Lokaler Fortschritt zurückgesetzt.");
    }
}
