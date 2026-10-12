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
/// Fuer einen weiteren Pflicht-Reset spaeter: <see cref="SaveVersion"/> hochzaehlen.
/// </summary>
public static class SaveReset
{
    public const int SaveVersion = 1;

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
            if (ReadVersion(markerPath) >= SaveVersion) return;

            foreach (string file in ProgressFiles)
            {
                string path = Path.Combine(dir, file);
                if (!File.Exists(path)) continue;
                File.Delete(path);
                Debug.Log("[SaveReset] geloescht: " + file);
            }

            File.WriteAllText(markerPath, SaveVersion.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveReset] Reset fehlgeschlagen: " + e.Message);
        }
    }

    private static int ReadVersion(string markerPath)
    {
        if (!File.Exists(markerPath)) return 0;
        return int.TryParse(File.ReadAllText(markerPath).Trim(), out int v) ? v : 0;
    }
}
