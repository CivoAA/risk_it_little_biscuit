using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Drei Spielstaende ("Profile") nebeneinander. Jedes Profil hat seinen eigenen
/// Ordner unter persistentDataPath (profile1, profile2, profile3); darin liegen
/// dieselben Dateien wie frueher direkt im Hauptordner: save.json, unlocks.json,
/// skills.json, achievements.json, loadout.json, bestiary.json,
/// level_records.json und story.json.
///
/// Jeder Katalog fragt nur <see cref="Directory"/> - welches Profil gerade
/// gilt, weiss allein diese Klasse. Einstellungen (Ton, Anzeige, Sprache)
/// bleiben fuer alle Profile gleich; profilgebundene Werte (Tutorials,
/// Schwierigkeit) liegen in <see cref="ProfilePrefs"/> (prefs.json), damit
/// die Steam Cloud sie mitnimmt.
///
/// Verwaltet werden die Profile im Hauptmenue (<see cref="ProfilePanel"/>):
/// waehlen, umbenennen, loeschen. Alles davon ist waehrend eines Laufs gesperrt
/// (<see cref="Allowed"/>), sonst schriebe der Lauf am Ende in das falsche Profil.
///
/// Uebergang: Liegt beim ersten Start noch ein alter Spielstand im Hauptordner,
/// wandert er nach profile1 - inklusive der Tutorial- und Schwierigkeits-Prefs.
/// </summary>
public static class SaveProfiles
{
    public const int SlotCount = 3;

    /// <summary>Hoechstlaenge eines Profilnamens (passt so auf jede Karte).</summary>
    public const int MaxNameLength = 14;

    private const string MetaFile = "profiles.json";

    /// <summary>Die Spielstand-Dateien eines Profils. Neue Speicherdatei = hier eintragen.</summary>
    public static readonly string[] ProgressFiles =
    {
        "save.json", "unlocks.json", "skills.json", "achievements.json",
        "loadout.json", "bestiary.json", "level_records.json", "story.json",
        ProfilePrefs.FileName,
    };

    /// <summary>Profilgebundene PlayerPrefs ohne festen Namen: alles, was so anfaengt.</summary>
    private static readonly string[] PrefKeysFixed = { Difficulty.PrefsKey };

    [Serializable]
    private class Slot
    {
        public bool used;
        public string name = "";
        public long created;
        public long lastPlayed;
        public double playSeconds;
    }

    [Serializable]
    private class Meta
    {
        public int version = 1;
        public int active;
        public List<Slot> slots = new List<Slot>();
    }

    private static Meta meta;
    private static bool loading;

