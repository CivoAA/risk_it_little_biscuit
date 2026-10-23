using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Die Ofenkartoffel - Zwischenboss der Vulkanwelt. Eine riesige
/// Folienkartoffel mit Sauerrahm-Krone, Butterstueck und gluehenden
/// Lava-Rissen, Topflappen als Haende, watschelt auf zwei Kartoffelfuesschen.
/// Prefab: Tools -> Gegner -> Zwischenbosse bauen (ZwischenbossBuilder).
///
///   Heisse Kartoffel  holt aus und wirft dem Spieler eine kleine, gluehende
///             Kartoffel zu. Die bleibt an ihm kleben und tickt - die rote Zone
///             um sie herum zeigt Reichweite und Restzeit. Weitergeben geht nur
///             im Vorbeilaufen (knapp vor dem Beruehren, danach kurz
///             unverwundbar): an irgendeinen Gegner (dann fliegt der mitsamt
///             seinen Nachbarn in die Luft) - oder zurueck an die Ofenkartoffel.
///             Die Lunte laeuft dabei einfach weiter. Hat der Boss sie, jagt er
///             dem Spieler hinterher, immer schneller, und gibt sie nach kurzer
///             Pause beim Beruehren zurueck. Knallt es bei ihm, kostet ihn das
///             10 % seiner vollen Leben.
///             Bleibt sie beim Spieler, knallt es dort.
///
/// Waffen kratzen ihn kaum (95 % weniger Schaden), und Beruehren tut nicht
/// weh - der Kampf ist die Kartoffel. Also moeglichst spaet abgeben, damit
/// sie bei ihm bleibt. Ab der Haelfte: kuerzere Lunte, oefter.
/// </summary>
public class EnemyOfenkartoffel : SelfAnimatedBoss
{
    // ------------------------------------------------------------- Balancing

    private const float FirstWalk = 3.5f;
    private const float WalkMin = 4.5f, WalkMax = 6f;
    private const float WalkMinPhase2 = 3f, WalkMaxPhase2 = 4.5f;

    /// <summary>Lunte ab dem Moment, in dem sie klebt.</summary>
    private const float Fuse = 10f, FusePhase2 = 8f;

    /// <summary>Faktor auf jeden Waffentreffer: 95 % weniger Schaden.</summary>
    private const float WeaponDamageTaken = 0.05f;

    /// <summary>
    /// Jagd mit der Kartoffel, als Vielfaches vom Lauftempo des SPIELERS (damit
    /// Tempo-Upgrades ihn nicht abhaengen): Start, Zuwachs pro Sekunde, Obergrenze.
    /// Er holt einen also sicher ein - frueh abgeben heisst, sie kommt zurueck.
    /// Durchgerechnet (10 s Lunte, 1,5 s Stun): direkt abgegeben holt er einen
    /// nach ~6 s Jagd ein, bei knapp 5 s Restlunte schafft er es nicht mehr.
    /// ChaseMax ist nur ein Notdeckel - innerhalb einer Lunte wird er nie erreicht.
    /// </summary>
    private const float ChaseStart = 0.6f, ChaseGrowth = 0.25f, ChaseMax = 4f;

    /// <summary>Kartoffel zurueckbekommen: so lange steht er erschrocken da - Zeit, Abstand aufzubauen.</summary>
    private const float Stun = 1.5f;

    /// <summary>Zwischen Boss und Spieler: so lange, bis sie wieder zurueck darf.</summary>
    private const float BackAndForthCooldown = 1.5f;

    /// <summary>Ab hier zappelt sie in Panik (Bilder 4-7).</summary>
    private const float PanicAt = 2f;

    /// <summary>
    /// So knapp vor dem Knall nimmt der Boss sie nicht mehr an - sonst waere
    /// "bis zur letzten Sekunde warten" immer richtig. Gleich PanicAt, damit
    /// das Zappeln es ankuendigt. An normale Gegner geht es weiter.
    /// </summary>
    private const float NoPassBackAt = PanicAt;

    private const float FlightTime = 0.65f;
    private const float HopTime = 0.22f;

    /// <summary>
    /// Weitergeben: Abstand Spieler - Gegnermitte bzw. Bossmitte. Bewusst etwas
    /// groesser als die Collider - die Kartoffel springt im Vorbeilaufen rueber,
    /// bevor man den Gegner wirklich beruehrt und Kontaktschaden kassiert.
    /// </summary>
    private const float PassRadius = 1.4f;
    private const float PassRadiusBoss = 2.5f;

    /// <summary>Nach dem Weitergeben kurz unverwundbar - fuer den Fall, dass man im Pulk doch jemanden streift.</summary>
    private const float PassImmunity = 0.6f;

