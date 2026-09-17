using UnityEngine;

/// <summary>
/// Merkt sich über den Szenenwechsel hinweg, welche Karte gespielt wird.
///
/// Die Shop-Werte liefen früher ebenfalls hierüber (float[30] extraData). Das
/// macht jetzt <see cref="Shop"/> selbst: beim Betreten einer Karte friert
/// Shop.CaptureRun() den Stand ein, und die Game-Szene fragt ihn über
/// Shop.Get / Shop.IsBought ab.
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
}
