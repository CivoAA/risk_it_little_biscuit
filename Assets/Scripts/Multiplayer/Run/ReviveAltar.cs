using UnityEngine;

/// <summary>
/// Aussehen des Kreuz-Altars (<see cref="NetRevive"/>): ein Holzkreuz auf
/// einem Steinsockel, daran die Seele des Toten (seine Figur, blass und
/// durchscheinend, sacht wippend), drumherum der grosse Kreis, der sich mit
/// der Rettung fuellt. Alles per Code gemalt, PPU 32. Die Mechanik steht in
/// <see cref="NetRevive"/> - hier wird nur gezeigt.
///
/// Nach der Rettung fliegt die Seele nach oben weg, der Kreis verschwindet,
/// das Kreuz bleibt leer stehen. Geht der Spieler ganz, verblasst alles.
/// </summary>
public class ReviveAltar : MonoBehaviour
{
    private const float PPU = 32f;
    private const string SortingLayer = "Objects";
    private const float SoulHeight = 1.35f;     // Mitte der Seele ueber dem Fuss des Kreuzes (Querbalken)
    private const float SoulSize = 1.1f;        // so hoch wird die Figur gezeichnet
    private const float SoulBob = 0.07f;
    private const float ReleaseTime = 0.9f;

    private static readonly Color SoulTint = new Color(0.7f, 0.85f, 1f, 0.6f);   // wie der Koop-Geist (NetRun)

    private SpriteRenderer cross, soul, field;
    private FieldRing ring;
    private Sprite[] soulFrames;
    private float progress;
    private float soulOffset;   // gleicht den Pivot der Figur aus, damit ihre Mitte am Querbalken haengt
    private bool active;
    private float releasedAt = -1f;
    private bool rescued;

    public static ReviveAltar Create(Vector2 position, float radius, int character)
    {
        var go = new GameObject("ReviveAltar");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        ReviveAltar altar = go.AddComponent<ReviveAltar>();
        altar.Build(radius, character);
        RunScene.Place(go, "Mixer");
        return altar;
    }

    private void Build(float radius, int character)
    {
        field = NewPart("Field", Vector3.zero, -2);
        ring = new FieldRing(field, radius, 5)
        {
            Empty = new Color32(0x22, 0x2a, 0x3c, 150),
            Full = new Color32(0x6f, 0xb7, 0xe0, 235),     // GameHudSkin.Icing
            Hot = new Color32(0xb4, 0xe1, 0xf7, 255),      // GameHudSkin.IcingLight
            Inner = new Color32(0x4a, 0x86, 0xb8, 22),     // GameHudSkin.IcingDark, hauchduenn
        };
        ring.Paint(0f, true);

        cross = NewPart("Cross", Vector3.zero, 0);
        cross.sprite = CrossSprite();

        soulFrames = CharacterLooks.PortraitFor(character);
        soul = NewPart("Soul", Vector3.zero, 1);
        soul.color = SoulTint;
        if (soulFrames != null && soulFrames.Length > 0)
        {
            Sprite first = soulFrames[0];
            soul.sprite = first;
            float hgt = first.bounds.size.y;
            float scale = hgt > 0.0001f ? SoulSize / hgt : 1f;
            soul.transform.localScale = new Vector3(scale, scale, 1f);
            soulOffset = -first.bounds.center.y * scale;
        }
        soul.transform.localPosition = new Vector3(0f, SoulHeight + soulOffset, 0f);
    }

    private SpriteRenderer NewPart(string name, Vector3 local, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sortingLayerName = SortingLayer;
        r.sortingOrder = order;
        return r;
    }

    private void OnDestroy() => ring?.Destroy();

    /// <summary>Fortschritt 0..1, und ob gerade jemand im Kreis steht.</summary>
    public void SetProgress(float value, bool someoneInside)
    {
        progress = Mathf.Clamp01(value);
        active = someoneInside;
    }