    /// <summary>Nach dem Fangen erst kurz nicht weitergeben (sonst springt sie sofort zum Naechstbesten).</summary>
    private const float CatchCooldown = 0.35f;

    private const float BlastRadius = 2.2f;
    private const float PlayerBlastFactor = 2.4f;
    private const float EnemyBlastDamage = 400f;

    /// <summary>Anteil seiner vollen Leben, den er sich selbst wegsprengt (fest, ohne Spieler-Multiplikatoren).</summary>
    private const float SelfBlastFraction = 0.10f;

    /// <summary>Im Wurf-Streifen: bei diesem Bild verlaesst die Kartoffel die Hand (aus dem Bildskript).</summary>
    private const int ReleaseFrame = 5;

    /// <summary>Wo die Hand die Kartoffel loslaesst (Bild schaut nach rechts).</summary>
    private static readonly Vector2 HandAt = new Vector2(1.2f, 2.1f);

    /// <summary>Mitte der Kartoffel - zum Zurueckgeben und als Halteplatz.</summary>
    private static readonly Vector2 BodyCenter = new Vector2(0f, 1.0f);

    // ------------------------------------------------------------- Bausteine

    [SerializeField] private Sprite[] walk;
    [SerializeField] private Sprite[] throwing;
    [SerializeField] private Sprite[] potato;
    [SerializeField] private Sprite[] boom;

    protected override Sprite[] WalkStrip => walk;

    private enum Holder { None, Flying, Player, Enemy, Boss, Ground }

    private Holder holder;
    private Enemy holderEnemy;
    private Vector2 groundAt;
    private float fuseLeft;
    private float fuseTotal;
    private float cooldown;
    private float age;
    private int lastTick;

    // Flug/Hopser
    private Vector2 flyFrom;
    private float flyClock, flyTime;
    private Holder flyTo;
    private Holder passedBy;

    /// <summary>Wie lange der Boss die Kartoffel schon hat - davon haengt sein Tempo ab.</summary>
    private float chaseClock;
    private float stunLeft;

    private SpriteRenderer potatoR;
    private BossTelegraphMarker zone;

    private class Blast
    {
        public SpriteRenderer r;
        public float age;
    }

    private readonly List<Blast> blasts = new List<Blast>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    // ---------------------------------------------------------------- Ablauf

    protected override void Awake()
    {
        base.Awake();
        // Beruehren tut nicht weh - Schaden macht nur die Kartoffel.
        enemy.ContactAllowed = () => false;
    }

    protected override IEnumerator Run()
    {
        enemy.DamageTakenFactor = WeaponDamageTaken;
        yield return Walk(FirstWalk);
        while (true)
        {
            // Nur eine heisse Kartoffel zur Zeit
            while (holder != Holder.None) yield return Walk(0.5f);
            if (Player != null) yield return Throw();
            bool p2 = IsPhaseTwo;
            yield return Walk(p2 ? Random.Range(WalkMinPhase2, WalkMaxPhase2) : Random.Range(WalkMin, WalkMax));
        }
    }

    private IEnumerator Throw()
    {
        FacePlayer();
        yield return PlayOnce(throwing, 0, ReleaseFrame - 1);
        SquiddySounds.Whoosh();
        Launch();
        yield return PlayOnce(throwing, ReleaseFrame);
    }

    /// <summary>Mit der eigenen Kartoffel jagt er dem Spieler hinterher, immer schneller.</summary>
    protected override float SpeedFactor
    {
        get
        {
            Transform player = Player;
            if (holder != Holder.Boss || player == null || enemy.CurrentSpeed <= 0f) return 1f;
            PlayerController local = PlayerController.Instance;
            float baseSpeed = local != null ? local.moveSpeed : 4f;
            float chase = baseSpeed * Mathf.Min(ChaseMax, ChaseStart + ChaseGrowth * chaseClock);
            return Mathf.Max(1f, chase / enemy.CurrentSpeed);
        }
    }

    /// <summary>Erschrocken (eigene Kartoffel zurueckbekommen): bleibt kurz stehen, dann geht die Jagd los.</summary>
    protected override bool Paused => stunLeft > 0f;

    protected override void Update()
    {
        base.Update();
        if (stunLeft > 0f)
        {
            stunLeft -= Time.deltaTime;
            Show(throwing, 6);
        }
        UpdatePotato();
        UpdateBlasts();
    }

    // ---------------------------------------------------------------- Kartoffel

    private void Launch()
    {
        if (potatoR == null) potatoR = NewEffectRenderer("Heisse Kartoffel", 26);
        potatoR.enabled = true;
        holder = Holder.Flying;
        flyFrom = Local(HandAt);
        flyClock = 0f;
        flyTime = FlightTime;
        flyTo = Holder.Player;
        potatoPlayer = Player;
        passedBy = Holder.None;
        fuseTotal = IsPhaseTwo ? FusePhase2 : Fuse;
        fuseLeft = fuseTotal;
        age = 0f;
        lastTick = -1;
    }

