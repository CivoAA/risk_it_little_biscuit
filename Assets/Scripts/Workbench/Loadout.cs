using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Wie weit ein Evo-Rezept mit dem aktuellen Verteiler kommt.</summary>
public enum EvoState
{
    /// <summary>Keine der beiden Zutaten liegt im Verteiler.</summary>
    None = 0,
    /// <summary>Eine der beiden Zutaten liegt im Verteiler.</summary>
    Partial = 1,
    /// <summary>Beide Zutaten liegen im Verteiler - die Evo kann im Lauf fallen.</summary>
    Ready = 2,
}

/// <summary>
/// DER VERTEILER und die Schnittstelle dazu.
///
/// Der Spieler stellt an der Werkbank vor dem Lauf zusammen, WORAUS beim
/// Level-Up gezogen wird. Die Sachen sind damit nicht ausgeruestet - sie sind
/// der Topf, aus dem <see cref="PlayerController.RandomWeapon"/> seine drei
/// Karten zieht.
///
/// Zwei Zustaende, die man auseinanderhalten muss:
///
///   * <see cref="IsActive"/> == false - die Werkbank wurde nie benutzt.
///     Das Spiel zieht wie vorher aus allem, was freigeschaltet ist. Genau so
///     verhaelt sich ein Spielstand von vor der Werkbank.
///   * <see cref="IsActive"/> == true - es gilt der Verteiler. Was nicht
///     drinliegt, kommt im Lauf nicht vor.
///
/// JEDER CHARAKTER HAT SEINEN EIGENEN VERTEILER - so wie er seinen eigenen
/// Skilltree hat. Alles hier drin meint immer den des gerade gewaehlten
/// (<see cref="CurrentCharacter"/>); ein Wechsel im Hub blaettert nur um und
/// verschiebt nichts. Der Build des anderen liegt so da, wie man ihn verlassen
/// hat, samt seinem "uebernommen".
///
/// Evos bleiben aussen vor: die zieht man nicht, die entstehen aus zwei
/// ausgereizten Zutaten. Die Werkbank zeigt nur an, welche mit dem aktuellen
/// Verteiler ueberhaupt erreichbar sind.
/// </summary>
public static class Loadout
{
    /// <summary>Plaetze im Verteiler. Das Raster im Fenster hat genauso viele Kacheln.</summary>
    public const int WeaponSlots = 20;
    public const int BuffSlots = 10;

    private static LoadoutStore store;
    private static bool initialized;
    private static bool sandbox;

    /// <summary>Feuert, wenn sich der Verteiler geaendert hat - die UI haengt daran.</summary>
    public static event Action Changed;

    /// <summary>Sandbox (Test-Szene): aendert im Speicher, schreibt nichts.</summary>
    public static bool SandboxMode
    {
        get => sandbox;
        set
        {
            sandbox = value;
            if (store != null) store.ReadOnly = value;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => Init();

    public static void Init()
    {
        if (initialized) return;
        initialized = true;

        store = new LoadoutStore { ReadOnly = sandbox };
        store.Load();

        // Sofort und nicht erst beim ersten Zugriff: ein Spielstand aus der
        // Zeit vor den getrennten Builds hat genau einen Verteiler, und den
        // erbt der Charakter, der hier als erster danach fragt.
        store.Use(CurrentCharacter);

        // Der feste Platz kann sich zwischen zwei Sitzungen geaendert haben -
        // der Spieler waehlt seinen Charakter woanders.
        if (EnsureLocked() | store.Migrated) store.Save();
    }

    /// <summary>
    /// Der Charakter, dessen Verteiler gerade gilt. Im Hub der gewaehlte, im
    /// Lauf der eingefrorene - <see cref="Shop.RunSkinIndex"/> unterscheidet
    /// das bereits, und damit spielt jeder Lauf mit dem Build, mit dem er
    /// gestartet wurde.
    /// </summary>
    public static int CurrentCharacter => Shop.RunSkinIndex;

    /// <summary>
    /// Der Speicher, immer schon auf den richtigen Charakter gestellt.
    ///
    /// Der Abgleich sitzt hier und nicht nur in <see cref="SyncCharacter"/>,
    /// weil der Wechsel ueberall passieren kann - Charakterauswahl im Hub,
    /// Cheat-Konsole, Testszene. Ein Vergleich zweier int ist billig genug,
    /// um ihn bei jedem Zugriff zu machen; das Umstellen selbst kostet nur
    /// dann etwas, wenn wirklich gewechselt wurde.
    /// </summary>
    private static LoadoutStore Store
    {
        get
        {
            if (!initialized) Init();

            if (store.Character != CurrentCharacter)
            {
                store.Use(CurrentCharacter);
                if (EnsureLocked()) store.Save();
            }

            return store;
        }
    }

    /// <summary>
    /// Liest loadout.json neu ein und wirft weg, was im Speicher steht. Fuer
    /// Tests, die den Sandbox-Modus wieder abschalten, und fuer alles, was die
    /// Datei von aussen aendert.
    /// </summary>
    public static void Reload()
    {
        initialized = false;
        store = null;
        Init();

        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Werkbank] Fehler im Changed-Handler: {e}");
        }
    }

