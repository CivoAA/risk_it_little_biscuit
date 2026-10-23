using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der kaiserliche Frostring: eine Welle aus Eiskristallen, die vom Eiskaiser
/// nach aussen rast - mit wenigen Luecken. Wer in einer Luecke steht, wenn die
/// Welle vorbeizieht, kommt durch. Alle anderen friert sie ein
/// (<see cref="PlayerIceBlock"/>) und es gibt Schaden.
///
/// Lesbarkeit:
///   - schon waehrend der Kaiser laedt, zeigen hellblaue Bahnen, wo die
///     Luecken liegen werden (blau = sicher), alles andere ist rot (hier tut
///     es weh). Hinter der Front verschwindet das Rot - dort kommt nichts mehr.
///   - die Kristalle brechen reihenweise aus dem Boden, so sieht man die
///     Front kommen
///
/// Die Luecken sind ueberall gleich breit (Bogenlaenge, nicht Winkel), sonst
/// waeren sie aussen riesig und innen nicht zu treffen.
/// </summary>
public class EiskaiserFrostRing : MonoBehaviour
{
    // ------------------------------------------------------------ Aussehen

    private const float RowSpacing = 0.7f;        // alle so viel Radius eine Kristallreihe
    private const float CrystalSpacing = 0.62f;   // Abstand der Kristalle auf der Reihe
    private const int MaxPerRow = 110;
    // Kurz: die Welle soll eine schmale Front sein (2-3 Reihen), kein Teppich
    private const float RiseTime = 0.08f;         // Bilder 0-3
    private const float StandTime = 0.10f;        // Bilder 4-6
    private const float SinkTime = 0.08f;         // 3..0 rueckwaerts
    private const float BandBehind = 0.55f;       // so weit hinter der Front trifft es noch
    private const float BandAhead = 0.2f;

    private static readonly Color LaneColor = new Color(0.62f, 0.9f, 1f, 0.3f);
    // Dasselbe Rot wie die Warnflaechen der anderen Boss-Attacken (BossTelegraph)
    private static readonly Color DangerColor = new Color(1f, 0.16f, 0.10f, 0.26f);
    private const float DangerFadeIn = 0.25f;
    private const int DangerPixelsPerUnit = 8;
    private const int DangerOrder = -4;           // unter den Bahnen (-3); nur diese Ebene wird maskiert
    private const float StartRadius = 1.8f;       // erst ausserhalb seines Koerpers
    private static Sprite whiteSprite;
    private static Sprite maskDisc;
    /// <summary>Rote Flaechen je Lueckenform - gedreht wird per Transform, gebaut nur einmal.</summary>
    private static readonly Dictionary<string, Sprite> dangerSprites = new Dictionary<string, Sprite>();

    // ------------------------------------------------------------ Zustand

    private Vector2 center;
    private float speed, maxRadius, gapLength;
    private float[] gapAngles;
    private Sprite[] crystal;
    private Sprite[] iceBlock;
    private float damage, freezeSeconds;

    private float radius;
    private float nextRow;
    private bool hitDone;
    private bool running;

    private readonly List<GameObject> lanes = new List<GameObject>();
    private SpriteRenderer danger;
    private SpriteMask passed;
    private float shownAt;

    /// <summary>Losgelassen und die Front ist ganz draussen - von diesem Ring kommt nichts mehr.</summary>
    public bool Done => running && radius > maxRadius;
    private readonly List<Crystal> live = new List<Crystal>();
    private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

    private struct Crystal
    {
        public SpriteRenderer Sr;
        public float Born;
    }

    // ------------------------------------------------------------ Aufbau

    /// <summary>
    /// Legt den Ring an und zeigt sofort die sicheren Bahnen. Losgelassen wird
    /// er mit <see cref="Release"/> (beim Stampfer).
    /// </summary>
    public static EiskaiserFrostRing Prepare(Vector2 center, float[] gapAngles, float gapLength, float maxRadius,
                                             float speed, Sprite[] crystal, Sprite[] iceBlock,
                                             float damage, float freezeSeconds)
    {
        var go = new GameObject("Frostring");
        RunScene.Place(go, "Effekte");
        go.transform.position = center;
        var ring = go.AddComponent<EiskaiserFrostRing>();
        ring.center = center;
        ring.gapAngles = gapAngles;
        ring.gapLength = gapLength;
        ring.maxRadius = maxRadius;
        ring.speed = speed;
        ring.crystal = crystal;
        ring.iceBlock = iceBlock;
        ring.damage = damage;
        ring.freezeSeconds = freezeSeconds;
        ring.ShowLanes();
        ring.ShowDanger();
        return ring;
    }

    public void Release()
    {
        running = true;
        radius = StartRadius;
        nextRow = radius;
    }

    /// <summary>Boss weg, bevor er stampfen konnte: Bahnen still wegnehmen.</summary>
    public void Abort()
    {
        if (!running) Destroy(gameObject);
    }

