using UnityEngine;

/// <summary>
/// Blaues Fuchsfeuer auf einem Gegner (<see cref="FoxFire"/>): tickt
/// regelmaessig Schaden und zeigt ein Flaemmchen ueber dem Kopf. Stirbt der
/// Gegner, solange er brennt, springt das Feuer weiter (Lauffeuer,
/// <see cref="FoxFire.Leap"/>) - erkannt ueber <see cref="Enemy.Damaged"/>,
/// das vor dem Tod feuert, wenn das Leben schon auf 0 steht.
/// </summary>
public class FoxBurn : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float MarkFps = 10f;

    private Enemy enemy;
    private SpriteRenderer enemySprite;
    private SpriteRenderer mark;
    private FoxFire.Hit hit;
    private float until;
    private float tickTimer;
    private bool spent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Enemy.Damaged -= OnEnemyDamaged;
        Enemy.Damaged += OnEnemyDamaged;
    }

    public static bool IsBurning(Enemy enemy)
    {
        if (enemy == null) return false;
        FoxBurn burn = enemy.GetComponent<FoxBurn>();
        return burn != null && burn.enabled && Time.time < burn.until;
    }

    /// <summary>Zuenden oder auffrischen; staerkere Werte gewinnen.</summary>
    public static void Apply(Enemy enemy, FoxFire.Hit hit)
    {
        if (enemy == null || hit.burnTime <= 0f) return;

        FoxBurn burn = enemy.GetComponent<FoxBurn>();
        if (burn == null)
        {
            burn = enemy.gameObject.AddComponent<FoxBurn>();
            burn.enemy = enemy;
            burn.tickTimer = TickInterval;
            burn.hit = hit;
        }
        else
        {
            burn.hit.tickDamage = Mathf.Max(burn.hit.tickDamage, hit.tickDamage);
            burn.hit.leaps = Mathf.Max(burn.hit.leaps, hit.leaps);
            burn.hit.damage = Mathf.Max(burn.hit.damage, hit.damage);
            burn.hit.radius = Mathf.Max(burn.hit.radius, hit.radius);
            burn.hit.burnTime = Mathf.Max(burn.hit.burnTime, hit.burnTime);
        }
        burn.until = Mathf.Max(burn.until, Time.time + hit.burnTime);
        burn.enabled = true;
        burn.ShowMark(true);
    }

    private static void OnEnemyDamaged(Enemy enemy, float damage)
    {
        if (enemy == null || enemy.HealthFraction > 0f) return;

        FoxBurn burn = enemy.GetComponent<FoxBurn>();
        if (burn == null || burn.spent || Time.time >= burn.until || burn.hit.leaps <= 0) return;

        burn.spent = true;
        FoxFire.Leap(enemy, burn.hit);
    }

    void Update()
    {
        if (enemy == null)
        {
            Destroy(this);
            return;
        }

        if (Time.time >= until)
        {
            ShowMark(false);
            enabled = false;
            return;
        }

        DrawMark();

        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer += TickInterval;

        // kein Rueckstoss - brennen soll den Gegner nicht wegschieben
        enemy.TakeDamage(hit.tickDamage, null, 0f);
    }

    void OnDestroy()
    {
        if (mark != null) Destroy(mark.gameObject);
    }

    private void ShowMark(bool on)
    {
        if (mark == null)
        {
            if (!on) return;
            enemySprite = enemy.GetComponentInChildren<SpriteRenderer>();
            GameObject go = new GameObject("FoxBurnMark");
            go.transform.SetParent(enemy.transform, false);
            mark = go.AddComponent<SpriteRenderer>();
            if (enemySprite != null)
            {
                mark.sortingLayerID = enemySprite.sortingLayerID;
                mark.sortingOrder = enemySprite.sortingOrder + 2;
            }
        }
        mark.enabled = on;
        if (on) DrawMark();
    }

    private void DrawMark()
    {
        if (mark == null) return;

        Sprite[] frames = FoxFire.MarkFrames;
        mark.sprite = frames[(int)(Time.time * MarkFps) % frames.Length];

        // oben auf den Kopf, auf ganze Pixel; die Groesse des Gegners
        // (Skalierung am Objekt) soll das Flaemmchen nicht aufblasen
        float top = enemySprite != null ? enemySprite.bounds.max.y : enemy.transform.position.y + 0.6f;
        float x = enemySprite != null ? enemySprite.bounds.center.x : enemy.transform.position.x;
        const float ppu = 32f;
        mark.transform.position = new Vector3(Mathf.Round(x * ppu) / ppu, Mathf.Round((top + 0.05f) * ppu) / ppu, 0f);

        Vector3 parent = enemy.transform.lossyScale;
        mark.transform.localScale = new Vector3(
            1f / Mathf.Max(0.01f, Mathf.Abs(parent.x)),
            1f / Mathf.Max(0.01f, Mathf.Abs(parent.y)), 1f);
    }
}
