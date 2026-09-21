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
/// Aussehen herum.
///
/// KOMMT EIN CHARAKTER DAZU: in beiden Listen unten einen Eintrag ergaenzen -
/// Startwaffe und Name - und im PlayerSkinSwitcher einen Fall dafuer. Der
/// Skilltree-Editor (Tools -> Skilltree -> Editor) listet ihn dann von selbst
/// oben in der Auswahl und legt auf Klick seinen Baum an, und die Auswahl im
/// Hub zeigt ihn ebenfalls von allein. Mehr ist dafuer nicht noetig.
/// </summary>
public static class Characters
{
    /// <summary>Startwaffe je Charakter - Index in PlayerController.activeWeapon.</summary>
    private static readonly int[] StartWeaponByskin = { 2, 6, 11, 1 };

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
}
