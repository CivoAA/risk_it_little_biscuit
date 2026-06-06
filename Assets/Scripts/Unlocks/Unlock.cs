using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Unlock
{
    public string id;            
    public string displayName;
    public string description;
    public Sprite unlockedIcon;
    public bool isUnlocked;
}

[System.Serializable]
public class UnlockData
{
    public string id;
    public bool isUnlocked;
}

[System.Serializable]
public class UnlockDataList
{
    public List<UnlockData> unlocks;
}