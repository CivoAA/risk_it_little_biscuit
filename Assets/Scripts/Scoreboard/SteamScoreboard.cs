using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>Eine Zeile im Scoreboard.</summary>
public struct ScoreEntry
{
    public int Rank;
    public int Score;
    public string Name;
    public bool IsMe;
    public CSteamID User;
    /// <summary>Charakter des Laufs (Shop.RunSkinIndex), -1 = unbekannt.</summary>
    public int Character;
    /// <summary>Laufzeit in Sekunden, -1 = unbekannt.</summary>
    public int Seconds;
    /// <summary>Endless-Lauf (sonst Story).</summary>
    public bool Endless;
}

/// <summary>
/// Die Steam-Leaderboards: eins je Welt fuer Story und Endless zusammen,
/// benannt nach dem Wellenplan (<see cref="BoardName"/>, z.B. "Score_World2"
/// fuer den Wald).
/// Sortiert absteigend, Steam behaelt je Spieler nur den besten Wert
/// (KeepBest).
///
/// Die Boards legt das Spiel beim ersten Zugriff selbst an
/// (FindOrCreateLeaderboard) - im Steamworks-Backend kann man sie auch vorher
/// anlegen, dann muessen Name, Sortierung (Descending) und Anzeige (Numeric)
/// passen.
///
/// Hochgeladen wird nur, was <see cref="ScoreIntegrity.CanSubmit"/> durchlaesst
/// (keine Mods, keine Cheats, kein Editor/Dev-Build). Laden geht immer, solange
/// Steam laeuft - so sieht man das Board auch beim Testen im Editor.
///
/// Details je Eintrag (int, in dieser Reihenfolge): Sekunden, Kills, Mixer,
/// Schwierigkeit (0-2), Charakter (Shop.RunSkinIndex), Lauf-Level, Endless (0/1).
/// </summary>
public static class SteamScoreboard
{
    public enum Scope { Friends, Global }

    /// <summary>Die Welten in der Reihenfolge der Route (level.1 ... level.5).</summary>
    public static IReadOnlyList<string> Worlds => StoryProgress.Route;

    /// <summary>So viele Zeilen holt <see cref="Download"/> fuer die Weltrangliste.</summary>
    public const int TopCount = 8;

    private const int MaxDetails = 8;

    /// <summary>Plaetze in den Details (siehe Kopf).</summary>
    private const int DetailSeconds = 0, DetailCharacter = 4, DetailEndless = 6;

    public static string BoardName(string planId) =>
        "Score_" + Canonical(planId);

    /// <summary>"World2Demo" -> "World2", Gross/Klein wie in der Route.</summary>
    private static string Canonical(string planId)
    {
        string p = (planId ?? "").Trim().Replace("Demo", "");
        foreach (string w in StoryProgress.Route)
            if (string.Equals(w, p, StringComparison.OrdinalIgnoreCase)) return w;
        return p;
    }

    /// <summary>Name der Welt in der Spielsprache (level.N.name).</summary>
    public static string WorldName(string planId)
    {
        int i = StoryProgress.RouteIndex(Canonical(planId));
        return i < 0 ? Canonical(planId) : Loc.Get("level." + (i + 1) + ".name", Canonical(planId));
    }

    // ==================================================================
    //  Board-Handles
    // ==================================================================

    private static readonly Dictionary<string, SteamLeaderboard_t> boards = new Dictionary<string, SteamLeaderboard_t>();
    private static readonly Dictionary<string, List<Action<SteamLeaderboard_t?>>> finding =
        new Dictionary<string, List<Action<SteamLeaderboard_t?>>>();

    // CallResults muessen leben, bis Steam antwortet - sonst raeumt der GC sie weg.
    private static readonly HashSet<object> pending = new HashSet<object>();

    /// <summary>Hochgeladen, aber noch nicht bestaetigt oder fehlgeschlagen: wird beim naechsten Mal nachgereicht.</summary>
    private static readonly Dictionary<string, (int Score, int[] Details)> unsent =
        new Dictionary<string, (int, int[])>();