    // ==================================================================
    //  Soll-Zahlen
    // ==================================================================

    /// <summary>
    /// Wie viele Waffen im Verteiler liegen muessen, damit der Build
    /// uebernommen werden kann.
    ///
    /// Es sind <see cref="WeaponSlots"/> - es sei denn, das Spiel hat gar
    /// nicht so viele. Heute gibt es 18 Waffen bei 20 Plaetzen; die Forderung
    /// "voll" waere damit unerfuellbar, und der Knopf bliebe fuer immer grau.
    /// Kommen Waffen dazu, zieht die Zahl von selbst nach.
    /// </summary>
    public static int RequiredWeapons =>
        Mathf.Min(WeaponSlots, WeaponCatalog.CountOfKind(PoolKind.Weapon));

    public static int RequiredBuffs =>
        Mathf.Min(BuffSlots, WeaponCatalog.CountOfKind(PoolKind.Buff));

    public static int Capacity(PoolKind kind) => kind == PoolKind.Buff ? BuffSlots : WeaponSlots;
    public static int Required(PoolKind kind) => kind == PoolKind.Buff ? RequiredBuffs : RequiredWeapons;

    // ==================================================================
    //  Der feste erste Platz
    // ==================================================================

    /// <summary>
    /// Die Standardwaffe des gewaehlten Charakters. Sie liegt immer auf dem
    /// ersten Waffenplatz, laesst sich nicht herausnehmen und zaehlt mit.
    ///
    /// Der Spieler bekommt sie zu Beginn jedes Laufs ohnehin
    /// (<see cref="Shop.RunStartWeapon"/>) - ein Verteiler ohne sie waere eine
    /// Waffe, die man hat, aber nie verbessern kann.
    /// </summary>
    public static string LockedWeaponId => Characters.StartWeaponId(CurrentCharacter);

    public static bool IsLocked(string id) =>
        !string.IsNullOrEmpty(id) && id == LockedWeaponId;

    /// <summary>
    /// Sorgt dafuer, dass die Standardwaffe ganz vorne liegt. Gibt true
    /// zurueck, wenn sich dabei etwas geaendert hat.
    /// </summary>
    private static bool EnsureLocked()
    {
        string id = LockedWeaponId;
        if (string.IsNullOrEmpty(id)) return false;
        if (WeaponCatalog.Find(id) == null)
        {
            Debug.LogWarning($"[Werkbank] Startwaffe '{id}' steht nicht im Katalog - " +
                             "siehe Characters.StartWeaponIdByskin.");
            return false;
        }

        List<string> weapons = Store.Weapons;
        int at = weapons.IndexOf(id);
        if (at == 0) return false;

        if (at > 0) weapons.RemoveAt(at);
        else if (weapons.Count >= WeaponSlots) weapons.RemoveAt(weapons.Count - 1);

        weapons.Insert(0, id);
        return true;
    }