    private Vector2 HolderPos(Holder h)
    {
        switch (h)
        {
            case Holder.Player:
                Transform p = PotatoPlayer;
                return p != null ? (Vector2)p.position + new Vector2(0f, 1.0f) : groundAt;
            case Holder.Enemy:
                return holderEnemy != null ? (Vector2)holderEnemy.transform.position + new Vector2(0f, 0.95f) : groundAt;
            case Holder.Boss:
                return Local(BodyCenter + new Vector2(0f, 1.6f));
            default:
                return groundAt;
        }
    }

    /// <summary>Wo die Zone liegt: am Boden unter der Kartoffel.</summary>
    private Vector2 ZoneCenter(Holder h)
    {
        switch (h)
        {
            case Holder.Player:
                Transform p = PotatoPlayer;
                return p != null ? (Vector2)p.position : groundAt;
            case Holder.Enemy:
                return holderEnemy != null ? (Vector2)holderEnemy.transform.position : groundAt;
            case Holder.Boss:
                return Local(BodyCenter);
            default:
                return groundAt;
        }
    }

    private void UpdatePotato()
    {
        if (holder == Holder.None || potatoR == null) return;
        float dt = Time.deltaTime;
        age += dt;

        if (holder == Holder.Flying)
        {
            flyClock += dt;
            float k = Mathf.Clamp01(flyClock / flyTime);
            Vector2 to = HolderPos(flyTo);
            float arc = flyTo == Holder.Player && flyTime > HopTime ? 2.6f : 1.0f;
            Vector2 p = Vector2.Lerp(flyFrom, to, k) + Vector2.up * arc * 4f * k * (1f - k);
            potatoR.transform.position = Snap(p);
            potatoR.sprite = potato[Mathf.FloorToInt(age * 10f) % 4];
            if (k >= 1f) Land(flyTo);
            return;
        }

        // Haelt jemand sie, der nicht mehr da ist? Dann bleibt sie liegen.
        if (holder == Holder.Enemy && (holderEnemy == null || !holderEnemy.gameObject.activeInHierarchy))
        {
            holder = Holder.Ground;
        }
        if (holder == Holder.Player && PotatoPlayer == null) holder = Holder.Ground;
        if (holder != Holder.Ground) groundAt = ZoneCenter(holder);

        fuseLeft -= dt;
        cooldown -= dt;
        if (holder == Holder.Boss && stunLeft <= 0f) chaseClock += dt;

        // Ticken: jede volle Sekunde
        int tick = Mathf.CeilToInt(fuseLeft);
        if (tick != lastTick)
        {
            lastTick = tick;
            SquiddySounds.Zap();
        }

        bool panic = fuseLeft <= PanicAt;
        potatoR.sprite = panic ? potato[4 + Mathf.FloorToInt(age * 14f) % 4] : potato[Mathf.FloorToInt(age * 8f) % 4];
        potatoR.transform.position = Snap(HolderPos(holder));

        if (zone != null)
        {
            zone.MoveTo(ZoneCenter(holder));
            zone.SetProgress(1f - fuseLeft / fuseTotal);
        }

        if (holder == Holder.Player && cooldown <= 0f) TryPass();
        else if (holder == Holder.Boss && cooldown <= 0f && stunLeft <= 0f) TryPassBack();

        if (fuseLeft <= 0f) Explode();
    }

    private void Land(Holder target)
    {
        holder = target;
        // Hin und her zwischen Boss und Spieler: erst nach einer Pause wieder zurueck.
        bool rally = target == Holder.Boss || passedBy == Holder.Boss;
        cooldown = rally ? BackAndForthCooldown : CatchCooldown;
        if (target == Holder.Boss)
        {
            chaseClock = 0f;
            stunLeft = Stun;
        }
        SquiddySounds.Plop();
        if (zone == null)
        {
            zone = BossTelegraph.Zone(ZoneCenter(holder), BlastRadius);
        }
    }

    /// <summary>Beruehrt der Spieler einen Gegner oder den Boss, springt sie rueber.</summary>
    private void TryPass()
    {
        Transform player = PotatoPlayer;
        if (player == null) return;
        Vector2 me = player.position;

        if (fuseLeft > NoPassBackAt && (me - Local(BodyCenter)).sqrMagnitude <= PassRadiusBoss * PassRadiusBoss)
        {
            Hop(Holder.Boss, null);
            return;
        }

        Enemy best = null;
        float bestD = PassRadius * PassRadius;
        foreach (Enemy e in Enemy.Alive)
        {
            if (e == null || e == enemy || e.IsBoss || e.Untouchable || e.IsRemote) continue;
            float d = ((Vector2)e.transform.position - me).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }
        if (best != null) Hop(Holder.Enemy, best);
    }

