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
public enum EnemyId
{
    None = 0,

    // Grundgegner
    Marshmello,
    EliteMarshmello,
    EvilSlime,
    MausMitMesser,
    MiniMilch,
    SaureMilch,
    Muffin,
    Suppe,
    Pancake,
    Fetti,

    /// <summary>Eine der zehn Slime-Varianten, zufaellig gewaehlt.</summary>
    Slime,

    // Besonderes
    Blocker,              // bewegt sich kaum - das ist der Kaefig, nicht der Gegner
    MiniBossMarshmello,
    MesserMaus1,
    MesserMaus2,
    KeksKoenig,

    // Neu (Gegner-Werkstatt)
    Eichel,
    Fliegenpilz,
    Fluegeldolch,
    Kirschslime,
    Milchpanzer,
    WeisseMessermaus,

    // Wald (World3): Elite- und Miniboss-Fassungen der neuen Gegner.
    // Gleiche Bilder, groesser gestellt - so wie der Miniboss-Marshmello in
    // der Kueche auch nur ein groesserer Marshmello ist.
    Sturmeichel,
    Dolchschwarm,
    Pilzkoenig,
    Rattenkoenigin,
    Milchkoloss,
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

    /// <summary>Wie Normal, nur dicker. Zaehlt auf dieselben Erfolge.</summary>
    Elite,

    /// <summary>Laesst eine Truhe fallen, raeumt den Kaefig, gibt 1 Seele.</summary>
    MiniBoss,

    /// <summary>Der Keks-Koenig: laeuft nicht selbst, ruft danach den Tod.</summary>
    Boss,

    /// <summary>Der Tod. Zieht alle XP an, gibt 50 Seelen und eine Truhe.</summary>
    DeathBoss,

    /// <summary>Kaefig-Wand. Zaehlt nicht zum Druck und gibt nichts.</summary>
    Blocker,
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
    /// Ein Fetti zaehlt wie acht Marshmellos - dadurch bleibt der Druck
    /// gleich, egal aus welchen Gegnern eine Phase besteht.
    /// </summary>
    public readonly float Threat;

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

    public EnemyDef(EnemyId id, string name, float health, float damage, float speed, int exp,
                    float threat, float pushTime, EnemyRole role, EnemyFacing facing,
                    string sheet, float fps, float colliderRadius, Vector2 colliderOffset,
                    float scale, string prefab)
    {
        Id             = id;
        Name           = string.IsNullOrEmpty(name) ? id.ToString() : name;
        Health         = Mathf.Max(1f, health);
        Damage         = Mathf.Max(0f, damage);
        Speed          = speed;
        Exp            = Mathf.Max(0, exp);
        Threat         = Mathf.Max(0f, threat);
        PushTime       = Mathf.Max(0f, pushTime);
        Role           = role;
        Facing         = facing;
        Sheet          = sheet ?? "";
        Fps            = Mathf.Clamp(fps, 0.5f, 60f);
        ColliderRadius = Mathf.Max(0f, colliderRadius);
        ColliderOffset = colliderOffset;
        Scale          = scale <= 0f ? 1f : scale;
        Prefab         = prefab ?? "";
    }

    /// <summary>Bosse und Minibosse lassen sich nicht ziehen oder wegschieben.</summary>
    public bool IsBoss
    {
        get { return Role == EnemyRole.MiniBoss || Role == EnemyRole.Boss || Role == EnemyRole.DeathBoss; }
    }

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

    /// <summary>Seelen ("Cookie Souls") je Rolle.</summary>
    public static int Souls(EnemyRole role)
    {
        switch (role)
        {
            case EnemyRole.MiniBoss:  return 1;
            case EnemyRole.Boss:      return 10;
            case EnemyRole.DeathBoss: return 50;
            default:                  return 0;
        }
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
                            int exp, float threat, float pushTime, EnemyRole role,
                            EnemyFacing facing, string sheet, float fps,
                            float colliderRadius, Vector2 colliderOffset, float scale,
                            string prefab)
    {
        var def = new EnemyDef(id, name, health, damage, speed, exp, threat, pushTime,
                               role, facing, sheet, fps, colliderRadius, colliderOffset,
                               scale, prefab);
        lookup[id] = def;
        all.Add(def);
    }

