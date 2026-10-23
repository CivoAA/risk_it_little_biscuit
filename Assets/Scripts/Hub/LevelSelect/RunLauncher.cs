using UnityEngine.SceneManagement;

/// <summary>
/// Startet einen Lauf aus dem Hub heraus. Stand frueher als Launch() in der
/// <see cref="HubLevelSelectUI"/>; seit es die Koop-Lobby gibt, starten beide
/// darueber - wer beim Anbinden eines Levels etwas vermisst, vergleicht am
/// besten mit dem, was in der World Map beim Betreten eines Map-Punktes
/// passiert (PlayerWorldInteraction, Fall "Map").
/// </summary>
public static class RunLauncher
{
    /// <param name="mapId">Welche Welt (MapsManager.selectedMap).</param>
    /// <param name="sceneToLoad">Die Map-Szene (siehe <see cref="MapSceneSystem.ResolveScene"/>).</param>
    /// <param name="endless">Endless statt Story.</param>
    /// <param name="hubScene">Rueckreiseziel - die Szene, aus der gestartet wird.</param>
    /// <param name="beforeSwitch">Laeuft hinter der Blende direkt vor dem Umschalten (Fenster zu).</param>
    /// <returns>false, wenn gerade schon eine Blende laeuft.</returns>
    public static bool Launch(int mapId, string sceneToLoad, bool endless, string hubScene, System.Action beforeSwitch)
    {
        // 1. Story oder Endless? Das liest der Rest des Spiels aus GameSession.
        GameSession.SelectedMode = endless ? GameMode.Endless : GameMode.Story;

        // 2. Stand sichern, bevor es losgeht: der Tracker merkt sich, welche
        //    Achievements und Unlocks vor dem Lauf schon offen waren, damit der
        //    Abschlussbildschirm nur die neuen zeigt. Ach.FirstGame wird hier
        //    bewusst NICHT freigeschaltet - das macht PlayerController.StartStats()
        //    in der Zielszene, wo auch das Charakter-Level vor dem Lauf gemerkt wird.
        SessionProgressTracker.Instance?.SnapshotBeforeGame();

        // 3. Skilltree auf den gewaehlten Charakter stellen.
        Skills.SetActiveTreeForCharacter(Shop.SkinIndex);

        // 4. Welche Welt gespielt wird, steht im MapsManager - er ueberlebt den
        //    Szenenwechsel. Kommt der Spieler aus dem Hub, legt Ensure() ihn an.
        MapsManager.Ensure().selectedMap = mapId;

        // 5. Shop-Stand fuer diesen Lauf einfrieren.
        Shop.CaptureRun();

        // 6. Umschalten. Der Hub-Name ist zugleich das Rueckreiseziel:
        //    GameManager.Restart() bringt den Spieler nach dem Lauf hierher zurueck.
        GameSession.ReturnScene = hubScene;

        // Wie Hauptmenue -> Hub: Bild und Hub-Musik blenden aus, der Wechsel
        // passiert hinter der Blende.
        return SceneFader.Switch(() =>
        {
            beforeSwitch?.Invoke();
            SwitchToLevel(sceneToLoad, hubScene);
        }, () => SceneManager.GetSceneByName(MapSceneSystem.CoreScene).isLoaded);
    }

    private static void SwitchToLevel(string sceneToLoad, string hubScene)
    {
        // Der uebliche Weg: additiv laden und den Hub stilllegen. Die Map-Szene
        // holt sich GameCore selbst dazu (MapBootstrap).
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ActivateScene(sceneToLoad);
            MenuManager.Instance.DeactivateScene(hubScene);
            return;
        }

        // Rueckfallebene ohne MenuManager (hub allein gestartet): hart umschalten.
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }
}
