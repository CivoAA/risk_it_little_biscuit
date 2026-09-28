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
/// STAND: die vier Charaktere der alten World Map sind wieder da, in genau der
/// Reihenfolge, die PlayerSkinSwitcher kennt (0 Normal, 1 Black, 2 RedSword,
/// 3 JamJar) - der Index MUSS dazu passen, sonst laeuft man mit dem falschen
/// Aussehen herum. Ab Index 4 holen sich beide Skin-Switcher den Animator aus
/// <see cref="CharacterLooks"/> (4 = Onigiri, der erste im 32x32-Format).
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
    private static readonly int[] StartWeaponByskin = { 2, 6, 11, 1, 2 };

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
        "shurikookie",   // 0 - Brauner Keks
        "spike_fork",    // 1 - Grauer Keks
        "blade_swarm",   // 2 - Roter Keks
        "jam_jar",       // 3 - Marmelade
        "shurikookie",   // 4 - Onigiri (vorlaeufig, bekommt spaeter eine eigene Waffe)
    };

    /// <summary>
    /// Anzeigename je Charakter, nur fuer Editor und Menues - nichts davon landet
    /// im Spielstand. Leer gelassen heisst schlicht "Charakter N".
    /// </summary>
    private static readonly string[] NameByskin =
    {
        "Brauner Keks",   // 0 - PlayerSkinSwitcher.SetNormalSkin
        "Grauer Keks",    // 1 - PlayerSkinSwitcher.SetBlackSkin
        "Roter Keks",     // 2 - PlayerSkinSwitcher.SetRedSwordSkin
        "Marmelade",      // 3 - PlayerSkinSwitcher.SetJamJarSkin
        "Onigiri",        // 4 - Animator aus CharacterLooks (erster Charakter im 32x32-Format)
    };

    /// <summary>
    /// Kurzer Spruch unter dem Namen in der Charakterauswahl. Rueckfall fuer
    /// <c>character.N.tagline</c> in den Sprachdateien - leer heisst: keiner.
    /// </summary>
    private static readonly string[] TaglineByskin =
    {
        "Der Klassiker",
        "Hart wie Zwieback",
        "Klinge voraus",
        "Klebt an allem",
        "Reis mit Stirnband",
    };

    /// <summary>Zwei, drei Saetze fuer die Charakterauswahl. Rueckfall fuer <c>character.N.desc</c>.</summary>
    private static readonly string[] DescriptionByskin =
    {
        "Frisch aus dem Ofen und bereit für alles. Knusprig, ehrlich, unterschätzt.",
        "Wochenlang in der Dose vergessen und entsprechend schlecht gelaunt. Beißt sich an allem die Zähne aus.",
        "Rote Glasur, scharfe Kante. Wer ihm zu nahe kommt, bekommt die Klinge zu spüren.",
        "Eigentlich nur der Belag. Aber ein ganzes Glas voller Wut - und es klebt.",
        "Aus der Bento-Box geflohen und fest entschlossen. Hält zusammen, was zusammengehört - vor allem sich selbst.",
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
