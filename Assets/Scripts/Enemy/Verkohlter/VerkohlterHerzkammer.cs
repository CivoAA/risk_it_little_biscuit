using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Die Herzkammer des Verkohlten (Phase 3): der Raum, in dem der Spieler nach
/// dem Einsaugen landet. In der Mitte schwebt das Herz (das ist der Boss
/// selbst, siehe <see cref="EnemyVerkohlter"/>), drumherum ein Krater aus
/// verkohltem Keksteig, durch dessen Glutadern jeder Herzschlag rollt.
///
/// Die Kammer liegt weit weg von der Welt (<see cref="Site"/>). Solange sie
/// offen ist:
///   * steht die Welt still: Chunk-Verwaltung und Objekt-Spawner sind aus,
///     der <see cref="SpawnDirector"/> ist angehalten, alle anderen Gegner
///     sind weggeschaltet (sie kommen beim Verlassen wieder),
///   * zoomt die Kamera auf 480x270 Bildpixel heraus und folgt dem Spieler,
///     aber nur so weit, dass das Herz immer im Bild bleibt
///     (<see cref="VerkohlterArt.CameraSlack"/>) - die Kammer ist groesser als
///     das Bild, damit spaeter der Kampf gegen das Herz Platz hat,
///   * halten zwei Raender den Spieler drin: die Bodenellipse und das Lavabecken.
///
/// Stirbt das Herz, verstummt der Herzschlag und die Kammer wird dunkel
/// (<see cref="HeartDying"/>). Danach:
///   * im echten Lauf (eine Karte ist geladen) kommt der Sieg-Bildschirm -
///     der Spieler bleibt in der erloschenen Kammer stehen,
///   * in der Test-Szene blendet die Kammer ab, setzt den Spieler dorthin
///     zurueck, wo der Verkohlte stand, und raeumt alles wieder auf.
/// Verschwindet der Boss anders (Test-Szene: Boss weg), gilt der zweite Weg.
///
/// Bilder: Tools/verkohlter_herz.py (herzkammer_hinten / _glut / _vorne).
/// </summary>
public class VerkohlterHerzkammer : MonoBehaviour
{
    /// <summary>Hier liegt die Kammer - weit weg von allem, was die Welt je erzeugt.</summary>
    public static readonly Vector2 Site = new Vector2(4000f, 4000f);

    public static VerkohlterHerzkammer Active { get; private set; }

    // --- Raender (Welt-Einheiten, ein Stueck innerhalb der gemalten Kanten)
    private const float WallInsetX = 0.42f, WallInsetY = 0.2f;
    private const float PoolPadX = 0.12f, PoolPadY = 0.08f;

    // --- Funken
    private const int EmberCount = 160;
    private const int AshCount = 34;

    /// <summary>Wie lange nach dem Tod des Herzens noch Zeit zum Aufsammeln bleibt.</summary>
    private const float ExitDelay = 1.6f;

    private Enemy heart;
    private Vector2 returnPoint;

    private SpriteRenderer glut;
    private Sprite[] glutFrames;
    private Transform cameraTarget;

    private float clock;
    private int frame = -1;
    private bool exiting;
    private bool closed;
    private bool dying;

    /// <summary>Wie dunkel die Kammer wird, wenn das Herz erlischt (Faktor auf alle Bilder).</summary>
    private static readonly Color DeadTint = new Color(0.42f, 0.36f, 0.38f, 1f);
    private const float DarkenTime = 2.6f;
    /// <summary>Ruhiges Bild des Herzschlags, auf dem die Glut stehen bleibt.</summary>
    private const int DeadGlutFrame = 12;
    /// <summary>Im Lauf: so lange nach dem Herz-Tod, bis der Sieg ausgeloest wird.</summary>
    private const float WinDelay = 1.0f;

    private readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();

    // Weltzustand zum Wiederherstellen
    private readonly List<Behaviour> frozen = new List<Behaviour>();
    private readonly List<GameObject> parked = new List<GameObject>();
    private readonly List<KeyValuePair<CinemachineCamera, Transform>> cameras =
        new List<KeyValuePair<CinemachineCamera, Transform>>();
    private PixelPerfectCamera pixelCamera;
    private int oldRefX, oldRefY;

