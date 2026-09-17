using UnityEngine;

/// <summary>
/// Die spielbaren Charaktere. Bisher steckte hier nur eine Zuordnung
/// "Charakter -> Startwaffe", die als switch in LevelPoint.Startweapon lag und
/// in extraData[1] geschrieben wurde.
///
/// Der Index ist der Platz im Karussell der Charakterauswahl und gleichzeitig
/// der Skin-Index des Spielers; er wird im Spielstand unter skinIndex abgelegt.
/// Die Waffennummer ist der Platz in PlayerController.activeWeapon.
///
/// Wenn der Remaster die Charaktere ebenfalls in einen Katalog holt (Name, Bild,
/// Startwerte), ist hier die Stelle dafür.
/// </summary>
public static class Characters
{
    /// <summary>Startwaffe je Charakter - Index in PlayerController.activeWeapon.</summary>
    private static readonly int[] StartWeaponByskin = { 2, 6, 11, 1 };

    public static int Count => StartWeaponByskin.Length;

    public static int StartWeaponIndex(int skinIndex)
    {
        if (StartWeaponByskin.Length == 0) return 0;
        return StartWeaponByskin[Mathf.Clamp(skinIndex, 0, StartWeaponByskin.Length - 1)];
    }
}
