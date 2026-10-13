using System;
using Steamworks;
using UnityEngine;

/// <summary>
/// Bindeglied zu Steam. Zwei Richtungen:
///
///   Spiel -> Steam: jedes lokal freigeschaltete Achievement wird gemeldet.
///   Steam -> Spiel: was bei Steam schon offen ist, wird lokal übernommen.
///
/// Die zweite Richtung ist der Grund, warum ein Spieler nach Neuinstallation
/// oder auf einem zweiten Rechner nicht wieder bei null anfängt. Zusammengeführt
/// wird immer als Vereinigung - es wird nie etwas entzogen.
///
/// StoreStats() ist ein Netzwerk-Aufruf und wird deshalb gesammelt und höchstens
/// einmal pro Speicherintervall abgeschickt, statt bei jedem Achievement.
/// </summary>
public static class AchievementSteamSync
{
    private static bool storePending;

    private static bool Available => SteamManager.Initialized && !Achievements.SandboxMode;

    /// <summary>Meldet ein lokal freigeschaltetes Achievement an Steam.</summary>
    public static void Push(AchievementDef def)
    {
        if (def == null || !Available) return;

        try
        {
            SteamUserStats.SetAchievement(def.SteamApiName);
            storePending = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] Steam-Meldung für '{def.SteamApiName}' fehlgeschlagen: {e.Message}");
        }

        // Balken auf voll, damit Steam-Profil und Achievement zusammenpassen.
        PushStat(def, def.Goal);
    }

    /// <summary>
    /// Meldet den Zählerstand eines Fortschritts-Achievements an seinen Stat.
    /// Nur lokal bei Steam - abgeschickt wird gesammelt über <see cref="StoreIfPending"/>.
    /// </summary>
    public static void PushStat(AchievementDef def, float value)
    {
        if (def == null || !def.HasProgress || !Available) return;

        try
        {
            if (SteamUserStats.SetStat(def.SteamStatName, Mathf.FloorToInt(value)))
            {
                storePending = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] Steam-Stat '{def.SteamStatName}' fehlgeschlagen: {e.Message}");
        }
    }

    /// <summary>Schickt gesammelte Änderungen ab. Wird vom AchievementRuntime getaktet.</summary>
    public static void StoreIfPending()
    {
        if (!storePending || !Available) return;

        storePending = false;

        try
        {
            SteamUserStats.StoreStats();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] SteamUserStats.StoreStats fehlgeschlagen: {e.Message}");
        }
    }

    /// <summary>
    /// Einmaliger Abgleich, sobald Steam bereit ist: fehlende Achievements in
    /// beide Richtungen nachtragen.
    /// </summary>
    public static void Reconcile()
    {
        if (!Available) return;

        int pulled = 0;
        int pushed = 0;

        foreach (AchievementDef def in Ach.All)
        {
            bool steamHasIt;

            try
            {
                if (!SteamUserStats.GetAchievement(def.SteamApiName, out steamHasIt))
                {
                    // Das Achievement ist im Steamworks-Backend noch nicht angelegt.
                    // Im Spiel funktioniert trotzdem alles, nur ohne Steam-Popup.
                    continue;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Achievements] Steam-Abfrage für '{def.SteamApiName}' fehlgeschlagen: {e.Message}");
                continue;
            }

            if (def.HasProgress && !steamHasIt) ReconcileStat(def);

            bool localHasIt = Achievements.IsUnlocked(def);

            if (steamHasIt && !localHasIt)
            {
                Achievements.AdoptFromSteam(def);
                pulled++;
            }
            else if (localHasIt && !steamHasIt)
            {
                Push(def);
                pushed++;
            }
        }

        StoreIfPending();

        if (pulled > 0 || pushed > 0)
        {
            Debug.Log($"[Achievements] Steam-Abgleich: {pulled} übernommen, {pushed} nachgemeldet.");
        }
    }

    /// <summary>
    /// Zählerstand zwischen Stat und Spielstand angleichen: der höhere gewinnt.
    /// Fehlt der Stat im Backend, liefert GetStat false - dann bleibt alles, wie es ist.
    /// </summary>
    private static void ReconcileStat(AchievementDef def)
    {
        try
        {
            if (!SteamUserStats.GetStat(def.SteamStatName, out int steamValue)) return;

            float localValue = Achievements.GetValue(def);

            if (steamValue > localValue) Achievements.AdoptValueFromSteam(def, steamValue);
            else if (localValue > steamValue) PushStat(def, localValue);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Achievements] Steam-Stat-Abgleich für '{def.SteamStatName}' fehlgeschlagen: {e.Message}");
        }
    }

    /// <summary>
    /// Löscht die Achievements im Steam-Profil. Nur für Entwicklung gedacht -
    /// wird nirgends automatisch aufgerufen.
    /// </summary>
    public static void ClearAllOnSteam()
    {
        if (!Available) return;

        foreach (AchievementDef def in Ach.All)
        {
            try
            {
                SteamUserStats.ClearAchievement(def.SteamApiName);
                if (def.HasProgress) SteamUserStats.SetStat(def.SteamStatName, 0);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Achievements] Steam-Reset für '{def.SteamApiName}' fehlgeschlagen: {e.Message}");
            }
        }

        storePending = true;
        StoreIfPending();
        Debug.Log("[Achievements] Steam-Achievements zurückgesetzt.");
    }
}
