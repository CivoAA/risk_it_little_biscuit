using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Haengt die neuen Waffen (Crumb Trail, Vortex, Turret), die neue Evo
/// (Sticky Shatter), die neuen Buffs (Cooldown, Duration, Glass Cannon,
/// Second Chance) und den Begleiter in "Assets/Scenes/Game.unity" ein.
///
/// Der Builder ist beliebig oft wiederholbar: vorhandene Objekte und Prefabs
/// werden wiederverwendet und nicht ueberschrieben. Wer also schon Sprites und
/// eigene Werte eingetragen hat, verliert sie beim erneuten Lauf nicht.
///
/// Die erzeugten Prefabs sind reine Platzhalter (Unity-Standardsprite,
/// halbtransparent) - die Optik kommt spaeter per Hand rein.
///
/// WICHTIG: Nach einem Lauf muss die Test-Szene ueber
/// "Tools/Test Scene/Test-Szene neu bauen" nachgezogen werden.
/// </summary>
public static class ContentBuilder
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const string CompanionPrefabFolder = "Assets/Prefabs/Companion";

    private const string SortingLayer = "Weapon";

    [MenuItem("Tools/Content/Neue Waffen, Buffs und Begleiter einbauen", false, 0)]
    public static void BuildMenu()
    {
        bool go = EditorUtility.DisplayDialog(
            "Neuen Content einbauen",
            "Legt in Game.unity die neuen Waffen, die Sticky-Shatter-Evo, die neuen Buffs " +
            "und den Begleiter an und traegt sie in den PlayerController ein.\n\n" +
            "Bereits vorhandene Objekte und Prefabs bleiben unveraendert.\n\n" +
            "Danach die Test-Szene neu bauen.",
            "Einbauen", "Abbrechen");

        if (!go) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Build();

        EditorUtility.DisplayDialog(
            "Fertig",
            "Der neue Content liegt in Game.unity.\n\n" +
            "Naechste Schritte:\n" +
            "1. Sprites/Icons an den neuen Objekten und Prefabs setzen\n" +
            "2. Shop-Button fuer den Begleiter anlegen (Button-Index 25)\n" +
            "3. Test-Szene neu bauen",
            "Ok");
    }

    public static void Build()
    {
        EnsureFolder(WeaponPrefabFolder);
        EnsureFolder(CompanionPrefabFolder);

        // Reihenfolge zaehlt: Projektile zuerst, damit die Schuetzen sie beim
        // Anlegen schon referenzieren koennen und kein Nachbearbeiten noetig ist.
        GameObject turretShot = EnsurePrefab(
            WeaponPrefabFolder + "/TurretProjectile.prefab", "TurretProjectile",
            typeof(TurretProjectile), 0.12f, 0.6f);

        GameObject turret = EnsurePrefab(
            WeaponPrefabFolder + "/TurretPrefab.prefab", "TurretPrefab",
            typeof(TurretPrefab), 0f, 1f);
        SetObjectField(turret.GetComponent<TurretPrefab>(), "projectilePrefab", turretShot);

        GameObject companionShot = EnsurePrefab(
            CompanionPrefabFolder + "/CompanionProjectile.prefab", "CompanionProjectile",
            typeof(CompanionProjectile), 0.12f, 0.6f);

        GameObject companion = EnsurePrefab(
            CompanionPrefabFolder + "/Companion.prefab", "Companion",
            typeof(Companion), 0f, 1f);
        SetObjectField(companion.GetComponent<Companion>(), "projectilePrefab", companionShot);
        SetObjectField(companion.GetComponent<Companion>(), "spriteRenderer",
            companion.GetComponent<SpriteRenderer>());

        GameObject crumb = EnsurePrefab(
            WeaponPrefabFolder + "/CrumbTrailPrefab.prefab", "CrumbTrailPrefab",
            typeof(CrumbTrailPrefab), 0.5f, 1f);

        GameObject vortex = EnsurePrefab(
            WeaponPrefabFolder + "/VortexPrefab.prefab", "VortexPrefab",
            typeof(VortexPrefab), 0.5f, 1f);

        GameObject jar = EnsurePrefab(
            WeaponPrefabFolder + "/StickyShatterJar.prefab", "StickyShatterJar",
            null, 0f, 0.7f);

        GameObject puddle = EnsurePrefab(
            WeaponPrefabFolder + "/StickyShatterPuddle.prefab", "StickyShatterPuddle",
            typeof(StickyShatterEvoPrefab), 0.5f, 1f);

        AssetDatabase.SaveAssets();

        BuildScene(crumb, vortex, turret, jar, puddle, companion);

        AssetDatabase.SaveAssets();
        Debug.Log("[ContentBuilder] Fertig. Test-Szene jetzt neu bauen.");
    }

    // ------------------------------------------------------------------
    // Szene
    // ------------------------------------------------------------------

    private static void BuildScene(GameObject crumbPrefab, GameObject vortexPrefab,
                                   GameObject turretPrefab, GameObject jarPrefab,
                                   GameObject puddlePrefab, GameObject companionPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Player");
        if (player == null)
        {
            Debug.LogError("[ContentBuilder] Kein Root-Objekt 'Player' in Game.unity gefunden.");
            return;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null)
        {
            Debug.LogError("[ContentBuilder] Am Player haengt kein PlayerController.");
            return;
        }

        Transform weapons = RequireChild(player.transform, "Weapons");
        Transform buffs = RequireChild(player.transform, "Buffs");
        Transform evos = RequireChild(player.transform, "EvoWeapons");
        if (weapons == null || buffs == null || evos == null) return;

        // ---------------- Waffen ----------------

        CrumbTrail crumbTrail = EnsureWeapon<CrumbTrail>(weapons, "Crumb Trail", "crumb_trail", 5);
        SetObjectField(crumbTrail, "prefab", crumbPrefab);
        SetStats(crumbTrail, new[]
        {
            //          cooldown duration damage range  tick  shots  description
            Stat(0.45f,  2.5f,  2f,  0.80f, 0.40f,  4f, "Damage +2"),
            Stat(0.42f,  2.8f,  3f,  0.85f, 0.38f,  5f, "Damage +1, laengere Spur"),
            Stat(0.40f,  3.1f,  4f,  0.90f, 0.35f,  6f, "Damage +1, laengere Spur"),
            Stat(0.37f,  3.4f,  5f,  0.95f, 0.32f,  7f, "Damage +1, laengere Spur"),
            Stat(0.35f,  3.7f,  6f,  1.00f, 0.30f,  8f, "Damage +1, laengere Spur"),
            Stat(0.32f,  4.0f,  7f,  1.10f, 0.28f, 10f, "Damage +1, maximale Spur"),
        });

        Vortex vortex = EnsureWeapon<Vortex>(weapons, "Vortex", "vortex", 5);
        SetObjectField(vortex, "prefab", vortexPrefab);
        EnsureTrigger(vortex.gameObject, 6f);
        SetStats(vortex, new[]
        {
            Stat(8.0f, 3.0f, 1f, 2.0f, 0.50f, 1f, "Zieht Gegner zusammen"),
            Stat(7.5f, 3.3f, 2f, 2.2f, 0.50f, 1f, "Damage +1, groesserer Sog"),
            Stat(7.0f, 3.6f, 2f, 2.4f, 0.45f, 2f, "Zweiter Wirbel"),
            Stat(6.5f, 4.0f, 3f, 2.6f, 0.45f, 2f, "Damage +1, laenger aktiv"),
            Stat(6.0f, 4.3f, 3f, 2.8f, 0.40f, 3f, "Dritter Wirbel"),
            Stat(5.5f, 4.6f, 4f, 3.0f, 0.40f, 3f, "Damage +1, maximaler Sog"),
        });

        Turret turret = EnsureWeapon<Turret>(weapons, "Turret", "turret", 5);
        SetObjectField(turret, "prefab", turretPrefab);
        SetStats(turret, new[]
        {
            Stat(7.0f, 4.0f, 3f, 4.5f, 0.80f, 1f, "Damage +3"),
            Stat(6.6f, 4.5f, 4f, 4.8f, 0.75f, 1f, "Damage +1, steht laenger"),
            Stat(6.2f, 5.0f, 5f, 5.1f, 0.70f, 2f, "Zweiter Turm"),
            Stat(5.8f, 5.5f, 6f, 5.4f, 0.65f, 2f, "Damage +1, schneller"),
            Stat(5.4f, 6.0f, 7f, 5.7f, 0.60f, 3f, "Dritter Turm"),
            Stat(5.0f, 6.5f, 8f, 6.0f, 0.55f, 3f, "Damage +1, maximale Standzeit"),
        });

        // ---------------- Evo ----------------

        StickyShatterEvo sticky = EnsureWeapon<StickyShatterEvo>(
            evos, "Sticky Shatter Evo", "evo_sticky_shatter", 0);
        SetObjectField(sticky, "jarPrefab", jarPrefab);
        SetObjectField(sticky, "puddlePrefab", puddlePrefab);
        SetStats(sticky, new[]
        {
            Stat(4.5f, 5.0f, 5f, 1.6f, 0.35f, 5f, ""),
        });

        // ---------------- Buffs ----------------
        // Achtung: die Werte dieser Buffs sind ABSOLUT pro Stufe, nicht
        // kumulativ wie bei den aelteren Buffs (siehe CooldownReduction).

        CooldownReduction cooldown = EnsureWeapon<CooldownReduction>(
            buffs, "Cooldown", "buff_cooldown", 3);
        SetStats(cooldown, new[]
        {
            Stat(0f, 0f, 0.08f, 0f, 0f, 0f, "-8% Cooldown"),
            Stat(0f, 0f, 0.15f, 0f, 0f, 0f, "-15% Cooldown"),
            Stat(0f, 0f, 0.21f, 0f, 0f, 0f, "-21% Cooldown"),
            Stat(0f, 0f, 0.27f, 0f, 0f, 0f, "-27% Cooldown"),
        });

        DurationBuff duration = EnsureWeapon<DurationBuff>(buffs, "Duration", "buff_duration", 3);
        SetStats(duration, new[]
        {
            Stat(0f, 0f, 0.12f, 0f, 0f, 0f, "+12% Wirkdauer"),
            Stat(0f, 0f, 0.22f, 0f, 0f, 0f, "+22% Wirkdauer"),
            Stat(0f, 0f, 0.31f, 0f, 0f, 0f, "+31% Wirkdauer"),
            Stat(0f, 0f, 0.40f, 0f, 0f, 0f, "+40% Wirkdauer"),
        });

        // damage = Schadensbonus, range = Max-HP-Malus
        GlassCannon glass = EnsureWeapon<GlassCannon>(buffs, "Glass Cannon", "buff_glass_cannon", 2);
        SetStats(glass, new[]
        {
            Stat(0f, 0f, 0.25f, 0.12f, 0f, 0f, "+25% Schaden, -12% Max HP"),
            Stat(0f, 0f, 0.45f, 0.20f, 0f, 0f, "+45% Schaden, -20% Max HP"),
            Stat(0f, 0f, 0.70f, 0.30f, 0f, 0f, "+70% Schaden, -30% Max HP"),
        });

        // damage = Ladungen, range = HP beim Zurueckkommen, duration = Immunitaet
        SecondChance second = EnsureWeapon<SecondChance>(buffs, "Second Chance", "buff_second_chance", 1);
        SetStats(second, new[]
        {
            Stat(0f, 2.0f, 1f, 0.30f, 0f, 0f, "1x wiederbeleben mit 30% HP"),
            Stat(0f, 2.5f, 2f, 0.40f, 0f, 0f, "2x wiederbeleben mit 40% HP"),
        });

        // ---------------- Registrierung im PlayerController ----------------

        controller.activeWeapon = Append(controller.activeWeapon, crumbTrail, vortex, turret);
        controller.activeEvos = Append(controller.activeEvos, sticky);
        controller.activeBuffs = Append(controller.activeBuffs, cooldown, duration, glass, second);

        AddEvoRecipe(controller, sticky,
            FindWeapon(controller.activeWeapon, "jam_jar"),
            FindWeapon(controller.activeBuffs, "buff_aoe_range"));

        // ---------------- Begleiter ----------------

        CompanionSpawner spawner = player.GetComponent<CompanionSpawner>();
        if (spawner == null) spawner = player.AddComponent<CompanionSpawner>();
        SetObjectField(spawner, "companionPrefab", companionPrefab);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // ------------------------------------------------------------------
    // Helfer: Szene
    // ------------------------------------------------------------------

    private static Transform RequireChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            Debug.LogError($"[ContentBuilder] '{parent.name}/{name}' fehlt in Game.unity.");
        }
        return child;
    }

    private static T EnsureWeapon<T>(Transform parent, string objectName, string weaponID, int maxLevel)
        where T : Weapon
    {
        Transform existing = parent.Find(objectName);

        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
        }

        T weapon = go.GetComponent<T>();
        if (weapon == null) weapon = go.AddComponent<T>();

        weapon.weaponID = weaponID;
        weapon.maxweaponLevel = maxLevel;

        // -1 = noch nicht erhalten. Nur beim ersten Anlegen setzen, damit ein
        // erneuter Lauf keinen laufenden Testaufbau zurueckwirft.
        if (existing == null) weapon.weaponLevel = -1;

        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    /// <summary>
    /// Nur befuellen, wenn noch keine Stats drinstehen: sonst ueberschreibt ein
    /// zweiter Lauf die von Hand ausbalancierten Werte.
    /// </summary>
    private static void SetStats(Weapon weapon, WeaponStats[] stats)
    {
        if (weapon.stats != null && weapon.stats.Count > 0) return;

        weapon.stats = new List<WeaponStats>(stats);
        EditorUtility.SetDirty(weapon);
    }

    private static WeaponStats Stat(float cooldown, float duration, float damage,
                                    float range, float attackSpeed, float shots,
                                    string description)
    {
        return new WeaponStats
        {
            cooldown = cooldown,
            duration = duration,
            damage = damage,
            range = range,
            AttackSpeed = attackSpeed,
            shots = shots,
            description = description
        };
    }

    private static Weapon[] Append(Weapon[] array, params Weapon[] items)
    {
        List<Weapon> list = array != null ? new List<Weapon>(array) : new List<Weapon>();

        foreach (Weapon item in items)
        {
            if (item != null && !list.Contains(item)) list.Add(item);
        }

        return list.ToArray();
    }

    private static Weapon FindWeapon(Weapon[] array, string weaponID)
    {
        if (array == null) return null;
        return array.FirstOrDefault(w => w != null && w.weaponID == weaponID);
    }

    private static void AddEvoRecipe(PlayerController controller, Weapon evo,
                                     Weapon required1, Weapon required2)
    {
        if (evo == null) return;

        if (required1 == null || required2 == null)
        {
            Debug.LogWarning("[ContentBuilder] Zutaten fuer die Sticky-Shatter-Evo nicht gefunden " +
                             "(erwartet: Waffe 'jam_jar' + Buff 'buff_aoe_range'). " +
                             "Rezept bitte von Hand im PlayerController eintragen.");
            return;
        }

        if (controller.EvoCombinations.Any(r => r != null && r.EvoWeapon == evo)) return;

        controller.EvoCombinations.Add(new EvoRecipe
        {
            EvoWeapon = evo,
            RequiredWeapon1 = required1,
            RequiredWeapon2 = required2
        });
    }

    // ------------------------------------------------------------------
    // Helfer: Prefabs
    // ------------------------------------------------------------------

    /// <summary>
    /// Legt ein Platzhalter-Prefab an, falls unter <paramref name="path"/> noch
    /// keins liegt. Vorhandene Prefabs werden unveraendert zurueckgegeben.
    /// </summary>
    private static GameObject EnsurePrefab(string path, string name, System.Type scriptType,
                                           float triggerRadius, float alpha)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject go = new GameObject(name);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PlaceholderSprite();
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = -1;
        renderer.color = new Color(1f, 1f, 1f, alpha);

        if (triggerRadius > 0f)
        {
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = triggerRadius;
        }

        if (scriptType != null) go.AddComponent(scriptType);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"[ContentBuilder] Platzhalter-Prefab angelegt: {path}");
        return prefab;
    }

    /// <summary>Unity-Bordmittel als sichtbarer Platzhalter, bis die Optik kommt.</summary>
    private static Sprite PlaceholderSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    private static void EnsureTrigger(GameObject go, float radius)
    {
        CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = go.AddComponent<CircleCollider2D>();
            collider.radius = radius;
        }

        collider.isTrigger = true;
        EditorUtility.SetDirty(go);
    }

    /// <summary>Setzt ein privates [SerializeField]-Objektfeld.</summary>
    private static void SetObjectField(Object target, string fieldName, Object value)
    {
        if (target == null) return;

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[ContentBuilder] Feld '{fieldName}' an '{target.GetType().Name}' nicht gefunden.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
