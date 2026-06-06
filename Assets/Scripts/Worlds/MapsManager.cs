using UnityEngine;

public class MapsManager : MonoBehaviour
{
    public static MapsManager Instance;
    public int selectedMap;
    public float[] extraData;

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
