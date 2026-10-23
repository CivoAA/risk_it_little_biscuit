using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stracciatella - Zwischenboss der Eiswelt. Ein Schneemann aus drei
/// Eiskugeln (Vanille, Stracciatella, Erdbeere) mit Minzschal,
/// Waffelroellchen-Armen und Waffeltueten-Hut. Schlittert auf seiner
/// Schmelzpfuetze hinter dem Spieler her. Prefab: Tools -> Gegner ->
/// Zwischenbosse bauen (ZwischenbossBuilder).
///
///   Frostblick  "Ochs am Berg": er haelt sich die Augen zu und zaehlt
///               kichernd, ueber dem Spieler oeffnet sich ein grosses Auge.
///               Dann reisst er die Arme runter und STARRT. Solange das Auge
///               offen ist, erstarrt die ganze Horde in der Naehe - und wer
///               sich jetzt bewegt, bekommt den Frostblick ab: Schaden und
///               eingefroren im Eisblock (der vom Eiskaiser).
///
/// Die Attacke dreht das Spiel einmal um: sonst heisst es immer "lauf weg",
/// hier "steh still". Wer stillhaelt, wird belohnt - die Waffen feuern
/// weiter, die Horde steht, und er steht auch.
///
/// Ab der Haelfte: kuerzer gezaehlt, laenger gestarrt, oefter.
/// </summary>
public class EnemyStracciatella : SelfAnimatedBoss
{
    // ------------------------------------------------------------- Balancing

    private const float FirstWalk = 4f;
    private const float WalkMin = 6f, WalkMax = 8f;
    private const float WalkMinPhase2 = 4f, WalkMaxPhase2 = 5.5f;

    /// <summary>So lange zaehlt er (Vorwarnung).</summary>
    private const float CountTime = 1.7f, CountTimePhase2 = 1.25f;

    /// <summary>So lange starrt er.</summary>
    private const float GlareTime = 2.2f, GlareTimePhase2 = 2.9f;

    /// <summary>Reaktionszeit am Anfang des Starrens: so lange zaehlt Bewegung noch nicht.</summary>
    private const float Grace = 0.18f;

    /// <summary>So lange muss man sich am Stueck bewegen, bis er es sieht (gegen Zucken am Stick).</summary>
    private const float MoveTolerance = 0.1f;

    private const float ZapDamageFactor = 1.6f;
    private const float FreezeSeconds = 1.4f;

    /// <summary>In diesem Umkreis erstarren die Gegner mit (etwa ein Bildschirm).</summary>
    private const float FreezeHordeRange = 13f;

    /// <summary>Das Auge schwebt so hoch ueber dem Spieler.</summary>
    private static readonly Vector2 EyeOffset = new Vector2(0f, 1.3f);

    /// <summary>Wo im Bild die Augen sitzen (Bild schaut nach rechts) - da beginnt der Strahl.</summary>
    private static readonly Vector2 EyesAt = new Vector2(0.25f, 2.65f);

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private Sprite[] glide;
    [SerializeField] private Sprite[] count;
    [SerializeField] private Sprite[] glare;
    [SerializeField] private Sprite[] eye;
    [SerializeField] private Sprite[] spark;
    [SerializeField] private Sprite[] iceBlock;

    protected override Sprite[] WalkStrip => glide;

    private SpriteRenderer eyeMarker;
    private readonly List<Spark> sparks = new List<Spark>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    private class Spark
    {
        public SpriteRenderer r;
        public float age;
        public bool live;
    }

    // ---------------------------------------------------------------- Ablauf

    protected override IEnumerator Run()
    {
        yield return Walk(FirstWalk);
        while (true)
        {
            if (Player != null) yield return FrostGlare();
            bool p2 = IsPhaseTwo;
            yield return Walk(p2 ? Random.Range(WalkMinPhase2, WalkMaxPhase2) : Random.Range(WalkMin, WalkMax));
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdateSparks();
    }

    private IEnumerator FrostGlare()
    {
        bool p2 = IsPhaseTwo;
        float countTime = p2 ? CountTimePhase2 : CountTime;
        float glareTime = p2 ? GlareTimePhase2 : GlareTime;
        FacePlayer();

        // 1. Augen zu und zaehlen - ueber dem Spieler geht das Auge auf
        EiskaiserSounds.Charge();
        ShowEye(true);
        float t = 0f;
        yield return PlayOnce(count, 0, 2);
        t += 3f / Fps;
        while (t < countTime)
        {
            t += Time.deltaTime;
            Show(count, 3 + Mathf.FloorToInt(t * Fps) % 8);
            SetEye(Mathf.Min(3, Mathf.FloorToInt(t / countTime * 4f)));
            yield return null;
        }

        // 2. Arme runter, starren - die Horde erstarrt
        EiskaiserSounds.Shatter();
        FreezeHorde(glareTime);
        yield return PlayOnce(glare, 0, 1);

        // Koop: der Blick gilt fuer jeden Spieler - wer sich zu lange bewegt, wird erwischt.
        var moved = new System.Collections.Generic.Dictionary<int, float>();
        bool zapped = false;
        t = 0f;
        while (t < glareTime)
        {
            t += Time.deltaTime;
            Show(glare, 2 + Mathf.FloorToInt(t * Fps) % 8);
            if (Player == null) break;

            if (!zapped)
            {
                SetEye(4 + Mathf.FloorToInt(t * 6f) % 4);
                Transform caught = null;
                foreach (NetPlayers.Target target in BossTargets.All())
                {
                    moved.TryGetValue(target.Slot, out float m);
                    m = NetPlayers.IsMoving(target) && t > Grace ? m + Time.deltaTime : 0f;
                    moved[target.Slot] = m;
                    if (m >= MoveTolerance && caught == null) caught = target.Transform;
                }
                if (caught != null)
                {
                    zapped = true;
                    yield return Zap(caught);
                    // nach dem Treffer starrt er nicht mehr lange
                    t = Mathf.Max(t, glareTime - 0.5f);
                }
            }
            yield return null;
        }

        ShowEye(false);
    }

    /// <summary>Frostblick: Funkenstrahl von seinen Augen zum Spieler, dann Eisblock.</summary>
    private IEnumerator Zap(Transform player)
    {
        SetEye(8);
        Vector2 from = Local(EyesAt);
        Vector2 to = (Vector2)player.transform.position + new Vector2(0f, 0.4f);
        int n = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(from, to) * 2.5f), 4, 40);
        for (int i = 0; i <= n; i++)
        {
            SpawnSpark(Vector2.Lerp(from, to, i / (float)n), -i * 0.012f);
        }
        yield return new WaitForSeconds(0.12f);
        SetEye(9);

