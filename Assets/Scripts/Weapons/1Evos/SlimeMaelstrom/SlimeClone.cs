using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ein Schleimklon der Evo <see cref="RoyalSplatEvo"/> (Schleimstrudel):
/// springt im Bogen vom Koenig auf einen festen Landepunkt (der Ring am Boden
/// zeigt ihn vorher an), klatscht auf und bleibt dort sitzen. Unter ihm dreht
/// sich ein Strudel, der jeden Gegner im Radius zur Mitte zieht, bremst und im
/// Takt verletzt (wie <see cref="VortexPrefab"/>, nur doppelt so gross). Laeuft
/// der Strudel aus, zerplatzt der Klon - mit dem Stern Zerplatzen in drei
/// Mini-Klopse.
///
/// Der Klon ist eine lila eingefaerbte Momentaufnahme des Spieler-Sprites
/// (wie der Doppelgaenger der <see cref="SlimeSplit"/>). Alle Werte kommen beim
/// Sprung als Kopie mit - der Klon ueberlebt so auch die Waffe.
///
/// Lebt in der Lauf-Szene, nicht am Spieler.
/// </summary>
public class SlimeClone : MonoBehaviour
{
    /// <summary>Alles, was der Strudel braucht.</summary>
    public struct Pull
    {
        public float radius;
        public float time;
        public float tickDamage;
        public float tickInterval;
    }

    private enum State { Fly, Sit }

    private const float PixelsPerUnit = 32f;

    /// <summary>Zuggeschwindigkeit am Rand (Tiles/s) - etwas mehr als beim Vortex, der Strudel ist doppelt so gross.</summary>
    private const float PullSpeed = 6f;

    /// <summary>Ab diesem Abstand zur Mitte wird nicht weiter gezogen - verhindert Zittern.</summary>
    private const float DeadZone = 0.3f;

    /// <summary>Tempo-Faktor im Strudel (wie beim Vortex).</summary>
    private const float SlowMultiplier = 0.5f;

    /// <summary>Umdrehungen des Strudels in Grad pro Sekunde.</summary>
    private const float SpinSpeed = 180f;

    private static readonly Color Tint = new Color(0.75f, 0.55f, 1f, 0.9f);

    private static readonly List<SlimeClone> live = new List<SlimeClone>();

    private SpriteRenderer body, shadow, mark, swirl;
    private Transform bodyT, shadowT, markT, swirlT;
    private SpriteFlipbook swirlLook;
    private Sprite[] markFrames;

    private State state;
    private Vector2 from, to;
    private float startHeight, arcHeight, flightTime, t;
    private float sitAge, nextTick;
    private Vector3 bodyScale;
    private RoyalSplat.Hit hit;
    private Pull pull;

