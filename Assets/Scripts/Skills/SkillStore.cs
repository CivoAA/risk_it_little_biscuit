using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand des Skilltrees: die Skillpunkte und pro Baum die Liste der
/// freigeschalteten Knoten. Sonst nichts - Namen, Preise, Werte und Struktur
/// kommen aus <see cref="SkillTrees"/>.
///
/// Die Punkte liegen bewusst ausserhalb der Bäume: ein gemeinsamer Vorrat für
/// alle Charaktere. Soll das später pro Charakter laufen, wandert das Feld in
/// den Baum-Eintrag - das Format ist schon darauf vorbereitet.
///
/// Dieselben Regeln wie bei Shop und Achievements: unbekannte Schlüssel bleiben
/// in der Datei liegen, fehlende starten gesperrt. Ein Baum, den der Katalog
/// nicht mehr kennt, wird also nicht gelöscht, sondern nur nicht angezeigt.
/// </summary>
public class SkillStore
{
    public const int CurrentVersion = 2;
    private const string FileName = "skills.json";

    [Serializable]
    private class TreeEntry
    {
        public string id;
        public List<string> unlocked = new List<string>();

        /// <summary>Reserviert: Punkte pro Baum, falls die mal getrennt laufen sollen.</summary>
        public int skillCurrency = -1;
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public int skillCurrency;
        public List<TreeEntry> trees = new List<TreeEntry>();
    }

    // ---- Altes Format (v1): eine flache Liste von Zahlen-IDs. Die Zahlen sind
    //      nach der Umstellung auf Text-Schlüssel nicht mehr zuzuordnen (sie waren
    //      teils doppelt vergeben), deshalb wird der Fortschritt nicht übernommen -
    //      nur die Punkte, und die ausgegebenen kommen als Erstattung zurück.
    [Serializable]
    private class LegacyFile
    {
        public List<int> unlockedSkillIDs = new List<int>();
        public int skillCurrency;
    }

    private readonly string savePath;
    private readonly Dictionary<string, TreeEntry> byTree = new Dictionary<string, TreeEntry>();
    private readonly List<TreeEntry> order = new List<TreeEntry>();

    public int SkillCurrency { get; set; }

    private bool dirty;

    /// <summary>Wenn true, wird nichts auf die Platte geschrieben (Test-Szene).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Es gibt ungeschriebene Änderungen.</summary>
    public bool IsDirty => dirty;

    /// <summary>
    /// Merkt eine Änderung vor, ohne zu schreiben. Für heisse Pfade wie die
    /// Skillpunkte, die im Spiel pro Miniboss-Kill anfallen - dort wäre ein
    /// Dateischreibvorgang je Gegner ein Ruckler. Weggeschrieben wird gebündelt
    /// über <see cref="Flush"/> (siehe AchievementRuntime).
    /// </summary>
    public void MarkDirty() => dirty = true;

    /// <summary>Schreibt nur, wenn sich etwas geändert hat.</summary>
    public void Flush()
    {
        if (!dirty) return;
        Save();
    }

    public SkillStore(string directory = null)
    {
        savePath = Path.Combine(directory ?? Application.persistentDataPath, FileName);
    }

    public string SavePath => savePath;

    // ==================================================================
    //  Zugriff
    // ==================================================================

    public bool IsUnlocked(string treeId, string key)
    {
        TreeEntry t = Get(treeId, false);
        return t != null && t.unlocked.Contains(key);
    }

    public void SetUnlocked(string treeId, string key, bool unlocked)
    {
        TreeEntry t = Get(treeId, true);

        if (unlocked)
        {
            if (!t.unlocked.Contains(key)) t.unlocked.Add(key);
        }
        else
        {
            t.unlocked.Remove(key);
        }
    }

    public IReadOnlyList<string> UnlockedIn(string treeId)
    {
        TreeEntry t = Get(treeId, false);
        return t != null ? (IReadOnlyList<string>)t.unlocked : Array.Empty<string>();
    }

