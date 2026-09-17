using UnityEngine;

/// <summary>
/// Faengt toedliche Treffer ab. Die eigentliche Wiederbelebung liegt in
/// <see cref="PlayerController.TryUseSecondChance"/>, hier werden nur die
/// Ladungen und die Rueckkehrwerte gesetzt.
///
/// stats[level].damage   = Anzahl Wiederbelebungen auf dieser Stufe (absolut)
/// stats[level].range    = HP-Anteil beim Zurueckkommen (0.3 = 30% der Max-HP)
/// stats[level].duration = Unverwundbarkeit direkt nach der Wiederbelebung
/// </summary>
public class SecondChance : Weapon
{
    private int lastAppliedLevel = -2;
    private int appliedCharges;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        int target = Mathf.Max(0, Mathf.RoundToInt(stats[weaponLevel].damage));

        // Nur die Differenz gutschreiben: bereits verbrauchte Ladungen sollen
        // durch ein Level-Up nicht wieder auftauchen.
        player.secondChanceCharges += target - appliedCharges;
        if (player.secondChanceCharges < 0) player.secondChanceCharges = 0;
        appliedCharges = target;

        player.secondChanceHealthPercent = Mathf.Clamp01(stats[weaponLevel].range);
        player.secondChanceImmunity = Mathf.Max(0f, stats[weaponLevel].duration);

        lastAppliedLevel = weaponLevel;

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxSecondChance);
        }
    }
}
