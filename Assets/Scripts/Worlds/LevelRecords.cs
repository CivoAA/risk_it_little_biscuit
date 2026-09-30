using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Bestwerte je Karte, fuer den Steckbrief in der Levelauswahl
/// (<see cref="HubLevelSelectUI"/>):
///
///   - Story:   schnellster Sieg (Lauf endet nach dem Boss) - kleiner ist besser.
///   - Endless: laengste Zeit, die man ueberlebt hat - groesser ist besser.
///
/// Eingetragen wird am Laufende in <see cref="GameManager.GameOver"/>. Die Karte
/// ist die Map-ID aus der Levelauswahl (MapsManager.selectedMap), sonst die der
/// geladenen <see cref="MapDefinition"/>.
///
/// Gespeichert in level_records.json im persistentDataPath, sofort beim
/// Eintragen - das passiert nur einmal je Lauf.
/// </summary>
public static class LevelRecords
{
    private const string FileName = "level_records.json";

    [Serializable]
    private class Entry
    {
        public int mapId;
        public float storyBest;      // 0 = noch nie gewonnen
        public float endlessBest;    // 0 = noch nie gespielt
    }

    [Serializable]
    private class SaveFile
    {
        public int version = 1;
        public List<Entry> entries = new List<Entry>();
    }

    private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private static bool loaded;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Schnellster Story-Sieg in Sekunden, 0 = keiner.</summary>
    public static float StoryBest(int mapId)
    {
        EnsureLoaded();
        return entries.TryGetValue(mapId, out Entry e) ? e.storyBest : 0f;
    }

    /// <summary>Laengster Endless-Lauf in Sekunden, 0 = keiner.</summary>
    public static float EndlessBest(int mapId)
    {
        EnsureLoaded();
        return entries.TryGetValue(mapId, out Entry e) ? e.endlessBest : 0f;
    }

    /// <summary>
    /// Laufende melden. Story zaehlt nur als Sieg; Endless zaehlt immer, dort
    /// endet jeder Lauf mit dem Tod.
    /// </summary>
    public static void ReportRun(int mapId, bool endless, bool won, float seconds)
    {
        if (mapId < 0 || seconds <= 0f) return;
        if (!endless && !won) return;

        EnsureLoaded();
        if (!entries.TryGetValue(mapId, out Entry e))
        {
            e = new Entry { mapId = mapId };
            entries[mapId] = e;
        }

        bool better = endless
            ? seconds > e.endlessBest
            : e.storyBest <= 0f || seconds < e.storyBest;
        if (!better) return;

        if (endless) e.endlessBest = seconds;
        else e.storyBest = seconds;
        Save();
    }

    /// <summary>Die Karte des laufenden Laufs, -1 wenn unbekannt.</summary>
    public static int CurrentMapId()
    {
        if (MapsManager.Instance != null) return MapsManager.Instance.selectedMap;
        if (MapDefinition.Active != null) return MapDefinition.Active.LegacyMapId;
        return -1;
    }

    /// <summary>"12:34" bzw. "1:02:03" ab einer Stunde.</summary>
    public static string Format(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        int h = s / 3600, m = (s / 60) % 60, sec = s % 60;
        return h > 0 ? $"{h}:{m:00}:{sec:00}" : $"{m:00}:{sec:00}";
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        entries.Clear();
        Save();
    }

    // ==================================================================

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        if (!File.Exists(SavePath)) return;

        try
        {
            SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));
            if (file?.entries == null) return;
            foreach (Entry e in file.entries)
                if (e != null) entries[e.mapId] = e;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelRecords] {FileName} nicht lesbar: {e.Message}");
        }
    }

    private static void Save()
    {
        var file = new SaveFile();
        file.entries.AddRange(entries.Values);

        try
        {
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
            File.Copy(tmp, SavePath, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelRecords] Speichern fehlgeschlagen: {e.Message}");
        }
    }
}
