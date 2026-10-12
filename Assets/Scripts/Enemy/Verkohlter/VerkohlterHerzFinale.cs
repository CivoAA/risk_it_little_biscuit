using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Endkampf gegen das Herz des Verkohlten - das Finale des Spiels.
/// Startet, sobald der Spieler in der Herzkammer gelandet ist
/// (<see cref="EnemyVerkohlter"/>, Phase 3), und laeuft bis zum Herz-Tod.
///
/// Alles schlaegt im Takt: das Herz feuert auf dem BUM (Musik "Herz der
/// Glut", 90 BPM, Herzschlag auf 1 und 3) und auf dem bumm danach - wer den
/// Herzschlag hoert, weiss, wann der naechste Ring kommt.
///
///   Phase I   "Glutschlag" (100 % - 70 %)
///             BUM: Funkenring. Dazwischen im Wechsel:
///             Glutsalve (gezielte Kugeln auf dem bumm) und Lavaregen (das
///             Becken spuckt Glut auf markierte Stellen um den Spieler).
///   Panzer 1  bei 70 %: das Herz panzert sich (nicht zu treffen) und heilt
///             sich ueber drei Schlagadern am Rand der Kammer (<see cref="HerzAder"/>).
///             Wer sie zerstoert, sprengt den Panzer - dabei dreht sich eine
///             Glutspirale. Danach liegt das Herz ein paar Sekunden offen
///             (mehr Schaden).
///   Phase II  (70 % - 36 %): dichtere Ringe; dazu Glutstrahl (ein Strahl
///             dreht sich durch die Kammer, man muss mitlaufen oder einmal
///             durch), Glutlinge (fangen Schuesse ab) und mehr Lavaregen.
///   Panzer 2  bei 36 %: vier Schlagadern, dreiarmige Spirale.
///   Phase III "Das Herz rast" (36 % - 0): die Feuerwand (<see cref="HerzFeuerwand"/>)
///             schliesst die Kammer enger, Ringe auf BUM und bumm,
///             Doppelstrahl und Herzinfarkt (Sog zum Herzen, dann Explosion
///             mit Riesenring).
///
/// Das Herz kann eine Phase nicht ueberspringen: <see cref="Enemy.MinHealthFraction"/>
/// haelt es an der naechsten Panzergrenze fest. Die Zahlen stehen als
/// Konstanten oben (Faktoren auf den Beruehrungsschaden des Verkohlten).
///
/// Testen: F10 im Lauf/Test-Szene setzt den Verkohlten auf 10 Leben (er
/// saugt sofort ein); F10 in der Kammer springt zur naechsten Phase.
/// </summary>
public class VerkohlterHerzFinale : MonoBehaviour
{
    // ------------------------------------------------------------- Balancing

    private const float ArmorOneAt = 0.70f;
    private const float ArmorTwoAt = 0.36f;

    // --- Schaden (mal Beruehrungsschaden des Verkohlten)
    private const float FunkeFactor = 0.16f;
    private const float KugelFactor = 0.32f;
    private const float LavaFactor = 0.5f;
    private const float LavaPatchFactor = 0.16f;
    private const float BeamFactor = 0.42f;
    private const float InfarktFactor = 0.9f;
    private const float WallFactor = 0.2f;
    private const float ArteryContactFactor = 0.2f;
    private const float LingContactFactor = 0.25f;

    // --- Ringe (Anzahl, Tempo) je Phase
    private const int RingOne = 16, RingTwo = 20, RingThree = 22, RingThreeDub = 12, RingArmor = 12;
    private const float RingSpeedOne = 3.0f, RingSpeedTwo = 3.3f, RingSpeedThree = 3.6f, RingSpeedArmor = 2.7f;
    private const float RingStart = 1.0f;     // Abstand zur Herzmitte, wo die Geschosse entstehen

    // --- Glutsalve
    private const float SalvoSpeed = 5.4f;
    private const float SalvoLead = 0.45f;   // Anteil Vorhalt auf die Laufrichtung

    // --- Lavaregen
    private const float LavaRadius = 0.85f;
    private const float LavaFlight = 1.15f;
    private const float LavaArc = 3.6f;
    private const float LavaPatchTime = 2.6f;
    /// <summary>Halbachsen der Lavapfuetze (herz_lavafleck, 41x23 px) - Schaden genau dort, wo sie zu sehen ist.</summary>
    private static readonly Vector2 LavaPatchSize = new Vector2(20.5f, 11.5f) / VerkohlterArt.PixelsPerUnit;

    // --- Glutstrahl
    private const float BeamCharge = 1.25f;
    private const float BeamLength = 17f;
    private const float BeamCorePx = 9f;
    private const float BeamSweepTwo = 200f, BeamTurnTwo = 38f;
    private const float BeamSweepThree = 180f, BeamTurnThree = 40f;

