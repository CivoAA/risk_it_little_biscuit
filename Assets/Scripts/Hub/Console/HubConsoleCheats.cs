/// <summary>
/// HIER kommen die Cheat-Codes rein.
///
/// Ein Eintrag = ein Code. Das Muster ist immer gleich:
///
///     HubConsole.Add("meincode", "was es tut", (args, sink) =>
///     {
///         ... irgendwas freischalten ...
///         sink.Print("Antwort im Terminal");
///     }, hidden: true);
///
/// hidden: true  -> taucht nicht in "hilfe" auf. Genau das will man bei Cheats,
///                  sonst kann man sie ja einfach ablesen.
/// hidden: false -> normaler Befehl, steht in der Liste.
///
/// Wer gar nicht in Code will: am HubConsoleTerminal im Inspector gibt es eine
/// Liste "Cheat-Codes", da reichen Code + Antwort + Unlock-ID.
/// </summary>
public static class HubConsoleCheats
{
    /// <summary>Wird einmal beim ersten Oeffnen der Konsole aufgerufen.</summary>
    public static void Register()
    {
        // ------------------------------------------------------------------
        // Beispiel 1: Muenzen schenken.  ->  "gibkekse"  oder  "gibkekse 2000"
        // ------------------------------------------------------------------
        HubConsole.Add("gibkekse", "Muenzen aufs Konto", (args, sink) =>
        {
            int betrag = ArgAsInt(args, 0, 500);

            if (SaveGame.Instance == null || SaveGame.Instance.currentData == null)
            {
                sink.PrintError("Kein Spielstand geladen. Hier gibt es nichts zu holen.");
                return;
            }

            SaveGame.Instance.currentData.currency += betrag;
            SaveGame.Instance.SaveGameData();
            sink.Print($"+{betrag} Muenzen. Neuer Stand: {SaveGame.Instance.currentData.currency}");
        }, hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 2: alles freischalten.  ->  "alleswirdgut"
        // ------------------------------------------------------------------
        HubConsole.Add("alleswirdgut", "schaltet alle Unlocks frei", (args, sink) =>
        {
            if (UnlockManager.Instance == null)
            {
                sink.PrintError("Kein UnlockManager in dieser Runde.");
                return;
            }

            int neu = 0;
            foreach (Unlock u in UnlockManager.Instance.unlocks)
            {
                if (u == null || u.isUnlocked) continue;
                UnlockManager.Instance.Unlock(u.id);
                neu++;
            }

            sink.Print(neu > 0
                ? $"{neu} Sachen freigeschaltet. Viel Spass damit."
                : "War schon alles offen. Gierig.");
        }, hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 3: gezielt eine ID.  ->  "freischalten unlock_boba_gun"
        // ------------------------------------------------------------------
        HubConsole.Add("freischalten", "schaltet ein einzelnes Unlock frei", (args, sink) =>
        {
            if (args.Length == 0)
            {
                sink.PrintError("Und was? -> freischalten <id>");
                return;
            }

            if (UnlockManager.Instance == null)
            {
                sink.PrintError("Kein UnlockManager in dieser Runde.");
                return;
            }

            string id = args[0];
            if (UnlockManager.Instance.GetUnlock(id) == null)
            {
                sink.PrintError($"'{id}' steht auf keiner Liste.");
                return;
            }

            UnlockManager.Instance.Unlock(id);
            sink.Print($"'{id}' ist jetzt offen.");
        }, usage: "<id>", hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 4: reine Anzeige, kein Cheat. Steht deshalb in der Hilfe.
        // ------------------------------------------------------------------
        HubConsole.Add("status", "zeigt Muenzen und Fortschritt", (args, sink) =>
        {
            if (SaveGame.Instance != null && SaveGame.Instance.currentData != null)
                sink.Print($"Muenzen: {SaveGame.Instance.currentData.currency}");
            else
                sink.Print("Muenzen: -");

            if (UnlockManager.Instance != null)
            {
                int offen = 0;
                int gesamt = UnlockManager.Instance.unlocks.Count;
                foreach (Unlock u in UnlockManager.Instance.unlocks)
                    if (u != null && u.isUnlocked) offen++;

                sink.Print($"Freigeschaltet: {offen} von {gesamt}");
            }
        });

        // ------------------------------------------------------------------
        // Ab hier: deine eigenen Codes.
        // ------------------------------------------------------------------
    }

    // ---------------------------------------------------------------- Helfer

    /// <summary>
    /// Argument Nr. <paramref name="index"/> als Zahl, sonst der Standardwert.
    /// Spart in jedem Cheat das gleiche int.TryParse-Gefummel.
    /// </summary>
    static int ArgAsInt(string[] args, int index, int fallback)
    {
        if (args == null || index >= args.Length) return fallback;
        return int.TryParse(args[index], out int value) ? value : fallback;
    }
}
