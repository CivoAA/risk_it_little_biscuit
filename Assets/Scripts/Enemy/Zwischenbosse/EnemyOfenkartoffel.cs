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
///             seinen Nachbarn in die Luft) - oder zurueck an die Ofenkartoffel
///             selbst, die dann erschrocken stehen bleibt und sich einen
///             ordentlichen Brocken ihrer Leben wegsprengt.
///             Bleibt sie beim Spieler, knallt es dort.
///
/// Wer das Spiel versteht, rennt also MIT der Bombe in die Horde - oder mutig
/// an den Boss heran. Ab der Haelfte: kuerzere Lunte, oefter.
/// </summary>
public class EnemyOfenkartoffel : SelfAnimatedBoss
{
    // ------------------------------------------------------------- Balancing

    private const float FirstWalk = 3.5f;
    private const float WalkMin = 4.5f, WalkMax = 6f;
    private const float WalkMinPhase2 = 3f, WalkMaxPhase2 = 4.5f;

    /// <summary>Lunte ab dem Moment, in dem sie klebt.</summary>
    private const float Fuse = 4.5f, FusePhase2 = 3.6f;

    /// <summary>Zurueck beim Boss: dann geht es schnell.</summary>
    private const float FuseOnBoss = 0.9f;

    /// <summary>Ab hier zappelt sie in Panik (Bilder 4-7).</summary>
    private const float PanicAt = 1.2f;

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

    /// <summary>Anteil seiner vollen Leben, den er sich selbst wegsprengt.</summary>
    private const float SelfBlastFraction = 0.08f;

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

    private SpriteRenderer potatoR;
    private BossTelegraphMarker zone;
    private bool scared;

    private class Blast
    {
        public SpriteRenderer r;
        public float age;
    }

    private readonly List<Blast> blasts = new List<Blast>();
    private readonly List<Enemy> scratch = new List<Enemy>();

    // ---------------------------------------------------------------- Ablauf

    protected override IEnumerator Run()
    {
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

    /// <summary>Erschrocken (eigene Kartoffel zurueckbekommen): bleibt stehen, bis es knallt.</summary>
    protected override bool Paused => scared;

    protected override void Update()
    {
        base.Update();
        if (scared) Show(throwing, 6);
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
                PlayerController p = Player;
                return p != null ? (Vector2)p.transform.position + new Vector2(0f, 1.0f) : groundAt;
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
                PlayerController p = Player;
                return p != null ? (Vector2)p.transform.position : groundAt;
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
        if (holder == Holder.Player && Player == null) holder = Holder.Ground;
        if (holder != Holder.Ground) groundAt = ZoneCenter(holder);

        fuseLeft -= dt;
        cooldown -= dt;

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

        if (fuseLeft <= 0f) Explode();
    }

    private void Land(Holder target)
    {
        holder = target;
        cooldown = CatchCooldown;
        SquiddySounds.Plop();
        if (target == Holder.Boss)
        {
            fuseLeft = Mathf.Min(fuseLeft, FuseOnBoss);
            scared = true;
        }
        if (zone == null)
        {
            zone = BossTelegraph.Zone(ZoneCenter(holder), BlastRadius);
        }
    }

    /// <summary>Beruehrt der Spieler einen Gegner oder den Boss, springt sie rueber.</summary>
    private void TryPass()
    {
        PlayerController player = Player;
        if (player == null) return;
        Vector2 me = player.transform.position;

        if ((me - Local(BodyCenter)).sqrMagnitude <= PassRadiusBoss * PassRadiusBoss)
        {
            Hop(Holder.Boss, null);
            return;
        }

        Enemy best = null;
        float bestD = PassRadius * PassRadius;
        foreach (Enemy e in Enemy.Alive)
        {
            if (e == null || e == enemy || e.IsBoss || e.Untouchable) continue;
            float d = ((Vector2)e.transform.position - me).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }
        if (best != null) Hop(Holder.Enemy, best);
    }

    private void Hop(Holder target, Enemy e)
    {
        SquiddySounds.Spit();
        if (holder == Holder.Player && Player != null) Player.GrantImmunity(PassImmunity);
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
        scared = false;
        if (potatoR != null) potatoR.enabled = false;

        VerkohlterSounds.Crack();
        ScreenShake.Kick(5f, 0.4f);
        SpawnBlast(at);

        PlayerController player = Player;
        if (player != null)
        {
            float reach = BlastRadius + 0.25f;
            if (((Vector2)player.transform.position - at).sqrMagnitude <= reach * reach)
            {
                player.TakeDamage(enemy.ContactDamage * PlayerBlastFactor);
            }
        }

        scratch.Clear();
        scratch.AddRange(Enemy.Alive);
        foreach (Enemy e in scratch)
        {
            if (e == null || e == enemy || e.IsBoss) continue;
            if (((Vector2)e.transform.position - at).sqrMagnitude <= BlastRadius * BlastRadius)
            {
                e.TakeDamage(EnemyBlastDamage);
            }
        }

        if (was == Holder.Boss || ((Vector2)transform.position + BodyCenter - at).sqrMagnitude <= BlastRadius * BlastRadius)
        {
            enemy.TakeDamage(enemy.MaxHealth * SelfBlastFraction);
        }
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
        scared = false;
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
