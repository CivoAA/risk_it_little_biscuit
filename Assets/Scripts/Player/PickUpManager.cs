using UnityEngine;

public class PickUpManager : MonoBehaviour
{
    public static PickUpManager Instance;
    public GameObject heart_PickUP;
    public GameObject magnet_PickUP;

    private void Awake()
    {
        Instance = this;
    }
}
