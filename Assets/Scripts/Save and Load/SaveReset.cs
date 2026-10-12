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
/// (Shop, Skills, Unlocks, Loadout, Achievements).
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
/// </summary>
public static class SaveReset
{
    public const int SaveVersion = 2;

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
        string dir = Application.persistentDataPath;
        string markerPath = Path.Combine(dir, MarkerFile);

        try
        {
            int version = ReadVersion(markerPath);
            if (version >= SaveVersion) return;

            if (version < 1) DeleteProgress(dir);
            else if (version < 2) ClearSkillTrees(dir);

            File.WriteAllText(markerPath, SaveVersion.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveReset] Reset fehlgeschlagen: " + e.Message);
        }
    }

    private static void DeleteProgress(string dir)
    {
        foreach (string file in ProgressFiles)
        {
            string path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            File.Delete(path);
            Debug.Log("[SaveReset] geloescht: " + file);
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
    /// loescht den gesamten Fortschritt und laedt das Hauptmenue neu.
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

        Debug.Log("[SaveReset] Spielstand zurueckgesetzt.");

        // Hart neu laden: alles, was in der Szene noch alte Werte zeigt, ist danach weg.
        GameSession.LoadMainMenu();
    }
}
