using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ein Irrlicht des <see cref="FoxFire"/>: schwebt an seinem Platz hinter
/// dem Fuchs, fliegt auf Befehl in einem Bogen zum Gegner (mit
/// Nachleuchten), verpufft dort im Flammenring und kehrt im Bogen zurueck.
/// Als Lauffeuer-Funke (<see cref="CreateLeap"/>) fliegt es nur hin und ist
/// danach weg.
///
/// Lebt in der Lauf-Szene, nicht am Spieler: so dreht und spiegelt es nichts
/// mit, und die Weltposition steuert es selbst.
///
/// Skaliert wird NIE (die Lauf-Kamera ist pixelgenau, sonst wird es unscharf):
/// Funke und Nachleuchten haben eigene, kleiner gemalte Bilder, Ein- und
/// Ausblenden laeuft nur ueber die Deckkraft.
/// </summary>
public class FoxWisp : MonoBehaviour
{
    private enum State { Idle, Out, Back, Leap, Vanish }

    private const float Fps = 10f;
    private const int Ghosts = 4;
    private const int History = 10;
    private const float BackTime = 0.4f;
    private const float VanishTime = 0.2f;

    /// <summary>Der Gegner sitzt mit dem Pivot unten - getroffen wird etwas hoeher.</summary>
    private static readonly Vector2 AimOffset = new Vector2(0f, 0.35f);

    private static readonly HashSet<Enemy> targeted = new HashSet<Enemy>();

    /// <summary>Wird dieser Gegner gerade schon von einem Irrlicht angeflogen?</summary>
    public static bool IsTargeted(Enemy enemy) { return enemy != null && targeted.Contains(enemy); }

    public Vector2 Slot;
    public float ReadyAt;

    private State state = State.Idle;
    private SpriteRenderer body;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[Ghosts];
    private readonly Vector2[] history = new Vector2[History];
    private int historyHead;

    private Vector2 pos;
    private Vector2 vel;
    private Vector2 from;
    private Vector2 ctrl;
    private Vector2 aim;
    private Enemy target;
    private FoxFire.Hit hit;
    private float t;
    private float flightTime;
    private float cooldown;
    private float animPhase;
    private bool leap;

    public bool IsIdle => state == State.Idle;
    public Vector2 Position => pos;

    // ------------------------------------------------------------------

    public static FoxWisp Create(Vector2 at)
    {
        FoxWisp wisp = Spawn("FoxWisp", at);
        wisp.Slot = at;
        return wisp;
    }

    /// <summary>Lauffeuer-Funke: fliegt von <paramref name="at"/> zum Gegner, zuendet ihn an und ist weg.</summary>
    public static void CreateLeap(Vector2 at, Enemy target, FoxFire.Hit hit)
    {
        FoxWisp wisp = Spawn("FoxLeap", at);
        wisp.leap = true;
        wisp.hit = hit;
        wisp.BeginFlight(target, State.Leap, 1.1f);
    }

    private static FoxWisp Spawn(string name, Vector2 at)
    {
        GameObject go = new GameObject(name);
        go.transform.position = at;

        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(go, gameScene);
        }

