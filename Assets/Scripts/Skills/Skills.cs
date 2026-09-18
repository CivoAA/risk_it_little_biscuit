using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Schnittstelle zum Skilltree. Statisch, ohne Szenenobjekt, ab dem ersten
/// Frame verfügbar.
///
///   Skills.CanUnlock(node)     - Vorbedingungen offen und genug Punkte?
///   Skills.TryUnlock(node)     - kaufen
///   Skills.IsUnlocked(node)
///   Skills.Bonus(SkillType.IncreaseDamage)  - Summe für den laufenden Run
///   Skills.HasGrant(SkillGrants.Irgendwas)  - ein Schalter aus dem Baum, also
///                                             z.B. ein Waffen-Upgrade
///
/// Welcher Baum gilt, steht in <see cref="ActiveTree"/>: der des gewählten
/// Charakters. Beim Charakterwechsel wird <see cref="SetActiveTreeForCharacter"/>
/// gerufen, der Rest passt sich an.
/// </summary>
public static class Skills
{
    private static SkillStore store;
    private static bool initialized;
    private static bool sandbox;

    private static SkillTreeDef activeTree;
    private static readonly Dictionary<SkillType, float> bonuses = new Dictionary<SkillType, float>();
    private static readonly HashSet<string> grants = new HashSet<string>();

    /// <summary>Feuert nach jeder Änderung - Freischalten, Zurücksetzen, Punkte.</summary>
    public static event Action Changed;

    /// <summary>Sandbox (Test-Szene): wirkt im Speicher, schreibt nicht auf die Platte.</summary>
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

        store = new SkillStore { ReadOnly = sandbox };
        store.Load();

