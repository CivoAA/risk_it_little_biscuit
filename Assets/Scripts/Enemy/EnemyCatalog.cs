using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Gegner, die ein Wellenplan ansprechen kann. Namen statt Prefab-Felder -
/// damit ein Plan reiner Text bleibt und nicht bei jeder Aenderung durch den
/// Inspector muss.
///
/// Stand frueher in SpawnCatalog.cs und ist hierher gewandert, weil jetzt der
/// ganze Gegner an dieser Id haengt und nicht mehr nur sein Prefab.
/// </summary>
/// <remarks>
/// Die Zahlen stehen fest dran, weil die Prefabs sie als Zahl speichern - ein
/// geloeschter oder eingeschobener Eintrag darf keine andere Id verschieben.
/// Der Name dagegen ist der Schluessel im Bestiarium (bestiary.json); beim
/// Umbenennen dort in <see cref="Bestiary"/> die alte Schreibweise nachtragen.
/// Freie Luecken (23, 24, 26, 27) waren Elite-/Miniboss-Entwuerfe, die nie
/// einen Katalogeintrag bekommen haben.
/// </remarks>
public enum EnemyId
{
    None = 0,

    // Grundgegner
    Marshmello = 1,
    EliteMarshmello = 2,
    EvilSlime = 3,
    MausMitMesser = 4,
    MiniMilch = 5,
    SaureMilch = 6,
    Muffin = 7,
    Suppe = 8,
    Pancake = 9,
    Fetti = 10,

    /// <summary>Eine der zehn Slime-Varianten, zufaellig gewaehlt.</summary>
    Slime = 11,

    // Besonderes
    Blocker = 12,              // bewegt sich kaum - das ist der Kaefig, nicht der Gegner
    EliteFluegdolch = 13, // hiess MiniBossMarshmello
    MesserMaus1 = 14,
    MesserMaus2 = 15,
    KeksKoenig = 16,

    // Neu (Gegner-Werkstatt)
    Eichel = 17,
    Fliegenpilz = 18,
    Fluegeldolch = 19,
    Kirschslime = 20,

    /// <summary>
    /// Zwischenboss der Kueche: Milchkarton auf Panzerketten mit Strohhalm-Sog
    /// und Ruelpser (EnemyMilchpanzer). War bis Oktober 2026 ein normaler Brocken.
    /// </summary>
    Milchpanzer = 21,
    WeisseMessermaus = 22,

    // Wald: Elite-Fassung des Fliegenpilzes.
    EliteFliegenpilz = 25,     // hiess MinibossFliegenpliz

    /// <summary>Endboss des Waldes: der Baumriese, der Feuer speit (EnemyGlutwurz).</summary>
    Glutwurz = 28,

    /// <summary>Wald: Honig-Klumpen. Hat die Werte und den Platz des alten Milchpanzers uebernommen.</summary>
    Honey = 29,

    /// <summary>Zwischenboss: grosser lila Schleim mit Krone, Riesensprung (EnemySchleimkoenig).</summary>
    Schleimkoenig = 30,

    /// <summary>
    /// Der Todes-Ramen: kommt im Story-Modus, sobald der Boss gefallen ist, und
    /// beendet den Lauf. Werte wie der alte Tod (fin_tot_0), Bild vom Ramen.
    /// </summary>
    TodesRamen = 31,

    /// <summary>
    /// Der Verkohlte: ein im Ofen vergessener Keks, Boss der Dunkelwelt
    /// (EnemyVerkohlter). Glutausbruch, Kruemelwurf, Aschesprung, ab der
    /// Haelfte Schattenklone. Steht noch in keinem Wellenplan - Test-Szene.
    /// </summary>
    Verkohlter = 32,

    /// <summary>
    /// Der Verkohlte als Tod (Finisher der Demo): taucht im Bild auf, laeuft
    /// ein Stueck auf den Spieler zu und explodiert in einer Zone, der niemand
    /// entkommt. Gleiche Bilder, gleiches Skript, Rolle DeathBoss.
    /// </summary>
    VerkohlterTod = 33,

    // Eiswelt (Eisgletscher, Karte 3)

    /// <summary>Kugel Minz-Schoko-Eis, schlittert auf ihrer Schmelzpfuetze. Grundgegner.</summary>
    Minzkugel = 34,

    /// <summary>Raketen-Wassereis, springt auf dem Stiel wie auf einem Pogo-Stick (HopMovement).</summary>
    Raketeneis = 35,

    /// <summary>Elite: Waffeltuete mit wirbelnder Softeis-Frisur, schaukelt auf der Spitze.</summary>
    Softi = 36,

    /// <summary>
    /// Endboss der Eiswelt: Kaiserpinguin aus Eiscreme (EnemyEiskaiser).
    /// Kaiser-Kick (Lawinenkugel), Frostring mit Luecken, der einfriert.
    /// </summary>
    Eiskaiser = 37,

    /// <summary>
    /// Die Lawinenkugel aus dem Kaiser-Kick: rollt dem Spieler nach und frisst
    /// die Horde (EiskaiserLawinenkugel). Kommt nur vom Eiskaiser, in keinem Plan.
    /// </summary>
    Lawinenkugel = 38,

    // Geisterwald (Karte 4)

    /// <summary>
    /// Endboss des Geisterwalds: Phase 1 ein grosses Laken-Gespenst, darunter
    /// steckt Squiddy, die Traubengelee-Qualle mit Heiligenschein (EnemySquiddy).
    /// Katalogname "Das Gespenst" - die Enthuellung soll eine Ueberraschung bleiben.
    /// </summary>
    Squiddy = 39,

    /// <summary>Baby-Squiddy aus der Quallen-Brut (SquiddyBaby). Kommt nur von Squiddy, in keinem Plan.</summary>
    QuallenBaby = 40,

    // Eiswelt, zweite Welle Gegner

    /// <summary>Eiskeks-Sandwich: das Vanilleeis ist ein Maul. Huepft und schnappt bei jeder Landung zu (HopMovement).</summary>
    EisSandwich = 41,

    /// <summary>Zuckerwatte-Woelkchen am Papierstiel, schwebt schnell heran und laesst es schneien. Schwarmgegner.</summary>
    Zuckerwolke = 42,

    /// <summary>Kokos-Yeti: schokogetunkte Kokosmakrone mit Kirsche, stapft schwer. Der Brocken der Eiswelt.</summary>
    KokosYeti = 43,

    // Eiswelt, dritte Welle Gegner

    /// <summary>Kakaotasse: rote Emaille-Tasse mit heissem Kakao und Marshmallows, watschelt auf Stummelfuesschen.</summary>
    Kakaotasse = 44,

    /// <summary>Puddingrobbe: Panna-Cotta-Robbe mit Himbeersosse, robbt und rutscht auf dem Bauch, jongliert ihre Himbeere.</summary>
    Puddingrobbe = 45,

    /// <summary>Sternplaetzchen mit Zuckerguss: rollt wie ein Rad, schnell und schwach. Schwarmgegner.</summary>
    Plaetzchen = 46,

    // Kueche, neue Gegner (Welle 4)

    /// <summary>Wurst-Dackel: Wiener Wuerstchen im Hotdog-Broetchen-Maentelchen, trabt flink. Schnell und schwach.</summary>
    Wurstdackel = 47,

