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
///     Luecken liegen werden (blau = sicher, rot bleibt "hier tut es weh")
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
    private const float StartRadius = 1.8f;       // erst ausserhalb seines Koerpers
    private static Sprite whiteSprite;

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
            if (!hitDone) CheckPlayer();

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

    private bool InGap(float angle, float r)
    {
        float half = gapLength * 0.5f / Mathf.Max(r, 0.5f);
        foreach (float g in gapAngles)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, g * Mathf.Rad2Deg)) * Mathf.Deg2Rad <= half) return true;
        }
        return false;
    }

    private void CheckPlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.gameObject.activeSelf) return;

        Vector2 d = (Vector2)player.transform.position - center;
        float dist = d.magnitude;
        // Direkt am Kaiser ist kein sicherer Platz: die Welle bricht dort beim
        // Stampfer schon aus (die Kristalle sind nur ausserhalb seines Koerpers gezeichnet).
        float low = radius < StartRadius + BandBehind + 0.3f ? 0f : radius - BandBehind;
        if (dist > radius + BandAhead || dist < low) return;

        // Schon im Eisblock (vom ersten Ring): nicht noch einmal - sonst ist der
        // zweite Ring ein sicherer Treffer, gegen den man nichts tun kann.
        if (player.IsFrozen || PlayerIceBlock.Active) return;

        // Luecke etwas grosszuegiger als gezeichnet: der Keks hat einen Koerper
        float a = Mathf.Atan2(d.y, d.x);
        if (InGap(a, Mathf.Max(0.5f, dist - 0.35f))) return;

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