    // --- Glutlinge
    private const float LingHealthShare = 0.012f;   // je Glutling, Anteil am Herzpool
    private const float LingSpeed = 2.6f, LingSpeedRage = 3.0f;

    // --- Panzer + Schlagadern
    private const float ArteryHealthShare = 0.07f;  // je Ader, Anteil am Herzpool
    private const float ArterySpitFactor = 0.14f;
    private const float HealPerPump = 0.011f;       // je Ader und BUM, Anteil am Herzpool
    private const float HealCap = 0.12f;            // so weit ueber die Panzergrenze heilt es hoechstens
    private const float ArteryWarn = 1.0f;
    private const float ExposedTime = 5f;
    private const float ExposedDamage = 1.6f;
    private const float SpiralEvery = 0.12f, SpiralEveryRage = 0.1f;
    private const float SpiralSpeed = 2.6f;
    private const float SpiralTurn = 72f;

    // --- Herzinfarkt
    private const float InfarktTime = 2.4f;
    private const float InfarktRadius = 3.4f;
    private const float InfarktPull = 2.4f;

    // --- Feuerwand
    private const float WallScale = 0.64f;

    private const float Fps = 12f;

    // ---------------------------------------------------------------- Zustand

    public static VerkohlterHerzFinale Active { get; private set; }

    private EnemyVerkohlter owner;
    private Enemy enemy;
    private VerkohlterHerzkammer chamber;
    private HerzGeschosse shots;
    private Sprite[] patch;
    private Sprite[] heartNormal, heartArmor, armorClose, armorBreak, heartRage, beamFrames;

    private int stage;
    private bool rings, spiral, ended;
    private int lubs, dubs;
    private float ringTurn;
    private float spiralAngle, spiralDir = 1f, spiralTimer;
    private int spiralArms = 2;
    private float pull;
    private float heartCap = 1f;
    private bool specialRunning;
    private Rigidbody2D playerBody;

    private readonly List<HerzAder> arteries = new List<HerzAder>();
    private readonly List<HerzGlutling> lings = new List<HerzGlutling>();
    private readonly List<FlameBeam> beams = new List<FlameBeam>();
    private readonly List<BossTelegraphMarker> markers = new List<BossTelegraphMarker>();
    private readonly List<GameObject> temp = new List<GameObject>();
    private HerzFeuerwand wall;

    private float Pool => enemy != null ? enemy.MaxHealth : 1f;
    private float Contact => enemy != null ? enemy.ContactDamage : 20f;
    private Vector2 Heart => chamber != null ? chamber.HeartPosition : (Vector2)transform.position;

    /// <summary>Bild des Herzschlags (0..15) - die Schlagadern pochen mit.</summary>
    public int BeatFrame => chamber != null ? chamber.Frame : 0;

    // ------------------------------------------------------------------ Start

    public static VerkohlterHerzFinale Begin(EnemyVerkohlter owner, Enemy enemy, VerkohlterHerzkammer chamber,
                                             Sprite[] patch, Sprite[] heartNormal)
    {
        var f = owner.gameObject.AddComponent<VerkohlterHerzFinale>();
        f.owner = owner;
        f.enemy = enemy;
        f.chamber = chamber;
        f.patch = patch;
        f.heartNormal = heartNormal;
        f.heartArmor = VerkohlterArt.Strip("verkohlter_herz_panzer");
        f.armorClose = VerkohlterArt.Strip("verkohlter_herz_panzer_zu");
        f.armorBreak = VerkohlterArt.Strip("verkohlter_herz_bruch");
        f.heartRage = VerkohlterArt.Strip("verkohlter_herz_wut");
        f.beamFrames = VerkohlterArt.Strip("herz_strahl");
        f.shots = HerzGeschosse.Create(chamber);
        chamber.Lub += f.OnLub;
        chamber.Dub += f.OnDub;
        Active = f;
        f.StartCoroutine(f.Run());
        return f;
    }

    private void OnDestroy()
    {
        if (chamber != null)
        {
            chamber.Lub -= OnLub;
            chamber.Dub -= OnDub;
        }
        Cleanup();
        if (shots != null) Destroy(shots.gameObject);
        if (wall != null) Destroy(wall.gameObject);
        if (Active == this) Active = null;
    }

    /// <summary>Das Herz ist tot: alles weg, was noch fliegt oder steht.</summary>
    public void End()
    {
        if (ended) return;
        ended = true;
        StopAllCoroutines();
        Cleanup();
        if (shots != null) shots.PopAll();
        if (wall != null) wall.Extinguish();
        if (enemy != null) enemy.DamageTakenFactor = 1f;
        owner.HeartOverride = null;
    }