    /// <summary>Der Boss hat sie: holt er den Spieler ein, springt sie zurueck.</summary>
    private void TryPassBack()
    {
        // Koop: wer den Boss einholt, bekommt sie - egal welcher Spieler.
        foreach (NetPlayers.Target target in BossTargets.All())
        {
            if ((target.Position - Local(BodyCenter)).sqrMagnitude > PassRadiusBoss * PassRadiusBoss) continue;
            Hop(Holder.Player, null);
            potatoPlayer = target.Transform;
            return;
        }
    }

    // Koop: wer die Kartoffel gerade hat (oder sie gleich bekommt).
    private Transform potatoPlayer;

    private Transform PotatoPlayer
    {
        get
        {
            if (potatoPlayer == null) return null;
            if (!NetPlayers.TryGetTarget(potatoPlayer, out NetPlayers.Target t)) return null;
            if (t.IsLocal ? !NetPlayers.LocalTargetable : t.Transform.GetComponent<NetAvatar>()?.Targetable != true) return null;
            return potatoPlayer;
        }
    }

    private void Hop(Holder target, Enemy e)
    {
        SquiddySounds.Spit();
        if (holder == Holder.Player && PotatoPlayer != null && NetPlayers.TryGetTarget(PotatoPlayer, out NetPlayers.Target holderTarget)
            && holderTarget.IsLocal)
            holderTarget.Local.GrantImmunity(PassImmunity);
        passedBy = holder;
        flyFrom = HolderPos(holder);
        holderEnemy = e;
        holder = Holder.Flying;
        flyTo = target;
        flyClock = 0f;
        flyTime = HopTime;
    }

    private void Explode()
    {
        Vector2 at = ZoneCenter(holder);
        Holder was = holder;

        if (zone != null) zone.Impact();
        zone = null;
        holder = Holder.None;
        holderEnemy = null;
        stunLeft = 0f;
        if (potatoR != null) potatoR.enabled = false;

        VerkohlterSounds.Crack();
        ScreenShake.Kick(5f, 0.4f);
        SpawnBlast(at);

        // Jeder Spieler in der Explosion (im Koop auch Mitspieler).
        BossTargets.HurtInCircle(at, BlastRadius + 0.25f, enemy.ContactDamage * PlayerBlastFactor);

        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.IsBoss || e.IsRemote) continue;
            if (((Vector2)e.transform.position - at).sqrMagnitude <= BlastRadius * BlastRadius)
            {
                e.TakeDamage(EnemyBlastDamage);
            }
        }

        if (was == Holder.Boss) enemy.TakeTrueDamage(enemy.MaxHealth * SelfBlastFraction);
    }

    // ---------------------------------------------------------------- Explosion

    private void SpawnBlast(Vector2 at)
    {
        if (boom == null || boom.Length == 0) return;
        var b = new Blast { r = NewEffectRenderer("Kartoffel-Explosion", 27) };
        // Doppelt so gross, in ganzen Pixeln: 64er Bild = 4 Einheiten breit
        b.r.transform.localScale = new Vector3(2f, 2f, 1f);
        b.r.transform.position = Snap(at);
        b.r.sprite = boom[0];
        b.r.enabled = true;
        blasts.Add(b);
    }

    private void UpdateBlasts()
    {
        for (int i = blasts.Count - 1; i >= 0; i--)
        {
            Blast b = blasts[i];
            b.age += Time.deltaTime;
            int f = Mathf.FloorToInt(b.age * 16f);
            if (f >= boom.Length)
            {
                Kill(b.r);
                blasts.RemoveAt(i);
                continue;
            }
            b.r.sprite = boom[f];
        }
    }

    protected override void Cleanup()
    {
        if (zone != null) zone.Cancel();
        zone = null;
        Kill(potatoR);
        potatoR = null;
        foreach (Blast b in blasts) Kill(b.r);
        blasts.Clear();
        holder = Holder.None;
        holderEnemy = null;
        stunLeft = 0f;
    }

#if UNITY_EDITOR
    /// <summary>Nur fuer ZwischenbossBuilder.</summary>
    public void EditorBind(SpriteRenderer bodyRenderer, Sprite[] walkStrip, Sprite[] throwStrip, Sprite[] potatoStrip,
                           Sprite[] boomStrip)
    {
        body = bodyRenderer;
        walk = walkStrip;
        throwing = throwStrip;
        potato = potatoStrip;
        boom = boomStrip;
    }
#endif
}