    /// <summary>
    /// Stellt auf den Verteiler des gewaehlten Charakters um und zieht dessen
    /// festen Platz nach. Gehoert ueberall dorthin, wo der Charakter gewechselt
    /// wird (<see cref="Shop.SkinIndex"/> tut es von selbst); die Werkbank ruft
    /// es beim Oeffnen noch einmal auf.
    ///
    /// Der Zugriff ueber <see cref="Store"/> merkt den Wechsel ohnehin - was
    /// hier dazukommt, ist das <see cref="Changed"/> danach, damit ein offenes
    /// Fenster den anderen Build auch anzeigt.
    /// </summary>
    public static void SyncCharacter()
    {
        if (!initialized)
        {
            Init();
            return;
        }

        bool switched = store.Character != CurrentCharacter;
        if (switched) store.Use(CurrentCharacter);

        // | statt ||: EnsureLocked muss auch dann laufen, wenn gewechselt wurde.
        if (EnsureLocked() | switched) Fire();
    }

    // ==================================================================
    //  Inhalt
    // ==================================================================

    /// <summary>Die gewaehlten Ids einer Art, in der Reihenfolge der Wahl.</summary>
    public static List<string> Get(PoolKind kind) =>
        kind == PoolKind.Buff ? Store.Buffs : Store.Weapons;

    public static int Count(PoolKind kind) => Get(kind).Count;

