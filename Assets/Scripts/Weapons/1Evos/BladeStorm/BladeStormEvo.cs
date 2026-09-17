using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Blade Storm Evo (Blade Swarm + Void Spike).
///
/// Die Klingen parken in festen Slots um den Spieler, werden im Takt auf ein
/// zufälliges Ziel im Erfassungsbereich abgefeuert, durchdringen alles auf der
/// Flugbahn und kehren danach zurück. Der eigentliche Flug liegt in
/// <see cref="BladeStormEvoPrefab"/>; diese Klasse verwaltet nur Bestand,
/// Orbit-Positionen, Zielwahl und Salventakt.
/// </summary>
public class BladeStormEvo : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    private const int MaxBlades = 9;
    private const float FireInterval = 0.1f;   // Abstand zwischen den Klingen einer Salve

    private readonly List<BladeStormEvoPrefab> blades = new List<BladeStormEvoPrefab>();
    private readonly List<BladeStormEvoPrefab> volley = new List<BladeStormEvoPrefab>();

    private Enemy currentTarget;
    private float attackCounter;

    private bool firing;
    private int fireIndex;
    private float fireTimer;

    /// <summary>Letzte bekannte Position des aktuellen Ziels.</summary>
    public Vector2 TargetPosition { get; private set; }
    public bool HasTarget { get { return currentTarget != null; } }

    void Update()
    {
        blades.RemoveAll(b => b == null);
        enemiesInRange.RemoveAll(e => e == null);

        if (!IsActive)
        {
            DespawnAllBlades();
            return;
        }

        // Achievment Unlocken
        Achievements.Unlock(Ach.BladeSwarmEvo);

        int desired = Mathf.Clamp(
            Mathf.RoundToInt(CurrentStats.shots + PlayerController.Instance.playerShots),
            1, MaxBlades);

        // Gegen blades.Count prüfen, nicht gegen einen Merker: so kommen
        // verloren gegangene Klingen von selbst zurück. Der alte Zähler
        // currentMaxBlades wurde nie kleiner, dadurch blieb jede zerstörte
        // Klinge dauerhaft weg.
        int missing = desired - blades.Count;
        for (int i = 0; i < missing; i++) SpawnBlade();

        PositionOrbitBlades();

        // Frisch gespawnte Klingen erst platzieren lassen, bevor gefeuert wird.
        if (missing > 0) return;

        if (currentTarget != null) TargetPosition = currentTarget.transform.position;

        UpdateVolley();
    }

    private void UpdateVolley()
    {
        if (firing)
        {
            fireTimer -= Time.deltaTime;
            while (firing && fireTimer <= 0f) LaunchNext();
            return;
        }

        // Der Cooldown läuft erst, wenn alle Klingen wieder im Orbit sind.
        // Vorher zählte attackCounter während des ganzen Fluges weiter ins
        // Negative – die nächste Salve startete dadurch ohne jede Pause und
        // der Cooldown-Wert war praktisch wirkungslos.
        if (!AllBladesInOrbit())
        {
            attackCounter = CurrentCooldown;
            return;
        }

        attackCounter -= Time.deltaTime;
        if (attackCounter > 0f) return;

        UpdateTarget();
        if (currentTarget == null) return;

        TargetPosition = currentTarget.transform.position;
        StartVolley();
    }

    private void StartVolley()
    {
        // Auf einer eigenen Liste arbeiten: Update darf blades parallel
        // verändern (Shots-Upgrade, verlorene Klingen), ohne dass hier eine
        // laufende Aufzählung kaputtgeht.
        volley.Clear();
        for (int i = 0; i < blades.Count; i++)
        {
            if (blades[i] != null && blades[i].InOrbit) volley.Add(blades[i]);
        }

        if (volley.Count == 0) return;

        firing = true;
        fireIndex = 0;
        fireTimer = 0f;
        LaunchNext();
    }

    private void LaunchNext()
    {
        while (fireIndex < volley.Count)
        {
            BladeStormEvoPrefab blade = volley[fireIndex];
            fireIndex++;

            if (blade == null || !blade.InOrbit) continue;

            Vector2 aim = TargetPosition - (Vector2)transform.position;
            blade.Launch(transform, aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.up);

            fireTimer += FireInterval;
            return;
        }

        firing = false;
    }

    private bool AllBladesInOrbit()
    {
        for (int i = 0; i < blades.Count; i++)
        {
            if (blades[i] != null && !blades[i].InOrbit) return false;
        }
        return true;
    }

    private void SpawnBlade()
    {
        GameObject obj = Instantiate(prefab, transform.position, Quaternion.identity, transform);
        BladeStormEvoPrefab blade = obj.GetComponent<BladeStormEvoPrefab>();

        if (blade == null)
        {
            Debug.LogError("BladeStormEvo: Prefab hat keine BladeStormEvoPrefab-Komponente!");
            Destroy(obj);
            return;
        }

        blade.weapon = this;
        blades.Add(blade);
    }

    private void DespawnAllBlades()
    {
        if (blades.Count == 0) return;

        for (int i = 0; i < blades.Count; i++)
        {
            if (blades[i] != null) Destroy(blades[i].gameObject);
        }

        blades.Clear();
        volley.Clear();
        firing = false;
    }

    /// <summary>
    /// Setzt alle Klingen im Orbit auf ihre Slots. Die Rotation bleibt dabei
    /// bewusst unangetastet – die regelt die Klinge selbst, sonst überschreiben
    /// sich beide Skripte jeden Frame gegenseitig.
    /// </summary>
    public void PositionOrbitBlades()
    {
        float baseRadius = 1f;
        int orbitIndex = 0;

        for (int i = 0; i < blades.Count; i++)
        {
            BladeStormEvoPrefab blade = blades[i];
            if (blade == null || !blade.InOrbit) continue;

            Vector2 pos = transform.position;

            switch (orbitIndex)
            {
                case 0: pos += (Vector2.left + Vector2.up * 0.4f) * baseRadius; break;
                case 1: pos += (Vector2.right + Vector2.up * 0.4f) * baseRadius; break;
                case 2: pos += Vector2.up * baseRadius * 1.4f; break;
                case 3: pos += (Vector2.left + Vector2.up * 0.8f) * baseRadius; break;
                case 4: pos += (Vector2.right + Vector2.up * 0.8f) * baseRadius; break;
                case 5: pos += (Vector2.left * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f); break;
                case 6: pos += (Vector2.right * 0.7f + Vector2.up * 1.3f) * (baseRadius * 0.8f); break;
                case 7: pos += (Vector2.left * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f); break;
                case 8: pos += (Vector2.right * 0.7f + Vector2.down * 0.25f) * (baseRadius * 0.8f); break;
                default: pos += Vector2.up * (baseRadius * 0.5f); break;
            }

            blade.transform.position = pos;
            orbitIndex++;
        }
    }

    private void UpdateTarget()
    {
        enemiesInRange.RemoveAll(e => e == null);

        if (enemiesInRange.Count == 0)
        {
            currentTarget = null;
            return;
        }

        currentTarget = enemiesInRange[Random.Range(0, enemiesInRange.Count)];
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
