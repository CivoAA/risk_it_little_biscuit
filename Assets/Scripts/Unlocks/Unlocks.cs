using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Unlock: eine Sache, die im Spiel erst auftaucht, nachdem man etwas
/// geschafft hat. Unlocks schalten selbst nichts frei - sie sind eine Fahne, auf
/// die sich andere beziehen (heute vor allem Shop-Einträge über
/// <c>requiredUnlock</c>).
/// </summary>
public sealed class UnlockDef
{
    /// <summary>Stabiler Schlüssel im Spielstand. Niemals ändern.</summary>
    public readonly string Id;

    public readonly string NameEn;
    public readonly string DescEn;

    /// <summary>Wie man ihn bekommt - die Zeile unten im Erfolgsbuch, auch solange er gesperrt ist.</summary>
    public readonly string HowEn;

    /// <summary>Dateiname in Assets/Resources/Unlocks/. Standard: die Id.</summary>
    public readonly string IconKey;

    /// <summary>Position im Katalog - entspricht der Reihenfolge in Unlocks.cs.</summary>
    public int Order { get; internal set; }

    /// <summary>Von Anfang an offen, ohne im Spielstand zu stehen - Platzhalter, bis es eine Bedingung gibt.</summary>
    public readonly bool ByDefault;

    public UnlockDef(string id, string nameEn, string descEn, string howEn, string iconKey, bool byDefault = false)
    {
        Id        = id;
        NameEn    = nameEn;
        DescEn    = descEn;
        HowEn     = howEn;
        IconKey   = string.IsNullOrEmpty(iconKey) ? id : iconKey;
        ByDefault = byDefault;
    }

    public bool IsUnlocked => Unlocks.IsUnlocked(this);

    public string Name => Loc.Get($"unlock.{Id}.name", NameEn);
    public string Description => Loc.Get($"unlock.{Id}.desc", DescEn);
    public string HowTo => Loc.Get($"unlock.{Id}.how", HowEn);
    public Sprite Icon => UnlockIcons.Get(IconKey);

    public override string ToString() => Id;
}

/// <summary>
/// DER UNLOCK-KATALOG und die Schnittstelle dazu.
///
/// Einen Unlock hinzufügen:
///   1. Eine Zeile unten schreiben (Id, Name, Text beim Freischalten, wie man
///      ihn bekommt - der letzte steht kurz unten im Erfolgsbuch, max. ~30 Zeichen).
///   2. An der Stelle, die ihn verdient, Unlocks.Grant(Unlocks.MeinDing) aufrufen.
///   3. Wer ihn abfragt (z.B. ein Shop-Eintrag), nimmt die Id als requiredUnlock.
///   4. Ein PNG namens [IconKey].png nach Assets/Resources/Unlocks/ legen.
///
/// Die Reihenfolge der Zeilen ist die Reihenfolge in der Unlock-Anzeige.
/// IDs niemals ändern - sie sind die Schlüssel in unlocks.json.
/// </summary>
public static class Unlocks
{
    // Muss textlich vor allen Def(...)-Feldern stehen.
    private static readonly List<UnlockDef> Registry = new List<UnlockDef>();

    // ==================================================================
    //  Welten
    // ==================================================================
    // Story: die naechste Welt geht auf, wenn man die davor geschafft hat.
    // Welche Karte was vergibt, steht in WorldChain weiter unten; die
    // Levelauswahl fragt ueber StoryFor nach.
    // Endless: ein Unlock fuer alle Welten, nach dem Finale (ReportFinaleDone).

    public static readonly UnlockDef StoryKitchen = Def(
        "unlock_story_kitchen",
        "Kitchen",
        "You beat the Forest - the Kitchen is now open!",
        "Beat the Forest story");

    public static readonly UnlockDef StoryGlacier = Def(
        "unlock_story_glacier",
        "Ice Cream Glacier",
        "You beat the Kitchen - the Ice Cream Glacier is now open!",
        "Beat the Kitchen story");

    public static readonly UnlockDef StoryGhost = Def(
        "unlock_story_ghost",
        "Haunted Forest",
        "You beat the Ice Cream Glacier - the Haunted Forest is now open!",
        "Beat the Ice Cream Glacier story");

    /// <summary>
    /// Endless in allen Welten: erst wenn die Story durch ist (alle fuenf
    /// Welten, das Herz des Verkohlten besiegt - StoryProgress.FinaleDone).
    /// Die alten Ids "unlock_endless_kitchen/forest/glacier/ghost" (Endless je
    /// Welt) nicht wiederverwenden.
    /// </summary>
    public static readonly UnlockDef Endless = Def(
        "unlock_endless",
        "Endless Mode",
        "You destroyed the Charred One's heart - Endless mode is now open in every world!",
        "Finish the story");

