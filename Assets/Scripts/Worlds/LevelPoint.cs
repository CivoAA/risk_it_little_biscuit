using UnityEngine;

/// <summary>
/// VERALTET - nur noch ein Platzhalter.
///
/// Hier lagen früher drei Dinge, die jetzt woanders besser aufgehoben sind:
///   * die Wertetabellen je Shop-Stufe  -> stehen am Eintrag in <see cref="Shop"/>
///   * das float[30] extraData          -> ersetzt durch Shop.Get / Shop.IsBought
///   * die Zuordnung Charakter -> Startwaffe -> <see cref="Characters"/>
///
/// Diese Komponente existiert nur, damit das alte GameObject in
/// "World Map.unity" kein fehlendes Skript anzeigt. Sie entfernt sich selbst.
///
/// ZU TUN (einmalig, in Unity): das GameObject mit dieser Komponente in der
/// World Map löschen. Danach kann diese Datei weg.
/// </summary>
[System.Obsolete("Shop-Werte laufen jetzt über Shop.cs. Objekt aus der Szene löschen.")]
[AddComponentMenu("")]
public class LevelPoint : MonoBehaviour
{
    private static bool warned;

    private void Awake()
    {
        if (!warned)
        {
            warned = true;
            Debug.LogWarning(
                $"[Shop] Das alte LevelPoint-Objekt in Szene '{gameObject.scene.name}' wird nicht mehr " +
                "gebraucht und hat sich entfernt. Es darf in der Szene gelöscht werden.");
        }

        Destroy(gameObject);
    }
}
