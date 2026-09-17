using UnityEngine;

/// <summary>
/// VERALTET - nur noch ein Platzhalter.
///
/// Währung, Charakter und die gekauften Shop-Stufen liegen jetzt in
/// <see cref="Shop"/> bzw. <see cref="ShopStore"/>. Der Katalog der Einträge
/// (Name, Preis, Wirkung) steht in Shop.cs statt in einer Inspector-Liste.
///
/// Die Speicherdatei heisst weiterhin save.json; ein alter Spielstand wird beim
/// ersten Start automatisch übernommen und als save.json.v1.bak gesichert.
///
/// Diese Komponente existiert nur, damit das alte GameObject in
/// "World Map.unity" kein fehlendes Skript anzeigt. Sie entfernt sich selbst.
///
/// ZU TUN (einmalig, in Unity): das GameObject "SaveGame" in der World Map
/// löschen. Danach kann diese Datei weg.
/// </summary>
[System.Obsolete("Shop-Spielstand läuft jetzt über Shop.cs. Objekt aus der Szene löschen.")]
[AddComponentMenu("")]
public class SaveGame : MonoBehaviour
{
    private static bool warned;

    private void Awake()
    {
        if (!warned)
        {
            warned = true;
            Debug.LogWarning(
                $"[Shop] Das alte SaveGame-Objekt in Szene '{gameObject.scene.name}' wird nicht mehr " +
                "gebraucht und hat sich entfernt. Es darf in der Szene gelöscht werden.");
        }

        Destroy(gameObject);
    }
}
