using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Feuerwand in Phase III des Endkampfs (<see cref="VerkohlterHerzFinale"/>):
/// Flammensaeulen entlang einer Ellipse um das Herz, die sich vom Kammerrand
/// nach innen schieben - die Kammer wird kleiner, der Spieler muss naeher ans
/// rasende Herz. Wer ausserhalb steht, brennt und wird nach innen gedrueckt.
///
/// Erst laeuft eine Warnung (Glutpunkte auf der Zielellipse), dann rueckt die
/// Wand an. Bild: herz_feuerwand (8 Bilder 24x40, Pivot unten).
/// </summary>
public class HerzFeuerwand : MonoBehaviour
{
    private const int Flames = 64;
    private const float Fps = 12f;
    private const float WarnTime = 1.6f;
    private const float CloseTime = 3.2f;
    private const float Tick = 0.5f;
    private const float Push = 3.2f;

    private VerkohlterHerzkammer chamber;
    private Sprite[] frames;
    private readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> warn = new List<SpriteRenderer>();
    private float targetScale, scale = 1.05f, age, tick, damage;
    private bool closing, fading;
    private float fade = 1f;

    /// <summary>Aktuelle Groesse der Wand als Anteil der Bodenellipse.</summary>
    public float Scale => scale;

    public static HerzFeuerwand Create(VerkohlterHerzkammer chamber, float target, float tickDamage)
    {
        var go = new GameObject("Herz_Feuerwand");
        RunScene.Place(go, "Effekte");
        var w = go.AddComponent<HerzFeuerwand>();
        w.chamber = chamber;
        w.targetScale = target;
        w.damage = tickDamage;
        w.frames = VerkohlterArt.Strip("herz_feuerwand");
        int objects = SortingLayer.NameToID("Objects");
        for (int i = 0; i < Flames; i++)
        {
            var f = new GameObject("Flamme");
            f.transform.SetParent(go.transform, false);
            var sr = f.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = objects;
            sr.sortingOrder = 4;
            sr.enabled = false;
            w.flames.Add(sr);
        }
        for (int i = 0; i < 96; i++)
        {
            var p = new GameObject("Warnung");
            p.transform.SetParent(go.transform, false);
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = VerkohlterArt.Pixel;
            sr.sortingLayerID = objects;
            sr.sortingOrder = -3;
            sr.transform.localScale = new Vector3(2f, 2f, 1f);
            w.warn.Add(sr);
        }
        return w;
    }

    /// <summary>Wand verlischt (Herz tot).</summary>
    public void Extinguish() => fading = true;

    private Vector2 Point(int i, int n, float s, out float y01)
    {
        float a = i / (float)n * Mathf.PI * 2f;
        Vector2 r = VerkohlterArt.FloorRadius * s;
        y01 = Mathf.Sin(a);
        return chamber.FloorMid + new Vector2(Mathf.Cos(a) * r.x, Mathf.Sin(a) * r.y);
    }

    private void Update()
    {
        if (chamber == null)
        {
            Destroy(gameObject);
            return;
        }
        age += Time.deltaTime;

        if (fading)
        {
            fade -= Time.deltaTime / 1.2f;
            if (fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        // Warnung: Glutpunkte auf der Zielellipse, blinken immer schneller
        if (age < WarnTime + CloseTime)
        {
            bool on = age < WarnTime ? Mathf.Repeat(age * (3f + age * 4f), 1f) < 0.6f : true;
            for (int i = 0; i < warn.Count; i++)
            {
                warn[i].enabled = on && !fading;
                Vector2 p = Point(i, warn.Count, targetScale, out _);
                warn[i].transform.position = VerkohlterArt.Snap(p);
                warn[i].color = VerkohlterArt.Fire[i % 2 == 0 ? 3 : 2];
            }
        }
        else
        {
            foreach (SpriteRenderer w in warn) w.enabled = false;
        }

        if (age >= WarnTime)
        {
            if (!closing)
            {
                closing = true;
                VerkohlterSounds.Erupt();
                ScreenShake.Kick(3f, 0.4f);
            }
            float q = Mathf.Clamp01((age - WarnTime) / CloseTime);
            float e = 1f - (1f - q) * (1f - q);
            scale = Mathf.Lerp(1.05f, targetScale, e);
            for (int i = 0; i < flames.Count; i++)
            {
                SpriteRenderer sr = flames[i];
                sr.enabled = true;
                Vector2 p = Point(i, flames.Count, scale, out float y01);
                sr.transform.position = VerkohlterArt.Snap(p);
                sr.sprite = VerkohlterArt.Frame(frames, (Mathf.FloorToInt(age * Fps) + i * 3) % Mathf.Max(1, frames.Length));
                // hinten (oben im Bild) hinter dem Spieler, vorne davor
                sr.sortingOrder = y01 > 0f ? -1 : 6;
                Color c = Color.white;
                c.a = Mathf.Clamp01(Mathf.Min(fade, q * 4f));
                sr.color = c;
            }
            if (!fading) Hurt();
        }
    }

    private void Hurt()
    {
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeSelf || pc.Captured) return;
        Vector2 p = pc.transform.position;
        Vector2 f = p - chamber.FloorMid;
        Vector2 r = VerkohlterArt.FloorRadius * scale;
        float k = Mathf.Sqrt((f.x * f.x) / (r.x * r.x) + (f.y * f.y) / (r.y * r.y));
        tick -= Time.deltaTime;
        if (k < 0.97f) return;
        if (tick <= 0f)
        {
            tick = Tick;
            pc.TakeDamage(damage);
        }
    }

    private void FixedUpdate()
    {
        if (!closing || fading || chamber == null) return;
        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeSelf || pc.Captured) return;
        Vector2 p = pc.transform.position;
        Vector2 f = p - chamber.FloorMid;
        Vector2 r = VerkohlterArt.FloorRadius * scale;
        float k = Mathf.Sqrt((f.x * f.x) / (r.x * r.x) + (f.y * f.y) / (r.y * r.y));
        if (k < 0.93f) return;
        // nach innen druecken - je weiter draussen, desto staerker
        pc.AddDrift(-f.normalized * Push * Mathf.Clamp01((k - 0.93f) * 6f + 0.4f));
    }
}
