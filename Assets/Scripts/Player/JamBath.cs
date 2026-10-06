using UnityEngine;

/// <summary>
/// Skilltree "Marmeladenbad" (<see cref="SkillGrants.Marmeladenbad"/>): wer
/// <see cref="SettleTime"/> Sekunden still in der eigenen Marmelade steht, heilt
/// <see cref="HealAmount"/> Leben alle <see cref="HealInterval"/> Sekunden.
/// Gilt fuer die Lachen von Jam Jar und Sticky Shatter. Laeuft ueber
/// <see cref="PlayerController.Heal"/>, volle HP gehen also mit Seelenschild in
/// den Schild.
///
/// Haengt am Spieler und wird zu Laufbeginn angelegt, wenn der Knoten offen ist.
/// </summary>
public class JamBath : MonoBehaviour
{
    /// <summary>So lange muss man still in der Marmelade stehen, bis es losgeht.</summary>
    public const float SettleTime = 1f;

    /// <summary>Abstand zwischen zwei Heilungen.</summary>
    public const float HealInterval = 0.25f;

    /// <summary>Leben je Heilung.</summary>
    public const float HealAmount = 1f;

    private PlayerController player;
    private float stillTime;
    private float healTimer;

    public static void Ensure(PlayerController player)
    {
        if (player != null && player.GetComponent<JamBath>() == null)
        {
            player.gameObject.AddComponent<JamBath>();
        }
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (player == null || Time.deltaTime <= 0f) return;

        bool bathing = player.playerMoveDirection == Vector3.zero
                       && AreaWeaponPrefabJamJar.IsInAnyPuddle(transform.position);

        if (!bathing)
        {
            stillTime = 0f;
            healTimer = 0f;
            return;
        }

        stillTime += Time.deltaTime;
        if (stillTime < SettleTime) return;

        healTimer -= Time.deltaTime;
        if (healTimer > 0f) return;

        healTimer += HealInterval;
        if (player.CanReceiveHealing) player.Heal(HealAmount);
    }
}