    /// <summary>Spiegelei: kriecht wie eine Amoebe, der Dotter mit Gesicht wabbelt hinterher.</summary>
    Spiegelei = 48,

    /// <summary>Spaghetti-Monster: grimmiges Fleischbaellchen mit Tomatensosse auf fuenf Nudelbeinen. Der Brocken der Kueche.</summary>
    Fleischball = 49,

    /// <summary>Erbsenschote: kriecht wie eine Raupe, drei Erbsen-Geschwister schauen heraus.</summary>
    Erbsenschote = 50,

    /// <summary>Kaesekeil: Lochkaese auf Stummelbeinen, in einem Loch wohnt eine Maus.</summary>
    Kaesekeil = 51,

    /// <summary>Popcorn: huepft in kleinen Saetzen (HopMovement). Schwarmgegner.</summary>
    Popcorn = 52,

    // Wald, neue Gegner (Welle 4)

    /// <summary>Maronen-Igel: Marone schaut aus der stacheligen Schale, trippelt.</summary>
    Maronenigel = 53,

    /// <summary>Beerenspinne: Heidelbeere auf acht Salzstangen-Beinen, schnell.</summary>
    Beerenspinne = 54,

    /// <summary>Walnuss-Krabbe: Walnusspanzer, Stielaugen, schnappende Scheren. Zaeh.</summary>
    Walnusskrabbe = 55,

    /// <summary>Honigbaerchen: Honig-Quetschflasche in Baerform, der Honig schwappt im Kopf, eine Biene kreist.</summary>
    Honigbaer = 56,

    // Eiswelt, vierte Welle Gegner

    /// <summary>Eiswuerfel-Baer: Gummibaer, in einen Eiswuerfel eingefroren, stapft auf den herausschauenden Fuessen.</summary>
    Eiswuerfelbaer = 57,

    /// <summary>Pfefferminz-Schnecke: Minzgelee-Schnecke mit Pfefferminzbonbon als Haus, gleitet mit Frostspur.</summary>
    Minzschnecke = 58,

    // Zweite Elites fuer Kueche und Eiswelt

    /// <summary>Elite der Kueche: wuetende Erdbeer-Sahne-Torte mit brennender Kerze, stapft auf Schoko-Stummelbeinen.</summary>
    Kerzentorte = 59,

    /// <summary>Elite der Eiswelt: Fuerst-Pueckler-Eisbombe mit Eiskristall-Krone, Monokel und Zwirbelbart.</summary>
    FuerstPueckler = 60,

    // Geisterwald (Karte 4): leuchtende Normalgegner

    /// <summary>Kuerbiswicht: Kuerbis mit geschnitztem Gesicht, die Kerze drin flackert. Watschelt auf Rankenbeinchen.</summary>
    Kuerbiswicht = 61,

    /// <summary>Gluehbirne: Glasbirne mit Gluehwendel und Messingsockel, stakst auf Drahtbeinen, Wackelkontakt.</summary>
    Gluehbirne = 62,

    /// <summary>Leuchtpilz: Nachtpilz mit cyan gluehenden Tupfen und Lamellen, der Hut federt nach.</summary>
    Leuchtpilz = 63,

    /// <summary>Baisergeist: schwebendes Baiser-Gespenst mit hohlen, fliederfarben gluehenden Augen.</summary>
    Baisergeist = 64,

    /// <summary>Lakritzkater: schwarzer Lakritz-Kater mit gluehenden Schlitzaugen und Bonbon-Gloeckchen, trabt flink.</summary>
    Lakritzkater = 65,

    /// <summary>Lebkuchen-Zombie: angebissener Lebkuchenmann mit giftgruen leuchtendem Zuckerguss. Der Brocken des Geisterwalds.</summary>
    Lebkuchenzombie = 66,

    /// <summary>Saurer Gluehwurm: Neon-Fruchtgummiwurm, buckelt sich wie eine Spannerraupe voran.</summary>
    Gluehwurm = 67,

    /// <summary>Windbeutel-Eule: aufgeschnittener Windbeutel mit Sahne, der Deckel ist der Eulenkopf mit Bernsteinaugen.</summary>
    Windbeuteleule = 68,

    /// <summary>Flambierkirsche: Kirsche mit blauer Rumflamme auf dem Kopf, flitzt. Schneller Schwarmgegner.</summary>
    Flambierkirsche = 69,

    // Vulkanwelt (Karte 5): Normalgegner

    /// <summary>Feuerschote: rote Chili mit brennendem Zuendschnur-Stiel, marschiert auf Stummelbeinen.</summary>
    Feuerschote = 70,

    /// <summary>Lavakuchen: Schoko-Fondant als Mini-Vulkan mit Puderzucker-Haube, der fluessige Kern blubbert ueber.</summary>
    Lavakuchen = 71,

    /// <summary>Takoyaki: Oktopus-Baellchen trippelt auf seinen Tentakeln, Bonitoflocken tanzen in der Hitze. Flink.</summary>
    Takoyaki = 72,

    /// <summary>Kaesefondue: Caquelon mit Schweizerkreuzen stapft auf dem Rechaud-Dreibein, Kaese blubbert. Zaeh.</summary>
    Kaesefondue = 73,

    /// <summary>Schaschlik: Grillspiess, huepft auf seiner Holzspitze wie auf einem Pogo-Stick (HopMovement).</summary>
    Schaschlik = 74,

    /// <summary>Wasabi-Klecks: kriechender Wasabi mit Ingwer-Huetchen, dem selbst die Augen traenen.</summary>
    Wasabiklecks = 75,

    /// <summary>Pizzastueck: Peperoni-Pizza auf Teigbeinchen, der Kaese tropft ueber den Rand.</summary>
    Pizzastueck = 76,

    /// <summary>Drachenfrucht: kleiner Pitaya-Drache mit gruenen Schuppenblatt-Zacken, stapft.</summary>
    Drachenfrucht = 77,

    /// <summary>Ofenbrot-Golem: riesiger Brotlaib mit gluehenden Einschnitten und Baguette-Armen. Der Brocken der Vulkanwelt.</summary>
    Brotgolem = 78,

    /// <summary>Knisterbrause: Brocken Brausekristall, huepft knisternd heran. Schneller Schwarmgegner.</summary>
    Knisterbrause = 79,

    // Zwischenbosse von Eiswelt, Geisterwald und Vulkanwelt

    /// <summary>
    /// Zwischenboss der Eiswelt: Schneemann aus drei Eiskugeln. Frostblick =
    /// "Ochs am Berg" - wer sich bewegt, waehrend er starrt, friert fest (EnemyStracciatella).
    /// </summary>
    Stracciatella = 80,

    /// <summary>
    /// Zwischenboss des Geisterwalds: Lebkuchenhaus auf Haehnchenkeulen. Spukspur =
    /// ruft den Geist des Spielers, der dessen Weg versetzt nachlaeuft (EnemyKnusperhaus).
    /// </summary>
    Knusperhaus = 81,

    /// <summary>
    /// Zwischenboss der Vulkanwelt: Riesen-Folienkartoffel. Heisse Kartoffel = tickende
    /// Bombe am Spieler, weitergeben durch Beruehren (EnemyOfenkartoffel).
    /// </summary>
    Ofenkartoffel = 82,

    // Elites von Geisterwald und Vulkanwelt (je zwei, Ring-Debuet wie in Kueche/Eiswelt)