    /// <summary>Ein Score ist bei Steam angekommen - offene Boards laden neu.</summary>
    public static event Action<string> Uploaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        boards.Clear();
        finding.Clear();
        pending.Clear();
        unsent.Clear();
        Uploaded = null;
        NamesChanged = null;
        personaChanged = null;
    }

    public static bool Available => SteamManager.Initialized;

    private static void Find(string board, Action<SteamLeaderboard_t?> done)
    {
        if (boards.TryGetValue(board, out SteamLeaderboard_t h)) { done(h); return; }

        if (finding.TryGetValue(board, out List<Action<SteamLeaderboard_t?>> waiting))
        {
            waiting.Add(done);
            return;
        }

        waiting = new List<Action<SteamLeaderboard_t?>> { done };
        finding[board] = waiting;

        try
        {
            SteamAPICall_t call = SteamUserStats.FindOrCreateLeaderboard(board,
                ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);

            CallResult<LeaderboardFindResult_t> result = null;
            result = CallResult<LeaderboardFindResult_t>.Create((r, ioFailure) =>
            {
                pending.Remove(result);
                finding.Remove(board);

                SteamLeaderboard_t? found = null;
                if (!ioFailure && r.m_bLeaderboardFound != 0)
                {
                    boards[board] = r.m_hSteamLeaderboard;
                    found = r.m_hSteamLeaderboard;
                }
                else
                {
                    Debug.LogWarning($"[Scoreboard] '{board}' nicht gefunden (IO-Fehler: {ioFailure}).");
                }

                foreach (var cb in waiting) Safe(() => cb(found));
            });
            result.Set(call);
            pending.Add(result);
        }
        catch (Exception e)
        {
            finding.Remove(board);
            Debug.LogWarning($"[Scoreboard] Suche nach '{board}' fehlgeschlagen: {e.Message}");
            foreach (var cb in waiting) Safe(() => cb(null));
        }
    }

    // ==================================================================
    //  Hochladen
    // ==================================================================

    /// <summary>Score eines Laufs einreichen. Bleibt still liegen, wenn er nicht zaehlen darf.</summary>
    public static void Submit(string planId, int score, int[] details)
    {
        string board = BoardName(planId);

        if (!ScoreIntegrity.CanSubmit(out string reason))
        {
            Debug.Log($"[Scoreboard] (nicht eingereicht: {reason}) {board} = {score}");
            return;
        }
        if (score <= 0) return;

        // Ein besserer, noch nicht angekommener Wert desselben Boards bleibt stehen.
        if (unsent.TryGetValue(board, out var old) && old.Score >= score) return;
        unsent[board] = (score, details ?? new int[0]);
        Flush(board);
    }

    /// <summary>Haengengebliebene Uploads (kein Netz, Steam weg) nochmal versuchen.</summary>
    public static void RetryUnsent()
    {
        if (!Available || unsent.Count == 0) return;
        foreach (string board in new List<string>(unsent.Keys)) Flush(board);
    }

    private static void Flush(string board)
    {
        if (!Available || !unsent.TryGetValue(board, out var entry)) return;

        Find(board, handle =>
        {
            if (handle == null) return;

            try
            {
                int[] details = entry.Details;
                if (details.Length > MaxDetails) Array.Resize(ref details, MaxDetails);

                SteamAPICall_t call = SteamUserStats.UploadLeaderboardScore(handle.Value,
                    ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                    entry.Score, details, details.Length);

                CallResult<LeaderboardScoreUploaded_t> result = null;
                result = CallResult<LeaderboardScoreUploaded_t>.Create((r, ioFailure) =>
                {
                    pending.Remove(result);
                    if (ioFailure || r.m_bSuccess == 0)
                    {
                        Debug.LogWarning($"[Scoreboard] Upload nach '{board}' fehlgeschlagen - wird nachgereicht.");
                        return;
                    }

                    // Nur austragen, wenn nicht inzwischen ein besserer wartet.
                    if (unsent.TryGetValue(board, out var now) && now.Score <= entry.Score) unsent.Remove(board);

                    Debug.Log($"[Scoreboard] {board}: {entry.Score} hochgeladen" +
                              (r.m_bScoreChanged != 0 ? $" - neuer Bestwert, Platz {r.m_nGlobalRankNew}." : " (alter Bestwert bleibt)."));
                    Safe(() => Uploaded?.Invoke(board));
                });
                result.Set(call);
                pending.Add(result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Scoreboard] Upload nach '{board}' fehlgeschlagen: {e.Message}");
            }
        });
    }

    // ==================================================================
    //  Laden
    // ==================================================================

    /// <summary>
    /// Holt die Zeilen eines Boards. Freunde: alle Freunde mit Eintrag (und man
    /// selbst), bis <see cref="TopCount"/>. Weltweit: die besten
    /// <see cref="TopCount"/>. Liegt man selbst nicht darin, haengt die eigene
    /// Zeile hinten an. <paramref name="done"/> bekommt null, wenn Steam nicht
    /// antwortet.
    /// </summary>
    public static void Download(string planId, Scope scope, Action<List<ScoreEntry>> done)
    {
        if (!Available) { done(null); return; }

        string board = BoardName(planId);
        Find(board, handle =>
        {
            if (handle == null) { done(null); return; }

            if (scope == Scope.Friends)
            {
                Request(handle.Value, ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0, rows =>
                {
                    // Platz unter den Freunden, nicht weltweit.
                    if (rows != null)
                        for (int i = 0; i < rows.Count; i++)
                        {
                            ScoreEntry e = rows[i];
                            e.Rank = i + 1;
                            rows[i] = e;
                        }

                    if (rows != null && rows.Count > TopCount)
                    {
                        int me = rows.FindIndex(r => r.IsMe);
                        ScoreEntry mine = me >= 0 ? rows[me] : default;
                        rows.RemoveRange(TopCount, rows.Count - TopCount);
                        if (me >= TopCount) rows.Add(mine);
                    }
                    done(rows);
                });
                return;
            }

            Request(handle.Value, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, TopCount, top =>
            {
                if (top == null || top.Exists(r => r.IsMe)) { done(top); return; }

                // Eigene Zeile dazu, falls man einen Eintrag hat.
                Request(handle.Value, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, 0, 0, around =>
                {
                    if (around != null)
                    {
                        int me = around.FindIndex(r => r.IsMe);
                        if (me >= 0) top.Add(around[me]);
                    }
                    done(top);
                });
            });
        });
    }

    private static void Request(SteamLeaderboard_t handle, ELeaderboardDataRequest kind, int from, int to,
                                Action<List<ScoreEntry>> done)
    {
        try
        {
            SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntries(handle, kind, from, to);
            CallResult<LeaderboardScoresDownloaded_t> result = null;
            result = CallResult<LeaderboardScoresDownloaded_t>.Create((r, ioFailure) =>
            {
                pending.Remove(result);
                if (ioFailure) { Safe(() => done(null)); return; }

                var rows = new List<ScoreEntry>(r.m_cEntryCount);
                CSteamID me = SteamUser.GetSteamID();
                int[] details = new int[MaxDetails];
                for (int i = 0; i < r.m_cEntryCount; i++)
                {
                    if (!SteamUserStats.GetDownloadedLeaderboardEntry(r.m_hSteamLeaderboardEntries, i,
                            out LeaderboardEntry_t e, details, MaxDetails)) continue;

                    rows.Add(new ScoreEntry
                    {
                        Rank = e.m_nGlobalRank,
                        Score = e.m_nScore,
                        User = e.m_steamIDUser,
                        IsMe = e.m_steamIDUser == me,
                        Name = NameOf(e.m_steamIDUser),
                        Character = e.m_cDetails > DetailCharacter ? details[DetailCharacter] : -1,
                        Seconds = e.m_cDetails > DetailSeconds ? details[DetailSeconds] : -1,
                        Endless = e.m_cDetails > DetailEndless && details[DetailEndless] == 1,
                    });
                }
                Safe(() => done(rows));
            });
            result.Set(call);
            pending.Add(result);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Scoreboard] Laden fehlgeschlagen: {e.Message}");
            done(null);
        }
    }

    /// <summary>Steam hat einen nachgefragten Namen geliefert (<see cref="NameOf"/>).</summary>
    public static event Action NamesChanged;

    private static Callback<PersonaStateChange_t> personaChanged;

    /// <summary>
    /// Steam-Name eines Spielers. Fremde kennt Steam oft noch nicht - dann wird
    /// nachgefragt, und <see cref="NamesChanged"/> meldet, wenn er da ist.
    /// </summary>
    public static string NameOf(CSteamID user)
    {
        if (personaChanged == null)
        {
            personaChanged = Callback<PersonaStateChange_t>.Create(r =>
            {
                if ((r.m_nChangeFlags & EPersonaChange.k_EPersonaChangeName) != 0) Safe(() => NamesChanged?.Invoke());
            });
        }

        try
        {
            if (SteamFriends.RequestUserInformation(user, true)) return "...";
            string name = SteamFriends.GetFriendPersonaName(user);
            return string.IsNullOrEmpty(name) || name == "[unknown]" ? "..." : name;
        }
        catch (Exception)
        {
            return "?";
        }
    }

    private static void Safe(Action a)
    {
        try { a(); }
        catch (Exception e) { Debug.LogError("[Scoreboard] " + e); }
    }
}