    private void Cleanup()
    {
        rings = false;
        spiral = false;
        pull = 0f;
        VerkohlterSounds.StopLoop();
        foreach (HerzAder a in arteries) if (a != null) a.Remove();
        arteries.Clear();
        foreach (HerzGlutling g in lings) if (g != null) g.Remove();
        lings.Clear();
        StopBeams(true);
        foreach (BossTelegraphMarker m in markers) if (m != null) m.Cancel();
        markers.Clear();
        foreach (GameObject go in temp) if (go != null) Destroy(go);
        temp.Clear();
    }

    // ---------------------------------------------------------------- Ablauf

    private IEnumerator Run()
    {
        enemy.MinHealthFraction = ArmorOneAt;
        yield return WaitLub();
        stage = 1;
        int step = 0;
        while (true)
        {
            if (!PlayerAlive)
            {
                rings = false;
                spiral = false;
                yield return null;
                continue;
            }
            rings = true;

            if (stage == 1 && Due(ArmorOneAt))
            {
                yield return Armor(3, ArmorOneAt, false);
                stage = 2;
                step = 0;
                continue;
            }
            if (stage == 2 && Due(ArmorTwoAt))
            {
                yield return Armor(4, ArmorTwoAt, true);
                yield return Rage();
                stage = 3;
                step = 0;
                continue;
            }

            yield return Special(step++);
            int pause = stage == 3 ? 1 : 2;
            for (int i = 0; i < pause && !StageDue(); i++) yield return WaitLub();
        }
    }

    private bool Due(float at) => enemy != null && enemy.HealthFraction <= at + 0.0005f;

    private bool StageDue() => (stage == 1 && Due(ArmorOneAt)) || (stage == 2 && Due(ArmorTwoAt));

    private IEnumerator Special(int step)
    {
        IEnumerator attack;
        switch (stage)
        {
            case 1:
                attack = step % 2 == 0 ? Salvo(3, 3, 14f) : LavaRain(6);
                break;
            case 2:
                switch (step % 4)
                {
                    case 0: attack = Beam(1, BeamSweepTwo, BeamTurnTwo); break;
                    case 1: attack = Salvo(3, 5, 12f); break;
                    case 2: attack = Lings(3, LingSpeed); break;
                    default: attack = LavaRain(8); break;
                }
                break;
            default:
                switch (step % 4)
                {
                    case 0: attack = Beam(2, BeamSweepThree, BeamTurnThree); break;
                    case 1: attack = Salvo(4, 5, 12f); break;
                    case 2: attack = Infarkt(); break;
                    default: attack = LavaAndLings(); break;
                }
                break;
        }

        specialRunning = true;
        Coroutine c = StartCoroutine(Wrap(attack));
        while (specialRunning)
        {
            if (StageDue() || !PlayerAlive)
            {
                StopCoroutine(c);
                AbortSpecial();
                break;
            }
            yield return null;
        }
    }

    private IEnumerator Wrap(IEnumerator e)
    {
        yield return e;
        specialRunning = false;
    }

    /// <summary>Eine Attacke bricht ab (Panzergrenze erreicht, Spieler tot): Strahl aus, Sog aus.</summary>
    private void AbortSpecial()
    {
        specialRunning = false;
        pull = 0f;
        StopBeams(false);
        VerkohlterSounds.StopLoop();
        VerkohlterSounds.StopAll();
        rings = true;
    }

    // ----------------------------------------------------------------- Takt