    /// <summary>Elite des Geisterwalds: Hexenkessel voll leuchtender Minzsuppe, Augen und Mund gluehen von innen. Kocht ueber.</summary>
    Spuksuppe = 83,

    /// <summary>Elite des Geisterwalds: leuchtende Himbeer-Goetterspeise mit Sahnehaube, wackelt heran und macht BUH.</summary>
    Wackelgeist = 84,

    /// <summary>Elite der Vulkanwelt: Mais-Taco in Cowboystiefeln, die Fuellung brennt - und bricht aus, wenn es zu scharf wird.</summary>
    Feuertaco = 85,

    /// <summary>Elite der Vulkanwelt: Creme Brulee im Porzellanfoermchen, flambiert sich mit dem Kuechenbrenner selbst.</summary>
    CremeBrulee = 86,

    // Wald, Ersatz fuer den Pfannkuchen (der bleibt in der Kueche)

    /// <summary>Baumkuchen-Stumpf: Baumkuchen als wandelnder Baumstumpf mit Schokoglasur, Setzling und Pilzchen obendrauf. Der Brocken des Waldes.</summary>
    Baumkuchen = 87,
}

/// <summary>
/// Was ein Gegner im Lauf IST. Loest die drei losen Schalter MiniBoss,
/// BossBoss und Death_Boss ab.
///
/// Die drei Bools konnten sich widersprechen (zwei gleichzeitig an), und jede
/// Stelle im Code musste sie in der richtigen Reihenfolge abfragen, um das
/// Richtige zu treffen. Eine Rolle kann nur eines davon sein.
/// </summary>
public enum EnemyRole
{
    /// <summary>Normaler Gegner. Zaehlt auf Kill100 / Kill1000 / Kill10000.</summary>
    Normal,

    /// <summary>
    /// Kleiner, leuchtender Gegner mit mehr Leben und mehr XP. Kommt IMMER
    /// einzeln (der Director spawnt nie mehrere auf einmal und nimmt ihn nie
    /// aus dem Pool). Raeumt den Kaefig, zaehlt auf die Elite-Erfolge.
    /// Hiess bis Oktober 2026 "MiniBoss" - die Zahl 1 ist geblieben.
    /// </summary>
    Elite,

    /// <summary>Der Keks-Koenig: laeuft nicht selbst, ruft danach den Tod.</summary>
    Boss,

    /// <summary>Der Tod. Zieht alle XP an.</summary>
    DeathBoss,

    /// <summary>Kaefig-Wand. Zaehlt nicht zum Druck und gibt nichts.</summary>
    Blocker,

    /// <summary>
    /// Echter Miniboss: ein Bosskampf in der Mitte des Levels, gesetzt per
    /// Boss-Beat im Wellenplan. Unverrueckbar wie ein Boss, beendet den Lauf
    /// aber nicht. Gibt ordentlich XP (aus dem Katalog) und zieht beim Tod alle
    /// XP an. In der Werkstatt heisst die Rolle "Zwischenboss".
    /// </summary>
    [InspectorName("Zwischenboss")]
    MiniBoss,
}

/// <summary>
/// In welche Richtung das Bild gezeichnet ist.
///
/// Der alte Schalter hiess "rightlooking", hat aber etwas anderes getan: er
/// hat entschieden, OB ueberhaupt gespiegelt wird - und dann in die falsche
/// Richtung (flipX = true, wenn der Spieler RECHTS steht). Deshalb stand er
/// bei der Haelfte der Prefabs auf 0, nur damit der Gegner nicht verkehrt
/// herum laeuft. Hier steht jetzt, was wirklich Sache ist, und das Spiegeln
/// folgt daraus.
/// </summary>
public enum EnemyFacing
{
    /// <summary>Schaut den Betrachter an oder ist symmetrisch - nie spiegeln.</summary>
    Neutral,

    /// <summary>Das Bild schaut nach rechts. Spiegeln, wenn der Spieler links steht.</summary>
    ArtFacesRight,

    /// <summary>Das Bild schaut nach links. Spiegeln, wenn der Spieler rechts steht.</summary>
    ArtFacesLeft,
}

/// <summary>
/// Alles, was einen Gegner ausmacht - an einer Stelle, im Code.
///
/// Frueher lagen diese Werte als [SerializeField] in 25 Prefab-Dateien. Das
/// hatte drei Folgen, die alle wehgetan haben: Balancing hiess 25 Dateien
/// oeffnen, ein Prefab-Neubau hat die Werte mitgenommen, und niemand konnte
/// zwei Gegner nebeneinander vergleichen. Dieselbe Begruendung wie bei den
/// Erfolgen (<see cref="Ach"/>) und den Wellenplaenen (<see cref="WavePlans"/>).
///
/// Die Felder ab <see cref="Sheet"/> braucht nur die Gegner-Werkstatt
/// (Tools -> Gegner). Sie stehen mit hier drin, damit ein Gegner EIN Eintrag
/// ist und nicht zwei, die auseinanderlaufen koennen.
/// </summary>
public sealed class EnemyDef
{
    public readonly EnemyId Id;

    /// <summary>Anzeigename in der Werkstatt. Im Spiel nicht sichtbar.</summary>
    public readonly string Name;

    // ------------------------------------------------------------ Kampfwerte

    public readonly float Health;
    public readonly float Damage;
    public readonly float Speed;
    public readonly int Exp;

    /// <summary>
    /// Was der Gegner im Druck-Budget des <see cref="SpawnDirector"/> wiegt.
    /// Wird aus Leben, Schaden und Tempo gerechnet (<see cref="EnemyCatalog.AutoThreat"/>),
    /// nicht mehr von Hand eingetragen - der Marshmello wiegt 1, alles andere
    /// ergibt sich daraus.
    /// </summary>
    public readonly float Threat;

    /// <summary>
    /// Weggelegt: taucht in den Werkstaetten nicht mehr in den Listen auf,
    /// bleibt aber vollstaendig erhalten und laesst sich mit einem Klick
    /// zurueckholen. Steht er noch in einem Wellenplan, spawnt er dort weiter -
    /// ein Archiv-Haken soll nie einen Lauf kaputt machen.
    /// </summary>
    public readonly bool Archived;

    /// <summary>Wie lange ein Treffer den Gegner zurueckschiebt. 0 = gar nicht.</summary>
    public readonly float PushTime;

    public readonly EnemyRole Role;
    public readonly EnemyFacing Facing;

    // -------------------------------------------------- Nur fuer die Werkstatt

    /// <summary>Sprite-Sheet, aus dem die Laufanimation gebaut wird. Leer = noch keins.</summary>
    public readonly string Sheet;

    /// <summary>Bilder pro Sekunde. Jeder Gegner hat andere Frames, also je Gegner eigen.</summary>
    public readonly float Fps;

    /// <summary>Trefferkreis. 0 = die Werkstatt rechnet ihn aus dem Sprite.</summary>
    public readonly float ColliderRadius;
    public readonly Vector2 ColliderOffset;

    /// <summary>Wie gross der Gegner im Spiel steht.</summary>
    public readonly float Scale;

    /// <summary>Wohin die Werkstatt das Prefab baut. Leer = noch keins gebaut.</summary>
    public readonly string Prefab;

