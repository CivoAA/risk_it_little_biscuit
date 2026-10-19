using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Darf ein Score ans Steam-Scoreboard? Nein, wenn:
///
///   - Steam nicht laeuft
///   - Editor oder Development-Build (unser eigenes Testen)
///   - Sandbox (Test-Szene)
///   - ein Mod-Loader im Spiel steckt (<see cref="ModsDetected"/>): BepInEx,
///     MelonLoader, Harmony, UnityExplorer & Co. - erkannt an geladenen
///     Assemblies und an den Dateien, die sie neben die .exe legen
///   - in diesem Profil je ein Cheat aus dem Terminal benutzt wurde
///     (<see cref="MarkCheated"/>). Die Fahne steht in prefs.json des Profils,
///     geht mit der Steam Cloud mit und bleibt auch nach "Spielstand
///     zuruecksetzen" stehen - Cheats wie "alleswirdgut" hinterlassen Spuren,
///     die ein Reset nicht ganz wegraeumt (Steam-Erfolge).
///
/// Das haelt Gelegenheits-Cheater fern, nicht jeden: Steam vertraut bei
/// Leaderboards dem Client. Wer gezielt faelscht, wird im Steamworks-Backend
/// von Hand rausgeworfen.
/// </summary>
public static class ScoreIntegrity
{
    /// <summary>Schluessel in <see cref="ProfilePrefs"/>.</summary>
    public const string CheatedKey = "scoreboard_cheated";

    /// <summary>Teile von Assembly-Namen, die nur Mod-Loader und Cheat-Tools mitbringen.</summary>
    private static readonly string[] ModAssemblies =
    {
        "bepinex", "harmony", "melonloader", "monomod", "unityexplorer", "doorstop",
        "unhollower", "il2cppinterop", "sinai", "cheat", "trainer",
    };

    /// <summary>Was Mod-Loader neben die .exe legen. Das Spiel selbst bringt nichts davon mit.</summary>
    private static readonly string[] ModFiles =
    {
        "winhttp.dll", "version.dll", "doorstop_config.ini", ".doorstop_version", "dobby.dll",
    };

    private static readonly string[] ModFolders = { "BepInEx", "MelonLoader", "Mods", "Plugins", "UserLibs" };

    private static bool? modsDetected;
    private static string modHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        modsDetected = null;
        modHint = null;
    }

    /// <summary>Ein Cheat aus dem Terminal ist gelaufen - dieses Profil reicht keine Scores mehr ein.</summary>
    public static void MarkCheated()
    {
        if (Cheated) return;
        ProfilePrefs.SetInt(CheatedKey, 1);
        Debug.Log("[Scoreboard] Cheat benutzt - dieses Profil zaehlt nicht mehr fuers Scoreboard.");
    }

    public static bool Cheated => ProfilePrefs.GetInt(CheatedKey, 0) != 0;

    /// <summary>Steckt ein Mod-Loader im Spiel? Wird einmal je Start geprueft.</summary>
    public static bool ModsDetected
    {
        get
        {
            if (modsDetected == null) modsDetected = Scan(out modHint);
            return modsDetected.Value;
        }
    }

    /// <summary>Darf jetzt ein Score hoch? <paramref name="reason"/> sagt sonst, warum nicht (fuers Log).</summary>
    public static bool CanSubmit(out string reason)
    {
        reason = Blocker;
        return reason == null;
    }

    /// <summary>Grund, warum nichts eingereicht wird - null, wenn alles passt.</summary>
    public static string Blocker
    {
        get
        {
            if (!SteamManager.Initialized) return "Steam laeuft nicht";
            if (Application.isEditor || Debug.isDebugBuild) return "Editor/Dev-Build";
            if (Achievements.SandboxMode) return "Sandbox";
            if (ModsDetected) return "Mod erkannt: " + modHint;
            if (Cheated) return "Cheats in diesem Profil";
            return null;
        }
    }

    /// <summary>Fuer den Hinweis im Scoreboard: Mods oder Cheats (nicht Editor & Co. - das sehen nur wir).</summary>
    public static bool BlockedForPlayer => ModsDetected || Cheated;

    private static bool Scan(out string hint)
    {
        hint = null;

        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name.ToLowerInvariant();
                foreach (string bad in ModAssemblies)
                {
                    if (!name.Contains(bad)) continue;
                    hint = asm.GetName().Name;
                    Debug.LogWarning($"[Scoreboard] Fremde Assembly geladen: {hint}");
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Scoreboard] Assemblies nicht lesbar: " + e.Message);
        }

        // Im Editor liegt "Plugins" & Co. ganz normal herum - nur im Build schauen.
        if (Application.isEditor) return false;

        try
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(root)) return false;

            foreach (string f in ModFiles)
            {
                if (!File.Exists(Path.Combine(root, f))) continue;
                hint = f;
                Debug.LogWarning($"[Scoreboard] Mod-Datei neben dem Spiel: {f}");
                return true;
            }

            foreach (string d in ModFolders)
            {
                if (!Directory.Exists(Path.Combine(root, d))) continue;
                hint = d + "/";
                Debug.LogWarning($"[Scoreboard] Mod-Ordner neben dem Spiel: {d}");
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Scoreboard] Spielordner nicht lesbar: " + e.Message);
        }

        return false;
    }
}
