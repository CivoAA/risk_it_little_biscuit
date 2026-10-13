using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kruemelchaos (Crumb Mayhem) - die Startwaffe des Tofus (nur fuer ihn).
///
/// Tofu schmeckt nach allem: die Waffe hat keinen eigenen Angriff, sondern
/// wird alle <see cref="SwapInterval"/> Sekunden zur Startwaffe eines anderen
/// Charakters. Dafuer hat der Tofu nur diesen einen Waffenslot
/// (<see cref="Characters.OnlyStartWeapon"/>) und zwei Buffslots mehr.
///
/// STUFEN: doppelt so viele wie eine normale Waffe (<see cref="Levels"/>).
/// Je zwei Tofu-Stufen kopieren eine Stufe der fremden Waffe - Stufe 1 und 2
/// deren Stufe 1, Stufe 3 und 4 deren Stufe 2 usw. Dazu kommen +10 % Schaden
/// je Tofu-Stufe (<see cref="DamagePerLevel"/>), weil es die einzige Waffe ist.
/// Auf Maximalstufe wird daraus ohne zweite Zutat die Evo
/// (<see cref="CrumbMayhemEvo"/>), die dasselbe mit den Evos der Startwaffen tut.
///
/// WIE KOPIERT WIRD: beim ersten Update legt die Waffe von jeder fremden
/// Startwaffe am Spieler eine echte Kopie als Kind an (Instantiate - so
/// kommen Prefabs und Bilder mit). Die Kopie steht in keiner Liste des
/// Spielers (<see cref="Weapon.IsCopy"/>) und bekommt eigene Stufenwerte mit
/// dem Tofu-Schaden. Wer gerade nicht dran ist, ruht (<see cref="Weapon.Resting"/>):
/// greift nicht neu an, bleibt aber aktiv - Marmeladenlachen, fliegende
/// Klingen und brennende Gegner machen also weiter Schaden, auch wenn laengst
/// die naechste Waffe dran ist.
///
/// WER DRANKOMMT: zufaellig, nie eine, die gerade schon dran ist. Kam eine
/// Waffe <see cref="PityAfterSwaps"/> Wechsel lang nicht, wird sie erzwungen.
///
/// SKILLTREE (gilt auch fuer die Evo):
///   - Doppelbiss (<see cref="SkillGrants.Doppelbiss"/>): zwei Plaetze statt
///     einem, jeder mit eigenem Wechsel, um einen halben Wechsel versetzt.
///     Jede Waffe macht dafuer nur <see cref="DoubleBiteDamage"/>.
///   - Nachgeschmack (<see cref="SkillGrants.Nachgeschmack"/>): die abgeloeste
///     Waffe ruht erst nach einem Cooldown (1-3 s) und macht bis dahin
///     doppelten Schaden - so greift sie noch einmal an.
///   - Kruemelsturm (<see cref="SkillGrants.Kruemelsturm"/>, ausgeloest vom
///     <see cref="PlayerController"/>): <see cref="StormDuration"/> Sekunden
///     lang greifen alle Kopien gleichzeitig an.
/// </summary>
public class CrumbMayhem : Weapon
{
    /// <summary>So lange bleibt eine kopierte Waffe dran (Sekunden).</summary>
    public const float SwapInterval = 5f;

    /// <summary>Schadensbonus je Tofu-Stufe - 0.1 = +10 %.</summary>
    public const float DamagePerLevel = 0.1f;

    /// <summary>Stufen des Kruemelchaos: doppelt so viele wie eine normale Waffe.</summary>
    public const int Levels = 12;

    /// <summary>So viele Tofu-Stufen teilen sich eine Stufe der kopierten Waffe.</summary>
    public const int LevelsPerCopiedLevel = 2;

    /// <summary>
    /// Pity: so viele Wechsel darf eine Waffe hoechstens ausbleiben, dann kommt
    /// sie sicher. Bei acht Waffen ist sie im Schnitt alle sieben Wechsel dran.
    /// </summary>
    public const int PityAfterSwaps = 10;

    /// <summary>Doppelbiss: Schadensfaktor jeder Kopie, weil zwei gleichzeitig laufen.</summary>
    public const float DoubleBiteDamage = 0.65f;

    /// <summary>Nachgeschmack: Schadensfaktor der abgeloesten Waffe.</summary>
    public const float AftertasteDamage = 2f;

