using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Marmeladenlache. Tickt alle AttackSpeed Sekunden Schaden an jedem
/// Gegner darin und verschwindet nach duration Sekunden. Gemeinsam fuer Jam Jar
/// und die Evo (<see cref="StickyShatterEvoPrefab"/>, die nur noch bremst).
///
/// Skilltree:
///   Einkochen     - je Tick in derselben Lache mehr Schaden, bis <see cref="SimmerMaxFactor"/>.
///   Marmeladenbad - <see cref="JamBath"/> fragt ueber <see cref="IsInAnyPuddle"/>, ob der Spieler drinsteht.
/// </summary>
public class AreaWeaponPrefabJamJar : MonoBehaviour
{
    public AreaWeaponJamJar weapon;
    public List<Enemy> enemiesInRange = new List<Enemy>();

    // ------------------------------------------------------------ Einkochen

    /// <summary>So viel mehr Schaden je Tick, den ein Gegner schon in derselben Lache steht.</summary>
    public const float SimmerStep = 0.15f;

    /// <summary>Hoechstens so viel Schaden wie normal (2.5 = 250 %).</summary>
    public const float SimmerMaxFactor = 2.5f;

    /// <summary>Schadensfaktor fuer den Tick, nachdem ein Gegner schon <paramref name="ticks"/> Ticks drinsteht.</summary>
    public static float SimmerFactor(int ticks) => Mathf.Min(SimmerMaxFactor, 1f + SimmerStep * ticks);

    private readonly Dictionary<Enemy, int> simmerTicks = new Dictionary<Enemy, int>();

    // ------------------------------------------------------------ Aussehen

    /// <summary>So lange waechst eine Lache auf volle Groesse (ohne eigene Animation).</summary>
    private const float GrowTime = 0.25f;

    /// <summary>So lange schrumpft sie am Ende weg (ohne eigene Animation).</summary>
    private const float ShrinkTime = 0.25f;

    private static readonly List<AreaWeaponPrefabJamJar> active = new List<AreaWeaponPrefabJamJar>();

    /// <summary>Durchmesser der Lache in Pixeln je Einheit range x AOE (Stufen aus Tools/marmelade.py).</summary>
    public const float PixelsPerRange = 204.8f;

    private CircleCollider2D area;
    private SpriteFlipbook look;
    private PixelPool pool;
    private Vector3 fullSize;
    private float lifeTimer;
    private float age;
    private float tickCounter;

    /// <summary>Die Bremse fuer Gegner in der Lache. Null = keine.</summary>
    protected virtual float? Slow => null;

    /// <summary>Notnagel, falls die Lache ohne Waffe gespawnt wurde.</summary>
    protected virtual AreaWeaponJamJar FindWeapon() => WeaponFinder.Find<AreaWeaponJamJar>("Throwing Jam Jar");

    protected virtual void Start()
    {
        if (weapon == null) weapon = FindWeapon();

        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        area = GetComponent<CircleCollider2D>();
        look = GetComponent<SpriteFlipbook>();
        lifeTimer = weapon.CurrentDuration;
        float size = weapon.CurrentStats.range * PlayerController.Instance.AOERange;
        fullSize = Vector3.one * size;

        // Pixelgenaue Lache: Groessenstufe statt Massstab, Kollider passend dazu.
        pool = GetComponent<PixelPool>();
        if (pool != null && pool.Begin(size * PixelsPerRange))
        {
            transform.position = PixelPool.Snap(transform.position);
            if (area != null)
            {
                area.offset = Vector2.zero;
                area.radius = pool.SizeUnits * 0.5f;
            }
            pool.SetLifeLeft(lifeTimer);
        }
        else
        {
            pool = null;
            // Mit eigener Animation blendet die Lache selbst ein und aus - sonst
            // waechst und schrumpft sie.
            transform.localScale = look != null ? fullSize : Vector3.zero;
        }

        if (AudioController.Instance != null)
        {
            AudioController.Instance.PalySound(AudioController.Instance.areaWeaponSpawn, 0.5f);
        }
    }

    protected virtual void OnEnable() => active.Add(this);

    protected virtual void OnDisable() => active.Remove(this);

    protected virtual void Update()
    {
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (pool != null)
        {
            pool.SetLifeLeft(lifeTimer);
        }
        else if (look != null)
        {
            look.SetLifeLeft(lifeTimer);
        }
        else
        {
            float grow = Mathf.Clamp01(age / GrowTime);
            float shrink = Mathf.Clamp01(lifeTimer / ShrinkTime);
            transform.localScale = fullSize * Mathf.Min(grow, shrink);
        }

        tickCounter -= Time.deltaTime;
        if (tickCounter > 0f) return;

        tickCounter = Mathf.Max(0.05f, weapon.CurrentStats.AttackSpeed);
        DamageTick();
    }

    private void DamageTick()
    {
        bool simmer = Skills.HasGrant(SkillGrants.Einkochen);
        float damage = weapon.CurrentStats.damage;
        float? slow = Slow;

        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemiesInRange[i];
            if (enemy == null)
            {
                enemiesInRange.RemoveAt(i);
                continue;
            }

            float tickDamage = damage;
            if (simmer)
            {
                simmerTicks.TryGetValue(enemy, out int ticks);
                tickDamage *= SimmerFactor(ticks);
                simmerTicks[enemy] = ticks + 1;
            }

            enemy.TakeDamage(tickDamage, slow, 0f);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy != null && !enemiesInRange.Contains(enemy)) enemiesInRange.Add(enemy);
    }

    protected virtual void OnTriggerExit2D(Collider2D collider)
    {
        if (!collider.CompareTag("Enemy")) return;

        Enemy enemy = collider.GetComponent<Enemy>();
        if (enemy == null) return;

        enemiesInRange.Remove(enemy);
        // Einkochen faengt in der naechsten Lache wieder von vorn an.
        simmerTicks.Remove(enemy);
    }

    // ------------------------------------------------------------ Marmeladenbad

    /// <summary>Liegt <paramref name="point"/> in dieser Lache?</summary>
    public bool Covers(Vector2 point)
    {
        if (area == null || lifeTimer <= 0f) return false;

        Vector2 center = transform.TransformPoint(area.offset);
        Vector3 scale = transform.lossyScale;
        float radius = area.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

        return (point - center).sqrMagnitude <= radius * radius;
    }

    /// <summary>Steht <paramref name="point"/> in irgendeiner Marmeladenlache?</summary>
    public static bool IsInAnyPuddle(Vector2 point)
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null && active[i].Covers(point)) return true;
        }
        return false;
    }
}
