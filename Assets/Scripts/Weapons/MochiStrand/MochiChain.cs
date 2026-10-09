using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Kette des <see cref="MochiStrand"/>, von Wurf bis Plopp:
///
///  1. Fliegen   - der Klecks fliegt auf seinen Gegner (folgt ihm). Ist der
///                 weg, landet er an der letzten Stelle und zerplatzt.
///  2. Verketten - Treffer, der Klecks klebt. Dann springt der Faden alle
///                 <see cref="LinkInterval"/> Sekunden zum naechsten freien
///                 Gegner (vom letzten Glied aus, hoechstens <see cref="LinkRange"/>).
///  3. Halten    - alle Gefesselten sind langsamer; wer zu weit wegwill, wird
///                 vom Faden zurueckgezogen und zieht den Nachbarn mit. Ein Teil
///                 jedes Treffers geht an die anderen (<see cref="Enemy.Damaged"/>).
///  4. Schnappen - der Faden zieht sich zusammen, alle fliegen zur Mitte,
///                 Treffer + Plopp (auf der letzten Stufe mit Schockring).
///
/// Ein Gegner haengt nie an zwei Ketten (<see cref="IsBound"/>). Stirbt einer,
/// faellt er raus und seine Nachbarn haengen direkt aneinander. Bosse lassen
/// sich nicht ziehen (Enemy.ApplyPull), werden aber gebremst und teilen mit.
///
/// Der Faden besteht aus kleinen Perlen-Sprites auf ganzen Pixeln (kein
/// LineRenderer - der passt nicht zur Pixelart): unten die Rand-Perlen, darueber
/// die Fuellung, so bleibt eine durchgehende Kontur.
/// </summary>
public class MochiChain : MonoBehaviour
{
    public struct Settings
    {
        public float hitDamage;
        public float snapDamage;
        public float holdTime;
        public int maxLinks;
        public float share;
        public float slow;
        public float shockRadius;
    }

    private const float LinkInterval = 0.1f;
    private const float LinkRange = 3.2f;
    /// <summary>Bis zu dieser Laenge haengt der Faden locker durch, darueber zieht er.</summary>
    private const float SlackLength = 2.2f;
    /// <summary>Weiter auseinander (z.B. Gegner umgesetzt) reisst das Glied.</summary>
    private const float BreakLength = 7f;
    private const float PullPerTile = 3.5f;
    private const float MaxPull = 6f;
    private const float SnapTime = 0.22f;
    private const float FlyMaxTime = 1.2f;

    private const float PixelsPerUnit = 32f;
    private const float BeadSpacing = 2f / PixelsPerUnit;
    private const float BlobFps = 9f;
    private const float PloppFps = 20f;

    private enum State { Flying, Linking, Holding, Snapping, Done }

    private class Link
    {
        public Enemy enemy;
        public SpriteRenderer body;
        public SpriteRenderer blob;
        public float since;
        public Vector2 lastAnchor;
    }

    private static readonly HashSet<Enemy> bound = new HashSet<Enemy>();
    /// <summary>Gerade wird Schaden weitergereicht - der darf nicht wieder weitergehen.</summary>
    private static bool sharing;

    /// <summary>True, wenn der Gegner schon an einer Kette haengt.</summary>
    public static bool IsBound(Enemy enemy) { return enemy != null && bound.Contains(enemy); }

    private Settings settings;
    private State state;
    private float stateTime;
    private float holdLeft;
    private float nextLinkAt;

    private Enemy flyTarget;
    private Vector2 flyPos;
    private SpriteRenderer flyBlob;

    private readonly List<Link> links = new List<Link>();
    private readonly List<SpriteRenderer> fills = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> rims = new List<SpriteRenderer>();
    private int usedBeads;
    private int sortingBase;
    private int sortingLayer;

    private SpriteRenderer plopp;
    private float ploppAge = -1f;

    public void Launch(Vector2 origin, Enemy target, Settings settings)
    {
        this.settings = settings;
        flyTarget = target;
        flyPos = origin;

        // Sortierung einmal vom Spieler holen, alles andere setzt darauf auf.
        SpriteRenderer probe = gameObject.AddComponent<SpriteRenderer>();
        SaladFan.CopySorting(probe, 0);
        sortingLayer = probe.sortingLayerID;
        sortingBase = probe.sortingOrder;
        Destroy(probe);

        flyBlob = NewRenderer("Klecks", 4);
        flyBlob.sprite = MochiStrand.BlobFrames[0];
        SetState(State.Flying);
    }

    void OnEnable() { Enemy.Damaged += OnEnemyDamaged; }

    void OnDisable()
    {
        Enemy.Damaged -= OnEnemyDamaged;
        // Nur die Gegner freigeben - die Kleckse gehen mit diesem Objekt unter.
        for (int i = 0; i < links.Count; i++)
            if (links[i].enemy != null) bound.Remove(links[i].enemy);
        links.Clear();
    }