    /// <summary>
    /// Huepfer: erstes und letztes Bild der Laufanimation, in denen der Gegner
    /// in der Luft ist. Nur dann kommt er vom Fleck (<see cref="HopMovement"/>,
    /// die Werkstatt haengt es beim Bauen an). -1 = laeuft normal.
    /// </summary>
    public readonly int HopFirst, HopLast;

    public bool Hops => HopFirst >= 0 && HopLast >= HopFirst;

    public EnemyDef(EnemyId id, string name, float health, float damage, float speed, int exp,
                    float pushTime, EnemyRole role, EnemyFacing facing,
                    string sheet, float fps, float colliderRadius, Vector2 colliderOffset,
                    float scale, string prefab, bool archived = false,
                    int hopFirst = -1, int hopLast = -1)
    {
        Id             = id;
        Name           = string.IsNullOrEmpty(name) ? id.ToString() : name;
        Health         = Mathf.Max(1f, health);
        Damage         = Mathf.Max(0f, damage);
        Speed          = speed;
        Exp            = Mathf.Max(0, exp);
        Threat         = EnemyCatalog.AutoThreat(Health, Damage, Speed, role);
        Archived       = archived;
        PushTime       = Mathf.Max(0f, pushTime);
        Role           = role;
        Facing         = facing;
        Sheet          = sheet ?? "";
        Fps            = Mathf.Clamp(fps, 0.5f, 60f);
        ColliderRadius = Mathf.Max(0f, colliderRadius);
        ColliderOffset = colliderOffset;
        Scale          = scale <= 0f ? 1f : scale;
        Prefab         = prefab ?? "";
        HopFirst       = hopFirst;
        HopLast        = hopLast;
    }

    /// <summary>Bosse und Minibosse lassen sich nicht ziehen oder wegschieben.</summary>
    public bool IsBoss => EnemyCatalog.IsBossRole(Role);

    /// <summary>Wie lange ein Durchlauf der Laufanimation dauert - reine Anzeige.</summary>
    public float LoopSeconds(int frameCount)
    {
        return Fps > 0f ? frameCount / Fps : 0f;
    }
}

/// <summary>
/// Der Katalog aller Gegner.
///
/// Den Datenblock unten schreibt die Gegner-Werkstatt (Tools -> Gegner ->
/// Werkstatt) beim Speichern neu. Alles ausserhalb der beiden Marker bleibt
/// stehen - dort darf von Hand ergaenzt werden, was ein Tool nicht kann.
/// </summary>
public static class EnemyCatalog
{
    // ------------------------------------------------------------ Beutewerte
    //
    // Standen frueher als nackte Zahlen mitten im Sterbe-Code von Enemy, mit
    // einem Kommentar, der nicht zur Zahl passte ("1 zu 50 (2%)" bei einem
    // Wurf aus 100). Hier stehen sie einmal und stimmen.

    /// <summary>Wahrscheinlichkeit auf einen Magneten beim Tod eines Gegners.</summary>
    public const float MagnetChance = 0.002f;   // 1 von 500

    /// <summary>Wahrscheinlichkeit auf ein Herz beim Tod eines Gegners.</summary>
    public const float HeartChance = 0.01f;     // 1 von 100

    /// <summary>
    /// So viel Max-Leben gibt ein goldenes Herz (Skill "GoldenHeartChance").
    /// Um denselben Betrag wird der Spieler zusaetzlich zur normalen
    /// Herz-Heilung geheilt.
    /// </summary>
    public const float GoldenHeartMaxHealth = 5f;

    // ------------------------------------------------------------ XP ueber die Zeit

    /// <summary>Bis hierhin (Sekunden) geben normale Gegner volle XP.</summary>
    public const float NormalXpFalloffStart = 180f;   // Minute 3

    /// <summary>Ab hier (Sekunden) gilt <see cref="NormalXpLateFactor"/> voll.</summary>
    public const float NormalXpFalloffEnd = 240f;     // Minute 4

    /// <summary>XP-Anteil normaler Gegner nach <see cref="NormalXpFalloffEnd"/>.</summary>
    public const float NormalXpLateFactor = 0.8f;

    /// <summary>Ab hier (Sekunden) sinkt der XP-Anteil ein zweites Mal.</summary>
    public const float NormalXpLate2Start = 600f;     // Minute 10

    /// <summary>Ab hier (Sekunden) gilt <see cref="NormalXpLate2Factor"/> voll.</summary>
    public const float NormalXpLate2End = 660f;       // Minute 11

    /// <summary>XP-Anteil normaler Gegner nach <see cref="NormalXpLate2End"/>.</summary>
    public const float NormalXpLate2Factor = 0.65f;

    /// <summary>
    /// XP-Faktor fuer normale Gegner zur Laufzeit: 1 bis Minute 3, gleitend
    /// runter bis Minute 4, ab Minute 10 noch einmal gleitend runter bis
    /// Minute 11. Elites und Bosse bleiben unberuehrt.
    /// </summary>
    public static float NormalXpFactor(float runTime)
    {
        if (runTime >= NormalXpLate2Start)
            return Mathf.Lerp(NormalXpLateFactor, NormalXpLate2Factor,
                              Mathf.InverseLerp(NormalXpLate2Start, NormalXpLate2End, runTime));
        return Mathf.Lerp(1f, NormalXpLateFactor,
                          Mathf.InverseLerp(NormalXpFalloffStart, NormalXpFalloffEnd, runTime));
    }

    /// <summary>
    /// Miniboss, Boss und Tod: unverrueckbar, zaehlen nicht zum Druck, werden
    /// nicht nach vorn geholt. Elites gehoeren NICHT dazu - die sind nur
    /// starke Einzelgaenger.
    /// </summary>
    public static bool IsBossRole(EnemyRole role) =>
        role == EnemyRole.MiniBoss || role == EnemyRole.Boss || role == EnemyRole.DeathBoss;

    // ------------------------------------------------------------ Elite
    //
    // Elites kommen immer einzeln. Ein Burst mit einem Elite setzt genau
    // einen, egal wie viel Druck im Plan steht; im Pool und als Ringfueller
    // werden sie uebersprungen.

    public static bool IsElite(EnemyId id) => Get(id)?.Role == EnemyRole.Elite;

    /// <summary>Farbe des Leuchtens um Elite-Gegner.</summary>
    public static readonly Color EliteGlow = new Color(1f, 0.82f, 0.25f);

    // ------------------------------------------------------------ Pixelgroesse
    //
    // Jeder Gegner ist so gross, wie er gezeichnet ist - keine Skalierung im
    // Spiel. Dafuer stehen alle Gegnerbilder auf DERSELBEN Pixeldichte wie der
    // Hub und die Pixel-Perfect-Kamera (32). Sonst waere ein Pilz-Pixel groesser
    // als ein Boden-Pixel (Mixels). Die Gegner-Werkstatt stellt die Bilder beim Bauen
    // darauf ein. Passt die Groesse zum Hintergrund nicht, ist das hier die
    // eine Zahl dafuer - danach in der Werkstatt "Pixelgroesse vereinheitlichen".
    //
    // Ausnahme: Bosse (der Keks-Koenig ist ein 1024px-Bild mit eigener Groesse).

    public const float PixelsPerUnit = 32f;

