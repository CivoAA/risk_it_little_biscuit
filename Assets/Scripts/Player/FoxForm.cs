using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Skilltree "Fuchsgestalt" (<see cref="SkillGrants.Fuchsgestalt"/>), Inaris
/// zweite Chance: ein toedlicher Treffer laesst ihn nicht sterben, sondern
/// mit 1 Leben als Geisterfuchs aufstehen - <see cref="Duration"/> Sekunden
/// lang:
///
///   - unverwundbar (<see cref="PlayerController.TakeDamage"/> fragt <see cref="Active"/>)
///   - alle Waffen ruhen (ihre Komponenten sind aus, Fliegendes trifft noch)
///   - jeder normale Gegner, den der Fuchs beruehrt, nimmt <see cref="ContactDamage"/>
///     Schaden - Elites, Minibosse und Bosse nicht
///   - jeder Gegner, der waehrenddessen stirbt, heilt <see cref="HealPerKill"/> Leben
///   - Gegner lassen nur <see cref="XpFactor"/> der XP fallen (<see cref="Enemy"/>)
///
/// Danach zerfaellt der Fuchs, Inari steht wieder da (kurz unverwundbar).
/// Ausgeloest von <see cref="PlayerController"/>, der auch die Abklingzeit
/// haelt. Bilder: die des Neunschwanzes aus der Evo (kyubi_rise/leap/fade,
/// Pivot am Boden unter der Fuchsmitte, Blick nach rechts).
/// </summary>
public class FoxForm : MonoBehaviour
{
    /// <summary>So lange bleibt Inari ein Fuchs (Sekunden).</summary>
    public const float Duration = 10f;

    /// <summary>Abklingzeit, gerechnet ab dem ENDE der Wirkung.</summary>
    public const float Cooldown = 120f;

    /// <summary>Schaden je Beruehrung an normalen Gegnern.</summary>
    public const float ContactDamage = 100f;

    /// <summary>So viel Leben bringt jeder Kill waehrend der Fuchsgestalt.</summary>
    public const float HealPerKill = 1f;

    /// <summary>Anteil der XP, den Gegner waehrenddessen fallen lassen.</summary>
    public const float XpFactor = 0.25f;

    /// <summary>Beruehrungs-Radius um die Fuchsmitte (Tiles).</summary>
    public const float ContactRadius = 1.4f;

    /// <summary>Derselbe Gegner wird hoechstens so oft getroffen (Sekunden).</summary>
    private const float HitInterval = 0.5f;

    /// <summary>Unverwundbar nach der Rueckverwandlung, damit der Pulk ihn nicht sofort wieder holt.</summary>
    private const float AfterImmunity = 1.5f;

    /// <summary>Fuchsmitte ueber dem Pivot (der liegt unter den Fuessen).</summary>
    private static readonly Vector2 BodyCenter = new Vector2(0f, 0.9f);

    private const float RiseFps = 13f;
    private const float RunFps = 14f;
    private const float FadeFps = 11f;

    // Bilder im Sprung-Streifen der Evo: 4-6 im Flug - als Galopp geloopt
    private const int RunFirst = 4, RunCount = 3;

    private enum Phase { Rise, Run, Fade }

    private static FoxForm current;

    /// <summary>Ist Inari gerade ein Fuchs? Waehrend des Zerfalls nicht mehr.</summary>
    public static bool Active => current != null && current.phase != Phase.Fade;

    private PlayerController player;
    private SpriteRenderer body;
    private SpriteRenderer shadow;
    private SpriteRenderer playerBody;
    private Phase phase;
    private float age;
    private float lifetime;
    private bool flip;
    private Vector2 lastPos;
    private float nextContact;

    private readonly List<Weapon> paused = new List<Weapon>();
    private readonly Dictionary<Enemy, float> lastHit = new Dictionary<Enemy, float>();
    private readonly HashSet<Enemy> counted = new HashSet<Enemy>();
    private static readonly List<Enemy> hitBuffer = new List<Enemy>();

    /// <summary>Verwandelt <paramref name="who"/> in den Geisterfuchs.</summary>
    public static void Fire(PlayerController who)
    {
        Cancel();

        GameObject go = new GameObject("FoxForm");
        go.transform.position = who.transform.position;

        Scene run = RunScene.Current;
        if (run.IsValid() && run.isLoaded) SceneManager.MoveGameObjectToScene(go, run);

        FoxForm form = go.AddComponent<FoxForm>();
        form.Begin(who);
    }

    /// <summary>Sofort zurueck (neuer Lauf) - ohne Zerfall.</summary>
    public static void Cancel()
    {
        if (current == null) return;
        Destroy(current.gameObject);
        current = null;
    }

