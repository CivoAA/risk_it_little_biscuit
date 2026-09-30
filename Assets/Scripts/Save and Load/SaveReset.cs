using UnityEngine;

/// <summary>
/// "Spielstand zuruecksetzen" aus den Optionen (Reiter SPIEL): loescht den
/// gesamten Fortschritt und laedt das Hauptmenue neu.
///
/// Fortschritt heisst die Speicherdateien - Shop (save.json), Unlocks,
/// Skills, Achievements, Verteiler und Bestiarium. Einstellungen (Ton, Anzeige, Sprache)
/// bleiben stehen. Steam-Achievements lassen sich von hier aus nicht
/// zuruecknehmen: laeuft Steam, holt sich das Spiel die dort offenen wieder
/// (siehe <see cref="AchievementSteamSync"/>).
/// </summary>
public static class SaveReset
{
    /// <summary>
    /// Waehrend eines Laufs gesperrt - der Lauf schreibt beim Ende Muenzen
    /// und Erfolge zurueck und haette damit einen halben alten Stand erzeugt.
    /// </summary>
    public static bool Allowed => GameManager.Instance == null;

    public static void ResetAll()
    {
        if (!Allowed) return;

        // Shop zuerst: Verteiler und Skilltree lesen den gewaehlten Charakter daraus.
        Shop.ResetProgress();
        Unlocks.ResetProgress();
        Skills.ResetProgress();
        Achievements.ResetProgress();
        Loadout.ResetProgress();
        Bestiary.ResetAll();
        LevelRecords.ResetAll();

        Debug.Log("[SaveReset] Spielstand zurueckgesetzt.");

        // Hart neu laden: alles, was in der Szene noch alte Werte zeigt, ist danach weg.
        GameSession.LoadMainMenu();
    }
}
