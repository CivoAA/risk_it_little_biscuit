using UnityEngine;

/// <summary>Schuss des <see cref="Companion"/>.</summary>
public class CompanionProjectile : TrackingProjectile
{
    private Companion owner;

    public void Launch(Companion companion, Vector2 flyDirection, Enemy target)
    {
        owner = companion;
        Launch(flyDirection, target);
    }

    protected override void ApplyDamage(Enemy enemy)
    {
        if (owner == null) return;

        enemy.TakeDamage(owner.Damage);
    }
}
