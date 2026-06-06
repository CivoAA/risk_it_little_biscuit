using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Achievement
{
    public string id;       // z.B. "KILL_100_ENEMIES"
    public string note; // deine interne Beschreibung / was es triggert
    public bool unlocked;   // freigeschaltet?
    public float value; 
    public float maxvalue = 1; 
    public string AchievmentName; 
    public string AchievmentNameDescription; 
    public Sprite icon;
}

[Serializable]
public class AchievementList
{
    public List<Achievement> achievements = new List<Achievement>();
}