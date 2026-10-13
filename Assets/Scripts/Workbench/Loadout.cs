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
/// PRESETS: Jeder Charakter hat bis zu <see cref="MaxPresets"/> gespeicherte
/// Builds, wie viele davon offen sind, kauft man im Shop
/// (<see cref="PresetCount"/>). Alles hier meint das gewaehlte - das zuletzt
/// geoeffnete, und das geht mit in den Lauf.
///
/// Evos bleiben aussen vor: die zieht man nicht, die entstehen aus zwei
/// ausgereizten Zutaten. Die Werkbank zeigt nur an, welche mit dem aktuellen
/// Verteiler ueberhaupt erreichbar sind.
/// </summary>
public static class Loadout
{
    /// <summary>
    /// Kacheln im Raster des Fensters - die Obergrenze. Wie viele davon offen
    /// sind, haengt an den Slots im Lauf (<see cref="Capacity"/>); der Rest
    /// ist gesperrt. Mehr als 10 Buffs gibt es nie.
    /// </summary>
    public const int MaxWeapons = 20;
    public const int MaxBuffs = 10;

    /// <summary>Slots im Lauf ohne Shop-Kauf. Frueher am Player-Prefab (3 / 1).</summary>
    public const int BaseWeaponSlots = 3;
    public const int BaseBuffSlots = 2;

    /// <summary>So viele Eintraege muss man je Slot im Lauf mitnehmen.</summary>
    public const int PicksPerSlot = 3;

    /// <summary>
    /// Waffenslots im Lauf: Grundwert + Shop. Der PlayerController setzt seine daraus.
    /// Der Tofu hat immer genau einen - seine eigene Waffe (<see cref="Characters.OnlyStartWeapon"/>).
    /// </summary>
    public static int RunWeaponSlots =>
        Characters.OnlyStartWeapon(CurrentCharacter) ? 1 : BaseWeaponSlots + Shop.GetInt(Shop.WeaponSlot);

    /// <summary>Buffslots im Lauf: Grundwert + Shop, beim Tofu zwei mehr.</summary>
    public static int RunBuffSlots =>
        BaseBuffSlots + Shop.GetInt(Shop.BuffSlot) + Characters.ExtraBuffSlots(CurrentCharacter);

    /// <summary>Gespeicherte Builds ohne Shop-Kauf, und hoechstens (= Knoepfe an der Werkbank).</summary>
    public const int BasePresets = 1;
    public const int MaxPresets = 4;

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
    /// <summary>
    /// Loescht den Spielstand auf der Platte und laedt leer neu. Nur fuer
    /// <see cref="SaveReset"/> (Optionen -> SPIEL -> Spielstand zuruecksetzen).
    /// In der Sandbox bleibt die Datei liegen.
    /// </summary>
    public static void ResetProgress()
    {
        Init();
        if (!sandbox && System.IO.File.Exists(store.SavePath)) System.IO.File.Delete(store.SavePath);
        initialized = false;
        Init();
        Changed?.Invoke();
    }

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
    /// Offene Plaetze im Verteiler: <see cref="PicksPerSlot"/> je Slot im
    /// Lauf, bei den Waffen plus die feste Startwaffe. 3 Waffenslots heissen
    /// also 10 Waffen, jeder gekaufte Slot oeffnet drei Plaetze mehr. Gedeckelt
    /// auf das Raster (<see cref="MaxWeapons"/> / <see cref="MaxBuffs"/>).
    /// </summary>
    public static int WeaponCapacity
    {
        get
        {
            int locked = string.IsNullOrEmpty(LockedWeaponId) ? 0 : 1;
            // Nur die Startwaffe (Tofu): kein Platz neben ihr.
            if (Characters.OnlyStartWeapon(CurrentCharacter)) return locked;
            return Mathf.Clamp(RunWeaponSlots * PicksPerSlot + locked, 0, MaxWeapons);
        }
    }

    public static int BuffCapacity => Mathf.Clamp(RunBuffSlots * PicksPerSlot, 0, MaxBuffs);

    /// <summary>
    /// Wie viele Waffen im Verteiler liegen muessen, damit der Build
    /// uebernommen werden kann.
    ///
    /// Es sind alle offenen Plaetze - es sei denn, der Spieler hat gar nicht
    /// so viele Waffen frei. Sonst waere "voll" unerfuellbar und der Knopf
    /// bliebe fuer immer grau. Kommen Waffen dazu, zieht die Zahl von selbst nach.
    /// </summary>
    public static int RequiredWeapons => Mathf.Min(WeaponCapacity, SelectableCount(PoolKind.Weapon));

    public static int RequiredBuffs => Mathf.Min(BuffCapacity, SelectableCount(PoolKind.Buff));

