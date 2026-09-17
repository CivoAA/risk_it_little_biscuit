using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Schnittstelle zum Achievement-System. Statisch und ohne Szenenobjekt:
/// das System startet über <see cref="Bootstrap"/> vor der ersten Szene und ist
/// damit ab dem allerersten Frame aktiv - egal, ob das Spiel im Hauptmenü, im
/// Hub, in der World Map oder direkt in der Test-Szene gestartet wird.
///
/// Benutzung im Spiel:
///   Achievements.Unlock(Ach.FirstWin);
///   Achievements.Progress(Ach.Kill100, 1f);
///
/// Beide Aufrufe sind billig und dürfen auch pro Frame passieren - ist ein
/// Achievement schon offen, kostet der Aufruf nur eine Dictionary-Abfrage.
/// </summary>
public static class Achievements
{
    private static AchievementStore store;
    private static bool initialized;

    /// <summary>
    /// Sandbox (Test-Szene): Fortschritt wird im Speicher geführt, aber nie auf
    /// die Platte geschrieben und nie an Steam gemeldet. Vor dem ersten Zugriff
    /// setzen - danach wirkt nur noch der Schreibschutz.
    /// </summary>
    public static bool SandboxMode
    {
        get => sandbox;
        set
        {
            sandbox = value;
            if (store != null) store.ReadOnly = value;
        }
    }
    private static bool sandbox;

    /// <summary>Feuert genau einmal pro Achievement, in dem Moment, in dem es freigeschaltet wird.</summary>
    public static event Action<AchievementDef> Unlocked;

    // ==================================================================
    //  Start
    // ==================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Init();
        AchievementRuntime.Ensure();
    }

    /// <summary>Lädt den Spielstand. Mehrfachaufrufe sind harmlos.</summary>
    public static void Init()
    {
        if (initialized) return;
        initialized = true;

        store = new AchievementStore { ReadOnly = sandbox };
        store.Load();
    }

    private static AchievementStore Store
    {
        get
        {
            if (!initialized) Init();
            return store;
        }
    }

    // ==================================================================
    //  Abfragen
    // ==================================================================

    public static bool IsUnlocked(AchievementDef def) => def != null && Store.IsUnlocked(def.Id);

    public static bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && Store.IsUnlocked(id);

    public static float GetValue(AchievementDef def) => def == null ? 0f : Store.GetValue(def.Id);

    /// <summary>Fortschritt von 0 bis 1 - für Balken in der Anzeige.</summary>
    public static float GetProgress01(AchievementDef def)
    {
        if (def == null) return 0f;
        if (IsUnlocked(def)) return 1f;
        return Mathf.Clamp01(Store.GetValue(def.Id) / def.Goal);
    }

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (AchievementDef def in Ach.All)
            {
                if (IsUnlocked(def)) n++;
            }
            return n;
        }
    }

    public static int TotalCount => Ach.All.Count;

    // ==================================================================
    //  Auslösen
    // ==================================================================

    /// <summary>Schaltet ein Achievement frei. Ist es schon offen, passiert nichts.</summary>
    public static void Unlock(AchievementDef def)
    {
        if (def == null) return;

        AchievementStore s = Store;
        if (s.IsUnlocked(def.Id)) return;

        s.SetUnlocked(def.Id, true);
        s.SetValue(def.Id, def.Goal);
        s.Flush();

        GrantRewards(def);
        AchievementSteamSync.Push(def);

        try
        {
            Unlocked?.Invoke(def);
        }
        catch (Exception e)
        {
            // Ein kaputter Zuhörer darf das Freischalten nicht verschlucken.
            Debug.LogError($"[Achievements] Fehler im Unlocked-Handler für '{def.Id}': {e}");
        }

        Debug.Log($"[Achievements] Freigeschaltet: {def.Id}");
    }

    /// <summary>Freischalten über die Id - für Konsole, Cheats und alte Aufrufstellen.</summary>
    public static void Unlock(string id)
    {
        AchievementDef def = Ach.Find(id);
        if (def == null)
        {
            Debug.LogWarning($"[Achievements] Unbekannte Id '{id}' - steht sie im Katalog (Ach.cs)?");
            return;
        }
        Unlock(def);
    }

    /// <summary>
    /// Zählt Fortschritt hoch und schaltet frei, sobald das Ziel erreicht ist.
    /// Zwischenstände werden gesammelt und gebündelt geschrieben, nicht bei jedem Kill.
    /// </summary>
    public static void Progress(AchievementDef def, float amount)
    {
        if (def == null || amount <= 0f) return;

        AchievementStore s = Store;
        if (s.IsUnlocked(def.Id)) return;

        float value = Mathf.Min(s.GetValue(def.Id) + amount, def.Goal);
        s.SetValue(def.Id, value);

        if (value >= def.Goal) Unlock(def);
    }

    public static void Progress(string id, float amount) => Progress(Ach.Find(id), amount);

    // ==================================================================
    //  Belohnungen
    // ==================================================================

    private static void GrantRewards(AchievementDef def)
    {
        // In der Sandbox darf nichts nach aussen wirken: Souls und Unlocks würden
        // sonst in skills.json bzw. unlocks.json landen.
        if (sandbox) return;

        if (def.Souls > 0)
        {
            Skills.AddCurrency(def.Souls);
            WM_UIController.Instance?.UpdateSkillCurrencyText();
        }

        if (!string.IsNullOrEmpty(def.GrantsUnlock))
        {
            Unlocks.Grant(def.GrantsUnlock);
        }
    }

    // ==================================================================
    //  Wartung
    // ==================================================================

    /// <summary>Schreibt anstehende Änderungen sofort auf die Platte.</summary>
    public static void Flush() => Store.Flush();

    /// <summary>Setzt jeden Fortschritt zurück. Steam bleibt unberührt.</summary>
    public static void ResetAll()
    {
        Store.ResetAll();
        Debug.Log("[Achievements] Alle Achievements zurückgesetzt.");
    }

    public static string SavePath => Store.SavePath;

    /// <summary>
    /// Übernimmt einen Stand, der bei Steam schon offen ist, in den lokalen
    /// Spielstand - ohne Belohnungen, denn die gab es auf dem anderen Rechner
    /// bereits. Wird von <see cref="AchievementSteamSync"/> aufgerufen.
    /// </summary>
    internal static void AdoptFromSteam(AchievementDef def)
    {
        if (def == null) return;

        AchievementStore s = Store;
        if (s.IsUnlocked(def.Id)) return;

        s.SetUnlocked(def.Id, true);
        s.SetValue(def.Id, def.Goal);
        s.Flush();

        Debug.Log($"[Achievements] Von Steam übernommen: {def.Id}");
    }

    /// <summary>Alle noch nicht freigeschalteten Achievements - für Cheats und Werkzeuge.</summary>
    public static IEnumerable<AchievementDef> Locked()
    {
        foreach (AchievementDef def in Ach.All)
        {
            if (!IsUnlocked(def)) yield return def;
        }
    }
}
