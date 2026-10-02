using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand des Skilltrees: pro Baum (also pro Charakter) die gesammelten
/// Charakter-XP und die Liste der freigeschalteten Knoten. Sonst nichts - Namen,
/// Preise, Werte und Struktur kommen aus <see cref="SkillTrees"/>.
///
/// Skillpunkte werden nicht gespeichert, sondern gerechnet: Charakter-Level
/// (aus den XP, siehe <see cref="CharLevel"/>, plus eins je erstmals besiegtem
/// Boss) minus die Preise der offenen Knoten. Siehe <see cref="Skills.Points"/>.
///
/// Dieselben Regeln wie bei Shop und Achievements: unbekannte Schlüssel bleiben
/// in der Datei liegen, fehlende starten gesperrt. Ein Baum, den der Katalog
/// nicht mehr kennt, wird also nicht gelöscht, sondern nur nicht angezeigt.
/// </summary>
public class SkillStore
{
    /// <summary>
    /// v1: flache Zahlen-IDs. v2: Text-Schlüssel, Knoten mit Seelen gekauft.
    /// v3: Charakter-XP pro Baum, Knoten mit Skillpunkten aus dem Level.
    /// </summary>
    public const int CurrentVersion = 3;
    private const string FileName = "skills.json";

    [Serializable]
    private class TreeEntry
    {
        public string id;
        public List<string> unlocked = new List<string>();

        /// <summary>Gesammelte Charakter-XP dieses Charakters.</summary>
        public double xp;

        /// <summary>
        /// Bosse, die dieser Charakter schon einmal besiegt hat (EnemyId-Name).
        /// Jeder davon ist ein geschenktes Charakter-Level.
        /// </summary>
        public List<string> bosses = new List<string>();
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<TreeEntry> trees = new List<TreeEntry>();
    }

    private readonly string savePath;
    private readonly Dictionary<string, TreeEntry> byTree = new Dictionary<string, TreeEntry>();
    private readonly List<TreeEntry> order = new List<TreeEntry>();

    private bool dirty;

    /// <summary>Wenn true, wird nichts auf die Platte geschrieben (Test-Szene).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Es gibt ungeschriebene Änderungen.</summary>
    public bool IsDirty => dirty;

    /// <summary>
    /// Merkt eine Änderung vor, ohne zu schreiben. Für heisse Pfade wie die
    /// Charakter-XP, die im Spiel bei jedem XP-Kristall anfallen - dort wäre ein
    /// Dateischreibvorgang je Aufsammeln ein Ruckler. Weggeschrieben wird
    /// gebündelt über <see cref="Flush"/> (siehe AchievementRuntime).
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
        if (t == null) return;

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

    public double XpOf(string treeId)
    {
        TreeEntry t = Get(treeId, false);
        return t != null ? t.xp : 0;
    }

    public void SetXp(string treeId, double xp)
    {
        TreeEntry t = Get(treeId, true);
        if (t != null) t.xp = Math.Max(0, xp);
    }

    public int BossCountOf(string treeId)
    {
        TreeEntry t = Get(treeId, false);
        return t != null ? t.bosses.Count : 0;
    }

    public bool HasBoss(string treeId, string bossKey)
    {
        TreeEntry t = Get(treeId, false);
        return t != null && t.bosses.Contains(bossKey);
    }

    /// <summary>Merkt einen Boss als besiegt. False, wenn er schon drinstand.</summary>
    public bool AddBoss(string treeId, string bossKey)
    {
        if (string.IsNullOrEmpty(bossKey)) return false;

        TreeEntry t = Get(treeId, true);
        if (t == null || t.bosses.Contains(bossKey)) return false;

        t.bosses.Add(bossKey);
        return true;
    }

    public void ClearBosses(string treeId)
    {
        TreeEntry t = Get(treeId, false);
        t?.bosses.Clear();
    }

    /// <summary>Freischaltungen, XP und Boss-Siege aller Bäume weg - für den harten Reset.</summary>
    public void ClearAll()
    {
        foreach (TreeEntry t in order)
        {
            t.unlocked.Clear();
            t.bosses.Clear();
            t.xp = 0;
        }
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

        // v1 (flache Zahlen-IDs) oder kaputt: sichern und leer anfangen. Aus v1
        // gab es nur Seelen zurück - und die gibt es nicht mehr.
        Backup(".old");
        Debug.LogWarning($"[Skills] {FileName} war nicht im aktuellen Format und wurde als .old gesichert.");
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

        // Bis v2 wurden Knoten mit Seelen gekauft. Die Punkte kommen jetzt aus dem
        // Charakter-Level - alte Käufe würden sonst Punkte belegen, die noch keiner
        // verdient hat. Einmalig alles zu, die alte Datei bleibt als Sicherung.
        bool fromSouls = file.version < 3;
        if (fromSouls) Backup(".v2.bak");

        if (file.trees != null)
        {
            foreach (TreeEntry t in file.trees)
            {
                if (t == null || string.IsNullOrEmpty(t.id) || byTree.ContainsKey(t.id)) continue;
                if (t.unlocked == null || fromSouls) t.unlocked = new List<string>();
                if (t.bosses == null) t.bosses = new List<string>();
                if (fromSouls) t.xp = 0;
                byTree.Add(t.id, t);
                order.Add(t);
            }
        }

        if (fromSouls)
        {
            Save();
            Debug.Log($"[Skills] Alter Seelen-Spielstand: Skilltrees zurückgesetzt, " +
                      $"Sicherung als {FileName}.v2.bak.");
        }

        return true;
    }

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
