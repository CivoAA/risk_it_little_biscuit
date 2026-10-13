using UnityEngine;

/// <summary>
/// Erster Buff mit Nachteil: deutlich mehr Schaden, dafuer tut jeder Treffer mehr weh.
///
/// stats[level].damage = Schadensbonus auf dieser Stufe (0.5 = +50%)
/// stats[level].range  = mehr erlittener Schaden als Anteil (0.4 = +40%)
///
/// Beide Werte sind absolut pro Stufe, nicht kumulativ. Beim Level-Up wird der
/// alte Stand erst zurueckgenommen und dann der neue verbucht.
/// </summary>
public class GlassCannon : Weapon
{
    private int lastAppliedLevel = -2;
    private float appliedDamage;
    private float appliedDamageTaken;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        // Alten Stand zuruecknehmen
        player.damageMultiplier -= appliedDamage;
        player.damageTakenMultiplier -= appliedDamageTaken;

        appliedDamage = Mathf.Max(0f, stats[weaponLevel].damage);
        appliedDamageTaken = Mathf.Max(0f, stats[weaponLevel].range);

        player.damageMultiplier += appliedDamage;
        player.damageTakenMultiplier += appliedDamageTaken;

        lastAppliedLevel = weaponLevel;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxGlassCannon);
        }
    }
}
