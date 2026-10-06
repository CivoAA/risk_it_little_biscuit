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
            sheet: "Assets/Art/Gegner/freeze_marshmallow.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Marshmello.prefab");

        Def(EnemyId.EliteMarshmello, "Elite-Marshmello",
            health: 50f, damage: 6f, speed: 1.2f, exp: 20, pushTime: 0.25f,
            role: EnemyRole.Elite, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/fin_marshmallow.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Elite_Marshmello.prefab");

        Def(EnemyId.EvilSlime, "Boeser Slime",
            health: 7f, damage: 4f, speed: 1.5f, exp: 3, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_evil_slime.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Evil_Slim.prefab");

        Def(EnemyId.EliteFluegdolch, "Elite-Fluegdolch",
            health: 400f, damage: 7f, speed: 1.25f, exp: 450, pushTime: 0.3f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/boss/fledermaus_flug.png", fps: 12f,
            colliderRadius: 0.55f, colliderOffset: new Vector2(0f, -0.12f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Elite_Gegner/Elite-Fluegdolch.prefab");

        Def(EnemyId.KeksKoenig, "Keks-Koenig",
            health: 5000f, damage: 38.7f, speed: 4f, exp: 0, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_der_konig_des_spielfelds.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
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
            health: 200f, damage: 5f, speed: 1.25f, exp: 300, pushTime: 0.3f,
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
            health: 900f, damage: 14f, speed: 1.5f, exp: 650, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesRight,
            sheet: "Assets/Art/Gegner/new/wirklich_saure_milch_1.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Gegner/Milchpanzer.prefab");

        Def(EnemyId.Honey, "Honey",
            health: 450f, damage: 10f, speed: 1.4f, exp: 350, pushTime: 0f,
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
            health: 2400f, damage: 12f, speed: 1.6f, exp: 600, pushTime: 0f,
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
            health: 6000f, damage: 25f, speed: 1.9f, exp: 0, pushTime: 0f,
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

        Def(EnemyId.Pancake, "Pfannkuchen",
            health: 600f, damage: 18f, speed: 1.75f, exp: 300, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_pencake.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Archiv/pancake.prefab", archived: true);

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
