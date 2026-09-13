using UnityEngine;

/// <summary>
/// Erster Buff mit Nachteil: deutlich mehr Schaden gegen weniger Max-HP.
///
/// stats[level].damage = Schadensbonus auf dieser Stufe (0.5 = +50%)
/// stats[level].range  = Max-HP-Malus als Anteil (0.3 = -30%)
///
/// Beide Werte sind absolut pro Stufe, nicht kumulativ. Der HP-Malus wird beim
/// Level-Up erst zurueckgenommen und dann neu berechnet, sonst wuerde er sich
/// auf einer bereits verkleinerten Basis stapeln und die Lebensleiste
/// Richtung 0 fressen.
/// </summary>
public class GlassCannon : Weapon
{
    [Tooltip("Obergrenze fuer den HP-Malus, damit der Buff nicht toedlich ist.")]
    [SerializeField] private float maxHealthPenalty = 0.6f;

    private int lastAppliedLevel = -2;
    private float appliedDamage;
    private float appliedHealthLoss;

    void Update()
    {
        if (weaponLevel == lastAppliedLevel || weaponLevel < 0) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        // Alten Stand zuruecknehmen
        player.damageMultiplier -= appliedDamage;
        player.playerMaxHealth += appliedHealthLoss;

        // Neuen Stand auf der unverfaelschten Basis berechnen
        appliedDamage = Mathf.Max(0f, stats[weaponLevel].damage);
        float penalty = Mathf.Clamp(stats[weaponLevel].range, 0f, maxHealthPenalty);
        appliedHealthLoss = player.playerMaxHealth * penalty;

        player.damageMultiplier += appliedDamage;
        player.playerMaxHealth -= appliedHealthLoss;

        // Mindestens 1 HP Kapazitaet lassen, sonst ist der naechste Tick toedlich
        if (player.playerMaxHealth < 1f)
        {
            appliedHealthLoss -= 1f - player.playerMaxHealth;
            player.playerMaxHealth = 1f;
        }

        if (player.playerHealth > player.playerMaxHealth)
        {
            player.playerHealth = player.playerMaxHealth;
        }

        UIController.Instance.UpdateHealthSlider();
        lastAppliedLevel = weaponLevel;

        if (weaponLevel == maxweaponLevel)
        {
            AchievementManager.Instance?.UnlockAchievement("Max_Level_GlassCannon");
        }
    }
}
