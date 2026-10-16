using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine Blase der <see cref="BubbleGun"/>. Zwei Phasen:
///
/// Wurf  - fliegt auf ihren Gegner zu. Stirbt er oder ist er nicht mehr
///         einfangbar, nimmt sie den naechsten freien in der Naehe. Findet sie
///         keinen oder braucht sie zu lange, zerplatzt sie ohne Schaden.
///
/// Tragen - hat ihn eingefangen (<see cref="Enemy.EnterBubble"/>) und fliegt mit
///         ihm zu dem Gegner in der Naehe, um den herum die meisten anderen
///         stehen. Diesen einen verfolgt sie; stirbt er, sucht sie den naechsten.
///         Sie platzt, sobald sie dort ist oder nach <see cref="BubbleGun.CarryTime"/>.
///
/// Beim Platzen: voller Schaden am Gefangenen, die Haelfte fuer alle anderen
/// im Radius. Der Gefangene wird vorher freigelassen und kann daran sterben.
/// </summary>
public class BubbleShot : MonoBehaviour
{
    /// <summary>Durchmesser der leeren Blase im Flug, in Tiles.</summary>
    private const float ThrowSize = 0.6f;

    /// <summary>So nah (Tiles) muss die Blase an ihren Gegner, um ihn einzufangen.</summary>
    private const float CatchDistance = 0.45f;

    /// <summary>So nah (Tiles) am Gruppenziel platzt die volle Blase.</summary>
    private const float ArriveDistance = 0.6f;

    /// <summary>So lange (Sekunden) waechst die Blase um den Gefangenen herum auf.</summary>
    private const float GrowTime = 0.15f;

    /// <summary>Gegner, die gerade eine Blase anfliegt oder traegt - keiner wird doppelt eingefangen.</summary>
    private static readonly HashSet<Enemy> Claimed = new HashSet<Enemy>();

    private static readonly List<Enemy> scratch = new List<Enemy>();
    private static readonly List<Enemy> nearScratch = new List<Enemy>();

    private SpriteRenderer sr;
    private float damage;
    private float blastRadius;
    private float searchRange;

    private Enemy target;       // Wurf: wen sie einfangen will
    private Enemy captive;      // Tragen: wen sie traegt
    private Enemy groupTarget;  // Tragen: wohin sie ihn bringt

    private Vector2 carryPos;      // Pivot des Gefangenen
    private Vector2 centerOffset;  // Pivot -> Koerpermitte
    private float bubbleSize;
    private float age;
    private bool popped;
    private float nextGroupSearch;

    /// <summary>So oft (Sekunden) sucht eine Blase ohne Gruppenziel neu.</summary>
    private const float GroupSearchInterval = 0.15f;

    /// <summary>Kann eine neue Blase diesen Gegner anfliegen?</summary>
    public static bool IsFreeTarget(Enemy enemy)
    {
        return enemy != null && enemy.CanBeBubbled && !Claimed.Contains(enemy);
    }

    public static BubbleShot Launch(Vector2 origin, Enemy target, float damage, float blastRadius, float searchRange)
    {
        // Zerstoerte Gegner, die eine Blase noch anflog, fallen hier raus.
        Claimed.RemoveWhere(e => e == null);

        GameObject go = BubbleGun.CreateInRunScene("BubbleShot", origin);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BubbleSprite;
        SaladFan.CopySorting(sr, 2);

        BubbleShot shot = go.AddComponent<BubbleShot>();
        shot.sr = sr;
        shot.damage = damage;
        shot.blastRadius = blastRadius;
        shot.searchRange = searchRange;
        shot.SetSize(ThrowSize);
        shot.Claim(target);
        return shot;
    }

    // ------------------------------------------------------------------
    //  Ablauf
    // ------------------------------------------------------------------

    void Update()
    {
        if (popped) return;
        age += Time.deltaTime;

        if (captive == null) UpdateThrow();
        else UpdateCarry();
    }

    void FixedUpdate()
    {
        if (popped || captive == null) return;

        // Kein Gruppenziel (noch niemand in der Naehe, oder es ist tot, selbst
        // eingefangen, weggelaufen ...) - regelmaessig neu suchen. Laeuft
        // jemand in den Umkreis, macht sich die Blase noch auf den Weg.
        if (!IsValidGroupTarget(groupTarget) && Time.time >= nextGroupSearch)
        {
            groupTarget = FindGroupTarget(Center);
            nextGroupSearch = Time.time + GroupSearchInterval;
        }

        if (groupTarget != null)
        {
            Vector2 goal = GroupCenter(groupTarget) - centerOffset;
            carryPos = Vector2.MoveTowards(carryPos, goal, BubbleGun.CarrySpeed * Time.fixedDeltaTime);
        }

        captive.MoveInBubble(carryPos);
    }

