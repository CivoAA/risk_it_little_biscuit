using UnityEngine;

/// <summary>
/// Die spielbaren Charaktere. Bisher steckte hier nur eine Zuordnung
/// "Charakter -> Startwaffe", die als switch in LevelPoint.Startweapon lag und
/// in extraData[1] geschrieben wurde.
///
/// Der Index ist der Platz in der Charakterauswahl im Hub
/// (<see cref="HubCharacterSelectUI"/>) und gleichzeitig der Skin-Index des
/// Spielers; er wird im Spielstand unter skinIndex abgelegt und landet ueber
/// Shop.RunSkinIndex im PlayerSkinSwitcher. Die Waffennummer ist der Platz in
/// PlayerController.activeWeapon.
///
/// STAND (10.10.2026): acht Charaktere - 0 Keks, 1 Marmelade, 2 Onigiri,
/// 3 Toast, 4 Zwiebelritter, 5 Mochi, 6 Inari (Fuchs), 7 Schleimkoenig, alle im 32x32-Format (der Ritter hat 64er-Zellen,
/// weil sein Schwert heraussteht - Pivot unter dem Koerper, PPU 32). Grauer und Roter Keks sind raus, die anderen sind
/// aufgerueckt. Beide Skin-Switcher holen den Animator fuer jeden Index aus
/// <see cref="CharacterLooks"/> - Eintrag i dort MUSS zu Charakter i passen.
///
/// KOMMT EIN CHARAKTER DAZU: in den Listen unten je einen Eintrag ergaenzen -
/// Startwaffe (beide), Name, Spruch, Beschreibung, ggf. Unlock-Id - und im
/// PlayerSkinSwitcher einen Fall dafuer. Fuer die Charakterauswahl im Hub
/// ausserdem Animator und Farbe in Resources/Characters/CharacterLooks.asset
/// (siehe <see cref="CharacterLooks"/>); uebersetzt wird ueber
/// character.N.name/tagline/desc in den Sprachdateien. Der Skilltree-Editor
/// (Tools -> Skilltree -> Editor) listet ihn dann von selbst oben in der
/// Auswahl und legt auf Klick seinen Baum an, und die Keksdose im Hub zeigt
/// ihn ebenfalls von allein (ab dem zehnten wird geblaettert).
/// </summary>
public static class Characters
{
    /// <summary>Startwaffe je Charakter - Index in PlayerController.activeWeapon.</summary>
    private static readonly int[] StartWeaponByskin = { 20, 1, 11, 19, 18, 25, 23, 24 };

    /// <summary>
    /// Dieselbe Startwaffe noch einmal, diesmal als <c>Weapon.weaponID</c>.
    ///
    /// Warum zweimal: der Index oben zeigt in ein Array, das es nur im Spiel
    /// gibt (<see cref="PlayerController.activeWeapon"/>). Der Hub hat den
    /// Spieler nicht - die Werkbank muss trotzdem wissen, welche Waffe beim
    /// gewaehlten Charakter fest im Verteiler liegt.
    ///
    /// BEIDE LISTEN MUESSEN DASSELBE MEINEN. Dass sie das tun, prueft
    /// <c>WorkbenchTests.Startwaffen_stimmen_mit_dem_Player_Prefab_ueberein</c>
    /// gegen das Prefab - eine verschobene Zeile faellt dort sofort auf.
    /// </summary>
    private static readonly string[] StartWeaponIdByskin =
    {
        "choco_chips",   // 0 - Keks (Schoko-Salve; frueher "shurikookie" = Index 2)
        "jam_jar",       // 1 - Marmelade
        "blade_swarm",   // 2 - Onigiri
        "salad_fan",     // 3 - Toast
        "sword_slash",   // 4 - Zwiebelritter
        "mochi_melody",  // 5 - Mochi (Mochi-Melodie seit 10.10.2026; davor Mochi-Faden "mochi_strand")
        "foxfire",       // 6 - Inari (Fuchsfeuer; vorher vorlaeufig "shurikookie")
        "royal_splat",   // 7 - Schleimkoenig (Koenigsplumps)
    };

    /// <summary>
    /// Waffe, die NUR dieser Charakter bekommt - leer heisst: keine. Alle
    /// anderen sehen sie weder in der Werkbank noch im Level-Up. Gedacht fuer
    /// Startwaffen, die zur Figur gehoeren (der Keks schiesst seine eigenen
    /// Schokostueckchen); die uebrigen Startwaffen bleiben fuer alle offen.
    /// </summary>
    private static readonly string[] ExclusiveWeaponIdByskin =
    {
        "choco_chips",   // 0 - Keks
        "",
        "",
        "",
        "",
        "mochi_melody",  // 5 - Mochi
        "foxfire",       // 6 - Inari
        "royal_splat",   // 7 - Schleimkoenig
    };

    /// <summary>
    /// Anzeigename je Charakter, nur fuer Editor und Menues - nichts davon landet
    /// im Spielstand. Leer gelassen heisst schlicht "Charakter N".
    /// </summary>
    private static readonly string[] NameByskin =
    {
        "Keks",           // 0 - Animator aus CharacterLooks (32x32, Char_Keks)
        "Marmelade",      // 1 - Animator aus CharacterLooks (32x32, Char_Jam)
        "Onigiri",        // 2 - Animator aus CharacterLooks (erster Charakter im 32x32-Format)
        "Toast",          // 3 - Animator aus CharacterLooks (32x32, Char_Toast)
        "Zwiebelritter",  // 4 - Animator aus CharacterLooks (64er-Zellen, Char_OnionKnight)
        "Mochi",          // 5 - Animator aus CharacterLooks (32x32, Char_Mochi aus Tools/char_erdbeere.py)
        "Inari",          // 6 - Animator aus CharacterLooks (32x32, Char_Fuchs aus Tools/char_fuchs.py)
        "Schleimkönig",   // 7 - Animator aus CharacterLooks (8x4-Sheet 32x32, Char_SlimeKing aus Tools/char_schleimkoenig.py)
    };

