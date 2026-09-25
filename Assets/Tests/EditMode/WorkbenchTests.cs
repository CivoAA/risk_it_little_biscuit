#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueft die Werkbank: den Katalog, seine Abschrift-Beziehung zum
/// Player-Prefab, die Icons und die Regeln des Verteilers.
///
/// Der wichtigste Test hier ist <see cref="Katalog_deckt_sich_mit_dem_Player_Prefab"/>.
/// <see cref="WeaponCatalog"/> ist eine Abschrift der Weapon-Bauteile am
/// Prefab - Abschriften laufen auseinander, und zwar leise: eine neue Waffe
/// taucht dann einfach nicht in der Werkbank auf, und niemand merkt es.
///
/// Der echte Spielstand wird nicht angefasst: <see cref="Loadout.SandboxMode"/>
/// schaltet das Schreiben ab, die Store-Tests bekommen ein Wegwerf-Verzeichnis.
/// </summary>
public class WorkbenchTests
{
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string IconFolder = "Assets/Resources/Workbench";

    private string tempDir;
    private int skinBefore;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "rifb_wb_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // Der Verteiler haengt am Charakter, also wechseln Tests ihn auch -
        // Shop und Skills muessen dabei stumm sein, sonst landet der Wechsel
        // im echten Spielstand.
        Shop.SandboxMode = true;
        Skills.SandboxMode = true;
        skinBefore = Shop.SkinIndex;

