using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// Anonyme Nutzungszaehler fuer uns Entwickler: welcher Charakter, welche Welt,
/// welche Waffen. Laeuft ueber Steam-Stats (wie die Erfolgs-Zaehler), die im
/// Steamworks-Backend als "Aggregated" markiert sind - Steam rechnet daraus
/// Gesamtsummen ueber alle Spieler. Im Spiel und im Steam-Profil sieht davon
/// niemand etwas; abgerufen wird mit dem Publisher-Key (Tools/steam_metriken.py).
///
/// Ein Stat, der im Backend (noch) nicht angelegt ist, wird stillschweigend
/// uebersprungen - GetStat liefert dann false. Die Liste aller Namen schreibt
/// <c>Tools > Erfolge > Metriken-Stats ausgeben</c>.
///
/// Editor, Development Builds und Sandbox-Spielstaende zaehlen nicht mit,
/// damit unser eigenes Testen die Zahlen nicht verfaelscht. Dort steht nur im
/// Log, was gezaehlt worden waere.
///
/// Pro Lauf:
///   m_char{N}_runs / _wins / _deaths / _min   Charakter N (Shop.RunSkinIndex)
///   m_map_{plan}_story / _endless / _wins / _min   Welt (MapDefinition.PlanId)
///   m_pick_{weaponId}   Waffe/Buff/Evo, die am Ende des Laufs im Besitz war
/// Abgebrochene Laeufe = runs - wins - deaths.
/// </summary>
public static class RunMetrics
{
    /// <summary>Welten, fuer die Stats angelegt werden (PlanId, klein geschrieben).</summary>
    public static readonly string[] Maps = { "world1", "world2", "world5", "world6", "world4" };

    private static bool runActive;
    private static bool picksReported;
    private static int character;
    private static string map;
    private static float minutesReported;

    private static bool Live =>
        SteamManager.Initialized && !Achievements.SandboxMode && !Debug.isDebugBuild;

    /// <summary>Vom GameManager beim Start eines Laufs auf einer echten Karte.</summary>
    public static void RunStarted()
    {
        runActive = false;
        if (MapDefinition.Active == null) return;

        runActive = true;
        picksReported = false;
        minutesReported = 0f;
        character = Shop.RunSkinIndex;
        map = Sanitize(MapDefinition.Active.PlanId);

        Add($"m_char{character}_runs", 1);
        Add($"m_map_{map}_{(GameSession.IsEndless ? "endless" : "story")}", 1);
    }

    /// <summary>
    /// Vom GameManager bei jedem Abschluss. Nach "Endless weiterspielen" kommt
    /// ein zweiter - Sieg und Tod zaehlen dann beide, Waffen nur einmal.
    /// </summary>
    public static void RunEnded(bool won, float gameTime, bool continuedAsEndless)
    {
        if (!runActive) return;

        if (won)
        {
            Add($"m_char{character}_wins", 1);
            Add($"m_map_{map}_wins", 1);
        }
        else if (!continuedAsEndless)
        {
            Add($"m_char{character}_deaths", 1);
        }

        int minutes = Mathf.FloorToInt(gameTime / 60f - minutesReported);
        if (minutes > 0)
        {
            minutesReported += minutes;
            Add($"m_char{character}_min", minutes);
            Add($"m_map_{map}_min", minutes);
        }

        if (!picksReported)
        {
            picksReported = true;
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                var seen = new HashSet<string>();
                ReportPicks(player.activeWeapon, seen);
                ReportPicks(player.activeBuffs, seen);
                ReportPicks(player.activeEvos, seen);
            }
        }

        if (!won) runActive = false;
    }

    private static void ReportPicks(Weapon[] weapons, HashSet<string> seen)
    {
        if (weapons == null) return;

        foreach (Weapon w in weapons)
        {
            // -1 = nie erhalten; durch eine Evo ersetzte (RemovedLevel) zaehlen mit.
            if (w == null || string.IsNullOrEmpty(w.weaponID) || w.weaponLevel == -1) continue;
            if (seen.Add(w.weaponID)) Add($"m_pick_{w.weaponID}", 1);
        }
    }

    private static void Add(string stat, int amount)
    {
        if (!Live)
        {
            if (Debug.isDebugBuild) Debug.Log($"[Metrics] (nicht gezaehlt) {stat} +{amount}");
            return;
        }

        try
        {
            if (!SteamUserStats.GetStat(stat, out int value)) return;
            if (SteamUserStats.SetStat(stat, value + amount)) AchievementSteamSync.MarkStorePending();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Metrics] Steam-Stat '{stat}' fehlgeschlagen: {e.Message}");
        }
    }

    private static string Sanitize(string id) =>
        string.IsNullOrEmpty(id) ? "unknown" : id.Trim().ToLowerInvariant().Replace(' ', '_');

    /// <summary>Alle Stat-Namen, die im Steamworks-Backend angelegt werden muessen.</summary>
    public static List<string> AllStatNames()
    {
        var names = new List<string>();

        for (int c = 0; c < Characters.Count; c++)
        {
            names.Add($"m_char{c}_runs");
            names.Add($"m_char{c}_wins");
            names.Add($"m_char{c}_deaths");
            names.Add($"m_char{c}_min");
        }

        foreach (string m in Maps)
        {
            names.Add($"m_map_{m}_story");
            names.Add($"m_map_{m}_endless");
            names.Add($"m_map_{m}_wins");
            names.Add($"m_map_{m}_min");
        }

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            names.Add($"m_pick_{def.Id}");
        }

        return names;
    }
}
