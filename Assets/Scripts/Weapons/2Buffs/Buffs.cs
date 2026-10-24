using UnityEngine;

public class Buffs : Weapon
{
    private int lastAppliedLevel = -2;
    private float applied;   // Summe aller verbuchten Stufen - fuer RemoveEffect

    void Update()
    {
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            // Werte übernehmen
            PlayerController.Instance.playerMaxHealth += stats[weaponLevel].damage;
            applied += stats[weaponLevel].damage;
            PlayerController.Instance.Heal(PlayerController.Instance.playerMaxHealth / 3);

            // neuen Stand merken
            lastAppliedLevel = weaponLevel;
        }
    }

    public override void RemoveEffect()
    {
        PlayerController player = PlayerController.Instance;
        if (player != null)
        {
            player.playerMaxHealth = Mathf.Max(1f, player.playerMaxHealth - applied);
            if (player.playerHealth > player.playerMaxHealth) player.playerHealth = player.playerMaxHealth;
            if (UIController.Instance != null) UIController.Instance.UpdateHealthSlider();
        }
        applied = 0f;
    }
}
