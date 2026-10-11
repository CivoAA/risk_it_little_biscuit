using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// DAS BESTIARIUM: Kills je Gegnerart, dauerhaft ueber alle Laeufe und
/// Charaktere gezaehlt. Je <see cref="KillsPerPercent"/> Kills einer Art gibt es
/// +1 % Schaden gegen genau diese Art.
///
/// Freigeschaltet wird es im Skilltree (Schalter <see cref="SkillGrants.Bestiarium"/>,
/// Knoten "wissen_bestiarium" in jedem Baum):
///   - Gezaehlt wird nur, wenn der gerade gespielte Charakter den Knoten hat.
///   - Den Schaden gibt es ebenfalls nur mit diesem Charakter.
///   - Den Reiter im Erfolge-/Unlocks-Buch (<see cref="AchievementsBookPanel"/>)
///     gibt es, sobald irgendein Charakter den Knoten hat.
///
/// Welche Gegner aufgelistet werden, steht nicht hier: es sind alle, die in den
/// Wellenplaenen der Welten (<see cref="Worlds"/>) vorkommen, gruppiert nach der
/// Welt, in der sie zuerst auftauchen. Aendert sich ein Plan, aendert sich die
/// Liste mit. Bosse gelten als bekannt, sobald irgendein Charakter sie besiegt
/// hat - auch ohne den Knoten (wie in der Levelauswahl).
///
/// Gespeichert wird gebuendelt (bestiary.json), im selben Takt wie Achievements
/// und Skillpunkte - siehe <see cref="AchievementRuntime"/>.
/// </summary>
public static class Bestiary
{
    /// <summary>So viele Kills einer Art braucht es fuer +1 % Schaden.</summary>
    public const int KillsPerPercent = 1000;

    /// <summary>
    /// Die Welten im Bestiarium, in der Reihenfolge der Levelauswahl: ihre
    /// Wellenplaene und die Nummer fuer den Namen (level.[n].name). Neue Welt =
    /// neue Zeile.
    /// </summary>
    private static readonly (string[] Plans, int Level, string Fallback)[] Worlds =
    {
        (new[] { "World1" },               1, "Küche"),
        (new[] { "World2", "World2Demo" }, 2, "Wald"),
        (new[] { "World5" },               3, "Eisgletscher"),
        (new[] { "World6" },               4, "Geisterwald"),
    };

    private const string FileName = "bestiary.json";

    [Serializable]
    private class Entry
    {
        public string id;
        public int kills;
    }

    [Serializable]
    private class SaveFile
    {
        public int version = 1;
        public List<Entry> entries = new List<Entry>();
    }

    // Nach Namen, nicht nach Zahl: ein spaeter eingeschobener EnemyId-Wert
    // verschiebt so keine Zaehler. Unbekannte Namen bleiben erhalten.
    private static readonly Dictionary<string, int> kills = new Dictionary<string, int>();

    /// <summary>Alte EnemyId-Namen -> neue. Beim Laden umgeschrieben, damit Zaehler bleiben.</summary>
    private static readonly Dictionary<string, string> RenamedIds = new Dictionary<string, string>
    {
        { "MinibossFliegenpliz", nameof(EnemyId.EliteFliegenpilz) },
        { "MiniBossMarshmello",  nameof(EnemyId.EliteFluegdolch) },
    };
    private static bool loaded;
    private static bool dirty;
    private static List<EnemyId> enemies;
    private static readonly Dictionary<EnemyId, int> worldOf = new Dictionary<EnemyId, int>();

    public static event Action Changed;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    // ==================================================================
    //  Abfragen
    // ==================================================================

    /// <summary>Die Gegner im Bestiarium: Welt fuer Welt, darin in der Reihenfolge ihres ersten Auftritts.</summary>
    public static IReadOnlyList<EnemyId> Enemies
    {
        get
        {
            if (enemies == null) CollectEnemies();
            return enemies;
        }
    }

    /// <summary>Name der Welt, in der dieser Gegner zuerst auftaucht. Leer, wenn er in keinem Plan steht.</summary>
    public static string WorldOf(EnemyId id)
    {
        if (enemies == null) CollectEnemies();
        if (!worldOf.TryGetValue(id, out int w)) return "";
        return Loc.Get($"level.{Worlds[w].Level}.name", Worlds[w].Fallback);
    }

    /// <summary>
    /// Schon einmal besiegt? Ueber die Kills - Bosse zusaetzlich ueber die
    /// Boss-Siege der Charaktere, die auch ohne Knoten gezaehlt werden.
    /// </summary>
    public static bool IsKnown(EnemyId id)
    {
        if (Kills(id) > 0) return true;
        EnemyDef def = EnemyCatalog.Get(id);
        return def != null && EnemyCatalog.IsBossRole(def.Role) && Skills.HasAnyoneBeatenBoss(id.ToString());
    }

