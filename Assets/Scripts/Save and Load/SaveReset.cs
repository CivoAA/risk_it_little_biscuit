using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Einmaliger Spielstand-Reset beim Versionswechsel.
///
/// Liegt im persistentDataPath keine Versionsmarke (save_version.txt bzw.
/// save_version.json, es gilt die hoehere) oder steht dort eine
/// kleinere Zahl als <see cref="SaveVersion"/>, werden alle Fortschritts-Dateien
/// geloescht - Shop, Gold, Charaktere, Skilltrees, Werkbank, Story, Rekorde,
/// Bestiarium, Erfolge. Einstellungen (Audio, Sprache, Grafik) bleiben.
///
/// Laeuft in SubsystemRegistration, also vor allen BeforeSceneLoad-Loadern
/// (Shop, Skills, Unlocks, Loadout, Achievements) und vor der Profil-Migration
/// in <see cref="SaveProfiles"/>. Die Marke liegt einmal im Hauptordner; der
/// Reset gilt fuer einen alten Spielstand dort und fuer alle Profilordner.
///
/// Fuer einen weiteren Pflicht-Reset spaeter: <see cref="SaveVersion"/> hochzaehlen
/// und in <see cref="Run"/> sagen, was ab der alten Version zu tun ist.
///
/// Versionen:
///   1 - kompletter Reset (alles oben).
///   2 - Reihen-Sperre im Skilltree: nur die gelernten Knoten aller Baeume
///       weg, Charakter-XP und Boss-Siege bleiben. Wer schon auf 1 war,
///       bekommt nur das; wer noch keine Marke hat, wie bisher den kompletten
///       Reset (der leert die Skilltrees ohnehin mit).
///   3 - Neue Levelkurve (CharLevel, voller Baum = 7,5 Mio. XP): wieder nur
///       die gelernten Knoten weg. Die XP bleiben, das Level rechnet sich
///       daraus neu - niemand faengt bei 0 an.
///   4 - Sterne kosten 3 statt 2 Punkte, Charakter-Level ab 25 teurer: wieder
///       nur die gelernten Knoten weg, alle Punkte kommen zurueck. XP bleiben.
/// </summary>
public static class SaveReset
{
    public const int SaveVersion = 4;

    private const string MarkerFile = "save_version.txt";

    /// <summary>
    /// Dieselbe Marke noch einmal als .json - die Steam Auto-Cloud nimmt *.json
    /// sicher mit. Ohne Marke hielte ein frischer PC den Spielstand aus der Cloud
    /// fuer einen alten und wuerde ihn loeschen.
    /// </summary>
    private const string CloudMarkerFile = "save_version.json";

    [Serializable]
    private class CloudMarker
    {
        public int version;
    }

    private static readonly string[] ProgressFiles =
    {
        "save.json",          // Shop, Gold, Charaktere
        "skills.json",        // Skilltrees, Char-Level
        "unlocks.json",
        "loadout.json",       // Werkbank + Presets
        "story.json",
        "level_records.json",
        "bestiary.json",
        "achievements.json",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Run()
    {
        string root = Application.persistentDataPath;
        string markerPath = Path.Combine(root, MarkerFile);
        string cloudMarkerPath = Path.Combine(root, CloudMarkerFile);

        try
        {
            int version = Math.Max(ReadVersion(markerPath), ReadCloudVersion(cloudMarkerPath));
            if (version < SaveVersion)
            {
                foreach (string dir in SaveDirectories(root))
                {
                    if (version < 1) DeleteProgress(dir);
                    else if (version < 4) ClearSkillTrees(dir);
                }
            }

            // Beide Marken nachziehen - fehlt eine (z. B. weil die Cloud nur die
            // andere mitgebracht hat), steht sie danach wieder da.
            string text = SaveVersion.ToString();
            if (!File.Exists(markerPath) || File.ReadAllText(markerPath).Trim() != text)
                File.WriteAllText(markerPath, text);
            if (ReadCloudVersion(cloudMarkerPath) != SaveVersion)
                File.WriteAllText(cloudMarkerPath, JsonUtility.ToJson(new CloudMarker { version = SaveVersion }));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveReset] Reset fehlgeschlagen: " + e.Message);
        }
    }

