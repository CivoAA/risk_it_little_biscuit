using UnityEngine;

/// <summary>
/// Projektil des <see cref="TurretPrefab"/>. Fliegt seinem Ziel hinterher und
/// verschwindet beim ersten Treffer oder nach Ablauf seiner Lebenszeit.
/// </summary>
public class TurretProjectile : TrackingProjectile
{
    private Turret weapon;

    public void Launch(Turret owner, Vector2 flyDirection, Enemy target)
    {
        weapon = owner;
        Launch(flyDirection, target);
    }

    protected override void ApplyDamage(Enemy enemy)
    {
        // Der Turm ueberlebt seine Waffe nicht, das Projektil kann es aber:
        // deshalb hier noch einmal pruefen statt blind CurrentStats zu lesen.
        if (weapon == null || !weapon.IsActive) return;

        enemy.TakeDamage(weapon.CurrentStats.damage);
    }
}