    public static bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return Store.Weapons.Contains(id) || Store.Buffs.Contains(id);
    }

    public static bool IsFull(PoolKind kind) => Count(kind) >= Capacity(kind);

    /// <summary>Beide Gruppen so voll, wie sie sein koennen.</summary>
    public static bool IsComplete =>
        Count(PoolKind.Weapon) >= RequiredWeapons && Count(PoolKind.Buff) >= RequiredBuffs;

    /// <summary>
    /// Legt einen Eintrag in den Verteiler. Gibt false zurueck, wenn er schon
    /// drinliegt, kein Platz mehr ist oder es keine Waffe/kein Buff ist.
    /// </summary>
    public static bool Add(string id)
    {
        WeaponDef def = WeaponCatalog.Find(id);
        if (def == null || def.Kind == PoolKind.Evo) return false;
        if (Contains(id) || IsFull(def.Kind)) return false;

        Get(def.Kind).Add(id);
        Fire();
        return true;
    }

    public static bool Remove(string id)
    {
        WeaponDef def = WeaponCatalog.Find(id);
        if (def == null) return false;
        if (IsLocked(id)) return false;
        if (!Get(def.Kind).Remove(id)) return false;

        Fire();
        return true;
    }

    public static bool Toggle(string id) => Contains(id) ? Remove(id) : Add(id);

    /// <summary>Leert den Verteiler. Der feste erste Platz bleibt.</summary>
    public static void Clear()
    {
        Store.Weapons.Clear();
        Store.Buffs.Clear();
        EnsureLocked();
        Fire();
    }

    /// <summary>
    /// Wuerfelt den Verteiler aus: beide Gruppen werden bis zum Anschlag mit
    /// zufaellig gezogenen Eintraegen gefuellt. Der feste erste Platz bleibt,
    /// was schon drinliegt, wird vorher weggeraeumt - "zufaellig" soll wirklich
    /// jedes Mal etwas anderes ergeben und nicht nur die Luecken stopfen.
    /// </summary>
    public static void FillRandom()
    {
        Store.Weapons.Clear();
        Store.Buffs.Clear();
        EnsureLocked();

        foreach (PoolKind kind in new[] { PoolKind.Weapon, PoolKind.Buff })
        {
            List<WeaponDef> pool = WeaponCatalog.OfKind(kind);

            // Fisher-Yates auf einer Kopie: zieht ohne Zuruecklegen und ohne
            // die Katalogreihenfolge anzufassen.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            foreach (WeaponDef def in pool)
            {
                if (IsFull(kind)) break;
                if (!Contains(def.Id)) Get(kind).Add(def.Id);
            }
        }

        Fire();
    }

    // ==================================================================
    //  Uebernehmen
    // ==================================================================

    /// <summary>Gilt der Verteiler gerade fuer das Spiel?</summary>
    public static bool IsActive => Store.Active;

    /// <summary>
    /// "Build uebernehmen": ab jetzt zieht das Spiel nur noch aus dem
    /// Verteiler. Erst moeglich, wenn beide Gruppen voll sind.
    /// </summary>
    public static bool Apply()
    {
        if (!IsComplete) return false;

        Store.Active = true;
        Store.Save();
        Fire();
        return true;
    }

    /// <summary>Zurueck auf "alles, was freigeschaltet ist".</summary>
    public static void Deactivate()
    {
        Store.Active = false;
        Store.Save();
        Fire();
    }

    /// <summary>Raeumt die Verteiler ALLER Charaktere - der grosse Knopf.</summary>
    public static void ResetAll()
    {
        Store.ResetAll();
        EnsureLocked();
        Fire();
    }

    /// <summary>
    /// Darf diese Waffe/dieser Buff im Lauf gezogen werden?
    ///
    /// Das ist der einzige Haken, den das Spiel in die Werkbank hat - siehe
    /// <see cref="PlayerController"/>. Solange der Verteiler nicht uebernommen
    /// wurde, sagt die Methode zu allem ja.
    /// </summary>
    public static bool AllowsInRun(string weaponId)
    {
        if (!IsActive) return true;
        if (string.IsNullOrEmpty(weaponId)) return true;

        // Evos haengen nicht am Verteiler: die entstehen aus ihren Zutaten.
        WeaponDef def = WeaponCatalog.Find(weaponId);
        if (def == null || def.Kind == PoolKind.Evo) return true;

        return Contains(weaponId);
    }

    // ==================================================================
    //  Evos
    // ==================================================================

    public static EvoState StateOf(EvoDef evo)
    {
        if (evo == null) return EvoState.None;

        int have = 0;
        if (Contains(evo.IngredientA)) have++;
        if (Contains(evo.IngredientB)) have++;

        if (have >= 2) return EvoState.Ready;
        return have == 1 ? EvoState.Partial : EvoState.None;
    }

    /// <summary>
    /// Alle Rezepte, das am weitesten Fortgeschrittene zuerst. Die Leiste im
    /// Fenster zeigt davon die ersten paar, das Overlay die ganze Liste.
    /// </summary>
    public static List<EvoDef> EvosByProgress()
    {
        List<EvoDef> list = new List<EvoDef>(WeaponCatalog.Evos);
        list.Sort((a, b) =>
        {
            int d = StateOf(b).CompareTo(StateOf(a));
            return d != 0 ? d : a.Order.CompareTo(b.Order);
        });
        return list;
    }

    public static int ReadyEvoCount()
    {
        int n = 0;
        foreach (EvoDef evo in WeaponCatalog.Evos)
        {
            if (StateOf(evo) == EvoState.Ready) n++;
        }
        return n;
    }

    // ==================================================================

    /// <summary>
    /// Jede Aenderung landet sofort auf der Platte. Der Verteiler ist kein
    /// Formular mit Abbrechen-Knopf - wer eine Kachel anklickt, hat sie
    /// gewaehlt, auch wenn danach der Strom ausfaellt.
    /// </summary>
    private static void Fire()
    {
        // "Uebernommen" und "unvollstaendig" darf es nicht gleichzeitig geben.
        // Wer nach dem Uebernehmen eine Kachel herausnimmt, faellt zurueck auf
        // "alles, was freigeschaltet ist" - sonst liefe der naechste Lauf
        // stillschweigend mit einem halben Verteiler.
        if (Store.Active && !IsComplete) Store.Active = false;

        Store.Save();

        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Werkbank] Fehler im Changed-Handler: {e}");
        }
    }
}