    // ------------------------------------------------------------ Gewicht
    //
    // Frueher stand das Gewicht von Hand bei jedem Gegner - und passte bei
    // der Haelfte nicht zu seinen Werten (Muffin: 400 Leben, Gewicht 3; Eichel:
    // 30 Leben, Gewicht 2). Jetzt folgt es aus den Werten, mit dem Marshmello
    // als Mass: der war schon immer richtig und wiegt deshalb genau 1.
    //
    // Die Exponenten sind bewusst klein. Das Gewicht ist keine Lebenssumme -
    // die Waffen wachsen im Lauf mit, ein Gegner mit zehnfachem Leben ist
    // spaeter also nicht zehnmal so viel Arbeit. Mit diesen Werten heisst
    // doppelt so viel ...
    //   Leben  -> x1.23 Gewicht
    //   Schaden-> x1.15
    //   Tempo  -> x1.32  (schnelle Gegner holen den Spieler ein, der 4 laeuft)

    /// <summary>Der Marshmello, an dem alles haengt. Fest, damit eine Aenderung
    /// an seinem Katalogeintrag nicht still alle anderen Gewichte verschiebt.</summary>
    public const float RefHealth = 3f;
    public const float RefDamage = 2f;
    public const float RefSpeed  = 1.5f;

    public const float HealthExponent = 0.3f;
    public const float DamageExponent = 0.2f;
    public const float SpeedExponent  = 0.4f;

    /// <summary>
    /// Das Gewicht aus den Werten. Blocker zaehlen nie. Bosse bekommen eine
    /// Zahl, zaehlen im Director aber ohnehin nicht zum Druck.
    /// </summary>
    public static float AutoThreat(float health, float damage, float speed, EnemyRole role)
    {
        if (role == EnemyRole.Blocker) return 0f;

        float h = Mathf.Pow(Mathf.Max(1f, health) / RefHealth, HealthExponent);
        float d = Mathf.Pow(Mathf.Max(0.5f, damage) / RefDamage, DamageExponent);
        float s = Mathf.Pow(Mathf.Max(0.3f, speed) / RefSpeed, SpeedExponent);

        // Auf eine Nachkommastelle, damit die Werkstatt dieselbe Zahl zeigt,
        // mit der der Director rechnet.
        return Mathf.Max(0.1f, Mathf.Round(h * d * s * 10f) / 10f);
    }

    // ---------------------------------------------------------------- Zugriff

    private static Dictionary<EnemyId, EnemyDef> lookup;
    private static List<EnemyDef> all;

    public static IReadOnlyList<EnemyDef> All
    {
        get { EnsureBuilt(); return all; }
    }

    /// <summary>Der Eintrag zur Id, oder null wenn es keinen gibt.</summary>
    public static EnemyDef Get(EnemyId id)
    {
        if (id == EnemyId.None) return null;

        EnsureBuilt();
        return lookup.TryGetValue(id, out EnemyDef def) ? def : null;
    }

    public static bool Has(EnemyId id)
    {
        return Get(id) != null;
    }

    /// <summary>
    /// Das Gewicht im Druck-Budget. Kennt der Katalog den Gegner nicht,
    /// zaehlt er als 1 - so kann ein halb fertiger Eintrag den Director
    /// nicht zum Stillstand bringen.
    /// </summary>
    public static float Threat(EnemyId id)
    {
        EnemyDef def = Get(id);
        return def != null ? def.Threat : 1f;
    }

    private static void EnsureBuilt()
    {
        if (lookup != null) return;

        lookup = new Dictionary<EnemyId, EnemyDef>();
        all = new List<EnemyDef>();

        Build();
    }

