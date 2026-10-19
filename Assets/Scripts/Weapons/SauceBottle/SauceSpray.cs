using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein Druck auf die Flasche der <see cref="SauceBottle"/>: die Flasche
/// erscheint, quetscht sich zusammen, Tropfen fliegen im Kegel davon und
/// bleiben teils als Flecken am Boden liegen. Getroffen wird, wer beim
/// Quetschen im Kegel steht - die Tropfen sind nur das Bild dazu.
/// </summary>
public class SauceSpray : MonoBehaviour
{
    private const float AppearTime = 0.08f;
    private const float SqueezeTime = 0.16f;
    private const float RestTime = 0.22f;
    private const int Drops = 11;
    private const float DropTime = 0.22f;
    private const float SplatChance = 0.35f;
    private const float SplatLife = 1.6f;

    /// <summary>Duese ueber dem Pivot der Flasche (Pivot unten).</summary>
    private static readonly Vector2 Nozzle = new Vector2(0f, 0.62f);

    private static readonly List<Enemy> hits = new List<Enemy>();

    private Vector2 dir;
    private float range;
    private float half;
    private bool mustard;
    private float damage;
    private SauceMark.Recipe recipe;
    private float delay;
    private float age;
    private bool squirted;
    private SpriteRenderer sr;

    public static void Spawn(Vector2 at, Vector2 dir, float range, float half, bool mustard, float damage,
                             SauceMark.Recipe recipe, float delay)
    {
        GameObject go = SweetFx.NewObject(mustard ? "MustardBottle" : "KetchupBottle", at);
        SauceSpray s = go.AddComponent<SauceSpray>();
        s.dir = dir.normalized;
        s.range = range;
        s.half = half;
        s.mustard = mustard;
        s.damage = damage;
        s.recipe = recipe;
        s.delay = delay;
        s.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(s.sr, 3);
        s.sr.enabled = false;
    }

    void Update()
    {
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            return;
        }
        // Flasche bleibt am Spieler
        if (PlayerController.Instance != null)
        {
            Vector2 p = PlayerController.Instance.transform.position;
            float side = dir.x >= 0f ? 1f : -1f;
            transform.position = SweetFx.Snap(p + new Vector2(0.5f * side, 0.05f));
        }

        age += Time.deltaTime;
        Sprite[] frames = SauceBottle.Bottle(mustard);
        sr.enabled = true;
        sr.flipX = dir.x < 0f;

        if (age < AppearTime)
        {
            sr.sprite = frames[0];
            return;
        }
        if (!squirted)
        {
            squirted = true;
            Squirt();
        }
        if (age < AppearTime + SqueezeTime)
        {
            sr.sprite = frames[Mathf.Min(1, frames.Length - 1)];
            return;
        }
        sr.sprite = frames[Mathf.Min(2, frames.Length - 1)];
        float fade = Mathf.Clamp01((age - AppearTime - SqueezeTime) / RestTime);
        sr.color = new Color(1f, 1f, 1f, 1f - fade * fade);
        if (fade >= 1f) Destroy(gameObject);
    }

    private void Squirt()
    {
        Vector2 nozzle = (Vector2)transform.position + Nozzle;
        WeaponSounds.Play(WeaponSounds.SauceSquirt, 1f, mustard ? 1.12f : 1f);

        for (int i = 0; i < Drops; i++)
        {
            float a = Random.Range(-half, half);
            Vector2 d = Quaternion.Euler(0f, 0f, a) * dir;
            float dist = range * Random.Range(0.35f, 1f);
            SauceDrop.Fly(nozzle, nozzle + d * dist, DropTime * Random.Range(0.8f, 1.2f), mustard,
                          Random.value < SplatChance ? SplatLife : 0f);
        }

        // Treffer: alle im Kegel ab dem Spieler
        Vector2 origin = PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, 0.3f) : nozzle;
        SweetFx.EnemiesWithin(origin, range + 0.35f, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            Enemy e = hits[i];
            if (e == null) continue;
            Vector2 to = SweetFx.Body(e) - origin;
            // ganz nah zaehlt immer, sonst muss der Gegner im Kegel stehen
            if (to.sqrMagnitude > 0.5f * 0.5f && Vector2.Angle(dir, to) > half + 6f) continue;
            e.TakeDamage(damage, null, 0.2f);
            if (SweetFx.Valid(e)) SauceMark.Apply(e, mustard, recipe);
        }
        hits.Clear();
    }
}
