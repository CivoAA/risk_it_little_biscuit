using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BladeSwarm : Weapon
{
    [SerializeField] private GameObject prefab;
    public List<Enemy> enemiesInRange = new List<Enemy>();
    private List<GameObject> activeBlades = new List<GameObject>();
    public Vector2 target;
    private Enemy currentTarget;
    private float attackCounter;
    private float durationCounter;
    private bool shooting = false;

    void Update()
    {
        // kaputte Objekte rauswerfen
        activeBlades.RemoveAll(b => b == null);

        // ► Waffe nicht aktiv: entweder noch nicht erhalten (-1) oder durch die
        // Evo ersetzt (Weapon.RemovedLevel). Früher wurde hier auf -10 geprüft –
        // diesen Wert setzt aber niemand, dadurch blieben die Klingen nach dem
        // Evo-Kauf als wirkungslose "Geister" am Spieler hängen und warfen bei
        // jedem Gegnerkontakt eine IndexOutOfRangeException.
        if (!IsActive)
        {
            if (activeBlades.Count > 0)
            {
                foreach (var blade in activeBlades)
                {
                    if (blade != null) Destroy(blade);
                }
                activeBlades.Clear();
            }
            return; // nichts mehr machen
        }

        if (weaponLevel == maxweaponLevel)
        {
            Achievements.Unlock(Ach.MaxBladeSwarm);
            Unlocks.Grant(Unlocks.BladeSwarm);
        }

        attackCounter -= Time.deltaTime;
        durationCounter -= Time.deltaTime;
        if (durationCounter <= 0f && !shooting)
        {
            // Bewusst ohne CurrentDuration: duration ist hier der Abstand bis zur
            // naechsten Klinge - der Duration-Buff wuerde die Waffe verlangsamen.
            durationCounter = stats[weaponLevel].duration;
            SpawnBlade();
        }
        if (attackCounter <= 0f)
        {
            UpdateTarget();
            if (target != Vector2.zero)
            {
                shooting = true;
                attackCounter = CurrentCooldown;
                StartCoroutine(FireAllBladesAtTarget(target));
            }
        }
    }
    private void SpawnBlade()
    {
        if (activeBlades.Count >= stats[weaponLevel].shots + ExtraBlades)
        {
            return;
        }
        else
        {
            Vector3 spawnPos = transform.position;
            GameObject blade = Instantiate(prefab, spawnPos, transform.rotation);
            blade.transform.SetParent(transform);

            activeBlades.Add(blade);
            PositionBlades();
        }
    }
    private void PositionBlades()
    {
        for (int i = 0; i < activeBlades.Count; i++)
        {
            if (activeBlades[i] == null) continue;

            float angle = RingAngle(i, activeBlades.Count);
            activeBlades[i].transform.localPosition = RingOffset(angle, activeBlades.Count, RingRadius, RingSpacing);
            activeBlades[i].transform.rotation = Quaternion.Euler(0f, 0f, angle + SpriteAngle);
        }
    }

    /// <summary>
    /// So viele Extra-Schuesse braucht es fuer einen Kunai mehr. Die Waffe hat
    /// viele kleine Treffer und skaliert sonst zu stark mit Extra-Schuss; die
    /// Kunai, die sie pro Stufe dazubekommt, bleiben davon unberuehrt.
    /// </summary>
    public const int ExtraShotsPerBlade = 2;

    /// <summary>Kunai aus Extra-Schuessen. Auch von <see cref="BladeStormEvo"/> genutzt.</summary>
    public static int ExtraBlades =>
        PlayerController.Instance != null ? PlayerController.Instance.ExtraShots / ExtraShotsPerBlade : 0;

    /// <summary>Ring-Radius bis zur Kunai-Mitte: halbe Kunai-Laenge (~0.64) + halbe Figur (0.5) + Luft.</summary>
    public const float RingRadius = 1.25f;

    /// <summary>Mindestabstand zweier Kunai auf dem Ring (Kunai ~0.3 breit + Luft).</summary>
    public const float RingSpacing = 0.45f;

    /// <summary>Das Kunai-Sprite zeigt bei Rotation 0 mit der Spitze nach rechts oben (45 Grad).</summary>
    private const float SpriteAngle = -45f;

    /// <summary>Ringmitte relativ zu den Fuessen: Figur = 1x1 Einheit, Pivot unten mittig.</summary>
    private static readonly Vector2 RingCenter = new Vector2(0f, 0.5f);

    /// <summary>
    /// Winkel (Grad) von Kunai <paramref name="i"/> auf einem Ring aus
    /// <paramref name="count"/> Kunai: gleichmaessig verteilt und immer
    /// links/rechts gespiegelt. Bei gerader Anzahl beginnt der Ring rechts
    /// (2 = rechts + links), bei ungerader oben ueber dem Kopf (3 = Dreieck mit
    /// Spitze oben). Mit jedem neuen Kunai ruecken alle nach.
    /// Auch von <see cref="BladeStormEvo"/> genutzt.
    /// </summary>
    public static float RingAngle(int i, int count)
    {
        count = Mathf.Max(1, count);
        float start = count % 2 == 0 ? 0f : 90f;
        return start + i * 360f / count;
    }

    /// <summary>
    /// Position auf dem Ring relativ zu den Fuessen des Spielers. Der Radius
    /// waechst mit, sobald die Kunai bei <paramref name="baseRadius"/> enger als
    /// <paramref name="minSpacing"/> stuenden - so passen beliebig viele drauf.
    /// Auch von <see cref="BladeStormEvo"/> genutzt.
    ///
    /// Das Ergebnis liegt immer auf ganzen Pixeln (1/32 Einheit): die Kamera
    /// rundet jedes Sprite einzeln aufs Pixelraster. Mit einem krummen Abstand
    /// (z.B. 0.45 = 14.4 px) wechselt der gerundete Abstand zwischen Spieler
    /// und Klinge beim Laufen staendig zwischen 14 und 15 px - die Klingen zittern.
    /// </summary>
    public static Vector2 RingOffset(float angleDeg, int count, float baseRadius, float minSpacing)
    {
        float radius = Mathf.Max(baseRadius, count * minSpacing / (2f * Mathf.PI));
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 o = RingCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        return new Vector2(Mathf.Round(o.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(o.y * PixelsPerUnit) / PixelsPerUnit);
    }

    private const float PixelsPerUnit = 32f;   // m_AssetsPPU der PixelPerfectCamera

    private void UpdateTarget()
    {
        enemiesInRange.RemoveAll(e => e == null);

        if (enemiesInRange.Count == 0)
        {
            currentTarget = null;
            target = Vector2.zero;
            return;
        }

        int randomIndex = Random.Range(0, enemiesInRange.Count);
        currentTarget = enemiesInRange[randomIndex];
        target = currentTarget.transform.position; // initial setzen
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
    private IEnumerator FireAllBladesAtTarget(Vector2 target)
    {
        activeBlades.RemoveAll(b => b == null);
        var bladesToFire = new List<GameObject>(activeBlades);

        // Skilltree "Schattenschwarm": jeder Kunai bekommt sein eigenes Ziel.
        bool swarm = Skills.HasGrant(SkillGrants.Schattenschwarm);
        if (swarm) PickTargets(enemiesInRange, bladesToFire.Count, swarmTargets);

        for (int i = 0; i < bladesToFire.Count; i++)
        {
            GameObject blade = bladesToFire[i];
            if (blade == null) continue;
            blade.transform.SetParent(null);

            BladeSwarmPrefab kunai = swarm ? blade.GetComponent<BladeSwarmPrefab>() : null;
            if (kunai != null)
                kunai.Swoop(i < swarmTargets.Count ? swarmTargets[i] : null, enemiesInRange);
            else
                StartCoroutine(MoveAndDestroy(blade, target));

            yield return new WaitForSeconds(0.13f); // Abstand zwischen den Blades
        }
        activeBlades.Clear();
        shooting = false;
    }

    private readonly List<Enemy> swarmTargets = new List<Enemy>();

    /// <summary>
    /// Verteilt <paramref name="count"/> Kunai auf die Gegner in Reichweite:
    /// erst jeder Gegner einmal (zufaellige Reihenfolge), sind es mehr Kunai
    /// als Gegner, geht es von vorn los. Auch von <see cref="BladeStormEvo"/> genutzt.
    /// </summary>
    public static void PickTargets(List<Enemy> pool, int count, List<Enemy> result)
    {
        result.Clear();
        pool.RemoveAll(e => e == null);
        if (pool.Count == 0 || count <= 0) return;

        var shuffled = new List<Enemy>(pool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        for (int i = 0; i < count; i++) result.Add(shuffled[i % shuffled.Count]);
    }
    IEnumerator MoveAndDestroy(GameObject blade, Vector2 Target)
    {
        float moveSpeed = 20f;
        float lifetime = 2f;             // maximale Lebenszeit in Sekunden
        float elapsed = 0f;

        Vector2 shooterPos = transform.position;
        Vector2 fallbackDirection = (Target - shooterPos).normalized;

        while (blade != null && elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            Vector2 currentEnemyPos;

            if (currentTarget != null) // Gegner noch da?
            {
                currentEnemyPos = currentTarget.transform.position;
            }
            else
            {
                // Gegner weg → flieg weiter in der ursprünglichen Richtung
                currentEnemyPos = (Vector2)blade.transform.position + fallbackDirection * 10f;
            }

            Vector2 direction = (currentEnemyPos - (Vector2)blade.transform.position).normalized;
            Vector2 targetPos = currentEnemyPos;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            blade.transform.rotation = Quaternion.Euler(0f, 0f, angle + -45f);

            blade.transform.position = Vector2.MoveTowards(
                blade.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime);

            // wenn nahe genug am Ziel beenden
            if (Vector2.Distance(blade.transform.position, targetPos) < 0.1f)
            {
                break;
            }

            yield return null;
        }

        if (blade != null)
        {
            Destroy(blade);
        }
    }

}