    private void SetState(State next)
    {
        state = next;
        stateTime = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        stateTime += dt;

        switch (state)
        {
            case State.Flying: UpdateFlying(dt); break;
            case State.Linking: DropLost(); UpdateLinking(); Hold(); break;
            case State.Holding: DropLost(); Hold(); UpdateHolding(dt); break;
            case State.Snapping: DropLost(); UpdateSnapping(); break;
        }

        DrawStrands();
        DrawBlobs();
        UpdatePlopp(dt);

        if (state == State.Done && ploppAge < 0f) Destroy(gameObject);
    }

    // ------------------------------------------------------------------
    //  1. Fliegen
    // ------------------------------------------------------------------

    private void UpdateFlying(float dt)
    {
        bool targetOk = Usable(flyTarget) && !bound.Contains(flyTarget);
        Vector2 goal = targetOk ? Anchor(flyTarget) : flyPos;
        Vector2 to = Vector2.MoveTowards(flyPos, goal, MochiStrand.ThrowSpeed * dt);
        flyPos = to;

        Sprite[] frames = MochiStrand.BlobFrames;
        flyBlob.sprite = frames[(int)(stateTime * BlobFps * 2f) % frames.Length];
        flyBlob.transform.position = Snap(flyPos);

        bool arrived = (goal - flyPos).sqrMagnitude < 0.0025f;
        if (!targetOk && (arrived || stateTime > FlyMaxTime))
        {
            Plopp(flyPos, 1);
            SetState(State.Done);
            Destroy(flyBlob.gameObject);
            return;
        }

        if (!arrived) return;

        Destroy(flyBlob.gameObject);
        flyBlob = null;
        flyTarget.TakeDamage(settings.hitDamage, null, 0.3f);
        if (!Usable(flyTarget))
        {
            // Gleich beim Aufprall gestorben: die Kette faengt beim Naechsten an.
            Enemy next = NearestFree(flyPos, LinkRange);
            if (next == null)
            {
                Plopp(flyPos, 1);
                SetState(State.Done);
                return;
            }
            flyTarget = next;
        }

        AddLink(flyTarget);
        holdLeft = settings.holdTime;
        nextLinkAt = LinkInterval;
        SetState(State.Linking);
    }

    // ------------------------------------------------------------------
    //  2. Verketten
    // ------------------------------------------------------------------

    private void UpdateLinking()
    {
        holdLeft -= Time.deltaTime;
        if (links.Count == 0) { EndWithoutSnap(); return; }

        if (links.Count >= settings.maxLinks) { SetState(State.Holding); return; }
        if (stateTime < nextLinkAt) return;
        nextLinkAt += LinkInterval;

        Link last = links[links.Count - 1];
        Enemy next = NearestFree(Anchor(last.enemy), LinkRange);
        if (next == null) { SetState(State.Holding); return; }

        AddLink(next);
        AudioController.Instance.PalySound(AudioController.Instance.Werfen, 0.03f);
    }

    private void AddLink(Enemy enemy)
    {
        bound.Add(enemy);
        Link link = new Link
        {
            enemy = enemy,
            body = enemy.GetComponentInChildren<SpriteRenderer>(),
            blob = NewRenderer("Klecks", 4),
            since = Time.time,
        };
        link.lastAnchor = Anchor(link);
        links.Add(link);
    }

    // ------------------------------------------------------------------
    //  3. Halten
    // ------------------------------------------------------------------

    private void UpdateHolding(float dt)
    {
        holdLeft -= dt;
        if (links.Count == 0) { EndWithoutSnap(); return; }
        if (holdLeft <= 0f)
        {
            snapCenter = Center();
            SetState(State.Snapping);
        }
    }

    /// <summary>Bremsen und Faden-Zug - jeden Frame, solange die Kette haelt.</summary>
    private void Hold()
    {
        for (int i = 0; i < links.Count; i++)
        {
            links[i].enemy.ApplySlow(settings.slow, 0.25f);
        }

        // Ueberdehnte Glieder ziehen beide Enden zueinander.
        for (int i = 0; i + 1 < links.Count; i++)
        {
            Enemy a = links[i].enemy;
            Enemy b = links[i + 1].enemy;
            Vector2 d = (Vector2)b.transform.position - (Vector2)a.transform.position;
            float over = d.magnitude - SlackLength;
            if (over <= 0f) continue;

            Vector2 pull = d.normalized * Mathf.Min(MaxPull, over * PullPerTile);
            a.ApplyPull(pull, 0.1f);
            b.ApplyPull(-pull, 0.1f);
        }
    }

