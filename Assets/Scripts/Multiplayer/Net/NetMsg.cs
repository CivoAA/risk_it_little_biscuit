/// <summary>
/// Alle Nachrichtenarten. Das erste Byte jeder Nachricht. Neue Arten nur
/// anhaengen und bei Aenderungen am Format <see cref="NetSession.Protocol"/>
/// hochzaehlen - ein Mitspieler mit altem Stand wird dann sauber abgewiesen
/// statt Unsinn zu lesen.
/// </summary>
public enum NetMsg : byte
{
    None = 0,

    // ------------------------------------------------------------- Lobby
    Hello = 1,          // C->H: Protokoll, Version, Name, SteamID, Charakter-Level
    Welcome = 2,        // H->C: dein Platz
    Reject = 3,         // H->C: Grund (Text-Schluessel)
    LobbyState = 4,     // H->alle: alle Plaetze und Einstellungen
    PickChar = 5,       // C->H: Charakter waehlen (-1 = keiner)
    SetReady = 6,       // C->H: bereit / nicht bereit
    StartRun = 7,       // H->alle: es geht los (Seed, Welt, Modus, Schwierigkeit, Belegung)
    Ping = 8,
    Pong = 9,
    ProfileInfo = 10,   // C->H: neue Charakter-Level (nach Skilltree/Shop)
    BackToLobby = 11,   // H->alle: Lauf vorbei, alle zurueck in die Lobby

    // -------------------------------------------------------------- Lauf
    RunLoaded = 30,     // C->H: Szene steht
    RunGo = 31,         // H->alle: alle da, los
    Clock = 32,         // H->alle: Laufzeit des Hosts
    PauseState = 33,    // ->alle: ich habe ein Auswahlfenster offen (Level-Up, Mixer, ...)
    XpGain = 34,        // ->H: so viel Erfahrung eingesammelt
    TeamXp = 35,        // H->alle: Gesamterfahrung des Teams
    PlayerState = 36,   // ->alle (unzuverlaessig): Position, Bild, Leben ...
    PlayerEvent = 37,   // ->alle: niedergegangen, wiederbelebt, raus
    BossOrder = 38,     // H->Spieler: setz diesen Boss bei dir hin
    BossAnnounce = 39,  // ->alle: Boss erscheint bei Spieler X
    BossDefeated = 40,  // ->alle: der Boss ist gefallen
    RunEnd = 41,        // H->alle: alle sind raus - verloren
    PlayerLeftRun = 42, // ->alle: Lauf verlassen (Aufgeben)

    // -------------------------------------- Bilder aus der Welt der anderen
    // (NetVisuals: Mitspieler-Figuren, Gegner, Effekte)
    VisEnter = 60,      // Besitzer->Spieler: das taucht jetzt bei dir auf (mit vollem Aussehen)
    VisLeave = 61,      // Besitzer->Spieler: weg (ausser Sicht, verschwunden oder tot)
    VisLook = 62,       // Besitzer->Spieler: Aussehen hat sich geaendert (Sprite, Farbe, ...)
    VisMotion = 63,     // Besitzer->Spieler (unzuverlaessig): Positionen
    EnemyHit = 64,      // Spieler->Besitzer: Treffer an deinen Gegnern
    EnemyCtrl = 65,     // Spieler->Besitzer: Bremsen, Festkleben, Ziehen, Locken

    // ------------------------------------------------------------ Bilder
    SpriteDef = 80,     // Absender->Empfaenger: Sprite-Nummer -> Textur/Name
    MaterialDef = 82,
    SpriteRequest = 83, // Empfaenger->Absender: dieses Sprite finde ich nicht, schick die Pixel
    SpriteBlob = 84,    // Absender->Empfaenger: Pixel eines zur Laufzeit gebauten Sprites

    // ----------------------------------------------------------- Pickups
    PickupEnter = 90,
    PickupLeave = 91,
    PickupTake = 92,    // Sammler->Besitzer: ich hab's
    PickupMove = 93,

    // ------------------------------------------------- Treffer am Spieler
    PlayerHit = 100,    // Besitzer des Angreifers->Spieler: du nimmst Schaden
    PlayerDrift = 101,  // Sog (Milchpanzer)
    PlayerFreeze = 102, // Eisblock (Eiskaiser)

    // ------------------------------------------------------------- Welt
    MixerUsed = 110,
}
