using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand-Teil des Achievement-Systems: ausschliesslich Id, Zählerstand
/// und Freischalt-Flag. Kein Name, keine Beschreibung, kein Symbol, kein Zielwert -
/// das alles kommt aus <see cref="Ach"/> und wird mit dem Spiel ausgeliefert.
///
/// Die drei Regeln, die alte Spielstände schützen:
///   * Eine Id, die in der Datei steht, aber nicht mehr im Katalog: bleibt
///     unangetastet in der Datei liegen. Kommt das Achievement zurück, ist der
///     Fortschritt noch da.
///   * Eine Id, die im Katalog steht, aber nicht in der Datei: wird mit 0
///     angelegt. Ein neues Achievement kann einen alten Stand nicht kaputtmachen.
///   * Beim Laden wird neu bewertet: Wer den Zielwert schon überschritten hat,
///     bekommt das Achievement sofort - wichtig, wenn ein Ziel gesenkt wird.
/// </summary>
public class AchievementStore
{
    public const int CurrentVersion = 2;
    private const string FileName = "achievements.json";

    [Serializable]
    private class Entry
    {
        public string id;
        public float value;
        public bool unlocked;
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<Entry> entries = new List<Entry>();
    }

    // ---- Altes Format (v1), nur zum Einlesen. Felder, die es nicht mehr gibt
    //      (Name, Beschreibung, Symbol, maxvalue), ignoriert JsonUtility einfach.
    [Serializable]
    private class LegacyEntry
    {
        public string id;
        public bool unlocked;
        public float value;
    }

    [Serializable]
    private class LegacyFile
    {
        public List<LegacyEntry> achievements = new List<LegacyEntry>();
    }

    private readonly string savePath;
    private readonly Dictionary<string, Entry> byId = new Dictionary<string, Entry>();
    private readonly List<Entry> order = new List<Entry>();

    private bool dirty;

    /// <summary>Wenn true, wird nichts auf die Platte geschrieben (Test-Szene).</summary>
    public bool ReadOnly { get; set; }

    public AchievementStore(string directory = null)
    {
        savePath = Path.Combine(directory ?? Application.persistentDataPath, FileName);
    }

    public string SavePath => savePath;

    // ==================================================================
    //  Zugriff
    // ==================================================================

    public bool IsUnlocked(string id) => Get(id, false)?.unlocked ?? false;

    public float GetValue(string id) => Get(id, false)?.value ?? 0f;

    public void SetUnlocked(string id, bool unlocked)
    {
        Entry e = Get(id, true);
        if (e.unlocked == unlocked) return;
        e.unlocked = unlocked;
        dirty = true;
    }

    public void SetValue(string id, float value)
    {
        Entry e = Get(id, true);
        if (Mathf.Approximately(e.value, value)) return;
        e.value = value;
        dirty = true;
    }

    private Entry Get(string id, bool create)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId.TryGetValue(id, out Entry e)) return e;
        if (!create) return null;

        e = new Entry { id = id, value = 0f, unlocked = false };
        byId.Add(id, e);
        order.Add(e);
        dirty = true;
        return e;
    }

    // ==================================================================
    //  Laden
    // ==================================================================

    public void Load()
    {
        byId.Clear();
        order.Clear();
        dirty = false;

        if (File.Exists(savePath))
        {
            string json = null;
            try
            {
                json = File.ReadAllText(savePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Achievements] {FileName} nicht lesbar: {e.Message}");
            }

            if (!string.IsNullOrWhiteSpace(json) && !TryReadCurrent(json) && !TryReadLegacy(json))
            {
                // Weder altes noch neues Format: Datei sichern statt überschreiben.
                Backup(".corrupt");
                Debug.LogWarning($"[Achievements] {FileName} war unlesbar und wurde als .corrupt gesichert.");
            }
        }

        SyncWithCatalog();

        if (dirty) Save();
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
            Debug.LogWarning($"[Achievements] Spielstand nicht lesbar: {e.Message}");
            return false;
        }

        if (file?.entries == null) return false;

        foreach (Entry e in file.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || byId.ContainsKey(e.id)) continue;
            byId.Add(e.id, e);
            order.Add(e);
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
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] Altes Format nicht lesbar: {e.Message}");
            return false;
        }

        if (file?.achievements == null || file.achievements.Count == 0) return false;

        foreach (LegacyEntry old in file.achievements)
        {
            if (old == null || string.IsNullOrEmpty(old.id) || byId.ContainsKey(old.id)) continue;

            Entry e = new Entry { id = old.id, value = old.value, unlocked = old.unlocked };
            byId.Add(e.id, e);
            order.Add(e);
        }

        Backup(".v1.bak");
        dirty = true;
        Debug.Log($"[Achievements] Alten Spielstand übernommen ({order.Count} Einträge), Sicherung als {FileName}.v1.bak.");
        return true;
    }

    /// <summary>
    /// Legt fehlende Einträge an und bewertet den Fortschritt neu. Einträge, die
    /// der Katalog nicht kennt, bleiben bewusst stehen.
    /// </summary>
    private void SyncWithCatalog()
    {
        foreach (AchievementDef def in Ach.All)
        {
            Entry e = Get(def.Id, true);

            // Ziel gesenkt oder Fortschritt aus einer älteren Version: nachziehen.
            if (!e.unlocked && e.value >= def.Goal)
            {
                e.unlocked = true;
                dirty = true;
            }

            float clamped = Mathf.Clamp(e.value, 0f, def.Goal);
            if (!Mathf.Approximately(clamped, e.value))
            {
                e.value = clamped;
                dirty = true;
            }
        }
    }

    // ==================================================================
    //  Speichern
    // ==================================================================

    public bool IsDirty => dirty;

    public void MarkDirty() => dirty = true;

    /// <summary>Schreibt nur, wenn sich etwas geändert hat.</summary>
    public void Flush()
    {
        if (!dirty) return;
        Save();
    }

    public void Save()
    {
        if (ReadOnly)
        {
            dirty = false;
            return;
        }

        SaveFile file = new SaveFile { version = CurrentVersion, entries = order };

        try
        {
            string json = JsonUtility.ToJson(file, true);
            string tmp = savePath + ".tmp";

            // Erst daneben schreiben, dann tauschen: ein Absturz mitten im
            // Schreiben kann so keine halbe Datei hinterlassen.
            File.WriteAllText(tmp, json);
            File.Copy(tmp, savePath, true);
            File.Delete(tmp);

            // Erst jetzt als "geschrieben" abhaken. Stand das vor dem try, war
            // eine gescheiterte Schreiboperation still verloren - der naechste
            // Flush haette nichts mehr zu tun gehabt.
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Achievements] Speichern fehlgeschlagen: {e.Message}");
        }
    }

    public void ResetAll()
    {
        foreach (Entry e in order)
        {
            e.value = 0f;
            e.unlocked = false;
        }

        dirty = true;
        Save();
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
