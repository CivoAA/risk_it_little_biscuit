using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Eine Seele (Hitodama) fuer den <see cref="FoxFireEvo"/>: steigt aus einem
/// Gegner, der im Fuchsfeuer gestorben ist, schwebt kurz hoch und zieht dann
/// immer schneller zu Inari. Kommt sie an, leuchtet es kurz auf und der
/// Kyuubi bekommt eine Seele gutgeschrieben.
///
/// Wie die Irrlichter: lebt in der Lauf-Szene, wird nie skaliert und sitzt
/// immer auf ganzen Pixeln.
/// </summary>
public class KyubiSoul : MonoBehaviour
{
    private const float Fps = 12f;
    private const float RiseTime = 0.28f;
    private const float MaxLife = 3.5f;
    private const float StartSpeed = 3f;
    private const float Accel = 26f;
    private const float MaxSpeed = 18f;
    private const float ArriveDist = 0.3f;
    private const int Ghosts = 2;

    private static int alive;

    /// <summary>So viele Seelen fliegen gerade sichtbar herum.</summary>
    public static int Alive => alive;

    private FoxFireEvo owner;
    private SpriteRenderer body;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[Ghosts];
    private readonly Vector2[] trail = new Vector2[6];
    private Vector2 pos;
    private Vector2 vel;
    private float age;
    private float phase;

    public static void Spawn(FoxFireEvo owner, Vector2 at)
    {
        GameObject go = new GameObject("KyubiSoul");
        go.transform.position = at;
        Scene gameScene = RunScene.Current;
        if (gameScene.IsValid() && gameScene.isLoaded) SceneManager.MoveGameObjectToScene(go, gameScene);

        KyubiSoul soul = go.AddComponent<KyubiSoul>();
        soul.owner = owner;
        soul.pos = at;
        for (int i = 0; i < soul.trail.Length; i++) soul.trail[i] = at;
        // erst nach oben, leicht zur Seite gewuerfelt
        soul.vel = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2.5f, 3.5f));
    }

    void Awake()
    {
        alive++;
        phase = Random.value * 10f;
        body = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(body, 3);
        for (int i = 0; i < Ghosts; i++)
        {
            GameObject g = new GameObject("Ghost");
            g.transform.SetParent(transform, false);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            ghosts[i] = sr;
        }
    }

    void OnDestroy() { alive--; }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (owner == null || !owner.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 home = owner.Center;
        if (age < RiseTime)
        {
            // aufsteigen, langsamer werdend
            vel *= Mathf.Exp(-4f * dt);
        }
        else
        {
            Vector2 to = home - pos;
            float dist = to.magnitude;
            if (dist < ArriveDist || age > MaxLife)
            {
                Arrive();
                return;
            }
            float speed = Mathf.Min(MaxSpeed, StartSpeed + Accel * (age - RiseTime));
            Vector2 want = to / dist * speed;
            // weich einschwenken - keine Kurve, die am Ziel vorbeischiesst
            vel = Vector2.Lerp(vel, want, 1f - Mathf.Exp(-9f * dt));
            if (vel.sqrMagnitude * dt * dt > dist * dist) vel = to / Mathf.Max(dt, 1e-4f);
        }
        pos += vel * dt;
        Draw();
    }

    private void Arrive()
    {
        FoxFx.Play(FoxFireEvo.AbsorbFrames, owner.Center, 18f, 4);
        WeaponSounds.Play(WeaponSounds.KyubiSoul);
        owner.AddSoul();
        Destroy(gameObject);
    }

    private void Draw()
    {
        Sprite[] frames = FoxFireEvo.SoulFrames;
        body.sprite = frames[(int)((age + phase) * Fps) % frames.Length];
        transform.position = Snap(pos);

        for (int i = trail.Length - 1; i > 0; i--) trail[i] = trail[i - 1];
        trail[0] = pos;
        Sprite[] t = FoxFire.TrailFrames;
        for (int i = 0; i < Ghosts; i++)
        {
            SpriteRenderer g = ghosts[i];
            g.sprite = t[Mathf.Min(t.Length - 1, 2 + i)];
            g.transform.position = Snap(trail[2 + i * 2]);
            g.color = new Color(0.8f, 0.82f, 1f, 0.55f - i * 0.2f);
        }
    }

    private static Vector3 Snap(Vector2 p)
    {
        const float ppu = 32f;
        return new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0f);
    }
}
