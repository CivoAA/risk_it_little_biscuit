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
            sink.Print(string.Format(T("cheat.coins", "+{0} Münzen. Neuer Stand: {1}"), betrag, Shop.Currency));
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
                ? string.Format(T("cheat.unlocks", "{0} Sachen freigeschaltet. Viel Spaß damit."), neu)
                : T("cheat.allopen", "War schon alles offen. Gierig."));
        }, hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 3: gezielt eine ID.  ->  "freischalten unlock_boba_gun"
        // ------------------------------------------------------------------
        HubConsole.Add("freischalten", "schaltet ein einzelnes Unlock frei", (args, sink) =>
        {
            // Ohne Argument: alle IDs mit Stand, damit man nicht in Unlocks.cs
            // nachschlagen muss.
            if (args.Length == 0)
            {
                sink.Print(T("cheat.unlock.ids", "freischalten <id> - bekannte IDs:"));
                foreach (UnlockDef u in Unlocks.All)
                    sink.Print($"  {(u.IsUnlocked ? "[x]" : "[ ]")} {u.Id}");
                return;
            }

            string id = args[0];
            if (Unlocks.Find(id) == null)
            {
                sink.PrintError(string.Format(T("unknown.id", "'{0}' steht auf keiner Liste."), id));
                return;
            }

            Unlocks.Grant(id);
            sink.Print(string.Format(T("cheat.unlock.done", "'{0}' ist jetzt offen."), id));
        }, usage: "<id>", hidden: true);

        // ------------------------------------------------------------------
        // Beispiel 4: reine Anzeige, kein Cheat. Steht deshalb in der Hilfe.
        // ------------------------------------------------------------------
        HubConsole.Add("status", "zeigt Muenzen und Fortschritt", (args, sink) =>
        {
            sink.Print(string.Format(T("status.coins", "Münzen: {0}"), Shop.Currency));

            sink.Print(string.Format(T("status.unlocks", "Freigeschaltet: {0} von {1}"),
                                     Unlocks.UnlockedCount, Unlocks.TotalCount));
        });

        // ------------------------------------------------------------------
        // Shop leerraeumen.  ->  "resetcookie"
        // Alle gekauften Stufen zurueck auf 0, das Ausgegebene kommt aufs Konto.
        // ------------------------------------------------------------------
        HubConsole.Add("resetcookie", "setzt den Shop zurueck", (args, sink) =>
        {
            int erstattet = Shop.ResetAllUpgrades();

            sink.Print(erstattet > 0
                ? string.Format(T("cheat.shopreset", "Shop zurückgesetzt. {0} Münzen erstattet, neuer Stand: {1}"),
                                erstattet, Shop.Currency)
                : string.Format(T("cheat.shopreset.none", "Shop war schon leer. Stand: {0}"), Shop.Currency));
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
        // Erfolge verteilen.  ->  "giberfolge"           der naechste offene
        //                         "giberfolge alle"      alle auf einmal
        //                         "giberfolge First_Win" ein bestimmter
        //
        // Geht ueber Achievements.Unlock, also mit allem was dranhaengt:
        // mitvergebene Unlocks und die Steam-Meldung.
        // ------------------------------------------------------------------
        HubConsole.Add("giberfolge", "schaltet Erfolge frei", (args, sink) =>
        {
            string was = args != null && args.Length > 0 ? args[0] : null;

            // Ohne Argument: der erste, der noch zu ist.
            if (string.IsNullOrEmpty(was))
            {
                foreach (AchievementDef d in Ach.All)
                {
                    if (d.IsUnlocked) continue;

                    Achievements.Unlock(d);
                    int offen = Achievements.TotalCount - Achievements.UnlockedCount;
                    sink.Print(string.Format(T("cheat.ach.next", "'{0}' freigeschaltet. Noch {1} zu holen."), d.Id, offen));
                    return;
                }

                sink.Print(T("cheat.ach.done", "Alles schon geschafft. Respekt."));
                return;
            }

            if (IsAll(was))
            {
                // Ueber Ach.All laufen und nicht ueber Achievements.Locked():
                // der Katalog bleibt beim Freischalten unveraendert, der
                // Spielstand nicht.
                int neu = 0;
                foreach (AchievementDef d in Ach.All)
                {
                    if (d.IsUnlocked) continue;
                    Achievements.Unlock(d);
                    neu++;
                }

                sink.Print(neu > 0
                    ? string.Format(T("cheat.ach.all", "{0} Erfolge freigeschaltet. Das war's dann wohl."), neu)
                    : T("cheat.allopen", "War schon alles offen. Gierig."));
                return;
            }

            AchievementDef def = Ach.Find(was);
            if (def == null)
            {
                sink.PrintError(string.Format(T("unknown.id", "'{0}' steht auf keiner Liste."), was) + " " +
                                T("cheat.ach.usage", "-> giberfolge / giberfolge alle / giberfolge <id>"));
                return;
            }

            if (def.IsUnlocked)
            {
                sink.Print(string.Format(T("cheat.ach.already", "'{0}' war schon offen."), def.Id));
                return;
            }

            Achievements.Unlock(def);
            sink.Print(string.Format(T("cheat.ach.one", "'{0}' freigeschaltet."), def.Id));
        }, usage: "[alle|<id>]", hidden: true);

        // ------------------------------------------------------------------
        // Erfolge zuruecksetzen.  ->  "erfolgeweg"
        // ------------------------------------------------------------------
        HubConsole.Add("erfolgeweg", "setzt alle Erfolge zurueck", (args, sink) =>
        {
            int vorher = Achievements.UnlockedCount;

            Achievements.ResetAll();

            // ResetAll feuert kein Ereignis - ein offenes Buch muss von Hand
            // nachgeladen werden, sonst steht dort noch der alte Stand.
            AchievementsBookPanel.RefreshIfOpen();

            sink.Print(vorher > 0
                ? string.Format(T("cheat.ach.reset", "{0} Erfolge zurückgesetzt. Bei Steam bleiben sie stehen, das geht nur dort."), vorher)
                : T("cheat.ach.reset.none", "Da war nichts zurückzusetzen."));
        }, hidden: true);

        // ------------------------------------------------------------------
        // Charakter-XP schenken (gewaehlter Charakter).  ->  "gibxp" (16.200 = Level 2)
        //                                                     oder "gibxp 5000"
        // ------------------------------------------------------------------
        HubConsole.Add("gibxp", "Charakter-XP fuer den gewaehlten Charakter", (args, sink) =>
        {
            int betrag = ArgAsInt(args, 0, (int)CharLevel.Level2Xp);

            SkillTreeDef tree = Skills.ActiveTree;
            Skills.SetXp(tree, Skills.XpOf(tree) + betrag);
            sink.Print($"+{betrag} XP. {CharStand()}");
        }, usage: "[anzahl]", hidden: true);

        // ------------------------------------------------------------------
        // Charakter-Level direkt setzen.  ->  "charlevel 10"   ("charlevel 0" = von vorn)
        // ------------------------------------------------------------------
        HubConsole.Add("charlevel", "setzt das Level des gewaehlten Charakters", (args, sink) =>
        {
            int level = UnityEngine.Mathf.Clamp(ArgAsInt(args, 0, 1), 0, CharLevel.MaxLevel);

            Skills.SetXp(Skills.ActiveTree, CharLevel.XpForLevel(level - Skills.BossLevelsOf(Skills.ActiveTree)));
            sink.Print(CharStand());
        }, usage: "<level>", hidden: true);

        // ------------------------------------------------------------------
        // Boss-Erstsiege des gewaehlten Charakters vergessen.  ->  "bossreset"
        // Danach gibt der naechste Sieg ueber jeden Boss wieder ein Level.
        // ------------------------------------------------------------------
        HubConsole.Add("bossreset", "vergisst die Boss-Siege des gewaehlten Charakters", (args, sink) =>
        {
            int vorher = Skills.BossLevelsOf(Skills.ActiveTree);
            Skills.ResetBossVictories(Skills.ActiveTree);
            sink.Print(string.Format(T("cheat.bossreset", "{0} Boss-Siege vergessen."), vorher) + " " + CharStand());
        }, hidden: true);

        // ------------------------------------------------------------------
        // Skilltree zuruecksetzen.  ->  "skillreset"       Baum des gewaehlten Charakters
        //                               "skillreset alle"  jeder Baum
        // Die Punkte sind danach wieder frei, das Charakter-Level bleibt.
        // ------------------------------------------------------------------
        HubConsole.Add("skillreset", "setzt den Skilltree zurueck", (args, sink) =>
        {
            bool alle = args != null && args.Length > 0 && IsAll(args[0]);

            if (alle)
            {
                foreach (SkillTreeDef tree in SkillTrees.All) Skills.ResetTree(tree);
                sink.Print(T("cheat.skillreset.all", "Alle Skilltrees zurückgesetzt.") + " " + CharStand());
                return;
            }

            SkillTreeDef active = Skills.ActiveTree;
            Skills.ResetActiveTree();
            sink.Print(string.Format(T("cheat.skillreset", "Skilltree '{0}' zurückgesetzt."),
                                     active != null ? active.Id : "?") + " " + CharStand());
        }, usage: "[alle]", hidden: true);

        // ------------------------------------------------------------------
        // Kompletter Spielstand auf Anfang.  ->  "werksreset ja"
        // Gleicher Weg wie der Knopf in den Optionen (SaveReset): Shop, Unlocks,
        // Skills/Charakter-Level, Erfolge, Verteiler, Bestiarium, Level-Rekorde.
        // Einstellungen bleiben. Ohne "ja" passiert nichts.
        // ------------------------------------------------------------------
        HubConsole.Add("werksreset", "setzt den kompletten Spielstand zurueck", (args, sink) =>
        {
            bool bestaetigt = args != null && args.Length > 0 &&
                              (args[0].Equals("ja", System.StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("yes", System.StringComparison.OrdinalIgnoreCase));
            if (!bestaetigt)
            {
                sink.Print(T("cheat.wipe.warn", "Löscht ALLES: Shop, Charaktere, Skills, Erfolge, Unlocks, Bestiarium, Rekorde."));
                sink.Print(T("cheat.wipe.confirm", "Wirklich? -> werksreset ja"));
                return;
            }

            if (!SaveReset.Allowed)
            {
                sink.PrintError(T("cheat.wipe.inrun", "Geht nicht während eines Laufs."));
                return;
            }

            sink.Close();
            SaveReset.ResetAll();
        }, usage: "ja", hidden: true);

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

    /// <summary>"Level 3 (34.830 XP, 1 Boss-Level), 2 Punkte frei." fuer den gewaehlten Charakter.</summary>
    static string CharStand()
        => string.Format(T("cheat.charstand", "Level {0} ({1} XP, {2} Boss-Level), {3} Punkte frei."),
                         Skills.Level, Skills.Xp.ToString("N0"), Skills.BossLevelsOf(Skills.ActiveTree), Skills.Points);

    /// <summary>"alle" oder "all" - die englische Fassung des Arguments geht auch.</summary>
    static bool IsAll(string arg)
        => arg.Equals("alle", System.StringComparison.OrdinalIgnoreCase) ||
           arg.Equals("all", System.StringComparison.OrdinalIgnoreCase);

    static string T(string key, string fallback) => HubConsole.T(key, fallback);
}
