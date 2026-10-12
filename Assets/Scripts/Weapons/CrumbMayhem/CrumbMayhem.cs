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
/// WER DRANKOMMT: zufaellig, nie zweimal hintereinander dieselbe. Kam eine
/// Waffe <see cref="PityAfterSwaps"/> Wechsel lang nicht, wird sie erzwungen.
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

    /// <summary>Eine kopierte Waffe samt ihren unveraenderten Stufenwerten.</summary>
    private sealed class Copy
    {
        public Weapon Weapon;
        public List<WeaponStats> BaseStats;
        public int Missed;
    }

    private readonly List<Copy> copies = new List<Copy>();
    private bool copiesBuilt;
    private Copy current;
    private float swapTimer;
    private int appliedLevel = int.MinValue;

    /// <summary>Kopiert die Evos der Startwaffen statt der Startwaffen selbst.</summary>
    protected virtual bool CopiesEvos => false;

    /// <summary>Die Waffe, die gerade dran ist - fuer Anzeigen. Null vor dem ersten Wechsel.</summary>
    public Weapon CurrentCopy => current != null ? current.Weapon : null;

    /// <summary>Sekunden bis zum naechsten Wechsel - das HUD zeigt sie neben der Waffe.</summary>
    public float SwapTimeLeft => current != null ? Mathf.Max(0f, swapTimer) : 0f;

    /// <summary>Schadensfaktor der aktuellen Stufe: Stufe 1 = 1.1, Stufe 12 = 2.2.</summary>
    public virtual float DamageFactor => 1f + DamagePerLevel * (weaponLevel + 1);

    /// <summary>Stufe der kopierten Waffe (0-basiert) fuer die aktuelle Tofu-Stufe.</summary>
    protected virtual int CopiedLevel => Mathf.Max(0, weaponLevel) / LevelsPerCopiedLevel;

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
            if (current != null) RestAll();
            return;
        }

        if (!copiesBuilt) BuildCopies();
        if (copies.Count == 0) return;

        if (appliedLevel != weaponLevel) ApplyLevel();

        swapTimer -= Time.deltaTime;
        if (current == null || swapTimer <= 0f) Swap();
    }

    // ------------------------------------------------------------------
    //  Kopien anlegen
    // ------------------------------------------------------------------

    private void BuildCopies()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;
        copiesBuilt = true;

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
    //  Stufe uebertragen
    // ------------------------------------------------------------------

    /// <summary>
    /// Schreibt Stufe und Schaden in alle Kopien. Die Stufenwerte sind eigene
    /// Kopien - viele Waffen teilen ihre Werte als statische Tabelle, und die
    /// echte Waffe soll vom Tofu-Bonus nichts abbekommen.
    /// </summary>
    private void ApplyLevel()
    {
        appliedLevel = weaponLevel;
        float factor = DamageFactor;

        foreach (Copy c in copies)
        {
            if (c.Weapon == null || c.BaseStats.Count == 0) continue;

            List<WeaponStats> scaled = CloneStats(c.BaseStats);
            foreach (WeaponStats s in scaled) s.damage *= factor;

            c.Weapon.stats = scaled;
            c.Weapon.weaponLevel = Mathf.Clamp(CopiedLevel, 0, scaled.Count - 1);
        }
    }

    // ------------------------------------------------------------------
    //  Wechsel
    // ------------------------------------------------------------------

    private void Swap()
    {
        Copy next = PickNext();
        if (next == null) return;

        if (current != null && current.Weapon != null) current.Weapon.Resting = true;

        foreach (Copy c in copies) c.Missed = c == next ? 0 : c.Missed + 1;

        current = next;
        current.Weapon.Resting = false;
        swapTimer = SwapInterval;
    }

    /// <summary>
    /// Zufaellig, aber nie dieselbe wie gerade. Ist eine Waffe zu lange
    /// ausgeblieben, kommt die, die am laengsten fehlt.
    /// </summary>
    private Copy PickNext()
    {
        List<Copy> pool = new List<Copy>();
        foreach (Copy c in copies)
        {
            if (c.Weapon != null && (c != current || copies.Count == 1)) pool.Add(c);
        }
        if (pool.Count == 0) return null;

        Copy overdue = null;
        foreach (Copy c in pool)
        {
            if (c.Missed >= PityAfterSwaps && (overdue == null || c.Missed > overdue.Missed)) overdue = c;
        }
        if (overdue != null) return overdue;

        return pool[Random.Range(0, pool.Count)];
    }

    private void RestAll()
    {
        foreach (Copy c in copies)
        {
            if (c.Weapon != null) c.Weapon.Resting = true;
        }
        current = null;
    }
}
