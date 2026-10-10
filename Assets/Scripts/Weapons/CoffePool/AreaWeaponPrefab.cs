using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Kaffeepool: eine Lache um den Spieler, die ihm folgt und Gegner darin
/// alle AttackSpeed Sekunden verletzt.
///
/// Groesse = range x AOE. Frueher wurde dafuer ein Bild per localScale
/// aufgezogen (Mixels) - jetzt waehlt <see cref="PixelPool"/> die passende
/// Groessenstufe aus Tools/kaffeepool.py, und der Kollider wird auf genau
/// diese Groesse gesetzt.
/// </summary>
public class AreaWeaponPrefab : MonoBehaviour
{
    /// <summary>Breite der Lache in Pixeln je Einheit range x AOE.</summary>
    public const float PixelsPerRange = 96f;

    /// <summary>Alle so viele Sekunden steigt ein Dampfwoelkchen auf.</summary>
    private const float SteamEvery = 0.22f;

    public AreaWeapon weapon;
    private Vector3 targetSize;
    private float timer;
    public List<Enemy> enemiesInRange;
    private float counter;

    private PixelPool pool;
    private float steamTimer;
    private static Sprite[] steamFrames;

    void Start()
    {
        weapon = WeaponFinder.Find<AreaWeapon>("Coffe Pool");
        if (weapon == null || !weapon.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        float size = weapon.CurrentStats.range * PlayerController.Instance.AOERange;
        timer = weapon.CurrentDuration;

        pool = GetComponent<PixelPool>();
        if (pool != null && pool.Begin(size * PixelsPerRange))
        {
            FitCollider();
            pool.SetLifeLeft(timer);
        }
        else
        {
            // Rueckfall ohne Bilder: altes Wachsen per Massstab
            pool = null;
            targetSize = Vector3.one * size;
            transform.localScale = Vector3.zero;
        }

        AudioController.Instance.PalySound(AudioController.Instance.areaWeaponSpawn);
    }

    /// <summary>Kapsel genau so gross wie die gewaehlte Lache (ohne Umriss).</summary>
    private void FitCollider()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Vector2 spriteSize = sr.sprite.rect.size / PixelPool.Ppu;
        Vector2 inner = spriteSize - Vector2.one * (4f / PixelPool.Ppu);

        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.offset = Vector2.zero;
            capsule.size = inner;
        }
    }

    void Update()
    {
        if (weapon == null || !weapon.IsActive) return;

        timer -= Time.deltaTime;
        if (pool != null)
        {
            pool.SetLifeLeft(timer);
            if (timer <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Steam();
        }
        else
        {
            //grow and shrink towards targetSize
            transform.localScale = Vector3.MoveTowards(transform.localScale, targetSize, Time.deltaTime * 17);
            //shrink and only then destory
            if (timer <= 0)
            {
                targetSize = Vector3.zero;
                if (transform.localScale.x == 0f)
                {
                    Destroy(gameObject);
                }
            }
        }

        // periodic damage
        counter -= Time.deltaTime;
        if (counter <= 0)
        {
            counter = weapon.CurrentStats.AttackSpeed;
            for (int i = enemiesInRange.Count - 1; i >= 0; i--)
            {
                // zerstörte Gegner aus der Liste entfernen statt Exception
                if (enemiesInRange[i] == null)
                {
                    enemiesInRange.RemoveAt(i);
                    continue;
                }
                enemiesInRange[i].TakeDamage(weapon.CurrentStats.damage, knockback: 0f);
            }
        }
    }

    /// <summary>Heisser Kaffee dampft: kleine Woelkchen an zufaelligen Stellen der Lache.</summary>
    private void Steam()
    {
        if (timer < pool.VanishTime) return;

        steamTimer -= Time.deltaTime;
        if (steamTimer > 0f) return;
        steamTimer = SteamEvery * Random.Range(0.7f, 1.3f);

        if (steamFrames == null || steamFrames.Length == 0 || steamFrames[0] == null)
        {
            steamFrames = SpriteStrip.Load("Weapons/coffee_steam");
        }

        // Punkt in der inneren Ellipse
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector2 half = sr.sprite.rect.size / PixelPool.Ppu * 0.5f * 0.75f;
        Vector2 p = Random.insideUnitCircle;
        Vector2 at = (Vector2)transform.position + new Vector2(p.x * half.x, p.y * half.y);
        FoxFx.Play(steamFrames, at, 10f, 1);
    }

    private  void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Add(collider.GetComponent<Enemy>());
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            enemiesInRange.Remove(collider.GetComponent<Enemy>());
        }
    }
}