    private void ShowLanes()
    {
        if (whiteSprite == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        }

        foreach (float a in gapAngles)
        {
            var lane = new GameObject("SichereBahn");
            lane.transform.SetParent(transform, false);
            lane.transform.position = center;
            lane.transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            lane.transform.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * StartRadius;
            lane.transform.localScale = new Vector3(maxRadius - StartRadius, gapLength * 0.55f, 1f);
            var sr = lane.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = LaneColor;
            sr.sortingLayerName = "Objects";
            sr.sortingOrder = -3;
            lanes.Add(lane);
        }
    }

    /// <summary>
    /// Rot: alles, wo der Ring trifft. Eine Maske waechst mit der Front mit und
    /// nimmt das Rot dahinter weg.
    /// </summary>
    private void ShowDanger()
    {
        var go = new GameObject("Gefahrenzone");
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, 0f, gapAngles[0] * Mathf.Rad2Deg);
        danger = go.AddComponent<SpriteRenderer>();
        danger.sprite = DangerSprite(gapAngles, gapLength, maxRadius);
        danger.sortingLayerName = "Objects";
        danger.sortingOrder = DangerOrder;
        danger.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        danger.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0f);
        shownAt = Time.time;

        var maskGo = new GameObject("Front");
        maskGo.transform.SetParent(transform, false);
        maskGo.transform.position = center;
        maskGo.transform.localScale = Vector3.zero;
        passed = maskGo.AddComponent<SpriteMask>();
        passed.sprite = MaskDisc();
        // Nur die rote Flaeche maskieren, sonst nichts in der Szene
        int layer = SortingLayer.NameToID("Objects");
        passed.isCustomRangeActive = true;
        passed.frontSortingLayerID = layer;
        passed.frontSortingOrder = DangerOrder;
        passed.backSortingLayerID = layer;
        passed.backSortingOrder = DangerOrder - 1;
    }

    /// <summary>
    /// Scheibe bis maxRadius, Luecken ausgespart - dieselben Luecken wie bei den
    /// Kristallen (InGap). Die erste Luecke liegt bei 0 Grad, die
    /// echte Lage kommt ueber die Drehung.
    /// </summary>
    private static Sprite DangerSprite(float[] gaps, float gapLength, float maxRadius)
    {
        var rel = new float[gaps.Length];
        var key = new System.Text.StringBuilder();
        key.Append(gapLength.ToString("F2")).Append('|').Append(maxRadius.ToString("F2"));
        for (int i = 0; i < gaps.Length; i++)
        {
            rel[i] = Mathf.Repeat(gaps[i] - gaps[0], Mathf.PI * 2f);
            key.Append('|').Append(rel[i].ToString("F3"));
        }
        if (dangerSprites.TryGetValue(key.ToString(), out Sprite cached) && cached != null) return cached;

        int size = Mathf.CeilToInt(maxRadius * 2f * DangerPixelsPerUnit) + 2;
        float mid = size * 0.5f;
        var pixels = new Color32[size * size];
        var on = new Color32(255, 255, 255, 255);
        var off = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - mid) / DangerPixelsPerUnit;
                float dy = (y + 0.5f - mid) / DangerPixelsPerUnit;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                bool hit = r <= maxRadius && !InGap(rel, gapLength, Mathf.Atan2(dy, dx), r);
                pixels[y * size + x] = hit ? on : off;
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), DangerPixelsPerUnit);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        dangerSprites[key.ToString()] = sprite;
        return sprite;
    }

    /// <summary>Scheibe mit Radius 1 bei Scale 1 - fuer die Maske hinter der Front.</summary>
    private static Sprite MaskDisc()
    {
        if (maskDisc != null) return maskDisc;
        const int size = 64;
        const float mid = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - mid, dy = y + 0.5f - mid;
                pixels[y * size + x] = dx * dx + dy * dy <= mid * mid ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        maskDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), mid);
        maskDisc.hideFlags = HideFlags.HideAndDontSave;
        return maskDisc;
    }

    // ------------------------------------------------------------ Ablauf

    private void Update()
    {
        float now = Time.time;
        if (running)
        {
            radius += speed * Time.deltaTime;
            while (radius >= nextRow && nextRow <= maxRadius)
            {
                SpawnRow(nextRow, now);
                nextRow += RowSpacing;
            }
            CheckPlayers();

            // Bahnen blassen aus, sobald die Front durch ist
            float k = Mathf.Clamp01(radius / maxRadius);
            foreach (GameObject lane in lanes)
            {
                if (lane == null) continue;
                var sr = lane.GetComponent<SpriteRenderer>();
                Color c = LaneColor;
                c.a *= 1f - k;
                sr.color = c;
            }

            // Was die Front hinter sich gelassen hat, trifft nicht mehr
            if (passed != null)
            {
                float behind = Mathf.Max(0f, radius - BandBehind);
                passed.transform.localScale = new Vector3(behind, behind, 1f);
            }
        }

        if (danger != null)
        {
            Color d = DangerColor;
            d.a *= Mathf.Clamp01((now - shownAt) / DangerFadeIn);
            danger.color = d;
        }

        AnimateCrystals(now);

        if (running && radius > maxRadius + 1f && live.Count == 0) Destroy(gameObject);
    }

    private void SpawnRow(float r, float now)
    {
        int count = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r / CrystalSpacing), 6, MaxPerRow);
        float step = 2f * Mathf.PI / count;
        float jitter = Random.value * step;
        for (int i = 0; i < count; i++)
        {
            float a = jitter + i * step;
            if (InGap(a, r)) continue;

            SpriteRenderer sr = Take();
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            sr.transform.position = new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, 0f);
            sr.flipX = (i & 1) == 0;
            sr.sprite = crystal[0];
            live.Add(new Crystal { Sr = sr, Born = now + Random.value * 0.04f });
        }
    }

    private void AnimateCrystals(float now)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Crystal c = live[i];
            float t = now - c.Born;
            int f;
            if (t < 0f) f = 0;
            else if (t < RiseTime) f = Mathf.Min(3, Mathf.FloorToInt(t / RiseTime * 4f));
            else if (t < RiseTime + StandTime) f = 4 + Mathf.Min(2, Mathf.FloorToInt((t - RiseTime) / StandTime * 3f));
            else if (t < RiseTime + StandTime + SinkTime) f = 3 - Mathf.Min(3, Mathf.FloorToInt((t - RiseTime - StandTime) / SinkTime * 4f));
            else
            {
                Give(c.Sr);
                live.RemoveAt(i);
                continue;
            }
            c.Sr.sprite = crystal[Mathf.Clamp(f, 0, crystal.Length - 1)];
        }
    }

    private bool InGap(float angle, float r) => InGap(gapAngles, gapLength, angle, r);

    private static bool InGap(float[] gapAngles, float gapLength, float angle, float r)
    {
        float half = gapLength * 0.5f / Mathf.Max(r, 0.5f);
        foreach (float g in gapAngles)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, g * Mathf.Rad2Deg)) * Mathf.Deg2Rad <= half) return true;
        }
        return false;
    }

    // Koop: jeder Spieler wird von einem Ring hoechstens einmal erwischt.
    private readonly BossTargets.HitSet ringHits = new BossTargets.HitSet();

    private void CheckPlayers()
    {
        foreach (NetPlayers.Target target in BossTargets.All())
        {
            if (ringHits.Contains(target.Slot)) continue;
            if (target.IsLocal && hitDone) continue;
            CheckPlayer(target);
        }
    }

    private void CheckPlayer(NetPlayers.Target target)
    {
        PlayerController player = target.Local;

        Vector2 d = target.Position - center;
        float dist = d.magnitude;
        // Direkt am Kaiser ist kein sicherer Platz: die Welle bricht dort beim
        // Stampfer schon aus (die Kristalle sind nur ausserhalb seines Koerpers gezeichnet).
        float low = radius < StartRadius + BandBehind + 0.3f ? 0f : radius - BandBehind;
        if (dist > radius + BandAhead || dist < low) return;
        // Ausserhalb der roten Scheibe trifft nichts - die Front laeuft dort
        // nur noch unsichtbar aus, bis die letzten Kristalle weg sind.
        if (dist > maxRadius) return;

        // Schon im Eisblock (vom ersten Ring): nicht noch einmal - sonst ist der
        // zweite Ring ein sicherer Treffer, gegen den man nichts tun kann.
        if (player != null && (player.IsFrozen || PlayerIceBlock.Active)) return;

        // Luecke etwas grosszuegiger als gezeichnet: der Keks hat einen Koerper
        float a = Mathf.Atan2(d.y, d.x);
        if (InGap(a, Mathf.Max(0.5f, dist - 0.35f))) return;

        ringHits.Add(target.Slot);
        if (player == null)
        {
            // Mitspieler: Schaden und Eis schickt NetPlayers ihm zu.
            NetPlayers.Hurt(target, damage);
            NetPlayers.Freeze(target, freezeSeconds);
            return;
        }

        hitDone = true;
        player.TakeDamage(damage);
        if (player.gameObject.activeSelf) PlayerIceBlock.Apply(player, iceBlock, freezeSeconds);
    }

    // ------------------------------------------------------------ Pool

    private SpriteRenderer Take()
    {
        if (pool.Count > 0)
        {
            SpriteRenderer sr = pool.Pop();
            sr.gameObject.SetActive(true);
            return sr;
        }
        var go = new GameObject("Kristall");
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sortingLayerName = "Objects";
        r.sortingOrder = 0;
        r.spriteSortPoint = SpriteSortPoint.Pivot;
        return r;
    }

    private void Give(SpriteRenderer sr)
    {
        sr.gameObject.SetActive(false);
        pool.Push(sr);
    }
}
