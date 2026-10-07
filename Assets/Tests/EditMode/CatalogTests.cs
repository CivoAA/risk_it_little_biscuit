#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Prüft die vier Code-Kataloge und ihre Spielstände, ohne den echten Spielstand
/// anzufassen: die Store-Klassen bekommen dafür ein Wegwerf-Verzeichnis.
///
/// Was hier abgesichert wird, ist genau das, was beim Hinzufügen neuer Inhalte
/// schiefgehen kann - doppelte Schlüssel, krumme Preislisten, kaputte Bäume und
/// Spielstände, die beim Erweitern des Katalogs alten Fortschritt verlieren.
/// </summary>
public class CatalogTests
{
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "rifb_tests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(tempDir, true); } catch { /* egal */ }
    }

    // ==================================================================
    //  Achievements
    // ==================================================================

    [Test]
    public void Achievements_Ids_sind_eindeutig_und_steamtauglich()
    {
        var seen = new HashSet<string>();

        foreach (AchievementDef def in Ach.All)
        {
            Assert.IsTrue(seen.Add(def.Id), $"Doppelte Achievement-Id '{def.Id}'.");
            StringAssert.IsMatch("^[A-Za-z0-9_]+$", def.Id,
                $"Achievement-Id '{def.Id}' enthält Zeichen, die Steam nicht erlaubt.");
            Assert.Greater(def.Goal, 0f, $"'{def.Id}' hat kein sinnvolles Ziel.");
        }
    }

    [Test]
    public void Achievements_Spielstand_uebersteht_neue_Eintraege()
    {
        var store = new AchievementStore(tempDir);
        store.Load();

        store.SetUnlocked(Ach.FirstWin.Id, true);
        store.SetValue(Ach.Kill100.Id, 57f);

        // Ein Eintrag, den der Katalog nicht kennt - z.B. ein später entferntes
        // Achievement. Der darf beim Speichern nicht verloren gehen.
        store.SetUnlocked("Ein_Altes_Achievement", true);
        store.Save();

        var reloaded = new AchievementStore(tempDir);
        reloaded.Load();

        Assert.IsTrue(reloaded.IsUnlocked(Ach.FirstWin.Id), "Freigeschaltetes ging verloren.");
        Assert.AreEqual(57f, reloaded.GetValue(Ach.Kill100.Id), 0.01f, "Fortschritt ging verloren.");
        Assert.IsTrue(reloaded.IsUnlocked("Ein_Altes_Achievement"),
            "Unbekannte Id wurde beim Speichern weggeworfen - ein entferntes und wieder " +
            "hinzugefügtes Achievement würde seinen Fortschritt verlieren.");
    }

    // ==================================================================
    //  Shop
    // ==================================================================

    [Test]
    public void Shop_Preise_und_Werte_passen_zusammen()
    {
        var seen = new HashSet<string>();
        var seenWeapons = new HashSet<string>();

        foreach (ShopItemDef def in Shop.All)
        {
            Assert.IsTrue(seen.Add(def.Id), $"Doppelte Shop-Id '{def.Id}'.");
            Assert.Greater(def.Costs.Count, 0, $"'{def.Id}' hat keine Preise.");

            Assert.AreEqual(def.Costs.Count + 1, def.Values.Count,
                $"'{def.Id}': {def.Costs.Count} Preise brauchen {def.Costs.Count + 1} Werte " +
                "(Stufe 0 zählt mit).");

            Assert.AreEqual(0f, def.Values[0], 0.001f,
                $"'{def.Id}': Stufe 0 muss den Wert 0 haben, sonst wirkt der Eintrag ungekauft.");

            if (!string.IsNullOrEmpty(def.UnlocksWeapon))
            {
                Assert.IsTrue(seenWeapons.Add(def.UnlocksWeapon),
                    $"Waffe '{def.UnlocksWeapon}' hängt an mehr als einem Shop-Eintrag.");
            }
        }
    }

    [Test]
    public void Shop_verlangt_nur_Unlocks_die_es_gibt()
    {
        foreach (ShopItemDef def in Shop.All)
        {
            if (string.IsNullOrWhiteSpace(def.RequiredUnlock)) continue;

            Assert.IsNotNull(Unlocks.Find(def.RequiredUnlock),
                $"'{def.Id}' verlangt Unlock '{def.RequiredUnlock}', den es im Katalog nicht gibt - " +
                "der Eintrag wäre nie sichtbar.");
        }
    }

    [Test]
    public void Shop_Spielstand_erstattet_bei_gesenkten_Preisen()
    {
        var store = new ShopStore(tempDir);
        store.Load();

        ShopItemDef item = Shop.Rerolls;

        // Zwei Stufen gekauft, aber absichtlich zu viel bezahlt - so als wären die
        // Preise nach dem Kauf gesenkt worden.
        store.SetLevel(item.Id, 2);
        store.AddSpent(item.Id, item.TotalCostUpTo(2) + 500);
        store.Currency = 0;
        store.Save();

        var reloaded = new ShopStore(tempDir);
        reloaded.Load();

        Assert.AreEqual(2, reloaded.LevelOf(item.Id), "Die gekaufte Stufe ging verloren.");
        Assert.AreEqual(500, reloaded.Currency,
            "Die Differenz aus gesenkten Preisen wurde nicht erstattet.");
    }

    [Test]
    public void Shop_Spielstand_behaelt_unbekannte_Eintraege()
    {
        var store = new ShopStore(tempDir);
        store.Load();

        store.SetLevel("ein_altes_item", 3);
        store.Save();

        var reloaded = new ShopStore(tempDir);
        reloaded.Load();

        Assert.AreEqual(3, reloaded.LevelOf("ein_altes_item"),
            "Ein Eintrag, den der Katalog nicht mehr kennt, wurde weggeworfen.");
    }

    // ==================================================================
    //  Skills
    // ==================================================================

    [Test]
    public void Skills_Schluessel_sind_eindeutig()
    {
        var seen = new HashSet<string>();

        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillNodeDef node in tree.AllNodes())
            {
                Assert.IsTrue(seen.Add(node.Key),
                    $"Doppelter Skill-Schlüssel '{node.Key}' - der Spielstand könnte die beiden " +
                    "nicht auseinanderhalten.");
            }
        }
    }

    [Test]
    public void Skills_jeder_Ast_faengt_am_Startknoten_an()
    {
        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillBranchDef branch in tree.Branches)
            {
                Assert.Greater(branch.Nodes.Count, 0, $"Ast '{branch.Id}' ist leer.");

                int roots = 0;
                SkillNodeDef start = null;
                foreach (SkillNodeDef node in branch.Nodes)
                {
                    if (node.IsStart) start = node;
                    if (node.IsRoot) roots++;

                    foreach (SkillNodeDef parent in node.Requires)
                    {
                        Assert.AreSame(branch, parent.Branch,
                            $"'{node.Key}' hängt an '{parent.Key}' aus einer anderen Kategorie.");
                        // Gleiche Spalte ist erlaubt (senkrechte Verbindung zwischen Bahnen).
                        Assert.LessOrEqual(parent.Step, node.Step,
                            $"'{node.Key}' steht links von seiner Vorbedingung " +
                            $"'{parent.Key}' - die Linie liefe rückwärts.");
                    }
                }

                Assert.NotNull(start,
                    $"Ast '{branch.Id}' hat keinen Startknoten - dort faengt jede Kategorie an.");

                // Genau ein Anfang: der Startknoten. Alles andere haengt daran,
                // sonst waere es im Hub ohne Linie sofort kaufbar.
                Assert.AreEqual(1, roots,
                    $"Ast '{branch.Id}' hat {roots} Knoten ohne Vorbedingung - erlaubt ist nur " +
                    "der Startknoten.");
            }
        }
    }

    [Test]
    public void Skills_Layout_legt_keine_zwei_Knoten_aufeinander()
    {
        foreach (SkillTreeDef tree in SkillTrees.All)
        {
            foreach (SkillBranchDef branch in tree.Branches)
            {
                var used = new HashSet<Vector2>();

                foreach (SkillNodeDef node in branch.Nodes)
                {
                    Vector2 pos = SkillTreeLayout.PositionOf(node);
                    Assert.IsTrue(used.Add(pos),
                        $"Zwei Knoten im Ast '{branch.Id}' landen auf derselben Stelle {pos} " +
                        $"(zuletzt '{node.Key}').");
                }
            }
        }
    }

    [Test]
    public void Skills_Spielstand_haelt_Baeume_getrennt()
    {
        var store = new SkillStore(tempDir);
        store.Load();

        store.SetUnlocked("default", "default.wind.move_speed_1", true);
        store.SetUnlocked("char_1", "char_1.wind.move_speed_1", true);
        store.SetXp("char_1", 4200);
        store.Save();

        var reloaded = new SkillStore(tempDir);
        reloaded.Load();

        Assert.AreEqual(4200, reloaded.XpOf("char_1"), 0.001);
        Assert.AreEqual(0, reloaded.XpOf("default"), 0.001, "XP gehoeren dem Charakter, nicht allen.");
        Assert.IsTrue(reloaded.IsUnlocked("default", "default.wind.move_speed_1"));
        Assert.IsTrue(reloaded.IsUnlocked("char_1", "char_1.wind.move_speed_1"));
        Assert.IsFalse(reloaded.IsUnlocked("default", "char_1.wind.move_speed_1"),
            "Die Bäume dürfen sich nicht gegenseitig freischalten.");
    }

    [Test]
    public void Skills_Boss_Erstsieg_zaehlt_einmal_je_Charakter()
    {
        var store = new SkillStore(tempDir);
        store.Load();

        Assert.IsTrue(store.AddBoss("char_0", "KeksKoenig"));
        Assert.IsFalse(store.AddBoss("char_0", "KeksKoenig"), "Derselbe Boss gibt nur einmal ein Level.");
        Assert.IsTrue(store.AddBoss("char_1", "KeksKoenig"), "Jeder Charakter muss selbst siegen.");
        store.Save();

        var reloaded = new SkillStore(tempDir);
        reloaded.Load();

        Assert.AreEqual(1, reloaded.BossCountOf("char_0"));
        Assert.AreEqual(1, reloaded.BossCountOf("char_1"));
        Assert.AreEqual(0, reloaded.BossCountOf("default"));
    }

    [Test]
    public void Gegner_Ids_behalten_ihre_Zahlen()
    {
        // Die Prefabs speichern die Id als Zahl. Verschiebt sich eine, ist am
        // Prefab ploetzlich ein anderer Gegner eingetragen.
        Assert.AreEqual(2,  (int)EnemyId.EliteMarshmello);
        Assert.AreEqual(13, (int)EnemyId.EliteFluegdolch);
        Assert.AreEqual(16, (int)EnemyId.KeksKoenig);
        Assert.AreEqual(22, (int)EnemyId.WeisseMessermaus);
        Assert.AreEqual(25, (int)EnemyId.EliteFliegenpilz);
        Assert.AreEqual(28, (int)EnemyId.Glutwurz);
    }

    [Test]
    public void CharLevel_Kurve_trifft_die_Ankerpunkte()
    {
        Assert.AreEqual(0, CharLevel.LevelFor(0));
        Assert.AreEqual(0, CharLevel.LevelFor(1749));
        Assert.AreEqual(1, CharLevel.LevelFor(1750), "Level 1 = Ingame-Level 15.");
        Assert.AreEqual(1, CharLevel.LevelFor(16199));
        Assert.AreEqual(2, CharLevel.LevelFor(16200), "Level 2 = Ingame-Level 30.");
        Assert.AreEqual(3, CharLevel.LevelFor(34830));

        for (int level = 1; level < 50; level++)
        {
            Assert.AreEqual(level, CharLevel.LevelFor(CharLevel.XpForLevel(level)));
            Assert.Greater(CharLevel.StepXp(level + 1), CharLevel.StepXp(level),
                "Jedes Level muss teurer sein als das davor.");
        }
    }

    // ==================================================================
    //  Unlocks
    // ==================================================================

    [Test]
    public void Unlocks_Ids_sind_eindeutig()
    {
        var seen = new HashSet<string>();

        foreach (UnlockDef def in Unlocks.All)
        {
            Assert.IsTrue(seen.Add(def.Id), $"Doppelte Unlock-Id '{def.Id}'.");
            StringAssert.IsMatch("^[a-z0-9_]+$", def.Id, $"Unlock-Id '{def.Id}' ist krumm.");
        }
    }

    [Test]
    public void Unlocks_Spielstand_uebersteht_eine_Runde()
    {
        var store = new UnlockStore(tempDir);
        store.Load();

        store.SetUnlocked(Unlocks.BobaGun.Id, true);
        store.SetUnlocked("unlock_irgendwas_altes", true);
        store.Save();

        var reloaded = new UnlockStore(tempDir);
        reloaded.Load();

        Assert.IsTrue(reloaded.IsUnlocked(Unlocks.BobaGun.Id));
        Assert.IsTrue(reloaded.IsUnlocked("unlock_irgendwas_altes"),
            "Eine unbekannte Unlock-Id wurde weggeworfen.");
    }

    // ==================================================================
    //  Übersetzung
    // ==================================================================

    [Test]
    public void Jeder_Katalogeintrag_hat_englische_Texte()
    {
        foreach (AchievementDef def in Ach.All)
        {
            Assert.IsNotEmpty(def.NameEn, $"Achievement '{def.Id}' hat keinen Namen.");
            Assert.IsNotEmpty(def.DescEn, $"Achievement '{def.Id}' hat keine Beschreibung.");
        }

        foreach (ShopItemDef def in Shop.All)
        {
            Assert.IsNotEmpty(def.NameEn, $"Shop-Eintrag '{def.Id}' hat keinen Namen.");
        }

        foreach (UnlockDef def in Unlocks.All)
        {
            Assert.IsNotEmpty(def.NameEn, $"Unlock '{def.Id}' hat keinen Namen.");
        }
    }

    // ==================================================================
    //  Wellenplaene
    // ==================================================================

    [Test]
    public void World1_benutzt_keine_archivierten_Gegner()
    {
        AssertNoArchived(WavePlans.World1());
    }

    /// <summary>Kueche: Demo-Skalierung auf 20 Minuten gestreckt, Keks-Koenig bei 20:00.</summary>
    [Test]
    public void World1_Keks_Koenig_bei_20_Minuten()
    {
        RunPlan plan = WavePlans.World1();

        float start = 0f;
        float bossAt = -1f;
        EnemyId boss = EnemyId.None;
        foreach (Phase phase in plan.Phases)
        {
            foreach (Beat beat in phase.Beats)
                if (beat.Kind == BeatKind.Boss) { bossAt = start + beat.Time; boss = beat.Enemy; }
            start += phase.Duration;
        }

        Assert.AreEqual(1200f, bossAt, 0.01f, "Boss soll bei 20:00 kommen.");
        Assert.AreEqual(EnemyId.KeksKoenig, boss);
    }

    /// <summary>
    /// Der Wald spielt nur aktive Gegner. Archivierte spawnen zwar weiter,
    /// wenn ein Plan sie nennt (siehe EnemyDef.Archived) - genau deshalb
    /// faellt es sonst niemandem auf. Der Kaefig zaehlt mit: er setzt
    /// Blocker, und die Kaefig-Wand liegt im Archiv.
    /// </summary>
    [Test]
    public void World2_benutzt_keine_archivierten_Gegner()
    {
        AssertNoArchived(WavePlans.World2());
    }

    [Test]
    public void World2Demo_benutzt_keine_archivierten_Gegner()
    {
        AssertNoArchived(WavePlans.World2Demo());
    }

    /// <summary>Demo: Boss bei 13:00, danach beendet der Verkohlte als Tod den Lauf.</summary>
    [Test]
    public void World2Demo_Boss_bei_13_Minuten_und_Verkohlter_Tod_danach()
    {
        RunPlan plan = WavePlans.World2Demo();

        float start = 0f;
        float bossAt = -1f;
        foreach (Phase phase in plan.Phases)
        {
            foreach (Beat beat in phase.Beats)
                if (beat.Kind == BeatKind.Boss) bossAt = start + beat.Time;
            start += phase.Duration;
        }

        Assert.AreEqual(780f, bossAt, 0.01f, "Boss soll bei 13:00 kommen.");
        Assert.AreEqual(EnemyId.VerkohlterTod, plan.Finisher);
        Assert.AreEqual(EnemyRole.DeathBoss, EnemyCatalog.Get(EnemyId.VerkohlterTod).Role);
    }

    [Test]
    public void Wald_Schalter_zeigt_auf_einen_Waldplan()
    {
        Assert.That(WavePlans.WaldPlan, Is.EqualTo("World2").Or.EqualTo("World2Demo"));
        Assert.AreEqual(WavePlans.WaldPlan, WavePlans.ForMap("World2").Id);
    }

    private static void AssertNoArchived(RunPlan plan)
    {
        AssertActive(plan.Finisher, "Nach dem Boss");

        var phases = new List<Phase>(plan.Phases);
        if (plan.Endless != null) phases.Add(plan.Endless);

        for (int p = 0; p < phases.Count; p++)
        {
            Phase phase = phases[p];

            foreach (PoolEntry entry in phase.Enemies)
                AssertActive(entry.Id, $"Phase {p + 1}, Pool");

            foreach (Beat beat in phase.Beats)
            {
                string where = $"Phase {p + 1}, {beat.Kind} bei {beat.Time:0}s";
                AssertActive(beat.Enemy, where);
                AssertActive(beat.RingEnemy, where);
                if (beat.Cage) AssertActive(EnemyId.Blocker, where + " (Kaefig)");
            }
        }
    }

    private static void AssertActive(EnemyId id, string where)
    {
        if (id == EnemyId.None) return;

        EnemyDef def = EnemyCatalog.Get(id);
        Assert.IsNotNull(def, $"{where}: {id} steht nicht im Gegner-Katalog.");
        Assert.IsFalse(def.Archived, $"{where}: {id} ist archiviert.");
    }
}
#endif