    void LateUpdate()
    {
        if (popped || captive == null) return;

        // Bild an den (interpolierten) Gefangenen haengen, sonst zittert die Blase gegen ihn.
        transform.position = (Vector2)captive.transform.position + centerOffset;

        // Leichtes Wabern, waehrend sie aufwaechst.
        float grow = Mathf.Clamp01(age / GrowTime);
        float wobble = 1f + 0.04f * Mathf.Sin(age * 14f);
        SetSize(bubbleSize * Mathf.Lerp(0.5f, 1f, grow) * wobble);
    }

    private void UpdateThrow()
    {
        if (age > BubbleGun.MaxThrowTime)
        {
            Fizzle();
            return;
        }

        if (target == null || !target.CanBeBubbled)
        {
            Claim(FindNewTarget(transform.position));
            if (target == null)
            {
                Fizzle();
                return;
            }
        }

        Vector2 goal = BodyCenter(target);
        Vector2 pos = Vector2.MoveTowards(transform.position, goal, BubbleGun.ThrowSpeed * Time.deltaTime);
        transform.position = pos;

        if ((goal - pos).sqrMagnitude <= CatchDistance * CatchDistance) Capture(target);
    }

    private void UpdateCarry()
    {
        if (age >= BubbleGun.CarryTime)
        {
            Pop();
            return;
        }

        if (groupTarget != null &&
            (Center - GroupCenter(groupTarget)).sqrMagnitude <= ArriveDistance * ArriveDistance)
        {
            Pop();
        }
    }

    private void Capture(Enemy enemy)
    {
        if (!enemy.EnterBubble())
        {
            // Inzwischen nicht mehr einfangbar - weiter mit dem naechsten.
            Claim(FindNewTarget(transform.position));
            return;
        }

        captive = enemy;
        target = null;
        age = 0f;   // ab jetzt zaehlt die Tragezeit

        carryPos = enemy.transform.position;
        centerOffset = BodyCenter(enemy) - carryPos;
        bubbleSize = SizeAround(enemy);

        // Vor den Gefangenen legen.
        SpriteRenderer enemySr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (enemySr != null)
        {
            sr.sortingLayerID = enemySr.sortingLayerID;
            sr.sortingOrder = enemySr.sortingOrder + 5;
        }

        groupTarget = FindGroupTarget(Center);
    }

    /// <summary>Platzt ohne Schaden (kein Gegner erwischt).</summary>
    private void Fizzle()
    {
        popped = true;
        BubblePop.Spawn(transform.position, Mathf.Max(0.4f, ThrowSize), sr);
        Destroy(gameObject);
    }