    /// <summary>Nachgeschmack: so lange bleibt sie hoechstens/mindestens noch dran (Sekunden).</summary>
    public const float AftertasteMin = 1f, AftertasteMax = 3f;

    /// <summary>Kruemelsturm: ab diesem Lebensanteil.</summary>
    public const float StormThreshold = 0.35f;

    /// <summary>Kruemelsturm: so lange greifen alle Kopien an (Sekunden).</summary>
    public const float StormDuration = 3f;

    /// <summary>Kruemelsturm: Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float StormCooldown = 35f;

    /// <summary>Eine kopierte Waffe samt ihren unveraenderten Stufenwerten.</summary>
    private sealed class Copy
    {
        public Weapon Weapon;
        public List<WeaponStats> BaseStats;
        public int Missed;

        /// <summary>Nachgeschmack laeuft bis hierhin (Time.time).</summary>
        public float AftertasteUntil;

        /// <summary>Zuletzt in die Stufenwerte geschriebener Faktor; NaN = neu schreiben.</summary>
        public float AppliedFactor = float.NaN;
    }

    /// <summary>Ein Platz, auf dem gerade eine Kopie dran ist. Mit Doppelbiss gibt es zwei.</summary>
    private sealed class Lane
    {
        public Copy Current;
        public float Timer;
    }

    private readonly List<Copy> copies = new List<Copy>();
    private readonly List<Lane> lanes = new List<Lane>();
    private bool copiesBuilt;
    private int appliedLevel = int.MinValue;
    private bool doubleBite, aftertaste;
    private float stormUntil;

    /// <summary>Kopiert die Evos der Startwaffen statt der Startwaffen selbst.</summary>
    protected virtual bool CopiesEvos => false;

    /// <summary>So viele Plaetze laufen gerade (1, mit Doppelbiss 2). 0 vor dem ersten Wechsel.</summary>
    public int LaneCount => lanes.Count;

    /// <summary>Die Waffe auf Platz <paramref name="lane"/> - fuer Anzeigen. Null vor dem ersten Wechsel.</summary>
    public Weapon CopyAt(int lane)
    {
        if (lane < 0 || lane >= lanes.Count || lanes[lane].Current == null) return null;
        return lanes[lane].Current.Weapon;
    }

    /// <summary>Sekunden bis zum naechsten Wechsel auf Platz <paramref name="lane"/> - das HUD zeigt sie neben der Waffe.</summary>
    public float SwapTimeLeftAt(int lane)
    {
        if (lane < 0 || lane >= lanes.Count || lanes[lane].Current == null) return 0f;
        return Mathf.Max(0f, lanes[lane].Timer);
    }

    /// <summary>Laeuft gerade der Kruemelsturm?</summary>
    public bool Storming => Time.time < stormUntil;

    /// <summary>Schadensfaktor der aktuellen Stufe: Stufe 1 = 1.1, Stufe 12 = 2.2.</summary>
    public virtual float DamageFactor => 1f + DamagePerLevel * (weaponLevel + 1);

    /// <summary>Stufe der kopierten Waffe (0-basiert) fuer die aktuelle Tofu-Stufe.</summary>
    protected virtual int CopiedLevel => Mathf.Max(0, weaponLevel) / LevelsPerCopiedLevel;

    /// <summary>
    /// Das Kruemelchaos, das am Spieler gerade laeuft - die Evo vor der
    /// Grundwaffe. Null, wenn keins aktiv ist (also kein Tofu).
    /// </summary>
    public static CrumbMayhem FindActive(PlayerController player)
    {
        if (player == null) return null;
        return Find(player.activeEvos) ?? Find(player.activeWeapon);
    }

    private static CrumbMayhem Find(Weapon[] list)
    {
        if (list == null) return null;
        foreach (Weapon w in list)
        {
            if (w is CrumbMayhem m && m.IsActive) return m;
        }
        return null;
    }

    protected virtual void Awake()
    {
        stats = new List<WeaponStats>();
        for (int i = 0; i < Levels; i++)
        {
            int copied = i / LevelsPerCopiedLevel + 1;
            int bonus = Mathf.RoundToInt(DamagePerLevel * (i + 1) * 100f);
            stats.Add(new WeaponStats
            {
                description = i == 0
                    ? $"Wird alle {SwapInterval:0} s zur Startwaffe eines anderen Charakters (Stufe {copied}, +{bonus} % Schaden)"
                    : $"Kopiert Stufe {copied}, +{bonus} % Schaden",
            });
        }
        maxweaponLevel = Levels - 1;
    }