    // ==================================================================
    //  Charaktere
    // ==================================================================
    // Ritter und Mochi sind von Anfang an spielbar und haben keinen Unlock
    // (leer in Characters.UnlockIdByskin). Die alten Ids
    // "unlock_char_onion_knight" / "unlock_char_mochi" nicht wiederverwenden.

    /// <summary>Kommt mit "Meltdown" (Eiskaiser besiegt = Eisgletscher geschafft).</summary>
    public static readonly UnlockDef CharInari = Def(
        "unlock_char_inari",
        "Inari",
        "Inari joins your team!",
        "Defeat the Ice Emperor");

    /// <summary>Kommt mit "Long Live the King" (Schleimkoenig 10-mal besiegt).</summary>
    public static readonly UnlockDef CharSlimeKing = Def(
        "unlock_char_slime_king",
        "Slime King",
        "The Slime King joins your team!",
        "Defeat the Slime King 10 times");

    /// <summary>Kommt mit "Full Bento" (mit jedem Charakter ein Sieg).</summary>
    public static readonly UnlockDef CharTofu = Def(
        "unlock_char_tofu",
        "Tofu",
        "Tofu joins your team!",
        "Win with every character");

    // ==================================================================
    //  Shop-Erweiterungen
    // ==================================================================

    public static readonly UnlockDef WeaponSlot = Def(
        "unlock_weapon_slot",
        "Weapon Slot",
        "You unlocked the Weapon Slot upgrade in the shop!",
        "Reach level 30 in a run",
        icon: "weapon_slot");

    public static readonly UnlockDef BuffSlot = Def(
        "unlock_buff_slot",
        "Buff Slot",
        "You unlocked the Buff Slot upgrade in the shop!",
        "Reach level 10 in a run",
        icon: "buff_slot");

    /// <summary>
    /// Nach der ersten Evo. Einen Evo-Slot hat jeder von Anfang an
    /// (<see cref="Shop.BaseEvoSlots"/>), der Unlock oeffnet nur den Kauf weiterer.
    /// Die Id gab es frueher schon einmal - wer sie im Spielstand hat, behaelt sie.
    /// </summary>
    public static readonly UnlockDef EvoSlot = Def(
        "unlock_evo_slot",
        "Evo Slot",
        "Your first Evo! More Evo Slots are now available in the shop!",
        "Get your first Evo",
        icon: "evo_slot");

    /// <summary>Sobald ein Charakter <see cref="CompanionCharLevel"/> erreicht.</summary>
    public static readonly UnlockDef Companion = Def(
        "unlock_companion",
        "Companion",
        "A character reached level 10 - companions are now available in the shop!",
        "Get a character to level 10",
        icon: "companion");

    /// <summary>
    /// Kommt mit dem ersten besiegten Boss (Enemy.GrantRewards, Rolle Boss).
    /// Frueher am Erfolg "Death" - den Tod gibt es nicht mehr.
    /// </summary>
    public static readonly UnlockDef CursedSugar = Def(
        "unlock_cursed_sugar",
        "Cursed Sugar",
        "You defeated your first boss - the Cursed Sugar buff is now available in the shop!",
        "Defeat your first boss",
        icon: "buff_curse");

    /// <summary>Charakter-Level, ab dem der Begleiter im Shop auftaucht.</summary>
    public const int CompanionCharLevel = 10;

    // ==================================================================
    //  Ab hier nur noch Mechanik.
    // ==================================================================

    public static IReadOnlyList<UnlockDef> All => Registry;

    /// <summary>Gehoert der Unlock ins Spiel? Seit es keine Waffen im Shop mehr gibt, jeder bekannte.</summary>
    public static bool IsShown(UnlockDef def) => def != null;

    /// <summary>Feuert genau einmal je Unlock, wenn er aufgeht.</summary>
    public static event Action<UnlockDef> Granted;

    private static readonly Dictionary<string, UnlockDef> ById = new Dictionary<string, UnlockDef>();

    private static UnlockStore store;
    private static bool initialized;
    private static bool sandbox;

    /// <summary>Sandbox (Test-Szene): schaltet im Speicher frei, schreibt nichts.</summary>
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

        ById.Clear();
        foreach (UnlockDef def in Registry)
        {
            if (!ById.ContainsKey(def.Id)) ById.Add(def.Id, def);
        }