    /// <summary>Feuert nach Wechsel, Umbenennen, Loeschen.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        meta = null;
        loading = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureLoaded();
        ProfileClock.Ensure();
    }

    // ==================================================================
    //  Abfragen
    // ==================================================================

    /// <summary>Profilwechsel & Co. nur ausserhalb eines Laufs.</summary>
    public static bool Allowed => GameManager.Instance == null;

    public static int Active
    {
        get
        {
            EnsureLoaded();
            return meta.active;
        }
    }

    /// <summary>Ordner des aktiven Profils. Wird bei Bedarf angelegt.</summary>
    public static string Directory => DirectoryOf(Active);

    public static string DirectoryOf(int slot)
    {
        string dir = Path.Combine(Application.persistentDataPath, "profile" + (Mathf.Clamp(slot, 0, SlotCount - 1) + 1));
        try
        {
            System.IO.Directory.CreateDirectory(dir);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Profiles] Ordner {dir} nicht anlegbar: {e.Message}");
        }
        return dir;
    }

    public static bool IsUsed(int slot)
    {
        EnsureLoaded();
        return Valid(slot) && meta.slots[slot].used;
    }

    /// <summary>Anzeigename - eigener Name oder "PROFIL 2".</summary>
    public static string NameOf(int slot)
    {
        EnsureLoaded();
        if (!Valid(slot)) return "";
        string custom = meta.slots[slot].name;
        return string.IsNullOrWhiteSpace(custom) ? DefaultName(slot) : custom;
    }

    public static bool HasCustomName(int slot)
    {
        EnsureLoaded();
        return Valid(slot) && !string.IsNullOrWhiteSpace(meta.slots[slot].name);
    }

    public static string DefaultName(int slot) =>
        string.Format(Loc.Get("ui.profile.default", "Profil {0}"), slot + 1);

    public static double PlaySeconds(int slot)
    {
        EnsureLoaded();
        return Valid(slot) ? meta.slots[slot].playSeconds : 0d;
    }

    /// <summary>Zuletzt gespielt (Ortszeit), null = nie.</summary>
    public static DateTime? LastPlayed(int slot)
    {
        EnsureLoaded();
        if (!Valid(slot) || meta.slots[slot].lastPlayed <= 0) return null;
        try
        {
            return new DateTime(meta.slots[slot].lastPlayed, DateTimeKind.Local);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static int UsedCount
    {
        get
        {
            EnsureLoaded();
            int n = 0;
            foreach (Slot s in meta.slots) if (s.used) n++;
            return n;
        }
    }

    /// <summary>Alter PlayerPrefs-Schluessel des aktiven Profils - nur noch fuer den Uebergang in <see cref="ProfilePrefs"/>.</summary>
    public static string PrefKey(string key) => PrefKeyOf(Active, key);

    private static string PrefKeyOf(int slot, string key) => "profile" + (slot + 1) + "." + key;

    // ==================================================================
    //  Aendern
    // ==================================================================

    /// <summary>
    /// Wechselt auf <paramref name="slot"/> und laedt alle Kataloge neu. Ein
    /// leerer Platz wird dabei angelegt.
    /// </summary>
    public static bool Activate(int slot)
    {
        EnsureLoaded();
        if (!Allowed || !Valid(slot)) return false;

        if (slot == meta.active && meta.slots[slot].used) return true;

        // Alles, was noch im Speicher wartet, gehoert dem alten Profil.
        FlushCatalogs();
        ProfileClock.Commit();

        meta.active = slot;
        Slot s = meta.slots[slot];
        if (!s.used) Claim(s, null);
        s.lastPlayed = DateTime.Now.Ticks;
        SaveMeta();

        ReloadCatalogs();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Legt einen leeren Platz an (mit Namen) und wechselt hin.</summary>
    public static bool Create(int slot, string name)
    {
        EnsureLoaded();
        if (!Allowed || !Valid(slot) || meta.slots[slot].used) return false;

        // Reste eines frueheren Profils (z.B. Loeschen halb gescheitert) weg.
        DeleteFiles(slot);
        Claim(meta.slots[slot], name);
        SaveMeta();
        return Activate(slot);
    }

    public static void Rename(int slot, string name)
    {
        EnsureLoaded();
        if (!Valid(slot) || !meta.slots[slot].used) return;
        meta.slots[slot].name = CleanName(name, slot);
        SaveMeta();
        Changed?.Invoke();
    }

    /// <summary>
    /// Loescht ein Profil samt Ordner. War es das aktive, geht es auf ein
    /// anderes vorhandenes Profil - gibt es keins, beginnt derselbe Platz leer
    /// von vorn (ein aktives Profil gibt es immer).
    /// </summary>
    public static bool Delete(int slot)
    {
        EnsureLoaded();
        if (!Allowed || !Valid(slot) || !meta.slots[slot].used) return false;

        bool wasActive = slot == meta.active;
        if (wasActive)
        {
            // Nichts mehr aus dem Speicher in den Ordner zuruecklaufen lassen.
            ProfileClock.Discard();
        }

        DeleteFiles(slot);
        meta.slots[slot] = new Slot();

        if (wasActive)
        {
            int next = -1;
            for (int i = 0; i < SlotCount; i++)
                if (meta.slots[i].used) { next = i; break; }

            if (next < 0)
            {
                next = slot;
                Claim(meta.slots[slot], null);
            }

            meta.active = next;
            meta.slots[next].lastPlayed = DateTime.Now.Ticks;
            SaveMeta();
            ReloadCatalogs();
        }
        else
        {
            SaveMeta();
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Fuer <see cref="SaveReset"/>: Spielzeit des aktiven Profils auf null.</summary>
    public static void ResetActiveStats()
    {
        EnsureLoaded();
        ProfileClock.Discard();
        meta.slots[meta.active].playSeconds = 0d;
        SaveMeta();
    }

    /// <summary>Gesammelte Spielzeit gutschreiben (ProfileClock).</summary>
    internal static void AddPlayTime(double seconds)
    {
        EnsureLoaded();
        if (seconds <= 0d) return;
        Slot s = meta.slots[meta.active];
        s.playSeconds += seconds;
        s.lastPlayed = DateTime.Now.Ticks;
        SaveMeta();
    }

    /// <summary>Erlaubte Zeichen: Buchstaben (lateinisch), Ziffern und ein paar Satzzeichen.</summary>
    public static bool IsNameChar(char c)
    {
        if (c == ' ' || c == '-' || c == '_' || c == '.' || c == '!' || c == '?' || c == '\'' || c == '&')
            return true;
        return c <= 0x24F && char.IsLetterOrDigit(c);
    }

    public static string CleanName(string name, int slot)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
            if (IsNameChar(c)) sb.Append(c);

        string clean = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " {2,}", " ").Trim();
        if (clean.Length > MaxNameLength) clean = clean.Substring(0, MaxNameLength).TrimEnd();
        // Wer den Vorgabenamen eintippt, bekommt ihn auch nach Sprachwechsel passend.
        return clean == DefaultName(slot) ? "" : clean;
    }

    // ==================================================================
    //  Kataloge
    // ==================================================================

    /// <summary>Was das aktive Profil noch im Speicher haelt, jetzt auf die Platte.</summary>
    public static void FlushActive()
    {
        FlushCatalogs();
        ProfileClock.Commit();
    }

    private static void FlushCatalogs()
    {
        try
        {
            Achievements.Flush();
            Skills.Flush();
            Bestiary.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Profiles] Speichern vor dem Wechsel: " + e.Message);
        }
    }

    /// <summary>Alle Spielstand-Kataloge aus dem (neuen) aktiven Ordner lesen.</summary>
    private static void ReloadCatalogs()
    {
        ProfilePrefs.Invalidate();
        // Shop zuerst: Verteiler und Skilltree haengen am gewaehlten Charakter.
        Shop.ReleaseRun();
        Shop.Reload();
        Achievements.Reload();
        Unlocks.Reload();
        Skills.Reload();
        Loadout.Reload();
        Bestiary.Reload();
        LevelRecords.Reload();
        StoryProgress.Reload();
        AchievementRuntime.ResyncSteam();

        Debug.Log($"[Profiles] Profil {meta.active + 1} ({NameOf(meta.active)}) geladen.");
    }

    // ==================================================================
    //  Datei
    // ==================================================================

    private static bool Valid(int slot) => slot >= 0 && slot < SlotCount;

    private static string MetaPath => Path.Combine(Application.persistentDataPath, MetaFile);

    private static void Claim(Slot s, string name)
    {
        s.used = true;
        s.name = name == null ? "" : CleanName(name, meta.slots.IndexOf(s));
        s.created = DateTime.Now.Ticks;
        s.lastPlayed = DateTime.Now.Ticks;
        s.playSeconds = 0d;
    }

    private static void EnsureLoaded()
    {
        if (meta != null || loading) return;
        loading = true;

        try
        {
            Meta loaded = null;
            if (File.Exists(MetaPath))
            {
                try
                {
                    loaded = JsonUtility.FromJson<Meta>(File.ReadAllText(MetaPath));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Profiles] {MetaFile} unlesbar, wird neu aufgebaut: {e.Message}");
                    try { File.Copy(MetaPath, MetaPath + ".corrupt", true); } catch { }
                }
            }

            bool fresh = loaded == null;
            meta = loaded ?? new Meta();
            if (meta.slots == null) meta.slots = new List<Slot>();
            while (meta.slots.Count < SlotCount) meta.slots.Add(new Slot());
            if (meta.slots.Count > SlotCount) meta.slots.RemoveRange(SlotCount, meta.slots.Count - SlotCount);
            for (int i = 0; i < SlotCount; i++) if (meta.slots[i] == null) meta.slots[i] = new Slot();

            if (fresh)
            {
                // Kaputte Meta-Datei: Ordner, in denen etwas liegt, sind trotzdem Profile.
                for (int i = 1; i < SlotCount; i++)
                    if (HasProgressFiles(Path.Combine(Application.persistentDataPath, "profile" + (i + 1))))
                        Claim(meta.slots[i], null);

                MigrateLegacy();
                Claim(meta.slots[0], null);
                meta.active = 0;
            }

            meta.active = Mathf.Clamp(meta.active, 0, SlotCount - 1);
            if (!meta.slots[meta.active].used) Claim(meta.slots[meta.active], null);

            if (fresh) SaveMeta();
        }
        finally
        {
            loading = false;
        }
    }

    private static bool HasProgressFiles(string dir)
    {
        if (!System.IO.Directory.Exists(dir)) return false;
        foreach (string f in ProgressFiles)
            if (File.Exists(Path.Combine(dir, f))) return true;
        return false;
    }

    /// <summary>
    /// Spielstand aus der Zeit vor den Profilen (direkt im Hauptordner) nach
    /// profile1 verschieben - samt Sicherungen (.v1.bak, .corrupt) und den
    /// profilgebundenen PlayerPrefs.
    /// </summary>
    private static void MigrateLegacy()
    {
        string root = Application.persistentDataPath;
        string target = Path.Combine(root, "profile1");

        try
        {
            System.IO.Directory.CreateDirectory(target);
            int moved = 0;

            foreach (string name in ProgressFiles)
            {
                foreach (string src in System.IO.Directory.GetFiles(root, name + "*"))
                {
                    string dst = Path.Combine(target, Path.GetFileName(src));
                    if (File.Exists(dst)) continue;   // schon da - nichts ueberschreiben
                    File.Copy(src, dst);
                    File.Delete(src);
                    moved++;
                }
            }

            if (moved > 0) Debug.Log($"[Profiles] Alter Spielstand nach profile1 uebernommen ({moved} Dateien).");
        }
        catch (Exception e)
        {
            Debug.LogError("[Profiles] Alter Spielstand konnte nicht verschoben werden: " + e.Message);
        }

        // PlayerPrefs: alte globale Schluessel werden zu Profil 1.
        foreach (string key in ProfilePrefKeys())
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            string to = PrefKeyOf(0, key);
            if (!PlayerPrefs.HasKey(to)) PlayerPrefs.SetInt(to, PlayerPrefs.GetInt(key));
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Alle profilgebundenen Schluessel (ohne Profil-Vorsilbe).</summary>
    internal static IEnumerable<string> ProfilePrefKeys()
    {
        foreach (string k in PrefKeysFixed) yield return k;
        foreach (string k in TutorialPanel.AllSeenKeys()) yield return k;
    }

    private static void DeleteFiles(int slot)
    {
        string dir = Path.Combine(Application.persistentDataPath, "profile" + (slot + 1));
        try
        {
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
        }
        catch (Exception e)
        {
            // Einzelne Datei gesperrt: wenigstens die Spielstaende selbst weg.
            Debug.LogWarning($"[Profiles] Ordner {dir} nicht ganz loeschbar: {e.Message}");
            foreach (string f in ProgressFiles)
            {
                try { File.Delete(Path.Combine(dir, f)); } catch { }
            }
        }

        foreach (string key in ProfilePrefKeys()) PlayerPrefs.DeleteKey(PrefKeyOf(slot, key));
        PlayerPrefs.Save();
        ProfilePrefs.Invalidate();
    }

    private static void SaveMeta()
    {
        try
        {
            string tmp = MetaPath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(meta, true));
            File.Copy(tmp, MetaPath, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Profiles] {MetaFile} nicht schreibbar: {e.Message}");
        }
    }
}

