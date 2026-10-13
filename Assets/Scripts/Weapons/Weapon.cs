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
    /// Eine Kopie, die das Kruemelchaos des Tofus angelegt hat
    /// (<see cref="CrumbMayhem"/>). Sie steht in keiner Liste des Spielers,
    /// zaehlt fuer keinen Erfolg und wird nie aufgelevelt.
    /// </summary>
    [System.NonSerialized] public bool IsCopy;

    /// <summary>
    /// Kopie, die gerade nicht dran ist: greift nicht neu an, laesst aber
    /// alles auslaufen, was schon unterwegs ist oder liegt (Marmeladenlachen,
    /// fliegende Klingen, ...). Deshalb bleibt <see cref="IsActive"/> dabei true.
    /// </summary>
    [System.NonSerialized] public bool Resting;

    /// <summary>Darf die Waffe gerade neu angreifen? Waffen fragen das statt <see cref="IsActive"/>.</summary>
    public bool CanAttack => IsActive && !Resting;

    /// <summary>
    /// Stufe fuer die Tabellen neben <see cref="stats"/> (Durchschlag,
    /// Spruenge, Ringgroesse ...). Normal die Waffenstufe; eine Evo mit nur
    /// einer Stufe stellt das auf die hoechste ihrer Basiswaffe.
    /// </summary>
    protected virtual int TableLevel => weaponLevel;

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

    /// <summary>
    /// So viele Extra-Schuesse braucht die Waffe fuer ein Geschoss mehr
    /// (2 = halb so stark wie 1:1). 0 = Extra-Schuss wirkt hier nicht.
    /// Waffen mit wenigen, schweren Treffern stehen hoeher, damit ein
    /// Extra-Schuss jeder Waffe ungefaehr gleich viel bringt.
    /// </summary>
    protected virtual int ExtraShotsPerShot => 1;

    /// <summary>Extra-Schuesse nach <see cref="ExtraShotsPerShot"/>, mit Bruchteil.</summary>
    private float ExtraShotValue
    {
        get
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || ExtraShotsPerShot <= 0) return 0f;
            return Mathf.Max(0f, player.playerShots) / ExtraShotsPerShot;
        }
    }

    /// <summary>
    /// Zusatz-Geschosse fuer EINEN Angriff: ganze sicher, der Rest als Chance
    /// (0,6 = 60 % auf eins mehr). Pro Angriff genau einmal aufrufen.
    /// </summary>
    protected int RollExtraShots()
    {
        float value = ExtraShotValue;
        int whole = Mathf.FloorToInt(value + 0.0001f);
        float rest = value - whole;
        return rest > 0.0001f && Random.value < rest ? whole + 1 : whole;
    }

    /// <summary>
    /// Zusatz fuer Dinge, die dauerhaft da sind (Irrlichter, Klingen im
    /// Orbit): nur ganze, sonst wuerde die Zahl jeden Frame springen.
    /// </summary>
    protected int WholeExtraShots => Mathf.FloorToInt(ExtraShotValue + 0.0001f);

    public void LevelUP()
    {
        if (weaponLevel < stats.Count - 1)
        {
            weaponLevel++;
        }

        // Maximalstufe: der Erfolg steht im Katalog (Ach.MaxLevelFor), nicht in jeder Waffe.
        if (maxweaponLevel > 0 && weaponLevel >= maxweaponLevel)
            Achievements.Unlock(Ach.MaxLevelFor(weaponID));
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

    /// <summary>Eigene Kopie - die Stufen der Waffen liegen oft als statische Tabelle vor.</summary>
    public WeaponStats Clone() => (WeaponStats)MemberwiseClone();
}