    private void OnEnemyDamaged(Enemy enemy, float damage)
    {
        if (sharing || settings.share <= 0f) return;
        if (state != State.Linking && state != State.Holding) return;
        if (!bound.Contains(enemy) || IndexOf(enemy) < 0) return;

        float passed = damage * settings.share;
        if (passed <= 0f) return;

        sharing = true;
        try
        {
            // Kopie: wer am weitergereichten Schaden stirbt, fliegt aus der Liste.
            Enemy[] others = new Enemy[links.Count];
            for (int i = 0; i < links.Count; i++) others[i] = links[i].enemy;

            for (int i = 0; i < others.Length; i++)
            {
                Enemy other = others[i];
                if (other != enemy && Usable(other)) other.TakeDamage(passed, null, 0f);
            }
        }
        finally
        {
            sharing = false;
        }
    }

    // ------------------------------------------------------------------
    //  4. Schnappen
    // ------------------------------------------------------------------

    private Vector2 snapCenter;

    private void UpdateSnapping()
    {
        if (links.Count == 0) { EndWithoutSnap(); return; }

        float left = Mathf.Max(0.05f, SnapTime - stateTime);
        for (int i = 0; i < links.Count; i++)
        {
            Enemy enemy = links[i].enemy;
            Vector2 d = snapCenter - (Vector2)enemy.transform.position;
            enemy.ApplyPull(Vector2.ClampMagnitude(d / left, 14f), 0.08f);
        }

        if (stateTime < SnapTime) return;

        // Treffer - ohne Weiterreichen, sonst trifft jeder jeden noch einmal.
        sharing = true;
        try
        {
            Enemy[] hit = new Enemy[links.Count];
            for (int i = 0; i < links.Count; i++) hit[i] = links[i].enemy;
            for (int i = 0; i < hit.Length; i++)
                if (Usable(hit[i])) hit[i].TakeDamage(settings.snapDamage, null, 0f);

            if (settings.shockRadius > 0f)
            {
                var around = new List<Enemy>();
                OverlapDamage.SweepEnemies(snapCenter, snapCenter, settings.shockRadius, around);
                for (int i = 0; i < around.Count; i++)
                {
                    Enemy e = around[i];
                    if (e != null && System.Array.IndexOf(hit, e) < 0) e.TakeDamage(settings.snapDamage * 0.5f);
                }
            }
        }
        finally
        {
            sharing = false;
        }

        AudioController.Instance.PalySound(AudioController.Instance.BOBA, 0.12f);
        Plopp(snapCenter, settings.shockRadius > 0f ? 3 : 1);
        ReleaseAll();
        SetState(State.Done);
    }

    private void EndWithoutSnap()
    {
        ReleaseAll();
        SetState(State.Done);
    }

    // ------------------------------------------------------------------
    //  Glieder verwalten
    // ------------------------------------------------------------------

    /// <summary>Tote, abgeschaltete oder weit weggesetzte Gegner fallen raus.</summary>
    private void DropLost()
    {
        for (int i = links.Count - 1; i >= 0; i--)
        {
            Link link = links[i];
            bool lost = !Usable(link.enemy);
            if (!lost && i > 0)
            {
                float dist = ((Vector2)link.enemy.transform.position - (Vector2)links[i - 1].enemy.transform.position).magnitude;
                lost = dist > BreakLength;
            }
            if (!lost) continue;

            Release(link);
            links.RemoveAt(i);
        }
    }