/// <summary>
/// Zaehlt die Spielzeit des aktiven Profils mit (unskaliert, auch in Menues)
/// und schreibt sie alle 30 Sekunden, beim Wegklicken und beim Beenden weg.
/// </summary>
[DisallowMultipleComponent]
public class ProfileClock : MonoBehaviour
{
    private const float CommitInterval = 30f;

    private static ProfileClock instance;
    private static double pending;
    private float timer;

    internal static void Ensure()
    {
        if (instance != null) return;
        GameObject go = new GameObject("~ProfileClock") { hideFlags = HideFlags.HideInHierarchy };
        instance = go.AddComponent<ProfileClock>();
        DontDestroyOnLoad(go);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState() => pending = 0d;

    /// <summary>Noch nicht geschriebene Zeit sofort gutschreiben.</summary>
    internal static void Commit()
    {
        double s = pending;
        pending = 0d;
        if (s > 0d) SaveProfiles.AddPlayTime(s);
    }

    /// <summary>Gesammelte Zeit verwerfen (Profil wird geloescht/zurueckgesetzt).</summary>
    internal static void Discard() => pending = 0d;

    private void Update()
    {
        // Gekappt: ein Haltepunkt oder ein haengender Ladevorgang zaehlt nicht als Spielzeit.
        pending += Mathf.Min(Time.unscaledDeltaTime, 1f);
        timer += Time.unscaledDeltaTime;
        if (timer < CommitInterval) return;
        timer = 0f;
        Commit();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) Commit();
    }

