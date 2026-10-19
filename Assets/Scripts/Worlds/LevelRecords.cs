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
///   - Endless-Punkte: bester Score (<see cref="EndlessScore"/>) - den zeigt
///              die Levelauswahl, die Zeit bleibt nur gespeichert.
///   - Sterne:  hoechste Schwierigkeit, auf der die Welt besiegt wurde
///              (Leicht 1, Normal 2, Schwer 3). Ein Sieg auf Schwer bringt
///              gleich alle drei.
///
/// Eingetragen wird am Laufende in <see cref="GameManager.GameOver"/>. Die Karte
/// ist die Map-ID aus der Levelauswahl (MapsManager.selectedMap), sonst die der
/// geladenen <see cref="MapDefinition"/>.
///
/// Gespeichert in level_records.json im Ordner des aktiven Profils, sofort beim
/// Eintragen - das passiert nur einmal je Lauf.
/// </summary>
public static class LevelRecords
{
    private const string FileName = "level_records.json";

    /// <summary>2 = mit Sternen. Siege aus Version 1 waren alle auf Normal.</summary>
    private const int CurrentVersion = 2;

    [Serializable]
    private class Entry
    {
        public int mapId;
        public float storyBest;      // 0 = noch nie gewonnen
        public float endlessBest;    // 0 = noch nie gespielt
        public int stars;            // 0 = noch nie gewonnen, sonst 1-3
        public int endlessScore;     // 0 = noch kein Endless-Score (EndlessScore)
    }

    [Serializable]
    private class SaveFile
    {
        public int version = CurrentVersion;
        public List<Entry> entries = new List<Entry>();
    }

    private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private static bool loaded;

    public static string SavePath => Path.Combine(SaveProfiles.Directory, FileName);

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

    /// <summary>Hoechster Endless-Punktestand (<see cref="EndlessScore"/>), 0 = keiner.</summary>
    public static int EndlessScoreBest(int mapId)
    {
        EnsureLoaded();
        return entries.TryGetValue(mapId, out Entry e) ? e.endlessScore : 0;
    }

    /// <summary>Endless-Punktestand melden - zaehlt nur, wenn er besser ist. Auch ohne Steam und mit Cheats (bleibt lokal).</summary>
    public static void ReportScore(int mapId, int score)
    {
        if (mapId < 0 || score <= 0) return;
        EnsureLoaded();
        Entry e = GetOrAdd(mapId);
        if (score <= e.endlessScore) return;
        e.endlessScore = score;
        Save();
    }

    /// <summary>Sterne der Karte (0-3): hoechste Schwierigkeit, auf der sie besiegt wurde.</summary>
    public static int Stars(int mapId)
    {
        EnsureLoaded();
        return entries.TryGetValue(mapId, out Entry e) ? Mathf.Clamp(e.stars, 0, 3) : 0;
    }

    /// <summary>
    /// Story-Sieg auf <paramref name="level"/> melden. Sterne gehen nie verloren -
    /// ein leichterer Sieg nach einem schweren aendert nichts.
    /// </summary>
    public static void ReportWin(int mapId, Difficulty.Level level)
    {
        if (mapId < 0) return;
        EnsureLoaded();
        Entry e = GetOrAdd(mapId);
        int stars = Difficulty.Stars(level);
        if (stars <= e.stars) return;
        e.stars = stars;
        Save();
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
        Entry e = GetOrAdd(mapId);

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

    /// <summary>Liest level_records.json neu ein (Profilwechsel, <see cref="SaveProfiles"/>).</summary>
    public static void Reload()
    {
        entries.Clear();
        loaded = false;
    }

    // Ohne Domain-Reload blieben sonst die Zahlen des letzten Profils stehen.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState() => Reload();

    // ==================================================================

    private static Entry GetOrAdd(int mapId)
    {
        if (!entries.TryGetValue(mapId, out Entry e))
        {
            e = new Entry { mapId = mapId };
            entries[mapId] = e;
        }
        return e;
    }

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
            {
                if (e == null) continue;
                // Vor den Sternen gab es keine Schwierigkeit - jeder Sieg war Normal.
                if (file.version < 2 && e.storyBest > 0f && e.stars <= 0) e.stars = 2;
                entries[e.mapId] = e;
            }
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
