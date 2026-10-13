using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Kleine Ganzzahl-Werte, die zu einem Profil gehoeren (Schwierigkeit, welche
/// Tutorials schon gezeigt wurden). Liegen als prefs.json im Profilordner statt
/// in den PlayerPrefs - die stehen unter Windows in der Registry und kaemen so
/// nicht mit der Steam Cloud auf einen anderen PC.
///
/// Uebergang: Fehlt die Datei noch, werden die alten profilgebundenen
/// PlayerPrefs (siehe <see cref="SaveProfiles.PrefKey"/>) einmal uebernommen.
/// </summary>
public static class ProfilePrefs
{
    public const string FileName = "prefs.json";

    [Serializable]
    private class Entry
    {
        public string key;
        public int value;
    }

    [Serializable]
    private class SaveFile
    {
        public List<Entry> entries = new List<Entry>();
    }

    private static Dictionary<string, int> values;
    private static string loadedDir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState() => Invalidate();

    /// <summary>Profil gewechselt oder geloescht: beim naechsten Zugriff neu lesen.</summary>
    public static void Invalidate()
    {
        values = null;
        loadedDir = null;
    }

    public static int GetInt(string key, int fallback = 0)
    {
        EnsureLoaded();
        return values.TryGetValue(key, out int v) ? v : fallback;
    }

    public static void SetInt(string key, int value)
    {
        EnsureLoaded();
        if (values.TryGetValue(key, out int old) && old == value) return;
        values[key] = value;
        Save();
    }

    /// <summary>Mehrere Schluessel weg, einmal schreiben.</summary>
    public static void DeleteKeys(IEnumerable<string> keys)
    {
        EnsureLoaded();
        bool changed = false;
        foreach (string k in keys) changed |= values.Remove(k);
        if (changed) Save();
    }

    private static void EnsureLoaded()
    {
        string dir = SaveProfiles.Directory;
        if (values != null && loadedDir == dir) return;

        values = new Dictionary<string, int>();
        loadedDir = dir;
        string path = Path.Combine(dir, FileName);

        if (!File.Exists(path))
        {
            MigrateFromPlayerPrefs();
            return;
        }

        try
        {
            SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            if (file?.entries != null)
                foreach (Entry e in file.entries)
                    if (e != null && !string.IsNullOrEmpty(e.key)) values[e.key] = e.value;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProfilePrefs] {FileName} unlesbar, beginnt leer: {e.Message}");
            try { File.Copy(path, path + ".corrupt", true); } catch { }
        }
    }

    /// <summary>Alte Werte aus den PlayerPrefs des aktiven Profils in die Datei holen.</summary>
    private static void MigrateFromPlayerPrefs()
    {
        int moved = 0;
        foreach (string key in SaveProfiles.ProfilePrefKeys())
        {
            string old = SaveProfiles.PrefKey(key);
            if (!PlayerPrefs.HasKey(old)) continue;
            values[key] = PlayerPrefs.GetInt(old);
            PlayerPrefs.DeleteKey(old);
            moved++;
        }

        if (moved == 0) return;
        Save();
        PlayerPrefs.Save();
        Debug.Log($"[ProfilePrefs] {moved} Werte aus den PlayerPrefs uebernommen.");
    }

    private static void Save()
    {
        var file = new SaveFile();
        foreach (KeyValuePair<string, int> kv in values) file.entries.Add(new Entry { key = kv.Key, value = kv.Value });

        string path = Path.Combine(loadedDir, FileName);
        try
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
            File.Copy(tmp, path, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfilePrefs] {FileName} nicht schreibbar: {e.Message}");
        }
    }
}
