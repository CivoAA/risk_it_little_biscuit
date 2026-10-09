using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ein Klops des <see cref="RoyalSplat"/>: fliegt im Bogen zu einem festen
/// Landepunkt (der Ring am Boden zeigt ihn vorher an), klatscht auf und
/// springt zur naechsten Gruppe weiter, bis die Abpraller verbraucht sind.
/// Zum Schluss bleibt je nach Stufe eine Pfuetze, oder er zerplatzt in
/// Mini-Klopse.
///
/// Damit es sich sauber anfuehlt:
///  - Landepunkt steht beim Abflug fest und wird angezeigt - kein Nachsteuern,
///    kein Zufall: wo der Ring ist, schlaegt er ein.
///  - Bodenposition laeuft linear, die Hoehe als Parabel; der Schatten bleibt
///    am Boden und schrumpft mit der Hoehe.
///  - Squash & Stretch aus festen Bildern (gestreckt beim Steigen/Fallen,
///    rund am Scheitel, gequetscht beim Aufschlag), alles auf ganzen Pixeln.
///
/// Lebt in der Lauf-Szene, nicht am Spieler. Skaliert wird nie.
/// </summary>
public class SplatBlob : MonoBehaviour
{
    private enum State { Fly, Land, Done }

    private const float PixelsPerUnit = 32f;

    /// <summary>So lange bleibt er gequetscht am Boden liegen, bevor er weiterspringt.</summary>
    private const float SquashTime = 0.07f;

    /// <summary>Letzter Aufschlag: gequetscht, dann wabbeln, dann weg.</summary>
    private const float EndTime = 0.16f;

    private SpriteRenderer body;
    private SpriteRenderer shadow;
    private SpriteRenderer mark;
    private Transform bodyT, shadowT, markT;

    private State state;
    private Vector2 from, to;
    private float startHeight, arcHeight, flightTime, t, landTimer;
    private RoyalSplat.Hit hit;
    private bool mini;
    private int bouncesLeft;
    private readonly HashSet<Enemy> seen = new HashSet<Enemy>();
    private Sprite[] frames;
    private Sprite[] markFrames;

    // ------------------------------------------------------------------

    /// <summary>Wurf vom Spieler: startet in Zepterhoehe ueber <paramref name="ground"/>.</summary>
    public static void Throw(Vector2 ground, float height, Vector2 spot, RoyalSplat.Hit hit)
    {
        SplatBlob blob = Spawn("RoyalSplat", ground, false);
        blob.hit = hit;
        blob.bouncesLeft = hit.bounces;
        float dist = (spot - ground).magnitude;
        blob.Begin(ground, height, spot, Mathf.Min(0.6f, 0.36f + 0.03f * dist), 1.1f + 0.16f * dist);
    }

    private static void ThrowMini(Vector2 ground, Vector2 spot, RoyalSplat.Hit hit)
    {
        SplatBlob blob = Spawn("RoyalSplatMini", ground, true);
        blob.hit = hit;
        blob.Begin(ground, 0.15f, spot, 0.3f, 0.7f);
    }

    private static SplatBlob Spawn(string name, Vector2 at, bool mini)
    {
        GameObject go = new GameObject(name);
        go.transform.position = at;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        SplatBlob blob = go.AddComponent<SplatBlob>();
        blob.mini = mini;
        blob.frames = mini ? RoyalSplat.MiniFrames : RoyalSplat.BlobFrames;
        return blob;
    }

    void Awake()
    {
        // Wurzel bleibt am Weltursprung, die Teile setzen ihre Position selbst
        // (je auf ganze Pixel) - Schatten und Ring liegen am Boden, der Klops darueber.
        shadow = Child("Shadow", -1, out shadowT);
        mark = Child("Mark", -1, out markT);
        body = Child("Body", 3, out bodyT);
    }

