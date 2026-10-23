/// <summary>
/// Texte des Koop-Modus. Alles laeuft ueber <see cref="Loc"/> (Schluessel
/// net.* in Resources/Localization); der deutsche Text hier ist nur der
/// Rueckfall, falls ein Schluessel fehlt.
/// </summary>
public static class NetText
{
    public static string Get(string key, string fallback) => Loc.Get(key, fallback);

    /// <summary>Warum eine Sitzung endete oder ein Beitritt scheiterte - als Satz fuer die Startseite.</summary>
    public static string Reason(string key)
    {
        switch (key)
        {
            case "net.end.host_lost": return Get(key, "Die Verbindung zum Host ist abgerissen.");
            case "net.end.connect_failed": return Get(key, "Keine Verbindung zum Host möglich.");
            case "net.end.no_steam": return Get(key, "Steam läuft nicht.");
            case "net.end.lobby_failed": return Get(key, "Steam konnte keine Lobby anlegen.");
            case "net.end.bad_code": return Get(key, "Der Code hat sechs Zeichen.");
            case "net.end.not_found": return Get(key, "Keine Lobby mit diesem Code gefunden.");
            case "net.end.join_failed": return Get(key, "Beitreten hat nicht geklappt.");
            case "net.end.host_left": return Get(key, "Der Host hat die Lobby geschlossen.");
            case "net.reject.version": return Get(key, "Ihr habt verschiedene Spielversionen.");
            case "net.reject.running": return Get(key, "Die Gruppe ist gerade in einem Lauf.");
            case "net.reject.full": return Get(key, "Die Lobby ist voll.");
            case "net.reject.kicked": return Get(key, "Der Host hat dich aus der Lobby genommen.");
            default: return string.IsNullOrEmpty(key) ? "" : Get(key, key);
        }
    }
}