        // Erst stummschalten, dann leeren - sonst schriebe das Leeren in die
        // echte loadout.json.
        Loadout.SandboxMode = true;
        Loadout.ResetAll();
    }

    [TearDown]
    public void TearDown()
    {
        // Noch im Sandbox-Modus zurueckstellen, damit der Wechsel nirgends
        // hin geschrieben wird.
        Shop.SkinIndex = skinBefore;
        Loadout.ResetAll();

        // Den Sandbox-Modus wieder abschalten und die echte Datei nachladen -
        // sonst laeuft der Rest der Editor-Sitzung mit leerem, nicht
        // speicherndem Verteiler weiter.
        Loadout.SandboxMode = false;
        Skills.SandboxMode = false;
        Shop.SandboxMode = false;
        Loadout.Reload();

        try { Directory.Delete(tempDir, true); } catch { /* egal */ }
    }

    // ==================================================================
    //  Katalog
    // ==================================================================

    [Test]
    public void Katalog_Ids_sind_eindeutig_und_passen_zur_Art()
    {
        var seen = new HashSet<string>();

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            Assert.IsTrue(seen.Add(def.Id), $"Doppelte Waffen-Id '{def.Id}'.");
            StringAssert.IsMatch("^[a-z0-9_]+$", def.Id,
                $"Waffen-Id '{def.Id}' passt nicht zum Schema der weaponIDs.");

            // Die Praefixe sind keine Kosmetik: PlayerController und die
            // Werkbank leiten daraus ab, was eine Waffe und was ein Buff ist.
            if (def.Kind == PoolKind.Buff)
                StringAssert.StartsWith("buff_", def.Id, $"'{def.Id}' ist als Buff gefuehrt.");
            else if (def.Kind == PoolKind.Evo)
                StringAssert.StartsWith("evo_", def.Id, $"'{def.Id}' ist als Evo gefuehrt.");
            else
                Assert.IsFalse(def.Id.StartsWith("buff_") || def.Id.StartsWith("evo_"),
                    $"'{def.Id}' ist als Waffe gefuehrt, heisst aber nicht so.");
        }
    }

    [Test]
    public void Jedes_Rezept_zeigt_auf_bekannte_Eintraege()
    {
        var seen = new HashSet<string>();

        foreach (EvoDef evo in WeaponCatalog.Evos)
        {
            Assert.IsTrue(seen.Add(evo.Id), $"Doppeltes Rezept fuer '{evo.Id}'.");

            Assert.IsNotNull(evo.Result, $"Rezept '{evo.Id}': Ergebnis steht nicht im Katalog.");
            Assert.AreEqual(PoolKind.Evo, evo.Result.Kind, $"'{evo.Id}' ist keine Evo.");

            Assert.IsNotNull(evo.A, $"Rezept '{evo.Id}': Zutat '{evo.IngredientA}' unbekannt.");
            Assert.IsNotNull(evo.B, $"Rezept '{evo.Id}': Zutat '{evo.IngredientB}' unbekannt.");

            // Eine Evo als Zutat einer Evo gaebe es im Spiel nicht - sie
            // entsteht erst im Lauf und liegt nie im Verteiler.
            Assert.AreNotEqual(PoolKind.Evo, evo.A.Kind, $"Rezept '{evo.Id}': Zutat ist eine Evo.");
            Assert.AreNotEqual(PoolKind.Evo, evo.B.Kind, $"Rezept '{evo.Id}': Zutat ist eine Evo.");

            Assert.AreNotEqual(evo.IngredientA, evo.IngredientB,
                $"Rezept '{evo.Id}' braucht zweimal dasselbe.");
        }
    }

    [Test]
    public void Katalog_deckt_sich_mit_dem_Player_Prefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        Assert.IsNotNull(player, $"{PlayerPrefab} nicht gefunden.");

        var problems = new List<string>();
        var fromPrefab = new Dictionary<string, Weapon>();

        foreach (Weapon w in player.GetComponentsInChildren<Weapon>(true))
        {
            if (w == null || string.IsNullOrEmpty(w.weaponID)) continue;
            fromPrefab[w.weaponID] = w;
        }

        foreach (KeyValuePair<string, Weapon> pair in fromPrefab)
        {
            WeaponDef def = WeaponCatalog.Find(pair.Key);
            if (def == null)
            {
                problems.Add($"Im Prefab, nicht im Katalog: {pair.Key} " +
                             $"(\"{pair.Value.gameObject.name}\")");
                continue;
            }

            if (def.NameEn != pair.Value.gameObject.name)
                problems.Add($"Name weicht ab bei {pair.Key}: Katalog \"{def.NameEn}\", " +
                             $"Prefab \"{pair.Value.gameObject.name}\"");
        }

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            if (!fromPrefab.ContainsKey(def.Id))
                problems.Add($"Im Katalog, nicht mehr im Prefab: {def.Id}");
        }

        Assert.IsEmpty(problems,
            "WeaponCatalog und Player-Prefab laufen auseinander:\n  " +
            string.Join("\n  ", problems) +
            "\n  Tools > Werkbank > Katalog gegen Player-Prefab prüfen zeigt die fehlenden Zeilen.");
    }

    [Test]
    public void Rezepte_decken_sich_mit_EvoCombinations()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        Assert.IsNotNull(player, $"{PlayerPrefab} nicht gefunden.");

        PlayerController pc = player.GetComponent<PlayerController>();
        Assert.IsNotNull(pc, "Kein PlayerController am Player-Prefab.");
        Assert.IsNotNull(pc.EvoCombinations, "EvoCombinations ist leer.");

        var problems = new List<string>();
        var seen = new HashSet<string>();

        foreach (EvoRecipe r in pc.EvoCombinations)
        {
            if (r == null || r.EvoWeapon == null) continue;

            string id = r.EvoWeapon.weaponID;
            string a = r.RequiredWeapon1 != null ? r.RequiredWeapon1.weaponID : null;
            string b = r.RequiredWeapon2 != null ? r.RequiredWeapon2.weaponID : null;
            seen.Add(id);

            EvoDef def = WeaponCatalog.Evos.FirstOrDefault(e => e.Id == id);
            if (def == null)
            {
                problems.Add($"Rezept fehlt im Katalog: {id} = {a} + {b}");
                continue;
            }

            bool same = (def.IngredientA == a && def.IngredientB == b)
                     || (def.IngredientA == b && def.IngredientB == a);

            if (!same)
                problems.Add($"Rezept {id}: Katalog {def.IngredientA} + {def.IngredientB}, " +
                             $"Prefab {a} + {b}");
        }

        foreach (EvoDef def in WeaponCatalog.Evos)
        {
            if (!seen.Contains(def.Id))
                problems.Add($"Rezept im Katalog, nicht mehr im Prefab: {def.Id}");
        }

        Assert.IsEmpty(problems, "Evo-Rezepte laufen auseinander:\n  " + string.Join("\n  ", problems));
    }

    [Test]
    public void Jeder_Eintrag_hat_beide_Icon_Groessen()
    {
        var missing = new List<string>();

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            foreach (int size in new[] { 14, 10 })
            {
                string path = $"{IconFolder}/{def.Id}_{size}.png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null) missing.Add(path);
            }
        }

        Assert.IsEmpty(missing, "Werkbank-Icons fehlen:\n  " + string.Join("\n  ", missing));
    }

    // ==================================================================
    //  Verteiler
    // ==================================================================

    [Test]
    public void Ohne_uebernommenen_Build_darf_im_Lauf_alles_gezogen_werden()
    {
        Assert.IsFalse(Loadout.IsActive);

        foreach (WeaponDef def in WeaponCatalog.All)
        {
            Assert.IsTrue(Loadout.AllowsInRun(def.Id),
                $"'{def.Id}' wird blockiert, obwohl die Werkbank nie benutzt wurde.");
        }
    }

    [Test]
    public void Verteiler_nimmt_nur_so_viel_wie_Plaetze_da_sind()
    {
        foreach (WeaponDef def in WeaponCatalog.OfKind(PoolKind.Weapon)) Loadout.Add(def.Id);
        foreach (WeaponDef def in WeaponCatalog.OfKind(PoolKind.Buff)) Loadout.Add(def.Id);

        Assert.LessOrEqual(Loadout.Count(PoolKind.Weapon), Loadout.WeaponSlots);
        Assert.LessOrEqual(Loadout.Count(PoolKind.Buff), Loadout.BuffSlots);
        Assert.AreEqual(Loadout.RequiredBuffs, Loadout.Count(PoolKind.Buff),
            "Bei mehr Buffs als Plaetzen muessen genau die Plaetze voll werden.");
    }

    [Test]
    public void Evos_lassen_sich_nicht_verteilen()
    {
        int before = Loadout.Count(PoolKind.Weapon);

        foreach (WeaponDef def in WeaponCatalog.OfKind(PoolKind.Evo))
        {
            Assert.IsFalse(Loadout.Add(def.Id), $"'{def.Id}' liess sich in den Verteiler legen.");
        }

        Assert.AreEqual(before, Loadout.Count(PoolKind.Weapon));
        Assert.AreEqual(0, Loadout.Count(PoolKind.Buff));
    }

    [Test]
    public void Uebernehmen_geht_erst_wenn_beide_Gruppen_voll_sind()
    {
        Loadout.Add(WeaponCatalog.CandyBomb.Id);
        Assert.IsFalse(Loadout.IsComplete);
        Assert.IsFalse(Loadout.Apply(), "Ein halber Verteiler liess sich uebernehmen.");
        Assert.IsFalse(Loadout.IsActive);

        Loadout.FillRandom();
        Assert.IsTrue(Loadout.IsComplete);
        Assert.IsTrue(Loadout.Apply());
        Assert.IsTrue(Loadout.IsActive);
    }

    [Test]
    public void Ein_uebernommener_Build_gilt_und_sperrt_den_Rest()
    {
        Loadout.FillRandom();
        Assert.IsTrue(Loadout.Apply());

        // Die Buffs passen nicht alle in den Verteiler - die uebrigen duerfen
        // im Lauf nicht mehr auftauchen.
        var buffs = WeaponCatalog.OfKind(PoolKind.Buff);
        Assert.Greater(buffs.Count, Loadout.BuffSlots, "Test setzt mehr Buffs als Plaetze voraus.");

        foreach (WeaponDef def in buffs)
        {
            Assert.AreEqual(Loadout.Contains(def.Id), Loadout.AllowsInRun(def.Id),
                $"'{def.Id}': Verteiler und Lauf sind sich uneinig.");
        }

        // Evos haengen nie am Verteiler - die entstehen aus ihren Zutaten.
        foreach (WeaponDef def in WeaponCatalog.OfKind(PoolKind.Evo))
        {
            Assert.IsTrue(Loadout.AllowsInRun(def.Id), $"Evo '{def.Id}' wurde gesperrt.");
        }
    }

    [Test]
    public void Wer_nach_dem_Uebernehmen_etwas_herausnimmt_faellt_zurueck()
    {
        Loadout.FillRandom();
        Assert.IsTrue(Loadout.Apply());

        // Platz 0 ist der feste - den kann man nicht herausnehmen
        Loadout.Remove(Loadout.Get(PoolKind.Weapon)[1]);

        Assert.IsFalse(Loadout.IsComplete);
        Assert.IsFalse(Loadout.IsActive,
            "Ein unvollstaendiger Verteiler darf nicht weiter fuer den Lauf gelten.");
    }

    [Test]
    public void Evo_Zustand_folgt_den_Zutaten()
    {
        // Ein Rezept, an dem die Standardwaffe nicht beteiligt ist - sonst
        // startet der Test schon bei "halb fertig".
        EvoDef evo = WeaponCatalog.Evos.First(
            e => !Loadout.IsLocked(e.IngredientA) && !Loadout.IsLocked(e.IngredientB));

        Assert.AreEqual(EvoState.None, Loadout.StateOf(evo));

        Loadout.Add(evo.IngredientA);
        Assert.AreEqual(EvoState.Partial, Loadout.StateOf(evo));

        Loadout.Add(evo.IngredientB);
        Assert.AreEqual(EvoState.Ready, Loadout.StateOf(evo));

        Loadout.Remove(evo.IngredientA);
        Assert.AreEqual(EvoState.Partial, Loadout.StateOf(evo));
    }

    [Test]
    public void Evo_Leiste_zeigt_das_Fortgeschrittenste_zuerst()
    {
        EvoDef target = WeaponCatalog.Evos.Last(
            e => !Loadout.IsLocked(e.IngredientA) && !Loadout.IsLocked(e.IngredientB));
        Loadout.Add(target.IngredientA);
        Loadout.Add(target.IngredientB);

        List<EvoDef> sorted = Loadout.EvosByProgress();

        Assert.AreEqual(WeaponCatalog.Evos.Count, sorted.Count, "Es fehlen Rezepte in der Liste.");
        Assert.AreEqual(EvoState.Ready, Loadout.StateOf(sorted[0]),
            "Das fertige Rezept steht nicht vorn.");
    }

    // ==================================================================
    //  Der feste erste Platz
    // ==================================================================

    [Test]
    public void Startwaffen_stimmen_mit_dem_Player_Prefab_ueberein()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        Assert.IsNotNull(player, $"{PlayerPrefab} nicht gefunden.");

        PlayerController pc = player.GetComponent<PlayerController>();
        Assert.IsNotNull(pc, "Kein PlayerController am Player-Prefab.");
        Assert.IsNotNull(pc.activeWeapon, "activeWeapon ist leer.");

        var problems = new List<string>();

        for (int skin = 0; skin < Characters.Count; skin++)
        {
            int index = Characters.StartWeaponIndex(skin);
            string id = Characters.StartWeaponId(skin);

            if (index < 0 || index >= pc.activeWeapon.Length)
            {
                problems.Add($"Charakter {skin} ({Characters.NameOf(skin)}): Index {index} " +
                             $"liegt ausserhalb von activeWeapon[{pc.activeWeapon.Length}]");
                continue;
            }

            string fromPrefab = pc.activeWeapon[index] != null
                ? pc.activeWeapon[index].weaponID
                : null;

            if (fromPrefab != id)
            {
                problems.Add($"Charakter {skin} ({Characters.NameOf(skin)}): Index {index} " +
                             $"zeigt auf '{fromPrefab}', die Id-Liste sagt '{id}'");
            }

            Assert.IsNotNull(WeaponCatalog.Find(id),
                $"Startwaffe '{id}' von Charakter {skin} steht nicht im Katalog.");
        }

        Assert.IsEmpty(problems,
            "Characters.StartWeaponByskin und StartWeaponIdByskin meinen nicht dasselbe:\n  " +
            string.Join("\n  ", problems));
    }

    [Test]
    public void Die_Standardwaffe_liegt_immer_auf_Platz_eins()
    {
        string locked = Loadout.LockedWeaponId;
        Assert.IsNotEmpty(locked, "Der Charakter hat keine Startwaffe.");

        CollectionAssert.IsNotEmpty(Loadout.Get(PoolKind.Weapon));
        Assert.AreEqual(locked, Loadout.Get(PoolKind.Weapon)[0]);

        Loadout.FillRandom();
        Assert.AreEqual(locked, Loadout.Get(PoolKind.Weapon)[0],
            "Zufall hat den festen Platz verschoben.");

        Loadout.Clear();
        Assert.AreEqual(locked, Loadout.Get(PoolKind.Weapon)[0],
            "Leeren hat den festen Platz mitgenommen.");
    }

    [Test]
    public void Die_Standardwaffe_laesst_sich_nicht_herausnehmen()
    {
        string locked = Loadout.LockedWeaponId;

        Assert.IsFalse(Loadout.Remove(locked));
        Assert.IsFalse(Loadout.Toggle(locked));
        Assert.IsTrue(Loadout.Contains(locked));
    }

    [Test]
    public void Zufall_fuellt_beide_Gruppen_und_mischt_wirklich()
    {
        Loadout.FillRandom();
        Assert.IsTrue(Loadout.IsComplete, "Zufall hat nicht bis zum Anschlag gefuellt.");

        // Bei 19 Buffs auf 10 Plaetzen muessen sich zwei Wuerfe unterscheiden.
        // Zweimal dieselbe Auswahl ist so unwahrscheinlich (1 zu ~92378), dass
        // acht gleiche Wuerfe hintereinander ein echter Fehler sind.
        var first = new List<string>(Loadout.Get(PoolKind.Buff));

        bool different = false;
        for (int i = 0; i < 8 && !different; i++)
        {
            Loadout.FillRandom();
            different = !first.SequenceEqual(Loadout.Get(PoolKind.Buff));
        }

        Assert.IsTrue(different, "Acht Wuerfe ergaben achtmal dieselbe Auswahl.");
    }

    // ==================================================================
    //  Ein Verteiler je Charakter
    // ==================================================================

    [Test]
    public void Der_Verteiler_gehoert_zum_Charakter()
    {
        Assert.GreaterOrEqual(Characters.Count, 2, "Test braucht zwei Charaktere.");

        int a = Shop.SkinIndex;
        int b = (a + 1) % Characters.Count;

        Loadout.FillRandom();
        Assert.IsTrue(Loadout.Apply());
        List<string> buildA = new List<string>(Loadout.Get(PoolKind.Weapon));

        Shop.SkinIndex = b;

        // Der andere Charakter faengt bei seiner eigenen Startwaffe an ...
        Assert.AreEqual(Characters.StartWeaponId(b), Loadout.Get(PoolKind.Weapon)[0],
            "Die Startwaffe des gewaehlten Charakters liegt nicht auf Platz 0.");
        Assert.AreEqual(1, Loadout.Count(PoolKind.Weapon),
            "Der Build des anderen Charakters ist mitgewandert.");
        Assert.IsFalse(Loadout.IsActive,
            "\"Uebernommen\" gehoert zum Charakter, nicht zum Spielstand.");

        Shop.SkinIndex = a;

        // ... und der erste findet seinen Build unveraendert vor. Genau das
        // ging vorher schief: die alte Startwaffe blieb als normale Waffe
        // liegen, die neue schob sich auf Platz 0 und alles rutschte.
        CollectionAssert.AreEqual(buildA, Loadout.Get(PoolKind.Weapon),
            "Der Wechsel hat den Build verschoben.");
        Assert.IsTrue(Loadout.IsActive, "Der uebernommene Build gilt nach dem Wechsel nicht mehr.");
    }

    [Test]
    public void Die_Startwaffe_des_anderen_Charakters_draengt_sich_nicht_dazwischen()
    {
        Assert.GreaterOrEqual(Characters.Count, 2, "Test braucht zwei Charaktere.");

        int a = Shop.SkinIndex;
        int b = (a + 1) % Characters.Count;
        Assert.AreNotEqual(Characters.StartWeaponId(a), Characters.StartWeaponId(b),
            "Test braucht zwei Charaktere mit verschiedenen Startwaffen.");

        Loadout.Add(WeaponCatalog.CandyBomb.Id);
        Shop.SkinIndex = b;
        Shop.SkinIndex = a;

        CollectionAssert.AreEqual(
            new[] { Characters.StartWeaponId(a), WeaponCatalog.CandyBomb.Id },
            Loadout.Get(PoolKind.Weapon),
            "Nach Hin und Her steht etwas anderes im Verteiler als vorher.");
    }

    // ==================================================================
    //  Spielstand
    // ==================================================================

    [Test]
    public void Spielstand_uebersteht_Schreiben_und_Lesen()
    {
        var store = new LoadoutStore(tempDir);
        store.Set(PoolKind.Weapon, new[] { "cookie_saw", "boba_gun", "cookie_saw" });
        store.Set(PoolKind.Buff, new[] { "buff_damage" });
        store.Active = true;
        store.Save();

        var reloaded = new LoadoutStore(tempDir);
        reloaded.Load();

        Assert.IsTrue(reloaded.Active);
        CollectionAssert.AreEqual(new[] { "cookie_saw", "boba_gun" }, reloaded.Weapons,
            "Doppelte Ids muessen beim Setzen wegfallen, die Reihenfolge bleibt.");
        CollectionAssert.AreEqual(new[] { "buff_damage" }, reloaded.Buffs);
    }

    [Test]
    public void Unbekannte_Ids_bleiben_im_Spielstand_liegen()
    {
        // Eine Waffe, die es heute nicht gibt: wird sie spaeter nachgereicht,
        // soll sie noch im Verteiler liegen - dieselbe Regel wie bei Unlocks.
        var store = new LoadoutStore(tempDir);
        store.Set(PoolKind.Weapon, new[] { "cookie_saw", "waffe_von_morgen" });
        store.Save();

        var reloaded = new LoadoutStore(tempDir);
        reloaded.Load();

        CollectionAssert.Contains(reloaded.Weapons, "waffe_von_morgen");
    }

    [Test]
    public void Spielstand_haelt_die_Charaktere_auseinander()
    {
        var store = new LoadoutStore(tempDir);
        store.Use(0);
        store.Set(PoolKind.Weapon, new[] { "cookie_saw" });
        store.Active = true;

        store.Use(1);
        store.Set(PoolKind.Weapon, new[] { "boba_gun" });
        store.Save();

        var reloaded = new LoadoutStore(tempDir);
        reloaded.Load();

        reloaded.Use(0);
        CollectionAssert.AreEqual(new[] { "cookie_saw" }, reloaded.Weapons);
        Assert.IsTrue(reloaded.Active);

        reloaded.Use(1);
        CollectionAssert.AreEqual(new[] { "boba_gun" }, reloaded.Weapons);
        Assert.IsFalse(reloaded.Active, "\"Uebernommen\" gehoert zum Charakter.");
    }

    [Test]
    public void Alter_Spielstand_geht_an_den_gewaehlten_Charakter()
    {
        // v1 kannte nur einen Verteiler fuer alle. Er gehoert dem Charakter,
        // mit dem der Spieler zuletzt unterwegs war - und der fragt als
        // erster danach (Loadout.Init).
        File.WriteAllText(Path.Combine(tempDir, "loadout.json"),
            "{\"version\":1,\"active\":true," +
            "\"weapons\":[\"cookie_saw\",\"boba_gun\"],\"buffs\":[\"buff_damage\"]}");

        var store = new LoadoutStore(tempDir);
        store.Load();
        Assert.IsTrue(store.Migrated, "Der alte Spielstand wurde nicht als solcher erkannt.");

        store.Use(2);
        CollectionAssert.AreEqual(new[] { "cookie_saw", "boba_gun" }, store.Weapons);
        CollectionAssert.AreEqual(new[] { "buff_damage" }, store.Buffs);
        Assert.IsTrue(store.Active);

        store.Use(0);
        CollectionAssert.IsEmpty(store.Weapons, "Der alte Verteiler wurde zweimal vergeben.");

        store.Save();

        var reloaded = new LoadoutStore(tempDir);
        reloaded.Load();
        Assert.IsFalse(reloaded.Migrated, "Die Datei steht immer noch im alten Format da.");

        reloaded.Use(2);
        CollectionAssert.AreEqual(new[] { "cookie_saw", "boba_gun" }, reloaded.Weapons);
    }

    [Test]
    public void Kaputter_Spielstand_wirft_nicht()
    {
        File.WriteAllText(Path.Combine(tempDir, "loadout.json"), "{ das ist kein json");

        var store = new LoadoutStore(tempDir);
        Assert.DoesNotThrow(() => store.Load());
        Assert.IsFalse(store.Active);
        Assert.IsEmpty(store.Weapons);
    }
}
#endif