    private void Begin(PlayerController who)
    {
        current = this;
        player = who;
        lastPos = who.transform.position;

        body = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(body, 4);

        // eigenes Objekt, kein Kind - wie beim Neunschwanz
        GameObject s = new GameObject("FoxFormShadow");
        if (RunScene.Current.IsValid() && RunScene.Current.isLoaded) SceneManager.MoveGameObjectToScene(s, RunScene.Current);
        shadow = s.AddComponent<SpriteRenderer>();
        shadow.sortingLayerID = body.sortingLayerID;
        shadow.sortingOrder = body.sortingOrder - 6;
        shadow.sprite = FoxFireEvo.ShadowSprite;

        playerBody = who.BodyRenderer();
        if (playerBody != null) playerBody.enabled = false;

        PauseWeapons();
        Enemy.Damaged -= OnEnemyDamaged;
        Enemy.Damaged += OnEnemyDamaged;

        phase = Phase.Rise;
        WeaponSounds.Play(WeaponSounds.KyubiRise);
        ScreenShake.Kick(1.5f, 0.5f);
        Follow();
        Draw();
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        if (phase != Phase.Fade)
        {
            lifetime += dt;
            // auch Waffen, die waehrenddessen dazukommen (Level-Up), ruhen
            PauseWeapons();
            Contact();

            if (phase == Phase.Rise && age >= FoxFireEvo.RiseFrames.Length / RiseFps) Enter(Phase.Run);
            if (lifetime >= Duration) BeginFade();
        }
        else if (age >= FoxFireEvo.FadeFrames.Length / FadeFps)
        {
            Destroy(gameObject);
            return;
        }

        Follow();
        Draw();
    }

    void OnDestroy()
    {
        Enemy.Damaged -= OnEnemyDamaged;
        if (current == this) current = null;
        if (shadow != null) Destroy(shadow.gameObject);
        RestorePlayer();
    }

    private void Enter(Phase p)
    {
        phase = p;
        age = 0f;
    }

    /// <summary>Der Fuchs zerfaellt, Inari steht wieder da - kurz unverwundbar.</summary>
    private void BeginFade()
    {
        Enter(Phase.Fade);
        RestorePlayer();
        if (player != null) player.GrantImmunity(AfterImmunity);
        WeaponSounds.Play(WeaponSounds.KyubiHowl);
    }

    // ------------------------------------------------------------------
    //  Waffen
    // ------------------------------------------------------------------

    private void PauseWeapons()
    {
        Pause(player.activeWeapon);
        Pause(player.activeEvos);
    }

    private void Pause(Weapon[] weapons)
    {
        if (weapons == null) return;
        foreach (Weapon w in weapons)
        {
            if (w == null || !w.enabled || !w.IsActive) continue;
            w.enabled = false;
            paused.Add(w);
        }
    }

    private void RestorePlayer()
    {
        for (int i = 0; i < paused.Count; i++)
        {
            if (paused[i] != null) paused[i].enabled = true;
        }
        paused.Clear();

        if (playerBody != null) playerBody.enabled = true;
        playerBody = null;
    }

    // ------------------------------------------------------------------
    //  Kampf
    // ------------------------------------------------------------------

    private void Contact()
    {
        if (Time.time < nextContact) return;
        nextContact = Time.time + 0.05f;

        Vector2 center = (Vector2)player.transform.position + BodyCenter;
        float r2 = ContactRadius * ContactRadius;

        // erst sammeln, dann treffen - TakeDamage kann Gegner aus Alive nehmen
        hitBuffer.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy e = alive[i];
            if (e == null || e.Untouchable || !e.IsNormal) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude > r2) continue;
            if (lastHit.TryGetValue(e, out float at) && Time.time - at < HitInterval) continue;
            hitBuffer.Add(e);
        }

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            Enemy e = hitBuffer[i];
            if (e == null) continue;
            lastHit[e] = Time.time;
            e.TakeDamage(ContactDamage);
        }
        hitBuffer.Clear();
    }

    /// <summary>Jeder Gegner, der waehrend der Fuchsgestalt stirbt, heilt Inari.</summary>
    private void OnEnemyDamaged(Enemy enemy, float damage)
    {
        if (phase == Phase.Fade || enemy == null || enemy.HealthFraction > 0f) return;
        if (!counted.Add(enemy)) return;
        if (player != null) player.Heal(HealPerKill);
    }

    // ------------------------------------------------------------------
    //  Bild
    // ------------------------------------------------------------------

    private void Follow()
    {
        Vector2 pos = player.transform.position;
        Vector2 delta = pos - lastPos;
        lastPos = pos;
        if (Mathf.Abs(delta.x) > 1e-4f) flip = delta.x < 0f;

        transform.position = Snap(pos);
        if (shadow != null) shadow.transform.position = Snap(pos);
    }

    private void Draw()
    {
        body.flipX = flip;
        switch (phase)
        {
            case Phase.Rise:
                ShowFrame(FoxFireEvo.RiseFrames, (int)(age * RiseFps));
                break;

            case Phase.Run:
                Sprite[] leap = FoxFireEvo.LeapFrames;
                bool moving = player.playerMoveDirection.sqrMagnitude > 0.01f;
                if (moving && leap.Length >= RunFirst + RunCount)
                {
                    ShowFrame(leap, RunFirst + (int)(age * RunFps) % RunCount);
                }
                else
                {
                    // steht er, bleibt der fertig aufgestiegene Fuchs stehen
                    Sprite[] rise = FoxFireEvo.RiseFrames;
                    ShowFrame(rise, rise.Length - 1);
                }
                break;

            case Phase.Fade:
                ShowFrame(FoxFireEvo.FadeFrames, (int)(age * FadeFps));
                if (shadow != null) shadow.enabled = false;
                break;
        }
    }

    private void ShowFrame(Sprite[] frames, int f)
    {
        if (frames == null || frames.Length == 0) return;
        body.sprite = frames[Mathf.Clamp(f, 0, frames.Length - 1)];
    }

    private static Vector3 Snap(Vector2 p)
    {
        const float ppu = 32f;
        return new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0f);
    }
}
