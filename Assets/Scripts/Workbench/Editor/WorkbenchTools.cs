using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Werkzeuge rund um die Werkbank: Fenster im Play Mode oeffnen, Icons pruefen
/// und - das Wichtigste - den Katalog gegen das Player-Prefab pruefen.
///
/// Der Katalog in <see cref="WeaponCatalog"/> ist eine Abschrift der
/// Weapon-Bauteile am Player-Prefab. Abschriften laufen auseinander, also
/// gibt es hier den Abgleich: er liest das Prefab, vergleicht Ids, Arten und
/// Rezepte und meldet jede Abweichung samt der Zeile, die im Katalog fehlt.
/// </summary>
public static class WorkbenchTools
{
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string IconFolder = "Assets/Resources/Workbench";

    // ==================================================================
    //  Play Mode
    // ==================================================================

    [MenuItem("Tools/Werkbank/Fenster öffnen (nur im Play Mode)", false, 216)]
    private static void OpenPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Werkbank] Geht nur im Play Mode - das Fenster baut sich zur Laufzeit auf.");
            return;
        }
        WorkbenchPanel.Toggle();
    }

    // ==================================================================
    //  Katalog gegen das Player-Prefab
    // ==================================================================

    private class PrefabEntry
    {
        public string Id;
        public string Name;
        public PoolKind Kind;
    }

    [MenuItem("Tools/Werkbank/Katalog gegen Player-Prefab prüfen", false, 204)]
    private static void CheckCatalog()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (player == null)
        {
            Debug.LogError($"[Werkbank] {PlayerPrefab} nicht gefunden.");
            return;
        }

        Dictionary<string, PrefabEntry> fromPrefab = new Dictionary<string, PrefabEntry>();
        Dictionary<Weapon, string> idOf = new Dictionary<Weapon, string>();

        foreach (Weapon w in player.GetComponentsInChildren<Weapon>(true))
        {
            if (w == null || string.IsNullOrEmpty(w.weaponID)) continue;

            idOf[w] = w.weaponID;
            fromPrefab[w.weaponID] = new PrefabEntry
            {
                Id = w.weaponID,
                Name = w.gameObject.name,
                Kind = w.weaponID.StartsWith("evo_") ? PoolKind.Evo
                     : w.weaponID.StartsWith("buff_") ? PoolKind.Buff
                     : PoolKind.Weapon,
            };
        }

        List<string> problems = new List<string>();

        foreach (PrefabEntry e in fromPrefab.Values)
        {
            WeaponDef def = WeaponCatalog.Find(e.Id);
            if (def == null)
            {
                problems.Add($"FEHLT im Katalog: {e.Id}\n" +
                             $"    Def(\"{e.Id}\", \"{e.Name}\", PoolKind.{e.Kind});");
                continue;
            }

            if (def.Kind != e.Kind)
                problems.Add($"Art weicht ab: {e.Id} - Katalog {def.Kind}, Prefab {e.Kind}");

            if (def.NameEn != e.Name)
                problems.Add($"Name weicht ab: {e.Id} - Katalog \"{def.NameEn}\", Prefab \"{e.Name}\"");
        }

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            if (!fromPrefab.ContainsKey(def.Id))
                problems.Add($"Steht im Katalog, aber nicht mehr im Prefab: {def.Id}");
        }

        CheckRecipes(player, idOf, problems);

        if (problems.Count == 0)
        {
            Debug.Log($"[Werkbank] Katalog stimmt mit dem Player-Prefab ueberein " +
                      $"({fromPrefab.Count} Eintraege, {WeaponCatalog.Evos.Count} Rezepte).");
            return;
        }

        Debug.LogWarning($"[Werkbank] {problems.Count} Abweichung(en):\n  " +
                         string.Join("\n  ", problems));
    }

    private static void CheckRecipes(GameObject player, Dictionary<Weapon, string> idOf,
                                     List<string> problems)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null || pc.EvoCombinations == null)
        {
            problems.Add("Kein PlayerController mit EvoCombinations am Prefab.");
            return;
        }

        HashSet<string> seen = new HashSet<string>();

        foreach (EvoRecipe r in pc.EvoCombinations)
        {
            if (r == null || r.EvoWeapon == null) continue;

            string evo = Id(idOf, r.EvoWeapon);
            string a = Id(idOf, r.RequiredWeapon1);
            string b = Id(idOf, r.RequiredWeapon2);
            seen.Add(evo);

            EvoDef def = WeaponCatalog.Evos.FirstOrDefault(e => e.Id == evo);
            if (def == null)
            {
                problems.Add($"Rezept fehlt im Katalog: {evo}\n" +
                             $"    Evo(\"{evo}\", \"{a}\", \"{b}\");");
                continue;
            }

            bool same = (def.IngredientA == a && def.IngredientB == b)
                     || (def.IngredientA == b && def.IngredientB == a);

            if (!same)
            {
                problems.Add($"Rezept weicht ab: {evo} - Katalog {def.IngredientA} + " +
                             $"{def.IngredientB}, Prefab {a} + {b}");
            }
        }

        foreach (EvoDef def in WeaponCatalog.Evos)
        {
            if (!seen.Contains(def.Id))
                problems.Add($"Rezept steht im Katalog, aber nicht mehr im Prefab: {def.Id}");
        }
    }

    private static string Id(Dictionary<Weapon, string> idOf, Weapon w)
    {
        if (w == null) return "?";
        return idOf.TryGetValue(w, out string id) ? id : (w.weaponID ?? "?");
    }

    // ==================================================================
    //  Icons
    // ==================================================================

    [MenuItem("Tools/Werkbank/Icons prüfen", false, 205)]
    private static void CheckIcons()
    {
        List<string> missing = new List<string>();

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            foreach (int size in new[] { 14, 10 })
            {
                string path = $"{IconFolder}/{def.Id}_{size}.png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                    missing.Add(path);
            }
        }

        if (missing.Count == 0)
        {
            Debug.Log($"[Werkbank] Alle {WeaponCatalog.All.Count * 2} Icons liegen unter {IconFolder}.");
            return;
        }

        Debug.LogWarning($"[Werkbank] {missing.Count} Icon(s) fehlen:\n  " +
                         string.Join("\n  ", missing) +
                         "\n  Erzeugt werden sie aus dem weaponIcon am Player-Prefab, " +
                         "siehe WORKBENCH_UI.md, Abschnitt Icons.");
    }

    // ==================================================================
    //  Spielstand
    // ==================================================================

    [MenuItem("Tools/Spielstand/Werkbank-Verteiler zurücksetzen", false, 325)]
    private static void ResetLoadout()
    {
        Loadout.ResetAll();
        WorkbenchPanel.RefreshIfOpen();
        Debug.Log("[Werkbank] Verteiler aller Charaktere geleert und abgeschaltet.");
    }

    [MenuItem("Tools/Spielstand/Werkbank-Datei im Explorer zeigen", false, 313)]
    private static void ShowSave()
    {
        string path = Path.Combine(Application.persistentDataPath, "loadout.json");
        if (File.Exists(path)) EditorUtility.RevealInFinder(path);
        else Debug.Log($"[Werkbank] Noch kein Spielstand - erwartet unter {path}.");
    }
}
