#if UNITY_INCLUDE_TESTS
using UnityEngine;

/// <summary>
/// Test-Gegner: zählt nur mit, wie viel Schaden er bekommt.
/// Erbt von <see cref="Enemy"/>, damit GetComponent&lt;Enemy&gt;() in den
/// Waffen-Skripten greift, lässt aber Bewegung und Sterbe-Logik weg – die
/// braucht Manager, die im Test nichts zu suchen haben.
/// </summary>
public class DamageProbe : Enemy
{
    public float totalDamage;
    public int hits;

    protected override void Start() { }
    protected override void FixedUpdate() { }

    public override void TakeDamage(float damage, float? slowMultiplier = null)
    {
        totalDamage += damage;
        hits++;
    }
}
#endif
