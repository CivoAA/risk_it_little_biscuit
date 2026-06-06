using UnityEngine;

public class UnlocksChecker : MonoBehaviour
{
    void Update()
    {
        if(PlayerController.Instance.playerShots >= 2)
        {
            UnlockManager.Instance.Unlock("unlock_extra_shot");
        }
    }
}