    private Particle[] embers;
    private Particle[] ash;

    private struct Particle
    {
        public SpriteRenderer Sr;
        public Vector2 Pos;
        public Vector2 Vel;
        public float Age, Life, Phase;
        public bool Big;
    }

    // ------------------------------------------------------------- Oberflaeche

    public Vector2 Anchor => transform.position;
    public Vector2 HeartPosition => Anchor + VerkohlterArt.HeartOffset;

    /// <summary>Wo der Spieler landet: vor dem Becken, unter dem Herzen.</summary>
    public Vector2 LandingSpot => Anchor + VerkohlterArt.PoolCenter + new Vector2(0f, -2.1f);

    /// <summary>Bild des Herzschlags gerade jetzt (0..15) - das Herz spielt dasselbe Bild.</summary>
    public int Frame => Mathf.Max(0, frame);

    /// <summary>
    /// Baut die Kammer auf und schaltet die Welt weg. Aufrufen, waehrend das
    /// Bild schwarz ist - die Kamera springt dabei.
    /// </summary>
    public static VerkohlterHerzkammer Open(Enemy heart, Vector2 returnPoint)
    {
        if (Active != null) Destroy(Active.gameObject);

        var go = new GameObject("Herzkammer");
        RunScene.Place(go, "Effekte");
        go.transform.position = Site;
        var chamber = go.AddComponent<VerkohlterHerzkammer>();
        chamber.heart = heart;
        chamber.returnPoint = returnPoint;
        chamber.Build();
        chamber.FreezeWorld();
        chamber.TakeCamera();
        Active = chamber;
        return chamber;
    }

    // ------------------------------------------------------------------ Aufbau