    /// <summary>
    /// Darf der gewaehlte Charakter das ueberhaupt haben? Nein bei einer
    /// Waffe, die einem anderen Charakter allein gehoert
    /// (<see cref="Characters.IsWeaponAllowed"/> - jede Startwaffe).
    /// </summary>
    public static bool IsAvailable(string id) => Characters.IsWeaponAllowed(id, CurrentCharacter);

    /// <summary>
    /// Darf es jetzt in den Verteiler? Zusaetzlich zu <see cref="IsAvailable"/>
    /// muss es freigeschaltet sein - Standard oder im Shop gekauft. Ungekauftes
    /// zeigt die Bibliothek gesperrt an.
    /// </summary>
    public static bool IsSelectable(string id) =>
        IsAvailable(id) && (IsLocked(id) || WeaponCatalog.IsUnlocked(id));

    /// <summary>
    /// Was die Bibliothek zeigt: alles, was der gewaehlte Charakter bekommen
    /// KANN - schon frei oder spaeter per Shop. Fremde Startwaffen und Waffen,
    /// die niemand mehr bekommt, fallen weg.
    /// </summary>
    public static List<WeaponDef> Available(PoolKind kind)
    {
        List<WeaponDef> list = WeaponCatalog.OfKind(kind);
        if (kind == PoolKind.Evo) return list;

        list.RemoveAll(def => !IsAvailable(def.Id) ||
                              !(IsLocked(def.Id) || WeaponCatalog.IsObtainable(def.Id)));
        return list;
    }

    private static int SelectableCount(PoolKind kind)
    {
        int n = 0;
        foreach (WeaponDef def in WeaponCatalog.OfKind(kind))
        {
            if (IsSelectable(def.Id)) n++;
        }
        return n;
    }

    /// <summary>Offene Plaetze einer Art - siehe <see cref="WeaponCapacity"/>.</summary>
    public static int Capacity(PoolKind kind) => kind == PoolKind.Buff ? BuffCapacity : WeaponCapacity;
    public static int Required(PoolKind kind) => kind == PoolKind.Buff ? RequiredBuffs : RequiredWeapons;

    /// <summary>Kacheln im Raster einer Art, offene und gesperrte zusammen.</summary>
    public static int GridSize(PoolKind kind) => kind == PoolKind.Buff ? MaxBuffs : MaxWeapons;

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
        // Gewaehltes Preset nicht mehr gekauft (Shop-Reset): zurueck auf das
        // letzte erlaubte. Der Inhalt dahinter bleibt im Spielstand liegen.
        bool removed = false;
        if (Store.PresetIndex >= PresetCount)
        {
            Store.PresetIndex = PresetCount - 1;
            removed = true;
        }

        // Was dem Charakter nicht (mehr) zusteht, fliegt raus - etwa eine
        // Startwaffe, die vor der Sperre im Verteiler eines anderen lag, oder
        // etwas Ungekauftes nach einem Shop-Reset. Unbekannte Ids bleiben
        // liegen (siehe LoadoutStore) - vielleicht kommt die Waffe ja wieder.
        removed |= Store.Weapons.RemoveAll(NotSelectable) > 0;
        removed |= Store.Buffs.RemoveAll(NotSelectable) > 0;

        removed |= EnsureStartWeapon();

