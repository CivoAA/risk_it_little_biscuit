using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um den Shop-Katalog. Menue unter Tools -> Shop,
/// der Spielstand unter Tools -> Spielstand.
///
///   Katalog prüfen    - doppelte Ids, krumme Preis-/Werte-Listen, fehlende
///                       Grafiken, fehlende Übersetzungen, Einträge ohne Wirkung.
///   Übersicht         - alle Einträge mit Preisen und Werten als Tabelle.
///   Spielstand        - anzeigen oder zurücksetzen.
///
/// Die Prüfung läuft auch automatisch nach jedem Compile.
/// </summary>
public static class ShopTools
{
    private const string IconFolder = "Assets/Resources/Shop";
    private const string LocFolder = "Assets/Resources/Localization";

    // ==================================================================
    //  Prüfung
    // ==================================================================

    [MenuItem("Tools/Shop/Katalog prüfen", false, 202)]
    public static void Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateInto(errors, warnings);

        foreach (string w in warnings) Debug.LogWarning("[Shop] " + w);
        foreach (string e in errors) Debug.LogError("[Shop] " + e);

        if (errors.Count == 0 && warnings.Count == 0)
            Debug.Log($"[Shop] Katalog in Ordnung - {Shop.All.Count} Einträge, nichts zu meckern.");
        else
            Debug.Log($"[Shop] Prüfung fertig: {errors.Count} Fehler, {warnings.Count} Hinweise.");
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
            Debug.LogError("[Shop] Prüfung abgebrochen: " + e.Message);
            return;
        }

        foreach (string err in errors) Debug.LogError("[Shop] " + err);
    }

    private static void ValidateInto(List<string> errors, List<string> warnings)
    {
        var seen = new HashSet<string>();
        var seenWeapons = new HashSet<string>();
        var idRegex = new Regex("^[a-z0-9_]+$");

        Dictionary<string, string> symbolById = SymbolsById();
        HashSet<string> referenced = ReferencedSymbols();
        HashSet<string> enKeys = KeysOf("en");
        HashSet<string> deKeys = KeysOf("de");
        HashSet<string> unlockIds = KnownUnlockIds();

        foreach (ShopItemDef def in Shop.All)
        {
            if (!seen.Add(def.Id))
                errors.Add($"Doppelte Id '{def.Id}' im Katalog.");

            if (!idRegex.IsMatch(def.Id))
                errors.Add($"Id '{def.Id}': bitte nur Kleinbuchstaben, Ziffern und _.");

            if (def.Costs.Count == 0)
                errors.Add($"'{def.Id}' hat keine Preise - es liesse sich nie kaufen.");

            if (def.Values.Count != def.Costs.Count + 1)
            {
                errors.Add($"'{def.Id}': {def.Costs.Count} Preise, aber {def.Values.Count} Werte. " +
                           $"Es müssen {def.Costs.Count + 1} sein (Stufe 0 zählt mit).");
            }

            for (int i = 0; i < def.Costs.Count; i++)
            {
                if (def.Costs[i] < 0) errors.Add($"'{def.Id}': negativer Preis auf Stufe {i + 1}.");
            }

            if (!string.IsNullOrEmpty(def.UnlocksWeapon) && !seenWeapons.Add(def.UnlocksWeapon))
                errors.Add($"Waffe/Buff '{def.UnlocksWeapon}' hängt an mehr als einem Shop-Eintrag.");

            if (!File.Exists(Path.Combine(IconFolder, def.IconKey + ".png")))
                warnings.Add($"'{def.Id}': keine Grafik {IconFolder}/{def.IconKey}.png - es wird der Platzhalter angezeigt.");

            if (!enKeys.Contains($"shop.{def.Id}.name") || !enKeys.Contains($"shop.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in en.json.");

            if (!deKeys.Contains($"shop.{def.Id}.name") || !deKeys.Contains($"shop.{def.Id}.desc"))
                warnings.Add($"'{def.Id}' fehlt in de.json.");

            if (!string.IsNullOrWhiteSpace(def.RequiredUnlock) &&
                unlockIds.Count > 0 && !unlockIds.Contains(def.RequiredUnlock))
            {
                warnings.Add($"'{def.Id}' verlangt Unlock '{def.RequiredUnlock}', den der Katalog " +
                             "nicht kennt - der Eintrag taucht dann nie im Shop auf.");
            }

            // Ein Eintrag wirkt entweder über unlocksWeapon oder weil ihn jemand
            // im Code abfragt. Trifft beides nicht zu, tut der Kauf nichts.
            bool hasEffect = !string.IsNullOrEmpty(def.UnlocksWeapon);
            if (!hasEffect && symbolById.TryGetValue(def.Id, out string symbol))
                hasEffect = referenced.Contains(symbol);

            if (!hasEffect)
            {
                warnings.Add($"'{def.Id}' wird im Code nirgends abgefragt und schaltet auch keine " +
                             "Waffe frei - der Kauf hätte im Spiel keine Wirkung.");
            }
        }

        if (!File.Exists(Path.Combine(IconFolder, ShopIcons.MissingKey + ".png")))
            warnings.Add($"Platzhalter {IconFolder}/{ShopIcons.MissingKey}.png fehlt.");
    }

    private static Dictionary<string, string> SymbolsById()
    {
        var map = new Dictionary<string, string>();

        foreach (FieldInfo field in typeof(Shop).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(ShopItemDef)) continue;
            if (field.GetValue(null) is ShopItemDef def && !map.ContainsKey(def.Id))
                map.Add(def.Id, field.Name);
        }

        return map;
    }

    private static HashSet<string> ReferencedSymbols()
    {
        var found = new HashSet<string>();
        var regex = new Regex(@"\bShop\.([A-Za-z_][A-Za-z0-9_]*)");

        string scriptRoot = Path.Combine(Application.dataPath, "Scripts");

        foreach (string path in Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            if (name == "Shop.cs" || name == "ShopTools.cs") continue;

            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

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
            keys.Add(m.Groups[1].Value);

        return keys;
    }

    /// <summary>Alle Ids aus dem Unlock-Katalog.</summary>
    private static HashSet<string> KnownUnlockIds()
    {
        var ids = new HashSet<string>();
        foreach (UnlockDef def in Unlocks.All) ids.Add(def.Id);
        return ids;
    }

    // ==================================================================
    //  Übersicht
    // ==================================================================

    [MenuItem("Tools/Shop/Übersicht ausgeben", false, 203)]
    public static void PrintOverview()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Shop.All.Count} Shop-Einträge (Reihenfolge = Reihenfolge in Shop.cs)\n");
        sb.AppendLine("Id\tKategorie\tStufen\tPreise\tWerte\tUnlock\tWaffe");

        foreach (ShopItemDef def in Shop.All)
        {
            sb.AppendLine($"{def.Id}\t{def.Category}\t{def.MaxLevel}\t" +
                          $"{string.Join(", ", def.Costs)}\t{string.Join(", ", def.Values)}\t" +
                          $"{def.RequiredUnlock}\t{def.UnlocksWeapon}");
        }

        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "shop_overview.tsv"));
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"[Shop] Übersicht geschrieben nach:\n{outPath}\n\n{sb}");
        EditorUtility.RevealInFinder(outPath);
    }

    [MenuItem("Tools/Shop/Fehlende Grafiken auflisten", false, 204)]
    public static void ListMissingIcons()
    {
        var missing = new List<string>();

        foreach (ShopItemDef def in Shop.All)
        {
            if (!File.Exists(Path.Combine(IconFolder, def.IconKey + ".png")))
                missing.Add($"{IconFolder}/{def.IconKey}.png   (für '{def.Id}')");
        }

        if (missing.Count == 0)
        {
            Debug.Log("[Shop] Jeder Eintrag hat eine Grafik.");
            return;
        }

        Debug.Log($"[Shop] {missing.Count} Grafik(en) fehlen:\n" + string.Join("\n", missing));
    }

    // ==================================================================
    //  Spielstand
    // ==================================================================

    [MenuItem("Tools/Spielstand/Shop anzeigen", false, 312)]
    public static void ShowSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "save.json");

        if (!File.Exists(path))
        {
            Debug.Log($"[Shop] Noch keine Speicherdatei unter {path}.");
            return;
        }

        Debug.Log($"[Shop] {path}\n\n{File.ReadAllText(path)}");
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/Spielstand/Shop zurücksetzen", false, 322)]
    public static void ResetSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "save.json");

        bool ok = EditorUtility.DisplayDialog(
            "Shop zurücksetzen",
            "Münzen, gekaufte Stufen und die Charakterwahl werden gelöscht.\n\n" + path,
            "Zurücksetzen", "Abbrechen");

        if (!ok) return;

        if (File.Exists(path)) File.Delete(path);
        Debug.Log("[Shop] Spielstand gelöscht. Wirkt ab dem nächsten Start des Spielmodus.");
    }
}
