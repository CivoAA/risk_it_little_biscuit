using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Klebrige Traubengelee-Pfuetze des <see cref="RoyalSplat"/> (ab Stufe 4):
/// liegt unter den Gegnern, glaenzt vor sich hin und bremst alle, die darin
/// stehen, auf <see cref="RoyalSplat.PuddleSlow"/>. Macht keinen Schaden -
/// sie haelt die Gruppe nur fuer den naechsten Klops zusammen.
/// </summary>
public class SplatPuddle : MonoBehaviour
{
    private const float Fps = 6f;
    private const float FadeIn = 0.1f;
    private const float FadeOut = 0.4f;
    private const float TickTime = 0.15f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float radius, life, age, nextTick;

    public static void Create(Vector2 at, float radius, float seconds)
    {
        Sprite[] frames = RoyalSplat.PuddleFor(radius);
        if (frames == null || frames.Length == 0) return;

        GameObject go = new GameObject("RoyalSplatPuddle");
        go.transform.position = new Vector3(Mathf.Round(at.x * 32f) / 32f, Mathf.Round(at.y * 32f) / 32f, 0f);

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SplatPuddle puddle = go.AddComponent<SplatPuddle>();
        puddle.frames = frames;
        puddle.radius = radius;
        puddle.life = Mathf.Max(0.5f, seconds);
        puddle.sr = go.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(puddle.sr, -2);
        puddle.sr.sprite = frames[0];
        puddle.sr.color = new Color(1f, 1f, 1f, 0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[Mathf.FloorToInt(age * Fps) % frames.Length];
        float a = Mathf.Min(Mathf.Clamp01(age / FadeIn), Mathf.Clamp01((life - age) / FadeOut));
        sr.color = new Color(1f, 1f, 1f, a);

        if (age < nextTick) return;
        nextTick = age + TickTime;

        // Die Pfuetze ist flach gemalt (Hoehe = Breite / 1.25, Tools/koenigsplumps.py) -
        // gebremst wird in genau dieser Ellipse, gemessen an den Fuessen.
        Vector2 at = transform.position;
        float reach = radius + 0.1f;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;
            Vector2 d = (Vector2)enemy.transform.position - at;
            d.y *= 1.25f;
            if (d.sqrMagnitude <= reach * reach)
                enemy.ApplySlow(RoyalSplat.PuddleSlow, TickTime * 2f);
        }
    }
}
