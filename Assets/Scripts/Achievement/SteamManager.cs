using Steamworks;
using UnityEngine;
using System;
using System.IO;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }
    public static bool Initialized { get; private set; } = false;

    private string logPath;

    void Awake()
    {
        // Singleton Setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Log-Datei vorbereiten
        logPath = Path.Combine(Application.persistentDataPath, "steam_debug.log");
        WriteDebug("==== SteamManager gestartet ====");
        WriteDebug("Build Pfad: " + Application.dataPath);
        WriteDebug("steam_api64.dll vorhanden: " + File.Exists(Path.Combine(Application.dataPath, "../steam_api64.dll")));
        WriteDebug("steam_appid.txt vorhanden: " + File.Exists(Path.Combine(Application.dataPath, "../steam_appid.txt")));
        WriteDebug("Aktuelles Datum: " + DateTime.Now);
        WriteDebug("System Info: " + SystemInfo.operatingSystem + " | " + SystemInfo.deviceName);

        // Steam Initialisierung
        WriteDebug("Versuche SteamAPI.Init() aufzurufen...");
        try
        {
            if (SteamAPI.Init())
            {
                Initialized = true;
                Debug.Log("✅ SteamAPI initialized successfully.");
                WriteDebug("✅ SteamAPI initialized successfully.");

                try
                {
                    string user = SteamFriends.GetPersonaName();
                    uint appId = SteamUtils.GetAppID().m_AppId;
                    Debug.Log($"[SteamManager] User: {user}");
                    Debug.Log($"[SteamManager] AppID: {appId}");
                    WriteDebug($"[SteamManager] Steam User: {user}");
                    WriteDebug($"[SteamManager] Steam AppID: {appId}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[SteamManager] Konnte Steam Infos nicht abrufen: " + e.Message);
                    WriteDebug("⚠ Steam Infos konnten nicht gelesen werden: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("❌ SteamAPI.Init() returned false.");
                WriteDebug("❌ SteamAPI.Init() returned false — möglicherweise falsche AppID oder kein Zugriff auf Build.");
                Initialized = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("⚠ SteamAPI Init exception: " + e.Message);
            WriteDebug("⚠ SteamAPI Init exception: " + e.Message);
            Initialized = false;
        }

        // Statusausgabe
        Debug.Log("[SteamManager] Steam Initialized: " + Initialized);
        if (!Initialized)
        {
            Debug.LogWarning("[SteamManager] Steam NOT initialized! Prüfe steam_appid.txt oder starte Spiel über Steam.");
            WriteDebug("[SteamManager] ❌ Steam NOT initialized!");
            WriteDebug("→ Stelle sicher, dass Steam läuft und dein Account Zugriff auf den Branch hat.");
        }
        else
        {
            WriteDebug("[SteamManager] ✅ Steam ist aktiv und verbunden.");
        }
    }

    void Update()
    {
        if (Initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    void OnDestroy()
    {
        if (Initialized)
        {
            WriteDebug("SteamAPI.Shutdown() wird ausgeführt...");
            SteamAPI.Shutdown();
            Initialized = false;
            WriteDebug("SteamAPI erfolgreich heruntergefahren.");
        }
    }

    // 🔹 Schreibt eine Zeile in die Debug-Datei UND Unity-Konsole
    private void WriteDebug(string msg)
    {
        string line = $"{DateTime.Now:HH:mm:ss} :: {msg}";
        Debug.Log(line);
        try
        {
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Konnte Debug-Log nicht schreiben: " + e.Message);
        }
    }
}