    private void OnApplicationQuit() => Commit();
}

/// <summary>
/// Was die Profilkarte zeigt, gelesen direkt aus den Dateien eines Profils -
/// ohne die Kataloge umzuschalten. Fehlt eine Datei, steht dort eben 0.
/// </summary>
public class ProfileSummary
{
    public int Coins;
    public int Character;
    public int Shards;
    public bool FinaleDone;
    public int Achievements;
    public int AchievementsTotal;

#pragma warning disable 0649 // JsonUtility fuellt die Felder
    [Serializable] private class ShopFile { public int currency; public int skinIndex; }
    [Serializable] private class StoryFile { public List<string> shards = new List<string>(); public bool finaleDone; }
    [Serializable] private class AchEntry { public string id; public bool unlocked; }
    [Serializable] private class AchFile { public List<AchEntry> entries = new List<AchEntry>(); }
#pragma warning restore 0649

    public static ProfileSummary Read(int slot)
    {
        var s = new ProfileSummary { AchievementsTotal = Ach.All.Count };
        if (!SaveProfiles.IsUsed(slot)) return s;

        string dir = SaveProfiles.DirectoryOf(slot);

        ShopFile shop = ReadJson<ShopFile>(dir, "save.json");
        if (shop != null)
        {
            s.Coins = Mathf.Max(0, shop.currency);
            s.Character = shop.skinIndex >= 0 && shop.skinIndex < Characters.Count ? shop.skinIndex : 0;
        }

        StoryFile story = ReadJson<StoryFile>(dir, "story.json");
        if (story != null)
        {
            if (story.shards != null)
                foreach (string w in StoryProgress.ShardWorlds)
                    if (story.shards.Exists(x => string.Equals(x, w, StringComparison.OrdinalIgnoreCase))) s.Shards++;
            s.FinaleDone = story.finaleDone;
        }

        AchFile ach = ReadJson<AchFile>(dir, "achievements.json");
        if (ach?.entries != null)
        {
            var known = new HashSet<string>();
            foreach (AchievementDef def in Ach.All) known.Add(def.Id);
            var counted = new HashSet<string>();
            foreach (AchEntry e in ach.entries)
                if (e != null && e.unlocked && known.Contains(e.id) && counted.Add(e.id)) s.Achievements++;
        }

        return s;
    }

    private static T ReadJson<T>(string dir, string file) where T : class
    {
        string path = Path.Combine(dir, file);
        try
        {
            return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
