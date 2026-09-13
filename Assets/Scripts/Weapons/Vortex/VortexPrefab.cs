using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein einzelner Strudel des <see cref="Vortex"/>. Zieht jeden Frame alle
/// Gegner im Trigger Richtung Mitte und verteilt im Tick-Takt Schaden.
///
/// Der Zug laeuft ueber <see cref="Enemy.ApplyPull"/> und wird jeden Frame
/// erneuert. Wird der Wirbel zerstoert, laeuft er von selbst aus - es bleibt
/// also kein Gegner haengen.
/// </summary>
public class VortexPrefab : MonoBehaviour
{
    public Vortex weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    [Tooltip("Zuggeschwindigkeit am Rand des Wirbels (Einheiten pro Sekunde).")]
    [SerializeField] private float pullSpeed = 4f;

    [Tooltip("Ab diesem Abstand zur Mitte wird nicht weiter gezogen - verhindert Zittern.")]
    [SerializeField] private float deadZone = 0.25f;

    [Tooltip("Umdrehungen pro Sekunde fuer die Optik.")]
    [SerializeField] private float spinSpeed = 180f;

    private float lifeTimer;
    private float tickCounter;

    void Start()
    {
        if (weapon == null)
        {
            weapon = WeaponFinder.Find<Vortex>("Vortex");
        }

        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        lifeTimer = weapon.CurrentDuration;
        transform.localScale = Vector3.one
            * weapon.CurrentStats.range
            * PlayerController.Instance.AOERange;
    }

    void Update()
    {
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        PullEnemies();
        DamageTick();
    }

    private void PullEnemies()
    {
        Vector2 center = transform.position;

        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemiesInRange[i];
            if (enemy == null)
            {
                enemiesInRange.RemoveAt(i);
                continue;
            }

            // Bosse ignorieren den Zug ohnehin (siehe Enemy.ApplyPull), hier
            // sparen wir uns nur die Rechnung.
            if (enemy.IsBoss) continue;

            Vector2 toCenter = center - (Vector2)enemy.transform.position;
            float distance = toCenter.magnitude;

            if (distance <= deadZone) continue;

            enemy.ApplyPull(toCenter.normalized * pullSpeed);
        }
    }

    private void DamageTick()
    {
        tickCounter -= Time.deltaTime;
        if (tickCounter > 0f) return;

        tickCounter = Mathf.Max(0.05f, weapon.CurrentStats.AttackSpeed);

        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            if (enemiesInRange[i] == null)
            {
                enemiesInRange.RemoveAt(i);
                continue;
            }

            enemiesInRange[i].TakeDamage(weapon.CurrentStats.damage);
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null && !enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null) enemiesInRange.Remove(enemy);
    }
}