    private void Build()
    {
        Sprite[] back = VerkohlterArt.Strip("herzkammer_hinten");
        Sprite[] front = VerkohlterArt.Strip("herzkammer_vorne");
        glutFrames = VerkohlterArt.Strip("herzkammer_glut");

        int background = SortingLayer.NameToID("Background");
        int objects = SortingLayer.NameToID("Objects");
        int foreground = SortingLayer.NameToID("Foreground");

        // Schwarz bis weit ueber jeden Bildrand hinaus
        SpriteRenderer voidSr = Layer("Leere", VerkohlterArt.Pixel, background, -100);
        voidSr.color = VerkohlterArt.Void;
        voidSr.transform.localPosition = new Vector3(-60f, -40f, 0f);
        voidSr.transform.localScale = new Vector3(120f * VerkohlterArt.PixelsPerUnit, 80f * VerkohlterArt.PixelsPerUnit, 1f);

        tinted.Add(Layer("Hinten", VerkohlterArt.Frame(back, 0), background, -50));
        glut = Layer("Glut", VerkohlterArt.Frame(glutFrames, 0), background, -49);
        tinted.Add(glut);
        tinted.Add(Layer("Vorne", VerkohlterArt.Frame(front, 0), foreground, 50));

        // Raender: Bodenellipse (innen bleiben) und Lavabecken (draussen bleiben)
        Vector2 floor = VerkohlterArt.FloorCenter;
        Vector2 fr = VerkohlterArt.FloorRadius - new Vector2(WallInsetX, WallInsetY);
        Ring("Rand", floor + new Vector2(0f, 0.04f), fr, 120);
        Ring("Becken", VerkohlterArt.PoolCenter, VerkohlterArt.PoolRadius + new Vector2(PoolPadX, PoolPadY), 48);

        var target = new GameObject("Kamerapunkt");
        target.transform.SetParent(transform, false);
        cameraTarget = target.transform;

        embers = new Particle[EmberCount];
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i].Sr = Layer("Funke", VerkohlterArt.Pixel, objects, 40);
            SpawnEmber(ref embers[i], true);
        }
        ash = new Particle[AshCount];
        for (int i = 0; i < ash.Length; i++)
        {
            ash[i].Sr = Layer("Asche", VerkohlterArt.Pixel, objects, 39);
            SpawnAsh(ref ash[i], true);
        }
    }

    private SpriteRenderer Layer(string name, Sprite sprite, int sortingLayer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = order;
        return sr;
    }

    private void Ring(string name, Vector2 center, Vector2 radius, int points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center;
        int wall = LayerMask.NameToLayer("Baundaries");
        go.layer = wall >= 0 ? wall : 3;
        var edge = go.AddComponent<EdgeCollider2D>();
        var pts = new Vector2[points + 1];
        for (int i = 0; i <= points; i++)
        {
            float a = i / (float)points * Mathf.PI * 2f;
            pts[i] = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
        }
        edge.points = pts;
        edge.edgeRadius = 0.02f;
    }

    // ---------------------------------------------------------- Welt anhalten

    private void FreezeWorld()
    {
        FreezeAll<WorldManager3x3>();
        FreezeAll<WorldManager>();
        FreezeAll<RandomObjectSpawner>();
        FreezeAll<RandomObjectSpawner3x3>();

        if (SpawnDirector.Active != null) SpawnDirector.Active.Suspended = true;

        // Alle anderen Gegner parken - sie warten, bis der Spieler zurueck ist.
        var list = new List<Enemy>(Enemy.Alive);
        foreach (Enemy e in list)
        {
            if (e == null || e == heart) continue;
            e.gameObject.SetActive(false);
            parked.Add(e.gameObject);
        }
    }

    private void FreezeAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>())
        {
            if (!b.enabled) continue;
            b.enabled = false;
            frozen.Add(b);
        }
    }

    private void TakeCamera()
    {
        FollowPlayer();
        Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>())
        {
            if (cam.Follow == null || (player != null && cam.Follow != player)) continue;
            cameras.Add(new KeyValuePair<CinemachineCamera, Transform>(cam, cam.Follow));
            cam.Follow = cameraTarget;
            cam.PreviousStateIsValid = false;
        }

        Camera main = Camera.main;
        pixelCamera = main != null ? main.GetComponent<PixelPerfectCamera>() : null;
        if (pixelCamera != null)
        {
            oldRefX = pixelCamera.refResolutionX;
            oldRefY = pixelCamera.refResolutionY;
            pixelCamera.refResolutionX = VerkohlterArt.ChamberRefWidth;
            pixelCamera.refResolutionY = VerkohlterArt.ChamberRefHeight;
        }
    }

    /// <summary>Alles wie vorher: Kamera, Zoom, Welt, geparkte Gegner.</summary>
    private void RestoreWorld()
    {
        if (closed) return;
        closed = true;

        foreach (var kv in cameras)
        {
            if (kv.Key == null) continue;
            kv.Key.Follow = kv.Value;
            kv.Key.PreviousStateIsValid = false;
        }
        cameras.Clear();

        if (pixelCamera != null)
        {
            pixelCamera.refResolutionX = oldRefX;
            pixelCamera.refResolutionY = oldRefY;
        }

        foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
        frozen.Clear();

        if (SpawnDirector.Active != null) SpawnDirector.Active.Suspended = false;

        foreach (GameObject go in parked) if (go != null) go.SetActive(true);
        parked.Clear();
    }

    private void OnDestroy()
    {
        // Laeuft auch, wenn der Lauf mitten in der Kammer endet (Szene weg) -
        // dann ist das meiste schon zerstoert, die Null-Pruefungen fangen das.
        RestoreWorld();
        if (Active == this) Active = null;
    }

    // ---------------------------------------------------------------- Kamera

    /// <summary>Wie stark die Kamera vom Spieler Richtung Herz gezogen wird (0 = gar nicht).</summary>
    private const float HeartPull = 0.3f;

    /// <summary>
    /// Kamerapunkt = Spieler, ein Stueck (<see cref="HeartPull"/>) zum Herzen
    /// gezogen und hoechstens <see cref="VerkohlterArt.CameraSlack"/> von ihm weg.
    /// Auf ganze Bildpixel gerundet.
    /// </summary>
    public void FollowPlayer()
    {
        if (cameraTarget == null) return;
        Vector2 heartPos = HeartPosition;
        Vector2 want = heartPos;
        if (PlayerController.Instance != null)
            want = (Vector2)PlayerController.Instance.transform.position + Vector2.up * 0.5f;
        Vector2 slack = VerkohlterArt.CameraSlack;
        Vector2 d = (want - heartPos) * (1f - HeartPull);
        d.x = Mathf.Clamp(d.x, -slack.x, slack.x);
        d.y = Mathf.Clamp(d.y, -slack.y, slack.y);
        Vector2 p = VerkohlterArt.Snap(heartPos + d);
        cameraTarget.position = new Vector3(p.x, p.y, 0f);
    }

    /// <summary>
    /// Wo ein Punkt der Kammer auf dem Schirm liegt (0..1), gerechnet aus dem
    /// Kamerapunkt - stimmt auch im Bild, in dem die Kamera erst springt.
    /// </summary>
    public Vector2 Viewport(Vector2 world)
    {
        Vector2 cam = cameraTarget != null ? (Vector2)cameraTarget.position : Anchor;
        Vector2 view = new Vector2(VerkohlterArt.ChamberRefWidth, VerkohlterArt.ChamberRefHeight) / VerkohlterArt.PixelsPerUnit;
        return new Vector2(0.5f + (world.x - cam.x) / view.x, 0.5f + (world.y - cam.y) / view.y);
    }

    // ------------------------------------------------------------ Herzschlag

    private void Update()
    {
        if (!closed) FollowPlayer();
        clock += Time.deltaTime;
        // Laeuft "Herz der Glut", schlaegt das Herz im Takt der Musik.
        if (!dying && VerkohlterMusik.HeartClock(out double musicClock)) clock = (float)musicClock;
        int f = Mathf.FloorToInt(clock * VerkohlterArt.Fps) % VerkohlterArt.BeatFrames;
        if (dying) f = DeadGlutFrame;
        if (f != frame)
        {
            frame = f;
            if (glut != null && glutFrames.Length > 0) glut.sprite = glutFrames[f % glutFrames.Length];
            if (!exiting && !dying)
            {
                if (f == VerkohlterArt.BeatLub)
                {
                    VerkohlterSounds.Lub();
                    ScreenShake.Kick(1f, 0.15f);
                }
                else if (f == VerkohlterArt.BeatDub)
                {
                    VerkohlterSounds.Dub();
                }
            }
        }

        UpdateEmbers(Time.deltaTime);

        // Das Herz ist tot (oder der Boss wurde weggeraeumt): Sieg oder raus.
        if (!exiting && heart == null)
        {
            exiting = true;
            if (dying && IsRealRun) StartCoroutine(Win());
            else StartCoroutine(Exit());
        }
    }

    // -------------------------------------------------------------- Herz-Tod

    /// <summary>Ein echter Lauf (Karte geladen) - nicht die Test-Szene.</summary>
    private static bool IsRealRun => MapDefinition.Active != null && GameManager.Instance != null;

    /// <summary>
    /// Das Herz zerfaellt: der Herzschlag verstummt, die Glut bleibt stehen und
    /// die ganze Kammer wird langsam dunkel. Keine neuen Funken mehr.
    /// </summary>
    public void HeartDying()
    {
        if (dying) return;
        dying = true;
        StartCoroutine(Darken());
    }

    private IEnumerator Darken()
    {
        float t = 0f;
        while (t < DarkenTime)
        {
            t += Time.deltaTime;
            float q = Mathf.SmoothStep(0f, 1f, t / DarkenTime);
            Color c = Color.Lerp(Color.white, DeadTint, q);
            foreach (SpriteRenderer sr in tinted) if (sr != null) sr.color = c;
            yield return null;
        }
    }

    /// <summary>Die Reste des Herzens (letztes Bild des Zerfalls) bleiben am Boden liegen - unter dem Spieler.</summary>
    public void LeaveRemains(Sprite sprite, Vector2 at)
    {
        SpriteRenderer sr = Layer("Herzreste", sprite, SortingLayer.NameToID("Objects"), -5);
        sr.transform.position = VerkohlterArt.Snap(at);
    }

    /// <summary>Im Lauf: kurz durchatmen (Beute fliegt zu), dann der Sieg-Bildschirm.</summary>
    private IEnumerator Win()
    {
        yield return new WaitForSeconds(WinDelay);
        PlayerController pc = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        if (pc == null || !pc.gameObject.activeSelf || gm == null || !gm.gameActiv) yield break;
        // Der Boss-Tod hat bossSpawned schon gesetzt - GameOver zeigt damit den Sieg.
        gm.bossSpawned = true;
        gm.GameOver();
    }

    // ---------------------------------------------------------------- Funken

    private void SpawnEmber(ref Particle p, bool anywhere)
    {
        Vector2 floor = Anchor + VerkohlterArt.FloorCenter;
        Vector2 pool = Anchor + VerkohlterArt.PoolCenter;
        Vector2 at;
        if (Random.value < 0.25f)
        {
            // aus dem Lavabecken
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * 0.9f;
            at = pool + new Vector2(Mathf.Cos(a) * r * VerkohlterArt.PoolRadius.x, Mathf.Sin(a) * r * VerkohlterArt.PoolRadius.y);
        }
        else
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * 0.97f;
            at = floor + new Vector2(Mathf.Cos(a) * r * VerkohlterArt.FloorRadius.x, Mathf.Sin(a) * r * VerkohlterArt.FloorRadius.y);
        }
        p.Pos = at;
        p.Vel = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1.4f));
        p.Life = Random.Range(1.4f, 3.4f);
        p.Age = anywhere ? Random.value * p.Life : 0f;
        if (anywhere) p.Pos += p.Vel * p.Age;
        p.Phase = Random.value * 10f;
        p.Big = Random.value < 0.22f;
    }

    private void SpawnAsh(ref Particle p, bool anywhere)
    {
        float halfW = VerkohlterArt.ChamberRefWidth * 0.5f / VerkohlterArt.PixelsPerUnit + 1f;
        float halfH = VerkohlterArt.ChamberRefHeight * 0.5f / VerkohlterArt.PixelsPerUnit + 0.5f;
        Vector2 cam = cameraTarget != null ? (Vector2)cameraTarget.position : Anchor;
        p.Pos = cam + new Vector2(Random.Range(-halfW, halfW), anywhere ? Random.Range(-halfH, halfH) : halfH);
        p.Vel = new Vector2(Random.Range(-0.25f, 0.1f), -Random.Range(0.18f, 0.45f));
        p.Life = 2f * halfH / -p.Vel.y + 0.5f;
        p.Age = 0f;
        p.Phase = Random.value * 10f;
        p.Big = Random.value < 0.3f;
    }

    private void UpdateEmbers(float dt)
    {
        if (embers == null) return;
        Color32[] fire = VerkohlterArt.Fire;
        // Beim BUM steigen kurz mehr und schnellere Funken
        float kick = frame == VerkohlterArt.BeatLub || frame == VerkohlterArt.BeatLub + 1 ? 1.8f : 1f;
        for (int i = 0; i < embers.Length; i++)
        {
            ref Particle p = ref embers[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                // Erloschen: keine neuen Funken, die alten verglimmen
                if (dying)
                {
                    p.Sr.enabled = false;
                    continue;
                }
                SpawnEmber(ref p, false);
            }
            float q = p.Age / p.Life;
            p.Pos += new Vector2(p.Vel.x + Mathf.Sin(clock * 2.3f + p.Phase) * 0.25f, p.Vel.y * kick) * dt;
            int k = q < 0.12f ? 6 : q < 0.3f ? 5 : q < 0.5f ? 4 : q < 0.72f ? 3 : 2;
            p.Sr.color = fire[k];
            float size = p.Big && q < 0.5f ? 2f : 1f;
            p.Sr.transform.localScale = new Vector3(size, size, 1f);
            p.Sr.transform.position = VerkohlterArt.Snap(p.Pos);
        }
        for (int i = 0; i < ash.Length; i++)
        {
            ref Particle p = ref ash[i];
            p.Age += dt;
            if (p.Age >= p.Life) SpawnAsh(ref p, false);
            p.Pos += new Vector2(p.Vel.x + Mathf.Sin(clock * 1.3f + p.Phase) * 0.18f, p.Vel.y) * dt;
            Color32 c = VerkohlterArt.Ash;
            c.a = (byte)(p.Big ? 200 : 140);
            p.Sr.color = c;
            p.Sr.transform.position = VerkohlterArt.Snap(p.Pos);
        }
    }

    /// <summary>Kleine Aschewolke (Landung): ein paar Pixel, die zur Seite spritzen.</summary>
    public void Puff(Vector2 at)
    {
        StartCoroutine(PuffRoutine(at));
    }

    private IEnumerator PuffRoutine(Vector2 at)
    {
        const int n = 14;
        var srs = new SpriteRenderer[n];
        var vel = new Vector2[n];
        var pos = new Vector2[n];
        int objects = SortingLayer.NameToID("Objects");
        for (int i = 0; i < n; i++)
        {
            srs[i] = Layer("Staub", VerkohlterArt.Pixel, objects, 25);
            float side = i % 2 == 0 ? 1f : -1f;
            vel[i] = new Vector2(side * Random.Range(0.8f, 2.6f), Random.Range(0.2f, 1.2f));
            pos[i] = at + new Vector2(side * Random.Range(0f, 0.25f), 0.05f);
            srs[i].transform.localScale = Vector3.one * (i % 3 == 0 ? 2f : 1f);
        }
        float t = 0f;
        const float life = 0.55f;
        while (t < life)
        {
            t += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                vel[i] *= 1f - 4f * Time.deltaTime;
                vel[i].y -= 3f * Time.deltaTime;
                pos[i] += vel[i] * Time.deltaTime;
                Color32 c = i % 4 == 0 ? VerkohlterArt.Fire[3] : VerkohlterArt.Ash;
                c.a = (byte)(255 * Mathf.Clamp01(1f - t / life));
                srs[i].color = c;
                srs[i].transform.position = VerkohlterArt.Snap(pos[i]);
            }
            yield return null;
        }
        foreach (SpriteRenderer sr in srs) if (sr != null) Destroy(sr.gameObject);
    }

    // ------------------------------------------------------------------ Raus

    private IEnumerator Exit()
    {
        yield return new WaitForSeconds(ExitDelay);

        PlayerController pc = PlayerController.Instance;
        if (pc == null || !pc.gameObject.activeSelf)
        {
            // Spieler ist tot - nichts mehr zu zeigen, nur aufraeumen.
            Destroy(gameObject);
            yield break;
        }

        var puppet = new PlayerPuppet(pc);
        ScreenIris iris = ScreenIris.Create();

        // Zuziehen auf den Spieler, mit hellem Blitz am Ende
        yield return IrisMove(iris, () => ViewportOf(pc.transform.position + Vector3.up * 0.5f), 1.1f, 0f, 0.6f, true);
        iris.Close();

        // Liegengebliebene Beute nimmt er mit
        Vector2 delta = returnPoint - (Vector2)pc.transform.position;
        CarryLoot(delta);

        RestoreWorld();
        VerkohlterMusik.Leave();
        puppet.MovePlayer(returnPoint, carryCompanions: true);
        foreach (Transform child in transform) child.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.35f);
        ScreenShake.Kick(4f, 0.35f);
        yield return IrisMove(iris, () => ViewportOf(pc.transform.position + Vector3.up * 0.5f), 0f, 1.2f, 0.7f, false);
        iris.Open();
        puppet.Release();
        Destroy(iris.gameObject);
        Destroy(gameObject);
    }

    private void CarryLoot(Vector2 delta)
    {
        const float reach = 14f;
        foreach (ExpPickup xp in FindObjectsByType<ExpPickup>())
        {
            if (Vector2.Distance(xp.transform.position, Anchor) < reach) xp.transform.position += (Vector3)delta;
        }
        foreach (PickUps pu in FindObjectsByType<PickUps>())
        {
            if (Vector2.Distance(pu.transform.position, Anchor) < reach) pu.transform.position += (Vector3)delta;
        }
    }

    // ------------------------------------------------------------- Helfer

    public static Vector2 ViewportOf(Vector3 world)
    {
        Camera cam = Camera.main;
        return cam != null ? (Vector2)cam.WorldToViewportPoint(world) : new Vector2(0.5f, 0.5f);
    }

    /// <summary>Blende von Radius a nach b in <paramref name="seconds"/> (Anteil der Bildhoehe).</summary>
    public static IEnumerator IrisMove(ScreenIris iris, System.Func<Vector2> center, float from, float to,
                                       float seconds, bool easeIn)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float q = Mathf.Clamp01(t / seconds);
            float e = easeIn ? q * q : 1f - (1f - q) * (1f - q);
            if (iris != null) iris.Set(center(), Mathf.Lerp(from, to, e));
            yield return null;
        }
    }
}
