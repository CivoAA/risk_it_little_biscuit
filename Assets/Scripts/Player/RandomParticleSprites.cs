using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Die Kruemel, die beim Treffer aus dem Spieler brechen. Frueher gab es ein
/// handgemaltes Sprite-Set pro Skin - und jeder neue Charakter bekam die des
/// alten Kekses. Jetzt werden sie aus dem Sprite geschnitten, das der Spieler
/// gerade zeigt: Farben und Pixelgroesse stimmen fuer jeden Charakter von
/// allein, auch fuer kuenftige.
///
/// PlayerHitFeedback ruft <see cref="BuildFrom"/> vor jedem Abspielen auf;
/// gebaut wird nur, wenn sich der Charakter geaendert hat.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class RandomParticleSprites : MonoBehaviour
{
    private const int CrumbCount = 16;
    private const int Columns = 4;

    private ParticleSystem ps;
    private int builtForSkin = int.MinValue;
    private Texture2D atlas;
    private readonly List<Sprite> crumbs = new();

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    public void BuildFrom(SpriteRenderer source)
    {
        if (source == null || source.sprite == null) return;

        int skin = PlayerSkinSwitcher.Instance != null ? PlayerSkinSwitcher.Instance.skinIndex : 0;
        if (skin == builtForSkin) return;

        Color32[] pixels = ReadSprite(source.sprite, out int w, out int h);
        if (pixels == null) return;

        var opaque = new List<int>();
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].a > 128) opaque.Add(i);
        if (opaque.Count == 0) return;

        // Zelle so gross, dass ein Kruemelpixel so gross ist wie ein Charakterpixel.
        float pixelWorld = Mathf.Abs(source.transform.lossyScale.x) / source.sprite.pixelsPerUnit;
        float quadWorld = ps.main.startSize.constant * Mathf.Abs(transform.localScale.x);
        int cell = Mathf.Clamp(Mathf.RoundToInt(quadWorld / pixelWorld), 4, 24);

        int rows = Mathf.CeilToInt(CrumbCount / (float)Columns);
        var atlasPixels = new Color32[Columns * cell * rows * cell];

        for (int c = 0; c < CrumbCount; c++)
        {
            int ox = (c % Columns) * cell, oy = (c / Columns) * cell;
            int start = opaque[Random.Range(0, opaque.Count)];
            CutCrumb(pixels, w, h, start % w, start / w, atlasPixels, Columns * cell, ox, oy, cell);
        }

        if (atlas != null) Destroy(atlas);
        foreach (var s in crumbs) Destroy(s);
        crumbs.Clear();

        atlas = new Texture2D(Columns * cell, rows * cell, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "HitCrumbs",
        };
        atlas.SetPixels32(atlasPixels);
        atlas.Apply(false, true);

        for (int c = 0; c < CrumbCount; c++)
        {
            var rect = new Rect((c % Columns) * cell, (c / Columns) * cell, cell, cell);
            crumbs.Add(Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), cell));
        }

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        for (int i = sheet.spriteCount - 1; i >= 0; i--)
            sheet.RemoveSprite(i);
        foreach (var s in crumbs)
            sheet.AddSprite(s);
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0, crumbs.Count);
        sheet.animation = ParticleSystemAnimationType.WholeSheet;

        builtForSkin = skin;
    }

    /// <summary>
    /// Ein zackiger Brocken: von einem deckenden Pixel aus zufaellig wachsen,
    /// Pixel samt Farbe in die Zelle uebernehmen. Die unterste/rechte Kante
    /// wird abgedunkelt, damit er wie die alten Kruemel etwas Tiefe hat.
    /// </summary>
    private static void CutCrumb(Color32[] src, int w, int h, int sx, int sy,
                                 Color32[] dst, int dstW, int ox, int oy, int cell)
    {
        int maxSize = Mathf.Max(3, cell * cell / 4);
        int size = Random.Range(maxSize / 2, maxSize + 1);
        int half = cell / 2;

        var taken = new HashSet<Vector2Int>();
        var frontier = new List<Vector2Int> { Vector2Int.zero };
        while (frontier.Count > 0 && taken.Count < size)
        {
            int pick = Random.Range(0, frontier.Count);
            Vector2Int d = frontier[pick];
            frontier.RemoveAt(pick);
            if (taken.Contains(d)) continue;

            int x = sx + d.x, y = sy + d.y;
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            if (d.x + half < 1 || d.y + half < 1 || d.x + half >= cell - 1 || d.y + half >= cell - 1) continue;
            if (src[y * w + x].a <= 128) continue;

            taken.Add(d);
            frontier.Add(d + Vector2Int.right);
            frontier.Add(d + Vector2Int.left);
            frontier.Add(d + Vector2Int.up);
            frontier.Add(d + Vector2Int.down);
        }

        foreach (var d in taken)
        {
            Color32 col = src[(sy + d.y) * w + (sx + d.x)];
            bool edge = !taken.Contains(d + Vector2Int.down) || !taken.Contains(d + Vector2Int.right);
            if (edge && taken.Count > 3)
                col = new Color32((byte)(col.r * 0.6f), (byte)(col.g * 0.6f), (byte)(col.b * 0.6f), col.a);
            col.a = 255;
            dst[(oy + d.y + half) * dstW + (ox + d.x + half)] = col;
        }
    }

    /// <summary>
    /// Pixel des Sprites, auch wenn die Textur nicht lesbar importiert ist
    /// (Umweg ueber eine RenderTexture).
    /// </summary>
    private static Color32[] ReadSprite(Sprite sprite, out int w, out int h)
    {
        Rect r = sprite.textureRect;
        w = Mathf.RoundToInt(r.width);
        h = Mathf.RoundToInt(r.height);
        int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y);
        Texture2D tex = sprite.texture;
        if (tex == null || w <= 0 || h <= 0) return null;

        if (tex.isReadable)
        {
            try
            {
                Color[] c = tex.GetPixels(x, y, w, h);
                var result = new Color32[c.Length];
                for (int i = 0; i < c.Length; i++) result[i] = c[i];
                return result;
            }
            catch (UnityException) { /* z.B. komprimiert - unten weiter */ }
        }

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;

        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(x, y, w, h), 0, 0);
        copy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = copy.GetPixels32();
        Destroy(copy);
        return pixels;
    }
}
