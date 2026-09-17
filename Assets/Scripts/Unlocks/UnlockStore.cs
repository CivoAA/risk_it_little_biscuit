using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Der Spielstand der Unlocks: eine Liste von Ids, die offen sind. Namen, Texte
/// und Symbole kommen aus <see cref="Unlocks"/>.
///
/// Wie überall: unbekannte Ids bleiben unangetastet in der Datei liegen, damit
/// ein entfernter und später zurückgeholter Unlock seinen Zustand behält.
/// </summary>
public class UnlockStore
{
    public const int CurrentVersion = 2;
    private const string FileName = "unlocks.json";

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<string> unlocked = new List<string>();
    }

    // ---- Altes Format (v1): { "unlocks": [ { "id": "...", "isUnlocked": true } ] }
    [Serializable]
    private class LegacyEntry
    {
        public string id;
        public bool isUnlocked;
    }

    [Serializable]
    private class LegacyFile
    {
        public List<LegacyEntry> unlocks;
    }

    private readonly string savePath;
    private readonly HashSet<string> unlocked = new HashSet<string>();
    private readonly List<string> order = new List<string>();

    public bool ReadOnly { get; set; }

    public UnlockStore(string directory = null)
    {
        savePath = Path.Combine(directory ?? Application.persistentDataPath, FileName);
    }

    public string SavePath => savePath;

    public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && unlocked.Contains(id);

    public void SetUnlocked(string id, bool value)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (value)
        {
            if (unlocked.Add(id)) order.Add(id);
        }
        else if (unlocked.Remove(id))
        {
            order.Remove(id);
        }
    }

    public void ResetAll()
    {
        unlocked.Clear();
        order.Clear();
        Save();
    }

    // ==================================================================

    public void Load()
    {
        unlocked.Clear();
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
            Debug.LogWarning($"[Unlocks] {FileName} nicht lesbar: {e.Message}");
        }

        if (string.IsNullOrWhiteSpace(json)) return;

        if (TryReadCurrent(json)) return;
        if (TryReadLegacy(json)) return;

        Backup(".corrupt");
        Debug.LogWarning($"[Unlocks] {FileName} war unlesbar und wurde als .corrupt gesichert.");
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
            Debug.LogWarning($"[Unlocks] Spielstand nicht lesbar: {e.Message}");
            return false;
        }

        if (file?.unlocked == null) return false;

        foreach (string id in file.unlocked) SetUnlocked(id, true);
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

        if (file?.unlocks == null) return false;

        foreach (LegacyEntry e in file.unlocks)
        {
            if (e != null && e.isUnlocked) SetUnlocked(e.id, true);
        }

        Backup(".v1.bak");
        Save();
        Debug.Log($"[Unlocks] Alten Spielstand übernommen ({order.Count} offen), " +
                  $"Sicherung als {FileName}.v1.bak.");
        return true;
    }

    public void Save()
    {
        if (ReadOnly) return;

        SaveFile file = new SaveFile { version = CurrentVersion, unlocked = order };

        try
        {
            string json = JsonUtility.ToJson(file, true);
            string tmp = savePath + ".tmp";

            File.WriteAllText(tmp, json);
            File.Copy(tmp, savePath, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Unlocks] Speichern fehlgeschlagen: {e.Message}");
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
