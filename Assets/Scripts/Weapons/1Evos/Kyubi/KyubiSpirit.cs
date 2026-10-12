using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Der Neunschwanz selbst - die grosse Vorstellung des <see cref="FoxFireEvo"/>:
///
///   1. Aufsteigen  Feuersaeule an Inari, aus der der Geisterfuchs waechst
///   2. Ducken      kurz in die Knie, dann
///   3. Sprung      im Bogen in die dichteste Horde (Schatten am Boden)
///   4. Einschlag   Flammenring + neun Feuersaeulen, Schaden, Brand, Wackeln
///   5. Heulen      Kopf in den Himmel - alles im weiten Umkreis faengt Feuer
///   6. Zerfall     die neun Schwaenze reissen als Funken los und jagen je
///                  einen Gegner, der Rest verglueht; Inaris Irrlichter
///                  kehren vom Landeplatz zurueck
///
/// Bilder: Resources/Weapons/kyubi_rise/leap/howl/fade/slam_*/shadow.
/// Alle 160x128 mit Pivot am Boden unter der Fuchsmitte, Blick nach rechts -
/// nach links wird nur gespiegelt (flipX, keine Skalierung).
/// </summary>
public class KyubiSpirit : MonoBehaviour
{
    void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    private enum Phase { Rise, Crouch, Leap, Land, Howl, Fade }

    private const float RiseFps = 13f;
    private const float MoveFps = 14f;
    private const float HowlFps = 11f;
    private const float LeapSpeed = 16f;       // Tiles pro Sekunde
    private const float HowlReach = 1.7f;      // Heulen zuendet im x-fachen Ring an
    private const float SparkRange = 8f;

    // Bilder im Sprung-Streifen
    private const int CrouchFrames = 3;        // 0-2
    private const int TakeoffFrame = 3;
    private const int FlyFirst = 4, FlyCount = 3;   // 4-6 im Flug, geloopt
    private const int LandFirst = 7, LandCount = 3; // 7-9

    /// <summary>
    /// Wo die neun Schwanzspitzen im ersten Zerfall-Bild sitzen (Tiles, relativ
    /// zum Pivot, Blick nach rechts) - dort reissen die Funken los.
    /// </summary>
    private static readonly Vector2[] TailTips =
    {
        new Vector2(0.577f, 1.779f),
        new Vector2(0.084f, 2.185f),
        new Vector2(-0.581f, 2.466f),
        new Vector2(-1.245f, 2.210f),
        new Vector2(-1.787f, 1.926f),
        new Vector2(-2.235f, 1.576f),
        new Vector2(-2.292f, 1.062f),
        new Vector2(-2.377f, 0.591f),
        new Vector2(-2.400f, 0.024f),
    };

    private FoxFireEvo owner;
    private Transform follow;
    private FoxFire.Hit slam;
    private FoxFire.Hit spark;
    private float slamRadius;

    private Phase phase;
    private float age;
    private Vector2 pos;
    private Vector2 from;
    private Vector2 to;
    private float flightTime;
    private float arc;
    private bool flip;
    private bool howled;
    private bool released;

    private SpriteRenderer body;
    private SpriteRenderer shadow;

    public static KyubiSpirit Spawn(FoxFireEvo owner, Transform follow, FoxFire.Hit slam, FoxFire.Hit spark, float slamRadius)
    {
        GameObject go = new GameObject("Kyubi");
        go.transform.position = follow.position;
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded) SceneManager.MoveGameObjectToScene(go, gameScene);