    private void Pop()
    {
        if (popped) return;
        popped = true;

        Vector2 center = Center;
        Enemy prisoner = captive;
        Release();

        WeaponSounds.Play(WeaponSounds.BubblePop);
        BubblePop.Spawn(center, blastRadius, sr);

        // Erst sammeln, dann treffen - wer stirbt, verlaesst Enemy.Alive.
        scratch.Clear();
        float maxSqr = blastRadius * blastRadius;
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy == prisoner) continue;
            if ((BodyCenter(enemy) - center).sqrMagnitude <= maxSqr) scratch.Add(enemy);
        }

        if (prisoner != null) prisoner.TakeDamage(damage);

        float splash = damage * BubbleGun.SplashFactor;
        for (int i = 0; i < scratch.Count; i++)
        {
            if (scratch[i] != null) scratch[i].TakeDamage(splash);
        }
        scratch.Clear();

        Destroy(gameObject);
    }

    /// <summary>Gefangenen freilassen und alle Ansprueche aufgeben.</summary>
    private void Release()
    {
        if (target != null) Claimed.Remove(target);
        target = null;

        if (captive != null)
        {
            Claimed.Remove(captive);
            captive.LeaveBubble();
        }
        captive = null;
    }

    void OnDestroy()
    {
        // Lauf vorbei, Szene entladen ... - niemand bleibt in einer Blase haengen.
        Release();
    }

    // ------------------------------------------------------------------
    //  Ziele
    // ------------------------------------------------------------------

    private void Claim(Enemy enemy)
    {
        if (target != null) Claimed.Remove(target);
        target = enemy;
        if (target != null) Claimed.Add(target);
    }

    /// <summary>Naechster freie, einfangbare Gegner im Suchradius der Waffe.</summary>
    private Enemy FindNewTarget(Vector2 from)
    {
        if (target != null) Claimed.Remove(target);

        Enemy best = null;
        float bestSqr = searchRange * searchRange;

        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (!IsFreeTarget(enemy)) continue;

            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = enemy;
            }
        }
        return best;
    }

    private bool IsValidGroupTarget(Enemy enemy)
    {
        return enemy != null && enemy != captive && !enemy.IsBubbled
            && (GroupCenter(enemy) - Center).sqrMagnitude <= BubbleGun.GroupSearchRadius * BubbleGun.GroupSearchRadius;
    }

    /// <summary>
    /// Der Gegner im Umkreis von <see cref="BubbleGun.GroupSearchRadius"/>, um
    /// den herum (im Explosionsradius) die meisten anderen stehen. Bei
    /// Gleichstand der naechste. Null, wenn niemand in der Naehe ist - dann
    /// schwebt die Blase auf der Stelle, bis sie platzt.
    /// </summary>
    private Enemy FindGroupTarget(Vector2 from)
    {
        float searchSqr = BubbleGun.GroupSearchRadius * BubbleGun.GroupSearchRadius;
        // Nachbarn bis knapp ueber den Explosionsradius hinaus zaehlen.
        float groupRadius = blastRadius + BubbleGun.BlastRadius * 0.5f;
        float searchAroundSqr = (BubbleGun.GroupSearchRadius + groupRadius) * (BubbleGun.GroupSearchRadius + groupRadius);
        float groupSqr = groupRadius * groupRadius;

        List<Enemy> near = nearScratch;
        near.Clear();
        IReadOnlyList<Enemy> alive = Enemy.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            Enemy enemy = alive[i];
            if (enemy == null || enemy == captive || enemy.IsBubbled) continue;
            if ((GroupCenter(enemy) - from).sqrMagnitude <= searchAroundSqr) near.Add(enemy);
        }

        Enemy best = null;
        int bestCount = -1;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < near.Count; i++)
        {
            Vector2 c = GroupCenter(near[i]);
            float sqr = (c - from).sqrMagnitude;
            if (sqr > searchSqr) continue;

            int count = 0;
            for (int j = 0; j < near.Count; j++)
            {
                if ((GroupCenter(near[j]) - c).sqrMagnitude <= groupSqr) count++;
            }

            if (count > bestCount || (count == bestCount && sqr < bestSqr))
            {
                best = near[i];
                bestCount = count;
                bestSqr = sqr;
            }
        }
        return best;
    }

    // ------------------------------------------------------------------
    //  Hilfen
    // ------------------------------------------------------------------

    /// <summary>Mitte der Blase.</summary>
    private Vector2 Center => captive != null ? carryPos + centerOffset : (Vector2)transform.position;

    private static Vector2 GroupCenter(Enemy enemy) => BodyCenter(enemy);

    private static Vector2 BodyCenter(Enemy enemy)
    {
        Collider2D col = enemy.GetComponent<Collider2D>();
        return col != null ? (Vector2)col.bounds.center : (Vector2)enemy.transform.position;
    }

    /// <summary>Durchmesser, damit der Gegner ganz hineinpasst - mindestens ein Tile.</summary>
    private static float SizeAround(Enemy enemy)
    {
        SpriteRenderer enemySr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (enemySr == null || enemySr.sprite == null) return 1f;
        Vector3 size = enemySr.bounds.size;
        return Mathf.Max(1f, Mathf.Max(size.x, size.y) + 0.3f);
    }

    private void SetSize(float diameter)
    {
        // Das Bild ist genau ein Tile gross.
        transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    // ------------------------------------------------------------------
    //  Bild
    // ------------------------------------------------------------------

    private static Sprite bubbleSprite;

    /// <summary>
    /// Platzhalter-Blase, bis es ein gezeichnetes Sprite gibt: 32x32 bei
    /// 32 PPU, also genau ein Tile. Hellblauer, fast durchsichtiger Bauch,
    /// kraeftigerer Rand, weisser Glanzpunkt oben links.
    /// </summary>
    public static Sprite BubbleSprite
    {
        get
        {
            if (bubbleSprite == null) bubbleSprite = BuildBubble(32);
            return bubbleSprite;
        }
    }

    private static Sprite BuildBubble(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 rim = new Color32(150, 215, 255, 235);
        Color32 rimShade = new Color32(205, 150, 255, 200);
        Color32 body = new Color32(185, 230, 255, 60);
        Color32 shine = new Color32(255, 255, 255, 240);

        float c = n * 0.5f;
        float outer = c - 0.5f;
        Color32[] px = new Color32[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f - c;
                float fy = y + 0.5f - c;
                float r = Mathf.Sqrt(fx * fx + fy * fy);

                Color32 col = clear;
                if (r <= outer)
                {
                    if (outer - r < 1.6f)
                    {
                        // Rand unten rechts ins Violette - Seifenblasen-Schimmer.
                        col = (fx - fy) > n * 0.25f ? rimShade : rim;
                    }
                    else
                    {
                        col = body;
                    }

                    // Glanzpunkt oben links.
                    float sx = fx + n * 0.2f;
                    float sy = fy - n * 0.2f;
                    if (sx * sx + sy * sy <= (n * 0.09f) * (n * 0.09f)) col = shine;
                }

                px[y * n + x] = col;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