    void Update()
    {
        if (!IsActive)
        {
            // Nicht erhalten oder durch die Evo ersetzt: alle Kopien ruhen,
            // was noch liegt oder fliegt, laeuft aus.
            if (lanes.Count > 0 || stormUntil > 0f) RestAll();
            return;
        }

        if (!copiesBuilt) BuildCopies();
        if (copies.Count == 0) return;

        if (appliedLevel != weaponLevel)
        {
            appliedLevel = weaponLevel;
            foreach (Copy c in copies) c.AppliedFactor = float.NaN;
        }

        if (lanes.Count == 0) StartLanes();

        foreach (Lane lane in lanes)
        {
            lane.Timer -= Time.deltaTime;
            if (lane.Current == null || lane.Timer <= 0f) Swap(lane);
        }

        UpdateCopies();
    }

    // ------------------------------------------------------------------
    //  Kopien anlegen
    // ------------------------------------------------------------------

    private void BuildCopies()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;
        copiesBuilt = true;

        // Der Skilltree aendert sich im Lauf nicht - einmal nachsehen reicht.
        doubleBite = Skills.HasGrant(SkillGrants.Doppelbiss);
        aftertaste = Skills.HasGrant(SkillGrants.Nachgeschmack);

        // Alle Waffen am Spieler - auch Evos ohne Rezept, die in keiner Liste stehen.
        Dictionary<string, Weapon> byId = new Dictionary<string, Weapon>();
        foreach (Weapon w in player.GetComponentsInChildren<Weapon>(true))
        {
            if (w == null || w.IsCopy || string.IsNullOrEmpty(w.weaponID)) continue;
            if (!byId.ContainsKey(w.weaponID)) byId.Add(w.weaponID, w);
        }

