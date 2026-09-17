using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um den Unlock-Katalog. Alles unter Tools -> Unlocks.
/// Die Prüfung läuft ausserdem automatisch nach jedem Compile.
/// </summary>
public static class UnlockTools
{
    private const string LocFolder = "Assets/Resources/Localization";

    [MenuItem("Tools/Unlocks/Katalog prüfen")]
    public static void Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateInto(errors, warnings);

        foreach (string w in warnings) Debug.LogWarning("[Unlocks] " + w);
        foreach (string e in errors) Debug.LogError("[Unlocks] " + e);

        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log($"[Unlocks] Katalog in Ordnung - {Unlocks.All.Count} Einträge.");
        else
            Debug.Log($"[Unlocks] Prüfung fertig: {errors.Count} Fehler, {warnings.Count} Hinweise.");
    }

    [DidReloadScripts]
    private static void ValidateOnReload()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            ValidateInto(errors, warnings);
        }
        catch (Exception e)
        {
            Debug.LogError("[Unlocks] Prüfung abgebrochen: " + e.Message);
            return;
        }

        foreach (string err in errors) Debug.LogError("[Unlocks] " + err);
    }

    private static void ValidateInto(List<string> errors, List<string> warnings)
    {
        var seen = new HashSet<string>();
        var idRegex = new Regex("^[a-z0-9_]+$");

        Dictionary<string, string> symbolById = SymbolsById();
        HashSet<string> granted = ReferencedSymbols();
        HashSet<string> required = RequiredByShop();
        HashSet<string> enKeys = KeysOf("en");
        HashSet<string> deKeys = KeysOf("de");

        foreach (UnlockDef def in Unlocks.All)
        {
            if (!seen.Add(def.Id))
                errors.Add($"Doppelte Id '{def.Id}' im Katalog.");

            if (!idRegex.IsMatch(def.Id))
                errors.Add($"Id '{def.Id}': bitte nur Kleinbuchstaben, Ziffern und _.");

            if (!enKeys.Contains($"unlock.{def.Id}.name") || !enKeys.Contains($"unlock.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in en.json.");

            if (!deKeys.Contains($"unlock.{def.Id}.name") || !deKeys.Contains($"unlock.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in de.json.");

            if (symbolById.TryGetValue(def.Id, out string symbol) && !granted.Contains(symbol))
            {
                warnings.Add($"'{def.Id}' (Unlocks.{symbol}) wird im Code nie vergeben - " +
                             "er kann so nie aufgehen.");
            }

            if (!required.Contains(def.Id))
            {
                warnings.Add($"'{def.Id}' wird von keinem Shop-Eintrag verlangt - " +
                             "er schaltet damit nichts frei.");
            }
        }

        // Umgekehrt: verlangt ein Shop-Eintrag etwas, das es nicht gibt?
        foreach (string id in required)
        {
            if (Unlocks.Find(id) == null)
                errors.Add($"Ein Shop-Eintrag verlangt Unlock '{id}', den der Katalog nicht kennt.");
        }
    }

    private static Dictionary<string, string> SymbolsById()
    {
        var map = new Dictionary<string, string>();

        foreach (FieldInfo field in typeof(Unlocks).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(UnlockDef)) continue;
            if (field.GetValue(null) is UnlockDef def && !map.ContainsKey(def.Id))
                map.Add(def.Id, field.Name);
        }

        return map;
    }

    private static HashSet<string> ReferencedSymbols()
    {
        var found = new HashSet<string>();
        var regex = new Regex(@"Unlocks\.Grant\(Unlocks\.([A-Za-z_][A-Za-z0-9_]*)\)");

        string scriptRoot = Path.Combine(Application.dataPath, "Scripts");

        foreach (string path in Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            if (name == "Unlocks.cs" || name == "UnlockTools.cs") continue;

            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

            foreach (Match m in regex.Matches(text)) found.Add(m.Groups[1].Value);
        }

        return found;
    }

    private static HashSet<string> RequiredByShop()
    {
        var ids = new HashSet<string>();

        foreach (ShopItemDef def in Shop.All)
        {
            if (!string.IsNullOrWhiteSpace(def.RequiredUnlock)) ids.Add(def.RequiredUnlock);
        }

        return ids;
    }

    private static HashSet<string> KeysOf(string language)
    {
        var keys = new HashSet<string>();
        string path = Path.Combine(LocFolder, language + ".json");
        if (!File.Exists(path)) return keys;

        foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"key\"\\s*:\\s*\"([^\"]+)\""))
            keys.Add(m.Groups[1].Value);

        return keys;
    }

    [MenuItem("Tools/Unlocks/Spielstand zurücksetzen")]
    public static void ResetSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "unlocks.json");

        bool ok = EditorUtility.DisplayDialog(
            "Unlocks zurücksetzen",
            "Alle Unlocks werden wieder gesperrt.\n\n" + path,
            "Zurücksetzen", "Abbrechen");

        if (!ok) return;

        if (Application.isPlaying) Unlocks.ResetAll();
        else if (File.Exists(path)) File.Delete(path);

        Debug.Log("[Unlocks] Spielstand zurückgesetzt.");
    }
}