    private static void Def(EnemyId id, string name, float health, float damage, float speed,
                            int exp, float pushTime, EnemyRole role,
                            EnemyFacing facing, string sheet, float fps,
                            float colliderRadius, Vector2 colliderOffset, float scale,
                            string prefab, bool archived = false,
                            int hopFirst = -1, int hopLast = -1)
    {
        var def = new EnemyDef(id, name, health, damage, speed, exp, pushTime,
                               role, facing, sheet, fps, colliderRadius, colliderOffset,
                               scale, prefab, archived, hopFirst, hopLast);
        lookup[id] = def;
        all.Add(def);
    }

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================
    private static void Build()
    {

        Def(EnemyId.Marshmello, "Marshmello",
            health: 3f, damage: 2f, speed: 1.5f, exp: 1, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/new/marshmallow_watschel.png", fps: 16f,
            colliderRadius: 0.45f, colliderOffset: new Vector2(0f, 0.5f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Marshmello.prefab");

        Def(EnemyId.EliteMarshmello, "Elite-Marshmello",
            health: 50f, damage: 6f, speed: 1.2f, exp: 120, pushTime: 0.25f,
            role: EnemyRole.Elite, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/new/marshmallow_elite_lauf.png", fps: 16f,
            colliderRadius: 0.5f, colliderOffset: new Vector2(0f, 0.69f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Elite_Marshmello.prefab");

        Def(EnemyId.EvilSlime, "Boeser Slime",
            health: 7f, damage: 4f, speed: 1.5f, exp: 3, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/boeser_slime_kriech.png", fps: 16f,
            colliderRadius: 0.3f, colliderOffset: new Vector2(0f, -0.18f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Evil_Slim.prefab");

        Def(EnemyId.EliteFluegdolch, "Elite-Fluegdolch",
            health: 400f, damage: 7f, speed: 1.25f, exp: 330, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/fledermaus_flug.png", fps: 12f,
            colliderRadius: 0.55f, colliderOffset: new Vector2(0f, -0.12f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Elite-Fluegdolch.prefab");

        Def(EnemyId.KeksKoenig, "Keks-Koenig",
            health: 7500f, damage: 38.7f, speed: 4f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/boss/kekskoenig_lauf.png", fps: 12f,
            colliderRadius: 1.95f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/KecksKoenig.prefab");

        Def(EnemyId.Eichel, "Eichel",
            health: 45f, damage: 5f, speed: 1.3f, exp: 18, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eichel.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Eichel.prefab");

        Def(EnemyId.Muffin, "Muffin",
            health: 45f, damage: 5f, speed: 1.3f, exp: 18, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/fin_muffin.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/muffin.prefab");

        Def(EnemyId.Fliegenpilz, "Fliegenpilz",
            health: 3.8f, damage: 2f, speed: 1.5f, exp: 1, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/pilz1.png", fps: 6f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Fliegenpilz.prefab");

        Def(EnemyId.EliteFliegenpilz, "Elite-Fliegenpilz",
            health: 200f, damage: 5f, speed: 1.25f, exp: 220, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/elite/fliegenpilz_elite_hop.png", fps: 12f,
            colliderRadius: 0.45f, colliderOffset: new Vector2(0f, -0.12f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Pilzkoenig.prefab", hopFirst: 3, hopLast: 9);

        Def(EnemyId.Fluegeldolch, "Fluegeldolch",
            health: 14f, damage: 3f, speed: 2.1f, exp: 6, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/new/image.png", fps: 12f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Fluegeldolch.prefab");

        Def(EnemyId.Kirschslime, "Kirschslime",
            health: 9f, damage: 3f, speed: 1.6f, exp: 3, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/new_slime.png", fps: 10f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Kirschslime.prefab", hopFirst: 4, hopLast: 6);

        Def(EnemyId.Milchpanzer, "Milchpanzer",
            health: 4500f, damage: 14f, speed: 1.4f, exp: 700, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/miniboss/milchpanzer_fahrt.png", fps: 12f,
            colliderRadius: 1.2f, colliderOffset: new Vector2(0f, 1.25f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Mini_Boss/Milchpanzer.prefab");

        Def(EnemyId.Honey, "Honey",
            health: 450f, damage: 10f, speed: 1.4f, exp: 120, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/honey.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Honey.prefab");

        Def(EnemyId.WeisseMessermaus, "Weisse Messermaus",
            health: 15f, damage: 3f, speed: 2f, exp: 5, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/MausMesser.png", fps: 6f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/WeisseMessermaus.prefab");

        Def(EnemyId.Glutwurz, "Glutwurz",
            health: 7500f, damage: 40f, speed: 2.2f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/baumboss_walk.png", fps: 12f,
            colliderRadius: 0.95f, colliderOffset: new Vector2(0f, 1.25f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/Glutwurz.prefab");

        Def(EnemyId.Schleimkoenig, "Schleimkoenig",
            health: 3600f, damage: 12f, speed: 1.6f, exp: 600, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/miniboss/schleimkoenig_hop.png", fps: 12f,
            colliderRadius: 1.15f, colliderOffset: new Vector2(0f, 0.85f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Mini_Boss/Schleimkoenig.prefab");

        Def(EnemyId.TodesRamen, "Todes-Ramen",
            health: 300000f, damage: 101f, speed: 11f, exp: 0, pushTime: 0f,
            role: EnemyRole.DeathBoss, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/V2_fin_ramen.png", fps: 2f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/TodesRamen.prefab");

        Def(EnemyId.Verkohlter, "Der Verkohlte",
            health: 9000f, damage: 25f, speed: 1.9f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/verkohlter_walk.png", fps: 12f,
            colliderRadius: 0.85f, colliderOffset: new Vector2(0f, 1f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/Verkohlter.prefab");

        Def(EnemyId.VerkohlterTod, "Der Verkohlte (Tod)",
            health: 300000f, damage: 101f, speed: 3.2f, exp: 0, pushTime: 0f,
            role: EnemyRole.DeathBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/verkohlter_walk.png", fps: 12f,
            colliderRadius: 0.85f, colliderOffset: new Vector2(0f, 1f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/VerkohlterTod.prefab");

        Def(EnemyId.Pancake, "Pfannkuchen",
            health: 600f, damage: 18f, speed: 1.75f, exp: 100, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/pfannkuchen_hop.png", fps: 16f,
            colliderRadius: 0.72f, colliderOffset: new Vector2(0f, 0.72f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/pancake.prefab");

        Def(EnemyId.Minzkugel, "Minzkugel",
            health: 4f, damage: 2f, speed: 1.5f, exp: 1, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/minzkugel_gleit.png", fps: 14f,
            colliderRadius: 0.34f, colliderOffset: new Vector2(0f, 0.38f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Minzkugel.prefab");

        Def(EnemyId.Raketeneis, "Raketeneis",
            health: 12f, damage: 3f, speed: 2f, exp: 4, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/raketeneis_hops.png", fps: 14f,
            colliderRadius: 0.3f, colliderOffset: new Vector2(0f, 0.72f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Raketeneis.prefab", hopFirst: 3, hopLast: 9);

        Def(EnemyId.Softi, "Softi",
            health: 280f, damage: 6f, speed: 1.2f, exp: 260, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/eis/softi_wirbel.png", fps: 16f,
            colliderRadius: 0.5f, colliderOffset: new Vector2(0f, 0.95f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Softi.prefab");

        Def(EnemyId.Eiskaiser, "Eiskaiser",
            health: 10500f, damage: 30f, speed: 1.7f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/eiskaiser_walk_s.png", fps: 12f,
            colliderRadius: 1.0f, colliderOffset: new Vector2(0f, 1.15f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/Eiskaiser.prefab");

        Def(EnemyId.Lawinenkugel, "Lawinenkugel",
            health: 200f, damage: 14f, speed: 4.4f, exp: 0, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/eiskaiser_kugel.png", fps: 12f,
            colliderRadius: 0.2f, colliderOffset: new Vector2(0f, 0.22f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/Lawinenkugel.prefab");

        Def(EnemyId.Squiddy, "Das Gespenst",
            health: 10800f, damage: 30f, speed: 1.9f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/squiddy_geist_schweben.png", fps: 12f,
            colliderRadius: 1.2f, colliderOffset: new Vector2(0f, 2.05f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/Squiddy.prefab");

        Def(EnemyId.QuallenBaby, "Baby-Squiddy",
            health: 70f, damage: 10f, speed: 2.4f, exp: 6, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/squiddy_baby.png", fps: 12f,
            colliderRadius: 0.32f, colliderOffset: new Vector2(0f, 0.55f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/QuallenBaby.prefab");

        Def(EnemyId.EisSandwich, "Eiskeks-Sandwich",
            health: 18f, damage: 4f, speed: 2.2f, exp: 6, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/sandwich_schnapp.png", fps: 14f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.36f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/EisSandwich.prefab", hopFirst: 3, hopLast: 9);

        Def(EnemyId.Zuckerwolke, "Zuckerwoelkchen",
            health: 7f, damage: 3f, speed: 2.6f, exp: 3, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/zuckerwolke_schweben.png", fps: 12f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.66f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Zuckerwolke.prefab");

        Def(EnemyId.KokosYeti, "Kokos-Yeti",
            health: 900f, damage: 14f, speed: 1.3f, exp: 200, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/kokosyeti_stapf.png", fps: 12f,
            colliderRadius: 0.5f, colliderOffset: new Vector2(0f, 0.62f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/KokosYeti.prefab");

        Def(EnemyId.Kakaotasse, "Kakaotasse",
            health: 30f, damage: 5f, speed: 1.7f, exp: 8, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/kakaotasse_watschel.png", fps: 12f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.42f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Kakaotasse.prefab");

        Def(EnemyId.Puddingrobbe, "Puddingrobbe",
            health: 24f, damage: 4f, speed: 2.3f, exp: 6, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/puddingrobbe_rutsch.png", fps: 12f,
            colliderRadius: 0.4f, colliderOffset: new Vector2(0f, 0.3f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Puddingrobbe.prefab");

        Def(EnemyId.Plaetzchen, "Sternplaetzchen",
            health: 8f, damage: 3f, speed: 2.8f, exp: 3, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/plaetzchen_roll.png", fps: 16f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.4f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Plaetzchen.prefab");

        // ---------------------------------------------------------- Welle 4
        // Kueche (47-52), Wald (53-56), Eiswelt (57-58).

        Def(EnemyId.Wurstdackel, "Wurst-Dackel",
            health: 10f, damage: 3f, speed: 2.6f, exp: 4, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/kueche/dackel_trab.png", fps: 16f,
            colliderRadius: 0.31f, colliderOffset: new Vector2(0f, 0.31f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Wurstdackel.prefab");

        Def(EnemyId.Spiegelei, "Spiegelei",
            health: 14f, damage: 3f, speed: 1.3f, exp: 5, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/kueche/spiegelei_kriech.png", fps: 12f,
            colliderRadius: 0.28f, colliderOffset: new Vector2(0f, 0.22f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Spiegelei.prefab");

        Def(EnemyId.Fleischball, "Spaghetti-Monster",
            health: 120f, damage: 8f, speed: 1.5f, exp: 45, pushTime: 0.1f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/kueche/fleischball_stakse.png", fps: 14f,
            colliderRadius: 0.43f, colliderOffset: new Vector2(0f, 0.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Fleischball.prefab");

        Def(EnemyId.Erbsenschote, "Erbsenschote",
            health: 30f, damage: 4f, speed: 1.4f, exp: 10, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/kueche/erbsen_kriech.png", fps: 12f,
            colliderRadius: 0.26f, colliderOffset: new Vector2(0f, 0.27f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Erbsenschote.prefab");

        Def(EnemyId.Kaesekeil, "Kaesekeil",
            health: 60f, damage: 6f, speed: 1.2f, exp: 22, pushTime: 0.15f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/kueche/kaese_watschel.png", fps: 12f,
            colliderRadius: 0.38f, colliderOffset: new Vector2(0f, 0.38f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Kaesekeil.prefab");

        Def(EnemyId.Popcorn, "Popcorn",
            health: 4f, damage: 2f, speed: 2.4f, exp: 2, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/kueche/popcorn_hops.png", fps: 14f,
            colliderRadius: 0.26f, colliderOffset: new Vector2(0f, 0.41f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Popcorn.prefab", hopFirst: 3, hopLast: 9);

        Def(EnemyId.Maronenigel, "Maronen-Igel",
            health: 20f, damage: 4f, speed: 1.9f, exp: 7, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/wald/maronenigel_tripp.png", fps: 16f,
            colliderRadius: 0.34f, colliderOffset: new Vector2(0f, 0.34f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Maronenigel.prefab");

        Def(EnemyId.Beerenspinne, "Beerenspinne",
            health: 16f, damage: 3f, speed: 2.5f, exp: 6, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/wald/beerenspinne_stakse.png", fps: 14f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.36f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Beerenspinne.prefab");

        Def(EnemyId.Walnusskrabbe, "Walnuss-Krabbe",
            health: 70f, damage: 6f, speed: 1.4f, exp: 26, pushTime: 0.15f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/wald/walnusskrabbe_krabbel.png", fps: 14f,
            colliderRadius: 0.4f, colliderOffset: new Vector2(0f, 0.4f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Walnusskrabbe.prefab");

        Def(EnemyId.Honigbaer, "Honigbaerchen",
            health: 35f, damage: 5f, speed: 1.8f, exp: 12, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/wald/honigbaer_watschel.png", fps: 14f,
            colliderRadius: 0.34f, colliderOffset: new Vector2(0f, 0.42f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Honigbaer.prefab");

        Def(EnemyId.Eiswuerfelbaer, "Eiswuerfel-Baer",
            health: 45f, damage: 5f, speed: 1.4f, exp: 14, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/eis/eiswuerfel_stapf.png", fps: 12f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.39f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Eiswuerfelbaer.prefab");

        Def(EnemyId.Minzschnecke, "Pfefferminz-Schnecke",
            health: 20f, damage: 3f, speed: 1.6f, exp: 6, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/eis/minzschnecke_gleit.png", fps: 12f,
            colliderRadius: 0.35f, colliderOffset: new Vector2(0f, 0.34f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Minzschnecke.prefab");

        Def(EnemyId.Kerzentorte, "Kerzentorte",
            health: 380f, damage: 7f, speed: 1.15f, exp: 300, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/kueche/kerzentorte_stapf.png", fps: 14f,
            colliderRadius: 0.55f, colliderOffset: new Vector2(0f, 0.55f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Kerzentorte.prefab");

        Def(EnemyId.FuerstPueckler, "Fuerst Pueckler",
            health: 450f, damage: 7f, speed: 1.1f, exp: 330, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/eis/pueckler_schreit.png", fps: 14f,
            colliderRadius: 0.56f, colliderOffset: new Vector2(0f, 0.6f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/FuerstPueckler.prefab");

        Def(EnemyId.Kuerbiswicht, "Kuerbiswicht",
            health: 40f, damage: 5f, speed: 1.6f, exp: 12, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/kuerbiswicht_watschel.png", fps: 14f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.36f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Kuerbiswicht.prefab");

        Def(EnemyId.Gluehbirne, "Gluehbirne",
            health: 30f, damage: 5f, speed: 1.9f, exp: 10, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/gluehbirne_stakse.png", fps: 14f,
            colliderRadius: 0.3f, colliderOffset: new Vector2(0f, 0.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Gluehbirne.prefab");

        Def(EnemyId.Leuchtpilz, "Leuchtpilz",
            health: 55f, damage: 6f, speed: 1.4f, exp: 16, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/leuchtpilz_tapp.png", fps: 12f,
            colliderRadius: 0.38f, colliderOffset: new Vector2(0f, 0.4f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Leuchtpilz.prefab");

        Def(EnemyId.Baisergeist, "Baisergeist",
            health: 28f, damage: 5f, speed: 2f, exp: 10, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/baisergeist_schweb.png", fps: 12f,
            colliderRadius: 0.32f, colliderOffset: new Vector2(0f, 0.55f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Baisergeist.prefab");

        Def(EnemyId.Lakritzkater, "Lakritzkater",
            health: 35f, damage: 6f, speed: 2.3f, exp: 12, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/lakritzkater_trab.png", fps: 14f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.35f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Lakritzkater.prefab");

        Def(EnemyId.Lebkuchenzombie, "Lebkuchen-Zombie",
            health: 110f, damage: 8f, speed: 1.2f, exp: 34, pushTime: 0.15f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/lebkuchenzombie_schlurf.png", fps: 12f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Lebkuchenzombie.prefab");

        Def(EnemyId.Gluehwurm, "Saurer Gluehwurm",
            health: 22f, damage: 4f, speed: 1.7f, exp: 7, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/gluehwurm_spann.png", fps: 12f,
            colliderRadius: 0.32f, colliderOffset: new Vector2(0f, 0.22f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Gluehwurm.prefab");

        Def(EnemyId.Windbeuteleule, "Windbeutel-Eule",
            health: 60f, damage: 6f, speed: 1.4f, exp: 18, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/windbeuteleule_watschel.png", fps: 14f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.42f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Windbeuteleule.prefab");

        Def(EnemyId.Flambierkirsche, "Flambierkirsche",
            health: 14f, damage: 4f, speed: 2.7f, exp: 5, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/geist/flambierkirsche_flitz.png", fps: 16f,
            colliderRadius: 0.26f, colliderOffset: new Vector2(0f, 0.32f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Flambierkirsche.prefab");

        Def(EnemyId.Feuerschote, "Feuerschote",
            health: 40f, damage: 6f, speed: 2f, exp: 13, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/feuerschote_marsch.png", fps: 14f,
            colliderRadius: 0.3f, colliderOffset: new Vector2(0f, 0.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Feuerschote.prefab");

        Def(EnemyId.Lavakuchen, "Lavakuchen",
            health: 55f, damage: 6f, speed: 1.4f, exp: 16, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/lavakuchen_watschel.png", fps: 12f,
            colliderRadius: 0.36f, colliderOffset: new Vector2(0f, 0.3f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Lavakuchen.prefab");

        Def(EnemyId.Takoyaki, "Takoyaki",
            health: 30f, damage: 5f, speed: 2.4f, exp: 11, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/takoyaki_trippel.png", fps: 16f,
            colliderRadius: 0.32f, colliderOffset: new Vector2(0f, 0.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Takoyaki.prefab");

        Def(EnemyId.Kaesefondue, "Kaesefondue",
            health: 70f, damage: 6f, speed: 1.4f, exp: 24, pushTime: 0.15f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/fondue_stapf.png", fps: 12f,
            colliderRadius: 0.42f, colliderOffset: new Vector2(0f, 0.7f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Kaesefondue.prefab");

        Def(EnemyId.Schaschlik, "Schaschlik",
            health: 45f, damage: 6f, speed: 1.9f, exp: 14, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/schaschlik_hops.png", fps: 14f,
            colliderRadius: 0.3f, colliderOffset: new Vector2(0f, 0.75f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Schaschlik.prefab", hopFirst: 3, hopLast: 9);

        Def(EnemyId.Wasabiklecks, "Wasabi-Klecks",
            health: 50f, damage: 5f, speed: 1.3f, exp: 15, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/wasabi_kriech.png", fps: 12f,
            colliderRadius: 0.38f, colliderOffset: new Vector2(0f, 0.3f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Wasabiklecks.prefab");

        Def(EnemyId.Pizzastueck, "Pizzastueck",
            health: 45f, damage: 6f, speed: 1.7f, exp: 14, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/pizza_stapf.png", fps: 14f,
            colliderRadius: 0.34f, colliderOffset: new Vector2(0f, 0.5f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Pizzastueck.prefab");

        Def(EnemyId.Drachenfrucht, "Drachenfrucht",
            health: 40f, damage: 6f, speed: 2.1f, exp: 14, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/drachenfrucht_stapf.png", fps: 14f,
            colliderRadius: 0.32f, colliderOffset: new Vector2(0f, 0.48f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Drachenfrucht.prefab");

        Def(EnemyId.Brotgolem, "Ofenbrot-Golem",
            health: 160f, damage: 9f, speed: 1.1f, exp: 45, pushTime: 0.1f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/brotgolem_stapf.png", fps: 12f,
            colliderRadius: 0.55f, colliderOffset: new Vector2(0f, 0.75f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Brotgolem.prefab");

        Def(EnemyId.Knisterbrause, "Knisterbrause",
            health: 14f, damage: 4f, speed: 2.8f, exp: 5, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/vulkan/knisterbrause_huepf.png", fps: 16f,
            colliderRadius: 0.24f, colliderOffset: new Vector2(0f, 0.25f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Knisterbrause.prefab");

        // ------------------------------------------------- Zwischenbosse (Welten 3-5)

        Def(EnemyId.Stracciatella, "Stracciatella",
            health: 4800f, damage: 14f, speed: 1.5f, exp: 750, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/miniboss/stracciatella_gleit.png", fps: 12f,
            colliderRadius: 1.0f, colliderOffset: new Vector2(0f, 1.15f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Mini_Boss/Stracciatella.prefab");

        Def(EnemyId.Knusperhaus, "Knusperhaus",
            health: 5400f, damage: 16f, speed: 1.7f, exp: 800, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/miniboss/knusperhaus_lauf.png", fps: 12f,
            colliderRadius: 1.05f, colliderOffset: new Vector2(0f, 1.45f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Mini_Boss/Knusperhaus.prefab");

        Def(EnemyId.Ofenkartoffel, "Ofenkartoffel",
            health: 6300f, damage: 18f, speed: 1.45f, exp: 900, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/miniboss/ofenkartoffel_lauf.png", fps: 12f,
            colliderRadius: 1.0f, colliderOffset: new Vector2(0f, 1.0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Mini_Boss/Ofenkartoffel.prefab");

        Def(EnemyId.Spuksuppe, "Spuksuppe",
            health: 480f, damage: 8f, speed: 1.1f, exp: 360, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/geist/spuksuppe_watschel.png", fps: 14f,
            colliderRadius: 0.52f, colliderOffset: new Vector2(0f, 0.5f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Spuksuppe.prefab");

        Def(EnemyId.Wackelgeist, "Wackelgeist",
            health: 520f, damage: 8f, speed: 1.15f, exp: 390, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/geist/wackelgeist_boing.png", fps: 14f,
            colliderRadius: 0.48f, colliderOffset: new Vector2(0f, 0.42f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Wackelgeist.prefab");

        Def(EnemyId.Feuertaco, "Feuertaco",
            health: 560f, damage: 9f, speed: 1.25f, exp: 420, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/vulkan/feuertaco_marsch.png", fps: 14f,
            colliderRadius: 0.5f, colliderOffset: new Vector2(0f, 0.48f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Feuertaco.prefab");

        Def(EnemyId.CremeBrulee, "Creme Brulee",
            health: 620f, damage: 9f, speed: 1.15f, exp: 460, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/vulkan/cremebrulee_trippel.png", fps: 14f,
            colliderRadius: 0.44f, colliderOffset: new Vector2(0f, 0.38f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/CremeBrulee.prefab");

        Def(EnemyId.Baumkuchen, "Baumkuchen-Stumpf",
            health: 600f, damage: 18f, speed: 1.75f, exp: 300, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/wald/baumkuchen_stapf.png", fps: 12f,
            colliderRadius: 0.55f, colliderOffset: new Vector2(0f, 0.62f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Baumkuchen.prefab");

        // ---------------------------------------------------------- Archiv
        //
        // Weggelegt, nicht geloescht. In der Werkstatt unter "Archiv"
        // zurueckholen.

        Def(EnemyId.MausMitMesser, "Maus mit Messer",
            health: 20f, damage: 5f, speed: 2f, exp: 4, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_MausMesser1.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/MausMesser.prefab", archived: true);

        Def(EnemyId.MiniMilch, "Mini-Milch",
            health: 90f, damage: 7f, speed: 2.5f, exp: 70, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_saure_milch.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/mini_milch.prefab", archived: true);

        Def(EnemyId.SaureMilch, "Saure Milch",
            health: 190f, damage: 10f, speed: 3f, exp: 187, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_saure_milch.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/saure_milch.prefab", archived: true);

        Def(EnemyId.Suppe, "Ramen",
            health: 1111f, damage: 25f, speed: 0.9f, exp: 555, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_ramen.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/ramen.prefab", archived: true);

        Def(EnemyId.Fetti, "Fetti",
            health: 750f, damage: 20f, speed: 1.2f, exp: 450, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fett.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/fetti.prefab", archived: true);

        Def(EnemyId.Slime, "Slime (10 Farben)",
            health: 150f, damage: 6f, speed: 2f, exp: 100, pushTime: 0.1f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_slime_new.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/Blue_Slime_Variants/Slime_Variant_Blue.prefab", archived: true);

        Def(EnemyId.Blocker, "Kaefig-Wand",
            health: 500f, damage: 1f, speed: 0.03f, exp: 0, pushTime: 0f,
            role: EnemyRole.Blocker, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/Slimes.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/Blue_Slime_Variants/Blocker.prefab", archived: true);

        Def(EnemyId.MesserMaus1, "Messermaus (Welle 1)",
            health: 5000f, damage: 10f, speed: 2.5f, exp: 0, pushTime: 0f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/MesserMaus.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/MiniBoss_MesserMaus.prefab", archived: true);

        Def(EnemyId.MesserMaus2, "Messermaus (Welle 2)",
            health: 10000f, damage: 20f, speed: 1.5f, exp: 0, pushTime: 0f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/MesserMaus.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/MesserMaus_Wave2.prefab", archived: true);
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
