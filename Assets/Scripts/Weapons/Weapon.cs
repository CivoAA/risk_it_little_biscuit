using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public string weaponID;
    public int weaponLevel;
    public int maxweaponLevel;
    public List<WeaponStats> stats;
    public Sprite weaponImage;
    public Sprite weaponIcon;
    public Sprite weaponIconEvo;
    public bool posssibleEvo = false;
    public bool hasBeenRemoved = false;

    public void LevelUP()
    {
        if (weaponLevel < stats.Count - 1)
        {
            weaponLevel++;
        }
    }
    void Update()
    {
        if (weaponLevel <= -10 && posssibleEvo == true)
        {
            posssibleEvo = false;
        }
    }
}

[System.Serializable]
public class WeaponStats
{
    public float cooldown;
    public float duration;
    public float damage;
    public float range;
    public float AttackSpeed;
    public float shots;
    public string description;
}
