using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Einmaliger Spielstand-Reset beim Versionswechsel.
///
/// Liegt im persistentDataPath keine save_version.txt oder steht dort eine
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
/// </summary>
public static class SaveReset
{
    public const int SaveVersion = 3;

    private const string MarkerFile = "save_version.txt";

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

        try
        {
            int version = ReadVersion(markerPath);
            if (version >= SaveVersion) return;

            foreach (string dir in SaveDirectories(root))
            {
                if (version < 1) DeleteProgress(dir);
                else if (version < 3) ClearSkillTrees(dir);
            }

            File.WriteAllText(markerPath, SaveVersion.ToString());
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
    }

    private static int ReadVersion(string markerPath)
    {
        if (!File.Exists(markerPath)) return 0;
        return int.TryParse(File.ReadAllText(markerPath).Trim(), out int v) ? v : 0;
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