    public void ClearTree(string treeId)
    {
        TreeEntry t = Get(treeId, false);
        t?.unlocked.Clear();
    }

    private TreeEntry Get(string treeId, bool create)
    {
        if (string.IsNullOrEmpty(treeId)) return null;
        if (byTree.TryGetValue(treeId, out TreeEntry t)) return t;
        if (!create) return null;

        t = new TreeEntry { id = treeId };
        byTree.Add(treeId, t);
        order.Add(t);
        return t;
    }

    // ==================================================================
    //  Laden
    // ==================================================================

    public void Load()
    {
        byTree.Clear();
        order.Clear();
        SkillCurrency = 0;

        if (!File.Exists(savePath))
        {
            Save();
            return;
        }

        string json = null;
        try
        {
            json = File.ReadAllText(savePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Skills] {FileName} nicht lesbar: {e.Message}");
        }

        if (string.IsNullOrWhiteSpace(json)) return;

        if (TryReadCurrent(json)) return;
        if (TryReadLegacy(json)) return;

        Backup(".corrupt");
        Debug.LogWarning($"[Skills] {FileName} war unlesbar und wurde als .corrupt gesichert.");
        Save();
    }

    private bool TryReadCurrent(string json)
    {
        if (json.IndexOf("\"version\"", StringComparison.Ordinal) < 0) return false;

        SaveFile file;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Skills] Spielstand nicht lesbar: {e.Message}");
            return false;
        }

        if (file == null) return false;

        SkillCurrency = file.skillCurrency;

        if (file.trees == null) return true;

        foreach (TreeEntry t in file.trees)
        {
            if (t == null || string.IsNullOrEmpty(t.id) || byTree.ContainsKey(t.id)) continue;
            if (t.unlocked == null) t.unlocked = new List<string>();
            byTree.Add(t.id, t);
            order.Add(t);
        }

        return true;
    }

    private bool TryReadLegacy(string json)
    {
        LegacyFile file;
        try
        {
            file = JsonUtility.FromJson<LegacyFile>(json);
        }
        catch
        {
            return false;
        }

        if (file == null) return false;

        // Die alten Zahlen-IDs waren teils doppelt vergeben und lassen sich den
        // neuen Schlüsseln nicht sauber zuordnen. Statt falsch zuzuordnen wird
        // alles zurückgesetzt und der Gegenwert erstattet: pro alter Freischaltung
        // der damalige Durchschnittspreis. Passiert genau einmal.
        int spent = (file.unlockedSkillIDs?.Count ?? 0) * LegacyRefundPerSkill;
        SkillCurrency = file.skillCurrency + spent;

        Backup(".v1.bak");
        Save();

        if (spent > 0)
        {
            Debug.Log($"[Skills] Alter Skill-Spielstand konnte nicht übernommen werden " +
                      $"(Zahlen-IDs waren mehrdeutig). {spent} Skillpunkte erstattet, " +
                      $"Sicherung als {FileName}.v1.bak.");
        }

        return true;
    }

    /// <summary>Fast alle alten Knoten kosteten 10; die beiden Crit-Knoten 50.</summary>
    private const int LegacyRefundPerSkill = 10;

    // ==================================================================
    //  Speichern
    // ==================================================================

    public void Save()
    {
        if (ReadOnly)
        {
            dirty = false;
            return;
        }

        SaveFile file = new SaveFile
        {
            version = CurrentVersion,
            skillCurrency = SkillCurrency,
            trees = order,
        };

        try
        {
            string json = JsonUtility.ToJson(file, true);
            string tmp = savePath + ".tmp";

            File.WriteAllText(tmp, json);
            File.Copy(tmp, savePath, true);
            File.Delete(tmp);

            // Erst nach dem erfolgreichen Schreiben abhaken.
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Skills] Speichern fehlgeschlagen: {e.Message}");
        }
    }

    private void Backup(string suffix)
    {
        try
        {
            File.Copy(savePath, savePath + suffix, true);
        }
        catch
        {
            // Sicherung ist nett, aber nichts, wofür das Laden scheitern darf.
        }
    }
}