        FoxWisp wisp = go.AddComponent<FoxWisp>();
        wisp.pos = at;
        for (int i = 0; i < History; i++) wisp.history[i] = at;
        return wisp;
    }

    void Awake()
    {
        body = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(body, 2);
        animPhase = Random.value * 10f;

        for (int i = 0; i < Ghosts; i++)
        {
            GameObject g = new GameObject("Ghost");
            g.transform.SetParent(transform, false);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            sr.enabled = false;
            ghosts[i] = sr;
        }
    }

    void OnDestroy()
    {
        if (target != null) targeted.Remove(target);
    }

    // ------------------------------------------------------------------

    public void Launch(Enemy enemy, FoxFire.Hit shot, float cooldownAfter)
    {
        hit = shot;
        cooldown = cooldownAfter;
        BeginFlight(enemy, State.Out, 1.6f);
    }

    /// <summary>Verglimmt und verschwindet (Waffe weg oder ein Irrlicht zu viel).</summary>
    public void Vanish()
    {
        if (this == null) return;
        if (target != null) targeted.Remove(target);
        target = null;
        state = State.Vanish;
        t = 0f;
    }

    private void BeginFlight(Enemy enemy, State flight, float arc)
    {
        target = enemy;
        if (target != null) targeted.Add(target);
        aim = target != null ? (Vector2)target.transform.position + AimOffset : pos;
        from = pos;

        Vector2 d = aim - from;
        float dist = d.magnitude;
        // Bogen: seitlich ausholen, abwechselnd links und rechts
        Vector2 perp = dist > 0.01f ? new Vector2(-d.y, d.x) / dist : Vector2.up;
        float side = Random.value < 0.5f ? -1f : 1f;
        ctrl = from + d * 0.35f + perp * side * Mathf.Min(arc, dist * 0.5f) + Vector2.up * 0.4f;

        flightTime = Mathf.Max(0.18f, dist / FoxFire.FlySpeed * 1.15f);
        t = 0f;
        state = flight;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Idle:
                pos = Vector2.SmoothDamp(pos, Slot, ref vel, 0.12f);
                break;

            case State.Out:
            case State.Leap:
                if (target != null && !target.Untouchable) aim = (Vector2)target.transform.position + AimOffset;
                t += dt / flightTime;
                pos = Bezier(from, ctrl, aim, EaseIn(Mathf.Min(1f, t)));
                if (t >= 1f) Arrive();
                break;

            case State.Back:
                t += dt / BackTime;
                pos = Bezier(from, ctrl, Slot, EaseOut(Mathf.Min(1f, t)));
                if (t >= 1f)
                {
                    state = State.Idle;
                    vel = Vector2.zero;
                    ReadyAt = Time.time + cooldown;
                }
                break;

            case State.Vanish:
                t += dt / VanishTime;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                break;
        }

        Draw();
    }

    private void Arrive()
    {
        if (target != null) targeted.Remove(target);
        target = null;
        FoxFire.Explode(aim, hit);

        if (state == State.Leap)
        {
            Destroy(gameObject);
            return;
        }

        // zurueck im Bogen auf die andere Seite
        from = pos;
        Vector2 d = Slot - from;
        Vector2 perp = d.sqrMagnitude > 0.0001f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
        ctrl = from + d * 0.5f + perp * 1.2f + Vector2.up * 0.8f;
        t = 0f;
        state = State.Back;
    }

    private void Draw()
    {
        Sprite[] frames = leap ? FoxFire.SparkFrames : FoxFire.WispFrames;
        int frame = (int)((Time.time + animPhase) * Fps) % frames.Length;
        body.sprite = frames[frame];

        // auf ganze Pixel setzen, sonst flimmert die Pixelart beim Schweben
        transform.position = Snap(pos);
        body.color = new Color(1f, 1f, 1f, state == State.Vanish ? 1f - t : 1f);

        // Nachleuchten: alte Positionen, immer blasser und kleiner
        historyHead = (historyHead + 1) % History;
        history[historyHead] = pos;
        bool moving = state == State.Out || state == State.Back || state == State.Leap;
        Sprite[] trail = FoxFire.TrailFrames;
        for (int i = 0; i < Ghosts; i++)
        {
            SpriteRenderer g = ghosts[i];
            g.enabled = moving;
            if (!moving) continue;

            int back = (i + 1) * 2;
            Vector2 p = history[(historyHead - back + History * 4) % History];
            // Funken ziehen einen kuerzeren Schweif: sie fangen beim kleineren Ball an
            int size = Mathf.Min(trail.Length - 1, i + (leap ? 1 : 0));
            g.sprite = trail[size];
            g.transform.position = Snap(p);
            float k = 1f - (i + 1) / (float)(Ghosts + 1);
            g.color = new Color(1f, 1f, 1f, 0.75f * k + 0.15f);
        }
    }

    private static Vector3 Snap(Vector2 p)
    {
        const float ppu = 32f;
        return new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0f);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float u)
    {
        float v = 1f - u;
        return v * v * a + 2f * v * u * c + u * u * b;
    }

    /// <summary>Losschnellen mit etwas Anlauf, dann immer schneller zum Ziel.</summary>
    private static float EaseIn(float u) { return 0.35f * u + 0.65f * u * u; }

    private static float EaseOut(float u) { return 1f - (1f - u) * (1f - u); }
}