        KyubiSpirit k = go.AddComponent<KyubiSpirit>();
        k.owner = owner;
        k.follow = follow;
        k.slam = slam;
        k.spark = spark;
        k.slamRadius = slamRadius;
        k.pos = follow.position;
        k.Enter(Phase.Rise);
        WeaponSounds.Play(WeaponSounds.KyubiRise);
        ScreenShake.Kick(1.5f, 0.5f);
        return k;
    }

    void Awake()
    {
        body = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(body, 4);

        // eigenes Objekt, kein Kind: der Schatten bleibt am Boden, waehrend der Fuchs fliegt
        GameObject s = new GameObject("KyubiShadow");
        if (RunScene.Current.IsValid() && RunScene.Current.isLoaded) SceneManager.MoveGameObjectToScene(s, RunScene.Current);
        shadow = s.AddComponent<SpriteRenderer>();
        shadow.sortingLayerID = body.sortingLayerID;
        shadow.sortingOrder = body.sortingOrder - 6;
        shadow.sprite = FoxFireEvo.ShadowSprite;
        shadow.enabled = false;
    }

    private void Enter(Phase p)
    {
        phase = p;
        age = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        switch (phase)
        {
            case Phase.Rise:
                if (follow != null) pos = follow.position;
                if (Show(FoxFireEvo.RiseFrames, RiseFps)) Enter(Phase.Crouch);
                break;

            case Phase.Crouch:
                if (follow != null) pos = follow.position;
                ShowFrame(FoxFireEvo.LeapFrames, Mathf.Min(CrouchFrames - 1, (int)(age * MoveFps)));
                if (age >= CrouchFrames / MoveFps) TakeOff();
                break;

            case Phase.Leap:
            {
                float u = Mathf.Min(1f, age / flightTime);
                Vector2 ground = Vector2.Lerp(from, to, u);
                pos = ground + Vector2.up * (Mathf.Sin(u * Mathf.PI) * arc);
                shadow.transform.position = Snap(ground);
                int f = u < 0.15f ? TakeoffFrame : FlyFirst + (int)(age * MoveFps) % FlyCount;
                ShowFrame(FoxFireEvo.LeapFrames, f);
                if (u >= 1f)
                {
                    pos = to;
                    shadow.enabled = false;
                    Slam();
                    Enter(Phase.Land);
                }
                break;
            }

            case Phase.Land:
                ShowFrame(FoxFireEvo.LeapFrames, LandFirst + Mathf.Min(LandCount - 1, (int)(age * MoveFps)));
                if (age >= LandCount / MoveFps) Enter(Phase.Howl);
                break;

            case Phase.Howl:
                if (!howled && age * HowlFps >= 3f) Howl();
                if (Show(FoxFireEvo.HowlFrames, HowlFps)) Enter(Phase.Fade);
                break;

            case Phase.Fade:
                if (!released && age * MoveFps >= 1f) Release();
                if (Show(FoxFireEvo.FadeFrames, MoveFps))
                {
                    Destroy(gameObject);
                    return;
                }
                break;
        }

        transform.position = Snap(pos);
        body.flipX = flip;
    }

    /// <summary>Bild nach Alter; true, wenn der Streifen durch ist.</summary>
    private bool Show(Sprite[] frames, float fps)
    {
        int f = (int)(age * fps);
        if (f >= frames.Length) return true;
        body.sprite = frames[f];
        return false;
    }

    private void ShowFrame(Sprite[] frames, int f)
    {
        body.sprite = frames[Mathf.Clamp(f, 0, frames.Length - 1)];
    }

    // ------------------------------------------------------------------

    private void TakeOff()
    {
        from = pos;
        // Ziel erst jetzt festlegen - die Horde hat sich waehrend des Aufsteigens bewegt
        Vector2? best = owner != null ? owner.BestSlamPoint(from, slamRadius) : null;
        to = best ?? from + (flip ? Vector2.left : Vector2.right) * 3f;

        float dist = Vector2.Distance(from, to);
        flightTime = Mathf.Clamp(dist / LeapSpeed, 0.3f, 0.6f);
        arc = Mathf.Min(3f, 0.9f + dist * 0.2f);
        if (Mathf.Abs(to.x - from.x) > 0.1f) flip = to.x < from.x;

        shadow.enabled = true;
        WeaponSounds.Play(WeaponSounds.FoxfireLaunch, 1.6f, 0.6f);
        Enter(Phase.Leap);
    }

    private void Slam()
    {
        FoxFx.Play(FoxFireEvo.SlamFor(slamRadius), to, 16f, 1);
        WeaponSounds.Play(WeaponSounds.KyubiSlam);
        ScreenShake.Kick(5f, 0.45f);

        var hits = new System.Collections.Generic.List<Enemy>();
        float reach = slamRadius + 0.3f;
        foreach (Enemy enemy in Enemy.Alive)
        {
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position - to).sqrMagnitude <= reach * reach) hits.Add(enemy);
        }
        foreach (Enemy enemy in hits)
        {
            if (enemy == null) continue;
            FoxBurn.Apply(enemy, slam);
            enemy.TakeDamage(slam.damage, null, 1.5f);
        }
    }

    /// <summary>Heulen: alles im weiten Umkreis faengt Fuchsfeuer (ohne Treffer-Schaden).</summary>
    private void Howl()
    {
        howled = true;
        WeaponSounds.Play(WeaponSounds.KyubiHowl);
        ScreenShake.Kick(2f, 0.6f);

        float reach = slamRadius * HowlReach;
        foreach (Enemy enemy in Enemy.Alive)
        {
            if (enemy == null || enemy.Untouchable) continue;
            if (((Vector2)enemy.transform.position - to).sqrMagnitude <= reach * reach) FoxBurn.Apply(enemy, spark);
        }
    }

    /// <summary>Die neun Schwaenze reissen los: je ein Funke jagt einen Gegner.</summary>
    private void Release()
    {
        released = true;
        for (int i = 0; i < TailTips.Length; i++)
        {
            Vector2 tip = TailTips[i];
            if (flip) tip.x = -tip.x;
            Vector2 at = pos + tip;
            Enemy target = FoxFire.FindTarget(at, SparkRange);
            if (target != null) FoxWisp.CreateLeap(at, target, spark);
        }
        if (owner != null) owner.SpiritDone(pos + Vector2.up * 1.2f);
    }

    private static Vector3 Snap(Vector2 p)
    {
        const float ppu = 32f;
        return new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0f);
    }
}