    /// <summary>
    /// Hauptordner (Spielstand von vor den Profilen, wird danach nach profile1
    /// verschoben) plus jeder vorhandene Profilordner. Bewusst ohne
    /// <see cref="SaveProfiles.DirectoryOf"/> - das legt Ordner an und laedt die Profile.
    /// </summary>
    private static System.Collections.Generic.IEnumerable<string> SaveDirectories(string root)
    {
        yield return root;
        for (int i = 1; i <= SaveProfiles.SlotCount; i++)
        {
            string dir = Path.Combine(root, "profile" + i);
            if (Directory.Exists(dir)) yield return dir;
        }
    }

    private static void DeleteProgress(string dir)
    {
        foreach (string file in ProgressFiles)
        {
            string path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            File.Delete(path);
            Debug.Log("[SaveReset] geloescht: " + path);
        }
    }

    /// <summary>Gelernte Knoten aller Baeume weg, XP und Boss-Siege bleiben.</summary>
    private static void ClearSkillTrees(string dir)
    {
        var store = new SkillStore(dir);
        if (!File.Exists(store.SavePath)) return;

        store.Load();
        store.ClearAllUnlocks();
        store.Save();
        Debug.Log("[SaveReset] Skilltrees zurueckgesetzt, Charakter-XP bleiben.");

        // Beim naechsten Oeffnen des Skilltrees einmal Bescheid sagen
        File.WriteAllText(Path.Combine(Application.persistentDataPath, NoticeFile), "1");
    }

    /// <summary>
    /// Merker fuer den Hinweis "Skilltree wurde zurueckgesetzt". Bewusst .txt:
    /// die Steam Cloud nimmt ihn nicht mit (nur *.json und save_version.txt),
    /// er gilt nur auf dem Rechner, auf dem der Reset lief.
    /// </summary>
    private const string NoticeFile = "skilltree_reset_notice.txt";

    /// <summary>
    /// True genau einmal nach einem Skilltree-Reset durch ein Update - der
    /// Merker ist danach weg. Fragt <see cref="HubSkilltreeUI"/> beim Oeffnen.
    /// </summary>
    public static bool TakeSkilltreeNotice()
    {
        string path = Path.Combine(Application.persistentDataPath, NoticeFile);
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveReset] Hinweis-Merker: " + e.Message);
            return false;
        }
    }

    private static int ReadVersion(string markerPath)
    {
        if (!File.Exists(markerPath)) return 0;
        return int.TryParse(File.ReadAllText(markerPath).Trim(), out int v) ? v : 0;
    }

    private static int ReadCloudVersion(string path)
    {
        if (!File.Exists(path)) return 0;
        try
        {
            return JsonUtility.FromJson<CloudMarker>(File.ReadAllText(path))?.version ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    // ---------- Manueller Reset (Optionen / Konsole) ----------

    /// <summary>
    /// Waehrend eines Laufs gesperrt - der Lauf schreibt beim Ende Muenzen
    /// und Erfolge zurueck und haette damit einen halben alten Stand erzeugt.
    /// </summary>
    public static bool Allowed => GameManager.Instance == null;

    /// <summary>
    /// "Spielstand zuruecksetzen" aus den Optionen (Reiter SPIEL) bzw. der Konsole:
    /// loescht den gesamten Fortschritt des aktiven Profils (<see cref="SaveProfiles"/>,
    /// auch welche Tutorials schon gezeigt wurden) und laedt das Hauptmenue neu.
    /// Einstellungen bleiben stehen; Steam-Achievements holt sich das Spiel
    /// ueber <see cref="AchievementSteamSync"/> wieder.
    /// </summary>
    public static void ResetAll()
    {
        if (!Allowed) return;

        // Shop zuerst: Verteiler und Skilltree lesen den gewaehlten Charakter daraus.
        Shop.ResetProgress();
        Unlocks.ResetProgress();
        Skills.ResetProgress();
        Achievements.ResetProgress();
        Loadout.ResetProgress();
        Bestiary.ResetAll();
        LevelRecords.ResetAll();
        StoryProgress.ResetAll();
        // Ein neuer Spielstand erklaert die Stationen im Hub wieder von vorn.
        TutorialPanel.ResetSeen();
        // Gilt fuer das aktive Profil - die anderen beiden bleiben unberuehrt.
        SaveProfiles.ResetActiveStats();

        Debug.Log("[SaveReset] Spielstand zurueckgesetzt.");

        // Hart neu laden: alles, was in der Szene noch alte Werte zeigt, ist danach weg.
        GameSession.LoadMainMenu();
    }
}