    // ================================================================
    // WERKSTATT-ANFANG - alles hier drin schreibt das Tool neu.
    // ================================================================
    private static void Build()
    {
        // ------------------------------------------------------ Grundgegner
        //
        // Werte und Prefab-Pfade stammen aus den Prefabs, die der SpawnCatalog
        // in GameCore tatsaechlich benutzt (Wave1/Wave2/Wave3) - nicht aus den
        // gleichnamigen Altlasten direkt unter Assets/Prefabs/Enemy. Die sehen
        // gleich aus, haben aber andere Zahlen und werden nirgends gespawnt.

        Def(EnemyId.Marshmello, "Marshmello",
            health: 3f, damage: 2f, speed: 1.5f, exp: 1, threat: 1f, pushTime: 0.25f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/freeze_marshmallow.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/Marshmello.prefab");

        Def(EnemyId.EliteMarshmello, "Elite-Marshmello",
            health: 50f, damage: 6f, speed: 1.2f, exp: 20, threat: 4f, pushTime: 0.25f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_marshmallow.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/Elite_Marshmello.prefab");

        Def(EnemyId.EvilSlime, "Boeser Slime",
            health: 7f, damage: 4f, speed: 1.5f, exp: 3, threat: 1.5f, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_evil_slime.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/Evil_Slim.prefab");

        Def(EnemyId.MausMitMesser, "Maus mit Messer",
            health: 20f, damage: 5f, speed: 2f, exp: 4, threat: 3f, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_MausMesser1.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/MausMesser.prefab");

        Def(EnemyId.MiniMilch, "Mini-Milch",
            health: 90f, damage: 7f, speed: 2.5f, exp: 70, threat: 2f, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_saure_milch.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave2/mini_milch.prefab");

        Def(EnemyId.SaureMilch, "Saure Milch",
            health: 190f, damage: 10f, speed: 3f, exp: 187, threat: 4f, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_saure_milch.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave2/saure_milch.prefab");

        Def(EnemyId.Muffin, "Muffin",
            health: 400f, damage: 12f, speed: 1.75f, exp: 200, threat: 3f, pushTime: 0.01f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_muffin.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave2/muffin.prefab");

        Def(EnemyId.Suppe, "Ramen",
            health: 1111f, damage: 25f, speed: 0.9f, exp: 555, threat: 5f, pushTime: 0f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_ramen.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave3/ramen.prefab");

        Def(EnemyId.Pancake, "Pfannkuchen",
            health: 600f, damage: 18f, speed: 1.75f, exp: 300, threat: 4f, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/fin_pencake.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave3/pancake.prefab");

        Def(EnemyId.Fetti, "Fetti",
            health: 750f, damage: 20f, speed: 1.2f, exp: 450, threat: 8f, pushTime: 0f,
            role: EnemyRole.Elite, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fett.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave3/fetti.prefab");

        // Die zehn Farbvarianten haengen als eigene Liste am SpawnCatalog in
        // GameCore - dieser Eintrag beschreibt sie nur gemeinsam. Das Prefab
        // hier ist die blaue Variante als Stellvertreter.
        Def(EnemyId.Slime, "Slime (10 Farben)",
            health: 150f, damage: 6f, speed: 2f, exp: 100, threat: 2f, pushTime: 0.1f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_slime_new.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Blue_Slime_Variants/Slime_Variant_Blue.prefab");

        // ------------------------------------------------------- Besonderes

        Def(EnemyId.Blocker, "Kaefig-Wand",
            health: 500f, damage: 1f, speed: 0.03f, exp: 0, threat: 0f, pushTime: 0f,
            role: EnemyRole.Blocker, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/Slimes.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Blue_Slime_Variants/Blocker.prefab");

        // ACHTUNG: heisst Miniboss, hat am Prefab aber MiniBoss = 0. Er gibt
        // also weder Truhe noch Seele und zaehlt auf keinen Miniboss-Erfolg.
        // Hier steht bewusst, was das Spiel HEUTE tut. Wer das aendern will,
        // stellt die Rolle in der Werkstatt auf MiniBoss - dann kommt beides
        // dazu, und der Kaefig eines Encirclements loest sich wieder auf.
        Def(EnemyId.MiniBossMarshmello, "Miniboss Marshmello",
            health: 200f, damage: 5f, speed: 1.25f, exp: 300, threat: 15f, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/freeze_marshmallow_miniboss.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/MiniBoss_Marshmello.prefab");

        Def(EnemyId.MesserMaus1, "Messermaus (Welle 1)",
            health: 5000f, damage: 10f, speed: 2.5f, exp: 0, threat: 40f, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/MesserMaus.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave1/MiniBoss_MesserMaus.prefab");

        Def(EnemyId.MesserMaus2, "Messermaus (Welle 2)",
            health: 10000f, damage: 20f, speed: 1.5f, exp: 0, threat: 40f, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/MesserMaus.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Wave2/MesserMaus_Wave2.prefab");

        Def(EnemyId.KeksKoenig, "Keks-Koenig",
            health: 5000f, damage: 38.7f, speed: 4f, exp: 0, threat: 100f, pushTime: 0f,
            role: EnemyRole.Boss, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/fin_der_konig_des_spielfelds.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Boss/KecksKoenig.prefab");

        // ------------------------------------------------------------- Neu
        //
        // Startwerte, hergeleitet aus der Kurve oben. Diese sechs sind von der
        // Werkstatt gebaut und laufen als einzige schon ueber den Katalog.

        Def(EnemyId.Eichel, "Eichel",
            health: 30f, damage: 3f, speed: 1.9f, exp: 25, threat: 2f, pushTime: 0.2f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/eichel.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/Eichel.prefab");

        Def(EnemyId.Fliegenpilz, "Fliegenpilz",
            health: 60f, damage: 6f, speed: 1.1f, exp: 55, threat: 3f, pushTime: 0.15f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/pilz.png", fps: 6f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/Fliegenpilz.prefab");

        Def(EnemyId.Fluegeldolch, "Fluegeldolch",
            health: 18f, damage: 5f, speed: 3.6f, exp: 30, threat: 3f, pushTime: 0.05f,
            role: EnemyRole.Normal, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/image.png", fps: 12f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/Fluegeldolch.prefab");

        Def(EnemyId.Kirschslime, "Kirschslime",
            health: 120f, damage: 5f, speed: 2.1f, exp: 90, threat: 2.5f, pushTime: 0.1f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/new_slime.png", fps: 10f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/Kirschslime.prefab");

        Def(EnemyId.Milchpanzer, "Milchpanzer",
            health: 450f, damage: 10f, speed: 1.4f, exp: 350, threat: 6f, pushTime: 0f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/wirklich_saure_milch_1.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/Milchpanzer.prefab");

        Def(EnemyId.WeisseMessermaus, "Weisse Messermaus",
            health: 8f, damage: 2f, speed: 2.6f, exp: 6, threat: 1.5f, pushTime: 0.3f,
            role: EnemyRole.Normal, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/MausMesser.png", fps: 6f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1f,
            prefab: "Assets/Prefabs/Enemy/Neu/WeisseMessermaus.prefab");

        // ------------------------------------------- Wald: Elite + Miniboss
        //
        // Dieselben Bilder, groesser und zaeher. Wer eigene Bilder dafuer hat,
        // haengt sie in der Gegner-Werkstatt ein und baut das Prefab neu - an
        // den Wellenplaenen aendert sich dadurch nichts.

        Def(EnemyId.Sturmeichel, "Sturmeichel (Elite)",
            health: 140f, damage: 9f, speed: 2.1f, exp: 110, threat: 5f, pushTime: 0.15f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/eichel.png", fps: 10f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1.4f,
            prefab: "Assets/Prefabs/Enemy/Neu/Sturmeichel.prefab");

        Def(EnemyId.Dolchschwarm, "Dolchschwarm (Elite)",
            health: 70f, damage: 9f, speed: 4f, exp: 80, threat: 5f, pushTime: 0.05f,
            role: EnemyRole.Elite, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/image.png", fps: 14f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 1.25f,
            prefab: "Assets/Prefabs/Enemy/Neu/Dolchschwarm.prefab");

        Def(EnemyId.Pilzkoenig, "Pilzkoenig (Miniboss)",
            health: 1800f, damage: 8f, speed: 1f, exp: 0, threat: 30f, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/pilz.png", fps: 5f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 3f,
            prefab: "Assets/Prefabs/Enemy/Neu/Pilzkoenig.prefab");

        Def(EnemyId.Rattenkoenigin, "Rattenkoenigin (Miniboss)",
            health: 3500f, damage: 14f, speed: 2f, exp: 0, threat: 40f, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.ArtFacesLeft,
            sheet: "Assets/Art/Gegner/new/MausMesser.png", fps: 8f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 2.6f,
            prefab: "Assets/Prefabs/Enemy/Neu/Rattenkoenigin.prefab");

        Def(EnemyId.Milchkoloss, "Milchkoloss (Miniboss)",
            health: 7000f, damage: 22f, speed: 1.1f, exp: 0, threat: 55f, pushTime: 0f,
            role: EnemyRole.MiniBoss, facing: EnemyFacing.Neutral,
            sheet: "Assets/Art/Gegner/new/wirklich_saure_milch_1.png", fps: 6f,
            colliderRadius: 0f, colliderOffset: new Vector2(0f, 0f), scale: 2.8f,
            prefab: "Assets/Prefabs/Enemy/Neu/Milchkoloss.prefab");
    }
    // ================================================================
    // WERKSTATT-ENDE
    // ================================================================
}
