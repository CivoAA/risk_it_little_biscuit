using System;
using System.Collections.Generic;

[Serializable]
public class SkillEntry
{
    public string skillName;
    public bool unlocked;
    public float value;
}

[Serializable]
public class SkillSaveData
{
    public List<int> unlockedSkillIDs = new List<int>();

    // 💰 neue Variable für Skill-Währung
    public int skillCurrency = 0;
}