    /// <summary>Kreuz wird frei: gerettet (Seele fliegt auf) oder verlassen (verblasst).</summary>
    public void Release(bool wasRescued)
    {
        if (releasedAt >= 0f) return;
        releasedAt = Time.unscaledTime;
        rescued = wasRescued;
        if (wasRescued) progress = 1f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        if (releasedAt >= 0f)
        {
            float t = Mathf.Clamp01((now - releasedAt) / ReleaseTime);
            field.enabled = false;
            // Die Seele steigt auf und loest sich in Stufen auf.
            soul.transform.localPosition = new Vector3(0f, SoulHeight + soulOffset + (rescued ? t * 1.5f : 0f), 0f);
            Color c = SoulTint;
            c.a *= Mathf.Round((1f - t) * 4f) / 4f;
            soul.color = c;
            if (!rescued) cross.color = new Color(1f, 1f, 1f, Mathf.Round((1f - t) * 4f) / 4f);
            else cross.color = Color.Lerp(Color.white, new Color(0.6f, 0.58f, 0.6f), t);
            if (t >= 1f)
            {
                if (rescued) enabled = false;
                else Destroy(gameObject);
            }
            return;
        }

        ring.Paint(progress);

        if (soulFrames != null && soulFrames.Length > 1)
            soul.sprite = soulFrames[Mathf.FloorToInt(now * 4f) % soulFrames.Length];

        // Sacht wippen; steht jemand im Kreis, leuchtet die Seele auf.
        float bob = Mathf.Sin(now * 2f) * SoulBob;
        soul.transform.localPosition = new Vector3(0f, SoulHeight + soulOffset + bob, 0f);
        Color tint = SoulTint;
        if (active) tint.a = 0.6f + 0.3f * (Mathf.Repeat(now * 2f, 1f) < 0.5f ? 1f : 0f);
        soul.color = tint;
    }

    // ==================================================================
    //  Bild: Holzkreuz auf Steinsockel
    // ==================================================================

    private static Sprite crossSprite;

    private static Sprite CrossSprite()
    {
        if (crossSprite != null) return crossSprite;

        const int w = 44, h = 64;
        var px = new Color32[w * h];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ink = GameHudSkin.Ink;
        Color32 wood = GameHudSkin.Wood, woodLight = GameHudSkin.WoodLight, woodDark = GameHudSkin.WoodDark;
        Color32 stone = GameHudSkin.Stone, stoneLight = GameHudSkin.StoneLight, stoneDark = GameHudSkin.StoneDark;

        void Fill(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    px[y * w + x] = c;
        }

        // Sockel (y = 0 unten): breite Platte, darauf ein schmalerer Block.
        Fill(2, 1, 41, 5, stone);
        Fill(2, 5, 41, 5, stoneLight);
        Fill(2, 1, 41, 1, stoneDark);
        Fill(7, 6, 36, 10, stone);
        Fill(7, 10, 36, 10, stoneLight);
        Fill(7, 6, 36, 6, stoneDark);

        // Laengsbalken und Querbalken, links Licht, rechts Schatten.
        Fill(18, 11, 25, 61, wood);
        Fill(18, 11, 18, 61, woodLight);
        Fill(25, 11, 25, 61, woodDark);
        Fill(5, 40, 38, 47, wood);
        Fill(5, 47, 38, 47, woodLight);
        Fill(5, 40, 38, 40, woodDark);
        // Maserung
        for (int y = 15; y < 60; y += 7) px[y * w + 21] = woodDark;
        for (int x = 9; x < 37; x += 8) px[43 * w + x] = woodDark;
        // Strick um die Kreuzung
        Fill(17, 41, 26, 41, GameHudSkin.ParchDark);
        Fill(17, 46, 26, 46, GameHudSkin.ParchDark);

        // 1-px-Kontur um alles, was steht.
        var outlined = (Color32[])px.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a != 0) continue;
                bool edge = (x > 0 && px[y * w + x - 1].a != 0) || (x < w - 1 && px[y * w + x + 1].a != 0)
                         || (y > 0 && px[(y - 1) * w + x].a != 0) || (y < h - 1 && px[(y + 1) * w + x].a != 0);
                outlined[y * w + x] = edge ? ink : clear;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ReviveCross",
        };
        tex.SetPixels32(outlined);
        tex.Apply(false);
        crossSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PPU);
        return crossSprite;
    }
}

/// <summary>
/// Pfeil mit Totenkopf ueber dem eigenen Spieler, der zum naechsten
/// Kreuz-Altar zeigt, solange der nicht im Bild ist - wie
/// <see cref="BossPointer"/>, nur blau mit Totenkopf.
/// </summary>
public class AltarPointer : MonoBehaviour
{
    private const int PPU = 32;
    private const float Radius = 0.75f;
    private const float SkullGap = 0.42f;
    private const float HeadHeight = 1.1f;
    private const float Bob = 0.08f;
    private const float FadeSpeed = 6f;
    private const float ShowOutside = 0.08f;
    private const float HideInside = 0.06f;