        for (int skin = 0; skin < Characters.Count; skin++)
        {
            // Der Tofu selbst nicht - sonst kopierte sich das Kruemelchaos.
            if (skin == Characters.Tofu) continue;

            string id = CopiesEvos ? Characters.StartEvoId(skin) : Characters.StartWeaponId(skin);
            if (string.IsNullOrEmpty(id)) continue;

            if (!byId.TryGetValue(id, out Weapon source) || source is CrumbMayhem)
            {
                Debug.LogWarning($"[Kruemelchaos] '{id}' ({Characters.NameOf(skin)}) haengt nicht am Spieler - wird nicht kopiert.");
                continue;
            }

            Weapon copy = MakeCopy(source);
            if (copy != null) copies.Add(new Copy { Weapon = copy, BaseStats = CloneStats(copy.stats) });
        }
    }

    private Weapon MakeCopy(Weapon source)
    {
        GameObject go = Instantiate(source.gameObject, source.transform.position, source.transform.rotation, transform);
        // Eigener Name: die Projektile mancher Waffen suchen ihre Waffe per Name.
        go.name = "Tofu Copy - " + source.gameObject.name;

        Weapon copy = go.GetComponent<Weapon>();
        if (copy == null)
        {
            Destroy(go);
            return null;
        }

        copy.IsCopy = true;
        copy.Resting = true;
        copy.weaponLevel = -1;
        copy.hasBeenRemoved = false;
        copy.posssibleEvo = false;
        return copy;
    }

    private static List<WeaponStats> CloneStats(List<WeaponStats> source)
    {
        List<WeaponStats> list = new List<WeaponStats>();
        if (source == null) return list;
        foreach (WeaponStats s in source) list.Add(s != null ? s.Clone() : new WeaponStats());
        return list;
    }

    // ------------------------------------------------------------------
    //  Stufe und Schaden uebertragen
    // ------------------------------------------------------------------

    /// <summary>
    /// Schreibt Stufe und Schaden in eine Kopie. Die Stufenwerte sind eigene
    /// Kopien - viele Waffen teilen ihre Werte als statische Tabelle, und die
    /// echte Waffe soll vom Tofu-Bonus nichts abbekommen.
    /// </summary>
    private void ApplyStats(Copy c, float factor)
    {
        c.AppliedFactor = factor;
        if (c.Weapon == null || c.BaseStats.Count == 0) return;

        List<WeaponStats> scaled = CloneStats(c.BaseStats);
        foreach (WeaponStats s in scaled) s.damage *= factor;

        c.Weapon.stats = scaled;
        c.Weapon.weaponLevel = Mathf.Clamp(CopiedLevel, 0, scaled.Count - 1);
    }

    /// <summary>
    /// Wer darf gerade angreifen, und mit welchem Schaden? Dran ist, wer auf
    /// einem Platz steht, im Nachgeschmack ist oder im Kruemelsturm mitmacht.
    /// </summary>
    private void UpdateCopies()
    {
        float now = Time.time;
        bool storm = now < stormUntil;
        float baseFactor = DamageFactor * (doubleBite ? DoubleBiteDamage : 1f);

        foreach (Copy c in copies)
        {
            if (c.Weapon == null) continue;

            bool onLane = IsOnLane(c);
            bool tasting = !onLane && now < c.AftertasteUntil;

            float factor = baseFactor * (tasting ? AftertasteDamage : 1f);
            if (!Mathf.Approximately(c.AppliedFactor, factor)) ApplyStats(c, factor);

            c.Weapon.Resting = !(onLane || tasting || storm);
        }
    }

    private bool IsOnLane(Copy c)
    {
        foreach (Lane lane in lanes)
        {
            if (lane.Current == c) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    //  Wechsel
    // ------------------------------------------------------------------

    /// <summary>
    /// Legt die Plaetze an: einer, mit Doppelbiss zwei. Der zweite wechselt
    /// zum ersten Mal schon nach einem halben Wechsel - danach sind die beiden
    /// immer um <see cref="SwapInterval"/>/2 versetzt.
    /// </summary>
    private void StartLanes()
    {
        int count = doubleBite ? Mathf.Min(2, copies.Count) : 1;
        for (int i = 0; i < count; i++)
        {
            Lane lane = new Lane();
            lanes.Add(lane);
            Swap(lane);
            if (i > 0) lane.Timer = SwapInterval * 0.5f;
        }
    }

    private void Swap(Lane lane)
    {
        Copy old = lane.Current;
        Copy next = PickNext(old);
        if (next == null) return;

        foreach (Copy c in copies)
        {
            if (c == next) c.Missed = 0;
            else if (!IsOnLane(c)) c.Missed++;
        }

        lane.Current = next;
        lane.Timer = SwapInterval;
        next.AftertasteUntil = 0f;

        // Nachgeschmack: die alte Waffe bleibt noch einen Cooldown lang dran.
        // Ihr Angriffstimer steht dann hoechstens auf einem Cooldown, also
        // greift sie in der Zeit noch einmal an - mit doppeltem Schaden.
        if (aftertaste && old != null && old != next && old.Weapon != null && old.Weapon.IsActive)
            old.AftertasteUntil = Time.time + Mathf.Clamp(old.Weapon.CurrentCooldown, AftertasteMin, AftertasteMax);
    }

    /// <summary>
    /// Zufaellig, aber nie eine, die gerade schon dran ist oder noch
    /// nachschmeckt. Ist eine Waffe zu lange ausgeblieben, kommt die, die am
    /// laengsten fehlt.
    /// </summary>
    private Copy PickNext(Copy leaving)
    {
        float now = Time.time;
        List<Copy> pool = new List<Copy>();
        foreach (Copy c in copies)
        {
            if (c.Weapon == null || IsOnLane(c) || now < c.AftertasteUntil) continue;
            pool.Add(c);
        }

        // Zu wenige Waffen fuer alle Plaetze: lieber dieselbe wieder als gar keine.
        if (pool.Count == 0 && leaving != null && leaving.Weapon != null) return leaving;
        if (pool.Count == 0) return null;

        Copy overdue = null;
        foreach (Copy c in pool)
        {
            if (c.Missed >= PityAfterSwaps && (overdue == null || c.Missed > overdue.Missed)) overdue = c;
        }
        if (overdue != null) return overdue;

        return pool[Random.Range(0, pool.Count)];
    }

    // ------------------------------------------------------------------
    //  Kruemelsturm
    // ------------------------------------------------------------------

    /// <summary>
    /// Kruemelsturm: <see cref="StormDuration"/> Sekunden lang greifen alle
    /// Kopien gleichzeitig an. False, wenn es (noch) nichts zu kopieren gibt.
    /// </summary>
    public bool Storm()
    {
        if (!IsActive || copies.Count == 0) return false;
        stormUntil = Time.time + StormDuration;
        return true;
    }

    private void RestAll()
    {
        foreach (Copy c in copies)
        {
            if (c.Weapon == null) continue;
            c.Weapon.Resting = true;
            c.AftertasteUntil = 0f;
        }
        lanes.Clear();
        stormUntil = 0f;
    }
}