    private SpriteRenderer Child(string name, int order, out Transform tr)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        tr = g.transform;
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(sr, order);
        return sr;
    }

    private void Begin(Vector2 start, float height, Vector2 spot, float time, float arc)
    {
        transform.position = Vector3.zero;
        from = start;
        to = spot;
        startHeight = height;
        flightTime = Mathf.Max(0.12f, time);
        arcHeight = arc;
        t = 0f;
        state = State.Fly;

        markFrames = mini ? null : RoyalSplat.MarkFor(hit.radius);
        mark.enabled = markFrames != null && markFrames.Length > 0;
        markT.position = Snap(spot);
        Render();
    }

    void Update()
    {
        switch (state)
        {
            case State.Fly:
                t += Time.deltaTime / flightTime;
                if (t >= 1f)
                {
                    t = 1f;
                    Touchdown();
                }
                Render();
                break;

            case State.Land:
                landTimer += Time.deltaTime;
                bool last = bouncesLeft < 0;
                if (!last && landTimer >= SquashTime)
                {
                    Bounce();
                    break;
                }
                if (last)
                {
                    body.sprite = frames[landTimer < EndTime * 0.5f ? 2 : Mathf.Min(3, frames.Length - 1)];
                    // die letzten Bilder blasst er aus - die Pfuetze oder die Minis uebernehmen
                    float fade = Mathf.Clamp01(1f - (landTimer - EndTime * 0.5f) / (EndTime * 0.5f));
                    body.color = new Color(1f, 1f, 1f, fade);
                    shadow.color = new Color(1f, 1f, 1f, fade);
                    if (landTimer >= EndTime)
                    {
                        state = State.Done;
                        Destroy(gameObject);
                    }
                }
                break;
        }
    }

    // ------------------------------------------------------------------

    private void Touchdown()
    {
        state = State.Land;
        landTimer = 0f;
        mark.enabled = false;

        if (mini)
        {
            RoyalSplat.Splash(to, hit.damage, hit.radius, null, 0f);
            bouncesLeft = -1;
            return;
        }

        bool final = bouncesLeft <= 0;
        // gewackelt wird nur beim letzten, grossen Aufschlag - bei jedem Abpraller waere es Dauerzittern
        RoyalSplat.Splash(to, hit.damage, hit.radius, seen, final ? 1.5f : 0f);

        if (!final)
        {
            Vector2 next;
            if (RoyalSplat.FindCluster(to, RoyalSplat.BounceRange, hit.radius, seen, null, hit.radius * 1.2f, out next))
            {
                pendingBounce = next;
                bouncesLeft--;
                return;
            }
        }

        // letzter Aufschlag (oder keiner mehr da): Pfuetze / Minis
        bouncesLeft = -1;
        if (hit.puddleTime > 0f) SplatPuddle.Create(to, hit.radius * 0.95f, hit.puddleTime);
        if (hit.split) Split();
    }

    private Vector2 pendingBounce;

    private void Bounce()
    {
        float dist = (pendingBounce - to).magnitude;
        Begin(to, 0f, pendingBounce, 0.3f + 0.025f * dist, 0.7f + 0.13f * dist);
    }

    private void Split()
    {
        RoyalSplat.Hit small = hit;
        small.damage = hit.damage * RoyalSplat.MiniFactor;
        small.radius = Mathf.Max(0.75f, hit.radius * 0.6f);
        small.bounces = 0;
        small.puddleTime = 0f;
        small.split = false;

        // Richtung der ersten Mini: zum naechsten Gegner, die anderen im 120-Grad-Abstand
        float baseAngle = Random.value * 360f;
        Vector2 aim;
        if (RoyalSplat.FindCluster(to, 2.5f, small.radius, seen, null, 0.8f, out aim) && (aim - to).sqrMagnitude > 0.04f)
            baseAngle = Mathf.Atan2(aim.y - to.y, aim.x - to.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 3; i++)
        {
            float a = (baseAngle + i * 120f) * Mathf.Deg2Rad;
            ThrowMini(to, to + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.7f, small);
        }
    }

    // ------------------------------------------------------------------

    private void Render()
    {
        Vector2 ground = Vector2.LerpUnclamped(from, to, t);
        float h = Mathf.Lerp(startHeight, 0f, t) + 4f * arcHeight * t * (1f - t);

        shadowT.position = Snap(ground);
        bodyT.position = Snap(ground + new Vector2(0f, h - 0.12f));

        // Bildwahl: ab dem Absprung gestreckt, um den Scheitel rund, vorm Aufprall wieder gestreckt
        if (state == State.Land)
        {
            body.sprite = frames[Mathf.Min(2, frames.Length - 1)];
        }
        else if (t < 0.18f || t > 0.78f)
        {
            body.sprite = frames[Mathf.Min(1, frames.Length - 1)];
        }
        else
        {
            body.sprite = frames[0];
        }

        Sprite[] sh = RoyalSplat.ShadowFrames;
        if (sh.Length > 0)
        {
            int idx = Mathf.Clamp(Mathf.FloorToInt(h / 0.55f), 0, sh.Length - 1);
            if (mini) idx = Mathf.Min(sh.Length - 1, idx + 2);
            shadow.sprite = sh[idx];
        }

        if (mark.enabled)
        {
            // Ring blinkt im Takt und wird zum Aufschlag hin kraeftiger
            mark.sprite = markFrames[Mathf.FloorToInt(Time.time * 8f) % markFrames.Length];
            mark.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, t));
        }
    }

    private static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, 0f);
    }
}
