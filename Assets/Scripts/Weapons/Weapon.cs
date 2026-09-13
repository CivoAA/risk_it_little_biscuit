using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    /// <summary>
    /// weaponLevel, das eine Waffe bekommt, wenn sie durch eine Evo ersetzt wird
    /// (siehe LevelUpButton.SelectUpgrade).
    /// </summary>
    public const int RemovedLevel = -99;

    public string weaponID;
    public int weaponLevel;
    public int maxweaponLevel;
    public List<WeaponStats> stats;
    public Sprite weaponImage;
    public Sprite weaponIcon;
    public Sprite weaponIconEvo;
    public bool posssibleEvo = false;
    public bool hasBeenRemoved = false;

    /// <summary>
    /// True, wenn die Waffe aktiv ist und <see cref="stats"/> mit
    /// <see cref="weaponLevel"/> indiziert werden darf. Nicht erhaltene Waffen
    /// stehen auf -1, durch eine Evo ersetzte auf <see cref="RemovedLevel"/>.
    /// </summary>
    public bool IsActive
    {
        get { return stats != null && weaponLevel >= 0 && weaponLevel < stats.Count; }
    }

    /// <summary>
    /// Werte der aktuellen Stufe, oder null wenn die Waffe nicht aktiv ist.
    /// Projektile, die ihre Waffe überleben, müssen das prüfen.
    /// </summary>
    public WeaponStats CurrentStats
    {
        get { return IsActive ? stats[weaponLevel] : null; }
    }

    /// <summary>
    /// Cooldown der aktuellen Stufe nach dem globalen Cooldown-Buff
    /// (<see cref="PlayerController.CooldownMultiplier"/>). Waffen und Evos
    /// setzen ihren Timer ausschliesslich hierueber, damit der Buff nicht an
    /// einzelnen Waffen vorbeilaeuft.
    /// </summary>
    public float CurrentCooldown
    {
        get
        {
            if (!IsActive) return 0f;
            float mult = PlayerController.Instance != null
                ? PlayerController.Instance.CooldownMultiplier
                : 1f;
            return CurrentStats.cooldown * mult;
        }
    }

    /// <summary>
    /// Wirkdauer der aktuellen Stufe nach dem globalen Duration-Buff.
    /// Bewusst NICHT dort verwenden, wo duration als Abstand zwischen zwei
    /// Spawns dient (Void Spike, Blade Swarm) - laenger waere dort schlechter.
    /// </summary>
    public float CurrentDuration
    {
        get
        {
            if (!IsActive) return 0f;
            float mult = PlayerController.Instance != null
                ? PlayerController.Instance.DurationMultiplier
                : 1f;
            return CurrentStats.duration * mult;
        }
    }

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