        // Welcher Baum gilt, hängt am gewählten Charakter - und der steht im
        // Shop-Spielstand. Der wird hier bewusst NICHT angefasst: Init läuft vor
        // der ersten Szene, und den Shop dort mitzuladen hiesse, die Reihenfolge
        // zweier Spielstände voneinander abhängig zu machen. Der Baum wird beim
        // ersten Zugriff auf ActiveTree nachgeholt.
    }

    private static SkillStore Store
    {
        get
        {
            if (!initialized) Init();
            return store;
        }
    }

    // ==================================================================
    //  Baumwahl
    // ==================================================================

    public static SkillTreeDef ActiveTree
    {
        get
        {
            if (!initialized) Init();
            if (activeTree == null) SetActiveTree(ResolveTreeForCurrentCharacter(), raise: false);
            return activeTree;
        }
    }

    /// <summary>
    /// Der Baum des gerade gewählten Charakters. Ist der Shop-Spielstand aus
    /// irgendeinem Grund nicht da, kommt der Notbaum zurück - lieber ein leeres
    /// Fenster als eine Ausnahme beim Start.
    /// </summary>
    private static SkillTreeDef ResolveTreeForCurrentCharacter()
    {
        try
        {
            return SkillTrees.ForCharacter(Shop.SkinIndex);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Skills] Charakter unbekannt, nehme den Notbaum: {e.Message}");
            return SkillTrees.Default;
        }
    }

    /// <summary>
    /// Holt den Baum des aktuellen Charakters neu. Billig genug, um beim Öffnen
    /// des Skilltree-Fensters einfach aufgerufen zu werden.
    /// </summary>
    public static void Sync()
    {
        Init();
        SetActiveTree(ResolveTreeForCurrentCharacter(), raise: true);
    }

    private static void SetActiveTree(SkillTreeDef tree, bool raise)
    {
        if (tree == activeTree) return;

        activeTree = tree;
        RecalculateBonuses();

        if (raise) RaiseChanged();
    }

    /// <summary>
    /// Stellt auf den Baum dieses Charakters um. Gehört überall dorthin, wo der
    /// Charakter gewechselt wird; hat der Charakter keinen eigenen Baum, greift
    /// der Notbaum aus <see cref="SkillTrees"/>.
    /// </summary>
    public static void SetActiveTreeForCharacter(int skinIndex)
    {
        Init();
        SetActiveTree(SkillTrees.ForCharacter(skinIndex), raise: true);
    }

    // ==================================================================
    //  Punkte
    // ==================================================================

    public static int Currency => Store.SkillCurrency;

    /// <summary>
    /// Skillpunkte gutschreiben. Läuft im Spiel in heissen Pfaden (jeder
    /// Miniboss-Kill, jedes freigeschaltete Achievement), deshalb wird hier nur
    /// vorgemerkt und gebündelt geschrieben - ein voller Dateischreibvorgang je
    /// Gegner wäre ein Ruckler. Das Wegschreiben taktet
    /// <see cref="AchievementRuntime"/>; wer es sofort auf der Platte braucht,
    /// ruft danach <see cref="Flush"/>.
    /// </summary>
    public static void AddCurrency(int amount)
    {
        Store.SkillCurrency += amount;
        Store.MarkDirty();
        RaiseChanged();
    }

    /// <summary>Schreibt vorgemerkte Änderungen sofort auf die Platte.</summary>
    public static void Flush() => Store.Flush();

    public static void SetCurrency(int amount)
    {
        Store.SkillCurrency = amount;
        Store.Save();
        RaiseChanged();
    }

    public static bool TrySpend(int amount)
    {
        if (Store.SkillCurrency < amount) return false;
        Store.SkillCurrency -= amount;
        Store.Save();
        return true;
    }

    // ==================================================================
    //  Freischalten
    // ==================================================================

    /// <summary>
    /// Ist der Knoten offen? Der Startknoten einer Kategorie ist es immer - er
    /// ist nur der Anker, an dem die drei Bahnen haengen, und steht darum auch
    /// nicht im Spielstand.
    /// </summary>
    public static bool IsUnlocked(SkillNodeDef node)
        => node != null && (node.IsStart || Store.IsUnlocked(node.Branch.Tree.Id, node.Key));

    /// <summary>Alle Vorbedingungen offen? Sagt noch nichts über den Preis.</summary>
    public static bool RequirementsMet(SkillNodeDef node)
    {
        if (node == null) return false;

        foreach (SkillNodeDef parent in node.Requires)
        {
            if (!IsUnlocked(parent)) return false;
        }

        return true;
    }

    public static bool CanUnlock(SkillNodeDef node)
        => node != null && !IsUnlocked(node) && RequirementsMet(node) && Currency >= node.Price;

    /// <summary>Kauft einen Knoten. False, wenn gesperrt, schon offen oder zu teuer.</summary>
    public static bool TryUnlock(SkillNodeDef node)
    {
        if (node == null || IsUnlocked(node)) return false;

        if (!RequirementsMet(node))
        {
            Debug.Log($"[Skills] '{node.Key}' ist noch gesperrt.");
            return false;
        }

        if (!TrySpend(node.Price))
        {
            Debug.Log($"[Skills] Nicht genug Skillpunkte für '{node.Key}' " +
                      $"({Currency} von {node.Price}).");
            return false;
        }

        Store.SetUnlocked(node.Branch.Tree.Id, node.Key, true);
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log($"[Skills] Freigeschaltet: {node.Key}");
        return true;
    }

    /// <summary>Setzt einen Baum zurück und erstattet die ausgegebenen Punkte.</summary>
    public static void ResetTree(SkillTreeDef tree)
    {
        Init();
        if (tree == null) return;

        int refund = 0;

        foreach (SkillNodeDef node in tree.AllNodes())
        {
            if (IsUnlocked(node)) refund += node.Price;
        }

        Store.ClearTree(tree.Id);
        Store.SkillCurrency += refund;
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log($"[Skills] '{tree.Id}' zurückgesetzt, {refund} Skillpunkte erstattet.");
    }

    /// <summary>Setzt jeden Baum zurück, ohne zu erstatten - für den harten Reset.</summary>
    public static void ResetEverything()
    {
        Init();

        foreach (SkillTreeDef tree in SkillTrees.All) Store.ClearTree(tree.Id);

        Store.SkillCurrency = 0;
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log("[Skills] Alle Bäume zurückgesetzt, Skillpunkte auf 0.");
    }

    public static void ResetActiveTree() => ResetTree(ActiveTree);

    // ==================================================================
    //  Boni
    // ==================================================================

    /// <summary>Summe aller freigeschalteten Knoten dieses Effekts im aktiven Baum.</summary>
    public static float Bonus(SkillType effect)
    {
        if (!initialized) Init();
        return bonuses.TryGetValue(effect, out float v) ? v : 0f;
    }

    public static int BonusInt(SkillType effect) => Mathf.RoundToInt(Bonus(effect));

    /// <summary>
    /// Ist dieser Schalter aus <see cref="SkillGrants"/> im aktiven Baum offen?
    /// Das ist die Frage, die Waffen und Spielregeln stellen:
    ///
    /// <code>
    ///   if (Skills.HasGrant(SkillGrants.ShurikookieVierRichtungen)) ...
    /// </code>
    /// </summary>
    public static bool HasGrant(string grantId)
    {
        if (string.IsNullOrEmpty(grantId)) return false;
        if (ActiveTree == null) return false;
        return grants.Contains(grantId);
    }

    /// <summary>Alle offenen Schalter - für Debug-Ausgaben und die Testszene.</summary>
    public static IEnumerable<string> ActiveGrants
    {
        get
        {
            if (ActiveTree == null) yield break;
            foreach (string g in grants) yield return g;
        }
    }

    private static void RecalculateBonuses()
    {
        bonuses.Clear();
        grants.Clear();

        if (activeTree == null) return;

        foreach (SkillNodeDef node in activeTree.AllNodes())
        {
            if (!IsUnlocked(node)) continue;

            foreach (SkillReward reward in node.Rewards)
            {
                if (reward.IsGrant)
                {
                    if (!string.IsNullOrEmpty(reward.GrantId)) grants.Add(reward.GrantId);
                    continue;
                }

                if (reward.Stat == SkillType.None) continue;

                bonuses.TryGetValue(reward.Stat, out float current);
                bonuses[reward.Stat] = current + reward.Value;
            }
        }
    }

    // ==================================================================

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (SkillNodeDef node in ActiveTree.AllNodes())
            {
                if (IsUnlocked(node)) n++;
            }
            return n;
        }
    }

    public static int TotalCount
    {
        get
        {
            int n = 0;
            foreach (SkillNodeDef _ in ActiveTree.AllNodes()) n++;
            return n;
        }
    }

    public static string SavePath => Store.SavePath;

    private static void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Skills] Fehler im Changed-Handler: {e}");
        }
    }
}