    public static int Kills(EnemyId id)
    {
        EnsureLoaded();
        return kills.TryGetValue(id.ToString(), out int n) ? n : 0;
    }

    /// <summary>Schadensbonus in Prozent, den die Kills ergeben - unabhaengig von der Freischaltung.</summary>
    public static int BonusPercent(EnemyId id) => Kills(id) / KillsPerPercent;

    /// <summary>
    /// Faktor auf den Schaden gegen diese Art (1 = kein Bonus). Nur mit dem
    /// Knoten im Baum des gerade gespielten Charakters.
    /// </summary>
    public static float DamageFactor(EnemyId id)
    {
        if (id == EnemyId.None || !Skills.HasGrant(SkillGrants.Bestiarium)) return 1f;
        return 1f + BonusPercent(id) / 100f;
    }

    /// <summary>Hat irgendein Charakter den Knoten? Dann gibt es den Tab.</summary>
    public static bool IsVisible => Skills.HasGrantInAnyTree(SkillGrants.Bestiarium);

    /// <summary>Anzeigename. Uebersetzbar ueber enemy.[Id].name, sonst der Name aus dem Katalog.</summary>
    public static string NameOf(EnemyId id)
    {
        EnemyDef def = EnemyCatalog.Get(id);
        return Loc.Get($"enemy.{id}.name", def != null ? def.Name : id.ToString());
    }

    private static readonly Dictionary<EnemyId, Sprite> icons = new Dictionary<EnemyId, Sprite>();

    /// <summary>
    /// Bild fuer die Kachel: Assets/Resources/Bestiary/[EnemyId].png (bei einem
    /// Sprite-Sheet das erste Bild). Null, wenn es keins gibt - dann zeigt die
    /// Kachel den Anfangsbuchstaben.
    /// </summary>
    public static Sprite Icon(EnemyId id)
    {
        if (icons.TryGetValue(id, out Sprite cached)) return cached;

        string path = "Bestiary/" + id;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Sprite[] all = Resources.LoadAll<Sprite>(path);
            if (all != null && all.Length > 0) sprite = all[0];
        }

        icons[id] = sprite;
        return sprite;
    }

    // ==================================================================
    //  Zaehlen
    // ==================================================================

    public static void AddKill(EnemyId id)
    {
        // Gezaehlt wird nur mit einem Charakter, der den Knoten hat.
        if (id == EnemyId.None || !Skills.HasGrant(SkillGrants.Bestiarium)) return;
        EnsureLoaded();

        string key = id.ToString();
        kills.TryGetValue(key, out int n);
        kills[key] = n + 1;
        dirty = true;

        // Nur bei einer neuen Stufe Bescheid geben - sonst liefe das bei jedem Kill.
        if ((n + 1) % KillsPerPercent == 0) Changed?.Invoke();
    }

    public static void Flush()
    {
        if (dirty) Save();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        kills.Clear();
        Save();
        Changed?.Invoke();
    }

    // ==================================================================
    //  Speichern
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
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                string id = RenamedIds.TryGetValue(e.id, out string now) ? now : e.id;
                kills.TryGetValue(id, out int had);
                kills[id] = had + Mathf.Max(0, e.kills);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bestiary] {FileName} nicht lesbar: {e.Message}");
        }
    }

    private static void Save()
    {
        var file = new SaveFile();
        foreach (KeyValuePair<string, int> kv in kills)
        {
            file.entries.Add(new Entry { id = kv.Key, kills = kv.Value });
        }

        try
        {
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
            File.Copy(tmp, SavePath, true);
            File.Delete(tmp);
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bestiary] Speichern fehlgeschlagen: {e.Message}");
        }
    }

    // ==================================================================

    private static void CollectEnemies()
    {
        enemies = new List<EnemyId>();
        worldOf.Clear();

        for (int w = 0; w < Worlds.Length; w++)
        {
            foreach (string planId in Worlds[w].Plans)
                CollectEnemies(WavePlans.For(planId), w);
        }
    }

    private static void CollectEnemies(RunPlan plan, int world)
    {
        if (plan == null) return;
        List<EnemyId> list = enemies;

        void Add(EnemyId id)
        {
            if (id == EnemyId.None || list.Contains(id)) return;

            // Die Kaefig-Wand ist Kulisse, kein Gegner.
            EnemyDef def = EnemyCatalog.Get(id);
            if (def != null && def.Role == EnemyRole.Blocker) return;

            list.Add(id);
            worldOf[id] = world;
        }

        void AddPhase(Phase phase)
        {
            if (phase == null) return;
            foreach (PoolEntry p in phase.Enemies) Add(p.Id);
            foreach (Beat b in phase.Beats)
            {
                Add(b.Enemy);
                Add(b.RingEnemy);
            }
        }

        foreach (Phase phase in plan.Phases) AddPhase(phase);
        AddPhase(plan.Endless);
    }
}
