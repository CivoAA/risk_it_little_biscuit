using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yeti-Lawine - die Evo des Schneeballeffekts (Yeti). Rezept:
/// Schneeballeffekt + P. Bubble Gun: der Yeti faengt Gegner ein wie die Blase.
///
/// Zwei Teile:
///   - Zwischendurch wirft sie Schneebaelle wie die Grundwaffe auf
///     Hoechststufe (Frostfeld, Schneesplitter) - so ist man zwischen zwei
///     Walzen nicht wehrlos und die Walze darf einen langen Cooldown haben.
///   - Alle <see cref="RollCooldown"/> Sekunden (Cooldown-Buff zaehlt), sobald
///     Gegner in der Naehe sind, rollt sich der Yeti selbst zur Walze
///     zusammen (<see cref="YetiRoll"/>): schneller, halber Schaden, rollt
///     Gegner ein und landet am Ende mit einer Lawine. Waehrenddessen ruhen
///     die Schneebaelle.
///
/// Ersetzt zwei verworfene Fassungen (24.10.2026): die Schneekugel
/// (Glasglocke) und einen losen Riesenball, der Gegner einsammelte.
///
/// cooldown = Pause zwischen zwei Schneeball-Wuerfen
/// damage   = Grundschaden (Schneebaelle wie Basis; Walze, Einrollen, Lawine rechnen davon)
/// range    = Rollweite der Schneebaelle in Tiles
/// duration = Frost in Sekunden (Duration-Buff zaehlt); verlaengert auch die Walze
/// shots    = Schneebaelle je Wurf (+ einer je 2 Extra-Schuesse)
/// </summary>
public class SnowballEffectEvo : SnowballEffect
{
    /// <summary>So oft rollt sich der Yeti ein (Sekunden, vor Cooldown-Buff).</summary>
    public const float RollCooldown = 10f;

    /// <summary>Erst einrollen, wenn ein Gegner so nah ist (Tiles).</summary>
    public const float RollTrigger = 5f;

    private static readonly WeaponStats[] EvoStats =
    {
        new WeaponStats
        {
            cooldown = 1.8f,
            damage = 6f,
            range = 7f,
            duration = 2.5f,
            shots = 2,
            description = "Der Yeti rollt sich zur Walze, rollt Gegner ein und landet mit einer Lawine",
        },
    };

    protected override int TableLevel => int.MaxValue;

    private float rollTimer = 2f;

    protected override void Awake()
    {
        base.Awake();
        stats = new List<WeaponStats>(EvoStats);
        maxweaponLevel = 0;
    }

    protected override void Update()
    {
        if (!CanAttack || PlayerController.Instance == null) return;
        if (!IsCopy) Achievements.Unlock(Ach.SnowballEffectEvo);

        // Waehrend der Walze ruhen die Schneebaelle, und der naechste Timer laeuft erst danach.
        if (YetiRoll.Active) return;

        base.Update();

        rollTimer -= Time.deltaTime;
        if (rollTimer > 0f || !EnemyNear()) return;

        // Die Kopie im Kruemelchaos des Tofus rollt nicht - die Walze ist der Yeti selbst.
        if (IsCopy)
        {
            rollTimer = RollCooldown;
            return;
        }

        PlayerController player = PlayerController.Instance;
        float duration = YetiRoll.Duration * SweetFx.DurationFactor;
        if (YetiRoll.Begin(player, CurrentStats.damage, duration, SweetFx.Aoe,
                           CurrentStats.duration * SweetFx.DurationFactor, CurrentRoll()))
        {
            float mult = player.CooldownMultiplier;
            rollTimer = RollCooldown * mult + duration;
        }
    }

    private bool EnemyNear()
    {
        Vector2 origin = transform.position;
        float sq = RollTrigger * RollTrigger;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable) continue;
            if (((Vector2)e.transform.position - origin).sqrMagnitude <= sq) return true;
        }
        return false;
    }

    void OnDisable()
    {
        // Evo weg (Opferschrein, Laufende) - die Walze nicht weiterrollen lassen.
        if (!IsCopy) YetiRoll.Cancel();
    }
}
