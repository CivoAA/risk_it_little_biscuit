using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saucenflecken auf einem Gegner (<see cref="SauceBottle"/>). Ketchup setzt
/// <see cref="Enemy.SauceFactor"/> - der Gegner nimmt von jeder Waffe mehr
/// Schaden. Senf bremst ihn. Beides zusammen gibt ab Stufe 5 noch etwas
/// obendrauf. Ab Stufe 6 platzt ein bekleckerter Gegner beim Tod und
/// bekleckert seine Nachbarn.
///
/// Haengt am Gegner und bleibt dort; die Flecken sind Kind-Renderer, die beim
/// Ablaufen nur ausgeblendet werden.
/// </summary>
public class SauceMark : MonoBehaviour
{
    /// <summary>Was ein Fleck kann - kommt mit jedem Spritzer neu.</summary>
    public struct Recipe
    {
        public float duration;
        public float bonus;
        public float combo;
        public float explodeDamage;
    }

    private const float DripFps = 2.5f;

    private Enemy enemy;
    private SpriteRenderer enemySprite;
    private SpriteRenderer ketchupMark;
    private SpriteRenderer mustardMark;

    private float ketchupSince;
    private float ketchupUntil;
    private float mustardSince;
    private float mustardUntil;
    private Recipe best;
    private bool exploded;

    public static void Apply(Enemy e, bool mustard, Recipe recipe)
    {
        if (e == null) return;
        Listen();

        SauceMark mark = e.GetComponent<SauceMark>();
        if (mark == null)
        {
            mark = e.gameObject.AddComponent<SauceMark>();
            mark.enemy = e;
            mark.enemySprite = e.GetComponentInChildren<SpriteRenderer>();
        }
        mark.enabled = true;

        // Der staerkste Fleck gilt - eine niedrigere Kopie macht nichts schlechter.
        mark.best.duration = recipe.duration;
        mark.best.bonus = Mathf.Max(mark.Active(false) ? mark.best.bonus : 0f, recipe.bonus);
        mark.best.combo = Mathf.Max(mark.Active(false) || mark.Active(true) ? mark.best.combo : 0f, recipe.combo);
        mark.best.explodeDamage = Mathf.Max(mark.best.explodeDamage, recipe.explodeDamage);

        float now = Time.time;
        if (mustard)
        {
            if (!mark.Active(true)) mark.mustardSince = now;
            mark.mustardUntil = now + recipe.duration;
            e.ApplySlow(SauceBottle.MustardSlow, recipe.duration);
        }
        else
        {
            if (!mark.Active(false)) mark.ketchupSince = now;
            mark.ketchupUntil = now + recipe.duration;
        }
        mark.Refresh();
    }

    private bool Active(bool mustard)
    {
        return Time.time < (mustard ? mustardUntil : ketchupUntil);
    }

    void Update()
    {
        if (enemy == null)
        {
            Destroy(this);
            return;
        }
        Refresh();
        if (!Active(false) && !Active(true)) enabled = false;
    }

    void OnDisable()
    {
        if (enemy != null) enemy.SauceFactor = 1f;
        if (ketchupMark != null) ketchupMark.enabled = false;
        if (mustardMark != null) mustardMark.enabled = false;
    }

    void OnDestroy()
    {
        if (ketchupMark != null) Destroy(ketchupMark.gameObject);
        if (mustardMark != null) Destroy(mustardMark.gameObject);
    }

    private static bool listening;

    private static void Listen()
    {
        if (listening) return;
        listening = true;
        Enemy.Damaged += OnEnemyDamaged;
    }

    /// <summary>Todestreffer auf einen Bekleckerten: Saucen-Explosion (vor dem Destroy).</summary>
    private static void OnEnemyDamaged(Enemy e, float raw)
    {
        if (e == null || e.HealthFraction > 0f) return;
        SauceMark mark = e.GetComponent<SauceMark>();
        if (mark == null || mark.exploded || mark.best.explodeDamage <= 0f) return;
        if (!mark.Active(false) && !mark.Active(true)) return;
        mark.exploded = true;
        mark.Explode();
    }

    private void Refresh()
    {
        bool k = Active(false);
        bool m = Active(true);
        float factor = 1f;
        if (k) factor += best.bonus;
        if (k && m) factor += best.combo;
        enemy.SauceFactor = factor;

        DrawMark(ref ketchupMark, false, k, ketchupSince, m ? -0.12f : 0f);
        DrawMark(ref mustardMark, true, m, mustardSince, k ? 0.12f : 0f);
    }

    private void DrawMark(ref SpriteRenderer mark, bool mustard, bool on, float since, float dx)
    {
        if (!on)
        {
            if (mark != null) mark.enabled = false;
            return;
        }
        if (mark == null)
        {
            GameObject go = new GameObject(mustard ? "MustardMark" : "KetchupMark");
            go.transform.SetParent(enemy.transform, false);
            mark = go.AddComponent<SpriteRenderer>();
            if (enemySprite != null)
            {
                mark.sortingLayerID = enemySprite.sortingLayerID;
                mark.sortingOrder = enemySprite.sortingOrder + (mustard ? 3 : 2);
            }
        }
        mark.enabled = true;
        Sprite[] frames = SauceBottle.Mark(mustard);
        mark.sprite = frames[Mathf.Min(frames.Length - 1, (int)((Time.time - since) * DripFps))];

        Vector2 c = enemySprite != null ? (Vector2)enemySprite.bounds.center : SweetFx.Body(enemy);
        mark.transform.position = SweetFx.Snap(c + new Vector2(dx, 0.04f));
        // gegen die Skalierung des Gegners, damit die Pixel 1:1 bleiben
        Vector3 parent = enemy.transform.lossyScale;
        mark.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(parent.x)),
                                                1f / Mathf.Max(0.01f, Mathf.Abs(parent.y)), 1f);
    }

    private void Explode()
    {
        Vector2 at = SweetFx.Body(enemy);
        bool mustard = Active(true) && !Active(false);
        WeaponSounds.Play(WeaponSounds.SauceSplat);
        for (int i = 0; i < 8; i++)
        {
            Vector2 d = Quaternion.Euler(0f, 0f, i * 45f + Random.Range(-15f, 15f)) * Vector2.right;
            bool m = Active(true) && (i % 2 == 1 || mustard);
            SauceDrop.Fly(at, at + d * SauceBottle.ExplodeRadius * Random.Range(0.6f, 1f), 0.2f, m, i % 3 == 0 ? 1.2f : 0f);
        }

        float radius = SauceBottle.ExplodeRadius * SweetFx.Aoe;
        // eigene Liste: die Explosion kann weitere Explosionen ausloesen
        List<Enemy> near = new List<Enemy>();
        SweetFx.EnemiesWithin(at, radius, near);
        Recipe r = best;
        for (int i = 0; i < near.Count; i++)
        {
            Enemy o = near[i];
            if (o == null || o == enemy) continue;
            o.TakeDamage(r.explodeDamage, null, 0.4f);
            if (!SweetFx.Valid(o)) continue;
            if (Active(false)) Apply(o, false, r);
            if (Active(true)) Apply(o, true, r);
        }
    }
}