    /// <summary>Haengt die Mitten aller stehenden Strudel an <paramref name="into"/> an.</summary>
    public static void AddLiveCenters(List<Vector2> into)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i] == null) { live.RemoveAt(i); continue; }
            into.Add(live[i].to);
        }
    }

    /// <summary>Sprung vom Spieler: startet in Zepterhoehe ueber <paramref name="ground"/>.</summary>
    public static void Jump(Vector2 ground, float height, Vector2 spot, RoyalSplat.Hit hit, Pull pull,
                            SpriteRenderer playerBody, Sprite[] vortexFrames)
    {
        GameObject go = new GameObject("SlimeClone");

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded) SceneManager.MoveGameObjectToScene(go, gameScene);

        SlimeClone clone = go.AddComponent<SlimeClone>();
        clone.hit = hit;
        clone.pull = pull;
        clone.Build(playerBody, vortexFrames);

        float dist = (spot - ground).magnitude;
        clone.from = ground;
        clone.to = spot;
        clone.startHeight = height;
        clone.flightTime = Mathf.Min(0.6f, 0.36f + 0.03f * dist);
        clone.arcHeight = 1.1f + 0.16f * dist;
        clone.markT.position = Snap(spot);
        clone.Render();

        live.Add(clone);
    }

    private void Build(SpriteRenderer playerBody, Sprite[] vortexFrames)
    {
        // Wurzel bleibt am Weltursprung, die Teile setzen ihre Position selbst
        swirl = Child("Swirl", -2, out swirlT);
        shadow = Child("Shadow", -1, out shadowT);
        mark = Child("Mark", -1, out markT);
        body = Child("Body", 3, out bodyT);

        if (playerBody != null)
        {
            body.sprite = playerBody.sprite;
            body.flipX = playerBody.flipX;
            bodyT.localScale = playerBody.transform.lossyScale;
        }
        body.color = Tint;
        bodyScale = bodyT.localScale;

        Sprite[] sh = RoyalSplat.ShadowFrames;
        shadow.enabled = sh.Length > 0;

        markFrames = RoyalSplat.MarkFor(hit.radius);
        mark.enabled = markFrames != null && markFrames.Length > 0;

        // Der Strudel wartet unsichtbar, bis der Klon landet
        swirl.enabled = false;
        if (vortexFrames != null && vortexFrames.Length > 0)
        {
            swirlLook = swirl.gameObject.AddComponent<SpriteFlipbook>();
            swirlLook.SetFrames(vortexFrames, 12f);
            swirlLook.enabled = false;
        }
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

    void Update()
    {
        if (state == State.Fly)
        {
            t += Time.deltaTime / Mathf.Max(0.12f, flightTime);
            if (t >= 1f)
            {
                t = 1f;
                Touchdown();
            }
            Render();
            return;
        }

        sitAge += Time.deltaTime;
        if (sitAge >= pull.time)
        {
            Burst();
            return;
        }

        // Gelee wabbelt, der Strudel dreht sich
        float wobble = Mathf.Sin(sitAge * 9f) * 0.08f;
        bodyT.localScale = new Vector3(bodyScale.x * (1f + wobble), bodyScale.y * (1f - wobble), bodyScale.z);
        swirlT.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
        if (swirlLook != null) swirlLook.SetLifeLeft(pull.time - sitAge);

        PullAndTick();
    }

    // ------------------------------------------------------------------

    private void Touchdown()
    {
        state = State.Sit;
        mark.enabled = false;
        shadow.enabled = false;

        RoyalSplat.Splash(to, hit.damage, hit.radius, null, 1.5f);
        if (hit.puddleTime > 0f) SplatPuddle.Create(to, hit.radius * 0.95f, hit.puddleTime);

        swirlT.position = Snap(to);
        swirlT.localScale = Vector3.one * (pull.radius * 2f);   // 64 px bei 64 PPU = 1 Tile Durchmesser
        swirl.enabled = swirlLook != null;
        if (swirlLook != null) swirlLook.enabled = true;

        nextTick = 0f;
    }

    private void PullAndTick()
    {
        bool tick = sitAge >= nextTick;
        if (tick) nextTick = sitAge + Mathf.Max(0.05f, pull.tickInterval);

        float r2 = pull.radius * pull.radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;

        // rueckwaerts - TakeDamage kann Gegner aus Alive nehmen
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (i >= alive.Count) continue;
            Enemy enemy = alive[i];
            if (enemy == null || enemy.Untouchable) continue;

            Vector2 toCenter = to - (Vector2)enemy.transform.position;
            float d2 = toCenter.sqrMagnitude;
            if (d2 > r2) continue;

            // Bosse ignorieren den Zug ohnehin (Enemy.ApplyPull)
            if (d2 > DeadZone * DeadZone) enemy.ApplyPull(toCenter.normalized * PullSpeed);
            if (tick) enemy.TakeDamage(pull.tickDamage, SlowMultiplier, 0f);
        }
    }

    private void Burst()
    {
        Vector2 center = to + RoyalSplat.BodyOffset;
        FoxFx.Play(RoyalSplat.HitFor(hit.radius), center, 22f, 1);
        WeaponSounds.Play(WeaponSounds.SplatLand);
        if (hit.split) SplatBlob.SplitAt(to, hit, null);

        live.Remove(this);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        live.Remove(this);
    }

    // ------------------------------------------------------------------

    private void Render()
    {
        Vector2 ground = Vector2.LerpUnclamped(from, to, t);
        float h = Mathf.Lerp(startHeight, 0f, t) + 4f * arcHeight * t * (1f - t);

        shadowT.position = Snap(ground);
        bodyT.position = Snap(ground + new Vector2(0f, h));

        Sprite[] sh = RoyalSplat.ShadowFrames;
        if (shadow.enabled && sh.Length > 0)
            shadow.sprite = sh[Mathf.Clamp(Mathf.FloorToInt(h / 0.55f), 0, sh.Length - 1)];

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