    private IEnumerator WaitLub()
    {
        int n = lubs;
        float t = 0f;
        while (lubs == n && t < 2f)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitDub()
    {
        int n = dubs;
        float t = 0f;
        while (dubs == n && t < 2f)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void OnLub()
    {
        lubs++;
        if (ended) return;

        // Schlagadern pumpen Glut ins Herz
        bool any = false;
        foreach (HerzAder a in arteries)
        {
            if (a == null || !a.Alive) continue;
            a.Pump();
            enemy.Heal(Pool * HealPerPump, heartCap);
            any = true;
        }
        if (any) StartCoroutine(HealGlow());

        if (!rings || !PlayerAlive) return;
        ringTurn += 360f / 32f * (stage % 2 == 0 ? -1f : 1f);
        int count; float speed;
        if (spiral) { count = RingArmor; speed = RingSpeedArmor; }
        else if (stage <= 1) { count = RingOne; speed = RingSpeedOne; }
        else if (stage == 2) { count = RingTwo; speed = RingSpeedTwo; }
        else { count = RingThree; speed = RingSpeedThree; }
        shots.Ring(Heart, RingStart, count, speed, ringTurn, HerzGeschosse.Art.Funke, Contact * FunkeFactor);
        VerkohlterSounds.Shot();
    }

    private void OnDub()
    {
        dubs++;
        if (ended) return;
        foreach (HerzAder a in arteries) if (a != null && a.Alive) a.Spit(shots);

        if (!rings || !PlayerAlive || stage < 3 || spiral) return;
        // Phase III: auch das bumm schiesst - langsamer Ring, versetzt
        shots.Ring(Heart, RingStart, RingThreeDub, 2.5f, ringTurn + 180f / RingThreeDub, HerzGeschosse.Art.Funke,
                   Contact * FunkeFactor, accel: 0.6f);
    }

    private void Update()
    {
        if (ended || !spiral || !PlayerAlive) return;
        spiralAngle += SpiralTurn * spiralDir * Time.deltaTime;
        spiralTimer -= Time.deltaTime;
        float every = stage >= 2 ? SpiralEveryRage : SpiralEvery;
        while (spiralTimer <= 0f)
        {
            spiralTimer += every;
            for (int k = 0; k < spiralArms; k++)
            {
                Vector2 d = HerzGeschosse.Dir(spiralAngle + k * 360f / spiralArms);
                shots.Fire(Heart + d * RingStart, d * SpiralSpeed, HerzGeschosse.Art.Funke, Contact * FunkeFactor);
            }
        }
    }

    private void FixedUpdate()
    {
        if (pull <= 0f || !PlayerAlive) return;
        PlayerController pc = PlayerController.Instance;
        Vector2 to = Heart - (Vector2)pc.transform.position;
        if (to.magnitude < 0.6f) return;
        pc.AddDrift(to.normalized * pull);
    }

    // ============================================================ Attacken

    /// <summary>Glutsalve: auf jedem bumm ein Faecher Glutkugeln auf den Spieler (mit Vorhalt).</summary>
    private IEnumerator Salvo(int salvos, int balls, float spread)
    {
        for (int s = 0; s < salvos; s++)
        {
            yield return WaitDub();
            if (!PlayerAlive) yield break;
            Vector2 from = Heart;
            Vector2 target = PlayerPos;
            Rigidbody2D prb = PlayerBody;
            if (prb != null)
            {
                float time = Vector2.Distance(from, target) / SalvoSpeed;
                target += prb.linearVelocity * time * SalvoLead;
            }
            Vector2 to = target - from;
            float a = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            for (int k = 0; k < balls; k++)
            {
                float off = (k - (balls - 1) * 0.5f) * spread;
                Vector2 d = HerzGeschosse.Dir(a + off);
                shots.Fire(from + d * RingStart, d * SalvoSpeed, HerzGeschosse.Art.Kugel, Contact * KugelFactor);
            }
            // Phase III: dazu ein langsamer Kugelkranz
            if (stage >= 3 && s % 2 == 1)
                shots.Ring(from, RingStart, 8, 2.0f, a + 22.5f, HerzGeschosse.Art.Kugel, Contact * KugelFactor, accel: 1.2f);
            VerkohlterSounds.Fireball();
            Burst(from, 6);
        }
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>Lavaregen: das Becken spuckt Glut auf markierte Stellen um den Spieler.</summary>
    private IEnumerator LavaRain(int count)
    {
        yield return WaitLub();
        if (!PlayerAlive) yield break;
        VerkohlterSounds.Erupt();
        Boom(3f, 0.3f);
        Vector2 pool = chamber.PoolMid;
        Rigidbody2D prb = PlayerBody;
        Vector2 player = PlayerPos;
        Vector2 lead = prb != null ? prb.linearVelocity * LavaFlight * 0.6f : Vector2.zero;
        for (int k = 0; k < count; k++)
        {
            Vector2 target = player + lead;
            if (k > 0)
            {
                bool ok = false;
                for (int tries = 0; tries < 8 && !ok; tries++)
                {
                    target = player + Random.insideUnitCircle.normalized * Random.Range(1.3f, 4.2f);
                    ok = chamber.OnFloor(target, 0.93f);
                }
                if (!ok) continue;
            }
            else if (!chamber.OnFloor(target, 0.93f))
            {
                target = player;
            }
            Vector2 from = pool + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-0.2f, 0.3f));
            StartCoroutine(LavaBlob(from, target, k * 0.08f));
            for (int s = 0; s < 3; s++) shots.AddSpark(from, new Vector2(Random.Range(-1f, 1f), Random.Range(2f, 4f)), 0.5f);
        }
        yield return new WaitForSeconds(LavaFlight + count * 0.08f + 0.2f);
    }

    private IEnumerator LavaBlob(Vector2 from, Vector2 to, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        BossTelegraphMarker zone = Track(BossTelegraph.Zone(to, LavaRadius));
        SpriteRenderer sr = NewSprite("Lavaklumpen", 22);
        Sprite[] kugel = VerkohlterArt.Strip("herz_glutkugel");
        float t = 0f;
        while (t < LavaFlight)
        {
            t += Time.deltaTime;
            float q = Mathf.Clamp01(t / LavaFlight);
            Vector2 p = Vector2.Lerp(from, to, q) + Vector2.up * (4f * q * (1f - q) * LavaArc);
            sr.transform.position = VerkohlterArt.Snap(p);
            if (kugel.Length > 0) sr.sprite = kugel[Mathf.FloorToInt(t * 14f) % kugel.Length];
            if (zone != null) zone.SetProgress(q);
            yield return null;
        }
        if (zone != null) zone.Impact();
        Untrack(zone);
        Kill(sr.gameObject);
        if (Inside(to, LavaRadius)) PlayerController.Instance.TakeDamage(Contact * LavaFactor);
        Burst(to, 10);
        ScreenShake.Kick(1.5f, 0.12f);

        Sprite[] puddle = VerkohlterArt.Strip("herz_lavafleck");
        if (puddle.Length == 0) puddle = patch;
        if (puddle == null || puddle.Length == 0) yield break;
        SpriteRenderer fire = NewSprite("Lavafleck", -1);
        fire.transform.position = VerkohlterArt.Snap(to);
        float life = 0f, tick = 0f;
        while (life < LavaPatchTime)
        {
            life += Time.deltaTime;
            tick -= Time.deltaTime;
            fire.sprite = puddle[Mathf.FloorToInt(life * Fps) % puddle.Length];
            // Kurz vor dem Erloeschen blinkt sie - dann weiss man, dass man gleich durch darf.
            float left = LavaPatchTime - life;
            bool blink = left < 0.6f && Mathf.Repeat(left * 8f, 1f) < 0.4f;
            Color c = Color.white;
            c.a = blink ? 0.45f : Mathf.Clamp01(left / 0.15f);
            fire.color = c;
            if (Random.value < 0.15f)
                shots.AddSpark(to + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f)),
                               Vector2.up * Random.Range(1f, 2f), 0.4f);
            if (tick <= 0f && InsideEllipse(to, LavaPatchSize))
            {
                tick = 0.5f;
                PlayerController.Instance.TakeDamage(Contact * LavaPatchFactor);
            }
            yield return null;
        }
        Kill(fire.gameObject);
    }

    /// <summary>
    /// Glutstrahl: das Herz laedt (rote Bahn zeigt, wo er anfaengt), dann
    /// brennt ein Strahl los und dreht sich durch die Kammer. Mitlaufen oder
    /// einmal durch (kostet einen Treffer).
    /// </summary>
    private IEnumerator Beam(int count, float sweep, float turn)
    {
        rings = false;
        yield return WaitLub();
        if (!PlayerAlive) yield break;

        Vector2 to = PlayerPos - Heart;
        float a0 = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        float dir = Random.value < 0.5f ? -1f : 1f;

        // Aufladen
        var paths = new List<BossTelegraphMarker>();
        for (int k = 0; k < count; k++)
        {
            float a = a0 + k * 360f / count;
            paths.Add(Track(BossTelegraph.Path(Heart, Heart + HerzGeschosse.Dir(a) * BeamLength, 1.0f)));
        }
        VerkohlterSounds.BeamCharge();
        float t = 0f;
        while (t < BeamCharge)
        {
            t += Time.deltaTime;
            foreach (BossTelegraphMarker p in paths) if (p != null) p.SetProgress(t / BeamCharge);
            // Glut stroemt zur Herzmitte
            if (Random.value < 0.7f)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(2.2f, 3.4f);
                shots.AddSpark(Heart + off, -off * 2.2f, 0.4f);
            }
            ScreenShake.Kick(1f + t, 0.1f);
            yield return null;
        }
        foreach (BossTelegraphMarker p in paths)
        {
            if (p != null) p.Cancel();
            Untrack(p);
        }

        // Feuer
        VerkohlterSounds.Beam();
        Boom(5f, 0.4f);
        for (int k = 0; k < count; k++) beams.Add(NewBeam(a0 + k * 360f / count));
        float duration = sweep / turn;
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = a0 + dir * turn * t;
            for (int k = 0; k < beams.Count; k++)
            {
                FlameBeam b = beams[k];
                if (b == null) continue;
                b.transform.position = Heart;
                b.transform.rotation = Quaternion.Euler(0f, 0f, a + k * 360f / beams.Count);
                if (PlayerAlive && b.Hits(PlayerPos, 0.2f))
                    PlayerController.Instance.TakeDamage(Contact * BeamFactor);
            }
            ScreenShake.Kick(1.5f, 0.1f);
            yield return null;
        }
        StopBeams(false);
        VerkohlterSounds.StopLoop();
        yield return new WaitForSeconds(0.45f);
        rings = true;
    }

    private FlameBeam NewBeam(float angle)
    {
        var go = new GameObject("Glutstrahl");
        go.SetActive(false);
        RunScene.Place(go, "Effekte");
        go.transform.position = Heart;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        FlameBeam b = go.AddComponent<FlameBeam>();
        Sprite[] f = beamFrames;
        var start = new Sprite[8];
        var mid = new Sprite[8];
        var end = new Sprite[8];
        for (int i = 0; i < 8; i++)
        {
            start[i] = VerkohlterArt.Frame(f, i);
            mid[i] = VerkohlterArt.Frame(f, 8 + i);
            end[i] = VerkohlterArt.Frame(f, 16 + i);
        }
        b.Bind(start, mid, end, BeamCorePx);
        b.Length = BeamLength;
        b.GrowTime = 0.3f;
        b.SetSorting("Objects", -2);   // unter dem Herzen (Ordnung 1): der Strahl kommt aus ihm heraus
        go.SetActive(true);
        temp.Add(go);
        return b;
    }

    private void StopBeams(bool hard)
    {
        foreach (FlameBeam b in beams)
        {
            if (b == null) continue;
            if (hard) Destroy(b.gameObject);
            else b.Stop();
        }
        beams.Clear();
    }

    /// <summary>Glutlinge kriechen aus dem Becken und jagen den Spieler.</summary>
    private IEnumerator Lings(int count, float speed)
    {
        yield return WaitLub();
        if (!PlayerAlive) yield break;
        lings.RemoveAll(g => g == null);
        VerkohlterSounds.Erupt();
        Boom(2f, 0.2f);
        Vector2 pool = chamber.PoolMid;
        for (int k = 0; k < count; k++)
        {
            float a = 200f + k * 140f / Mathf.Max(1, count - 1) + Random.Range(-10f, 10f);
            Vector2 land = pool + Vector2.Scale(HerzGeschosse.Dir(a), new Vector2(3.4f, 1.9f));
            Vector2 from = pool + new Vector2(Random.Range(-0.8f, 0.8f), 0.1f);
            lings.Add(HerzGlutling.Spawn(from, land, Pool * LingHealthShare, Contact * LingContactFactor, speed, shots));
        }
        yield return new WaitForSeconds(0.8f);
    }

    private IEnumerator LavaAndLings()
    {
        StartCoroutine(Lings(2, LingSpeedRage));
        yield return LavaRain(10);
    }

    /// <summary>
    /// Herzinfarkt: das Herz zieht sich zusammen und saugt den Spieler an,
    /// die Zone um das Herz fuellt sich - voll = Explosion und ein Riesenring.
    /// </summary>
    private IEnumerator Infarkt()
    {
        rings = false;
        yield return WaitLub();
        if (!PlayerAlive) yield break;
        BossTelegraphMarker zone = Track(BossTelegraph.Zone(Heart, InfarktRadius));
        VerkohlterSounds.Suck();
        float t = 0f;
        while (t < InfarktTime)
        {
            t += Time.deltaTime;
            float q = Mathf.Clamp01(t / InfarktTime);
            pull = InfarktPull * Mathf.Clamp01(q * 1.6f);
            if (zone != null) zone.SetProgress(q);
            // Sog: Glut aus der ganzen Kammer stroemt zum Herzen
            for (int k = 0; k < 2; k++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(4f, 8f);
                off.y *= 0.6f;
                shots.AddSpark(Heart + off, -off * 2.6f, 0.38f);
            }
            ScreenShake.Kick(1f + 3f * q, 0.1f);
            yield return null;
        }
        pull = 0f;
        VerkohlterSounds.StopAll();
        VerkohlterSounds.Crack();
        if (zone != null) zone.Impact(0.4f);
        Untrack(zone);
        Boom(8f, 0.6f);
        StartCoroutine(Flash(0.45f));
        if (Inside(Heart, InfarktRadius)) PlayerController.Instance.TakeDamage(Contact * InfarktFactor);
        shots.Ring(Heart, RingStart, 30, 4.2f, ringTurn, HerzGeschosse.Art.Funke, Contact * FunkeFactor);
        shots.Ring(Heart, RingStart, 12, 2.4f, ringTurn + 15f, HerzGeschosse.Art.Kugel, Contact * KugelFactor, accel: 0.8f);
        Burst(Heart, 30);
        yield return new WaitForSeconds(0.9f);
        rings = true;
    }

    // ============================================================== Panzer

    private IEnumerator Armor(int count, float at, bool second)
    {
        rings = false;
        spiral = false;
        yield return WaitLub();

        // 1. Der Panzer waechst zu
        Say("boss.verkohlter.armor", "DAS HERZ PANZERT SICH!");
        owner.ShieldHeart(true);
        VerkohlterSounds.Armor();
        Boom(4f, 0.5f);
        for (int i = 0; i < armorClose.Length; i++)
        {
            owner.HeartOverride = armorClose[i];
            yield return new WaitForSeconds(1f / Fps);
        }
        owner.SetHeartStrip(heartArmor);
        owner.HeartOverride = null;
        yield return new WaitForSeconds(0.4f);

        // 2. Schlagadern brechen am Rand aus dem Boden
        Say("boss.verkohlter.arteries", "VERNICHTE DIE SCHLAGADERN!");
        List<Vector2> spots = ArterySpots(count);
        var zones = new List<BossTelegraphMarker>();
        foreach (Vector2 s in spots) zones.Add(Track(BossTelegraph.Zone(s + Vector2.up * 0.4f, 1.1f)));
        float t = 0f;
        while (t < ArteryWarn)
        {
            t += Time.deltaTime;
            foreach (BossTelegraphMarker z in zones) if (z != null) z.SetProgress(t / ArteryWarn);
            yield return null;
        }
        heartCap = Mathf.Min(1f, at + HealCap);
        foreach (BossTelegraphMarker z in zones)
        {
            if (z != null) z.Impact();
            Untrack(z);
        }
        foreach (Vector2 s in spots)
        {
            if (Inside(s + Vector2.up * 0.4f, 1.1f)) PlayerController.Instance.TakeDamage(Contact * LavaFactor);
            arteries.Add(HerzAder.Spawn(this, s, Heart, Pool * ArteryHealthShare, Contact * ArteryContactFactor,
                                        Contact * ArterySpitFactor));
        }
        VerkohlterSounds.Erupt();
        Boom(5f, 0.5f);

        // 3. Spirale, bis alle Adern geplatzt sind
        spiralArms = second ? 3 : 2;
        spiralDir = -spiralDir;
        spiralTimer = 0.4f;
        rings = true;
        spiral = true;
        while (AnyArteryAlive()) yield return null;
        spiral = false;
        rings = false;
        yield return new WaitForSeconds(0.5f);

        // 4. Der Panzer platzt
        Say("boss.verkohlter.exposed", "DAS HERZ LIEGT OFFEN!");
        VerkohlterSounds.Crack();
        Boom(8f, 0.7f);
        for (int i = 0; i < armorBreak.Length; i++)
        {
            owner.HeartOverride = armorBreak[i];
            if (i == 3)
            {
                StartCoroutine(Flash(0.6f));
                shots.PopAll();     // der Blitz fegt die Kammer frei - Luft zum Draufhauen
                Burst(Heart, 40);
            }
            yield return new WaitForSeconds(1f / Fps);
        }
        owner.SetHeartStrip(second ? heartRage : heartNormal);
        owner.HeartOverride = null;
        enemy.MinHealthFraction = second ? 0f : ArmorTwoAt;
        owner.ShieldHeart(false);

        // 5. Offen: ein paar Sekunden Ruhe und mehr Schaden
        enemy.DamageTakenFactor = ExposedDamage;
        t = 0f;
        while (t < ExposedTime)
        {
            t += Time.deltaTime;
            if (Random.value < 0.5f)
                shots.AddSpark(Heart + Random.insideUnitCircle * 1.2f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1f, 2.5f)), 0.6f);
            yield return null;
        }
        enemy.DamageTakenFactor = 1f;
        arteries.Clear();
    }

    private bool AnyArteryAlive()
    {
        foreach (HerzAder a in arteries) if (a != null && a.Alive) return true;
        return false;
    }

    /// <summary>Gleichmaessig verteilt am Rand des Bodens, nicht auf dem Spieler.</summary>
    private List<Vector2> ArterySpots(int count)
    {
        var list = new List<Vector2>(count);
        float baseAngle = Random.Range(0f, 360f);
        Vector2 r = VerkohlterArt.FloorRadius;
        for (int k = 0; k < count; k++)
        {
            float a = baseAngle + k * 360f / count + Random.Range(-12f, 12f);
            Vector2 p = Vector2.zero;
            for (int tries = 0; tries < 6; tries++)
            {
                Vector2 d = HerzGeschosse.Dir(a);
                p = chamber.FloorMid + new Vector2(d.x * r.x * 0.72f, d.y * r.y * 0.62f);
                if (chamber.OnFloor(p, 0.9f) && (!PlayerAlive || Vector2.Distance(p, PlayerPos) > 1.6f)) break;
                a += 25f;
            }
            list.Add(VerkohlterArt.Snap(p));
        }
        return list;
    }

    /// <summary>Wird von einer platzenden Ader gemeldet: Glut spritzt.</summary>
    public void OnArteryBurst(HerzAder a)
    {
        if (a == null) return;
        Burst((Vector2)a.transform.position + Vector2.up * 1.35f, 16);
    }

    // ============================================================ Phase III

    private IEnumerator Rage()
    {
        Say("boss.verkohlter.rage", "DAS HERZ RAST!");
        owner.SetHeartStrip(heartRage);
        Boom(6f, 0.8f);
        wall = HerzFeuerwand.Create(chamber, WallScale, Contact * WallFactor);
        yield return WaitLub();
        yield return WaitLub();
    }

    // ============================================================== Kleinkram

    /// <summary>Testtaste: naechste Phase (Adern platzen lassen bzw. Leben auf die naechste Grenze).</summary>
    public void DebugNext()
    {
        if (AnyArteryAlive())
        {
            foreach (HerzAder a in arteries)
                if (a != null && a.Alive && a.Enemy != null) a.Enemy.TakeDamage(a.Enemy.MaxHealth * 100f, null, 0f);
            return;
        }
        if (stage == 1) enemy.DebugSetHealthFraction(ArmorOneAt);
        else if (stage == 2) enemy.DebugSetHealthFraction(ArmorTwoAt);
        else enemy.DebugSetHealthFraction(0.03f);
        Debug.Log("[Herz] Testtaste: naechste Phase (Stufe " + stage + ").");
    }

    private IEnumerator HealGlow()
    {
        // Glut sammelt sich am Herzen, wenn eine Ader pumpt (kommt kurz nach dem BUM an)
        yield return new WaitForSeconds(0.3f);
        for (int k = 0; k < 6; k++)
            shots.AddSpark(Heart + Random.insideUnitCircle * 1.1f, Vector2.up * Random.Range(0.5f, 1.5f), 0.5f);
    }

    private IEnumerator Flash(float alpha)
    {
        ScreenIris iris = ScreenIris.Create();
        iris.Open();
        temp.Add(iris.gameObject);
        Color c = new Color(1f, 0.95f, 0.82f, 1f);
        float t = 0f;
        const float time = 0.35f;
        while (t < time)
        {
            t += Time.deltaTime;
            iris.Flash(c, alpha * (1f - t / time));
            yield return null;
        }
        temp.Remove(iris.gameObject);
        Destroy(iris.gameObject);
    }

    private void Burst(Vector2 at, int n)
    {
        for (int k = 0; k < n; k++) shots.AddSpark(at, Random.insideUnitCircle * 3.5f, 0.5f);
    }

    private static void Say(string key, string fallback)
    {
        if (SpawnDirector.Active != null) SpawnDirector.Active.Say(Loc.Get(key, fallback));
    }

    private SpriteRenderer NewSprite(string name, int order)
    {
        var go = new GameObject("Herz_" + name);
        RunScene.Place(go, "Effekte");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Objects";
        sr.sortingOrder = order;
        temp.Add(go);
        return sr;
    }

    private void Kill(GameObject go)
    {
        temp.Remove(go);
        if (go != null) Destroy(go);
    }

    private BossTelegraphMarker Track(BossTelegraphMarker m)
    {
        if (m != null) markers.Add(m);
        return m;
    }

    private void Untrack(BossTelegraphMarker m)
    {
        if (m != null) markers.Remove(m);
    }

    private static void Boom(float pixels, float seconds)
    {
        ScreenShake.Kick(pixels, seconds);
        if (AudioController.Instance != null && AudioController.Instance.EarthHit != null)
            AudioController.Instance.PalySound(AudioController.Instance.EarthHit);
    }

    private static bool Inside(Vector2 center, float radius)
    {
        if (!PlayerAlive) return false;
        float reach = radius + 0.25f;
        return (PlayerPos - center).sqrMagnitude <= reach * reach;
    }

    /// <summary>Steht der Spieler (Kreis 0.15) in einer Ellipse mit diesen Halbachsen?</summary>
    private static bool InsideEllipse(Vector2 center, Vector2 half)
    {
        if (!PlayerAlive) return false;
        Vector2 d = PlayerPos - center;
        float rx = half.x + 0.15f, ry = half.y + 0.15f;
        return (d.x * d.x) / (rx * rx) + (d.y * d.y) / (ry * ry) <= 1f;
    }

    private Rigidbody2D PlayerBody
    {
        get
        {
            if (playerBody == null && PlayerController.Instance != null)
                playerBody = PlayerController.Instance.GetComponent<Rigidbody2D>();
            return playerBody;
        }
    }

    private static bool PlayerAlive =>
        PlayerController.Instance != null && PlayerController.Instance.gameObject.activeSelf;

    private static Vector2 PlayerPos =>
        PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position : Vector2.zero;
}