    private SpriteRenderer arrow, skull;
    private float alpha;
    private bool showing;

    public static AltarPointer Create(Transform parent)
    {
        var go = new GameObject("AltarPointer");
        go.transform.SetParent(parent, false);
        return go.AddComponent<AltarPointer>();
    }

    private void Awake()
    {
        arrow = NewRenderer("Arrow", BuildArrow());
        skull = NewRenderer("Skull", BuildSkull());
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingLayerName = "Foreground";
        r.sortingOrder = 500;
        r.enabled = false;
        return r;
    }

    /// <summary>Jeden Frame von <see cref="NetRevive"/>: wer schaut, wohin (null = nichts zeigen).</summary>
    public void Track(PlayerController player, Vector2? target)
    {
        Camera cam = Camera.main;
        if (player == null || target == null || cam == null) showing = false;
        else if (showing) showing = !InView(cam, target.Value, HideInside);
        else showing = !InView(cam, target.Value, -ShowOutside);

        alpha = Mathf.MoveTowards(alpha, showing ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
        if (alpha <= 0f || player == null || target == null)
        {
            arrow.enabled = skull.enabled = false;
            return;
        }

        Vector2 head = (Vector2)player.transform.position + Vector2.up * HeadHeight;
        Vector2 dir = target.Value - head;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        float bob = (Mathf.Sin(Time.unscaledTime * 8f) * 0.5f + 0.5f) * Bob;
        Vector2 pos = head + dir * (Radius + bob);
        arrow.transform.position = new Vector3(pos.x, pos.y, 0f);
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        // Der Totenkopf sitzt hinter dem Pfeil (zur Figur hin) und dreht sich nicht mit.
        Vector2 sp = pos - dir * SkullGap;
        skull.transform.position = new Vector3(sp.x, sp.y, 0f);

        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
        arrow.color = skull.color = new Color(1f, 1f, 1f, alpha * pulse);
        arrow.enabled = skull.enabled = true;
    }

    private static bool InView(Camera cam, Vector3 world, float margin)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
    }

    // ---------- Bilder ----------

    // Pfeil zeigt nach rechts (+x); gedreht wird per Transform.
    private static readonly string[] ArrowShape =
    {
        "....##.....",
        "....#o#....",
        "....#oo#...",
        "#####ooo#..",
        "#lllllooo#.",
        "#oooooooo#.",
        "#####ooo#..",
        "....#oo#...",
        "....#o#....",
        "....##.....",
    };

    private static readonly string[] SkullShape =
    {
        "..#####..",
        ".#lllll#.",
        "#lllllll#",
        "#l##l##l#",
        "#l##l##l#",
        "#lllolll#",
        ".#lllll#.",
        "..#l#l#..",
        "...###...",
    };

    private static Sprite BuildArrow() => Paint(ArrowShape, "AltarArrow", new Vector2(5f, 5f),
        new Color32(0x6f, 0xb7, 0xe0, 0xFF), new Color32(0xb4, 0xe1, 0xf7, 0xFF));

    private static Sprite BuildSkull() => Paint(SkullShape, "AltarSkull", new Vector2(4f, 4f),
        new Color32(0x3b, 0x2b, 0x33, 0xFF), new Color32(0xff, 0xf4, 0xe0, 0xFF));

    /// <param name="fill">Farbe fuer 'o'.</param>
    /// <param name="light">Farbe fuer 'l'.</param>
    private static Sprite Paint(string[] shape, string name, Vector2 pivotPx, Color32 fill, Color32 light)
    {
        int h = shape.Length, w = shape[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        var px = new Color32[w * h];
        Color32 outline = new Color32(0x2A, 0x10, 0x18, 0xFF);
        for (int y = 0; y < h; y++)
        {
            string row = shape[h - 1 - y];
            for (int x = 0; x < w; x++)
            {
                switch (row[x])
                {
                    case '#': px[y * w + x] = outline; break;
                    case 'o': px[y * w + x] = fill; break;
                    case 'l': px[y * w + x] = light; break;
                    default: px[y * w + x] = new Color32(0, 0, 0, 0); break;
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotPx.x / w, pivotPx.y / h), PPU);
    }
}
