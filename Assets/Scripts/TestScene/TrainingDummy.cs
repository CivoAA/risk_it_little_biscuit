using UnityEngine;

/// <summary>
/// Unverwundbarer Trainings-Dummy für die Test-Szene.
///
/// Erbt von <see cref="Enemy"/>, damit alle Waffen ihn ganz normal treffen
/// (sie suchen per <c>GetComponent&lt;Enemy&gt;()</c> und Tag "Enemy").
/// Er bewegt sich nicht, stirbt nicht und macht dem Spieler keinen Schaden.
/// Jeder Treffer wird an den <see cref="DamageMeter"/> gemeldet.
/// </summary>
public class TrainingDummy : Enemy
{
    [Header("Dummy")]
    [Tooltip("Schadenszahlen über dem Dummy anzeigen (wie im echten Spiel).")]
    public bool showDamageNumbers = true;

    [Tooltip("Treffer an den DamageMeter melden (DPS/DPM-Anzeige).")]
    public bool countForMeter = true;

    private Rigidbody2D body;

    protected override void Start()
    {
        body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            // Kinematic: der Dummy bleibt stehen, blockt den Spieler aber weiterhin.
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
        }
    }

    protected override void FixedUpdate()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    // Der Dummy tut dem Spieler nichts.
    protected override void OnCollisionStay2D(Collision2D collision) { }
    protected override void OnTriggerStay2D(Collider2D other) { }

    /// <summary>
    /// Spiegelt die Schadensberechnung aus <see cref="Enemy.TakeDamage"/>
    /// (Damage-Multiplier + Crit), zieht aber keine Leben ab.
    /// </summary>
    public override void TakeDamage(float damage, float? slowMultiplier = null)
    {
        // Abgeschaltete Dummies (kleinere Gruppe gewählt) dürfen nicht mitzählen:
        // manche Waffen halten ihre Zielliste noch einen Moment länger.
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        PlayerController player = PlayerController.Instance;

        float finalDamage = damage;
        bool isCrit = false;

        if (player != null)
        {
            finalDamage = damage * player.damageMultiplier;
            isCrit = Random.value < player.critChance;
            if (isCrit)
            {
                finalDamage *= player.critDamage;
            }
        }

        if (showDamageNumbers && DamageNumberController.Instance != null)
        {
            if (isCrit)
            {
                DamageNumberController.Instance.CreateNumberCrit(finalDamage, transform.position);
            }
            else
            {
                DamageNumberController.Instance.CreateNumber(finalDamage, transform.position);
            }
        }

        // Lifesteal soll sich im Test genauso verhalten wie im Spiel.
        if (LifeSteal.Instance != null)
        {
            LifeSteal.Instance.StealLife((int)damage);
        }

        if (countForMeter)
        {
            DamageMeter.Report(finalDamage, isCrit);
        }
    }
}
