using UnityEngine;

/// <summary>
/// Merkt sich über den Szenenwechsel hinweg, welche Karte gespielt wird.
///
/// Die Shop-Werte liefen früher ebenfalls hierüber (float[30] extraData). Das
/// macht jetzt <see cref="Shop"/> selbst: beim Betreten einer Karte friert
/// Shop.CaptureRun() den Stand ein, und die Game-Szene fragt ihn über
/// Shop.Get / Shop.IsBought ab.
///
/// Als Szenen-Objekt steht er nur in der World Map. Wer von woanders aus ein
/// Level startet - Hub-Levelauswahl, Test-Szene - kommt über <see cref="Ensure"/>
/// an einen Manager, der sich notfalls selbst anlegt. Ohne ihn bliebe
/// selectedMap ungesetzt und der <see cref="WorldSelector"/> in der Game-Szene
/// wüsste nicht, welche Welt gemeint ist.
/// </summary>
public class MapsManager : MonoBehaviour
{
    public static MapsManager Instance;
    public int selectedMap;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Liefert den Manager und legt ihn an, falls die aktuelle Szene keinen
    /// mitbringt. Das Objekt wird vor dem AddComponent deaktiviert, damit Awake
    /// erst läuft, wenn es fertig ist - sonst könnte ein zweiter Manager es
    /// direkt wieder zerstören.
    /// </summary>
    public static MapsManager Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("MapsManager (auto)");
        go.SetActive(false);
        MapsManager maps = go.AddComponent<MapsManager>();
        maps.selectedMap = 0;
        go.SetActive(true);

        return Instance != null ? Instance : maps;
    }
}
