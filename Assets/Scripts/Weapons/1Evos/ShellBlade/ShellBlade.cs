using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Schalenklinge - Evo aus Schwerthieb + Armor.
///
/// Schlaegt wie der <see cref="SwordSlash"/> (erbt von ihm) nur nach vorne,
/// aber ohne Serien und ohne Cooldown dazwischen: ein Hieb nach dem anderen,
/// hin und her, mit einer kurzen Pause (cooldown). Die Pause verkuerzt nur
/// Cooldown (Buff, Skilltree) - Extra-Schuss zaehlt bewusst nicht.
///
/// Dazu waechst dem Ritter alle 2 Sekunden eine Zwiebelschale
/// (<see cref="OnionShell"/>), die einen ganzen Treffer abfaengt - hoechstens
/// eine, mit Zwiebelhaut drei.
///
/// Skilltree (Zwiebelritter) laeuft ueber den Schwerthieb mit:
///   Zwiebelring - hier ist jeder 3. Hieb eine Drehung.
///   Gegenschlag - wie beim Schwerthieb; eine platzende Schale zaehlt als Treffer.
///
/// cooldown = Pause zwischen zwei Hieben
/// damage   = Schaden pro Hieb
/// range    = Tiefe des Bereichs in Tiles
/// </summary>
public class ShellBlade : SwordSlash
{
    /// <summary>Schaden eines Hiebs relativ zur hoechsten Schwerthieb-Stufe.</summary>
    public const float DamageFactor = 1.25f;

    /// <summary>Pause zwischen zwei Hieben (vor Cooldown-Buff und Skilltree).</summary>
    public const float SwingPause = 0.3f;

    private int swingCount;

    protected override int TableLevel => int.MaxValue;

    protected override void Awake()
    {
        base.Awake();
        WeaponStats max = stats[stats.Count - 1].Clone();
        max.cooldown = SwingPause;
        max.damage *= DamageFactor;
        max.shots = 1;
        max.description = "Hiebe ohne Pause, Zwiebelschale alle 2 s";

        stats = new List<WeaponStats> { max };
        maxweaponLevel = 0;
    }

    protected override void Update()
    {
        base.Update();

        if (CanAttack)
        {
            int maxShells = Skills.HasGrant(SkillGrants.Zwiebelhaut) ? OnionShell.SkinShells : OnionShell.BaseShells;
            OnionShell.Ensure(PlayerController.Instance)?.Charge(maxShells);
        }
    }

    void LateUpdate()
    {
        // Die Evo hat nur eine Stufe: aktiv sein heisst, sie wurde erhalten.
        if (IsActive && !IsCopy) Achievements.Unlock(Ach.SwordSlashEvo);
    }

    /// <summary>Ein einzelner Hieb, abwechselnd hin und her; mit Zwiebelring jeder 3. als Drehung.</summary>
    protected override IEnumerator Attack()
    {
        swingCount++;
        bool circle = Skills.HasGrant(SkillGrants.Zwiebelring) && swingCount % CircleEvery == 0;
        Swing(MoveDir, swingCount % 2 == 0, circle ? SwingKind.Circle : SwingKind.Normal);
        yield break;
    }

    protected override float SwingInterval => CurrentCooldown;
}