    private void Release(Link link)
    {
        if (link.enemy != null) bound.Remove(link.enemy);
        if (link.blob != null) Destroy(link.blob.gameObject);
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < links.Count; i++) Release(links[i]);
        links.Clear();
    }

    private int IndexOf(Enemy enemy)
    {
        for (int i = 0; i < links.Count; i++)
            if (links[i].enemy == enemy) return i;
        return -1;
    }

    private Vector2 Center()
    {
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < links.Count; i++) sum += (Vector2)links[i].enemy.transform.position;
        return sum / links.Count;
    }

    private static bool Usable(Enemy enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && !enemy.Untouchable;
    }

    private static Enemy NearestFree(Vector2 from, float radius)
    {
        Enemy best = null;
        float bestSqr = radius * radius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (!Usable(enemy) || bound.Contains(enemy)) continue;

            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = enemy;
            }
        }
        return best;
    }

    /// <summary>Wo der Klecks am Gegner klebt: Mitte seines Bildes.</summary>
    private static Vector2 Anchor(Enemy enemy)
    {
        SpriteRenderer body = enemy.GetComponentInChildren<SpriteRenderer>();
        return body != null ? (Vector2)body.bounds.center : (Vector2)enemy.transform.position;
    }

    private static Vector2 Anchor(Link link)
    {
        if (link.enemy == null || !link.enemy.isActiveAndEnabled) return link.lastAnchor;
        link.lastAnchor = link.body != null ? (Vector2)link.body.bounds.center : (Vector2)link.enemy.transform.position;
        return link.lastAnchor;
    }

    // ------------------------------------------------------------------
    //  Zeichnen
    // ------------------------------------------------------------------

    private SpriteRenderer NewRenderer(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = sortingBase + order;
        return sr;
    }

    private static Vector3 Snap(Vector2 p)
    {
        return new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit,
                           Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, 0f);
    }

    private void DrawBlobs()
    {
        Sprite[] frames = MochiStrand.BlobFrames;
        for (int i = 0; i < links.Count; i++)
        {
            Link link = links[i];
            float age = Time.time - link.since;
            int frame = ((int)(age * BlobFps) + i * 2) % frames.Length;
            link.blob.sprite = frames[frame];
            // Klebt sich mit einem kurzen Plopp an (halbe -> volle Groesse).
            float pop = Mathf.Clamp01(age / 0.08f);
            link.blob.transform.localScale = Vector3.one * (pop < 1f ? Mathf.Lerp(0.5f, 1f, pop) : 1f);
            link.blob.transform.position = Snap(Anchor(link));
        }
    }

    private void DrawStrands()
    {
        usedBeads = 0;

        for (int i = 0; i + 1 < links.Count; i++)
        {
            Vector2 a = Anchor(links[i]);
            Vector2 b = Anchor(links[i + 1]);

            // Neues Glied: Faden schiesst vom Vorgaenger rueber.
            float grow = Mathf.Clamp01((Time.time - links[i + 1].since) / LinkInterval);
            // Beim Schnappen zieht sich alles zur Mitte zusammen.
            float shrink = state == State.Snapping ? Mathf.Clamp01(stateTime / SnapTime) : 0f;

            DrawStrand(a, b, grow, shrink, i);
        }

        for (int i = usedBeads; i < fills.Count; i++)
        {
            if (fills[i].enabled) { fills[i].enabled = false; rims[i].enabled = false; }
        }
    }

    private void DrawStrand(Vector2 a, Vector2 b, float grow, float shrink, int seed)
    {
        Vector2 d = b - a;
        float length = d.magnitude;
        if (length < 0.05f) return;

        // Locker: haengt durch und wackelt. Gespannt: gerade und duenn.
        float tension = Mathf.Clamp01((length - 0.6f) / (SlackLength * 1.3f));
        float sag = Mathf.Max(0f, SlackLength - length) * 0.22f + 0.12f * (1f - tension);
        sag += Mathf.Sin(Time.time * 7f + seed * 1.7f) * 0.05f * (1f - tension);
        sag *= 1f - shrink;
        int middle = tension < 0.4f ? 0 : (tension < 0.8f ? 1 : 2);

        float from = shrink * 0.5f;
        float to = Mathf.Min(grow, 1f - shrink * 0.5f);
        int beads = Mathf.Max(2, Mathf.CeilToInt(length * (to - from) / BeadSpacing));

        Sprite[] frames = MochiStrand.BeadFrames;
        for (int k = 0; k <= beads; k++)
        {
            float t = Mathf.Lerp(from, to, k / (float)beads);
            Vector2 p = a + d * t + Vector2.down * (sag * 4f * t * (1f - t));
            bool end = t < 0.15f || t > 0.85f;
            int size = end ? Mathf.Max(0, middle - 1) : middle;

            Bead(Snap(p), frames[size], frames[size + 3]);
        }
    }

    private void Bead(Vector3 position, Sprite fill, Sprite rim)
    {
        if (usedBeads == fills.Count)
        {
            fills.Add(NewRenderer("Faden", 3));
            rims.Add(NewRenderer("FadenRand", 2));
        }

        SpriteRenderer f = fills[usedBeads];
        SpriteRenderer r = rims[usedBeads];
        usedBeads++;

        f.enabled = true;
        r.enabled = true;
        f.sprite = fill;
        r.sprite = rim;
        f.transform.position = position;
        r.transform.position = position;
    }

    // ------------------------------------------------------------------
    //  Plopp
    // ------------------------------------------------------------------

    private void Plopp(Vector2 at, int scale)
    {
        Sprite[] frames = MochiStrand.PloppFrames;
        if (frames == null || frames.Length == 0) return;

        if (plopp == null) plopp = NewRenderer("Plopp", 5);
        plopp.transform.position = Snap(at);
        plopp.transform.localScale = Vector3.one * scale;
        plopp.sprite = frames[0];
        ploppAge = 0f;
    }

    private void UpdatePlopp(float dt)
    {
        if (ploppAge < 0f || plopp == null) return;

        ploppAge += dt;
        Sprite[] frames = MochiStrand.PloppFrames;
        int frame = (int)(ploppAge * PloppFps);
        if (frame >= frames.Length)
        {
            plopp.enabled = false;
            ploppAge = -1f;
            return;
        }
        plopp.sprite = frames[frame];
    }
}
