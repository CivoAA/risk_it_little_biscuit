using UnityEngine;

public class HPReg : Weapon
{
    private float regTimer = 0f;
    private int lastAppliedLevel = -2; 

    void Update()
    {
        if (PlayerController.Instance == null)
            return;

        // ► Level-Up-Handling: beim neuen Level einmalig den Reg-Wert addieren
        if (weaponLevel != lastAppliedLevel && weaponLevel >= 0)
        {
            PlayerController.Instance.playerHealthReg += stats[weaponLevel].damage;
            lastAppliedLevel = weaponLevel;
        }

        // Timer runterzählen
        regTimer -= Time.deltaTime;

        // ► Cooldown bestimmen (lesbar per if)
        float currentCooldown;
        if (weaponLevel >= 0)
        {
            currentCooldown = stats[weaponLevel].cooldown;
        }
        else
        {
            currentCooldown = 2.5f; // läuft auch bei Level -1
        }

        // ► Tick: nur heilen, wenn nicht voll
        if (regTimer <= 0f && PlayerController.Instance.playerHealth < PlayerController.Instance.playerMaxHealth)
        {
            PlayerController.Instance.PlayerHealthReg(); // nimmt den Wert aus playerHealthReg
            UIController.Instance.UpdateHealthSlider();
            regTimer = currentCooldown;
        }
    }
}