    /// <summary>
    /// Kurzer Spruch unter dem Namen in der Charakterauswahl. Rueckfall fuer
    /// <c>character.N.tagline</c> in den Sprachdateien - leer heisst: keiner.
    /// </summary>
    private static readonly string[] TaglineByskin =
    {
        "Der Klassiker",
        "Klebt an allem",
        "Reis mit Stirnband",
        "Knusprig bis zum Rand",
        "Schwert hoch, Tränen runter",
        "Weich, aber nicht wehrlos",
        "Klein, flink, gut gefüllt",
        "Klein, aber gekrönt",
    };

    /// <summary>Zwei, drei Saetze fuer die Charakterauswahl. Rueckfall fuer <c>character.N.desc</c>.</summary>
    private static readonly string[] DescriptionByskin =
    {
        "Frisch aus dem Ofen und bereit für alles. Knusprig, ehrlich, unterschätzt.",
        "Eigentlich nur der Belag. Aber ein ganzes Glas voller Wut - und es klebt.",
        "Aus der Bento-Box geflohen und fest entschlossen. Hält zusammen, was zusammengehört - vor allem sich selbst.",
        "Direkt aus dem Toaster und noch warm. Das Stirnband sitzt, die Kruste auch - wer ihn anfasst, verbrennt sich die Finger.",
        "Schicht für Schicht Rüstung. Zieht er blank, weinen die Gegner - und manchmal er selbst ein bisschen mit.",
        "Ein Erdbeer-Mochi mit Schleife und dem süßesten Blick der Küche. Wer sie unterschätzt, wird plattgehüpft.",
        "Ein kleiner Fuchs aus Inari-Sushi: außen goldene Tofutasche, innen Reis. Das blaue Halstuch hat er sich selbst gebunden - schief, aber mit Stolz.",
        "Ein Wackelpudding aus Traubengelee, der sich selbst zum König gekrönt hat. Die Krone ist eine Nummer zu klein, das Zepter eine Nummer zu groß - aber wer ihm im Weg steht, wird eingeschleimt.",
    };

    /// <summary>
    /// Freischaltung je Charakter (Id aus <see cref="Unlocks"/>). Leer = von
    /// Anfang an spielbar. Gesperrte Charaktere zeigt die Auswahl als Schatten.
    /// </summary>
    private static readonly string[] UnlockIdByskin =
    {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
    };

    public static int Count => StartWeaponByskin.Length;

    public static int StartWeaponIndex(int skinIndex)
    {
        if (StartWeaponByskin.Length == 0) return 0;
        return StartWeaponByskin[Mathf.Clamp(skinIndex, 0, StartWeaponByskin.Length - 1)];
    }

    /// <summary>
    /// Startwaffe des Charakters als weaponID. Leer, wenn der Charakter keine
    /// hat - dann ist in der Werkbank auch kein Platz reserviert.
    /// </summary>
    public static string StartWeaponId(int skinIndex)
    {
        if (StartWeaponIdByskin.Length == 0) return "";
        return StartWeaponIdByskin[Mathf.Clamp(skinIndex, 0, StartWeaponIdByskin.Length - 1)];
    }

    /// <summary>
    /// Darf dieser Charakter die Waffe bekommen? Nein nur dann, wenn sie einem
    /// anderen Charakter allein gehoert (<see cref="ExclusiveWeaponIdByskin"/>).
    /// </summary>
    public static bool IsWeaponAllowed(string weaponId, int skinIndex)
    {
        if (string.IsNullOrEmpty(weaponId)) return true;

        for (int skin = 0; skin < ExclusiveWeaponIdByskin.Length; skin++)
        {
            if (skin != skinIndex && ExclusiveWeaponIdByskin[skin] == weaponId) return false;
        }
        return true;
    }

    public static string NameOf(int skinIndex)
    {
        if (skinIndex < 0 || skinIndex >= NameByskin.Length) return "Charakter " + skinIndex;

        string name = NameByskin[skinIndex];
        return string.IsNullOrWhiteSpace(name) ? "Charakter " + skinIndex : name;
    }

    /// <summary>Name fuer Menues, uebersetzt ueber <c>character.N.name</c>; Rueckfall ist <see cref="NameOf"/>.</summary>
    public static string DisplayName(int skinIndex) => Loc.Get($"character.{skinIndex}.name", NameOf(skinIndex));

    public static string Tagline(int skinIndex) =>
        Loc.Get($"character.{skinIndex}.tagline", Pick(TaglineByskin, skinIndex));

    public static string Description(int skinIndex) =>
        Loc.Get($"character.{skinIndex}.desc", Pick(DescriptionByskin, skinIndex));

    /// <summary>Darf der Charakter gewaehlt werden? Ohne Unlock-Id immer.</summary>
    public static bool IsAvailable(int skinIndex)
    {
        string id = Pick(UnlockIdByskin, skinIndex);
        return string.IsNullOrWhiteSpace(id) || Unlocks.IsUnlocked(id);
    }

    private static string Pick(string[] list, int index) =>
        index >= 0 && index < list.Length && list[index] != null ? list[index] : "";
}