        // Der Erwischte bekommt Schaden und Eis - ein Mitspieler per Nachricht.
        if (player != null && NetPlayers.TryGetTarget(player, out NetPlayers.Target target))
        {
            NetPlayers.Hurt(target, enemy.ContactDamage * ZapDamageFactor);
            PlayerController local = target.Local;
            if (local != null)
            {
                if (local.gameObject.activeSelf) PlayerIceBlock.Apply(local, iceBlock, FreezeSeconds);
            }
            else
            {
                NetPlayers.Freeze(target, FreezeSeconds);
            }
        }
        ScreenShake.Kick(3f, 0.25f);
    }

    /// <summary>Alle normalen Gegner und Elites in der Naehe frieren so lange ein, wie er starrt.</summary>
    private void FreezeHorde(float seconds)
    {
        Vector2 me = transform.position;
        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.IsBoss) continue;
            if (((Vector2)e.transform.position - me).sqrMagnitude > FreezeHordeRange * FreezeHordeRange) continue;
            e.Freeze(seconds, Random.Range(0.1f, 0.6f));
        }
    }

    // ---------------------------------------------------------------- Auge

    private void ShowEye(bool on)
    {
        if (eye == null || eye.Length == 0) return;
        if (eyeMarker == null) eyeMarker = NewEffectRenderer("Frostblick-Auge", 30);
        eyeMarker.enabled = on;
        if (on) SetEye(0);
    }

    private void SetEye(int frame)
    {
        if (eyeMarker == null || eye == null || eye.Length == 0) return;
        eyeMarker.sprite = eye[Mathf.Clamp(frame, 0, eye.Length - 1)];
    }

    private void LateUpdate()
    {
        if (eyeMarker == null || !eyeMarker.enabled) return;
        Transform player = Player;
        if (player == null)
        {
            eyeMarker.enabled = false;
            return;
        }
        eyeMarker.transform.position = Snap((Vector2)player.position + EyeOffset);
    }

    // ---------------------------------------------------------------- Funken

    private void SpawnSpark(Vector2 at, float delay)
    {
        if (spark == null || spark.Length == 0) return;
        Spark s = null;
        foreach (Spark x in sparks) if (!x.live && x.r != null) { s = x; break; }
        if (s == null)
        {
            s = new Spark { r = NewEffectRenderer("Frostfunke", 25) };
            sparks.Add(s);
        }
        s.age = delay;
        s.live = true;
        s.r.sprite = spark[0];
        s.r.enabled = delay >= 0f;
        s.r.transform.position = Snap(at + Random.insideUnitCircle * 0.15f);
    }

    private void UpdateSparks()
    {
        foreach (Spark s in sparks)
        {
            if (!s.live) continue;
            s.age += Time.deltaTime;
            if (s.age < 0f) continue;
            int i = Mathf.FloorToInt(s.age * 16f);
            if (i >= spark.Length)
            {
                s.live = false;
                s.r.enabled = false;
                continue;
            }
            s.r.enabled = true;
            s.r.sprite = spark[i];
        }
    }

    protected override void Cleanup()
    {
        Kill(eyeMarker);
        eyeMarker = null;
        foreach (Spark s in sparks) Kill(s.r);
        sparks.Clear();
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer ZwischenbossBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Sprite[] glideStrip, Sprite[] countStrip, Sprite[] glareStrip,
                           Sprite[] eyeStrip, Sprite[] sparkStrip, Sprite[] iceBlockStrip)
    {
        body = bodyRenderer;
        glide = glideStrip;
        count = countStrip;
        glare = glareStrip;
        eye = eyeStrip;
        spark = sparkStrip;
        iceBlock = iceBlockStrip;
    }
#endif
}
