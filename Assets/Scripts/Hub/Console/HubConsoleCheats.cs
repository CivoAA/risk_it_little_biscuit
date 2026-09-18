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
        // Beispiel 1: Muenzen schenken.  ->  "gibkekse" (500.000) oder "gibkekse 2000"
        // ------------------------------------------------------------------
        HubConsole.Add("gibkekse", "Muenzen aufs Konto", (args, sink) =>
        {
            int betrag = ArgAsInt(args, 0, 500000);

            Shop.AddCurrency(betrag);
            sink.Print($"+{betrag} Muenzen. Neuer Stand: {Shop.Currency}");
        }, hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 2: alles freischalten.  ->  "alleswirdgut"
        // ------------------------------------------------------------------
        HubConsole.Add("alleswirdgut", "schaltet alle Unlocks frei", (args, sink) =>
        {
            int neu = 0;
            foreach (UnlockDef u in Unlocks.All)
            {
                if (u.IsUnlocked) continue;
                Unlocks.Grant(u);
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

            string id = args[0];
            if (Unlocks.Find(id) == null)
            {
                sink.PrintError($"'{id}' steht auf keiner Liste.");
                return;
            }

            Unlocks.Grant(id);
            sink.Print($"'{id}' ist jetzt offen.");
        }, usage: "<id>", hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 4: reine Anzeige, kein Cheat. Steht deshalb in der Hilfe.
        // ------------------------------------------------------------------
        HubConsole.Add("status", "zeigt Muenzen und Fortschritt", (args, sink) =>
        {
            sink.Print($"Muenzen: {Shop.Currency}");

            sink.Print($"Freigeschaltet: {Unlocks.UnlockedCount} von {Unlocks.TotalCount}");
        });

        // ------------------------------------------------------------------
        // Shop leerraeumen.  ->  "resetcookie"
        // Alle gekauften Stufen zurueck auf 0, das Ausgegebene kommt aufs Konto.
        // ------------------------------------------------------------------
        HubConsole.Add("resetcookie", "setzt den Shop zurueck", (args, sink) =>
        {
            int erstattet = Shop.ResetAllUpgrades();

            sink.Print(erstattet > 0
                ? $"Shop zurueckgesetzt. {erstattet} Muenzen erstattet, neuer Stand: {Shop.Currency}"
                : $"Shop war schon leer. Stand: {Shop.Currency}");
        }, hidden: true);

        // ------------------------------------------------------------------
        // Unlock-Liste aufmachen.  ->  "unlocks"
        // Im Hub gibt es dafuer noch keinen Knopf - das Fenster soll spaeter bei
        // den Achievements haengen (siehe HUB_UNLOCKS_TODO.md). Bis dahin ist das
        // hier der Weg, es anzuschauen.
        // ------------------------------------------------------------------
        HubConsole.Add("unlocks", "zeigt die Unlock-Liste", (args, sink) =>
        {
            // Erst zu, dann auf: zwei Fenster uebereinander, die beide auf Escape
            // hoeren, ist nur Verwirrung.
            sink.Close();
            UnlockPanel.Open();
        }, hidden: true);

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
