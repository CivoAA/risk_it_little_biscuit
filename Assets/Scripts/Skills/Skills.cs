using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Schnittstelle zum Skilltree. Statisch, ohne Szenenobjekt, ab dem ersten
/// Frame verfügbar.
///
///   Skills.Level / Points      - Charakter-Level und freie Skillpunkte
///   Skills.AddXp(xp)           - Charakter-XP aus dem Lauf gutschreiben
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

    /// <summary>
    /// Loescht den Spielstand auf der Platte und laedt leer neu. Nur fuer
    /// <see cref="SaveReset"/> (Optionen -> SPIEL -> Spielstand zuruecksetzen).
    /// In der Sandbox bleibt die Datei liegen.
    /// </summary>
    public static void ResetProgress()
    {
        Init();
        if (!sandbox && System.IO.File.Exists(store.SavePath)) System.IO.File.Delete(store.SavePath);
        activeTree = null;
        bonuses.Clear();
        grants.Clear();
        initialized = false;
        Init();
        RaiseChanged();
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

    //  Jeder Charakter sammelt eigene XP (alles, was er im Lauf einsammelt).
    //  Jedes Charakter-Level gibt einen Skillpunkt für SEINEN Baum - die Kurve
    //  steht in CharLevel. Dazu ein Level je Boss, den der Charakter zum ersten
    //  Mal besiegt. Gespeichert werden XP und Boss-Siege; die freien Punkte sind
    //  Level minus die Preise der offenen Knoten.

    /// <summary>Gesammelte Charakter-XP des Baums.</summary>
    public static double XpOf(SkillTreeDef tree) => tree != null ? Store.XpOf(tree.Id) : 0;

    /// <summary>
    /// Charakter-Level = Level aus den XP plus eins je erstmals besiegtem Boss.
    /// Der Boss schiebt die Kurve also um ein Level: was vorher fuer Level 10
    /// gefehlt hat, fehlt danach fuer Level 11 - die XP-Schwellen bleiben, wo
    /// sie sind, und das Maximum ist mit weniger XP erreicht.
    /// </summary>
    public static int LevelOf(SkillTreeDef tree) => CharLevel.LevelFor(XpOf(tree)) + BossLevelsOf(tree);

    /// <summary>Geschenkte Level durch Boss-Erstsiege dieses Charakters.</summary>
    public static int BossLevelsOf(SkillTreeDef tree) => tree != null ? Store.BossCountOf(tree.Id) : 0;

    /// <summary>
    /// Ein Boss ist gefallen. Beim ERSTEN Sieg des gewaehlten Charakters ueber
    /// diesen Boss gibt es ein Charakter-Level; jeder Charakter muss jeden Boss
    /// selbst einmal schlagen. True, wenn es das Level gab.
    /// </summary>
    public static bool RegisterBossVictory(string bossKey)
    {
        SkillTreeDef tree = ActiveTree;
        if (tree == null || string.IsNullOrEmpty(bossKey)) return false;
        if (!Store.AddBoss(tree.Id, bossKey)) return false;

        Store.Save();
        RaiseChanged();

        Debug.Log($"[Skills] Erster Sieg ueber {bossKey} mit '{tree.Id}': +1 Charakter-Level.");
        return true;
    }

    /// <summary>Vergisst die Boss-Siege eines Baums (Konsole, zum Testen).</summary>
    public static void ResetBossVictories(SkillTreeDef tree)
    {
        if (tree == null) return;
        Store.ClearBosses(tree.Id);
        Store.Save();
        RaiseChanged();
    }

    /// <summary>Hat der gewaehlte Charakter diesen Boss schon einmal besiegt?</summary>
    public static bool HasBeatenBoss(string bossKey)
        => ActiveTree != null && Store.HasBoss(ActiveTree.Id, bossKey);

    /// <summary>Für Knoten ausgegebene Punkte im Baum.</summary>
    public static int SpentIn(SkillTreeDef tree)
    {
        if (tree == null) return 0;

        int spent = 0;
        foreach (SkillNodeDef node in tree.AllNodes())
        {
            if (!node.IsStart && IsUnlocked(node)) spent += node.Price;
        }
        return spent;
    }

    /// <summary>Freie Skillpunkte im Baum.</summary>
    public static int PointsIn(SkillTreeDef tree) => LevelOf(tree) - SpentIn(tree);

    public static double Xp    => XpOf(ActiveTree);
    public static int    Level => LevelOf(ActiveTree);

    /// <summary>Freie Skillpunkte des gewählten Charakters.</summary>
    public static int Points => PointsIn(ActiveTree);

    /// <summary>
    /// Charakter-XP für den gewählten Charakter gutschreiben. Läuft im Spiel bei
    /// jedem eingesammelten XP-Kristall, deshalb wird nur vorgemerkt und
    /// gebündelt geschrieben. Das Wegschreiben taktet
    /// <see cref="AchievementRuntime"/>; wer es sofort auf der Platte braucht,
    /// ruft danach <see cref="Flush"/>. <see cref="Changed"/> feuert nur, wenn
    /// sich dabei das Level ändert.
    /// </summary>
    public static void AddXp(double amount)
    {
        SkillTreeDef tree = ActiveTree;
        if (tree == null || amount <= 0) return;

        double before = Store.XpOf(tree.Id);
        Store.SetXp(tree.Id, before + amount);
        Store.MarkDirty();

        if (CharLevel.LevelFor(before) != CharLevel.LevelFor(before + amount)) RaiseChanged();
    }

    /// <summary>Setzt die XP eines Baums direkt (Konsole).</summary>
    public static void SetXp(SkillTreeDef tree, double xp)
    {
        if (tree == null) return;
        Store.SetXp(tree.Id, xp);
        Store.Save();
        RaiseChanged();
    }

    /// <summary>Schreibt vorgemerkte Änderungen sofort auf die Platte.</summary>
    public static void Flush() => Store.Flush();

    // ==================================================================
    //  Freischalten
    // ==================================================================

    /// <summary>
    /// Ist der Knoten offen? Der Startknoten einer Kategorie ist es immer - er
    /// ist nur der Anker, an dem die drei Bahnen haengen, und steht darum auch
    /// nicht im Spielstand.
    /// </summary>
    public static bool IsUnlocked(SkillNodeDef node)
        => node != null && (node.IsStart || (!IsDemoLocked(node) && Store.IsUnlocked(node.Branch.Tree.Id, node.Key)));

    /// <summary>
    /// Liegt der Knoten in einer Kategorie, die die Demo sperrt? Dann zaehlt
    /// er weder als gelernt noch laesst er sich lernen - sichtbar bleibt er.
    /// </summary>
    public static bool IsDemoLocked(SkillNodeDef node)
        => node != null && node.Branch != null && Demo.IsSkillCategoryLocked(node.Branch.Category);

    /// <summary>Alle Vorbedingungen offen? Sagt noch nichts über den Preis.</summary>
    public static bool RequirementsMet(SkillNodeDef node)
    {
        if (node == null) return false;
        if (IsDemoLocked(node)) return false;

        foreach (SkillNodeDef parent in node.Requires)
        {
            if (!IsUnlocked(parent)) return false;
        }

        return true;
    }

    public static bool CanUnlock(SkillNodeDef node)
        => node != null && !IsUnlocked(node) && RequirementsMet(node) && CanAfford(node);

    /// <summary>Genug freie Punkte im Baum dieses Knotens?</summary>
    public static bool CanAfford(SkillNodeDef node)
        => node != null && PointsIn(node.Branch.Tree) >= node.Price;

    /// <summary>Kauft einen Knoten. False, wenn gesperrt, schon offen oder zu teuer.</summary>
    public static bool TryUnlock(SkillNodeDef node)
    {
        if (node == null || IsUnlocked(node)) return false;

        if (!RequirementsMet(node))
        {
            Debug.Log($"[Skills] '{node.Key}' ist noch gesperrt.");
            return false;
        }

        if (!CanAfford(node))
        {
            Debug.Log($"[Skills] Nicht genug Skillpunkte für '{node.Key}' " +
                      $"({PointsIn(node.Branch.Tree)} von {node.Price}).");
            return false;
        }

        Store.SetUnlocked(node.Branch.Tree.Id, node.Key, true);
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log($"[Skills] Freigeschaltet: {node.Key}");
        return true;
    }

    /// <summary>
    /// Setzt einen Baum zurück. Die Punkte sind danach von selbst wieder frei,
    /// weil sie aus dem Level gerechnet werden - das Level bleibt.
    /// </summary>
    public static void ResetTree(SkillTreeDef tree)
    {
        Init();
        if (tree == null) return;

        int refund = SpentIn(tree);

        Store.ClearTree(tree.Id);
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log($"[Skills] '{tree.Id}' zurückgesetzt, {refund} Skillpunkte wieder frei.");
    }

    /// <summary>Setzt jeden Baum UND alle Charakter-XP zurück - für den harten Reset.</summary>
    public static void ResetEverything()
    {
        Init();

        Store.ClearAll();
        Store.Save();

        RecalculateBonuses();
        RaiseChanged();

        Debug.Log("[Skills] Alle Bäume und Charakter-Level zurückgesetzt.");
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

    /// <summary>
    /// Ist der Schalter in IRGENDEINEM Baum offen, egal welcher Charakter gerade
    /// gewählt ist? Für Anzeigen, die man einmal freischaltet und dann immer
    /// sieht (z.B. den Bestiarium-Tab).
    /// </summary>
    public static bool HasGrantInAnyTree(string grantId)
    {
        if (string.IsNullOrEmpty(grantId)) return false;
        if (!initialized) Init();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillNodeDef node in tree.AllNodes())
            {
                if (!IsUnlocked(node)) continue;
                foreach (SkillReward reward in node.Rewards)
                {
                    if (reward.IsGrant && reward.GrantId == grantId) return true;
                }
            }
        }
        return false;
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