        // Weniger offene Plaetze als belegt (etwa nach einem Shop-Reset):
        // hinten abschneiden, der feste Platz vorne bleibt.
        removed |= Trim(Store.Weapons, WeaponCapacity);
        removed |= Trim(Store.Buffs, BuffCapacity);
        return removed;
    }

    private static bool NotSelectable(string id) =>
        WeaponCatalog.Find(id) != null && !IsSelectable(id);

    private static bool Trim(List<string> ids, int capacity)
    {
        if (ids.Count <= capacity) return false;
        ids.RemoveRange(Mathf.Max(capacity, 0), ids.Count - Mathf.Max(capacity, 0));
        return true;
    }

    private static bool EnsureStartWeapon()
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
    //  Presets
    // ==================================================================

    /// <summary>
    /// Wie viele gespeicherte Builds der Spieler benutzen darf: einer immer,
    /// jeder Shop-Kauf (<see cref="Shop.LoadoutPreset"/>) einer mehr. Gilt fuer
    /// jeden Charakter, jeder hat aber seine eigenen Builds darin.
    ///
    /// Der aktuelle Shop-Stand, nicht der eingefrorene: gewaehlt wird im Hub,
    /// und ein Kauf soll sofort an der Werkbank ankommen.
    /// </summary>
    public static int PresetCount =>
        Mathf.Clamp(BasePresets + Mathf.RoundToInt(Shop.CurrentValue(Shop.LoadoutPreset)),
                    BasePresets, MaxPresets);

    /// <summary>
    /// Das gewaehlte Preset (0-basiert). Es ist immer das zuletzt geoeffnete -
    /// und genau das geht mit in den naechsten Lauf.
    /// </summary>
    public static int ActivePreset => Store.PresetIndex;

    /// <summary>Darf dieses Preset gewaehlt werden? Nein, solange es nicht gekauft ist.</summary>
    public static bool IsPresetOpen(int index) => index >= 0 && index < PresetCount;

    /// <summary>
    /// Wechselt auf ein anderes Preset. Der bisherige Build bleibt darin
    /// liegen, wie er war. Gibt false zurueck, wenn das Preset nicht gekauft ist.
    /// </summary>
    public static bool SelectPreset(int index)
    {
        if (!IsPresetOpen(index)) return false;
        if (index == ActivePreset) return true;

        Store.PresetIndex = index;
        EnsureLocked();
        Fire();
        return true;
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
        if (!IsSelectable(id)) return false;
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

    /// <summary>
    /// Ein Klick auf eine Evo: liegen schon alle Zutaten im Verteiler, fliegen
    /// sie raus - sonst kommen die fehlenden dazu. Die Startwaffe des
    /// Charakters bleibt dabei immer liegen.
    ///
    /// Alles oder nichts: ist eine Zutat nicht waehlbar (fremde Startwaffe,
    /// nicht gekauft) oder fehlt fuer eine davon der Platz, passiert gar
    /// nichts - eine halb eingeraeumte Evo hilft keinem. Gibt false zurueck,
    /// wenn sich nichts geaendert hat.
    /// </summary>
    public static bool ToggleEvo(EvoDef evo)
    {
        if (evo == null) return false;

        List<WeaponDef> parts = new List<WeaponDef>();
        foreach (string id in new[] { evo.IngredientA, evo.IngredientB })
        {
            if (string.IsNullOrEmpty(id)) continue;
            WeaponDef def = WeaponCatalog.Find(id);
            if (def == null || def.Kind == PoolKind.Evo) return false;
            if (!parts.Contains(def)) parts.Add(def);
        }
        if (parts.Count == 0) return false;

        bool allIn = parts.TrueForAll(def => Contains(def.Id));
        bool changed = false;

        if (allIn)
        {
            foreach (WeaponDef def in parts)
            {
                if (IsLocked(def.Id)) continue;
                changed |= Get(def.Kind).Remove(def.Id);
            }
        }
        else
        {
            int missingWeapons = 0, missingBuffs = 0;
            foreach (WeaponDef def in parts)
            {
                if (Contains(def.Id)) continue;
                if (!IsSelectable(def.Id)) return false;
                if (def.Kind == PoolKind.Buff) missingBuffs++;
                else missingWeapons++;
            }

            if (Count(PoolKind.Weapon) + missingWeapons > WeaponCapacity) return false;
            if (Count(PoolKind.Buff) + missingBuffs > BuffCapacity) return false;

            foreach (WeaponDef def in parts)
            {
                if (Contains(def.Id)) continue;
                Get(def.Kind).Add(def.Id);
                changed = true;
            }
        }

        if (changed) Fire();
        return changed;
    }

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

        FillGroupRandom(PoolKind.Weapon);
        FillGroupRandom(PoolKind.Buff);

        Fire();
    }

    /// <summary>Stopft eine Gruppe mit zufaellig gezogenen Eintraegen bis zum Anschlag.</summary>
    private static void FillGroupRandom(PoolKind kind)
    {
        List<WeaponDef> pool = WeaponCatalog.OfKind(kind);
        pool.RemoveAll(def => !IsSelectable(def.Id));

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

    /// <summary>
    /// Beim Start eines Laufs (<see cref="PlayerController"/>): ab hier gilt
    /// der Verteiler immer, auch ohne "Build uebernehmen". Im Lauf kommt nur,
    /// was drinliegt.
    ///
    /// Offene Plaetze, die der Spieler nicht selbst belegt hat, werden
    /// zufaellig aufgefuellt - was er gewaehlt hat, bleibt stehen. Das
    /// Ergebnis wird gespeichert, die Werkbank zeigt danach also, womit
    /// gespielt wurde.
    ///
    /// In der Test-Szene (Sandbox) passiert nichts - die hat ihre eigene Auswahl.
    /// </summary>
    public static void PrepareForRun()
    {
        if (sandbox) return;

        SyncCharacter();

        FillGroupRandom(PoolKind.Weapon);
        FillGroupRandom(PoolKind.Buff);

        Store.Active = true;
        Store.Save();
        Fire();
    }

    // ==================================================================
    //  Uebernehmen
    // ==================================================================

    /// <summary>Gilt der Verteiler gerade fuer das Spiel?</summary>
    public static bool IsActive => Store.Active;

    /// <summary>
    /// Ab jetzt zieht das Spiel nur noch aus dem Verteiler. Erst moeglich,
    /// wenn beide Gruppen voll sind.
    ///
    /// Einen Knopf dafuer gibt es seit 11.10.2026 nicht mehr: jeder Lauf
    /// uebernimmt das gewaehlte Preset ohnehin (<see cref="PrepareForRun"/>).
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

        if (have >= evo.IngredientCount) return EvoState.Ready;
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
