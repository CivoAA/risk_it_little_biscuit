using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saucenflecken auf einem Gegner (<see cref="SauceBottle"/>). Ketchup setzt
/// <see cref="Enemy.SauceFactor"/> - der Gegner nimmt von jeder Waffe mehr
/// Schaden. Senf bremst ihn. Beides zusammen gibt ab Stufe 5 noch etwas
/// obendrauf. Ab Stufe 6 platzt ein bekleckerter Gegner beim Tod und
/// bekleckert seine Nachbarn.
///
/// Zu sehen ist der Fleck als Kontur in Saucenfarbe: einfarbige Kopien des
/// Gegner-Sprites (Shader Risk/SpriteSilhouette wie bei <see cref="EliteGlow"/>),
/// einen Bildpixel versetzt hinter dem Gegner. Bei Ketchup + Senf liegt
/// Ketchup innen und Senf einen Pixel weiter aussen. Waehrenddessen ist das
/// Elite-Leuchten aus - gleiche Sortierung hinter dem Gegner wuerde sonst
/// flackern.
///
/// Haengt am Gegner und bleibt dort; die Kopien werden beim Ablaufen nur
/// ausgeblendet.
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

    private const string ShaderPath = "Shaders/SpriteSilhouette";
    /// <summary>So lange blendet die Kontur am Ende aus.</summary>
    private const float FadeTime = 0.4f;

    private static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    private static Material material;
    private static bool materialMissing;

    private Enemy enemy;
    private SpriteRenderer enemySprite;
    private EliteGlow eliteGlow;
    private SpriteRenderer[] ketchupRim;
    private SpriteRenderer[] mustardRim;

    private float ketchupUntil;
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
            mark.mustardUntil = now + recipe.duration;
            e.ApplySlow(SauceBottle.MustardSlow, recipe.duration);
        }
        else
        {
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

    // Nach dem Animator, damit die Kontur schon das aktuelle Bild zeigt.
    void LateUpdate()
    {
        if (enemy == null) return;
        DrawRims();
    }

    void OnDisable()
    {
        if (enemy != null) enemy.SauceFactor = 1f;
        HideRim(ketchupRim);
        HideRim(mustardRim);
        if (eliteGlow != null) eliteGlow.Hidden = false;
    }

    void OnDestroy()
    {
        DestroyRim(ketchupRim);
        DestroyRim(mustardRim);
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
        DrawRims();
    }

    // ------------------------------------------------------------------
    //  Kontur
    // ------------------------------------------------------------------

    private void DrawRims()
    {
        bool k = Active(false);
        bool m = Active(true);

        if (eliteGlow == null && enemySprite != null) eliteGlow = enemySprite.GetComponent<EliteGlow>();
        if (eliteGlow != null) eliteGlow.Hidden = k || m;

        DrawRim(ref ketchupRim, k, SauceBottle.Ketchup, ketchupUntil, 1);
        // Mit Ketchup zusammen liegt Senf einen Pixel weiter aussen.
        DrawRim(ref mustardRim, m, SauceBottle.Mustard, mustardUntil, k ? 2 : 1);
    }

    private void DrawRim(ref SpriteRenderer[] rim, bool on, Color color, float until, int distance)
    {
        Sprite sprite = enemySprite != null ? enemySprite.sprite : null;
        if (!on || sprite == null || !enemySprite.enabled)
        {
            HideRim(rim);
            return;
        }
        if (rim == null)
        {
            rim = BuildRim();
            if (rim == null) return;
        }

        color.a *= Mathf.Clamp01((until - Time.time) / FadeTime);
        float unit = sprite.pixelsPerUnit > 0f ? 1f / sprite.pixelsPerUnit : 1f / SweetFx.PixelsPerUnit;
        for (int i = 0; i < rim.Length; i++)
        {
            SpriteRenderer r = rim[i];
            r.enabled = true;
            r.sprite = sprite;
            r.flipX = enemySprite.flipX;
            r.flipY = enemySprite.flipY;
            r.sortingLayerID = enemySprite.sortingLayerID;
            // innen direkt hinter dem Gegner, aussen noch einen dahinter
            r.sortingOrder = enemySprite.sortingOrder - distance;
            r.color = color;
            r.transform.localPosition = new Vector3(Dirs[i].x * distance * unit, Dirs[i].y * distance * unit, 0f);
        }
    }

    private SpriteRenderer[] BuildRim()
    {
        Material mat = SharedMaterial();
        if (mat == null || enemySprite == null) return null;

        SpriteRenderer[] rim = new SpriteRenderer[Dirs.Length];
        for (int i = 0; i < rim.Length; i++)
        {
            // Kind des Sprites: folgt Skalierung und Spiegelung des Bildes
            GameObject go = new GameObject("SauceRim");
            go.transform.SetParent(enemySprite.transform, false);
            rim[i] = go.AddComponent<SpriteRenderer>();
            rim[i].sharedMaterial = mat;
        }
        return rim;
    }

    private static Material SharedMaterial()
    {
        if (material != null || materialMissing) return material;

        Shader shader = Resources.Load<Shader>(ShaderPath);
        if (shader == null)
        {
            materialMissing = true;
            Debug.LogWarning($"[SauceMark] Shader Resources/{ShaderPath} fehlt - Sauce hat keine Kontur.");
            return null;
        }
        material = new Material(shader) { name = "SauceRim" };
        return material;
    }

    private static void HideRim(SpriteRenderer[] rim)
    {
        if (rim == null) return;
        foreach (SpriteRenderer r in rim)
            if (r != null) r.enabled = false;
    }

    private static void DestroyRim(SpriteRenderer[] rim)
    {
        if (rim == null) return;
        foreach (SpriteRenderer r in rim)
            if (r != null) Destroy(r.gameObject);
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