        store = new UnlockStore { ReadOnly = sandbox };
        store.Load();

        // Erste Evo -> weitere Evo-Slots im Shop. Ueber die Evo-Erfolge, die
        // feuern auf jedem Weg, auf dem eine Evo ins Spiel kommt.
        Achievements.Unlocked -= OnAchievementUnlocked;
        Achievements.Unlocked += OnAchievementUnlocked;
    }

    private static void OnAchievementUnlocked(AchievementDef def)
    {
        if (def == null) return;
        if (def.Category == AchievementCategory.Evolutions) Grant(EvoSlot);
        if (def.Category == AchievementCategory.Characters)
            CheckAllCharacters();
    }

    /// <summary>
    /// Zieht <see cref="Ach.AllCharacters"/> (Tofu) auf den Stand aus den Siegen
    /// nach. Ist alles da, schaltet der Erfolg den Tofu frei.
    /// </summary>
    public static void CheckAllCharacters()
    {
        AchievementDef all = Ach.AllCharacters;
        if (all.IsUnlocked) return;

        Achievements.SetCountedValue(all, Ach.AllCharactersDone());
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
        initialized = false;
        Init();
    }

    /// <summary>Liest den Spielstand neu ein (Profilwechsel, <see cref="SaveProfiles"/>).</summary>
    public static void Reload()
    {
        initialized = false;
        Init();
    }

    private static UnlockStore Store
    {
        get
        {
            if (!initialized) Init();
            return store;
        }
    }

    public static UnlockDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Init();
        return ById.TryGetValue(id, out UnlockDef def) ? def : null;
    }

    // ==================================================================

    public static bool IsUnlocked(UnlockDef def)
    {
        if (def == null) return false;
        // Endless haengt allein an der Story: ein Eintrag in unlocks.json
        // (Testen, Story zurueckgesetzt) oeffnet ihn nicht.
        if (def == Endless) return StoryProgress.FinaleDone;
        return def.ByDefault || Store.IsUnlocked(def.Id);
    }

    /// <summary>
    /// Abfrage über die Id. Eine unbekannte Id gilt als nicht freigeschaltet -
    /// damit versteckt sich ein Tippfehler nicht hinter "ist halt offen".
    /// </summary>
    public static bool IsUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        UnlockDef def = Find(id);
        return def != null ? IsUnlocked(def) : Store.IsUnlocked(id);
    }

    /// <summary>Schaltet frei. Ist er schon offen, passiert nichts.</summary>
    public static void Grant(UnlockDef def)
    {
        if (def == null) return;

        UnlockStore s = Store;
        if (s.IsUnlocked(def.Id)) return;

        s.SetUnlocked(def.Id, true);
        s.Save();

        try
        {
            Granted?.Invoke(def);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Unlocks] Fehler im Granted-Handler für '{def.Id}': {e}");
        }

        Debug.Log($"[Unlocks] Freigeschaltet: {def.Id}");
    }

    /// <summary>Freischalten über die Id - für Konsole und Cheats.</summary>
    public static void Grant(string id)
    {
        UnlockDef def = Find(id);

        if (def == null)
        {
            Debug.LogWarning($"[Unlocks] Unbekannte Id '{id}' - steht sie im Katalog (Unlocks.cs)?");
            return;
        }

        Grant(def);
    }

    public static void GrantAll()
    {
        foreach (UnlockDef def in Registry) Grant(def);
    }

    public static void ResetAll()
    {
        Store.ResetAll();
        Debug.Log("[Unlocks] Alle Unlocks zurückgesetzt.");
    }

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (UnlockDef def in Registry)
            {
                if (IsShown(def) && IsUnlocked(def)) n++;
            }
            return n;
        }
    }

    /// <summary>Alle, die gerade im Spiel vorkommen (<see cref="IsShown"/>).</summary>
    public static int TotalCount
    {
        get
        {
            int n = 0;
            foreach (UnlockDef def in Registry)
            {
                if (IsShown(def)) n++;
            }
            return n;
        }
    }

    public static string SavePath => Store.SavePath;

    // ==================================================================
    //  Welten-Kette
    // ==================================================================

    /// <summary>
    /// Map-ID (Levelauswahl) -> was ihre Story braucht und was ein Story-Sieg
    /// dort fuer die naechste Welt oeffnet. Reihenfolge wie in der
    /// Levelauswahl. Neue Welt = neue Zeile (und die alte letzte bekommt ihr Next).
    /// </summary>
    private static readonly (int MapId, UnlockDef Story, UnlockDef Next)[] WorldChain =
    {
        (3, null,         StoryKitchen),  // Wald
        (0, StoryKitchen, StoryGlacier),  // Kueche
        (5, StoryGlacier, StoryGhost),    // Eisgletscher
        (6, StoryGhost,   null),          // Geisterwald
    };

    /// <summary>Der Unlock, den die Story dieser Karte braucht. Null = immer offen.</summary>
    public static UnlockDef StoryFor(int mapId)
    {
        foreach (var w in WorldChain) if (w.MapId == mapId) return w.Story;
        return null;
    }

    /// <summary>Story-Sieg auf dieser Karte (GameManager.GameOver): die Story der naechsten Welt geht auf.</summary>
    public static void ReportStoryWin(int mapId)
    {
        foreach (var w in WorldChain)
            if (w.MapId == mapId) Grant(w.Next);
    }

    /// <summary>Das Herz des Verkohlten ist besiegt (StoryDirector): Endless geht ueberall auf.</summary>
    public static void ReportFinaleDone() => Grant(Endless);

    /// <summary>Begleiter im Shop, sobald irgendein Charakter <see cref="CompanionCharLevel"/> hat.</summary>
    public static void CheckCharLevels()
    {
        if (IsUnlocked(Companion)) return;
        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            if (tree != null && Skills.LevelOf(tree) >= CompanionCharLevel)
            {
                Grant(Companion);
                return;
            }
        }
    }

    /// <summary>
    /// Holt Unlocks nach, die ein alter Spielstand schon verdient hat - leise,
    /// ohne Meldung. Laeuft beim Betreten des Hubs (HubUI), nicht beim Start:
    /// beim Zuruecksetzen des Spielstands waeren die anderen Stores sonst noch
    /// nicht leer.
    /// </summary>
    public static void CatchUp()
    {
        UnlockStore s = Store;
        bool changed = false;

        void Give(UnlockDef def)
        {
            if (def == null || def.ByDefault || !IsShown(def) || s.IsUnlocked(def.Id)) return;
            s.SetUnlocked(def.Id, true);
            changed = true;
        }

        // Geschaffte Welten
        foreach (var w in WorldChain)
        {
            if (LevelRecords.StoryBest(w.MapId) <= 0f) continue;
            Give(w.Next);
        }
        if (StoryProgress.FinaleDone) Give(Endless);
        else if (s.IsUnlocked(Endless.Id))
        {
            // Alter Eintrag ohne geschaffte Story weg - sonst bliebe beim echten
            // Finale die Meldung "Endless ist offen" aus (Grant sieht ihn schon).
            s.SetUnlocked(Endless.Id, false);
            changed = true;
        }
        if (StoryProgress.FinaleDone) Achievements.Unlock(Ach.StoryComplete);

        // Erfolge mit Unlock (z.B. Eiskaiser -> Inari) und die erste Evo
        foreach (AchievementDef def in Ach.All)
        {
            if (!def.IsUnlocked) continue;
            if (!string.IsNullOrEmpty(def.GrantsUnlock)) Give(Find(def.GrantsUnlock));
            if (def.Category == AchievementCategory.Evolutions) Give(EvoSlot);
        }

        // Schon einmal einen Boss besiegt -> Cursed Sugar
        // (Nicht Ach.BossPhantom - das ist trotz Namen "100 Elites besiegen".)
        if (Ach.Bosses5.Value >= 1f || Ach.BossCookieKing.IsUnlocked || Ach.BossEmberroot.IsUnlocked
            || Ach.BossIceEmperor.IsUnlocked)
        {
            Give(CursedSugar);
        }

        // Schon Gekauftes bleibt sichtbar
        foreach (ShopItemDef item in Shop.All)
        {
            if (!string.IsNullOrEmpty(item.RequiredUnlock) && Shop.LevelOf(item) > 0) Give(Find(item.RequiredUnlock));
        }

        // Charakter-Level
        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            if (tree != null && Skills.LevelOf(tree) >= CompanionCharLevel) Give(Companion);
        }

        if (changed) s.Save();

        // Tofu-Zaehler aus schon vorhandenen Evo-Erfolgen
        CheckAllCharacters();
    }

    private static UnlockDef Def(string id, string nameEn, string descEn, string howEn, string icon = null, bool byDefault = false)
    {
        UnlockDef def = new UnlockDef(id, nameEn, descEn, howEn, icon, byDefault) { Order = Registry.Count };
        Registry.Add(def);
        return def;
    }
}
