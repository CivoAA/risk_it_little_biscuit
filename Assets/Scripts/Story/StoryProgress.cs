using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Wie weit die Story ist. Gespeichert in story.json im persistentDataPath,
/// sofort bei jeder Aenderung (passiert nur ein paar Mal im ganzen Spiel).
///
/// Die Welten heissen hier wie ihr Wellenplan (LevelEntry.planId bzw.
/// SpawnDirector.PlanId): World2 Wald, World1 Kueche, World5 Eis, World6
/// Geisterwald, World4 Vulkan - in dieser festen Reihenfolge (<see cref="Route"/>).
/// Die vier ersten bringen je einen Glutsplitter, jeder Splitter oeffnet die
/// naechste Welt, der Vulkan ist das Finale.
///
/// Zurueckgesetzt ueber <see cref="SaveReset"/> (Optionen) oder die
/// Konsole ("story reset").
/// </summary>
public static class StoryProgress
{
    private const string FileName = "story.json";

    /// <summary>Unlock-Id, an der der Vulkan in der Levelauswahl haengt (hub.unity).</summary>
    public const string FinaleUnlockId = "Level5_Story";

    /// <summary>Die vier Welten mit Splitter, in der Reihenfolge der Reiseroute.</summary>
    public static readonly string[] ShardWorlds = { "World2", "World1", "World5", "World6" };

    /// <summary>Die letzte Welt.</summary>
    public const string FinaleWorld = "World4";

    /// <summary>Die feste Reihenfolge: Wald, Kueche, Eis, Geisterwald, Vulkan.</summary>
    public static readonly string[] Route = { "World2", "World1", "World5", "World6", FinaleWorld };

    [Serializable]
    private class SaveFile
    {
        public int version = 1;
        public bool introSeen;
        public List<string> briefed = new List<string>();
        public List<string> shards = new List<string>();
        public List<string> debriefed = new List<string>();
        public bool allShardsSeen;
        public bool finaleDone;
        public bool epilogueSeen;
    }

    private static SaveFile data;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Schreibt nichts auf die Platte (Test-Szene, Tests).</summary>
    public static bool SandboxMode { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        data = null;
        SandboxMode = false;
    }

    private static SaveFile Data
    {
        get
        {
            if (data != null) return data;
            data = new SaveFile();
            try
            {
                if (File.Exists(SavePath))
                    data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath)) ?? new SaveFile();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Story] story.json nicht lesbar, Story beginnt neu: " + e.Message);
                data = new SaveFile();
            }
            return data;
        }
    }

    private static void Save()
    {
        if (SandboxMode) return;
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Story] story.json nicht schreibbar: " + e.Message);
        }
    }

    // ==================================================================
    //  Abfragen
    // ==================================================================

    public static bool IntroSeen => Data.introSeen;
    public static bool FinaleDone => Data.finaleDone;
    public static bool EpilogueSeen => Data.epilogueSeen;
    public static bool AllShardsSeen => Data.allShardsSeen;

    public static int ShardCount
    {
        get
        {
            int n = 0;
            foreach (string w in ShardWorlds) if (Data.shards.Contains(w)) n++;
            return n;
        }
    }

    public static bool HasAllShards => ShardCount >= ShardWorlds.Length;

    public static bool IsStoryWorld(string plan) => IsShardWorld(plan) || Same(plan, FinaleWorld);

    public static bool IsShardWorld(string plan)
    {
        foreach (string w in ShardWorlds) if (Same(w, plan)) return true;
        return false;
    }

    public static bool HasShard(string plan) => Contains(Data.shards, plan);
    public static bool WasBriefed(string plan) => Contains(Data.briefed, plan);
    public static bool WasDebriefed(string plan) => Contains(Data.debriefed, plan);

    /// <summary>Hat einen Splitter, aber Pomo hat im Hub noch nichts dazu gesagt.</summary>
    public static string PendingDebrief
    {
        get
        {
            foreach (string w in ShardWorlds)
                if (Contains(Data.shards, w) && !Contains(Data.debriefed, w)) return w;
            return null;
        }
    }

    /// <summary>Darf der Vulkan im Story-Modus gespielt werden?</summary>
    public static bool FinaleOpen => HasAllShards;

    /// <summary>
    /// Ist diese Welt auf der Route schon erreicht? Die erste immer, jede
    /// weitere, sobald die davor ihren Splitter abgegeben hat (oder sie ihren
    /// eigenen schon hat - alte Spielstaende aus der freien Reihenfolge). Welten,
    /// die nicht auf der Route stehen, sind immer offen.
    /// </summary>
    public static bool IsWorldOpen(string plan)
    {
        int i = RouteIndex(plan);
        if (i <= 0) return true;
        if (Same(Route[i], FinaleWorld)) return HasAllShards;
        return HasShard(Route[i]) || HasShard(Route[i - 1]);
    }

    /// <summary>Platz auf der Route, -1 wenn nicht drauf.</summary>
    public static int RouteIndex(string plan)
    {
        for (int i = 0; i < Route.Length; i++) if (Same(Route[i], plan)) return i;
        return -1;
    }

    // ==================================================================
    //  Eintragen
    // ==================================================================

    public static void MarkIntroSeen() { Data.introSeen = true; Save(); }
    public static void MarkBriefed(string plan) { if (Add(Data.briefed, plan)) Save(); }
    public static void MarkDebriefed(string plan) { if (Add(Data.debriefed, plan)) Save(); }
    public static void MarkAllShardsSeen() { Data.allShardsSeen = true; Save(); }
    public static void MarkFinaleDone() { Data.finaleDone = true; Save(); }
    public static void MarkEpilogueSeen() { Data.epilogueSeen = true; Save(); }

    /// <summary>Splitter eingesammelt. Gibt true zurueck, wenn er neu ist.</summary>
    public static bool AddShard(string plan)
    {
        if (!IsShardWorld(plan) || !Add(Data.shards, Canonical(plan))) return false;
        Save();
        return true;
    }

    /// <summary>Alles auf Anfang - auch die Datei.</summary>
    public static void ResetAll()
    {
        data = new SaveFile();
        if (!SandboxMode && File.Exists(SavePath)) File.Delete(SavePath);
        Debug.Log("[Story] Story zurueckgesetzt.");
    }

    /// <summary>
    /// Nur fuer Konsole/Tests: setzt den Stand so, als waeren die ersten
    /// <paramref name="shards"/> Welten geschafft (mit allen Gespraechen davor).
    /// </summary>
    public static void DebugSetChapter(int shards)
    {
        data = new SaveFile { introSeen = true };
        for (int i = 0; i < Mathf.Clamp(shards, 0, ShardWorlds.Length); i++)
        {
            data.briefed.Add(ShardWorlds[i]);
            data.shards.Add(ShardWorlds[i]);
            data.debriefed.Add(ShardWorlds[i]);
        }
        data.allShardsSeen = shards >= ShardWorlds.Length;
        Save();
    }

    // ------------------------------------------------------------------

    private static bool Same(string a, string b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string plan)
    {
        foreach (string w in ShardWorlds) if (Same(w, plan)) return w;
        return Same(plan, FinaleWorld) ? FinaleWorld : (plan ?? "").Trim();
    }

    private static bool Contains(List<string> list, string plan)
    {
        foreach (string s in list) if (Same(s, plan)) return true;
        return false;
    }

    private static bool Add(List<string> list, string plan)
    {
        if (string.IsNullOrWhiteSpace(plan) || Contains(list, plan)) return false;
        list.Add(Canonical(plan));
        return true;
    }
}
